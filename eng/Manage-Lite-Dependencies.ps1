#requires -Version 5.1
<#
  Standalone Dvinholm Lite dependency manager (Windows PowerShell / .NET Framework).
  Runs BEFORE the WinUI/.NET 10 app when started with Start-Dvinholm-Lite.cmd.
  Download/install ONLY after an explicit click. Official signed installers.
#>
[CmdletBinding()]
param(
    [ValidateSet('Launch','Manage','Install','Probe','VersionReport','SelfTest')][string]$Mode = 'Manage',
    [ValidateSet('DotNet','WinApp','VC')][string]$Component = 'WinApp',
    [string]$LiteDirectory = $PSScriptRoot
)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$root = (Resolve-Path -LiteralPath $LiteDirectory -ErrorAction Stop).Path
$checker = Join-Path $root 'Check-Lite-Prerequisites.ps1'
$app = Join-Path $root 'Dvinholm.App.exe'
# The standalone Updater Banner can probe or install prerequisites before
# ANY Dvinholm files exist. Launch still requires the full application.
if ($Mode -eq 'Launch' -and
    (-not (Test-Path -LiteralPath $checker -PathType Leaf) -or
     -not (Test-Path -LiteralPath $app -PathType Leaf))) {
    throw 'The complete Dvinholm Lite distribution must be present beside the dependency manager.'
}

function Test-Ready {
    try {
        # This source-matched checker is the FINAL gate, not the status labels.
        $output = @(& $checker -LiteDirectory $root -WindowsAppSdkLine '2.5' 2>&1)
        return [pscustomobject]@{ Ready = $true; Reason = 'All Lite prerequisites passed.' }
    } catch {
        return [pscustomobject]@{ Ready = $false; Reason = [string]$_.Exception.Message }
    }
}

function Get-Inventory {
    $dotnet = $false
    $sdk = $false
    $vc = $false
    try {
        $pf = if ([Environment]::Is64BitOperatingSystem) {
            [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
        } else { [Environment]::GetEnvironmentVariable('ProgramFiles') }
        $hostExe = Join-Path $pf 'dotnet\dotnet.exe'
        if (Test-Path -LiteralPath $hostExe -PathType Leaf) {
            $runtimes = @(& $hostExe --list-runtimes 2>$null)
            $dotnet = (@($runtimes | Where-Object {
                $_ -match '^Microsoft\.WindowsDesktop\.App\s+10\.\d+\.\d+\s+\['
            }).Count -gt 0)
        }
    } catch { $dotnet = $false }
    try {
        $packages = @(Get-AppxPackage -Name '*AppRuntime*' -ErrorAction Stop)
        $arch = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
        $framework = @($packages | Where-Object {
            $_.Name -eq 'Microsoft.WindowsAppRuntime.2' -and
            $_.Architecture.ToString() -eq 'X86' -and
            [version]$_.Version -ge [version]'2.5.0.0'
        }).Count -gt 0
        $main = @($packages | Where-Object {
            $_.Name -eq 'MicrosoftCorporationII.WinAppRuntime.Main.2' -and
            $_.Architecture.ToString() -eq $arch -and
            [version]$_.Version -ge [version]'2.5.0.0'
        }).Count -gt 0
        $singleton = @($packages | Where-Object {
            $_.Name -eq 'MicrosoftCorporationII.WinAppRuntime.Singleton' -and
            $_.Architecture.ToString() -eq $arch -and
            [version]$_.Version -ge [version]'8002.5.0.0'
        }).Count -gt 0
        $ddlm = @($packages | Where-Object {
            $_.Name -match '^Microsoft\.WinAppRuntime\.DDLM\.2\.5\.' -and
            $_.Architecture.ToString() -eq 'X86'
        }).Count -gt 0
        $sdk = ($framework -and $main -and $singleton -and $ddlm)
    } catch { $sdk = $false }
    try {
        $views = @([Microsoft.Win32.RegistryView]::Registry32)
        if ([Environment]::Is64BitOperatingSystem) {
            $views += [Microsoft.Win32.RegistryView]::Registry64
        }
        foreach ($view in $views) {
            $hive = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
                [Microsoft.Win32.RegistryHive]::LocalMachine, $view)
            try {
                $key = $hive.OpenSubKey('SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x86')
                if ($null -ne $key) {
                    try {
                        if ([int]$key.GetValue('Installed',0) -eq 1 -and
                            [int]$key.GetValue('Major',0) -ge 14) { $vc = $true }
                    } finally { $key.Dispose() }
                }
            } finally { $hive.Dispose() }
        }
    } catch { $vc = $false }
    return [pscustomobject]@{ DotNet=$dotnet; WinApp=$sdk; VC=$vc }
}

