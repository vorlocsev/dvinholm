#requires -Version 5.1
<#
Offline, deterministic native Launcher version-state tests.
No GitHub access, downloads, or native launcher launch.
#>
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Launcher-ReleaseDiscovery.ps1')
$cases=@(
 @('Development','ci-42','ci-43','Newer release available'),
 @('Development','ci-43','ci-43','Current'),
 @('Development','ci-44','ci-43','Installed build newer'),
 @('Development','Unverified','ci-43','Unverified or other channel'),
 @('Development','v0.2.4','ci-43','Unverified or other channel'),
 @('Stable','v0.2.3','v0.2.4','Newer release available'),
 @('Stable','v0.2.4','v0.2.4','Current'),
 @('Stable','v0.2.4','Not published','Not published')
)
$checks=0
foreach($case in $cases){
 $actual=Get-LauncherBuildRelation ([string]$case[0]) ([string]$case[1]) ([string]$case[2])
 if($actual -cne [string]$case[3]){
  throw ('Incorrect native Launcher version comparison: '+($case[0..2] -join ' / '))
 }
 $checks++
}
if($checks -ne 8){throw 'Unexpected version test count.'}
Write-Host 'Native Launcher version check: 8/8 offline state cases passed.'
