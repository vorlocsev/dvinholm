#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Portable', 'Lite')][string]$Flavor,
    [Parameter(Mandatory)][string]$ZipPath
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
# ZipFile lives in FileSystem on Windows PowerShell 5.1 (.NET Framework).
# PowerShell 7 generally loads the core type without the older facade.
if (-not ('System.IO.Compression.ZipFile' -as [type])) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
}
$zip = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($ZipPath))
try {
    # The built-in updater extracts archives under a fixed installation
    # directory. Validate *all* entries, not only application files.
    $seen = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $zip.Entries) {
        $name = $entry.FullName.Replace('\', '/')
        if ([string]::IsNullOrEmpty($name) -or
            $name.StartsWith('/') -or
            $name -match '^[A-Za-z]:' -or
            $name -match '(^|/)\.\.?(?=/|$)' -or
            $name -match '//' -or
            $name -match '[\x00-\x1F]') {
            throw "$Flavor ZIP contains an unsafe archive path: $name"
        }
        $canonicalName = $name.TrimEnd('/')
        if (-not $seen.Add($canonicalName)) {
            throw "$Flavor ZIP contains conflicting archive entries: $name"
        }
    }
    $paths = @($zip.Entries | Where-Object { -not [string]::IsNullOrEmpty($_.Name) } |
        ForEach-Object { $_.FullName.Replace('\', '/') })
    if ($paths -notcontains 'Dvinholm.App.exe') {
        throw "$Flavor archive is missing the root application executable."
    }
    if ($paths -notcontains 'Assets/Dvinholm.ico') {
        throw "$Flavor archive is missing the branded application icon."
    }
    if ($paths -notcontains 'Dvinholm.App.runtimeconfig.json') {
        throw "$Flavor archive is missing .NET runtime configuration."
    }
    if ($paths -notcontains 'Dvinholm.App.deps.json') {
        throw "$Flavor archive is missing .NET dependency metadata."
    }

    $debugExtension = '\.(pdb|mdb|dbg|ilk|iobj|ipch|map|dmp|mdmp|etl|binlog|trx|coverage|coveragexml)$'
    $debugExecutables = '^(vsdbg|windbg|cdb|ntsd|dotnet-dump|dotnet-trace|dotnet-counters|dotnet-monitor|createdump)\.exe$'
    $debugFiles = @($paths | Where-Object {
        $_ -match $debugExtension -or
        [IO.Path]::GetFileName($_) -match $debugExecutables
    })
    if ($debugFiles.Count -ne 0) {
        throw "$Flavor ZIP includes debugging byproducts/tools: $($debugFiles -join ', ')"
    }

    $hasCoreClr = $paths -contains 'coreclr.dll'
    $hasHostFxr = $paths -contains 'hostfxr.dll'
    if ($Flavor -eq 'Portable') {
        if (-not $hasCoreClr -or -not $hasHostFxr) {
            throw 'Portable is missing the bundled .NET x86 runtime.'
        }
    } elseif ($hasCoreClr -or $hasHostFxr) {
        throw 'Lite unexpectedly bundles .NET runtime: publish must be framework-dependent.'
    } else {
        # Merely removing coreclr.dll does not make a runnable Lite build:
        # a framework-dependent .NET 10 runtimeconfig must name its host.
        $runtimeEntry = $zip.GetEntry('Dvinholm.App.runtimeconfig.json')
        $reader = [System.IO.StreamReader]::new($runtimeEntry.Open())
        try { $runtimeJson = $reader.ReadToEnd() } finally { $reader.Dispose() }
        try { $runtime = $runtimeJson | ConvertFrom-Json -ErrorAction Stop }
        catch { throw 'Lite has an unreadable .NET runtimeconfig.json.' }
        $frameworks = @($runtime.runtimeOptions.framework) +
            @($runtime.runtimeOptions.frameworks)
        $compatible = @($frameworks | Where-Object {
            $null -ne $_ -and
            $_.name -in @('Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App') -and
            $_.version -match '^10\.'
        })
        if ($compatible.Count -eq 0) {
            throw 'Lite must reference a .NET 10 x86-compatible shared runtime.'
        }
    }
    Write-Host "Verified clean $Flavor ZIP: $($paths.Count) files; bundled .NET: $hasCoreClr"
}
finally { $zip.Dispose() }
