#requires -Version 5.1
<#
Inspect the real PNG-backed ICO created for the native Launcher.exe.
Eight offline gates. Exports only three pixel previews for manual review.
No Windows UI, installer, updater, network or vehicle access.
#>
[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$IconPath,
 [Parameter(Mandatory=$true)][string]$PreviewDirectory
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing

$icon=(Resolve-Path -LiteralPath $IconPath -ErrorAction Stop).Path
if(-not(Test-Path -LiteralPath $icon -PathType Leaf)){throw 'ICO is not a regular file.'}
$bytes=[IO.File]::ReadAllBytes($icon)
$checks=0
if($bytes.Length -lt 120 -or $bytes.Length -gt 2MB){throw 'ICO is empty or exceeds pixel-audit size budget.'}
$stream=[IO.MemoryStream]::new($bytes,$false)
$reader=[IO.BinaryReader]::new($stream)
$records=@()
try{
 # Gate 1: legitimate ICO header, exactly seven images.
 if($reader.ReadUInt16() -ne 0 -or $reader.ReadUInt16() -ne 1 -or
    $reader.ReadUInt16() -ne 7){throw 'Launcher ICO header/count mismatch.'}
 $checks++

 # Gate 2: each size and 32bpp, ordered from smallest to largest.
 $expected=@(16,24,32,48,64,128,256)
 for($i=0;$i -lt $expected.Count;$i++){
  $w=[int]$reader.ReadByte();if($w -eq 0){$w=256}
  $h=[int]$reader.ReadByte();if($h -eq 0){$h=256}
  $palette=$reader.ReadByte();$reserved=$reader.ReadByte()
  $planes=$reader.ReadUInt16();$depth=$reader.ReadUInt16()
  $length=[long]$reader.ReadUInt32();$offset=[long]$reader.ReadUInt32()
  if($w -ne $expected[$i] -or $h -ne $w -or
     $palette -ne 0 -or $reserved -ne 0 -or
     $planes -ne 1 -or $depth -ne 32){
   throw ('Unexpected ICO size/bit-depth entry at '+$i)
  }
  $records+=@{Size=$w;Length=$length;Offset=$offset}
 }
 $checks++

 # Gate 3: PNG streams must not overlap each other or the directory,
 # leave the file, or use empty/malformed lengths.
 $minStart=6+16*$expected.Count
 $priorEnd=$minStart
 foreach($item in $records){
  $start=$item.Offset;$end=$start+$item.Length
  if($start -lt $priorEnd -or $end -gt $bytes.Length -or
     $item.Length -lt 33){
   throw ('Overlapping or truncated ICO frame at '+$item.Size)
  }
  $priorEnd=$end
 }
 $checks++

 # Gate 4: all images are PNG streams, not DIBs or renamed bitmaps.
 $pngSig=[byte[]]@(137,80,78,71,13,10,26,10)
 foreach($item in $records){
  $start=[int]$item.Offset
  for($p=0;$p -lt $pngSig.Length;$p++){
   if($bytes[$start+$p] -ne $pngSig[$p]){
    throw ('Non-PNG ICO frame '+$item.Size)
   }
  }
  if([Text.Encoding]::ASCII.GetString($bytes,$start+12,4) -cne 'IHDR'){
   throw ('Missing PNG IHDR in frame '+$item.Size)
  }
  $bigW=0;$bigH=0
  for($p=0;$p -lt 4;$p++){
   $bigW=($bigW -shl 8) -bor [int]$bytes[$start+16+$p]
   $bigH=($bigH -shl 8) -bor [int]$bytes[$start+20+$p]
  }
  if($bigW -ne $item.Size -or $bigH -ne $item.Size){
   throw ('PNG frame dimensions differ from ICO directory: '+$item.Size)
  }
 }
 $checks++

 # Gate 5: GDI+ really decodes all seven frames on Windows PowerShell 5.1.
 foreach($item in $records){
  $data=[byte[]]::new([int]$item.Length)
  [Array]::Copy($bytes,[int]$item.Offset,$data,0,$data.Length)
  $pngStream=[IO.MemoryStream]::new($data,$false)
  try{
   $bitmap=[Drawing.Bitmap]::new($pngStream)
   try{
    if($bitmap.Width -ne $item.Size -or $bitmap.Height -ne $item.Size){
     throw ('GDI+ rejected ICO frame dimensions '+$item.Size)
    }
   }finally{$bitmap.Dispose()}
  }finally{$pngStream.Dispose()}
 }
 $checks++

 $previewSizes=@(16,32,256)
 $previews=@{}
 foreach($size in $previewSizes){
  $item=@($records|Where-Object{$_.Size -eq $size})[0]
  $data=[byte[]]::new([int]$item.Length)
  [Array]::Copy($bytes,[int]$item.Offset,$data,0,$data.Length)
  $pngStream=[IO.MemoryStream]::new($data,$false)
  try{
   $bitmap=[Drawing.Bitmap]::new($pngStream)
   try{
    $bright=0;$dark=0
    $colors=[Collections.Generic.HashSet[int]]::new()
    $visible=0
    for($y=0;$y -lt $bitmap.Height;$y++){
     for($x=0;$x -lt $bitmap.Width;$x++){
      $pixel=$bitmap.GetPixel($x,$y)
      if($pixel.A -lt 180){continue}
      $visible++
      $sum=[int]$pixel.R+[int]$pixel.G+[int]$pixel.B
      if($sum -ge 520){$bright++}
      if($sum -le 270){$dark++}
      [void]$colors.Add($pixel.ToArgb())
     }
    }
    $previews[$size]=@{Bright=$bright;Dark=$dark;Visible=$visible;Colors=$colors.Count}
   }finally{$bitmap.Dispose()}
  }finally{$pngStream.Dispose()}
 }
 # Gate 6: 16px glyph has both light metal and dark negative space.
 if($previews[16].Bright -lt 2 -or $previews[16].Dark -lt 2 -or
    $previews[16].Visible -lt 80){throw '16px launcher glyph has insufficient visible metal/dark contrast.'}
 $checks++
 # Gate 7: 32px and 256px preserve the same essential contrast/detail.
 if($previews[32].Bright -lt 5 -or $previews[32].Dark -lt 5 -or
    $previews[32].Colors -lt 15 -or
    $previews[256].Bright -lt 64 -or $previews[256].Dark -lt 64 -or
    $previews[256].Colors -lt 50){
  throw '32px or 256px metal/gull/waves are visually blank or flat.'
 }
 $checks++

 # Gate 8: export unchanged PNG bytes from the actual ICO, not a separately
 # drawn mock, for approval on Windows at the real displayed resolutions.
 $target=[IO.Path]::GetFullPath($PreviewDirectory)
 [IO.Directory]::CreateDirectory($target)|Out-Null
 foreach($size in $previewSizes){
  $item=@($records|Where-Object{$_.Size -eq $size})[0]
  $data=[byte[]]::new([int]$item.Length)
  [Array]::Copy($bytes,[int]$item.Offset,$data,0,$data.Length)
  $output=Join-Path $target ('Launcher-'+$size+'.png')
  [IO.File]::WriteAllBytes($output,$data)
  if((Get-Item -LiteralPath $output).Length -ne $item.Length){
   throw 'Exported PNG does not match the true embedded ICO frame.'
  }
 }
 $checks++
 if($checks -ne 8){throw 'Wrong ICO pixel-audit check count.'}
 Write-Host 'Northern Launcher ICO frames 8/8 offline checks passed.'
 foreach($size in $previewSizes){
  Write-Host ("{0}px: visible={1}; bright={2}; dark={3}; unique-color={4}" -f
   $size,$previews[$size].Visible,$previews[$size].Bright,
   $previews[$size].Dark,$previews[$size].Colors)
 }
}finally{
 $reader.Dispose();$stream.Dispose()
}