# Read-only installed version inventory, independent of startup preflight.
function Get-InstalledComponentVersions {
 $result=@{DotNet='Not detected';WinApp='Not detected';VC='Not detected'}
 try {
  $pf=if([Environment]::Is64BitOperatingSystem){
   [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
  }else{[Environment]::GetEnvironmentVariable('ProgramFiles')}
  if($pf){
   $hostExe=Join-Path $pf 'dotnet\dotnet.exe'
   if(Test-Path -LiteralPath $hostExe -PathType Leaf){
    $lines=@(& $hostExe --list-runtimes 2>$null)
    if($LASTEXITCODE -eq 0){
     $found=@($lines|ForEach-Object{
      if($_ -match '^Microsoft\.WindowsDesktop\.App\s+(10\.\d+\.\d+)\s+\['){
       [version]$Matches[1]
      }
     }|Sort-Object -Descending)
     if($found.Count){$result.DotNet=$found[0].ToString()}
    }else{$result.DotNet='Unknown'}
   }
  }else{$result.DotNet='Unknown'}
 }catch{$result.DotNet='Unknown'}
 try{
  $packages=@(Get-AppxPackage -Name 'Microsoft.WindowsAppRuntime.2' -ErrorAction Stop |
   Where-Object{$_.Name -eq 'Microsoft.WindowsAppRuntime.2' -and
    $_.Architecture.ToString() -eq 'X86'} |
   ForEach-Object{[version]$_.Version}|Sort-Object -Descending)
  if($packages.Count){$result.WinApp=$packages[0].ToString()}
 }catch{$result.WinApp='Unknown'}
 try{
  $versions=New-Object Collections.Generic.List[version]
  $views=@([Microsoft.Win32.RegistryView]::Registry32)
  if([Environment]::Is64BitOperatingSystem){
   $views+=[Microsoft.Win32.RegistryView]::Registry64
  }
  foreach($view in $views){
   $hive=[Microsoft.Win32.RegistryKey]::OpenBaseKey(
     [Microsoft.Win32.RegistryHive]::LocalMachine,$view)
   try{
    $key=$hive.OpenSubKey('SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x86')
    if($null -ne $key){
     try{
      if([int]$key.GetValue('Installed',0) -eq 1){
       $value=[string]$key.GetValue('Version','')
       if($value -match '^v?(14\.\d+\.\d+(?:\.\d+)?)
# This path does not read a runtimeconfig, launch WinUI or install software.
if ($Mode -eq 'Probe') {
    $inventory = Get-Inventory
    foreach ($item in @('DotNet','WinApp','VC')) {
        $key = if ($item -eq 'DotNet') { 'DOTNET' } else { $item.ToUpperInvariant() }
        $state = if ($inventory.$item) { 'READY' } else { 'MISSING' }
        Write-Output "$key=$state"
    }
    return
}

function Select-DotNetInstaller($index) {
    # Pure selector: test Microsoft release metadata offline, before making
    # any installer download, Authenticode check or process launch.
    if ($null -eq $index) { throw 'Missing Microsoft .NET release metadata.' }
    $latest = [string]$index.'latest-release'
    if ($latest -cnotmatch '^10\.\d+\.\d+$') {
        throw 'Microsoft .NET 10 release metadata has no stable current version.'
    }
    $matching = @($index.releases | Where-Object {
        [string]$_.'release-version' -ceq $latest
    })
    if ($matching.Count -ne 1) {
        throw 'Cannot select one current .NET 10 release from official metadata.'
    }
    $files = @($matching[0].windowsdesktop.files | Where-Object {
        [string]$_.rid -ceq 'win-x86' -and
        [string]$_.name -ceq 'windowsdesktop-runtime-win-x86.exe'
    })
    if ($files.Count -ne 1) {
        throw 'Cannot select a unique Microsoft .NET Desktop x86 installer.'
    }
    $file = $files[0]
    $uri = [uri]([string]$file.url)
    if ($uri.Scheme -cne 'https' -or $uri.UserInfo -or $uri.Port -ne 443 -or
        $uri.Host -cnotin @('builds.dotnet.microsoft.com',
                           'download.visualstudio.microsoft.com',
                           'dotnetcli.azureedge.net',
                           'dotnetcli.blob.core.windows.net')) {
        throw 'The .NET release index references an unexpected installer host.'
    }
    if ([string]$file.hash -cnotmatch '^[a-fA-F0-9]{128}$') {
        throw 'The official .NET Desktop installer has no usable SHA-512 digest.'
    }
    return [pscustomobject]@{
        Url = $uri.AbsoluteUri
        Hash = ([string]$file.hash).ToUpperInvariant()
    }
}
function Test-InstallerMetadataOffline {
    $count = 0
    function Reject([scriptblock]$action) {
        $rejected = $false
        try { & $action | Out-Null } catch { $rejected = $true }
        if (-not $rejected) { throw 'Unsafe .NET installer metadata was accepted.' }
    }
    function Fixture {
        $file = [pscustomobject]@{
            rid='win-x86'
            name='windowsdesktop-runtime-win-x86.exe'
            url='https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.10/windowsdesktop-runtime-10.0.10-win-x86.exe'
            hash=('ab'*64)
        }
        return [pscustomobject]@{
            'latest-release'='10.0.10'
            releases=@([pscustomobject]@{
                'release-version'='10.0.10'
                windowsdesktop=[pscustomobject]@{files=@($file)}
            })
        }
    }
    $valid = Fixture
    $selected = Select-DotNetInstaller $valid
    if ($selected.Hash -cne ('AB'*64) -or
        $selected.Url -cne $valid.releases[0].windowsdesktop.files[0].url) {
        throw 'Valid Microsoft .NET Desktop x86 installer selection failed.'
    }
    $count++
    $invalid = Fixture; $invalid.'latest-release'='11.0.0'
    Reject {Select-DotNetInstaller $invalid};$count++
    $invalid = Fixture; $invalid.'latest-release'='10.0.11-preview'
    Reject {Select-DotNetInstaller $invalid};$count++
    $invalid = Fixture
    $invalid.releases=@($invalid.releases[0],$invalid.releases[0])
    Reject {Select-DotNetInstaller $invalid};$count++
    $invalid = Fixture
    $invalid.releases[0].windowsdesktop.files[0].rid='win-x64'
    Reject {Select-DotNetInstaller $invalid};$count++
    $invalid = Fixture
    $file = $invalid.releases[0].windowsdesktop.files[0]
    $invalid.releases[0].windowsdesktop.files=@($file,$file)
    Reject {Select-DotNetInstaller $invalid};$count++
    $invalid = Fixture
    $invalid.releases[0].windowsdesktop.files[0].url='https://untrusted.example/runtime.exe'
    Reject {Select-DotNetInstaller $invalid};$count++
    $invalid = Fixture
    $invalid.releases[0].windowsdesktop.files[0].hash='invalid'
    Reject {Select-DotNetInstaller $invalid};$count++
    if ($count -ne 8) { throw 'Wrong .NET installer metadata test count.' }
    Write-Host 'Lite dependency manager 8/8 offline installer metadata checks passed.'
}
if ($Mode -eq 'SelfTest') {
    Test-InstallerMetadataOffline
    Test-DependencyVersionLogic
    exit 0
}

function Get-OfficialInstaller {
    param([string]$Name)
    if ($Name -eq 'WinApp') {
        # Main and Singleton match the OS architecture; x64 runtime installer
        # also provisions the x86 Framework/DDLM needed by the x86 app.
        $nativeArch = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
        if ($nativeArch -notin @('X86','X64','Arm64')) {
            throw 'Unsupported Windows architecture for the official runtime installer.'
        }
        $suffix = $nativeArch.ToLowerInvariant()
        return [pscustomobject]@{
            Url = "https://aka.ms/windowsappsdk/2.5/2.5.1/windowsappruntimeinstall-$suffix.exe"
            Hash = $null
        }
    }
    if ($Name -eq 'VC') {
        return [pscustomobject]@{
            Url = 'https://aka.ms/vc14/vc_redist.x86.exe'
            Hash = $null
        }
    }
    if ($Name -ne 'DotNet') { throw 'Unknown dependency.' }

    # Microsoft official .NET release metadata binds each installer to SHA-512.
    $indexUrl = 'https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/10.0/releases.json'
    $index = Invoke-RestMethod -Uri $indexUrl -UseBasicParsing -TimeoutSec 40
    return (Select-DotNetInstaller $index)
}

function Install-Component {
    param([string]$Name)
    $source = Get-OfficialInstaller -Name $Name
    $temp = Join-Path ([IO.Path]::GetTempPath()) ('Dvinholm-Microsoft-' + [guid]::NewGuid().ToString('N'))
    $null = New-Item -ItemType Directory -Path $temp
    $exe = Join-Path $temp 'MicrosoftDependencySetup.exe'
    try {
        Write-Host "Downloading the official $Name installer..." -ForegroundColor Cyan
        Invoke-WebRequest -Uri $source.Url -UseBasicParsing -OutFile $exe -TimeoutSec 240
        $length = (Get-Item -LiteralPath $exe).Length
        if ($length -lt 65536 -or $length -gt 350MB) {
            throw 'Downloaded installer size is outside allowed limits.'
        }
        if ($null -ne $source.Hash) {
            $actual = (Get-FileHash -LiteralPath $exe -Algorithm SHA512).Hash
            if ($actual -ne $source.Hash) {
                throw 'The Microsoft release-metadata SHA-512 verification failed.'
            }
        }
        $signature = Get-AuthenticodeSignature -LiteralPath $exe
        if ($signature.Status -ne 'Valid' -or
            $null -eq $signature.SignerCertificate -or
            $signature.SignerCertificate.Subject -notmatch '(?i)(^|,\s*)CN=Microsoft Corporation(,|$)') {
            throw 'Installer is not authentically signed by Microsoft Corporation.'
        }
        Write-Host 'Microsoft digital signature verified. Launching the official installer...' -ForegroundColor Green
        # The vendor installer owns its UAC elevation and visible UI. No /quiet
        # and no forced package-state change without a user click.
        $installer = Start-Process -FilePath $exe -PassThru -Wait
        if ($installer.ExitCode -notin @(0,3010)) {
            throw "The Microsoft installer exited with code $($installer.ExitCode)."
        }
        Write-Host 'Vendor installer completed. Check the application dependencies again.'
        if ($installer.ExitCode -eq 3010) { Write-Host 'A Windows restart may be required.' }
    }
    finally {
        Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if ($Mode -eq 'Install') {
    try { Install-Component -Name $Component; exit 0 }
    catch {
        Write-Host ("Installation failed: " + $_.Exception.Message) -ForegroundColor Red
        Read-Host 'Press Enter to close'
        exit 1
    }
}

$initial = Test-Ready
if ($Mode -eq 'Launch' -and $initial.Ready) {
    Start-Process -FilePath $app -WorkingDirectory $root
    exit 0
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$form = New-Object Windows.Forms.Form
$form.Text = 'Dvinholm Lite - Dependencies'
$form.Size = New-Object Drawing.Size(660,390)
$form.MinimumSize = New-Object Drawing.Size(660,390)
$form.StartPosition = 'CenterScreen'
$form.Font = New-Object Drawing.Font('Segoe UI',10)
$intro = New-Object Windows.Forms.Label
$intro.SetBounds(15,15,610,42)
$intro.Text = 'Install or update Microsoft components for Dvinholm Lite x86. Downloads start only after confirmation. Windows may request administrator approval.'
$form.Controls.Add($intro)

$rows = @(
    @{ Name='DotNet'; Text='.NET 10 Desktop Runtime x86'; Top=68 },
    @{ Name='WinApp'; Text='Windows App Runtime 2.5.1'; Top=114 },
    @{ Name='VC'; Text='Visual C++ Redistributable x86'; Top=160 }
)
$labels = @{}
$buttons = @{}
$script:currentInstaller = $null
foreach ($entry in $rows) {
    $name = $entry.Name
    $label = New-Object Windows.Forms.Label
    $label.SetBounds(15,$entry.Top,345,30)
    $form.Controls.Add($label)
    $labels[$name] = $label
    $button = New-Object Windows.Forms.Button
    $button.SetBounds(370,$entry.Top-5,240,35)
    $button.Text = 'Install / Update'
    $button.Tag = $name
    $form.Controls.Add($button)
    $buttons[$name] = $button
    $button.Add_Click({
        $component = [string]$this.Tag
        if ($null -ne $script:currentInstaller) { return }
        $prompt = "Download and run the Microsoft installer for $component? " +
                  "A vendor window and UAC prompt may appear."
        if ([Windows.Forms.MessageBox]::Show($prompt,'Confirm Microsoft installation',
            [Windows.Forms.MessageBoxButtons]::YesNo,
            [Windows.Forms.MessageBoxIcon]::Question) -ne
            [Windows.Forms.DialogResult]::Yes) { return }
        $powershell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
        $args = '-NoProfile -ExecutionPolicy Bypass -File "' +
                $PSCommandPath + '" -Mode Install -Component ' + $component +
                ' -LiteDirectory "' + $root + '"'
        try {
            $script:currentInstaller = Start-Process -FilePath $powershell -ArgumentList $args -PassThru
            $status.Text = 'Microsoft installer session started. Keep its window open until completed.'
            foreach ($b in $buttons.Values) { $b.Enabled = $false }
        } catch {
            [Windows.Forms.MessageBox]::Show('Could not launch the installer session.',
                'Installation error') | Out-Null
        }
    })
}
$status = New-Object Windows.Forms.Label
$status.SetBounds(15,212,615,53)
$form.Controls.Add($status)
$refresh = New-Object Windows.Forms.Button
$refresh.SetBounds(15,284,160,36)
$refresh.Text = 'Recheck'
$form.Controls.Add($refresh)
$launch = New-Object Windows.Forms.Button
$launch.SetBounds(185,284,215,36)
$launch.Text = 'Start Dvinholm'
$form.Controls.Add($launch)
$exit = New-Object Windows.Forms.Button
$exit.SetBounds(410,284,200,36)
$exit.Text = 'Close'
$form.Controls.Add($exit)
$script:ready = $false
function Update-View {
    $inventory = Get-Inventory
    foreach ($entry in $rows) {
        $ok = [bool]$inventory.($entry.Name)
        $labels[$entry.Name].Text = $entry.Text + '   ' +
            $(if ($ok) { 'Detected' } else { 'Not verified' })
    }
    $check = Test-Ready
    $script:ready = [bool]$check.Ready
    $status.Text = if ($script:ready) {
        'Ready: exact Lite prerequisite checker passed. You can start Dvinholm.'
    } else {
        'Not ready: ' + [string]$check.Reason
    }
    $launch.Enabled = $script:ready
}
$refresh.Add_Click({ Update-View })
$launch.Add_Click({
    if (-not (Test-Ready).Ready) { Update-View; return }
    Start-Process -FilePath $app -WorkingDirectory $root
    $form.Close()
})
$exit.Add_Click({ $form.Close() })
$timer = New-Object Windows.Forms.Timer
$timer.Interval = 1500
$timer.Add_Tick({
    if ($null -ne $script:currentInstaller -and $script:currentInstaller.HasExited) {
        $script:currentInstaller.Dispose()
        $script:currentInstaller = $null
        foreach ($b in $buttons.Values) { $b.Enabled = $true }
        Update-View
    }
})
$timer.Start()
$form.Add_Shown({ Update-View })
try { [void]$form.ShowDialog() }
finally { $timer.Stop(); $timer.Dispose(); $form.Dispose() }
){
        $versions.Add([version]$Matches[1])
       }
      }
     }finally{$key.Dispose()}
    }
   }finally{$hive.Dispose()}
  }
  if($versions.Count){$result.VC=($versions|Sort-Object -Descending|Select-Object -First 1).ToString()}
 }catch{$result.VC='Unknown'}
 return $result
}
function Get-VersionDisposition([string]$installed,[string]$latest){
 if($installed -eq 'Not detected'){return 'Missing'}
 if($installed -eq 'Unknown'){return 'Installed version unknown'}
 if($latest -eq 'Unknown'){return 'Latest version not verified'}
 try{
  $i=[version]$installed;$l=[version]$latest
  if($l -gt $i){return 'Update available'}
  if($l -eq $i){return 'Current'}
  return 'Installed newer'
 }catch{return 'Version comparison unavailable'}
}
function Get-OfficialLatestVersions {
 # Network used only when explicitly requesting VersionReport.
 # Microsoft does not provide a reliable VC++ version via its permalink.
 $latest=@{DotNet='Unknown';WinApp='Unknown';VC='Unknown'}
 try {
  $url='https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/10.0/releases.json'
  $index=Invoke-RestMethod -Uri $url -UseBasicParsing -TimeoutSec 20
  $value=[string]$index.'latest-release'
  if($value -cmatch '^10\.\d+\.\d+
# This path does not read a runtimeconfig, launch WinUI or install software.
if ($Mode -eq 'Probe') {
    $inventory = Get-Inventory
    foreach ($item in @('DotNet','WinApp','VC')) {
        $key = if ($item -eq 'DotNet') { 'DOTNET' } else { $item.ToUpperInvariant() }
        $state = if ($inventory.$item) { 'READY' } else { 'MISSING' }
        Write-Output "$key=$state"
    }
    return
}

function Select-DotNetInstaller($index) {
    # Pure selector: test Microsoft release metadata offline, before making
    # any installer download, Authenticode check or process launch.
    if ($null -eq $index) { throw 'Missing Microsoft .NET release metadata.' }
    $latest = [string]$index.'latest-release'
    if ($latest -cnotmatch '^10\.\d+\.\d+$') {
        throw 'Microsoft .NET 10 release metadata has no stable current version.'
    }
    $matching = @($index.releases | Where-Object {
        [string]$_.'release-version' -ceq $latest
    })
    if ($matching.Count -ne 1) {
        throw 'Cannot select one current .NET 10 release from official metadata.'
    }
    $files = @($matching[0].windowsdesktop.files | Where-Object {
        [string]$_.rid -ceq 'win-x86' -and
        [string]$_.name -ceq 'windowsdesktop-runtime-win-x86.exe'
    })
    if ($files.Count -ne 1) {
        throw 'Cannot select a unique Microsoft .NET Desktop x86 installer.'
    }
    $file = $files[0]
    $uri = [uri]([string]$file.url)
    if ($uri.Scheme -cne 'https' -or $uri.UserInfo -or $uri.Port -ne 443 -or
        $uri.Host -cnotin @('builds.dotnet.microsoft.com',
                           'download.visualstudio.microsoft.com',
                           'dotnetcli.azureedge.net',
                           'dotnetcli.blob.core.windows.net')) {
        throw 'The .NET release index references an unexpected installer host.'
    }
    if ([string]$file.hash -cnotmatch '^[a-fA-F0-9]{128}$') {
        throw 'The official .NET Desktop installer has no usable SHA-512 digest.'
    }
    return [pscustomobject]@{
        Url = $uri.AbsoluteUri
        Hash = ([string]$file.hash).ToUpperInvariant()
    }
}
function Test-InstallerMetadataOffline {
    $count = 0
    function Reject([scriptblock]$action) {
        $rejected = $false
        try { & $action | Out-Null } catch { $rejected = $true }
        if (-not $rejected) { throw 'Unsafe .NET installer metadata was accepted.' }
    }
    function Fixture {
        $file = [pscustomobject]@{
            rid='win-x86'
            name='windowsdesktop-runtime-win-x86.exe'
            url='https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.10/windowsdesktop-runtime-10.0.10-win-x86.exe'
            hash=('ab'*64)
        }
        return [pscustomobject]@{
            'latest-release'='10.0.10'
            releases=@([pscustomobject]@{
                'release-version'='10.0.10'
                windowsdesktop=[pscustomobject]@{files=@($file)}
            })
        }
    }
    $valid = Fixture
    $selected = Select-DotNetInstaller $valid
    if ($selected.Hash -cne ('AB'*64) -or
        $selected.Url -cne $valid.releases[0].windowsdesktop.files[0].url) {
        throw 'Valid Microsoft .NET Desktop x86 installer selection failed.'
    }
    $count++
    $invalid = Fixture; $invalid.'latest-release'='11.0.0'
    Reject {Select-DotNetInstaller $invalid};$count++
    $invalid = Fixture; $invalid.'latest-release'='10.0.11-preview'
    Reject {Select-DotNetInstaller $invalid};$count++
    $invalid = Fixture
    $invalid.releases=@($invalid.releases[0],$invalid.releases[0])
    Reject {Select-DotNetInstaller $invalid};$count++
    $invalid = Fixture
    $invalid.releases[0].windowsdesktop.files[0].rid='win-x64'
    Reject {Select-DotNetInstaller $invalid};$count++
    $invalid = Fixture
    $file = $invalid.releases[0].windowsdesktop.files[0]
    $invalid.releases[0].windowsdesktop.files=@($file,$file)
    Reject {Select-DotNetInstaller $invalid};$count++
    $invalid = Fixture
    $invalid.releases[0].windowsdesktop.files[0].url='https://untrusted.example/runtime.exe'
    Reject {Select-DotNetInstaller $invalid};$count++
    $invalid = Fixture
    $invalid.releases[0].windowsdesktop.files[0].hash='invalid'
    Reject {Select-DotNetInstaller $invalid};$count++
    if ($count -ne 8) { throw 'Wrong .NET installer metadata test count.' }
    Write-Host 'Lite dependency manager 8/8 offline installer metadata checks passed.'
}
if ($Mode -eq 'SelfTest') {
    Test-InstallerMetadataOffline
    exit 0
}

function Get-OfficialInstaller {
    param([string]$Name)
    if ($Name -eq 'WinApp') {
        # Main and Singleton match the OS architecture; x64 runtime installer
        # also provisions the x86 Framework/DDLM needed by the x86 app.
        $nativeArch = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
        if ($nativeArch -notin @('X86','X64','Arm64')) {
            throw 'Unsupported Windows architecture for the official runtime installer.'
        }
        $suffix = $nativeArch.ToLowerInvariant()
        return [pscustomobject]@{
            Url = "https://aka.ms/windowsappsdk/2.5/2.5.1/windowsappruntimeinstall-$suffix.exe"
            Hash = $null
        }
    }
    if ($Name -eq 'VC') {
        return [pscustomobject]@{
            Url = 'https://aka.ms/vc14/vc_redist.x86.exe'
            Hash = $null
        }
    }
    if ($Name -ne 'DotNet') { throw 'Unknown dependency.' }

    # Microsoft official .NET release metadata binds each installer to SHA-512.
    $indexUrl = 'https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/10.0/releases.json'
    $index = Invoke-RestMethod -Uri $indexUrl -UseBasicParsing -TimeoutSec 40
    return (Select-DotNetInstaller $index)
}

function Install-Component {
    param([string]$Name)
    $source = Get-OfficialInstaller -Name $Name
    $temp = Join-Path ([IO.Path]::GetTempPath()) ('Dvinholm-Microsoft-' + [guid]::NewGuid().ToString('N'))
    $null = New-Item -ItemType Directory -Path $temp
    $exe = Join-Path $temp 'MicrosoftDependencySetup.exe'
    try {
        Write-Host "Downloading the official $Name installer..." -ForegroundColor Cyan
        Invoke-WebRequest -Uri $source.Url -UseBasicParsing -OutFile $exe -TimeoutSec 240
        $length = (Get-Item -LiteralPath $exe).Length
        if ($length -lt 65536 -or $length -gt 350MB) {
            throw 'Downloaded installer size is outside allowed limits.'
        }
        if ($null -ne $source.Hash) {
            $actual = (Get-FileHash -LiteralPath $exe -Algorithm SHA512).Hash
            if ($actual -ne $source.Hash) {
                throw 'The Microsoft release-metadata SHA-512 verification failed.'
            }
        }
        $signature = Get-AuthenticodeSignature -LiteralPath $exe
        if ($signature.Status -ne 'Valid' -or
            $null -eq $signature.SignerCertificate -or
            $signature.SignerCertificate.Subject -notmatch '(?i)(^|,\s*)CN=Microsoft Corporation(,|$)') {
            throw 'Installer is not authentically signed by Microsoft Corporation.'
        }
        Write-Host 'Microsoft digital signature verified. Launching the official installer...' -ForegroundColor Green
        # The vendor installer owns its UAC elevation and visible UI. No /quiet
        # and no forced package-state change without a user click.
        $installer = Start-Process -FilePath $exe -PassThru -Wait
        if ($installer.ExitCode -notin @(0,3010)) {
            throw "The Microsoft installer exited with code $($installer.ExitCode)."
        }
        Write-Host 'Vendor installer completed. Check the application dependencies again.'
        if ($installer.ExitCode -eq 3010) { Write-Host 'A Windows restart may be required.' }
    }
    finally {
        Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if ($Mode -eq 'Install') {
    try { Install-Component -Name $Component; exit 0 }
    catch {
        Write-Host ("Installation failed: " + $_.Exception.Message) -ForegroundColor Red
        Read-Host 'Press Enter to close'
        exit 1
    }
}

$initial = Test-Ready
if ($Mode -eq 'Launch' -and $initial.Ready) {
    Start-Process -FilePath $app -WorkingDirectory $root
    exit 0
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$form = New-Object Windows.Forms.Form
$form.Text = 'Dvinholm Lite - Dependencies'
$form.Size = New-Object Drawing.Size(660,390)
$form.MinimumSize = New-Object Drawing.Size(660,390)
$form.StartPosition = 'CenterScreen'
$form.Font = New-Object Drawing.Font('Segoe UI',10)
$intro = New-Object Windows.Forms.Label
$intro.SetBounds(15,15,610,42)
$intro.Text = 'Install or update Microsoft components for Dvinholm Lite x86. Downloads start only after confirmation. Windows may request administrator approval.'
$form.Controls.Add($intro)

$rows = @(
    @{ Name='DotNet'; Text='.NET 10 Desktop Runtime x86'; Top=68 },
    @{ Name='WinApp'; Text='Windows App Runtime 2.5.1'; Top=114 },
    @{ Name='VC'; Text='Visual C++ Redistributable x86'; Top=160 }
)
$labels = @{}
$buttons = @{}
$script:currentInstaller = $null
foreach ($entry in $rows) {
    $name = $entry.Name
    $label = New-Object Windows.Forms.Label
    $label.SetBounds(15,$entry.Top,345,30)
    $form.Controls.Add($label)
    $labels[$name] = $label
    $button = New-Object Windows.Forms.Button
    $button.SetBounds(370,$entry.Top-5,240,35)
    $button.Text = 'Install / Update'
    $button.Tag = $name
    $form.Controls.Add($button)
    $buttons[$name] = $button
    $button.Add_Click({
        $component = [string]$this.Tag
        if ($null -ne $script:currentInstaller) { return }
        $prompt = "Download and run the Microsoft installer for $component? " +
                  "A vendor window and UAC prompt may appear."
        if ([Windows.Forms.MessageBox]::Show($prompt,'Confirm Microsoft installation',
            [Windows.Forms.MessageBoxButtons]::YesNo,
            [Windows.Forms.MessageBoxIcon]::Question) -ne
            [Windows.Forms.DialogResult]::Yes) { return }
        $powershell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
        $args = '-NoProfile -ExecutionPolicy Bypass -File "' +
                $PSCommandPath + '" -Mode Install -Component ' + $component +
                ' -LiteDirectory "' + $root + '"'
        try {
            $script:currentInstaller = Start-Process -FilePath $powershell -ArgumentList $args -PassThru
            $status.Text = 'Microsoft installer session started. Keep its window open until completed.'
            foreach ($b in $buttons.Values) { $b.Enabled = $false }
        } catch {
            [Windows.Forms.MessageBox]::Show('Could not launch the installer session.',
                'Installation error') | Out-Null
        }
    })
}
$status = New-Object Windows.Forms.Label
$status.SetBounds(15,212,615,53)
$form.Controls.Add($status)
$refresh = New-Object Windows.Forms.Button
$refresh.SetBounds(15,284,160,36)
$refresh.Text = 'Recheck'
$form.Controls.Add($refresh)
$launch = New-Object Windows.Forms.Button
$launch.SetBounds(185,284,215,36)
$launch.Text = 'Start Dvinholm'
$form.Controls.Add($launch)
$exit = New-Object Windows.Forms.Button
$exit.SetBounds(410,284,200,36)
$exit.Text = 'Close'
$form.Controls.Add($exit)
$script:ready = $false
function Update-View {
    $inventory = Get-Inventory
    foreach ($entry in $rows) {
        $ok = [bool]$inventory.($entry.Name)
        $labels[$entry.Name].Text = $entry.Text + '   ' +
            $(if ($ok) { 'Detected' } else { 'Not verified' })
    }
    $check = Test-Ready
    $script:ready = [bool]$check.Ready
    $status.Text = if ($script:ready) {
        'Ready: exact Lite prerequisite checker passed. You can start Dvinholm.'
    } else {
        'Not ready: ' + [string]$check.Reason
    }
    $launch.Enabled = $script:ready
}
$refresh.Add_Click({ Update-View })
$launch.Add_Click({
    if (-not (Test-Ready).Ready) { Update-View; return }
    Start-Process -FilePath $app -WorkingDirectory $root
    $form.Close()
})
$exit.Add_Click({ $form.Close() })
$timer = New-Object Windows.Forms.Timer
$timer.Interval = 1500
$timer.Add_Tick({
    if ($null -ne $script:currentInstaller -and $script:currentInstaller.HasExited) {
        $script:currentInstaller.Dispose()
        $script:currentInstaller = $null
        foreach ($b in $buttons.Values) { $b.Enabled = $true }
        Update-View
    }
})
$timer.Start()
$form.Add_Shown({ Update-View })
try { [void]$form.ShowDialog() }
finally { $timer.Stop(); $timer.Dispose(); $form.Dispose() }
){$latest.DotNet=$value}
 }catch{}
 try {
  $headers=@{'User-Agent'='Dvinholm-DependencyVersionCheck';'Accept'='application/vnd.github+json'}
  $release=Invoke-RestMethod -Uri 'https://api.github.com/repos/microsoft/WindowsAppSDK/releases/latest' -Headers $headers -UseBasicParsing -TimeoutSec 20
  $tag=[string]$release.tag_name
  if(-not $release.draft -and -not $release.prerelease -and
     $tag -cmatch '^v(2\.\d+\.\d+)
# This path does not read a runtimeconfig, launch WinUI or install software.
if ($Mode -eq 'Probe') {
    $inventory = Get-Inventory
    foreach ($item in @('DotNet','WinApp','VC')) {
        $key = if ($item -eq 'DotNet') { 'DOTNET' } else { $item.ToUpperInvariant() }
        $state = if ($inventory.$item) { 'READY' } else { 'MISSING' }
        Write-Output "$key=$state"
    }
    return
}

function Select-DotNetInstaller($index) {
    # Pure selector: test Microsoft release metadata offline, before making
    # any installer download, Authenticode check or process launch.
    if ($null -eq $index) { throw 'Missing Microsoft .NET release metadata.' }
    $latest = [string]$index.'latest-release'
    if ($latest -cnotmatch '^10\.\d+\.\d+$') {
        throw 'Microsoft .NET 10 release metadata has no stable current version.'
    }
    $matching = @($index.releases | Where-Object {
        [string]$_.'release-version' -ceq $latest
    })
    if ($matching.Count -ne 1) {
        throw 'Cannot select one current .NET 10 release from official metadata.'
    }
    $files = @($matching[0].windowsdesktop.files | Where-Object {
        [string]$_.rid -ceq 'win-x86' -and
        [string]$_.name -ceq 'windowsdesktop-runtime-win-x86.exe'
    })
    if ($files.Count -ne 1) {
        throw 'Cannot select a unique Microsoft .NET Desktop x86 installer.'
    }
    $file = $files[0]
    $uri = [uri]([string]$file.url)
    if ($uri.Scheme -cne 'https' -or $uri.UserInfo -or $uri.Port -ne 443 -or
        $uri.Host -cnotin @('builds.dotnet.microsoft.com',
                           'download.visualstudio.microsoft.com',
                           'dotnetcli.azureedge.net',
                           'dotnetcli.blob.core.windows.net')) {
        throw 'The .NET release index references an unexpected installer host.'
    }
    if ([string]$file.hash -cnotmatch '^[a-fA-F0-9]{128}$') {
        throw 'The official .NET Desktop installer has no usable SHA-512 digest.'
    }
    return [pscustomobject]@{
        Url = $uri.AbsoluteUri
        Hash = ([string]$file.hash).ToUpperInvariant()
    }
}
function Test-InstallerMetadataOffline {
    $count = 0
    function Reject([scriptblock]$action) {
        $rejected = $false
        try { & $action | Out-Null } catch { $rejected = $true }
        if (-not $rejected) { throw 'Unsafe .NET installer metadata was accepted.' }
    }
    function Fixture {
        $file = [pscustomobject]@{
            rid='win-x86'
            name='windowsdesktop-runtime-win-x86.exe'
            url='https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.10/windowsdesktop-runtime-10.0.10-win-x86.exe'
            hash=('ab'*64)
        }
        return [pscustomobject]@{
            'latest-release'='10.0.10'
            releases=@([pscustomobject]@{
                'release-version'='10.0.10'
                windowsdesktop=[pscustomobject]@{files=@($file)}
            })
        }
    }
    $valid = Fixture
    $selected = Select-DotNetInstaller $valid
    if ($selected.Hash -cne ('AB'*64) -or
        $selected.Url -cne $valid.releases[0].windowsdesktop.files[0].url) {
        throw 'Valid Microsoft .NET Desktop x86 installer selection failed.'
    }
    $count++
    $invalid = Fixture; $invalid.'latest-release'='11.0.0'
    Reject {Select-DotNetInstaller $invalid};$count++
    $invalid = Fixture; $invalid.'latest-release'='10.0.11-preview'
    Reject {Select-DotNetInstaller $invalid};$count++
    $invalid = Fixture
    $invalid.releases=@($invalid.releases[0],$invalid.releases[0])
    Reject {Select-DotNetInstaller $invalid};$count++
    $invalid = Fixture
    $invalid.releases[0].windowsdesktop.files[0].rid='win-x64'
    Reject {Select-DotNetInstaller $invalid};$count++
    $invalid = Fixture
    $file = $invalid.releases[0].windowsdesktop.files[0]
    $invalid.releases[0].windowsdesktop.files=@($file,$file)
    Reject {Select-DotNetInstaller $invalid};$count++
    $invalid = Fixture
    $invalid.releases[0].windowsdesktop.files[0].url='https://untrusted.example/runtime.exe'
    Reject {Select-DotNetInstaller $invalid};$count++
    $invalid = Fixture
    $invalid.releases[0].windowsdesktop.files[0].hash='invalid'
    Reject {Select-DotNetInstaller $invalid};$count++
    if ($count -ne 8) { throw 'Wrong .NET installer metadata test count.' }
    Write-Host 'Lite dependency manager 8/8 offline installer metadata checks passed.'
}
if ($Mode -eq 'SelfTest') {
    Test-InstallerMetadataOffline
    exit 0
}

function Get-OfficialInstaller {
    param([string]$Name)
    if ($Name -eq 'WinApp') {
        # Main and Singleton match the OS architecture; x64 runtime installer
        # also provisions the x86 Framework/DDLM needed by the x86 app.
        $nativeArch = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
        if ($nativeArch -notin @('X86','X64','Arm64')) {
            throw 'Unsupported Windows architecture for the official runtime installer.'
        }
        $suffix = $nativeArch.ToLowerInvariant()
        return [pscustomobject]@{
            Url = "https://aka.ms/windowsappsdk/2.5/2.5.1/windowsappruntimeinstall-$suffix.exe"
            Hash = $null
        }
    }
    if ($Name -eq 'VC') {
        return [pscustomobject]@{
            Url = 'https://aka.ms/vc14/vc_redist.x86.exe'
            Hash = $null
        }
    }
    if ($Name -ne 'DotNet') { throw 'Unknown dependency.' }

    # Microsoft official .NET release metadata binds each installer to SHA-512.
    $indexUrl = 'https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/10.0/releases.json'
    $index = Invoke-RestMethod -Uri $indexUrl -UseBasicParsing -TimeoutSec 40
    return (Select-DotNetInstaller $index)
}

function Install-Component {
    param([string]$Name)
    $source = Get-OfficialInstaller -Name $Name
    $temp = Join-Path ([IO.Path]::GetTempPath()) ('Dvinholm-Microsoft-' + [guid]::NewGuid().ToString('N'))
    $null = New-Item -ItemType Directory -Path $temp
    $exe = Join-Path $temp 'MicrosoftDependencySetup.exe'
    try {
        Write-Host "Downloading the official $Name installer..." -ForegroundColor Cyan
        Invoke-WebRequest -Uri $source.Url -UseBasicParsing -OutFile $exe -TimeoutSec 240
        $length = (Get-Item -LiteralPath $exe).Length
        if ($length -lt 65536 -or $length -gt 350MB) {
            throw 'Downloaded installer size is outside allowed limits.'
        }
        if ($null -ne $source.Hash) {
            $actual = (Get-FileHash -LiteralPath $exe -Algorithm SHA512).Hash
            if ($actual -ne $source.Hash) {
                throw 'The Microsoft release-metadata SHA-512 verification failed.'
            }
        }
        $signature = Get-AuthenticodeSignature -LiteralPath $exe
        if ($signature.Status -ne 'Valid' -or
            $null -eq $signature.SignerCertificate -or
            $signature.SignerCertificate.Subject -notmatch '(?i)(^|,\s*)CN=Microsoft Corporation(,|$)') {
            throw 'Installer is not authentically signed by Microsoft Corporation.'
        }
        Write-Host 'Microsoft digital signature verified. Launching the official installer...' -ForegroundColor Green
        # The vendor installer owns its UAC elevation and visible UI. No /quiet
        # and no forced package-state change without a user click.
        $installer = Start-Process -FilePath $exe -PassThru -Wait
        if ($installer.ExitCode -notin @(0,3010)) {
            throw "The Microsoft installer exited with code $($installer.ExitCode)."
        }
        Write-Host 'Vendor installer completed. Check the application dependencies again.'
        if ($installer.ExitCode -eq 3010) { Write-Host 'A Windows restart may be required.' }
    }
    finally {
        Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if ($Mode -eq 'Install') {
    try { Install-Component -Name $Component; exit 0 }
    catch {
        Write-Host ("Installation failed: " + $_.Exception.Message) -ForegroundColor Red
        Read-Host 'Press Enter to close'
        exit 1
    }
}

$initial = Test-Ready
if ($Mode -eq 'Launch' -and $initial.Ready) {
    Start-Process -FilePath $app -WorkingDirectory $root
    exit 0
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$form = New-Object Windows.Forms.Form
$form.Text = 'Dvinholm Lite - Dependencies'
$form.Size = New-Object Drawing.Size(660,390)
$form.MinimumSize = New-Object Drawing.Size(660,390)
$form.StartPosition = 'CenterScreen'
$form.Font = New-Object Drawing.Font('Segoe UI',10)
$intro = New-Object Windows.Forms.Label
$intro.SetBounds(15,15,610,42)
$intro.Text = 'Install or update Microsoft components for Dvinholm Lite x86. Downloads start only after confirmation. Windows may request administrator approval.'
$form.Controls.Add($intro)

$rows = @(
    @{ Name='DotNet'; Text='.NET 10 Desktop Runtime x86'; Top=68 },
    @{ Name='WinApp'; Text='Windows App Runtime 2.5.1'; Top=114 },
    @{ Name='VC'; Text='Visual C++ Redistributable x86'; Top=160 }
)
$labels = @{}
$buttons = @{}
$script:currentInstaller = $null
foreach ($entry in $rows) {
    $name = $entry.Name
    $label = New-Object Windows.Forms.Label
    $label.SetBounds(15,$entry.Top,345,30)
    $form.Controls.Add($label)
    $labels[$name] = $label
    $button = New-Object Windows.Forms.Button
    $button.SetBounds(370,$entry.Top-5,240,35)
    $button.Text = 'Install / Update'
    $button.Tag = $name
    $form.Controls.Add($button)
    $buttons[$name] = $button
    $button.Add_Click({
        $component = [string]$this.Tag
        if ($null -ne $script:currentInstaller) { return }
        $prompt = "Download and run the Microsoft installer for $component? " +
                  "A vendor window and UAC prompt may appear."
        if ([Windows.Forms.MessageBox]::Show($prompt,'Confirm Microsoft installation',
            [Windows.Forms.MessageBoxButtons]::YesNo,
            [Windows.Forms.MessageBoxIcon]::Question) -ne
            [Windows.Forms.DialogResult]::Yes) { return }
        $powershell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
        $args = '-NoProfile -ExecutionPolicy Bypass -File "' +
                $PSCommandPath + '" -Mode Install -Component ' + $component +
                ' -LiteDirectory "' + $root + '"'
        try {
            $script:currentInstaller = Start-Process -FilePath $powershell -ArgumentList $args -PassThru
            $status.Text = 'Microsoft installer session started. Keep its window open until completed.'
            foreach ($b in $buttons.Values) { $b.Enabled = $false }
        } catch {
            [Windows.Forms.MessageBox]::Show('Could not launch the installer session.',
                'Installation error') | Out-Null
        }
    })
}
$status = New-Object Windows.Forms.Label
$status.SetBounds(15,212,615,53)
$form.Controls.Add($status)
$refresh = New-Object Windows.Forms.Button
$refresh.SetBounds(15,284,160,36)
$refresh.Text = 'Recheck'
$form.Controls.Add($refresh)
$launch = New-Object Windows.Forms.Button
$launch.SetBounds(185,284,215,36)
$launch.Text = 'Start Dvinholm'
$form.Controls.Add($launch)
$exit = New-Object Windows.Forms.Button
$exit.SetBounds(410,284,200,36)
$exit.Text = 'Close'
$form.Controls.Add($exit)
$script:ready = $false
function Update-View {
    $inventory = Get-Inventory
    foreach ($entry in $rows) {
        $ok = [bool]$inventory.($entry.Name)
        $labels[$entry.Name].Text = $entry.Text + '   ' +
            $(if ($ok) { 'Detected' } else { 'Not verified' })
    }
    $check = Test-Ready
    $script:ready = [bool]$check.Ready
    $status.Text = if ($script:ready) {
        'Ready: exact Lite prerequisite checker passed. You can start Dvinholm.'
    } else {
        'Not ready: ' + [string]$check.Reason
    }
    $launch.Enabled = $script:ready
}
$refresh.Add_Click({ Update-View })
$launch.Add_Click({
    if (-not (Test-Ready).Ready) { Update-View; return }
    Start-Process -FilePath $app -WorkingDirectory $root
    $form.Close()
})
$exit.Add_Click({ $form.Close() })
$timer = New-Object Windows.Forms.Timer
$timer.Interval = 1500
$timer.Add_Tick({
    if ($null -ne $script:currentInstaller -and $script:currentInstaller.HasExited) {
        $script:currentInstaller.Dispose()
        $script:currentInstaller = $null
        foreach ($b in $buttons.Values) { $b.Enabled = $true }
        Update-View
    }
})
$timer.Start()
$form.Add_Shown({ Update-View })
try { [void]$form.ShowDialog() }
finally { $timer.Stop(); $timer.Dispose(); $form.Dispose() }
){$latest.WinApp=$Matches[1]}
 }catch{}
 return $latest
}
function Get-DependencyVersionReport {
 $installed=Get-InstalledComponentVersions
 $ready=Get-Inventory
 $latest=Get-OfficialLatestVersions
 $components=[ordered]@{}
 foreach($key in @('DotNet','WinApp','VC')){
  $components[$key]=[ordered]@{
   Installed=[string]$installed[$key]
   Ready=[bool]$ready.$key
   Latest=[string]$latest[$key]
   Status=Get-VersionDisposition ([string]$installed[$key]) ([string]$latest[$key])
  }
 }
 # Windows App SDK latest is advisory, not automatically compatible with
 # the baseline 2.5.1 app and pinned runtime installer.
 return [ordered]@{
  DotNet=$components.DotNet
  WinApp=$components.WinApp
  VC=$components.VC
  WinAppBaseline='2.5.1'
  VCUpdateNote='Microsoft provides a latest-x86-installer link but not a verified version feed.'
 }
}
function Test-DependencyVersionLogic {
 $checks=@(
  @('Not detected','10.0.12','Missing'),
  @('Unknown','10.0.12','Installed version unknown'),
  @('10.0.11','Unknown','Latest version not verified'),
  @('10.0.11','10.0.12','Update available'),
  @('10.0.12','10.0.12','Current'),
  @('10.0.13','10.0.12','Installed newer'),
  @('2.5.1.0','2.6.4','Update available'),
  @('14.50.0.0','Unknown','Latest version not verified')
 )
 foreach($item in $checks){
  $actual=Get-VersionDisposition $item[0] $item[1]
  if($actual -cne $item[2]){throw ('Version decision failed for '+$item[0]+' / '+$item[1])}
 }
 Write-Host 'Lite dependency version decisions 8/8 offline checks passed.'
}
if($Mode -eq 'VersionReport'){
 Get-DependencyVersionReport|ConvertTo-Json -Depth 6 -Compress
 return
}

# Read-only, machine-readable inventory for the separate Updater Banner.
# This path does not read a runtimeconfig, launch WinUI or install software.
if ($Mode -eq 'Probe') {
    $inventory = Get-Inventory
    foreach ($item in @('DotNet','WinApp','VC')) {
        $key = if ($item -eq 'DotNet') { 'DOTNET' } else { $item.ToUpperInvariant() }
        $state = if ($inventory.$item) { 'READY' } else { 'MISSING' }
        Write-Output "$key=$state"
    }
    return
}

function Select-DotNetInstaller($index) {
    # Pure selector: test Microsoft release metadata offline, before making
    # any installer download, Authenticode check or process launch.
    if ($null -eq $index) { throw 'Missing Microsoft .NET release metadata.' }
    $latest = [string]$index.'latest-release'
    if ($latest -cnotmatch '^10\.\d+\.\d+$') {
        throw 'Microsoft .NET 10 release metadata has no stable current version.'
    }
    $matching = @($index.releases | Where-Object {
        [string]$_.'release-version' -ceq $latest
    })
    if ($matching.Count -ne 1) {
        throw 'Cannot select one current .NET 10 release from official metadata.'
    }
    $files = @($matching[0].windowsdesktop.files | Where-Object {
        [string]$_.rid -ceq 'win-x86' -and
        [string]$_.name -ceq 'windowsdesktop-runtime-win-x86.exe'
    })
    if ($files.Count -ne 1) {
        throw 'Cannot select a unique Microsoft .NET Desktop x86 installer.'
    }
    $file = $files[0]
    $uri = [uri]([string]$file.url)
    if ($uri.Scheme -cne 'https' -or $uri.UserInfo -or $uri.Port -ne 443 -or
        $uri.Host -cnotin @('builds.dotnet.microsoft.com',
                           'download.visualstudio.microsoft.com',
                           'dotnetcli.azureedge.net',
                           'dotnetcli.blob.core.windows.net')) {
        throw 'The .NET release index references an unexpected installer host.'
    }
    if ([string]$file.hash -cnotmatch '^[a-fA-F0-9]{128}$') {
        throw 'The official .NET Desktop installer has no usable SHA-512 digest.'
    }
    return [pscustomobject]@{
        Url = $uri.AbsoluteUri
        Hash = ([string]$file.hash).ToUpperInvariant()
    }
}
function Test-InstallerMetadataOffline {
    $count = 0
    function Reject([scriptblock]$action) {
        $rejected = $false
        try { & $action | Out-Null } catch { $rejected = $true }
        if (-not $rejected) { throw 'Unsafe .NET installer metadata was accepted.' }
    }
    function Fixture {
        $file = [pscustomobject]@{
            rid='win-x86'
            name='windowsdesktop-runtime-win-x86.exe'
            url='https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.10/windowsdesktop-runtime-10.0.10-win-x86.exe'
            hash=('ab'*64)
        }
        return [pscustomobject]@{
            'latest-release'='10.0.10'
            releases=@([pscustomobject]@{
                'release-version'='10.0.10'
                windowsdesktop=[pscustomobject]@{files=@($file)}
            })
        }
    }
    $valid = Fixture
    $selected = Select-DotNetInstaller $valid
    if ($selected.Hash -cne ('AB'*64) -or
        $selected.Url -cne $valid.releases[0].windowsdesktop.files[0].url) {
        throw 'Valid Microsoft .NET Desktop x86 installer selection failed.'
    }
    $count++
    $invalid = Fixture; $invalid.'latest-release'='11.0.0'
    Reject {Select-DotNetInstaller $invalid};$count++
    $invalid = Fixture; $invalid.'latest-release'='10.0.11-preview'
    Reject {Select-DotNetInstaller $invalid};$count++
    $invalid = Fixture
    $invalid.releases=@($invalid.releases[0],$invalid.releases[0])
    Reject {Select-DotNetInstaller $invalid};$count++
    $invalid = Fixture
    $invalid.releases[0].windowsdesktop.files[0].rid='win-x64'
    Reject {Select-DotNetInstaller $invalid};$count++
    $invalid = Fixture
    $file = $invalid.releases[0].windowsdesktop.files[0]
    $invalid.releases[0].windowsdesktop.files=@($file,$file)
    Reject {Select-DotNetInstaller $invalid};$count++
    $invalid = Fixture
    $invalid.releases[0].windowsdesktop.files[0].url='https://untrusted.example/runtime.exe'
    Reject {Select-DotNetInstaller $invalid};$count++
    $invalid = Fixture
    $invalid.releases[0].windowsdesktop.files[0].hash='invalid'
    Reject {Select-DotNetInstaller $invalid};$count++
    if ($count -ne 8) { throw 'Wrong .NET installer metadata test count.' }
    Write-Host 'Lite dependency manager 8/8 offline installer metadata checks passed.'
}
if ($Mode -eq 'SelfTest') {
    Test-InstallerMetadataOffline
    exit 0
}

function Get-OfficialInstaller {
    param([string]$Name)
    if ($Name -eq 'WinApp') {
        # Main and Singleton match the OS architecture; x64 runtime installer
        # also provisions the x86 Framework/DDLM needed by the x86 app.
        $nativeArch = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
        if ($nativeArch -notin @('X86','X64','Arm64')) {
            throw 'Unsupported Windows architecture for the official runtime installer.'
        }
        $suffix = $nativeArch.ToLowerInvariant()
        return [pscustomobject]@{
            Url = "https://aka.ms/windowsappsdk/2.5/2.5.1/windowsappruntimeinstall-$suffix.exe"
            Hash = $null
        }
    }
    if ($Name -eq 'VC') {
        return [pscustomobject]@{
            Url = 'https://aka.ms/vc14/vc_redist.x86.exe'
            Hash = $null
        }
    }
    if ($Name -ne 'DotNet') { throw 'Unknown dependency.' }

    # Microsoft official .NET release metadata binds each installer to SHA-512.
    $indexUrl = 'https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/10.0/releases.json'
    $index = Invoke-RestMethod -Uri $indexUrl -UseBasicParsing -TimeoutSec 40
    return (Select-DotNetInstaller $index)
}

function Install-Component {
    param([string]$Name)
    $source = Get-OfficialInstaller -Name $Name
    $temp = Join-Path ([IO.Path]::GetTempPath()) ('Dvinholm-Microsoft-' + [guid]::NewGuid().ToString('N'))
    $null = New-Item -ItemType Directory -Path $temp
    $exe = Join-Path $temp 'MicrosoftDependencySetup.exe'
    try {
        Write-Host "Downloading the official $Name installer..." -ForegroundColor Cyan
        Invoke-WebRequest -Uri $source.Url -UseBasicParsing -OutFile $exe -TimeoutSec 240
        $length = (Get-Item -LiteralPath $exe).Length
        if ($length -lt 65536 -or $length -gt 350MB) {
            throw 'Downloaded installer size is outside allowed limits.'
        }
        if ($null -ne $source.Hash) {
            $actual = (Get-FileHash -LiteralPath $exe -Algorithm SHA512).Hash
            if ($actual -ne $source.Hash) {
                throw 'The Microsoft release-metadata SHA-512 verification failed.'
            }
        }
        $signature = Get-AuthenticodeSignature -LiteralPath $exe
        if ($signature.Status -ne 'Valid' -or
            $null -eq $signature.SignerCertificate -or
            $signature.SignerCertificate.Subject -notmatch '(?i)(^|,\s*)CN=Microsoft Corporation(,|$)') {
            throw 'Installer is not authentically signed by Microsoft Corporation.'
        }
        Write-Host 'Microsoft digital signature verified. Launching the official installer...' -ForegroundColor Green
        # The vendor installer owns its UAC elevation and visible UI. No /quiet
        # and no forced package-state change without a user click.
        $installer = Start-Process -FilePath $exe -PassThru -Wait
        if ($installer.ExitCode -notin @(0,3010)) {
            throw "The Microsoft installer exited with code $($installer.ExitCode)."
        }
        Write-Host 'Vendor installer completed. Check the application dependencies again.'
        if ($installer.ExitCode -eq 3010) { Write-Host 'A Windows restart may be required.' }
    }
    finally {
        Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if ($Mode -eq 'Install') {
    try { Install-Component -Name $Component; exit 0 }
    catch {
        Write-Host ("Installation failed: " + $_.Exception.Message) -ForegroundColor Red
        Read-Host 'Press Enter to close'
        exit 1
    }
}

$initial = Test-Ready
if ($Mode -eq 'Launch' -and $initial.Ready) {
    Start-Process -FilePath $app -WorkingDirectory $root
    exit 0
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$form = New-Object Windows.Forms.Form
$form.Text = 'Dvinholm Lite - Dependencies'
$form.Size = New-Object Drawing.Size(660,390)
$form.MinimumSize = New-Object Drawing.Size(660,390)
$form.StartPosition = 'CenterScreen'
$form.Font = New-Object Drawing.Font('Segoe UI',10)
$intro = New-Object Windows.Forms.Label
$intro.SetBounds(15,15,610,42)
$intro.Text = 'Install or update Microsoft components for Dvinholm Lite x86. Downloads start only after confirmation. Windows may request administrator approval.'
$form.Controls.Add($intro)

$rows = @(
    @{ Name='DotNet'; Text='.NET 10 Desktop Runtime x86'; Top=68 },
    @{ Name='WinApp'; Text='Windows App Runtime 2.5.1'; Top=114 },
    @{ Name='VC'; Text='Visual C++ Redistributable x86'; Top=160 }
)
$labels = @{}
$buttons = @{}
$script:currentInstaller = $null
foreach ($entry in $rows) {
    $name = $entry.Name
    $label = New-Object Windows.Forms.Label
    $label.SetBounds(15,$entry.Top,345,30)
    $form.Controls.Add($label)
    $labels[$name] = $label
    $button = New-Object Windows.Forms.Button
    $button.SetBounds(370,$entry.Top-5,240,35)
    $button.Text = 'Install / Update'
    $button.Tag = $name
    $form.Controls.Add($button)
    $buttons[$name] = $button
    $button.Add_Click({
        $component = [string]$this.Tag
        if ($null -ne $script:currentInstaller) { return }
        $prompt = "Download and run the Microsoft installer for $component? " +
                  "A vendor window and UAC prompt may appear."
        if ([Windows.Forms.MessageBox]::Show($prompt,'Confirm Microsoft installation',
            [Windows.Forms.MessageBoxButtons]::YesNo,
            [Windows.Forms.MessageBoxIcon]::Question) -ne
            [Windows.Forms.DialogResult]::Yes) { return }
        $powershell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
        $args = '-NoProfile -ExecutionPolicy Bypass -File "' +
                $PSCommandPath + '" -Mode Install -Component ' + $component +
                ' -LiteDirectory "' + $root + '"'
        try {
            $script:currentInstaller = Start-Process -FilePath $powershell -ArgumentList $args -PassThru
            $status.Text = 'Microsoft installer session started. Keep its window open until completed.'
            foreach ($b in $buttons.Values) { $b.Enabled = $false }
        } catch {
            [Windows.Forms.MessageBox]::Show('Could not launch the installer session.',
                'Installation error') | Out-Null
        }
    })
}
$status = New-Object Windows.Forms.Label
$status.SetBounds(15,212,615,53)
$form.Controls.Add($status)
$refresh = New-Object Windows.Forms.Button
$refresh.SetBounds(15,284,160,36)
$refresh.Text = 'Recheck'
$form.Controls.Add($refresh)
$launch = New-Object Windows.Forms.Button
$launch.SetBounds(185,284,215,36)
$launch.Text = 'Start Dvinholm'
$form.Controls.Add($launch)
$exit = New-Object Windows.Forms.Button
$exit.SetBounds(410,284,200,36)
$exit.Text = 'Close'
$form.Controls.Add($exit)
$script:ready = $false
function Update-View {
    $inventory = Get-Inventory
    foreach ($entry in $rows) {
        $ok = [bool]$inventory.($entry.Name)
        $labels[$entry.Name].Text = $entry.Text + '   ' +
            $(if ($ok) { 'Detected' } else { 'Not verified' })
    }
    $check = Test-Ready
    $script:ready = [bool]$check.Ready
    $status.Text = if ($script:ready) {
        'Ready: exact Lite prerequisite checker passed. You can start Dvinholm.'
    } else {
        'Not ready: ' + [string]$check.Reason
    }
    $launch.Enabled = $script:ready
}
$refresh.Add_Click({ Update-View })
$launch.Add_Click({
    if (-not (Test-Ready).Ready) { Update-View; return }
    Start-Process -FilePath $app -WorkingDirectory $root
    $form.Close()
})
$exit.Add_Click({ $form.Close() })
$timer = New-Object Windows.Forms.Timer
$timer.Interval = 1500
$timer.Add_Tick({
    if ($null -ne $script:currentInstaller -and $script:currentInstaller.HasExited) {
        $script:currentInstaller.Dispose()
        $script:currentInstaller = $null
        foreach ($b in $buttons.Values) { $b.Enabled = $true }
        Update-View
    }
})
$timer.Start()
$form.Add_Shown({ Update-View })
try { [void]$form.ShowDialog() }
finally { $timer.Stop(); $timer.Dispose(); $form.Dispose() }
