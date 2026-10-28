#requires -Version 5.1
<#
Standalone Windows 11 updater. Only inbox PowerShell 5.1 and .NET Framework
WinForms are needed; .NET 10, Windows App SDK and VC x86 are NOT needed to run.
No software installs or updates until the user explicitly confirms.
#>
[CmdletBinding()]
param(
 [ValidateSet('UI','Check','Install','SelfTest')][string]$Mode='UI',
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
$channelRoot=Join-Path $base $Channel
$current=Join-Path $channelRoot 'current'
$state=[ordered]@{Done=$false;Failed=$false;Current='Not installed';Available='Not checked';
 DotNet='Unknown';WinApp='Unknown';VC='Unknown';Detail='Ready'}
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
function Installed {
 $file=Join-Path $current 'updater-banner.json'
 if(-not(Test-Path $file -PathType Leaf)){return 'Not installed'}
 try{
  $m=Get-Content $file -Raw|ConvertFrom-Json
  if($m.Channel -eq $Channel -and [string]$m.Tag -match '^(v\d+\.\d+\.\d+|ci-\d+)$'){
   return [string]$m.Tag
  }
 }catch{}
 return 'Unverified installation'
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
function Install {
 if(Get-Process -Name 'Dvinholm.App' -ErrorAction SilentlyContinue){
  throw 'Close the application before update.'
 }
 $r=Latest
 $script:state.Available=$r.Tag
 Progress 'Downloading GitHub Lite ZIP and SHA256SUMS...'
 New-Item -ItemType Directory -Path $channelRoot -Force | Out-Null
 Assert-ManagedFolder $base;Assert-ManagedFolder $channelRoot
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
  Assert-ManagedFolder $previous
  if(Test-Path $previous){Remove-Item $previous -Recurse -Force}
  if(Test-Path $current){Move-Item $current $previous;$movedOld=$true}
  try{Move-Item $pending $current}
  catch{
   if($movedOld -and -not(Test-Path $current)){Move-Item $previous $current}
   throw
  }
  $script:state.Current=$r.Tag
  Progress ('Installed '+$r.Tag+'; previous version retained for rollback.')
 }finally{
  if(Test-Path $downloads){Remove-Item $downloads -Recurse -Force -ErrorAction SilentlyContinue}
  if(Test-Path $pending){Remove-Item $pending -Recurse -Force -ErrorAction SilentlyContinue}
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
}
if($Mode -eq 'SelfTest'){SelfTest;exit 0}
if($Mode -ne 'UI'){
 try{
  $script:state.Current=Installed
  $inventory=Probe
  foreach($key in @('DotNet','WinApp','VC')){$script:state[$key]=$inventory[$key]}
  Save-State
  if($Mode -eq 'Check'){
   $r=Latest;$script:state.Available=$r.Tag
   Progress ('Latest '+$Channel+' release: '+$r.Tag+'. No changes made.')
  }else{Install}
  $script:state.Done=$true;Save-State;exit 0
 }catch{
  $message=[string]$_.Exception.Message
  $script:state.Detail=if($message -match 'prerequisite|runtime|dependency|SDK|VC|Desktop'){
   'Missing Lite prerequisites. Install/update Microsoft components, then retry.'
  }elseif($message -match 'Close the application'){
   'Close Dvinholm, then retry. Previous files were preserved.'
  }elseif($message -match 'SHA|ZIP|digest|manifest|archive|asset|unsafe'){
   'Release integrity/security check failed. Nothing was installed.'
  }else{'Update/check failed. Existing application files were not modified.'}
  $script:state.Failed=$true;$script:state.Done=$true;Save-State;exit 1
 }
}
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$form=New-Object Windows.Forms.Form
$form.Text='Dvinholm Updater Banner'
$form.Size=New-Object Drawing.Size(740,410)
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
$installed=Label 'Installed: not checked' 126 25
$available=Label 'Available: not checked' 151 25
$deps=Label '.NET x86: ?    WinApp: ?    VC++ x86: ?' 180 26
$status=Label 'Ready.' 206 46
$bar=New-Object Windows.Forms.ProgressBar
$bar.SetBounds(18,257,695,13);$bar.Style='Marquee';$bar.MarqueeAnimationSpeed=0
$form.Controls.Add($bar)
function Button([string]$value,[int]$x,[int]$y,[int]$w){
 $b=New-Object Windows.Forms.Button;$b.Text=$value;$b.SetBounds($x,$y,$w,34)
 $form.Controls.Add($b);return $b
}
$check=Button 'Check updates' 18 282 158
$install=Button 'Download / Install' 184 282 185
$launch=Button 'Start Dvinholm' 377 282 159
$refresh=Button 'Recheck deps' 544 282 169
$dot=Button 'Install .NET x86' 18 326 218;$dot.Tag='DotNet'
$win=Button 'Install WinApp' 252 326 218;$win.Tag='WinApp'
$vc=Button 'Install VC++ x86' 486 326 227;$vc.Tag='VC'
$script:child=$null;$script:vendor=$null
$script:statefile=Join-Path ([IO.Path]::GetTempPath()) ('dvinholm-banner-'+[guid]::NewGuid().ToString('N')+'.json')
$powershell=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
function Begin-Action([string]$action){
 if($script:child -or $script:vendor){return}
 if($action -eq 'Install'){
  if([Windows.Forms.MessageBox]::Show(
   'Download the selected Lite ZIP, verify checksums and install into an isolated channel? Existing manual installations will not be changed.',
   'Confirm update',[Windows.Forms.MessageBoxButtons]::YesNo,
   [Windows.Forms.MessageBoxIcon]::Question) -ne [Windows.Forms.DialogResult]::Yes){return}
 }
 if(Test-Path $script:statefile){Remove-Item $script:statefile -Force}
 $args='-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "'+$PSCommandPath+
  '" -Mode '+$action+' -Channel '+$choice.SelectedItem.ToString()+
  ' -StateFile "'+$script:statefile+'"'
 try{
  $script:child=Start-Process -FilePath $powershell -ArgumentList $args -WindowStyle Hidden -PassThru
  $bar.MarqueeAnimationSpeed=25
  $choice.Enabled=$false;$install.Enabled=$false;$check.Enabled=$false
  $status.Text='Working...'
 }catch{$status.Text='Unable to launch updater helper.'}
}
$check.Add_Click({Begin-Action 'Check'})
$refresh.Add_Click({Begin-Action 'Check'})
$install.Add_Click({Begin-Action 'Install'})
$launch.Add_Click({
 $target=Join-Path (Join-Path (Join-Path $base $choice.SelectedItem.ToString()) 'current') 'Start-Dvinholm-Lite.cmd'
 if(-not(Test-Path $target -PathType Leaf)){
  [Windows.Forms.MessageBox]::Show('Channel not installed; choose Download / Install.',
   'No managed installation')|Out-Null
 }else{Start-Process -FilePath $target -WorkingDirectory (Split-Path $target -Parent)}
})
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
$choice.Add_SelectedIndexChanged({if(-not $script:child){Begin-Action 'Check'}})
$timer=New-Object Windows.Forms.Timer;$timer.Interval=500
$timer.Add_Tick({
 if(Test-Path $script:statefile){
  try{
   $s=Get-Content $script:statefile -Raw|ConvertFrom-Json
   $installed.Text='Installed: '+$s.Current
   $available.Text='Available: '+$s.Available
   $deps.Text='.NET x86: '+$s.DotNet+'    WinApp: '+$s.WinApp+'    VC++ x86: '+$s.VC
   $status.Text=[string]$s.Detail
  }catch{}
 }
 if($script:child -and $script:child.HasExited){
  $script:child.Dispose();$script:child=$null;$bar.MarqueeAnimationSpeed=0
  $choice.Enabled=$true;$install.Enabled=$true;$check.Enabled=$true
 }
 if($script:vendor -and $script:vendor.HasExited){
  $script:vendor.Dispose();$script:vendor=$null;$bar.MarqueeAnimationSpeed=0
  Begin-Action 'Check'
 }
})
$timer.Start()
$form.Add_Shown({$choice.SelectedIndex=if($Channel -eq 'Stable'){1}else{0}})
try{[void]$form.ShowDialog()}
finally{
 $timer.Stop();$timer.Dispose()
 if($script:child){$script:child.Dispose()}
 if($script:vendor){$script:vendor.Dispose()}
 if(Test-Path $script:statefile){Remove-Item $script:statefile -Force -ErrorAction SilentlyContinue}
 $form.Dispose()
}