#requires -Version 5.1
<#
Standalone Windows 11 updater. Only inbox PowerShell 5.1 and .NET Framework
WinForms are needed; .NET 10, Windows App SDK and VC x86 are NOT needed to run.
No software installs or updates until the user explicitly confirms.
#>
[CmdletBinding()]
param(
 [ValidateSet('UI','UiSmokeTest','Check','Install','Rollback','Repair','RepairInstall','Launch','Probe','SelfTest')][string]$Mode='UI',
 [ValidateSet('Stable','Development')][string]$Channel='Development',
 [string]$StateFile
)
$ErrorActionPreference='Stop'
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
$repo='vorlocsev/dvinholm'
$zipName='Dvinholm-Windows-x86-Lite.zip'
$sumsName='SHA256SUMS.txt'
$manager=Join-Path $PSScriptRoot 'Manage-Lite-Dependencies.ps1'
$base=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Dvinholm\UpdaterBanner'
$preferenceFile=Join-Path $base 'channel-preference.txt'
$channelRoot=Join-Path $base $Channel
$current=Join-Path $channelRoot 'current'
$state=[ordered]@{Done=$false;Failed=$false;Current='Not installed';Available='Not checked';
 DotNet='Unknown';WinApp='Unknown';VC='Unknown';Recovery='Not checked';Detail='Ready'}
