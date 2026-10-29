#requires -Version 5.1
<#
Eight offline safety checks against the actual packaged standalone updater.
Tests stage new files only; never launches an EXE or modifies a live launcher.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ZipPath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$original=(Resolve-Path -LiteralPath $ZipPath).Path
$validator=Join-Path $PSScriptRoot 'Assert-LauncherSelfUpdatePackage.ps1'
$root=Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-selfstage-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($root)|Out-Null
$checks=0
function Digest([string]$path){
 return (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
}
function Stage([string]$zip,[string]$sha,[string]$name){
 $dest=Join-Path $root $name
 & $validator -ZipPath $zip -ExpectedSha256 $sha -StagingDirectory $dest | Out-Null
 return $dest
}
function Reject([string]$zip,[string]$sha,[string]$name){
 $dest=Join-Path $root $name
 $failed=$false
 try{ $null=Stage $zip $sha $name }catch{$failed=$true}
 if(-not $failed){throw ('Unsafe launcher package accepted: '+$name)}
 if(Test-Path -LiteralPath $dest){throw ('Rejected package created files: '+$name)}
 $script:checks++
}
function Mutate([string]$kind){
 $result=Join-Path $root ($kind+'.zip')
 $src=[IO.Compression.ZipFile]::OpenRead($original)
 $dst=[IO.Compression.ZipFile]::Open($result,[IO.Compression.ZipArchiveMode]::Create)
 try{
  foreach($entry in $src.Entries){
   if($kind -eq 'missing' -and $entry.FullName -ceq 'Binaries/Launcher-ServiceCockpit.ps1'){
    continue
   }
   $name=$entry.FullName
   if($kind -eq 'traversal' -and $name -ceq 'Binaries/Launcher-ServiceCockpit.ps1'){
    $name='../escape.ps1'
   }
   $new=$dst.CreateEntry($name,[IO.Compression.CompressionLevel]::Optimal)
   $from=$entry.Open();$to=$new.Open()
   try{
    if($kind -eq 'badpe' -and $name -ceq 'Launcher.exe'){
     $mem=New-Object IO.MemoryStream
     try{
      $from.CopyTo($mem)
      $bytes=$mem.ToArray()
      $bytes[0]=0
      $to.Write($bytes,0,$bytes.Length)
     }finally{$mem.Dispose()}
    }else{$from.CopyTo($to)}
   }finally{$to.Dispose();$from.Dispose()}
  }
  if($kind -in @('duplicate','extra')){
   $name=if($kind -eq 'duplicate'){'LAUNCHER.EXE'}else{'Binaries/unknown-helper.ps1'}
   $entry=$dst.CreateEntry($name)
   $stream=$entry.Open()
   try{
    $bytes=[Text.Encoding]::ASCII.GetBytes('test')
    $stream.Write($bytes,0,$bytes.Length)
   }finally{$stream.Dispose()}
  }
 }finally{$dst.Dispose();$src.Dispose()}
 return $result
}
try{
 # 1: real distribution can be staged into a previously absent directory.
 $staged=Stage $original (Digest $original) 'valid'
 if(-not(Test-Path -LiteralPath (Join-Path $staged 'Launcher.exe') -PathType Leaf) -or
    -not(Test-Path -LiteralPath (Join-Path $staged 'Binaries/Updater-Banner.ps1') -PathType Leaf)){
  throw 'Valid launcher package was not staged correctly.'
 }
 $checks++
 # 2: caller-supplied digest must match the exact archive.
 Reject $original ('0'*64) 'wrong-hash'
 # 3: modified bytes must not pass against the original release digest.
 $modified=Mutate 'badpe'
 Reject $modified (Digest $original) 'changed-after-hash'
 # 4: case-folding duplicate (Windows filesystem collision).
 $duplicate=Mutate 'duplicate'
 Reject $duplicate (Digest $duplicate) 'duplicate-entry'
 # 5: traversal and alternate names.
 $traversal=Mutate 'traversal'
 Reject $traversal (Digest $traversal) 'traversal-entry'
 # 6: unexpected executable/script is never extracted.
 $extra=Mutate 'extra'
 Reject $extra (Digest $extra) 'unlisted-entry'
 # 7: incomplete bundle.
 $missing=Mutate 'missing'
 Reject $missing (Digest $missing) 'missing-entry'
 # 8: a correct archive digest is not enough for a non-x86 PE.
 Reject $modified (Digest $modified) 'bad-executable'
 if($checks -ne 8){throw 'Wrong launcher stage test count.'}
 Write-Host 'Launcher self-update staging: 8/8 offline security checks passed.'
}finally{
 if(Test-Path -LiteralPath $root){
  Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
 }
}
