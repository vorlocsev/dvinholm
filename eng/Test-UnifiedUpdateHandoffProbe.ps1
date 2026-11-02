#requires -Version 7.0
# Integration checks for the non-shipping x64 fixture-only handoff executable.
# No installed Dvinholm files are read, replaced, or executed.
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProbeExe)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$exe = (Resolve-Path -LiteralPath $ProbeExe).Path
$marker = 'Dvinholm recovery simulation v1'
$checks = 0

function New-Fixture {
    param([string]$Name = ('dvinholm-recovery-fixture-' + [guid]::NewGuid().ToString('N')))
    $path = Join-Path ([IO.Path]::GetTempPath()) $Name
    [IO.Directory]::CreateDirectory($path) | Out-Null
    [IO.File]::WriteAllText((Join-Path $path '.dvinholm-offline-fixture'), $marker, [Text.Encoding]::ASCII)
    return $path
}

function Start-Sleeper {
    param([int]$Milliseconds)
    $psi = [Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = (Get-Command powershell.exe).Source
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.ArgumentList.Add('-NoProfile')
    $psi.ArgumentList.Add('-NonInteractive')
    $psi.ArgumentList.Add('-Command')
    $psi.ArgumentList.Add("Start-Sleep -Milliseconds $Milliseconds")
    $p = [Diagnostics.Process]::Start($psi)
    if ($null -eq $p) { throw 'Could not start a synthetic child process.' }
    return $p
}

function Start-Probe {
    param([string]$Fixture, [int]$PidValue, [long]$Ticks, [int]$TimeoutMs)
    $psi = [Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $exe
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    foreach($arg in @('--fixture-probe', [string]$PidValue, [string]$Ticks,
        [string]$TimeoutMs, $Fixture)) { $psi.ArgumentList.Add($arg) }
    $p = [Diagnostics.Process]::Start($psi)
    if ($null -eq $p) { throw 'Could not launch handoff fixture probe.' }
    return $p
}

function Wait-Code {
    param([Diagnostics.Process]$Process)
    if (-not $Process.WaitForExit(15000)) {
        $Process.Kill($true)
        throw 'Fixture probe exceeded test timeout.'
    }
    return $Process.ExitCode
}

function Stop-Child {
    param([Diagnostics.Process]$Process)
    if ($null -ne $Process) {
        try { if (-not $Process.HasExited) { $Process.Kill($true); $Process.WaitForExit(5000) | Out-Null } }
        catch [InvalidOperationException] { }
        finally { $Process.Dispose() }
    }
}

function Check {
    param([bool]$Condition, [string]$Label)
    if (-not $Condition) { throw "FAIL: $Label" }
    $script:checks++
    Write-Host "PASS $script:checks/8 $Label"
}

# 1: Child already exited, but its still-open process handle confirms identity.
$root = New-Fixture
try {
    $child = Start-Sleeper -Milliseconds 200
    try {
        $ticks = $child.StartTime.ToUniversalTime().Ticks
        $child.WaitForExit(10000) | Out-Null
        $probe = Start-Probe $root $child.Id $ticks 5000
        try { $status = Wait-Code $probe }
        finally { $probe.Dispose() }
        # On Windows, GetProcessById may fail once the OS removes an exited PID.
        # In that case the helper MUST fail closed rather than report success.
        Check (($status -eq 0 -and (Test-Path (Join-Path $root 'fixture-handoff-ready.txt'))) -or
               ($status -eq 2 -and -not (Test-Path (Join-Path $root 'fixture-handoff-ready.txt')))) 'Exited child never yields false success'
    } finally { Stop-Child $child }
} finally { Remove-Item -LiteralPath $root -Recurse -Force }

# 2: Active process cannot produce a receipt until process exit.
$root = New-Fixture
try {
    $child = Start-Sleeper -Milliseconds 1800
    try {
        $ticks = $child.StartTime.ToUniversalTime().Ticks
        $probe = Start-Probe $root $child.Id $ticks 7000
        try {
            Start-Sleep -Milliseconds 250
            if (Test-Path (Join-Path $root 'fixture-handoff-ready.txt')) { throw 'Premature handoff receipt.' }
            $status = Wait-Code $probe
            Check ($status -eq 0 -and (Test-Path (Join-Path $root 'fixture-handoff-ready.txt'))) 'Real handle exited before receipt'
        } finally { Stop-Child $probe }
    } finally { Stop-Child $child }
} finally { Remove-Item -LiteralPath $root -Recurse -Force }

# 3: PID alone is insufficient. Recycled/wrong creation time refused.
$root = New-Fixture
try {
    $child = Start-Sleeper -Milliseconds 1600
    try {
        $probe = Start-Probe $root $child.Id ($child.StartTime.ToUniversalTime().Ticks + 1) 5000
        try { $status = Wait-Code $probe }
        finally { $probe.Dispose() }
        Check ($status -eq 2 -and -not (Test-Path (Join-Path $root 'fixture-handoff-ready.txt'))) 'Wrong start-time identity refused'
    } finally { Stop-Child $child }
} finally { Remove-Item -LiteralPath $root -Recurse -Force }

# 4: A non-fixture directory cannot be used even with a copied marker.
$root = New-Fixture -Name ('dvinholm-not-a-fixture-' + [guid]::NewGuid().ToString('N'))
try {
    $child = Start-Sleeper -Milliseconds 1400
    try {
        $probe = Start-Probe $root $child.Id $child.StartTime.ToUniversalTime().Ticks 5000
        try { $status = Wait-Code $probe }
        finally { $probe.Dispose() }
        Check ($status -eq 2 -and -not (Test-Path (Join-Path $root 'fixture-handoff-ready.txt'))) 'Outside fixture naming refused'
    } finally { Stop-Child $child }
} finally { Remove-Item -LiteralPath $root -Recurse -Force }

# 5: The exact fixture marker is mandatory.
$root = New-Fixture
try {
    [IO.File]::WriteAllText((Join-Path $root '.dvinholm-offline-fixture'), 'incorrect', [Text.Encoding]::ASCII)
    $child = Start-Sleeper -Milliseconds 1400
    try {
        $probe = Start-Probe $root $child.Id $child.StartTime.ToUniversalTime().Ticks 5000
        try { $status = Wait-Code $probe }
        finally { $probe.Dispose() }
        Check ($status -eq 2 -and -not (Test-Path (Join-Path $root 'fixture-handoff-ready.txt'))) 'Corrupt marker refused'
    } finally { Stop-Child $child }
} finally { Remove-Item -LiteralPath $root -Recurse -Force }

# 6: Timeout must not cause any file modification.
$root = New-Fixture
try {
    $child = Start-Sleeper -Milliseconds 1800
    try {
        $probe = Start-Probe $root $child.Id $child.StartTime.ToUniversalTime().Ticks 100
        try { $status = Wait-Code $probe }
        finally { $probe.Dispose() }
        Check ($status -eq 3 -and -not (Test-Path (Join-Path $root 'fixture-handoff-ready.txt'))) 'Timeout writes no handoff receipt'
    } finally { Stop-Child $child }
} finally { Remove-Item -LiteralPath $root -Recurse -Force }

# 7: Unknown process identity must fail closed.
$root = New-Fixture
try {
    $probe = Start-Probe $root 2147483600 42 5000
    try { $status = Wait-Code $probe }
    finally { $probe.Dispose() }
    Check ($status -eq 2 -and -not (Test-Path (Join-Path $root 'fixture-handoff-ready.txt'))) 'Unknown process cannot authorize handoff'
} finally { Remove-Item -LiteralPath $root -Recurse -Force }

# 8: An existing receipt cannot be overwritten by a second helper.
$root = New-Fixture
try {
    $receipt = Join-Path $root 'fixture-handoff-ready.txt'
    [IO.File]::WriteAllText($receipt, 'prior', [Text.Encoding]::ASCII)
    $child = Start-Sleeper -Milliseconds 550
    try {
        $probe = Start-Probe $root $child.Id $child.StartTime.ToUniversalTime().Ticks 5000
        try { $status = Wait-Code $probe }
        finally { $probe.Dispose() }
        Check ($status -eq 2 -and [IO.File]::ReadAllText($receipt) -eq 'prior') 'Existing receipt cannot be overwritten'
    } finally { Stop-Child $child }
} finally { Remove-Item -LiteralPath $root -Recurse -Force }

if ($checks -ne 8) { throw 'Unexpected number of offline handoff checks.' }
Write-Host 'PASS: 8/8 Windows x64 handoff helper fixture checks, no real install.'
# Intentionally rejected child invocations exit 2 or 3. PowerShell preserves
# the last native exit code across script return, even when all checks pass.
# Reset it explicitly only after verifying all eight assertions.
$global:LASTEXITCODE = 0