function Save-State {
 if (-not $StateFile) { return }
 $tmp=$StateFile+'.'+[guid]::NewGuid().ToString('N')+'.tmp'
 [IO.File]::WriteAllText($tmp,($script:state|ConvertTo-Json -Compress),
  [Text.UTF8Encoding]::new($false))
 Move-Item -LiteralPath $tmp -Destination $StateFile -Force
}
function Progress([string]$text){$script:state.Detail=$text;Save-State}
function Hash([string]$path){
 $s=[IO.File]::OpenRead($path);$h=[Security.Cryptography.SHA256]::Create()
 try{return ([BitConverter]::ToString($h.ComputeHash($s))-replace '-','').ToLowerInvariant()}
 finally{$s.Dispose();$h.Dispose()}
}
function Display-Version([string]$tag) {
 # GitHub release API keeps v0.2.4 as the canonical Stable tag; the GUI
 # shows 0.2.4 only. Development CI tags must not be transformed.
 if($Channel -ceq 'Stable' -and $tag -cmatch '^v(\d+\.\d+\.\d+)$'){
  return $Matches[1]
 }
 return $tag
}
function Installed {
 # Do not report arbitrary metadata as installed. Validate ordinary Lite
 # files, selected channel, matching tag and Windows x86 executable header.
 if(-not(Test-Path -LiteralPath $current)){return 'Not installed'}
 try {return (Display-Version (Read-ManagedVersion $current))}
 catch {return 'Unverified installation'}
}
function Validate-Asset($a,[string]$tag,[string]$filename){
 if($null -eq $a -or $a.name -cne $filename -or
  [string]$a.digest -cnotmatch '^sha256:[0-9a-f]{64}$' -or
  [long]$a.size -lt 1 -or [long]$a.size -gt 160MB -or
  [string]$a.browser_download_url -cne
  ('https://github.com/'+$repo+'/releases/download/'+$tag+'/'+$filename)){
  throw 'Untrusted release asset metadata/digest.'
 }
}
function Latest {
 $header=@{'User-Agent'='Dvinholm-UpdaterBanner/1.0';'Accept'='application/vnd.github+json'}
 if($Channel -eq 'Stable'){
  $r=Invoke-RestMethod -Uri ('https://api.github.com/repos/'+$repo+'/releases/latest') -Headers $header -TimeoutSec 30
  if($r.draft -or $r.prerelease -or [string]$r.tag_name -cnotmatch '^v\d+\.\d+\.\d+$'){
   throw 'Untrusted Stable release.'
  }
 }else{
  $all=@(Invoke-RestMethod -Uri ('https://api.github.com/repos/'+$repo+'/releases?per_page=40') -Headers $header -TimeoutSec 30)
  $eligible=@($all|Where-Object {
   $_.draft -eq $false -and $_.prerelease -eq $true -and
   [string]$_.tag_name -cmatch '^ci-[1-9][0-9]{0,8}$' -and
   [string]$_.target_commitish -cmatch '^[0-9a-f]{40}$'
  }|Sort-Object @{Expression={[long](([string]$_.tag_name).Substring(3))};Descending=$true})
  $r=$null
  foreach($candidate in $eligible){
   if(@($candidate.assets|Where-Object{$_.name -ceq $zipName}).Count -eq 1 -and
      @($candidate.assets|Where-Object{$_.name -ceq $sumsName}).Count -eq 1){
    $r=$candidate;break
   }
  }
  if($null -eq $r){throw 'No public Development Lite release found.'}
 }
 $zip=@($r.assets|Where-Object{$_.name -ceq $zipName})
 $sums=@($r.assets|Where-Object{$_.name -ceq $sumsName})
 if($zip.Count -ne 1 -or $sums.Count -ne 1){throw 'Missing unique Lite ZIP or sums.'}
 Validate-Asset $zip[0] $r.tag_name $zipName
 Validate-Asset $sums[0] $r.tag_name $sumsName
 return [pscustomobject]@{Tag=[string]$r.tag_name;Zip=$zip[0];Sums=$sums[0]}
}
function Entry-Path([string]$name){
 $p=$name.Replace('\','/').TrimEnd('/')
 if(-not $p -or $p.StartsWith('/') -or $p.Contains(':') -or
  $p -match '[\x00-\x1f<>|?*]'){throw 'Unsafe ZIP path.'}
 foreach($part in ($p -split '/')){
  if(-not $part -or $part -in @('.','..') -or $part.EndsWith('.') -or
    $part.EndsWith(' ') -or
    $part -match '^(?i:CON|AUX|PRN|NUL|COM[1-9]|LPT[1-9])(\..*)?$'){
    throw 'Unsafe ZIP path segment.'
  }
 }
 return $p
}
function Unpack([string]$archive,[string]$target){
 Add-Type -AssemblyName System.IO.Compression
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 $z=[IO.Compression.ZipFile]::OpenRead($archive)
 $prefix=[IO.Path]::GetFullPath($target).TrimEnd('\')+'\'
 $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
 $total=[long]0
 try{
  if($z.Entries.Count -lt 1 -or $z.Entries.Count -gt 1500){throw 'Archive entry budget exceeded.'}
  foreach($e in $z.Entries){
   $name=Entry-Path ([string]$e.FullName)
   if(-not $seen.Add($name)){throw 'Duplicate ZIP entry.'}
   if((($e.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000 -or
      ($e.ExternalAttributes -band 0x400) -ne 0){throw 'ZIP links forbidden.'}
   $total+=[long]$e.Length
   if($e.Length -gt 128MB -or $total -gt 750MB){throw 'ZIP size budget exceeded.'}
   $file=[IO.Path]::GetFullPath((Join-Path $target $name.Replace('/','\')))
   if(-not $file.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){
    throw 'ZIP path escaped staging.'
   }
   if($e.FullName.EndsWith('/')){[IO.Directory]::CreateDirectory($file)|Out-Null;continue}
   [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($file))|Out-Null
   $src=$e.Open();$dst=[IO.File]::Create($file)
   try{$src.CopyTo($dst)}finally{$dst.Dispose();$src.Dispose()}
   if(([IO.FileInfo]$file).Length -ne $e.Length){throw 'ZIP expanded size mismatch.'}
  }
 }finally{$z.Dispose()}
 foreach($name in @('Dvinholm.App.exe','Dvinholm.App.runtimeconfig.json',
   'Check-Lite-Prerequisites.ps1','Start-Dvinholm-Lite.cmd','Manage-Lite-Dependencies.ps1')){
  if(-not(Test-Path (Join-Path $target $name) -PathType Leaf)){
   throw 'Lite archive missing required files.'
  }
 }
 $exe=[IO.File]::ReadAllBytes((Join-Path $target 'Dvinholm.App.exe'))
 if($exe.Length -lt 512 -or $exe[0] -ne 77 -or $exe[1] -ne 90){throw 'Invalid x86 app PE.'}
 $pe=[BitConverter]::ToInt32($exe,60)
 if($pe -lt 64 -or $pe+6 -ge $exe.Length -or
    [BitConverter]::ToUInt32($exe,$pe) -ne 0x4550 -or
    [BitConverter]::ToUInt16($exe,$pe+4) -ne 0x14c){throw 'Invalid x86 app PE.'}
 if(Test-Path (Join-Path $target 'coreclr.dll')){throw 'Not a clean Lite archive.'}
}
function Assert-ManagedFolder([string]$path){
 if(Test-Path $path){
  if(((Get-Item -LiteralPath $path -Force).Attributes -band
    [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Managed folder is a reparse point.'}
 }
}
function Read-ChannelPreference {
 try {
  if(-not(Test-Path -LiteralPath $preferenceFile -PathType Leaf)){return $null}
  $item=Get-Item -LiteralPath $preferenceFile -Force
  if($item.Length -gt 32 -or
    ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){return $null}
  $saved=[IO.File]::ReadAllText($preferenceFile,[Text.Encoding]::ASCII).Trim()
  if($saved -cin @('Development','Stable')){return $saved}
 }catch{}
 return $null
}
function Save-ChannelPreference([string]$selected) {
 if($selected -cnotin @('Development','Stable')){return $false}
 $temp=$null
 try {
  Assert-ManagedFolder (Split-Path -Parent $base)
  Assert-ManagedFolder $base
  [IO.Directory]::CreateDirectory($base)|Out-Null
  if(Test-Path -LiteralPath $preferenceFile) {
   $item=Get-Item -LiteralPath $preferenceFile -Force
   if(($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
    return $false
   }
  }
  $temp=Join-Path $base ('channel-preference-'+[guid]::NewGuid().ToString('N')+'.tmp')
  [IO.File]::WriteAllText($temp,$selected,[Text.Encoding]::ASCII)
  Move-Item -LiteralPath $temp -Destination $preferenceFile -Force
  return $true
 }catch{return $false}
 finally {
  if($temp -and (Test-Path -LiteralPath $temp)){
   Remove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue
  }
 }
}
function Read-ManagedVersion([string]$folder) {
 Assert-ManagedFolder $base
 Assert-ManagedFolder $channelRoot
 Assert-ManagedFolder $folder
 if(-not(Test-Path -LiteralPath $folder -PathType Container)){
  throw 'No valid managed version in selected channel.'
 }
 $mandatory=@('Dvinholm.App.exe','Dvinholm.App.runtimeconfig.json',
  'Check-Lite-Prerequisites.ps1','Start-Dvinholm-Lite.cmd',
  'Manage-Lite-Dependencies.ps1','updater-banner.json')
 foreach($name in $mandatory) {
  $path=Join-Path $folder $name
  if(-not(Test-Path -LiteralPath $path -PathType Leaf) -or
     ((Get-Item -LiteralPath $path -Force).Attributes -band
       [IO.FileAttributes]::ReparsePoint) -ne 0) {
   throw 'Managed version is missing an ordinary Lite file.'
  }
 }
 if(Test-Path -LiteralPath (Join-Path $folder 'coreclr.dll')){
  throw 'Managed version is not Lite.'
 }
 try {
  $metadata=Get-Content -LiteralPath (Join-Path $folder 'updater-banner.json') -Raw |
    ConvertFrom-Json
 }catch{throw 'Managed version metadata is unreadable.'}
 $pattern=if($Channel -ceq 'Stable'){'^v\d+\.\d+\.\d+$'}else{'^ci-[1-9][0-9]{0,8}$'}
 if([string]$metadata.Channel -cne $Channel -or
    [string]$metadata.Tag -cnotmatch $pattern) {
  throw 'Managed version has an incompatible channel or tag.'
 }
 $file=Join-Path $folder 'Dvinholm.App.exe'
 $exe=[IO.File]::ReadAllBytes($file)
 if($exe.Length -lt 512 -or $exe[0] -ne 77 -or $exe[1] -ne 90){
  throw 'Managed Lite executable has an invalid x86 header.'
 }
 $pe=[BitConverter]::ToInt32($exe,60)
 if($pe -lt 64 -or $pe+6 -ge $exe.Length -or
    [BitConverter]::ToUInt32($exe,$pe) -ne 0x4550 -or
    [BitConverter]::ToUInt16($exe,$pe+4) -ne 0x14c) {
  throw 'Managed Lite executable has an invalid x86 header.'
 }
 return [string]$metadata.Tag
}
function Invoke-ChannelExclusive([scriptblock]$action) {
 # OS-level per-session mutex prevents workers changing this channel at once.
 $mutex=[Threading.Mutex]::new($false,('Local\Dvinholm.UpdaterBanner.'+$Channel))
 $locked=$false
 try {
  try{$locked=$mutex.WaitOne(0)}
  catch [Threading.AbandonedMutexException] {$locked=$true}
  if(-not $locked){throw 'Another updater operation is already running.'}
  & $action
 }finally{
  if($locked){$mutex.ReleaseMutex()}
  $mutex.Dispose()
 }
}

function Get-RollbackStaging {
 Assert-ManagedFolder $base
 Assert-ManagedFolder $channelRoot
 if(-not(Test-Path -LiteralPath $channelRoot -PathType Container)){return @()}
 return @(Get-ChildItem -LiteralPath $channelRoot -Force -ErrorAction Stop |
  Where-Object { $_.Name -like 'rollback-*' })
}
function Assert-NoInterruptedRollback {
 if(@(Get-RollbackStaging).Count -ne 0){
  throw 'Interrupted rollback requires explicit repair before other updates.'
 }
}
function Repair-InterruptedRollback {
 if(Get-Process -Name 'Dvinholm.App' -ErrorAction SilentlyContinue){
  throw 'Close the application before repair.'
 }
 $staging=@(Get-RollbackStaging)
 if($staging.Count -ne 1){throw 'Repair requires exactly one interrupted rollback directory.'}
 $entry=$staging[0]
 if($entry.Name -cnotmatch '^rollback-[0-9a-f]{32}$' -or
    -not $entry.PSIsContainer -or
    ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
  throw 'Untrusted rollback staging directory.'
 }
 $swap=[string]$entry.FullName
 $swapTag=Read-ManagedVersion $swap
 $previous=Join-Path $channelRoot 'previous'
 Assert-ManagedFolder $current
 Assert-ManagedFolder $previous
 $hasCurrent=Test-Path -LiteralPath $current -PathType Container
 $hasPrevious=Test-Path -LiteralPath $previous -PathType Container
 if($hasCurrent -eq $hasPrevious){
  throw 'Ambiguous interrupted rollback state; no files changed.'
 }
 $other=if($hasCurrent){$current}else{$previous}
 $otherTag=Read-ManagedVersion $other
 if($otherTag -ceq $swapTag){
  throw 'Interrupted rollback versions are identical; no files changed.'
 }
 Progress ('Restoring original '+$swapTag+' from interrupted rollback...')
 if($hasCurrent){
  # On interruption after previous->current, the second rename may
  # leave current absent. Rerunning Repair then follows the other branch.
  Move-Item -LiteralPath $current -Destination $previous -ErrorAction Stop
 }
 Move-Item -LiteralPath $swap -Destination $current -ErrorAction Stop
 $script:state.Current=Display-Version $swapTag
 $script:state.Available='Not checked'
 Progress ('Recovered '+$swapTag+'; '+$otherTag+' retained as the previous version.')
}

function Get-InstallTransactionFile {
 return (Join-Path $channelRoot 'install-transaction.json')
}
function Get-OptionalManagedVersion([string]$folder) {
 if(-not(Test-Path -LiteralPath $folder)){return ''}
 return (Read-ManagedVersion $folder)
}
function Get-InstallMarkerEntries {
 if(-not(Test-Path -LiteralPath $channelRoot -PathType Container)){return @()}
 Assert-ManagedFolder $base
 Assert-ManagedFolder $channelRoot
 return @(Get-ChildItem -LiteralPath $channelRoot -Force -ErrorAction Stop |
  Where-Object { $_.Name -like 'pending-*' -or $_.Name -like 'older-*' })
}
function Get-ChannelRecoveryState {
 # Read-only classification. Repair actions separately verify every file.
 try {
  Assert-ManagedFolder $base
  Assert-ManagedFolder $channelRoot
  $rollbacks=@(Get-RollbackStaging)
  $installs=@(Get-InstallMarkerEntries)
  $journal=Test-Path -LiteralPath (Get-InstallTransactionFile)
  if($rollbacks.Count -gt 0){
   if($journal -or $installs.Count -gt 0 -or $rollbacks.Count -ne 1){
    return 'Inspect'
   }
   $one=$rollbacks[0]
   if($one.Name -cmatch '^rollback-[0-9a-f]{32}$' -and
      $one.PSIsContainer -and
      ($one.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0){
    return 'Repair rollback'
   }
   return 'Inspect'
  }
  if($journal){return 'Repair install'}
  if($installs.Count -gt 0){return 'Inspect'}
  return 'Ready'
 }catch{return 'Inspect'}
}
function Assert-NoInterruptedInstall {
 if((Test-Path -LiteralPath (Get-InstallTransactionFile)) -or
    @(Get-InstallMarkerEntries).Count -gt 0){
  throw 'Interrupted install requires explicit repair before changing managed versions.'
 }
}
function Write-InstallTransaction([string]$id,[string]$targetTag) {
 $journal=Get-InstallTransactionFile
 if(Test-Path -LiteralPath $journal){throw 'Existing install journal prevents promotion.'}
 $staged=Join-Path $channelRoot ('pending-'+$id)
 if((Get-OptionalManagedVersion $staged) -cne $targetTag){
  throw 'Prepared Lite version does not match intended install.'
 }
 $markers=@(Get-InstallMarkerEntries)
 if($markers.Count -ne 1 -or $markers[0].Name -cne ('pending-'+$id)){
  throw 'Unexpected install staging marker before promotion.'
 }
 $previous=Join-Path $channelRoot 'previous'
 $oldCurrent=Get-OptionalManagedVersion $current
 $oldPrevious=Get-OptionalManagedVersion $previous
 if($oldPrevious -and -not $oldCurrent){
  throw 'Managed backup exists without current; cannot start installation.'
 }
 $metadata=@{
  Channel=$Channel
  Id=$id
  TargetTag=$targetTag
  HadCurrent=[bool]$oldCurrent
  HadPrevious=[bool]$oldPrevious
  OriginalCurrentTag=[string]$oldCurrent
  OriginalPreviousTag=[string]$oldPrevious
 }
 $tmp=$journal+'.'+[guid]::NewGuid().ToString('N')+'.tmp'
 try {
  [IO.File]::WriteAllText($tmp,($metadata|ConvertTo-Json -Compress),
   [Text.UTF8Encoding]::new($false))
  Move-Item -LiteralPath $tmp -Destination $journal -ErrorAction Stop
 }finally {
  if(Test-Path -LiteralPath $tmp){Remove-Item -LiteralPath $tmp -Force}
 }
}
function Repair-InterruptedInstall {
 if(Get-Process -Name 'Dvinholm.App' -ErrorAction SilentlyContinue){
  throw 'Close the application before repair.'
 }
 Assert-NoInterruptedRollback
 $journal=Get-InstallTransactionFile
 if(-not(Test-Path -LiteralPath $journal -PathType Leaf)){
  throw 'No verifiable install transaction. Preserve orphan staging for manual inspection.'
 }
 Assert-ManagedFolder $base
 Assert-ManagedFolder $channelRoot
 $item=Get-Item -LiteralPath $journal -Force
 if($item.Length -gt 4096 -or $item.Length -lt 10 -or
    ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
  throw 'Untrusted install journal.'
 }
 try {$m=Get-Content -LiteralPath $journal -Raw | ConvertFrom-Json}
 catch {throw 'Unreadable install journal.'}
 $id=[string]$m.Id
 $targetTag=[string]$m.TargetTag
 $origCur=[string]$m.OriginalCurrentTag
 $origPrev=[string]$m.OriginalPreviousTag
 $tagPattern=if($Channel -ceq 'Stable'){'^v\d+\.\d+\.\d+$'}else{'^ci-[1-9][0-9]{0,8}$'}
 if([string]$m.Channel -cne $Channel -or
    $id -cnotmatch '^[0-9a-f]{32}$' -or
    $targetTag -cnotmatch $tagPattern -or
    $m.HadCurrent -isnot [bool] -or $m.HadPrevious -isnot [bool] -or
    [bool]$m.HadCurrent -ne [bool]$origCur -or
    [bool]$m.HadPrevious -ne [bool]$origPrev -or
    ($origCur -and $origCur -cnotmatch $tagPattern) -or
    ($origPrev -and $origPrev -cnotmatch $tagPattern) -or
    ($m.HadPrevious -and -not $m.HadCurrent)){
  throw 'Untrusted install journal fields.'
 }
 $pending=Join-Path $channelRoot ('pending-'+$id)
 $abandoned=Join-Path $channelRoot ('abandoned-'+$id)
 $older=Join-Path $channelRoot ('older-'+$id)
 $retired=Join-Path $channelRoot ('retired-'+$id)
 $previous=Join-Path $channelRoot 'previous'
 $allowed=@("pending-$id","older-$id")
 foreach($marker in @(Get-InstallMarkerEntries)){
  if($marker.Name -cnotin $allowed){
   throw 'Unexpected install staging marker.'
  }
 }
 foreach($folder in @($current,$previous,$pending,$abandoned,$older,$retired)){
  Assert-ManagedFolder $folder
 }
 $c=Get-OptionalManagedVersion $current
 $p=Get-OptionalManagedVersion $previous
 $n=Get-OptionalManagedVersion $pending
 $q=Get-OptionalManagedVersion $abandoned
 $o=Get-OptionalManagedVersion $older
 $t=Get-OptionalManagedVersion $retired
 if(($n -and $q) -or ($o -and $t) -or
    ($n -and $n -cne $targetTag) -or
    ($q -and $q -cne $targetTag) -or
    ($o -and $o -cne $origPrev) -or
    ($t -and $t -cne $origPrev)){
  throw 'Install staging is inconsistent with transaction journal.'
 }
 $phase=''
 if($c -ceq $targetTag -and $m.HadCurrent -and
    $p -ceq $origCur -and -not $n -and -not $q -and
    (($m.HadPrevious -and (($o -ceq $origPrev -and -not $t) -or
                             ($t -ceq $origPrev -and -not $o))) -or
     (-not $m.HadPrevious -and -not $o -and -not $t))){
  $phase='promoted'
 }elseif($c -ceq $targetTag -and -not $m.HadCurrent -and
         -not $p -and -not $n -and -not $q -and -not $o -and -not $t){
  $phase='promoted'
 }elseif(($n -ceq $targetTag -or $q -ceq $targetTag) -and
         $c -ceq $origCur -and $p -ceq $origPrev -and
         -not $o -and -not $t){
  $phase='prepared'
 }elseif($m.HadPrevious -and ($n -ceq $targetTag -or $q -ceq $targetTag) -and
         $c -ceq $origCur -and -not $p -and $o -ceq $origPrev -and -not $t){
  $phase='previous-moved'
 }elseif($m.HadPrevious -and ($n -ceq $targetTag -or $q -ceq $targetTag) -and
         -not $c -and $p -ceq $origCur -and $o -ceq $origPrev -and -not $t){
  $phase='current-moved'
 }elseif($m.HadCurrent -and -not $m.HadPrevious -and
         ($n -ceq $targetTag -or $q -ceq $targetTag) -and
         -not $c -and $p -ceq $origCur -and -not $o -and -not $t){
  $phase='current-moved'
 }
 if(-not $phase){throw 'Ambiguous interrupted install. Managed files left untouched.'}
 if($phase -eq 'promoted'){
  Progress ('Install '+$targetTag+' already promoted. Finalizing safe backup cleanup...')
  if($o){
   Move-Item -LiteralPath $older -Destination $retired -ErrorAction Stop
  }
 }else{
  Progress ('Restoring original '+$origCur+' from interrupted installation...')
  if($n){
   Move-Item -LiteralPath $pending -Destination $abandoned -ErrorAction Stop
  }
  if($phase -eq 'current-moved'){
   Move-Item -LiteralPath $previous -Destination $current -ErrorAction Stop
  }
  if($phase -eq 'current-moved' -or $phase -eq 'previous-moved'){
   if($m.HadPrevious){
    Move-Item -LiteralPath $older -Destination $previous -ErrorAction Stop
   }
  }
 }
 Remove-Item -LiteralPath $journal -Force -ErrorAction Stop
 if(Test-Path -LiteralPath $retired){
  Remove-Item -LiteralPath $retired -Recurse -Force -ErrorAction SilentlyContinue
 }
 $script:state.Current=Installed
 $script:state.Available='Not checked'
 Progress ('Install recovery complete: '+$script:state.Current+'. Staged files retained as abandoned when present.')
}
function Get-VerifiedLaunchTarget {
 # Called in the channel worker, not in the GUI's default Development context.
 # Refuse any unresolved install/rollback and revalidate the selected channel
 # immediately before launching its dependency-aware Lite command.
 if((Get-ChannelRecoveryState) -cne 'Ready'){
  throw 'Launch blocked: recovery or manual inspection is required.'
 }
 try {$tag=Read-ManagedVersion $current}
 catch {throw 'Launch blocked: no valid managed Lite x86 installation in this channel.'}
 return [pscustomobject]@{
  Tag=$tag
  File=(Join-Path $current 'Start-Dvinholm-Lite.cmd')
 }
}
function Launch {
 $target=Get-VerifiedLaunchTarget
 Start-Process -FilePath $target.File -WorkingDirectory $current -ErrorAction Stop
 Progress ('Opened Lite launcher for '+$Channel+' '+$target.Tag+'.')
}
function Rollback {
 Assert-NoInterruptedInstall
 Assert-NoInterruptedRollback
 if(Get-Process -Name 'Dvinholm.App' -ErrorAction SilentlyContinue) {
  throw 'Close the application before rollback.'
 }
 $previous=Join-Path $channelRoot 'previous'
 $oldTag=Read-ManagedVersion $current
 $restoreTag=Read-ManagedVersion $previous
 if($restoreTag -ceq $oldTag){throw 'Previous and current versions are identical.'}
 $swap=Join-Path $channelRoot ('rollback-'+[guid]::NewGuid().ToString('N'))
 $movedCurrent=$false
 $movedPrevious=$false
 Progress ('Restoring '+$restoreTag+' from the offline channel backup...')
 try{
  Move-Item -LiteralPath $current -Destination $swap -ErrorAction Stop
  $movedCurrent=$true
  Move-Item -LiteralPath $previous -Destination $current -ErrorAction Stop
  $movedPrevious=$true
  Move-Item -LiteralPath $swap -Destination $previous -ErrorAction Stop
  $movedCurrent=$false
 }catch{
  $repairFailed=$false
  if($movedPrevious){
   try{
    Move-Item -LiteralPath $current -Destination $previous -ErrorAction Stop
   }catch{$repairFailed=$true}
  }
  if($movedCurrent -and -not $repairFailed){
   try{
    Move-Item -LiteralPath $swap -Destination $current -ErrorAction Stop
   }catch{$repairFailed=$true}
  }
  if($repairFailed){
   throw 'Rollback interrupted. Managed folders retained; manual recovery required.'
  }
  throw 'Rollback failed; original managed versions restored.'
 }
 $script:state.Current=Display-Version $restoreTag
 $script:state.Available='Not checked'
 Progress ('Restored '+$restoreTag+'; '+$oldTag+' retained as the previous version.')
}
function Probe {
 $a=@{DotNet='Unknown';WinApp='Unknown';VC='Unknown'}
 if(-not(Test-Path $manager -PathType Leaf)){return $a}
 try{
  $v=@(& $manager -Mode Probe -LiteDirectory $PSScriptRoot 2>&1)
  foreach($line in $v){
   if([string]$line -match '^(DOTNET|WINAPP|VC)=(READY|MISSING)$'){
    $key=switch($Matches[1]){'DOTNET'{'DotNet'}'WINAPP'{'WinApp'}default{'VC'}}
    $a[$key]=if($Matches[2] -eq 'READY'){'Installed'}else{'Missing'}
   }
  }
 }catch{}
 return $a
}
function Assert-InstallDependencies($inventory) {
 # The download is pointless if the same Windows host cannot run Lite.
 # Fail closed on an absent manager, partial inventory, or unknown values.
 if($null -eq $inventory -or $inventory -isnot [Collections.IDictionary]){
  throw 'Missing Lite prerequisites. Recheck Microsoft runtimes first.'
 }
 foreach($key in @('DotNet','WinApp','VC')){
  if(-not $inventory.Contains($key) -or
     [string]$inventory[$key] -cne 'Installed'){
   throw ('Missing Lite prerequisites: '+$key+
    '. Install/update Microsoft components, then recheck.')
  }
 }
}
function Install {
 Assert-NoInterruptedInstall
 Assert-NoInterruptedRollback
 if(Get-Process -Name 'Dvinholm.App' -ErrorAction SilentlyContinue){
  throw 'Close the application before update.'
 }
 # No GitHub API, ZIP download or install staging until all three
 # runtime prerequisites are positively identified on this host.
 Assert-InstallDependencies (Probe)
 $r=Latest
 $script:state.Available=Display-Version $r.Tag
 Progress 'Downloading GitHub Lite ZIP and SHA256SUMS...'
 Assert-ManagedFolder $base;Assert-ManagedFolder $channelRoot
 New-Item -ItemType Directory -Path $channelRoot -Force | Out-Null
 Assert-ManagedFolder $current
 $id=[guid]::NewGuid().ToString('N')
 $downloads=Join-Path $channelRoot ('download-'+$id)
 $pending=Join-Path $channelRoot ('pending-'+$id)
 $previous=Join-Path $channelRoot 'previous'
 New-Item -ItemType Directory -Path $downloads | Out-Null
 $movedOld=$false
 try{
  $zip=Join-Path $downloads $zipName;$sums=Join-Path $downloads $sumsName
  Invoke-WebRequest -Uri $r.Zip.browser_download_url -UseBasicParsing -OutFile $zip -TimeoutSec 300
  Invoke-WebRequest -Uri $r.Sums.browser_download_url -UseBasicParsing -OutFile $sums -TimeoutSec 60
  if((Get-Item $zip).Length -ne [long]$r.Zip.size -or
    (Get-Item $sums).Length -ne [long]$r.Sums.size -or
    ('sha256:'+(Hash $zip)) -cne [string]$r.Zip.digest -or
    ('sha256:'+(Hash $sums)) -cne [string]$r.Sums.digest){
   throw 'Downloaded GitHub asset SHA256 mismatch.'
  }
  $matches=@([IO.File]::ReadAllLines($sums)|Where-Object{
   $_ -cmatch ('^[0-9a-f]{64}  '+[regex]::Escape($zipName)+'$')
  })
  if($matches.Count -ne 1 -or $matches[0].Substring(0,64) -cne (Hash $zip)){
   throw 'Release checksum manifest mismatch.'
  }
  Progress 'Validating and extracting Lite archive...'
  New-Item -ItemType Directory -Path $pending|Out-Null
  Unpack $zip $pending
  Progress 'Checking actual Windows Lite dependencies...'
  & (Join-Path $pending 'Check-Lite-Prerequisites.ps1') -LiteDirectory $pending -WindowsAppSdkLine '2.5'|Out-Null
  if(-not $?){throw 'Lite prerequisite validation failed.'}
  if(Get-Process -Name 'Dvinholm.App' -ErrorAction SilentlyContinue){
   throw 'Close the application before update.'
  }
  $meta=@{Channel=$Channel;Tag=$r.Tag;InstalledUtc=[DateTime]::UtcNow.ToString('o')}
  [IO.File]::WriteAllText((Join-Path $pending 'updater-banner.json'),
   ($meta|ConvertTo-Json -Compress),[Text.UTF8Encoding]::new($false))
  Progress 'Replacing the banner-managed application directory...'
  Write-InstallTransaction $id $r.Tag
  Assert-ManagedFolder $previous
  $older=Join-Path $channelRoot ('older-'+$id)
  $movedPrevious=$false
  try {
   if((Test-Path -LiteralPath $current -PathType Container) -and
      (Test-Path -LiteralPath $previous -PathType Container)){
    Move-Item -LiteralPath $previous -Destination $older -ErrorAction Stop
    $movedPrevious=$true
   }
   if(Test-Path -LiteralPath $current -PathType Container) {
    Move-Item -LiteralPath $current -Destination $previous -ErrorAction Stop
    $movedOld=$true
   }
   Move-Item -LiteralPath $pending -Destination $current -ErrorAction Stop
  }catch{
   if($movedOld -and -not(Test-Path -LiteralPath $current)) {
    Move-Item -LiteralPath $previous -Destination $current -ErrorAction Stop
   }
   if($movedPrevious -and -not(Test-Path -LiteralPath $previous)){
    Move-Item -LiteralPath $older -Destination $previous -ErrorAction Stop
   }
   throw
  }
  $retired=Join-Path $channelRoot ('retired-'+$id)
  if($movedPrevious){
   Move-Item -LiteralPath $older -Destination $retired -ErrorAction Stop
  }
  Remove-Item -LiteralPath (Get-InstallTransactionFile) -Force -ErrorAction Stop
  if($movedPrevious){
   Remove-Item -LiteralPath $retired -Recurse -Force -ErrorAction SilentlyContinue
  }
  $script:state.Current=Display-Version $r.Tag
  Progress ('Installed '+$r.Tag+'; previous version retained for rollback.')
 }finally{
  if(Test-Path $downloads){Remove-Item $downloads -Recurse -Force -ErrorAction SilentlyContinue}
  if(-not(Test-Path -LiteralPath (Get-InstallTransactionFile)) -and
     (Test-Path -LiteralPath $pending)){
   Remove-Item -LiteralPath $pending -Recurse -Force -ErrorAction SilentlyContinue
  }
 }
}
function SelfTest {
 $count=0
 function Reject([scriptblock]$action){
  $failed=$false
  try{& $action|Out-Null}catch{$failed=$true}
  if(-not $failed){throw 'Unsafe fixture was accepted.'}
 }
 Entry-Path 'Assets/Dvinholm.ico'|Out-Null;$count++
 Reject {Entry-Path '../escape.dll'};$count++
 Reject {Entry-Path 'C:/absolute.exe'};$count++
 Reject {Entry-Path 'CON.txt'};$count++
 Reject {Entry-Path 'a//b.dll'};$count++
 $good=[pscustomobject]@{name=$zipName;size=10000;digest=('sha256:'+('a'*64));
  browser_download_url=('https://github.com/'+$repo+'/releases/download/ci-4096/'+$zipName)}
 Validate-Asset $good 'ci-4096' $zipName;$count++
 $other=$good|Select-Object *
 $other.browser_download_url='https://untrusted.invalid/file.zip'
 Reject {Validate-Asset $other 'ci-4096' $zipName};$count++
 $other=$good|Select-Object *;$other.digest='sha256:invalid'
 Reject {Validate-Asset $other 'ci-4096' $zipName};$count++
 if($count -ne 8){throw 'Wrong test count.'}
 Write-Host 'Updater Banner 8/8 synthetic offline checks passed.'
 $originalBase=$script:base
 $originalPreferenceFile=$script:preferenceFile
 $testDirectory=Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-banner-test-'+[guid]::NewGuid().ToString('N'))
 try {
  $script:base=$testDirectory
  $script:preferenceFile=Join-Path $testDirectory 'channel-preference.txt'
  if(-not(Save-ChannelPreference 'Stable') -or
    (Read-ChannelPreference) -cne 'Stable'){throw 'Channel preference roundtrip failed.'}
  [IO.File]::WriteAllText($script:preferenceFile,'Stable;untrusted',[Text.Encoding]::ASCII)
  if($null -ne (Read-ChannelPreference)){throw 'Untrusted channel preference accepted.'}
 }finally{
  $script:base=$originalBase
  $script:preferenceFile=$originalPreferenceFile
  if(Test-Path -LiteralPath $testDirectory){
   Remove-Item -LiteralPath $testDirectory -Force -Recurse
  }
 }
 Write-Host 'Updater Banner 2/2 channel preference checks passed.'
 $originalChannelRoot=$script:channelRoot
 $originalCurrent=$script:current
 $originalBase=$script:base
 $originalChannel=$script:Channel
 $originalStateFile=$script:StateFile
 $testDir=Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-rollback-test-'+[guid]::NewGuid().ToString('N'))
 function New-SyntheticLite([string]$folder,[string]$tag,[string]$channel){
  [IO.Directory]::CreateDirectory($folder)|Out-Null
  [byte[]]$pe=New-Object byte[] 512
  $pe[0]=77;$pe[1]=90
  [BitConverter]::GetBytes([int]128).CopyTo($pe,60)
  $pe[128]=80;$pe[129]=69;$pe[132]=76;$pe[133]=1
  [IO.File]::WriteAllBytes((Join-Path $folder 'Dvinholm.App.exe'),$pe)
  foreach($file in @('Dvinholm.App.runtimeconfig.json',
   'Check-Lite-Prerequisites.ps1','Start-Dvinholm-Lite.cmd',
   'Manage-Lite-Dependencies.ps1')){
   [IO.File]::WriteAllText((Join-Path $folder $file),'synthetic')
  }
  [IO.File]::WriteAllText((Join-Path $folder 'updater-banner.json'),
   (@{Channel=$channel;Tag=$tag;InstalledUtc='2026-01-01T00:00:00Z'}|ConvertTo-Json))
 }
 function Reset-SyntheticLite {
  if(Test-Path -LiteralPath $script:channelRoot){
   Remove-Item -LiteralPath $script:channelRoot -Recurse -Force
  }
  New-SyntheticLite $script:current 'ci-200' 'Development'
  New-SyntheticLite (Join-Path $script:channelRoot 'previous') 'ci-199' 'Development'
 }
 $rollbackChecks=0
 try {
  $script:base=$testDir
  $script:Channel='Development'
  $script:channelRoot=Join-Path $testDir 'Development'
  $script:current=Join-Path $script:channelRoot 'current'
  $script:StateFile=$null
  Reset-SyntheticLite
  if((Read-ManagedVersion $script:current) -cne 'ci-200' -or
     (Read-ManagedVersion (Join-Path $script:channelRoot 'previous')) -cne 'ci-199'){
   throw 'Synthetic managed version validation failed.'
  }
  $rollbackChecks++
  Rollback
  if((Read-ManagedVersion $script:current) -cne 'ci-199' -or
     (Read-ManagedVersion (Join-Path $script:channelRoot 'previous')) -cne 'ci-200'){
   throw 'Offline rollback failed to retain the replaced version.'
  }
  $rollbackChecks++
  Rollback
  if((Read-ManagedVersion $script:current) -cne 'ci-200'){throw 'Reverse rollback failed.'}
  $rollbackChecks++
  Reset-SyntheticLite
  Remove-Item -LiteralPath (Join-Path $script:channelRoot 'previous') -Recurse -Force
  Reject {Rollback}
  if((Read-ManagedVersion $script:current) -cne 'ci-200'){throw 'Missing backup changed current.'}
  $rollbackChecks++
  Reset-SyntheticLite
  $backup=Join-Path $script:channelRoot 'previous'
  [IO.File]::WriteAllText((Join-Path $backup 'updater-banner.json'),
   (@{Channel='Stable';Tag='v0.2.4'}|ConvertTo-Json))
  Reject {Rollback}
  if((Read-ManagedVersion $script:current) -cne 'ci-200'){throw 'Cross-channel rollback changed current.'}
  $rollbackChecks++
  Reset-SyntheticLite
  [IO.File]::WriteAllText((Join-Path $backup 'updater-banner.json'),
   (@{Channel='Development';Tag='ci-malformed'}|ConvertTo-Json))
  Reject {Rollback}
  if((Read-ManagedVersion $script:current) -cne 'ci-200'){throw 'Bad tag changed current.'}
  $rollbackChecks++
  Reset-SyntheticLite
  Remove-Item -LiteralPath (Join-Path $backup 'Start-Dvinholm-Lite.cmd') -Force
  Reject {Rollback}
  if((Read-ManagedVersion $script:current) -cne 'ci-200'){throw 'Incomplete backup changed current.'}
  $rollbackChecks++
  Reset-SyntheticLite
  $exe=Join-Path $backup 'Dvinholm.App.exe'
  $bytes=[IO.File]::ReadAllBytes($exe);$bytes[0]=0
  [IO.File]::WriteAllBytes($exe,$bytes)
  Reject {Rollback}
  if((Read-ManagedVersion $script:current) -cne 'ci-200'){throw 'Non-x86 backup changed current.'}
  $rollbackChecks++
  if($rollbackChecks -ne 8){throw 'Wrong rollback test count.'}
  Write-Host 'Updater Banner 8/8 synthetic offline rollback checks passed.'
  $repairChecks=0
  $staged=Join-Path $script:channelRoot 'rollback-0123456789abcdef0123456789abcdef'
  Reset-SyntheticLite
  Move-Item -LiteralPath $script:current -Destination $staged
  Reject {Rollback}
  Reject {Install}
  Repair-InterruptedRollback
  if((Read-ManagedVersion $script:current) -cne 'ci-200' -or
     (Read-ManagedVersion (Join-Path $script:channelRoot 'previous')) -cne 'ci-199' -or
     (Test-Path -LiteralPath $staged)){throw 'Phase-one recovery failed.'}
  $repairChecks++
  Reset-SyntheticLite
  Move-Item -LiteralPath $script:current -Destination $staged
  Move-Item -LiteralPath (Join-Path $script:channelRoot 'previous') -Destination $script:current
  Repair-InterruptedRollback
  if((Read-ManagedVersion $script:current) -cne 'ci-200' -or
     (Read-ManagedVersion (Join-Path $script:channelRoot 'previous')) -cne 'ci-199' -or
     (Test-Path -LiteralPath $staged)){throw 'Phase-two recovery failed.'}
  $repairChecks++
  Reset-SyntheticLite
  Reject {Repair-InterruptedRollback}
  if((Read-ManagedVersion $script:current) -cne 'ci-200'){throw 'No-marker repair changed current.'}
  $repairChecks++
  Reset-SyntheticLite
  Move-Item -LiteralPath $script:current -Destination $staged
  $extra=Join-Path $script:channelRoot 'rollback-fedcba9876543210fedcba9876543210'
  New-SyntheticLite $extra 'ci-201' 'Development'
  Reject {Repair-InterruptedRollback}
  if(-not(Test-Path -LiteralPath $staged) -or -not(Test-Path -LiteralPath $extra)){
   throw 'Multiple-marker repair changed staged folders.'
  }
  $repairChecks++
  Reset-SyntheticLite
  New-SyntheticLite $staged 'ci-201' 'Development'
  Reject {Repair-InterruptedRollback}
  if((Read-ManagedVersion $script:current) -cne 'ci-200' -or
     (Read-ManagedVersion (Join-Path $script:channelRoot 'previous')) -cne 'ci-199'){
   throw 'Ambiguous complete-state repair changed versions.'
  }
  $repairChecks++
  Reset-SyntheticLite
  Move-Item -LiteralPath $script:current -Destination $staged
  Remove-Item -LiteralPath (Join-Path $script:channelRoot 'previous') -Recurse -Force
  Reject {Repair-InterruptedRollback}
  if(-not(Test-Path -LiteralPath $staged)){throw 'Empty-state repair lost staging.'}
  $repairChecks++
  Reset-SyntheticLite
  Move-Item -LiteralPath $script:current -Destination $staged
  [IO.File]::WriteAllText((Join-Path $staged 'updater-banner.json'),
    (@{Channel='Stable';Tag='v0.2.4'}|ConvertTo-Json))
  Reject {Repair-InterruptedRollback}
  if(-not(Test-Path -LiteralPath $staged) -or
     (Read-ManagedVersion (Join-Path $script:channelRoot 'previous')) -cne 'ci-199'){
   throw 'Cross-channel repair mutated managed folders.'
  }
  $repairChecks++
  Reset-SyntheticLite
  $bad=Join-Path $script:channelRoot 'rollback-untrusted'
  Move-Item -LiteralPath $script:current -Destination $bad
  Reject {Repair-InterruptedRollback}
  if(-not(Test-Path -LiteralPath $bad)){throw 'Invalid staging name was changed.'}
  $repairChecks++
  if($repairChecks -ne 8){throw 'Wrong interrupted rollback repair test count.'}
  Write-Host 'Updater Banner 8/8 interrupted rollback repair checks passed.'
  $installChecks=0
  $installId='0123456789abcdef0123456789abcdef'
  $newPending=Join-Path $script:channelRoot ('pending-'+$installId)
  $olderInstall=Join-Path $script:channelRoot ('older-'+$installId)
  $abandonedInstall=Join-Path $script:channelRoot ('abandoned-'+$installId)
  function Reset-SyntheticInstall([bool]$withPrevious=$true) {
   Reset-SyntheticLite
   if(-not $withPrevious){
    Remove-Item -LiteralPath (Join-Path $script:channelRoot 'previous') -Recurse -Force
   }
   New-SyntheticLite $newPending 'ci-201' 'Development'
   Write-InstallTransaction $installId 'ci-201'
  }
  Reset-SyntheticInstall
  Reject {Rollback}
  Repair-InterruptedInstall
  if((Read-ManagedVersion $script:current) -cne 'ci-200' -or
     (Read-ManagedVersion (Join-Path $script:channelRoot 'previous')) -cne 'ci-199' -or
     -not(Test-Path -LiteralPath $abandonedInstall) -or
     (Test-Path -LiteralPath (Get-InstallTransactionFile))){
   throw 'Prepared-state install recovery failed.'
  }
  $installChecks++
  Reset-SyntheticInstall
  Move-Item -LiteralPath (Join-Path $script:channelRoot 'previous') -Destination $olderInstall
  Repair-InterruptedInstall
  if((Read-ManagedVersion $script:current) -cne 'ci-200' -or
     (Read-ManagedVersion (Join-Path $script:channelRoot 'previous')) -cne 'ci-199'){
   throw 'Previous-relocated install recovery failed.'
  }
  $installChecks++
  Reset-SyntheticInstall
  Move-Item -LiteralPath (Join-Path $script:channelRoot 'previous') -Destination $olderInstall
  Move-Item -LiteralPath $script:current -Destination (Join-Path $script:channelRoot 'previous')
  Repair-InterruptedInstall
  if((Read-ManagedVersion $script:current) -cne 'ci-200' -or
     (Read-ManagedVersion (Join-Path $script:channelRoot 'previous')) -cne 'ci-199'){
   throw 'Current-relocated install recovery failed.'
  }
  $installChecks++
  Reset-SyntheticInstall
  Move-Item -LiteralPath (Join-Path $script:channelRoot 'previous') -Destination $olderInstall
  Move-Item -LiteralPath $script:current -Destination (Join-Path $script:channelRoot 'previous')
  Move-Item -LiteralPath $newPending -Destination $script:current
  Repair-InterruptedInstall
  if((Read-ManagedVersion $script:current) -cne 'ci-201' -or
     (Read-ManagedVersion (Join-Path $script:channelRoot 'previous')) -cne 'ci-200' -or
     (Test-Path -LiteralPath $olderInstall) -or
     (Test-Path -LiteralPath (Get-InstallTransactionFile))){
   throw 'Promoted install reconciliation failed.'
  }
  $installChecks++
  Reset-SyntheticInstall
  Move-Item -LiteralPath (Join-Path $script:channelRoot 'previous') -Destination $olderInstall
  Move-Item -LiteralPath $newPending -Destination $abandonedInstall
  Repair-InterruptedInstall
  if((Read-ManagedVersion $script:current) -cne 'ci-200' -or
     (Read-ManagedVersion (Join-Path $script:channelRoot 'previous')) -cne 'ci-199'){
   throw 'Interrupted repair was not resumable.'
  }
  $installChecks++
  Reset-SyntheticInstall $false
  Move-Item -LiteralPath $script:current -Destination (Join-Path $script:channelRoot 'previous')
  Repair-InterruptedInstall
  if((Read-ManagedVersion $script:current) -cne 'ci-200' -or
     (Test-Path -LiteralPath (Join-Path $script:channelRoot 'previous'))){
   throw 'Single-version install recovery failed.'
  }
  $installChecks++
  Reset-SyntheticInstall
  [IO.File]::WriteAllText((Join-Path $newPending 'updater-banner.json'),
   (@{Channel='Stable';Tag='v0.2.4'}|ConvertTo-Json))
  Reject {Repair-InterruptedInstall}
  if((Read-ManagedVersion $script:current) -cne 'ci-200' -or
     -not(Test-Path -LiteralPath (Get-InstallTransactionFile))){
   throw 'Cross-channel install repair mutated managed files.'
  }
  $installChecks++
  Reset-SyntheticInstall
  $unexpectedPending=Join-Path $script:channelRoot 'pending-fedcba9876543210fedcba9876543210'
  New-SyntheticLite $unexpectedPending 'ci-202' 'Development'
  Reject {Repair-InterruptedInstall}
  if((Read-ManagedVersion $script:current) -cne 'ci-200' -or
     -not(Test-Path -LiteralPath (Get-InstallTransactionFile))){
   throw 'Unexpected staging marker was ignored.'
  }
  $installChecks++
  if($installChecks -ne 8){throw 'Wrong interrupted install recovery test count.'}
  Write-Host 'Updater Banner 8/8 interrupted install recovery checks passed.'
  $launchChecks=0
  Reset-SyntheticLite
  $validLaunch=Get-VerifiedLaunchTarget
  if($validLaunch.Tag -cne 'ci-200' -or
     $validLaunch.File -cne (Join-Path $script:current 'Start-Dvinholm-Lite.cmd')){
   throw 'Valid Development launch candidate was not selected.'
  }
  $launchChecks++
  Reset-SyntheticLite
  Remove-Item -LiteralPath $script:current -Recurse -Force
  Reject {Get-VerifiedLaunchTarget}
  $launchChecks++
  Reset-SyntheticLite
  Remove-Item -LiteralPath (Join-Path $script:current 'Start-Dvinholm-Lite.cmd') -Force
  Reject {Get-VerifiedLaunchTarget}
  $launchChecks++
  Reset-SyntheticLite
  [IO.File]::WriteAllBytes((Join-Path $script:current 'Dvinholm.App.exe'),[byte[]](0,1,2,3))
  Reject {Get-VerifiedLaunchTarget}
  $launchChecks++
  Reset-SyntheticLite
  [IO.File]::WriteAllText((Join-Path $script:current 'updater-banner.json'),
   (@{Channel='Stable';Tag='v0.2.4'}|ConvertTo-Json))
  Reject {Get-VerifiedLaunchTarget}
  $launchChecks++
  Reset-SyntheticInstall
  Reject {Get-VerifiedLaunchTarget}
  if((Test-Path -LiteralPath (Get-InstallTransactionFile)) -eq $false){
   throw 'Launch validation mutated the install journal.'
  }
  $launchChecks++
  Reset-SyntheticLite
  Move-Item -LiteralPath $script:current -Destination $staged
  Reject {Get-VerifiedLaunchTarget}
  $launchChecks++
  Reset-SyntheticLite
  Remove-Item -LiteralPath (Join-Path $script:channelRoot 'previous') -Recurse -Force
  if((Get-VerifiedLaunchTarget).Tag -cne 'ci-200'){
   throw 'First installed version could not launch without a backup.'
  }
  $launchChecks++
  if($launchChecks -ne 8){throw 'Wrong managed launch candidate test count.'}
  Write-Host 'Updater Banner 8/8 verified Lite launch candidate checks passed.'
  $statusChecks=0
  Reset-SyntheticLite
  if((Get-ChannelRecoveryState) -cne 'Ready'){throw 'Clean recovery status incorrect.'}
  $statusChecks++
  Reset-SyntheticInstall
  if((Get-ChannelRecoveryState) -cne 'Repair install'){throw 'Install recovery status incorrect.'}
  $statusChecks++
  Reset-SyntheticLite
  New-SyntheticLite $newPending 'ci-201' 'Development'
  if((Get-ChannelRecoveryState) -cne 'Inspect'){throw 'Orphan staging was not flagged.'}
  $statusChecks++
  Reset-SyntheticLite
  Move-Item -LiteralPath $script:current -Destination $staged
  if((Get-ChannelRecoveryState) -cne 'Repair rollback'){throw 'Rollback recovery status incorrect.'}
  $statusChecks++
  Reset-SyntheticLite
  Move-Item -LiteralPath $script:current -Destination $bad
  if((Get-ChannelRecoveryState) -cne 'Inspect'){throw 'Invalid rollback marker was not flagged.'}
  $statusChecks++
  Reset-SyntheticLite
  Move-Item -LiteralPath $script:current -Destination $staged
  [IO.File]::WriteAllText((Get-InstallTransactionFile),'unverified')
  if((Get-ChannelRecoveryState) -cne 'Inspect'){throw 'Mixed recovery markers were not flagged.'}
  $statusChecks++
  if($statusChecks -ne 6){throw 'Wrong recovery status check count.'}
  Write-Host 'Updater Banner 6/6 read-only recovery status checks passed.'
  $versionChecks=0
  Reset-SyntheticLite
  if((Installed) -cne 'ci-200'){throw 'Valid Development current version not recognized.'}
  $versionChecks++
  Reset-SyntheticLite
  Remove-Item -LiteralPath $script:current -Recurse -Force
  if((Installed) -cne 'Not installed'){throw 'Missing Lite directory marked as installed.'}
  $versionChecks++
  Reset-SyntheticLite
  Remove-Item -LiteralPath (Join-Path $script:current 'Start-Dvinholm-Lite.cmd') -Force
  if((Installed) -cne 'Unverified installation'){
   throw 'Incomplete Lite installation was reported as verified.'
  }
  $versionChecks++
  Reset-SyntheticLite
  [IO.File]::WriteAllBytes((Join-Path $script:current 'Dvinholm.App.exe'),[byte[]](0,1,2,3))
  if((Installed) -cne 'Unverified installation'){
   throw 'Invalid x86 PE was reported as verified.'
  }
  $versionChecks++
  Reset-SyntheticLite
  [IO.File]::WriteAllText((Join-Path $script:current 'updater-banner.json'),
   (@{Channel='Stable';Tag='v0.2.4'}|ConvertTo-Json))
  if((Installed) -cne 'Unverified installation'){
   throw 'Cross-channel version was reported as installed.'
  }
  $versionChecks++
  if((Display-Version 'ci-4120') -cne 'ci-4120'){
   throw 'Development CI version was altered.'
  }
  $versionChecks++
  $script:Channel='Stable'
  $script:channelRoot=Join-Path $testDir 'Stable'
  $script:current=Join-Path $script:channelRoot 'current'
  New-SyntheticLite $script:current 'v0.2.4' 'Stable'
  if((Installed) -cne '0.2.4'){
   throw 'Stable installed version retained the GitHub v prefix.'
  }
  $versionChecks++
  if((Display-Version 'v0.2.5') -cne '0.2.5'){
   throw 'Stable available version retained the GitHub v prefix.'
  }
  $versionChecks++
  if($versionChecks -ne 8){throw 'Wrong verified-version display check count.'}
  Write-Host 'Updater Banner 8/8 verified channel version display checks passed.'
  $preflightChecks=0
  $allInstalled=@{DotNet='Installed';WinApp='Installed';VC='Installed'}
  Assert-InstallDependencies $allInstalled
  $preflightChecks++
  foreach($key in @('DotNet','WinApp','VC')){
   $missing=@{DotNet='Installed';WinApp='Installed';VC='Installed'}
   $missing[$key]='Missing'
   Reject {Assert-InstallDependencies $missing}
   $preflightChecks++
  }
  Reject {Assert-InstallDependencies @{DotNet='Unknown';WinApp='Installed';VC='Installed'}}
  $preflightChecks++
  Reject {Assert-InstallDependencies @{DotNet='Installed';VC='Installed'}}
  $preflightChecks++
  Reject {Assert-InstallDependencies 'Installed'}
  $preflightChecks++
  if($preflightChecks -ne 7){throw 'Wrong Lite dependency preflight check count.'}
  Write-Host 'Updater Banner 7/7 offline dependency preflight checks passed.'
 }finally{
  $script:StateFile=$originalStateFile
  $script:base=$originalBase
  $script:Channel=$originalChannel
  $script:channelRoot=$originalChannelRoot
  $script:current=$originalCurrent
  if(Test-Path -LiteralPath $testDir){
   Remove-Item -LiteralPath $testDir -Recurse -Force
  }
 }
}
if($Mode -eq 'SelfTest'){SelfTest;exit 0}
if($Mode -ne 'UI' -and $Mode -ne 'UiSmokeTest'){
 try{
  $script:state.Current=Installed
  $script:state.Recovery=Get-ChannelRecoveryState
  $inventory=Probe
  foreach($key in @('DotNet','WinApp','VC')){$script:state[$key]=$inventory[$key]}
  Save-State
  if($Mode -eq 'Probe'){
   if($script:state.Recovery -eq 'Ready'){
    Progress 'Runtime dependencies rechecked locally. No GitHub request made.'
   }else{
    Progress ('Recovery required: '+$script:state.Recovery+'. No GitHub request made.')
   }
  }elseif($Mode -eq 'Check'){
   if($script:state.Recovery -ne 'Ready'){
    throw 'Recovery must be handled before update checks.'
   }
   $r=Latest;$script:state.Available=Display-Version $r.Tag
   Progress ('Latest '+$Channel+' release: '+$r.Tag+'. No changes made.')
  }elseif($Mode -eq 'Launch'){
   Invoke-ChannelExclusive {Launch}
  }elseif($Mode -eq 'Rollback'){
   Invoke-ChannelExclusive {Rollback}
  }elseif($Mode -eq 'Repair'){
   Invoke-ChannelExclusive {Repair-InterruptedRollback}
  }elseif($Mode -eq 'RepairInstall'){
   Invoke-ChannelExclusive {Repair-InterruptedInstall}
  }else{
   Invoke-ChannelExclusive {Install}
  }
  $script:state.Recovery=Get-ChannelRecoveryState
  $script:state.Done=$true;Save-State;exit 0
 }catch{
  $message=[string]$_.Exception.Message
  $script:state.Detail=if($message -match 'prerequisite|runtime|dependency|SDK|VC|Desktop'){
   'Missing Lite prerequisites. Install/update Microsoft components, then retry.'
  }elseif($message -match 'Close the application'){
   'Close Dvinholm, then retry. Managed files were not changed.'
  }elseif($message -match '^Launch blocked:'){
   $message
  }elseif($Mode -eq 'Launch'){
   'Unable to open the selected Lite launcher. No installation changes were made.'
  }elseif($message -match 'Recovery must be handled'){
   'Recovery required. Use the matching repair button or inspect the managed folders.'
  }elseif($message -match 'Interrupted rollback requires'){
   'Interrupted rollback detected. Use Repair rollback before updating or restoring.'
  }elseif($message -match 'Interrupted install requires'){
   'Interrupted installation detected. Use Repair install before changing managed versions.'
  }elseif($message -match 'install transaction|install journal|Install staging|No verifiable|Ambiguous interrupted install'){
   'Installation cannot be repaired safely from this state. No automatic changes were made.'
  }elseif($message -match 'Repair requires|Untrusted rollback|Ambiguous interrupted|versions are identical'){
   'Cannot safely repair these rollback folders. No additional files were changed.'
  }elseif($message -match 'Another updater operation'){
   'Another updater is already changing this channel. Try again after it finishes.'
  }elseif($message -match 'Rollback interrupted'){
   'Rollback was interrupted. Managed folders retained; manual recovery required.'
  }elseif($message -match 'rollback|previous|managed version|Managed Lite'){
   'Rollback unavailable or failed. Check that both managed versions are valid.'
  }elseif($message -match 'SHA|ZIP|digest|manifest|archive|asset|unsafe'){
   'Release integrity/security check failed. Nothing was installed.'
  }else{'Update/check failed. Inspect the managed install directory if a replacement was interrupted.'}
  $script:state.Recovery=Get-ChannelRecoveryState
  $script:state.Failed=$true;$script:state.Done=$true;Save-State;exit 1
 }
}
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$form=New-Object Windows.Forms.Form
$form.Text='Dvinholm Updater Banner'
$form.Size=New-Object Drawing.Size(740,464)
$form.MinimumSize=$form.Size;$form.MaximumSize=$form.Size
$form.Font=New-Object Drawing.Font('Segoe UI',10)
$form.StartPosition='CenterScreen'
function Label([string]$value,[int]$top,[int]$height){
 $l=New-Object Windows.Forms.Label;$l.Text=$value;$l.SetBounds(18,$top,695,$height)
 $form.Controls.Add($l);return $l
}
$title=Label 'DVINHOLM   |   UPDATER BANNER' 15 38
$title.Font=New-Object Drawing.Font('Segoe UI',14,[Drawing.FontStyle]::Bold)
$null=Label 'No .NET 10 or WinApp Runtime needed. Stable and Development install separately.' 52 26
$choice=New-Object Windows.Forms.ComboBox;$choice.DropDownStyle='DropDownList'
$choice.Items.AddRange([object[]]@('Development','Stable'))
$choice.SetBounds(18,87,185,29);$form.Controls.Add($choice)
$rollback=New-Object Windows.Forms.Button
$rollback.Text='Restore previous';$rollback.SetBounds(215,85,175,33)
$form.Controls.Add($rollback)
$repair=New-Object Windows.Forms.Button
$repair.Text='Repair rollback';$repair.SetBounds(401,85,165,33)
$form.Controls.Add($repair)
$repairInstall=New-Object Windows.Forms.Button
$repairInstall.Text='Repair install';$repairInstall.SetBounds(575,85,138,33)
$form.Controls.Add($repairInstall)
$installed=Label 'Installed: not checked' 126 25
$available=Label 'Available: not checked' 151 25
$deps=Label '.NET x86: ?    WinApp: ?    VC++ x86: ?' 180 26
$recovery=Label 'Recovery: not checked' 209 23
$status=Label 'Ready.' 231 45
$bar=New-Object Windows.Forms.ProgressBar
$bar.SetBounds(18,286,695,13);$bar.Style='Marquee';$bar.MarqueeAnimationSpeed=0
$form.Controls.Add($bar)
function Button([string]$value,[int]$x,[int]$y,[int]$w){
 $b=New-Object Windows.Forms.Button;$b.Text=$value;$b.SetBounds($x,$y,$w,34)
 $form.Controls.Add($b);return $b
}
$check=Button 'Check updates' 18 311 158
$install=Button 'Download / Install' 184 311 185
$launch=Button 'Start Dvinholm' 377 311 159
$refresh=Button 'Recheck deps' 544 311 169
$dot=Button 'Install .NET x86' 18 355 218;$dot.Tag='DotNet'
$win=Button 'Install WinApp' 252 355 218;$win.Tag='WinApp'
$vc=Button 'Install VC++ x86' 486 355 227;$vc.Tag='VC'
$script:child=$null;$script:vendor=$null
$script:statefile=Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-banner-'+[guid]::NewGuid().ToString('N')+'.json')
$powershell=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$script:recoveryStatus='Not checked'
function Update-ActionAvailability {
 $busy=($null -ne $script:child -or $null -ne $script:vendor)
 $state=[string]$script:recoveryStatus
 $ready=($state -ceq 'Ready')
 $choice.Enabled=-not $busy
 $check.Enabled=($ready -and -not $busy)
 $install.Enabled=($ready -and -not $busy)
 $launch.Enabled=($ready -and -not $busy)
 $refresh.Enabled=-not $busy
 $rollback.Enabled=($ready -and -not $busy)
 $repair.Enabled=($state -ceq 'Repair rollback' -and -not $busy)
 $repairInstall.Enabled=($state -ceq 'Repair install' -and -not $busy)
}
function Begin-Action([string]$action){
 if($script:child -or $script:vendor){return}
 if($action -in @('Install','Rollback','Check','Launch') -and
    $script:recoveryStatus -cne 'Ready'){return}
 if($action -eq 'RepairInstall'){
  if([Windows.Forms.MessageBox]::Show(
   'Attempt offline recovery of an interrupted Lite installation for the selected channel? Only a verified transaction journal and unambiguous folders will be accepted. Existing staged files are retained. Close Dvinholm first.',
   'Confirm install recovery',[Windows.Forms.MessageBoxButtons]::YesNo,
   [Windows.Forms.MessageBoxIcon]::Warning) -ne [Windows.Forms.DialogResult]::Yes){return}
 }
 if($action -eq 'Repair'){
  if([Windows.Forms.MessageBox]::Show(
   'Attempt offline repair of an interrupted rollback for the selected channel? The original current build will be restored only when the on-disk state is unambiguous. Close Dvinholm first.',
   'Confirm rollback recovery',[Windows.Forms.MessageBoxButtons]::YesNo,
   [Windows.Forms.MessageBoxIcon]::Warning) -ne [Windows.Forms.DialogResult]::Yes){return}
 }
 if($action -eq 'Rollback'){
  if([Windows.Forms.MessageBox]::Show(
   'Restore the previous Lite build of the selected channel, without downloading anything? The currently installed build becomes the backup. Close Dvinholm first.',
   'Confirm rollback',[Windows.Forms.MessageBoxButtons]::YesNo,
   [Windows.Forms.MessageBoxIcon]::Warning) -ne [Windows.Forms.DialogResult]::Yes){return}
 }
 if($action -eq 'Install'){
  if([Windows.Forms.MessageBox]::Show(
   'Download the selected Lite ZIP, verify checksums and install into an isolated channel? Existing manual installations will not be changed.',
   'Confirm update',[Windows.Forms.MessageBoxButtons]::YesNo,
   [Windows.Forms.MessageBoxIcon]::Question) -ne [Windows.Forms.DialogResult]::Yes){return}
 }
 if(Test-Path $script:statefile){Remove-Item $script:statefile -Force}
 $script:recoveryStatus='Not checked'
 $recovery.Text='Recovery: checking local state...'
 Update-ActionAvailability
 $args='-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "'+$PSCommandPath+
  '" -Mode '+$action+' -Channel '+$choice.SelectedItem.ToString()+
  ' -StateFile "'+$script:statefile+'"'
 try{
  $script:child=Start-Process -FilePath $powershell -ArgumentList $args -WindowStyle Hidden -PassThru
  $bar.MarqueeAnimationSpeed=25
  Update-ActionAvailability
  $status.Text='Working...'
 }catch{$status.Text='Unable to launch updater helper.'}
}
$check.Add_Click({Begin-Action 'Check'})
$refresh.Add_Click({Begin-Action 'Probe'})
$install.Add_Click({Begin-Action 'Install'})
$rollback.Add_Click({Begin-Action 'Rollback'})
$repair.Add_Click({Begin-Action 'Repair'})
$repairInstall.Add_Click({Begin-Action 'RepairInstall'})
$launch.Add_Click({Begin-Action 'Launch'})
foreach($b in @($dot,$win,$vc)){
 $b.Add_Click({
  if($script:child -or $script:vendor){return}
  $which=[string]$this.Tag
  if([Windows.Forms.MessageBox]::Show(
   'Download and run the official Microsoft '+$which+' installer? It may request administrator approval.',
   'Confirm dependency installation',[Windows.Forms.MessageBoxButtons]::YesNo,
   [Windows.Forms.MessageBoxIcon]::Question) -ne [Windows.Forms.DialogResult]::Yes){return}
  $args='-NoProfile -ExecutionPolicy Bypass -File "'+$manager+'" -Mode Install -Component '+
   $which+' -LiteDirectory "'+$PSScriptRoot+'"'
  try{$script:vendor=Start-Process -FilePath $powershell -ArgumentList $args -PassThru
   $status.Text='Microsoft installer opened; follow its prompts.';$bar.MarqueeAnimationSpeed=25
  }catch{$status.Text='Unable to start Microsoft dependency installer.'}
 })
}
$choice.Add_SelectedIndexChanged({
 if($null -ne $choice.SelectedItem){
  $null=Save-ChannelPreference ([string]$choice.SelectedItem)
 }
 if(-not $script:child -and -not $script:vendor){
  $script:recoveryStatus='Not checked'
  $recovery.Text='Recovery: checking local state...'
  Update-ActionAvailability
  Begin-Action 'Probe'
 }
})
function Apply-WorkerSnapshot($snapshot) {
 # Worker state is written atomically but may be read while it is still
 # running. Do not authorize further actions from a partial progress update.
 if($null -eq $snapshot){return $false}
 $installed.Text='Installed: '+[string]$snapshot.Current
 $available.Text='Available: '+[string]$snapshot.Available
 $deps.Text='.NET x86: '+[string]$snapshot.DotNet+
  '    WinApp: '+[string]$snapshot.WinApp+'    VC++ x86: '+[string]$snapshot.VC
 $status.Text=[string]$snapshot.Detail
 $finished=($snapshot.PSObject.Properties['Done'] -and
            $snapshot.Done -is [bool] -and $snapshot.Done -eq $true)
 $valid=($snapshot.PSObject.Properties['Recovery'] -and
         [string]$snapshot.Recovery -cin @('Ready','Repair install',
                                           'Repair rollback','Inspect'))
 if($finished -and $valid){
  $script:recoveryStatus=[string]$snapshot.Recovery
  $recovery.Text='Recovery: '+$script:recoveryStatus
  Update-ActionAvailability
  return $true
 }
 $script:recoveryStatus='Not checked'
 $recovery.Text='Recovery: checking local state...'
 Update-ActionAvailability
 return $false
}
function Poll-WorkerState {
 $complete=$false
 if(Test-Path -LiteralPath $script:statefile -PathType Leaf){
  try{
   $snapshot=Get-Content -LiteralPath $script:statefile -Raw | ConvertFrom-Json
   $complete=Apply-WorkerSnapshot $snapshot
  }catch{}
 }
 if($script:child -and $script:child.HasExited){
  $script:child.Dispose();$script:child=$null;$bar.MarqueeAnimationSpeed=0
  if(-not $complete){
   $script:recoveryStatus='Not checked'
   $recovery.Text='Recovery: not checked'
   $status.Text='Updater helper stopped without a complete status. Use Recheck deps.'
  }
  Update-ActionAvailability
 }
 if($script:vendor -and $script:vendor.HasExited){
  $script:vendor.Dispose();$script:vendor=$null;$bar.MarqueeAnimationSpeed=0
  Begin-Action 'Probe'
 }
}
$timer=New-Object Windows.Forms.Timer;$timer.Interval=500
$timer.Add_Tick({Poll-WorkerState})
$timer.Start()
Update-ActionAvailability
if($Mode -eq 'UiSmokeTest'){
 # Instantiate the real Windows PowerShell 5.1 WinForms GUI, but do not
 # display a modal dialog, contact GitHub, launch installers or start Dvinholm.
 $checks=0
 try{
  if($null -eq $form -or
     $form -isnot [Windows.Forms.Form] -or
     $choice.Items.Count -ne 2 -or
     $choice.Items[0] -cne 'Development' -or
     $choice.Items[1] -cne 'Stable' -or
     -not $form.Controls.Contains($recovery)){
   throw 'Standalone WinForms controls were not constructed.'
  }
  $checks++
  # Default: nothing runs until the first local inventory has been checked.
  if($script:recoveryStatus -cne 'Not checked' -or
     $install.Enabled -or $check.Enabled -or $launch.Enabled -or
     $rollback.Enabled -or $repair.Enabled -or $repairInstall.Enabled -or
     -not $refresh.Enabled){throw 'Startup actions were not locked.'}
  $checks++
  $script:recoveryStatus='Ready';Update-ActionAvailability
  if(-not $install.Enabled -or -not $check.Enabled -or
     -not $launch.Enabled -or -not $rollback.Enabled -or
     $repair.Enabled -or $repairInstall.Enabled){
   throw 'Ready-state button policy failed.'
  }
  $checks++
  $script:recoveryStatus='Repair install';Update-ActionAvailability
  if(-not $repairInstall.Enabled -or $repair.Enabled -or
     $install.Enabled -or $rollback.Enabled -or $check.Enabled -or
     $launch.Enabled){throw 'Install recovery was not isolated.'}
  $checks++
  $script:recoveryStatus='Repair rollback';Update-ActionAvailability
  if(-not $repair.Enabled -or $repairInstall.Enabled -or
     $install.Enabled -or $rollback.Enabled -or $check.Enabled -or
     $launch.Enabled){throw 'Rollback recovery was not isolated.'}
  $checks++
  $script:recoveryStatus='Inspect';Update-ActionAvailability
  if($repair.Enabled -or $repairInstall.Enabled -or
     $install.Enabled -or $rollback.Enabled -or $check.Enabled -or
     $launch.Enabled -or -not $refresh.Enabled){
   throw 'Ambiguous recovery state was not fail-closed.'
  }
  $checks++
  $script:child=[pscustomobject]@{HasExited=$false}
  $script:recoveryStatus='Ready';Update-ActionAvailability
  if($choice.Enabled -or $refresh.Enabled -or $install.Enabled -or
     $rollback.Enabled -or $check.Enabled -or $launch.Enabled -or
     $repair.Enabled -or $repairInstall.Enabled){
   throw 'Busy-state buttons were not locked.'
  }
  $checks++
  if($checks -ne 7){throw 'Wrong Windows WinForms smoke check count.'}
  Write-Host 'Updater Banner 7/7 Windows PowerShell 5.1 WinForms smoke checks passed.'
  $snapshotChecks=0
  $script:child=$null
  $completed=[pscustomobject]@{
   Done=$true;Failed=$false;Recovery='Ready';Current='ci-4118'
   Available='ci-4118';DotNet='Installed';WinApp='Installed'
   VC='Installed';Detail='Completed'
  }
  if(-not (Apply-WorkerSnapshot $completed) -or
     -not $install.Enabled -or -not $launch.Enabled){
   throw 'Completed worker snapshot failed to re-enable Ready actions.'
  }
  $snapshotChecks++
  $script:child=[pscustomobject]@{HasExited=$false}
  if(-not (Apply-WorkerSnapshot $completed) -or
     $install.Enabled -or $launch.Enabled -or $refresh.Enabled){
   throw 'Completed snapshot unlocked actions while helper was still running.'
  }
  $snapshotChecks++
  $script:child=$null
  if(-not (Apply-WorkerSnapshot $completed) -or
     -not $install.Enabled -or -not $check.Enabled){
   throw 'Late snapshot after worker completion did not unlock the UI.'
  }
  $snapshotChecks++
  $incomplete=$completed | Select-Object *
  $incomplete.Done=$false
  if((Apply-WorkerSnapshot $incomplete) -or
     $install.Enabled -or $launch.Enabled -or
     $script:recoveryStatus -cne 'Not checked'){
   throw 'Incomplete worker progress unlocked the UI.'
  }
  $snapshotChecks++
  $invalid=$completed | Select-Object *
  $invalid.Recovery='Untrusted'
  if((Apply-WorkerSnapshot $invalid) -or
     $install.Enabled -or $repair.Enabled -or $repairInstall.Enabled){
   throw 'Untrusted worker recovery field was accepted.'
  }
  $snapshotChecks++
  $repairSnapshot=$completed | Select-Object *
  $repairSnapshot.Recovery='Repair install'
  if(-not (Apply-WorkerSnapshot $repairSnapshot) -or
     -not $repairInstall.Enabled -or $install.Enabled -or $repair.Enabled){
   throw 'Completed recovery snapshot did not activate the matching repair.'
  }
  $snapshotChecks++
  if($snapshotChecks -ne 6){throw 'Wrong worker state synchronization test count.'}
  Write-Host 'Updater Banner 6/6 WinForms worker-state synchronization checks passed.'
 }finally{
  $script:child=$null
  $timer.Stop();$timer.Dispose();$form.Dispose()
 }
 exit 0
}
$startupChannel=$Channel
if(-not $PSBoundParameters.ContainsKey('Channel')){
 $savedChannel=Read-ChannelPreference
 if($null -ne $savedChannel){$startupChannel=$savedChannel}
}
$form.Add_Shown({$choice.SelectedIndex=if($startupChannel -eq 'Stable'){1}else{0}})
try{[void]$form.ShowDialog()}
finally{
 $timer.Stop();$timer.Dispose()
 if($script:child){$script:child.Dispose()}
 if($script:vendor){$script:vendor.Dispose()}
 if(Test-Path $script:statefile){Remove-Item $script:statefile -Force -ErrorAction SilentlyContinue}
 $form.Dispose()
}