#requires -Version 5.1
<#
Standalone Windows 11 updater. Only inbox PowerShell 5.1 and .NET Framework
WinForms are needed; .NET 10, Windows App SDK and VC x86 are NOT needed to run.
No software installs or updates until the user explicitly confirms.
#>
[CmdletBinding()]
param(
 [ValidateSet('UI','UiSmokeTest','Check','Install','Rollback','Repair','RepairInstall','Launch','Probe','Versions','SelfTest')][string]$Mode='UI',
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
$languageFile=Join-Path $base 'language-preference.txt'
# Dvinholm.exe follows the same remembered channel as Launcher.exe.
if($Mode -eq 'Launch' -and -not $PSBoundParameters.ContainsKey('Channel')){
 try {
  if(Test-Path -LiteralPath $preferenceFile -PathType Leaf){
   $item=Get-Item -LiteralPath $preferenceFile -Force
   if($item.Length -le 32 -and
      ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0){
    $saved=[IO.File]::ReadAllText($preferenceFile,[Text.Encoding]::ASCII).Trim()
    if($saved -cin @('Stable','Development')){$Channel=$saved}
   }
  }
 }catch{}
}
$channelRoot=Join-Path $base $Channel
$current=Join-Path $channelRoot 'current'
$state=[ordered]@{Done=$false;Failed=$false;Current='Not installed';Available='Not checked';
 DotNet='Unknown';WinApp='Unknown';VC='Unknown';DependencyReport='';Recovery='Not checked';Detail='Ready';DownloadId='';DownloadTotal=0}
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
  if($null -eq $r){throw 'No public Development release found.'}
 }
 $zip=@($r.assets|Where-Object{$_.name -ceq $zipName})
 $sums=@($r.assets|Where-Object{$_.name -ceq $sumsName})
 if($zip.Count -ne 1 -or $sums.Count -ne 1){throw 'Missing unique application ZIP or sums.'}
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
# RU/EN is a per-user preference, independent of the installed channel.
# Read small regular files only; never follow an attacker-controlled reparse point.
function Read-LauncherLanguage {
 try {
  if(-not(Test-Path -LiteralPath $languageFile -PathType Leaf)){return 'RU'}
  $item=Get-Item -LiteralPath $languageFile -Force
  if($item.Length -gt 8 -or
     ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){return 'RU'}
  $value=[IO.File]::ReadAllText($languageFile,[Text.Encoding]::ASCII).Trim()
  if($value -cin @('RU','EN')){return $value}
 }catch{}
 return 'RU'
}
function Save-LauncherLanguage([string]$value) {
 if($value -cnotin @('RU','EN')){return $false}
 $tmp=$null
 try {
  Assert-ManagedFolder (Split-Path -Parent $base)
  Assert-ManagedFolder $base
  [IO.Directory]::CreateDirectory($base)|Out-Null
  if(Test-Path -LiteralPath $languageFile) {
   $item=Get-Item -LiteralPath $languageFile -Force
   if(($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){return $false}
  }
  $tmp=Join-Path $base ('language-'+[guid]::NewGuid().ToString('N')+'.tmp')
  [IO.File]::WriteAllText($tmp,$value,[Text.Encoding]::ASCII)
  Move-Item -LiteralPath $tmp -Destination $languageFile -Force
  return $true
 }catch{return $false}
 finally{
  if($tmp -and (Test-Path -LiteralPath $tmp)){
   Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
  }
 }
}
$script:launcherLanguage=Read-LauncherLanguage
function Translate([string]$key) {
 $labels=@{
  RU=@{
   home='▶  Запуск';updates='↻  Обновления'
   dependencies='◈  Компоненты';settings='⚙  Настройки'
   about='ⓘ  О программе';drivers='▣  Драйверы'
   channel='КАНАЛ ОБНОВЛЕНИЙ';progress='ЗАГРУЗКА И УСТАНОВКА'
   prereqs='КОМПОНЕНТЫ WINDOWS';stable='Stable · официальный релиз'
   dev='Development · сборки CI';installed='Установлено';available='Доступно'
   recovery='Восстановление';check='Проверить обновления'
   install='Скачать / установить';launch='Запустить Dvinholm'
   refresh='Перепроверить';versions='Проверить версии компонентов'
   dot='Установить .NET x86';win='Установить WinApp'
   vc='Установить VC++ x86';rollback='Предыдущая версия'
   repair='Исправить откат';repairInstall='Исправить установку'
   aboutMessage='Dvinholm Launcher · Windows x86. Stable и Development устанавливаются отдельно. Загрузки проверяются SHA-256. Поддерживается откат, проверка компонентов Windows и драйверов J2534. Физический адаптер здесь не тестируется.'
   settingsTitle='Настройки лаунчера';language='Язык';channelSetting='Канал обновлений'
   save='Применить';cancel='Отмена'
   confirmInstall='Скачать архив выбранной версии, проверить SHA-256 и установить в отдельный каталог канала?'
   confirmRollback='Восстановить предыдущую сборку выбранного канала без загрузки? Закройте Dvinholm.'
   confirmRepair='Восстановить прерванный откат? Закройте Dvinholm.'
   confirmRepairInstall='Восстановить прерванную установку? Закройте Dvinholm.'
   confirmVendor='Скачать и запустить официальный установщик Microsoft? Возможен запрос UAC.'
   confirmTitle='Подтверждение'
   cockpitReady='ГОТОВ';cockpitUnchecked='НЕ ПРОВЕРЕНО';cockpitCaption='Диагностика и конфигурация автомобиля'
   channelHeading='Канал обновлений';stableHint='Stable: релиз / Development: сборки CI'
   afterLaunch='После запуска Dvinholm лаунчер закроется.'
   updatesHeading='Обновления Dvinholm';updatesHint='Проверка SHA-256, загрузка и установка'
   windowsHeading='Требования Windows';windowsHint='Проверка компонентов и версий x86'
   driversHeading='Драйверы адаптеров';driversHint='J2534 · Volvo DiCE · Mongoose JLR'
   driversUnchecked='Драйверы ещё не проверены';driversScan='Проверить'
   driversOpen='Открыть драйверы';settingsQuick='Настройки'
   windowsQuick='Перепроверить';depsInstall='Установить компоненты'
   componentsTitle='Компоненты Microsoft x86';componentsNotice='Выберите компонент для установки. Будет показано подтверждение.'
  }
  EN=@{
   home='▶  Launch';updates='↻  Updates'
   dependencies='◈  Dependencies';settings='⚙  Settings'
   about='ⓘ  About';drivers='▣  Drivers'
   channel='UPDATE CHANNEL';progress='DOWNLOAD AND INSTALL'
   prereqs='WINDOWS PREREQUISITES';stable='Stable · official release'
   dev='Development · latest CI';installed='Installed';available='Available'
   recovery='Recovery';check='Check updates'
   install='Download / Install';launch='Start Dvinholm'
   refresh='Recheck dependencies';versions='Check runtime versions'
   dot='Install .NET x86';win='Install WinApp'
   vc='Install VC++ x86';rollback='Restore previous'
   repair='Repair rollback';repairInstall='Repair install'
   aboutMessage='Dvinholm Launcher · Windows x86. Stable and Development use isolated installs. Downloads are SHA-256 verified. Offline rollback, Windows prerequisites and J2534 driver checks are available. Physical hardware is not tested.'
   settingsTitle='Launcher settings';language='Language';channelSetting='Update channel'
   save='Apply';cancel='Cancel'
   confirmInstall='Download the selected application ZIP, verify SHA-256 and install it into an isolated channel?'
   confirmRollback='Restore the previous build in the selected channel without downloading? Close Dvinholm first.'
   confirmRepair='Repair an interrupted rollback? Close Dvinholm first.'
   confirmRepairInstall='Repair an interrupted installation? Close Dvinholm first.'
   confirmVendor='Download and run the official Microsoft installer? UAC may be requested.'
   confirmTitle='Confirm action'
   cockpitReady='READY';cockpitUnchecked='NOT CHECKED';cockpitCaption='Vehicle diagnostics and configuration'
   channelHeading='Update channel';stableHint='Stable releases / Development CI builds'
   afterLaunch='Launcher closes after Dvinholm starts successfully.'
   updatesHeading='Dvinholm updates';updatesHint='SHA-256 verification, download and installation'
   windowsHeading='Windows requirements';windowsHint='Check installed x86 components and versions'
   driversHeading='Adapter drivers';driversHint='J2534 · Volvo DiCE · Mongoose JLR'
   driversUnchecked='Drivers not checked yet';driversScan='Scan drivers'
   driversOpen='Open drivers';settingsQuick='Settings'
   windowsQuick='Recheck';depsInstall='Install components'
   componentsTitle='Microsoft x86 prerequisites';componentsNotice='Select the component to install. Confirmation is required.'
  }
 }
 $value=$labels[$script:launcherLanguage][$key]
 if($null -eq $value){return $key}
 return [string]$value
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
  # A clean channel must contain either no installation, a validated first
  # installation, or a valid current/previous pair. An orphan previous or
  # damaged folder has no unambiguous automatic recovery path.
  $previous=Join-Path $channelRoot 'previous'
  $hasCurrent=Test-Path -LiteralPath $current
  $hasPrevious=Test-Path -LiteralPath $previous
  if($hasPrevious -and -not $hasCurrent){return 'Inspect'}
  if($hasCurrent){$null=Read-ManagedVersion $current}
  if($hasPrevious){$null=Read-ManagedVersion $previous}
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
 # The combined first-run ZIP carries its verified Lite payload in Binaries.
 # Once this channel has a managed current version, never fall back to an
 # obsolete or tampered bundled executable on failure.
 if(-not(Test-Path -LiteralPath $current) -and
    (Test-Path -LiteralPath $PSScriptRoot -PathType Container)){
  $bundled=$PSScriptRoot
  Assert-ManagedFolder $bundled
  foreach($name in @('Dvinholm.App.exe','Dvinholm.App.runtimeconfig.json',
    'Check-Lite-Prerequisites.ps1','Manage-Lite-Dependencies.ps1',
    'Start-Dvinholm-Lite.cmd')){
   $file=Join-Path $bundled $name
   if(-not(Test-Path -LiteralPath $file -PathType Leaf) -or
      ((Get-Item -LiteralPath $file -Force).Attributes -band
       [IO.FileAttributes]::ReparsePoint) -ne 0){
    throw 'Bundled Lite installation is incomplete or unsafe.'
   }
  }
  $app=[IO.File]::ReadAllBytes((Join-Path $bundled 'Dvinholm.App.exe'))
  if($app.Length -lt 512 -or $app[0] -ne 77 -or $app[1] -ne 90){
   throw 'Bundled executable is not a valid x86 PE.'
  }
  $offset=[BitConverter]::ToInt32($app,60)
  if($offset -lt 64 -or $offset+6 -ge $app.Length -or
     [BitConverter]::ToUInt32($app,$offset) -ne 0x4550 -or
     [BitConverter]::ToUInt16($app,$offset+4) -ne 0x14c){
   throw 'Bundled executable is not a valid x86 PE.'
  }
  return [pscustomobject]@{Tag='bundled Lite';File=(Join-Path $bundled 'Start-Dvinholm-Lite.cmd')}
 }
 try {$tag=Read-ManagedVersion $current}
 catch {throw 'Launch blocked: no valid managed Lite x86 installation in this channel.'}
 return [pscustomobject]@{
  Tag=$tag
  File=(Join-Path $current 'Start-Dvinholm-Lite.cmd')
 }
}
function Start-VerifiedApplicationHelper([string]$directory,[string]$testHelper='') {
 # Use the launcher's own dependency manager, not scripts from a downloaded
 # application channel. The optional test helper is used only in SelfTest.
 # Never invoke a .cmd shell association or inherit a channel's scripts.
 $helper=if($testHelper){$testHelper}else{$manager}
 if(-not(Test-Path -LiteralPath $helper -PathType Leaf)){
  throw 'Launch blocked: application dependency manager is missing.'
 }
 $hostExe=Join-Path ([Environment]::GetFolderPath('Windows')) 'System32\WindowsPowerShell\v1.0\powershell.exe'
 # Append \. so a trailing directory separator cannot escape the closing quote.
 $arguments='-NoLogo -NoProfile -STA -ExecutionPolicy Bypass -File "'+
  $helper+'" -Mode Launch -LiteDirectory "'+$directory+'\."'
 try {
  $start=New-Object Diagnostics.ProcessStartInfo
  $start.FileName=$hostExe
  $start.Arguments=$arguments
  $start.WorkingDirectory=$directory
  $start.UseShellExecute=$false
  $process=[Diagnostics.Process]::Start($start)
  try {
   # Wait for this helper only. Start-Process -Wait also waits for descendants,
   # which would keep Launcher busy for the entire lifetime of the WinUI app.
   $process.WaitForExit()
   $exitCode=$process.ExitCode
  }finally{$process.Dispose()}
 }catch{
  throw ('Launch blocked: could not start the application helper. '+$_.Exception.Message)
 }
 if($exitCode -ne 0){
  throw ('Launch blocked: application helper exited with code '+$exitCode+
   '. Run Start-Dvinholm-Lite.cmd in the selected application folder for details.')
 }
}
function Launch {
 $target=Get-VerifiedLaunchTarget
 Start-VerifiedApplicationHelper (Split-Path -Parent $target.File)
 Progress ('Application launch helper completed for '+$Channel+' '+$target.Tag+'.')
}
function Test-ApplicationHelperHandoff {
 $temp=Join-Path ([IO.Path]::GetTempPath()) ('Dvinholm-Handoff-'+[guid]::NewGuid().ToString('N'))
 [IO.Directory]::CreateDirectory($temp)|Out-Null
 try{
  # Exercise real Windows PowerShell argument parsing with a harmless helper.
  # No application, installer, network or adapter is used.
  $names=@('plain','with spaces','amp&ersand','percent%PATH%',"quote'and[brackets]",([string][char]0x416+[char]0x44F))
  foreach($name in $names){
   $dir=Join-Path $temp $name
   [IO.Directory]::CreateDirectory($dir)|Out-Null
   $helper=Join-Path $dir 'Manage-Lite-Dependencies.ps1'
   $fixture=@'
param([string]$Mode,[string]$LiteDirectory)
if($Mode -cne 'Launch'){exit 21}
$actual=[IO.Path]::GetFullPath($LiteDirectory).TrimEnd('\')
if($actual -cne $PSScriptRoot.TrimEnd('\')){exit 22}
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'observed.txt'),$actual)
exit 0
'@
   [IO.File]::WriteAllText($helper,$fixture,[Text.Encoding]::ASCII)
   Start-VerifiedApplicationHelper $dir $helper
   if([IO.File]::ReadAllText((Join-Path $dir 'observed.txt')) -cne $dir){
    throw 'Application helper directory argument changed.'
   }
  }
  [IO.File]::WriteAllText($helper,'exit 23',[Text.Encoding]::ASCII)
  $failed=$false
  try{Start-VerifiedApplicationHelper $dir $helper}catch{
   if($_.Exception.Message -notmatch 'helper exited with code 23'){throw}
   $failed=$true
  }
  if(-not $failed){throw 'Failed application helper was reported as successful.'}
  Remove-Item -LiteralPath $helper -Force
  $missing=$false
  try{Start-VerifiedApplicationHelper $dir $helper}catch{
   if($_.Exception.Message -notmatch 'dependency manager is missing'){throw}
   $missing=$true
  }
  if(-not $missing){throw 'Missing application helper was accepted.'}
  Write-Host 'Application helper handoff 8/8 Windows process checks passed.'
 }finally{Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue}
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
function Get-InstallDisposition([string]$latestTag) {
 # Compare verified, channel-local versions before any ZIP download or rename.
 # Never downgrade in-place; Restore previous is the explicit rollback path.
 $pattern=if($Channel -ceq 'Stable'){'^v\d+\.\d+\.\d+$'}else{'^ci-[1-9][0-9]{0,8}$'}
 if($latestTag -cnotmatch $pattern){throw 'Invalid latest release tag for selected channel.'}
 try {$installedTag=Get-OptionalManagedVersion $current}
 catch {throw 'Cannot safely determine current Lite version.'}
 if(-not $installedTag){return 'Install'}
 if($installedTag -ceq $latestTag){return 'Current'}
 if($Channel -ceq 'Stable'){
  $installedNumber=[version]($installedTag.Substring(1))
  $latestNumber=[version]($latestTag.Substring(1))
 }else{
  $installedNumber=[long]($installedTag.Substring(3))
  $latestNumber=[long]($latestTag.Substring(3))
 }
 if($latestNumber -lt $installedNumber){return 'Older'}
 return 'Install'
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
 $decision=Get-InstallDisposition $r.Tag
 if($decision -eq 'Current'){
  Progress ('Up to date: '+(Display-Version $r.Tag)+'. No files downloaded or replaced.')
  return
 }
 if($decision -eq 'Older'){
  throw 'Latest published release is older than installed Lite. No files downloaded.'
 }
 Progress 'Downloading GitHub Lite ZIP and SHA256SUMS...'
 Assert-ManagedFolder $base;Assert-ManagedFolder $channelRoot
 New-Item -ItemType Directory -Path $channelRoot -Force | Out-Null
 Assert-ManagedFolder $current
 $id=[guid]::NewGuid().ToString('N')
 $script:state.DownloadId=$id
 $script:state.DownloadTotal=[long]$r.Zip.size
 Save-State
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
  $folderChecks=0
  if(Test-Path -LiteralPath $script:channelRoot){
   Remove-Item -LiteralPath $script:channelRoot -Recurse -Force
  }
  if((Get-ChannelRecoveryState) -cne 'Ready'){
   throw 'Empty selected channel was not Ready for first installation.'
  }
  $folderChecks++
  Reset-SyntheticLite
  if((Get-ChannelRecoveryState) -cne 'Ready'){
   throw 'Valid current and previous versions were not Ready.'
  }
  $folderChecks++
  Reset-SyntheticLite
  Remove-Item -LiteralPath (Join-Path $script:channelRoot 'previous') -Recurse -Force
  if((Get-ChannelRecoveryState) -cne 'Ready'){
   throw 'Valid first Lite version without backup was not Ready.'
  }
  $folderChecks++
  Reset-SyntheticLite
  Remove-Item -LiteralPath $script:current -Recurse -Force
  if((Get-ChannelRecoveryState) -cne 'Inspect'){
   throw 'Orphan previous Lite version was not flagged for manual inspection.'
  }
  $folderChecks++
  Reset-SyntheticLite
  Remove-Item -LiteralPath (Join-Path $script:current 'Start-Dvinholm-Lite.cmd') -Force
  if((Get-ChannelRecoveryState) -cne 'Inspect'){
   throw 'Damaged current Lite version was not flagged for inspection.'
  }
  $folderChecks++
  Reset-SyntheticLite
  Remove-Item -LiteralPath (Join-Path (Join-Path $script:channelRoot 'previous') 'Manage-Lite-Dependencies.ps1') -Force
  if((Get-ChannelRecoveryState) -cne 'Inspect'){
   throw 'Damaged previous Lite version was not flagged for inspection.'
  }
  $folderChecks++
  Reset-SyntheticLite
  [IO.File]::WriteAllText((Join-Path (Join-Path $script:channelRoot 'previous') 'updater-banner.json'),
   (@{Channel='Stable';Tag='v0.2.4'}|ConvertTo-Json))
  if((Get-ChannelRecoveryState) -cne 'Inspect'){
   throw 'Cross-channel backup was not flagged for manual inspection.'
  }
  $folderChecks++
  if($folderChecks -ne 7){throw 'Wrong managed folder recovery gate test count.'}
  Write-Host 'Updater Banner 7/7 managed folder recovery gate checks passed.'
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
  $decisionChecks=0
  if((Get-InstallDisposition 'v0.2.4') -cne 'Current'){
   throw 'Latest Stable version was not treated as already installed.'
  }
  $decisionChecks++
  if((Get-InstallDisposition 'v0.2.5') -cne 'Install'){
   throw 'Newer Stable release was rejected.'
  }
  $decisionChecks++
  if((Get-InstallDisposition 'v0.2.3') -cne 'Older'){
   throw 'Stable downgrade candidate was not blocked.'
  }
  $decisionChecks++
  Reject {Get-InstallDisposition 'ci-4124'}
  $decisionChecks++
  $script:Channel='Development'
  $script:channelRoot=Join-Path $testDir 'Development'
  $script:current=Join-Path $script:channelRoot 'current'
  Reset-SyntheticLite
  if((Get-InstallDisposition 'ci-200') -cne 'Current'){
   throw 'Latest Development CI was not treated as installed.'
  }
  $decisionChecks++
  if((Get-InstallDisposition 'ci-201') -cne 'Install'){
   throw 'Newer Development CI was rejected.'
  }
  $decisionChecks++
  if((Get-InstallDisposition 'ci-199') -cne 'Older'){
   throw 'Development CI downgrade candidate was not blocked.'
  }
  $decisionChecks++
  Remove-Item -LiteralPath $script:channelRoot -Recurse -Force
  if((Get-InstallDisposition 'ci-201') -cne 'Install'){
   throw 'First Lite installation was incorrectly blocked.'
  }
  $decisionChecks++
  if($decisionChecks -ne 8){throw 'Wrong same-or-older version gate check count.'}
  Write-Host 'Updater Banner 8/8 same-or-older version gate checks passed.'
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
if($Mode -eq 'SelfTest'){SelfTest;Test-ApplicationHelperHandoff;exit 0}
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
  }elseif($Mode -eq 'Versions'){
   if(-not(Test-Path -LiteralPath $manager -PathType Leaf)){
    throw 'Microsoft dependency version manager is missing.'
   }
   # Prefer the selected, validated channel build for strict runtimeconfig
   # compatibility, never a partially installed or untrusted managed folder.
   $versionTarget=$PSScriptRoot
   $targetLabel='Bundled application (not channel-specific)'
   $strictTargetValid=($script:state.Recovery -ceq 'Ready')
   if(Test-Path -LiteralPath $current){
    try{
     $null=Read-ManagedVersion $current
     if($strictTargetValid){
      $versionTarget=$current
      $targetLabel='Selected '+$Channel+' channel'
     }
    }catch{$strictTargetValid=$false}
   }
   $json=(& $manager -Mode VersionReport -LiteDirectory $versionTarget|Out-String).Trim()
   if(-not $json){throw 'Dependency version report is unavailable.'}
   $report=ConvertFrom-Json -InputObject $json -ErrorAction Stop
   $lines=New-Object Collections.Generic.List[string]
   foreach($key in @('DotNet','DotNetCore','WinApp','VC')){
    $part=$report.$key
    if($null -eq $part){throw 'Incomplete dependency version report.'}
    $label=switch($key){
     'DotNet'{'.NET Desktop x86'}
     'DotNetCore'{'.NET Core x86'}
     'WinApp'{'Windows App Runtime x86'}
     default{'VC++ Redist x86'}
    }
    $readiness=if([bool]$part.Ready){
     'required components detected; strict launch preflight still applies'
    }else{
     'required components not fully verified'
    }
    $latestLabel=if($key -eq 'WinApp'){'latest SDK line'}else{'latest'}
    $lines.Add($label+': '+[string]$part.Installed+
     ' | '+$latestLabel+': '+[string]$part.Latest+
     ' | '+[string]$part.Status+' ('+$readiness+')')
   }
   $strict=$report.StrictPreflight
   if($null -eq $strict){throw 'Strict compatibility result is missing.'}
   if(-not $strictTargetValid){
    $strict.Status='Not verified'
    $strict.Detail='Selected channel needs recovery or its managed installation could not be validated.'
    $targetLabel='Selected '+$Channel+' channel'
   }
   $lines.Add('Strict compatibility ['+$targetLabel+']: '+[string]$strict.Status)
   $lines.Add('Strict check: '+[string]$strict.Detail)
   $lines.Add('WinApp 2.5.1 is the tested installer baseline; newer SDK versions are advisory.')
   $lines.Add('VC++ current installer version cannot be verified without vendor metadata.')
   $script:state.DependencyReport=[string]::Join([Environment]::NewLine,$lines.ToArray())
   Progress 'Microsoft dependency version check finished. No software changed.'
  }elseif($Mode -eq 'Check'){
   if($script:state.Recovery -ne 'Ready'){
    throw 'Recovery must be handled before update checks.'
   }
   $r=Latest;$script:state.Available=Display-Version $r.Tag
   $decision=Get-InstallDisposition $r.Tag
   if($decision -eq 'Current'){
    Progress ('Up to date: '+(Display-Version $r.Tag)+'. No changes made.')
   }elseif($decision -eq 'Older'){
    Progress ('Installed version is newer than the published '+$Channel+
     ' release. Use Restore previous for an intentional rollback.')
   }else{
    Progress ('Latest '+$Channel+' release: '+(Display-Version $r.Tag)+'. No changes made.')
   }
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
   ('Unable to start the selected application: '+$message)
  }elseif($message -match 'Latest published release is older|Cannot safely determine current Lite version'){
   'Update blocked: installed Lite is newer or cannot be verified. No files were downloaded.'
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
$form.Text='Dvinholm | Launcher'
$form.Size=New-Object Drawing.Size(740,604)
$form.MinimumSize=$form.Size;$form.MaximumSize=$form.Size
$form.Font=New-Object Drawing.Font('Segoe UI',10)
$form.StartPosition='CenterScreen'
function Label([string]$value,[int]$top,[int]$height){
 $l=New-Object Windows.Forms.Label;$l.Text=$value;$l.SetBounds(18,$top,695,$height)
 $form.Controls.Add($l);return $l
}
$title=Label 'DVINHOLM   |   UPDATER BANNER' 15 38
$title.Font=New-Object Drawing.Font('Segoe UI',14,[Drawing.FontStyle]::Bold)
$strap=Label 'No .NET 10 or WinApp Runtime needed. Stable and Development install separately.' 52 26
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
$bar.SetBounds(18,286,695,13);$bar.Style='Marquee';$bar.MarqueeAnimationSpeed=0;$bar.Style=[Windows.Forms.ProgressBarStyle]::Continuous
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

# Branded shell: the verified updater engine above remains unchanged.
# Shift the interactive controls under a gradient hero instead of changing
# their event handlers or recovery-locking rules.
$form.BackColor=[Drawing.Color]::FromArgb(10,20,35)
$form.ForeColor=[Drawing.Color]::FromArgb(235,242,248)
$form.MaximizeBox=$false
$form.FormBorderStyle=[Windows.Forms.FormBorderStyle]::FixedSingle
foreach($control in @($form.Controls)){
 $control.Top+=120
 if($control -is [Windows.Forms.Label]){
  $control.ForeColor=[Drawing.Color]::FromArgb(217,230,242)
  $control.BackColor=[Drawing.Color]::Transparent
 }elseif($control -is [Windows.Forms.Button]){
  $control.FlatStyle=[Windows.Forms.FlatStyle]::Flat
  $control.FlatAppearance.BorderSize=1
  $control.FlatAppearance.BorderColor=[Drawing.Color]::FromArgb(70,115,143)
  $control.BackColor=[Drawing.Color]::FromArgb(28,56,76)
  $control.ForeColor=[Drawing.Color]::White
  $control.Cursor=[Windows.Forms.Cursors]::Hand
 }elseif($control -is [Windows.Forms.ComboBox]){
  $control.BackColor=[Drawing.Color]::FromArgb(27,47,65)
  $control.ForeColor=[Drawing.Color]::White
 }
}
$title.Visible=$false;$strap.Visible=$false
$install.BackColor=[Drawing.Color]::FromArgb(20,119,135)
$launch.BackColor=[Drawing.Color]::FromArgb(35,111,89)
$hero=New-Object Windows.Forms.Panel
$hero.SetBounds(0,0,740,142)
$hero.Add_Paint({
 $rect=New-Object Drawing.Rectangle(0,0,740,142)
 $left=[Drawing.Color]::FromArgb(24,74,100)
 $right=[Drawing.Color]::FromArgb(8,27,50)
 $brush=New-Object Drawing.Drawing2D.LinearGradientBrush($rect,$left,$right,0)
 try{$_.Graphics.FillRectangle($brush,$rect)}finally{$brush.Dispose()}
})
$form.Controls.Add($hero)
$brandTitle=New-Object Windows.Forms.Label
$brandTitle.Text='DVINHOLM'
$brandTitle.Font=New-Object Drawing.Font('Segoe UI',27,[Drawing.FontStyle]::Bold)
$brandTitle.ForeColor=[Drawing.Color]::White
$brandTitle.BackColor=[Drawing.Color]::Transparent
$brandTitle.SetBounds(138,30,550,56)
$hero.Controls.Add($brandTitle)
$brandSub=New-Object Windows.Forms.Label
$brandSub.Text='LAUNCHER  /  WINDOWS X86  /  ECU DIAGNOSTICS'
$brandSub.ForeColor=[Drawing.Color]::FromArgb(160,211,224)
$brandSub.BackColor=[Drawing.Color]::Transparent
$brandSub.SetBounds(142,92,570,29)
$hero.Controls.Add($brandSub)
$script:brandImage=$null
$brandFile=Join-Path $PSScriptRoot 'Dvinholm-icon.png'
if(Test-Path -LiteralPath $brandFile -PathType Leaf){
 try{
  $script:brandImage=[Drawing.Image]::FromFile($brandFile)
  $logo=New-Object Windows.Forms.PictureBox
  $logo.SetBounds(24,21,104,100)
  $logo.Image=$script:brandImage
  $logo.SizeMode=[Windows.Forms.PictureBoxSizeMode]::Zoom
  $hero.Controls.Add($logo)
 }catch{$script:brandImage=$null}
}
$foot=Label 'Stable releases  |  Development CI  |  SHA-256 verification  |  Safe rollback' 535 23
$foot.Font=New-Object Drawing.Font('Segoe UI',8)
$foot.ForeColor=[Drawing.Color]::FromArgb(115,166,184)

# The approved Aurora dashboard is a separate UI-only, source-matched module.
$themeFile=Join-Path $PSScriptRoot 'Launcher-AuroraTheme.ps1'
if(-not(Test-Path -LiteralPath $themeFile -PathType Leaf)){
 throw 'Dvinholm Aurora launcher theme file is missing.'
}
. $themeFile
$driverUi=Join-Path $PSScriptRoot 'J2534-DriverUI.ps1'
if(-not(Test-Path -LiteralPath $driverUi -PathType Leaf)){throw 'Driver UI missing'}
. $driverUi

# Explicit online versions check; local Recheck deps remains offline.
$versionsButton=Button 'Check runtime versions' 927 596 215
$versionsButton.SetBounds(918,570,226,30)
$versionsButton.Font=New-Object Drawing.Font('Segoe UI',9)
$versionsButton.BackColor=[Drawing.Color]::FromArgb(25,76,114)
$versionsButton.ForeColor=[Drawing.Color]::White
$versionsButton.FlatStyle=[Windows.Forms.FlatStyle]::Flat
$versionsButton.BringToFront()
# Approved Service Cockpit layout; keeps the existing updater workers intact.
$serviceTheme=Join-Path $PSScriptRoot 'Launcher-ServiceCockpit.ps1'
if(-not(Test-Path -LiteralPath $serviceTheme -PathType Leaf)){
 throw 'Dvinholm Service Cockpit theme missing.'
}
. $serviceTheme
# Fit the complete Service Cockpit dashboard inside the available desktop work area,
# including the Windows taskbar, before showing the fixed-size window.
Set-AuroraCompactLayout

# Localize labels, shortcuts and all settings controls without changing any
# Stable/Development worker tokens or validation rules.
$script:lastCurrent='Not checked'
$script:lastAvailable='Not checked'
$script:lastRecovery='Not checked'
$script:serviceIsReady=$false
function Apply-LauncherLanguage {
 $navHome.Text=Translate 'home'
 $navUpdates.Text=Translate 'updates'
 $navDependencies.Text=Translate 'dependencies'
 $navSettings.Text=Translate 'settings'
 $navAbout.Text=Translate 'about'
 $navDrivers.Text=Translate 'drivers'
 $channelTitle.Text=Translate 'channel'
 $progressTitle.Text=Translate 'progress'
 $dependenciesTitle.Text=Translate 'prereqs'
 $stableRadio.Text=Translate 'stable'
 $developmentRadio.Text=Translate 'dev'
 $installed.Text=(Translate 'installed')+': '+$script:lastCurrent
 $available.Text=(Translate 'available')+': '+$script:lastAvailable
 $recovery.Text=(Translate 'recovery')+': '+$script:lastRecovery
 $check.Text=Translate 'check'
 $install.Text=Translate 'install'
 $launch.Text=Translate 'launch'
 $refresh.Text=Translate 'refresh'
 $versionsButton.Text=Translate 'versions'
 $dot.Text=Translate 'dot'
 $win.Text=Translate 'win'
 $vc.Text=Translate 'vc'
 $rollback.Text=Translate 'rollback'
 $repair.Text=Translate 'repair'
 $repairInstall.Text=Translate 'repairInstall'
 # Service Cockpit controls are attached by Launcher-ServiceCockpit.ps1.
 $serviceHeading.Text='DVINHOLM  ·  '+(Translate $(if($script:serviceIsReady){'cockpitReady'}else{'cockpitUnchecked'}))
 $serviceStatus.Text=Translate 'cockpitCaption'
 $channelLabel.Text=Translate 'channelHeading'
 $releaseHint.Text=Translate 'stableHint'
 $startNote.Text=Translate 'afterLaunch'
 $updateHeading.Text=Translate 'updatesHeading'
 $updateHint.Text=Translate 'updatesHint'
 $windowsHeading.Text=Translate 'windowsHeading'
 $windowsHint.Text=Translate 'windowsHint'
 $driversHeading.Text=Translate 'driversHeading'
 $driversHint.Text=Translate 'driversHint'
 $windowsQuick.Text=Translate 'windowsQuick'
 $depsInstallQuick.Text=Translate 'depsInstall'
 $driverQuick.Text=Translate 'driversOpen'
 $driverScanQuick.Text=Translate 'driversScan'
 $settingsQuick.Text=Translate 'settingsQuick'
 if(-not $script:driversWereProbed){
  $driverStatus.Text=Translate 'driversUnchecked'
 }
 $form.Text='Dvinholm | '+$(if($script:launcherLanguage -eq 'RU'){'Лаунчер'}else{'Launcher'})
}
function Show-LauncherSettings {
 if($script:child -or $script:vendor){return}
 $dlg=New-Object Windows.Forms.Form
 $dlg.Text=Translate 'settingsTitle'
 $dlg.ClientSize=[Drawing.Size]::new(375,313)
 $dlg.FormBorderStyle=[Windows.Forms.FormBorderStyle]::FixedDialog
 $dlg.MaximizeBox=$false;$dlg.MinimizeBox=$false
 $dlg.StartPosition=[Windows.Forms.FormStartPosition]::CenterParent
 $dlg.Font=[Drawing.Font]::new('Segoe UI',10)
 $dlg.BackColor=[Drawing.Color]::FromArgb(12,30,49)
 $dlg.ForeColor=[Drawing.Color]::White
 $heading=New-Object Windows.Forms.Label
 $heading.Text=Translate 'channelSetting'
 $heading.SetBounds(18,18,330,22);$dlg.Controls.Add($heading)
 $channelSelect=New-Object Windows.Forms.ComboBox
 $channelSelect.DropDownStyle='DropDownList'
 [void]$channelSelect.Items.Add('Development')
 [void]$channelSelect.Items.Add('Stable')
 $channelSelect.SelectedIndex=$choice.SelectedIndex
 $channelSelect.SetBounds(18,43,335,28);$dlg.Controls.Add($channelSelect)
 $languageHeading=New-Object Windows.Forms.Label
 $languageHeading.Text=Translate 'language'
 $languageHeading.SetBounds(18,81,330,22);$dlg.Controls.Add($languageHeading)
 $languageSelect=New-Object Windows.Forms.ComboBox
 $languageSelect.DropDownStyle='DropDownList'
 [void]$languageSelect.Items.Add('Русский')
 [void]$languageSelect.Items.Add('English')
 $languageSelect.SelectedIndex=$languageChoice.SelectedIndex
 $languageSelect.SetBounds(18,106,335,28);$dlg.Controls.Add($languageSelect)
 $save=New-Object Windows.Forms.Button
 $save.Text=Translate 'save'
 $save.SetBounds(174,151,88,30);$dlg.Controls.Add($save)
 $cancel=New-Object Windows.Forms.Button
 $cancel.Text=Translate 'cancel'
 $cancel.SetBounds(269,151,84,30);$dlg.Controls.Add($cancel)
 $advanced=New-Object Windows.Forms.Label
 $advanced.Text=if($script:launcherLanguage -eq 'RU'){'ВОССТАНОВЛЕНИЕ'}else{'RECOVERY'}
 $advanced.SetBounds(18,194,340,24);$dlg.Controls.Add($advanced)
 $restore=New-Object Windows.Forms.Button
 $restore.Text=Translate 'rollback'
 $restore.SetBounds(18,226,335,28)
 $restore.Enabled=($script:recoveryStatus -ceq 'Ready')
 $dlg.Controls.Add($restore)
 $repairRollback=New-Object Windows.Forms.Button
 $repairRollback.Text=Translate 'repair'
 $repairRollback.SetBounds(18,263,160,28)
 $repairRollback.Enabled=($script:recoveryStatus -ceq 'Repair rollback')
 $dlg.Controls.Add($repairRollback)
 $repairInstallButton=New-Object Windows.Forms.Button
 $repairInstallButton.Text=Translate 'repairInstall'
 $repairInstallButton.SetBounds(188,263,165,28)
 $repairInstallButton.Enabled=($script:recoveryStatus -ceq 'Repair install')
 $dlg.Controls.Add($repairInstallButton)
 $restore.Add_Click({$dlg.Close();Begin-Action 'Rollback'})
 $repairRollback.Add_Click({$dlg.Close();Begin-Action 'Repair'})
 $repairInstallButton.Add_Click({$dlg.Close();Begin-Action 'RepairInstall'})
 $save.Add_Click({
  if($channelSelect.SelectedIndex -ge 0){$choice.SelectedIndex=$channelSelect.SelectedIndex}
  if($languageSelect.SelectedIndex -ge 0){$languageChoice.SelectedIndex=$languageSelect.SelectedIndex}
  $dlg.Close()
 })
 $cancel.Add_Click({$dlg.Close()})
 try{[void]$dlg.ShowDialog($form)}finally{$dlg.Dispose()}
}
$languageChoice.Add_SelectedIndexChanged({
 if($languageChoice.SelectedIndex -lt 0){return}
 $script:launcherLanguage=if($languageChoice.SelectedIndex -eq 0){'RU'}else{'EN'}
 $null=Save-LauncherLanguage $script:launcherLanguage
 Apply-LauncherLanguage
})
$languageChoice.SelectedIndex=if($script:launcherLanguage -eq 'EN'){1}else{0}
Apply-LauncherLanguage

$script:child=$null;$script:vendor=$null
$script:pendingAction=''
$script:statefile=Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-banner-'+[guid]::NewGuid().ToString('N')+'.json')
$powershell=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$script:recoveryStatus='Not checked'
# Enable installation only after a NEWER build was checked for this channel.
# All transactions still repeat version and signature verification in worker.
function Test-CockpitUpdateAvailable([string]$currentTag,[string]$availableTag,
 [string]$channelName) {
 if($channelName -cnotin @('Stable','Development')){return $false}
 if($channelName -ceq 'Development'){
  if(-not($availableTag -cmatch '^ci-([1-9][0-9]{0,8})$')){return $false}
  $new=[long]$Matches[1]
  if($currentTag -ceq 'Not installed'){return $true}
  if(-not($currentTag -cmatch '^ci-([1-9][0-9]{0,8})$')){return $false}
  return ($new -gt [long]$Matches[1])
 }
 if(-not($availableTag -cmatch '^([0-9]{1,8}\.[0-9]{1,8}\.[0-9]{1,8})$')){return $false}
 $newVersion=[version]$Matches[1]
 if($currentTag -ceq 'Not installed'){return $true}
 if(-not($currentTag -cmatch '^([0-9]{1,8}\.[0-9]{1,8}\.[0-9]{1,8})$')){return $false}
 return ($newVersion -gt [version]$Matches[1])
}
function Update-ActionAvailability {
 $busy=($null -ne $script:child -or $null -ne $script:vendor)
 $state=[string]$script:recoveryStatus
 $ready=($state -ceq 'Ready')
 $choice.Enabled=-not $busy
 $navHome.Enabled=($ready -and -not $busy)
 $navUpdates.Enabled=($ready -and -not $busy)
 $navDependencies.Enabled=-not $busy
 $navSettings.Enabled=-not $busy
 $navAbout.Enabled=-not $busy
 $navDrivers.Enabled=-not $busy
 $languageChoice.Enabled=-not $busy
 $settingsQuick.Enabled=-not $busy
 $windowsQuick.Enabled=-not $busy
 $depsInstallQuick.Enabled=-not $busy
 $driverQuick.Enabled=-not $busy
 $driverScanQuick.Enabled=-not $busy
 $stableRadio.Enabled=-not $busy
 $developmentRadio.Enabled=-not $busy
 $check.Enabled=($ready -and -not $busy)
 $selectedChannel=if($null -ne $choice.SelectedItem){
  [string]$choice.SelectedItem
 }else{[string]$Channel}
 $install.Enabled=($ready -and -not $busy -and
  (Test-CockpitUpdateAvailable $script:lastCurrent $script:lastAvailable
   $selectedChannel))
 $launch.Enabled=($ready -and -not $busy)
 $refresh.Enabled=-not $busy
 $versionsButton.Enabled=-not $busy
 $rollback.Enabled=($ready -and -not $busy)
 $repair.Enabled=($state -ceq 'Repair rollback' -and -not $busy)
 $repairInstall.Enabled=($state -ceq 'Repair install' -and -not $busy)
 foreach($vendorButton in @($dot,$win,$vc)){
  $vendorButton.Enabled=-not $busy
 }
}
function Get-VendorExitError([int]$exitCode){
 if($exitCode -eq 0){return ''}
 return ('Microsoft dependency installer failed or was cancelled (exit code '+
  $exitCode+'). Use Recheck deps before retrying.')
}
function Begin-Action([string]$action){
 if($script:child -or $script:vendor){return}
 if($action -in @('Install','Rollback','Check','Launch') -and
    $script:recoveryStatus -cne 'Ready'){return}
 $selectedChannel=if($null -ne $choice.SelectedItem){
  [string]$choice.SelectedItem
 }else{[string]$Channel}
 if($action -ceq 'Install' -and
    -not(Test-CockpitUpdateAvailable $script:lastCurrent $script:lastAvailable
      $selectedChannel)){return}
 if($action -eq 'RepairInstall'){
  if([Windows.Forms.MessageBox]::Show(
   (Translate 'confirmRepairInstall'),
   (Translate 'confirmTitle'),[Windows.Forms.MessageBoxButtons]::YesNo,
   [Windows.Forms.MessageBoxIcon]::Warning) -ne [Windows.Forms.DialogResult]::Yes){return}
 }
 if($action -eq 'Repair'){
  if([Windows.Forms.MessageBox]::Show(
   (Translate 'confirmRepair'),
   (Translate 'confirmTitle'),[Windows.Forms.MessageBoxButtons]::YesNo,
   [Windows.Forms.MessageBoxIcon]::Warning) -ne [Windows.Forms.DialogResult]::Yes){return}
 }
 if($action -eq 'Rollback'){
  if([Windows.Forms.MessageBox]::Show(
   (Translate 'confirmRollback'),
   (Translate 'confirmTitle'),[Windows.Forms.MessageBoxButtons]::YesNo,
   [Windows.Forms.MessageBoxIcon]::Warning) -ne [Windows.Forms.DialogResult]::Yes){return}
 }
 if($action -eq 'Install'){
  if([Windows.Forms.MessageBox]::Show(
   (Translate 'confirmInstall'),
   (Translate 'confirmTitle'),[Windows.Forms.MessageBoxButtons]::YesNo,
   [Windows.Forms.MessageBoxIcon]::Question) -ne [Windows.Forms.DialogResult]::Yes){return}
 }
 $downloadPercent.Text=''
 if(Test-Path $script:statefile){Remove-Item $script:statefile -Force}
 $script:recoveryStatus='Not checked'
 $script:lastRecovery='Checking'
 $recovery.Text=(Translate 'recovery')+': '+$script:lastRecovery
 Update-ActionAvailability
 $args='-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "'+$PSCommandPath+
  '" -Mode '+$action+' -Channel '+$choice.SelectedItem.ToString()+
  ' -StateFile "'+$script:statefile+'"'
 try{
  $script:child=Start-Process -FilePath $powershell -ArgumentList $args -WindowStyle Hidden -PassThru
  $script:pendingAction=$action
  $bar.Style=[Windows.Forms.ProgressBarStyle]::Marquee;$bar.MarqueeAnimationSpeed=25
  Update-ActionAvailability
  $status.Text='Working...'
 }catch{$script:pendingAction='';$status.Text='Unable to launch updater helper.'}
}
$check.Add_Click({Begin-Action 'Check'})
$refresh.Add_Click({Begin-Action 'Probe'})
$versionsButton.Add_Click({Begin-Action 'Versions'})
$install.Add_Click({Begin-Action 'Install'})
$rollback.Add_Click({Begin-Action 'Rollback'})
$repair.Add_Click({Begin-Action 'Repair'})
$repairInstall.Add_Click({Begin-Action 'RepairInstall'})
$launch.Add_Click({Begin-Action 'Launch'})
function Start-MicrosoftComponent([string]$which) {
 if($script:child -or $script:vendor){return}
 if($which -cnotin @('DotNet','WinApp','VC')){return}
 if([Windows.Forms.MessageBox]::Show($form,
  (Translate 'confirmVendor')+' '+$which,
  (Translate 'confirmTitle'),[Windows.Forms.MessageBoxButtons]::YesNo,
  [Windows.Forms.MessageBoxIcon]::Question) -ne [Windows.Forms.DialogResult]::Yes){return}
 $args='-NoProfile -ExecutionPolicy Bypass -File "'+$manager+'" -Mode Install -Component '+
  $which+' -LiteDirectory "'+$PSScriptRoot+'"'
 try {
  $script:vendor=Start-Process -FilePath $powershell -ArgumentList $args -PassThru
  $status.Text='Microsoft installer opened; follow its prompts.'
  $bar.MarqueeAnimationSpeed=25
  Update-ActionAvailability
 }catch{
  $status.Text='Unable to start Microsoft dependency installer.'
  Update-ActionAvailability
 }
}
foreach($b in @($dot,$win,$vc)){
 $b.Add_Click({Start-MicrosoftComponent ([string]$this.Tag)})
}
# Full prerequisite controls are reachable via a real dialog.
function Show-MicrosoftComponents {
 if($script:child -or $script:vendor){return}
 $dlg=New-Object Windows.Forms.Form
 $dlg.Text=Translate 'componentsTitle'
 $dlg.ClientSize=[Drawing.Size]::new(430,253)
 $dlg.StartPosition=[Windows.Forms.FormStartPosition]::CenterParent
 $dlg.FormBorderStyle=[Windows.Forms.FormBorderStyle]::FixedDialog
 $dlg.MaximizeBox=$false
 $dlg.BackColor=[Drawing.Color]::FromArgb(10,31,52)
 $dlg.ForeColor=[Drawing.Color]::White
 $dlg.Font=[Drawing.Font]::new('Segoe UI',10)
 $intro=New-Object Windows.Forms.Label
 $intro.Text=Translate 'componentsNotice'
 $intro.SetBounds(17,16,394,40)
 $dlg.Controls.Add($intro)
 foreach($entry in @(
  @{Key='DotNet';Caption='.NET 10 Desktop x86';Y=64},
  @{Key='WinApp';Caption='Windows App Runtime x86';Y=119},
  @{Key='VC';Caption='Microsoft VC++ x86';Y=174}
 )){
  $b=New-Object Windows.Forms.Button
  $b.Text=$entry.Caption
  $b.Tag=$entry.Key
  $b.SetBounds(17,[int]$entry.Y,394,42)
  $b.BackColor=[Drawing.Color]::FromArgb(23,77,118)
  $b.ForeColor=[Drawing.Color]::White
  $b.FlatStyle=[Windows.Forms.FlatStyle]::Flat
  $dlg.Controls.Add($b)
  $b.Add_Click({
   $component=[string]$this.Tag
   $dlg.Close()
   Start-MicrosoftComponent $component
  })
 }
 try{[void]$dlg.ShowDialog($form)}finally{$dlg.Dispose()}
}

$choice.Add_SelectedIndexChanged({
 if($null -ne $choice.SelectedItem){
  $null=Save-ChannelPreference ([string]$choice.SelectedItem)
 }
 if(-not $script:child -and -not $script:vendor){
  $script:recoveryStatus='Not checked'
  $script:lastRecovery='Checking'
  $recovery.Text=(Translate 'recovery')+': '+$script:lastRecovery
  Update-ActionAvailability
  Begin-Action 'Probe'
 }
})
function Apply-WorkerSnapshot($snapshot) {
 # Worker state is written atomically but may be read while it is still
 # running. Do not authorize further actions from a partial progress update.
 if($null -eq $snapshot){return $false}
 $script:lastCurrent=[string]$snapshot.Current
 $script:lastAvailable=[string]$snapshot.Available
 $installed.Text=(Translate 'installed')+': '+$script:lastCurrent
 $available.Text=(Translate 'available')+': '+$script:lastAvailable
 $deps.Text='.NET x86: '+[string]$snapshot.DotNet+
  '    WinApp: '+[string]$snapshot.WinApp+'    VC++ x86: '+[string]$snapshot.VC
 $status.Text=[string]$snapshot.Detail
 $downloadPercent.Text=''
 # The ZIP byte count is read from the actual download, never fabricated.
 if($script:child -and [string]$snapshot.DownloadId -cmatch '^[0-9a-f]{32}$' -and
    [long]$snapshot.DownloadTotal -gt 0){
  $selected=[string]$choice.SelectedItem
  if($selected -cin @('Development','Stable')){
   $file=Join-Path (Join-Path (Join-Path $base $selected) ('download-'+$snapshot.DownloadId)) $zipName
   if(Test-Path -LiteralPath $file -PathType Leaf){
    $bytes=(Get-Item -LiteralPath $file).Length
    $percent=[int][Math]::Min(99,[Math]::Max(0,[Math]::Floor(
     100.0*[double]$bytes/[double]$snapshot.DownloadTotal)))
    $bar.Style=[Windows.Forms.ProgressBarStyle]::Continuous
    $bar.Value=$percent
    $downloadPercent.Text=([string]$percent+'%')
   }
  }
 }
 $finished=($snapshot.PSObject.Properties['Done'] -and
            $snapshot.Done -is [bool] -and $snapshot.Done -eq $true)
 $valid=($snapshot.PSObject.Properties['Recovery'] -and
         [string]$snapshot.Recovery -cin @('Ready','Repair install',
                                           'Repair rollback','Inspect'))
 if($finished -and $valid){
  $script:recoveryStatus=[string]$snapshot.Recovery
  $script:serviceIsReady=($script:recoveryStatus -ceq 'Ready' -and
    $script:lastCurrent -cnotin @('Not installed','Not checked','Unverified installation') -and
    [string]$snapshot.DotNet -ceq 'Installed' -and
    [string]$snapshot.WinApp -ceq 'Installed' -and
    [string]$snapshot.VC -ceq 'Installed')
  $serviceHeading.Text='DVINHOLM  ·  '+(Translate $(if($script:serviceIsReady){'cockpitReady'}else{'cockpitUnchecked'}))
  $script:lastRecovery=$script:recoveryStatus
  $recovery.Text=(Translate 'recovery')+': '+$script:lastRecovery
  Update-ActionAvailability
  return $true
 }
 $script:recoveryStatus='Not checked'
 $script:lastRecovery='Checking'
 $recovery.Text=(Translate 'recovery')+': '+$script:lastRecovery
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
  $reportText=''
  if($complete -and $null -ne $snapshot -and
     $snapshot.PSObject.Properties['DependencyReport']){
   $reportText=[string]$snapshot.DependencyReport
  }
  # Close only when launch worker completed successfully AND the dependency
  # helper positively reported creation of the main application process.
  $finishedAction=$script:pendingAction
  $workerExit=[int]$script:child.ExitCode
  $script:pendingAction=''
  $launchSucceeded=($finishedAction -ceq 'Launch' -and $workerExit -eq 0 -and
    $complete -and $null -ne $snapshot -and
    $snapshot.Done -eq $true -and $snapshot.Failed -eq $false)
  $script:child.Dispose();$script:child=$null;$bar.MarqueeAnimationSpeed=0;$bar.Style=[Windows.Forms.ProgressBarStyle]::Continuous
  if(-not $complete){
   $script:recoveryStatus='Not checked'
   $recovery.Text='Recovery: not checked'
   $status.Text='Updater helper stopped without a complete status. Use Recheck deps.'
  }
  Update-ActionAvailability
  if($launchSucceeded){
   $timer.Stop()
   $form.Close()
   return
  }
  if($reportText){
   [void][Windows.Forms.MessageBox]::Show($form,$reportText,
    'Dvinholm | Installed and available runtimes',
    [Windows.Forms.MessageBoxButtons]::OK,
    [Windows.Forms.MessageBoxIcon]::Information)
  }
 }
 if($script:vendor -and $script:vendor.HasExited){
  $exitCode=[int]$script:vendor.ExitCode
  $fromDriverInstaller=($script:driverInstallerPending -eq $true)
  $script:driverInstallerPending=$false
  $downloadPercent.Text=''
  $script:vendor.Dispose();$script:vendor=$null;$bar.MarqueeAnimationSpeed=0;$bar.Style=[Windows.Forms.ProgressBarStyle]::Continuous
  # A completed installer must not have its outcome overwritten by an old
  # updater worker snapshot on the next 500ms timer tick.
  if(Test-Path -LiteralPath $script:statefile -PathType Leaf){
   Remove-Item -LiteralPath $script:statefile -Force -ErrorAction SilentlyContinue
  }
  if($fromDriverInstaller){
   # A vendor setup is independent of .NET/WinApp prerequisites. Do not
   # report driver success by running the generic updater dependency probe.
   # Rescan only Windows PnP/registry. Never load the adapter or request CAN.
   if($exitCode -eq 0){
    try{
     if($script:driverRescan){
      & $script:driverRescan
      $status.Text='J2534 installer finished. Driver inventory refreshed; hardware untested.'
     }else{
      $status.Text='Vendor installer finished. Open Drivers and select Check to verify.'
     }
    }catch{
     $status.Text='Vendor setup finished; driver inventory could not be refreshed.'
    }
   }elseif($exitCode -in @(3010,1641)){
    # MSI success requiring reboot is not failure, but the adapter is
    # not ready until Windows restarts. Do not rescan as a success.
    $status.Text='Vendor setup finished. Restart Windows before using the driver (exit '+$exitCode+').'
    if($script:driverResultControl -and -not $script:driverResultControl.IsDisposed){
     $script:driverResultControl.Text='Windows restart required. Recheck adapter drivers after restarting Windows.'
    }
   }else{
    $status.Text='Vendor driver installer failed or was cancelled (exit '+$exitCode+').'
    if($script:driverResultControl -and -not $script:driverResultControl.IsDisposed){
     $script:driverResultControl.Text='Vendor driver setup did not complete. Check the vendor installer log.'
    }
   }
   Update-ActionAvailability
  }elseif($exitCode -eq 0){
   # Only plain success permits automatic local dependency rescan.
   Begin-Action 'Probe'
  }elseif($exitCode -in @(3010,1641)){
   # Microsoft dependency helper preserved a success-with-restart code.
   # No automatic re-probe or ready status until Windows is restarted.
   $script:recoveryStatus='Not checked'
   $recovery.Text='Recovery: not checked'
   $status.Text='Microsoft runtime installed. Restart Windows before rechecking prerequisites (exit '+$exitCode+').'
   Update-ActionAvailability
  }else{
   # Preserve failure without launching an unrelated dependency probe.
   $script:recoveryStatus='Not checked'
   $recovery.Text='Recovery: not checked'
   $status.Text=Get-VendorExitError $exitCode
   Update-ActionAvailability
  }
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
  if($install.Enabled -or -not $check.Enabled -or
     -not $launch.Enabled -or -not $rollback.Enabled -or
     $repair.Enabled -or $repairInstall.Enabled){
   throw 'Ready-state button policy failed: install must await a version check.'
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
   Available='ci-4119';DotNet='Installed';WinApp='Installed'
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
  $vendorChecks=0
  $script:recoveryStatus='Ready';Update-ActionAvailability
  if(-not $dot.Enabled -or -not $win.Enabled -or -not $vc.Enabled){
   throw 'Microsoft installer buttons were disabled when no worker was running.'
  }
  $vendorChecks++
  $script:child=[pscustomobject]@{HasExited=$false}
  Update-ActionAvailability
  if($dot.Enabled -or $win.Enabled -or $vc.Enabled){
   throw 'Microsoft installers were not locked during updater work.'
  }
  $vendorChecks++
  $script:child=$null;Update-ActionAvailability
  if(-not $dot.Enabled -or -not $win.Enabled -or -not $vc.Enabled){
   throw 'Microsoft installer buttons did not unlock after updater completion.'
  }
  $vendorChecks++
  $script:vendor=[pscustomobject]@{HasExited=$false}
  Update-ActionAvailability
  if($dot.Enabled -or $win.Enabled -or $vc.Enabled -or
     $choice.Enabled -or $refresh.Enabled -or $launch.Enabled -or
     $install.Enabled){
   throw 'Parallel Microsoft installer activity was not locked.'
  }
  $vendorChecks++
  $script:vendor=$null;Update-ActionAvailability
  if(-not $dot.Enabled -or -not $win.Enabled -or -not $vc.Enabled){
   throw 'Microsoft installer buttons stayed disabled after worker completion.'
  }
  $vendorChecks++
  if((Get-VendorExitError 0) -cne ''){
   throw 'Successful Microsoft installer completion reported a failure.'
  }
  $vendorChecks++
  if((Get-VendorExitError 1) -cnotmatch 'failed or was cancelled.*exit code 1'){
   throw 'Failed Microsoft installer completion was not explained.'
  }
  $vendorChecks++
  if($vendorChecks -ne 7){throw 'Wrong Microsoft installer lifecycle test count.'}
  Write-Host 'Updater Banner 7/7 WinForms vendor worker lifecycle checks passed.'

  # Eight deterministic update-button gates, no network or installers.
  $updates=@(
   @('Development','Not installed','Not checked',$false),
   @('Development','Not installed','ci-4119',$true),
   @('Development','ci-4118','ci-4119',$true),
   @('Development','ci-4119','ci-4119',$false),
   @('Development','ci-4120','ci-4119',$false),
   @('Stable','0.2.3','0.2.4',$true),
   @('Stable','0.2.4','0.2.4',$false),
   @('Stable','Unverified installation','0.2.4',$false)
  )
  $updateChecks=0
  foreach($item in $updates){
   $actual=Test-CockpitUpdateAvailable ([string]$item[1]) ([string]$item[2]) ([string]$item[0])
   if($actual -ne [bool]$item[3]){
    throw ('Wrong update availability for '+($item[0..2] -join ' / '))
   }
   $updateChecks++
  }
  if($updateChecks -ne 8){throw 'Wrong update gate check count.'}
  Write-Host 'Service Cockpit update buttons 8/8 offline state checks passed.'

  # Eight Windows-only GUI contracts for the approved Service Cockpit.
  # No modal window, network request, installer, or ECU access.
  $auroraChecks=0
  if($script:auroraScale -gt 0.80 -or $script:auroraScale -lt 0.65 -or
     $form.ClientSize.Width -gt 944 -or $form.ClientSize.Height -gt 552){
   throw 'Service Cockpit is larger than the compact display envelope.'
  }
  $auroraChecks++
  if($hero.Width -ne [int][Math]::Round(1180*$script:auroraScale) -or
     $hero.Height -ne [int][Math]::Round(184*$script:auroraScale) -or
     $brandTitle.Text -cne 'DVINHOLM'){
   throw 'Service Cockpit upper banner is missing or not correctly scaled.'
  }
  $auroraChecks++
  if($choice.Parent -ne $form -or
     $null -eq $script:serviceCardLeft -or
     $sidebar.Controls.Count -lt 6 -or $stableRadio.Parent -ne $form){
   throw 'Service Cockpit root selection or backing layout is missing.'
  }
  $auroraChecks++
  if($script:serviceCardUpdates -eq $null -or
     $script:serviceCardWindows -eq $null -or
     $script:serviceCardDrivers -eq $null -or
     $script:serviceCardUpdates.Top -ge $script:serviceCardWindows.Top -or
     $script:serviceCardWindows.Top -ge $script:serviceCardDrivers.Top){
   throw 'Service Cockpit three operation cards are missing or out of order.'
  }
  $auroraChecks++
  $contentWidth=[int][Math]::Round(1180*$script:auroraScale)
  $contentHeight=[int][Math]::Round(690*$script:auroraScale)
  if($install.Right -gt $contentWidth -or
     $versionsButton.Right -gt $contentWidth -or
     $driverQuick.Right -gt $contentWidth -or
     $script:serviceCardDrivers.Bottom -gt $contentHeight -or
     $bar.Width -lt [int][Math]::Floor(650*$script:auroraScale)){
   throw 'Service Cockpit controls extend past the display boundary.'
  }
  $auroraChecks++
  if($launch.Bottom -ge $recovery.Top -or
     $recovery.Bottom -ge $startNote.Top){
   throw 'Service Cockpit Start button overlaps its recovery or help labels.'
  }
  $working=[Windows.Forms.Screen]::FromControl($form).WorkingArea
  if($form.Width -gt $working.Width-16 -or $form.Height -gt $working.Height-16){
   throw 'Service Cockpit extends beyond the Windows working area.'
  }
  $auroraChecks++
  $currentLanguage=$script:launcherLanguage
  $script:launcherLanguage='EN';Apply-LauncherLanguage
  if($windowsHeading.Text -cne 'Windows requirements' -or
     $driverQuick.Text -cne 'Open drivers'){throw 'English localization did not apply.'}
  $script:launcherLanguage='RU';Apply-LauncherLanguage
  if($windowsHeading.Text -cne 'Требования Windows' -or
     $driverQuick.Text -cne 'Открыть драйверы'){throw 'Russian localization did not apply.'}
  $script:launcherLanguage=$currentLanguage;Apply-LauncherLanguage
  $auroraChecks++
  $script:child=[pscustomobject]@{HasExited=$false}
  Update-ActionAvailability
  if($versionsButton.Enabled -or $windowsQuick.Enabled -or
     $driverQuick.Enabled -or $driverScanQuick.Enabled -or
     $settingsQuick.Enabled -or $languageChoice.Enabled){
   throw 'Service Cockpit actions were not locked during updater operation.'
  }
  $script:child=$null
  Update-ActionAvailability
  if(-not $windowsQuick.Enabled -or -not $driverQuick.Enabled){
   throw 'Service Cockpit actions did not recover after updater operation.'
  }
  $auroraChecks++
  if($auroraChecks -ne 8){throw 'Wrong Service Cockpit smoke check count.'}
  Write-Host 'Service Cockpit 8/8 Windows WinForms layout and action smoke checks passed.'
  Write-Host 'Aurora Launcher 8/8 Windows WinForms compact theme smoke checks passed.'

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
