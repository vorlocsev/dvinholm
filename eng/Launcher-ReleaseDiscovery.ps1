#requires -Version 5.1
<#
Read-only native Launcher release discovery. Loaded by Updater-Banner.ps1.
Application release comparison is deliberately independent from native
Launcher build identity; no downloads, execution or installation.
#>
function Read-LauncherInstalledBuild {
 $receipt=Join-Path $PSScriptRoot 'launcher-build.txt'
 try {
  if(-not(Test-Path -LiteralPath $receipt -PathType Leaf)){return 'Unverified'}
  $item=Get-Item -LiteralPath $receipt -Force
  if($item.Length -lt 3 -or $item.Length -gt 32 -or
     ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){
   return 'Unverified'
  }
  $value=[IO.File]::ReadAllText($receipt,[Text.Encoding]::ASCII).Trim()
  if($value -cmatch '^(?:ci-[1-9][0-9]{0,8}|v[0-9]+\.[0-9]+\.[0-9]+)$'){
   return $value
  }
 }catch{}
 return 'Unverified'
}
function Get-LauncherBuildRelation([string]$selected,[string]$installed,
 [string]$available){
 if($selected -cnotin @('Stable','Development')){return 'Unknown'}
 if($available -ceq 'Not published'){return 'Not published'}
 $pattern=if($selected -ceq 'Development'){'^ci-([1-9][0-9]{0,8})$'}else{
  '^v([0-9]+\.[0-9]+\.[0-9]+)$'
 }
 if($available -cnotmatch $pattern){return 'Unknown'}
 if($installed -ceq $available){return 'Current'}
 if($installed -cnotmatch $pattern){return 'Unverified or other channel'}
 if($selected -ceq 'Development'){
  $old=[long]$Matches[1]
  $null=$available -cmatch $pattern
  $new=[long]$Matches[1]
 }else{
  $old=[version]$Matches[1]
  $null=$available -cmatch $pattern
  $new=[version]$Matches[1]
 }
 if($new -gt $old){return 'Newer release available'}
 return 'Installed build newer'
}
function LatestLauncher {
 $header=@{'User-Agent'='Dvinholm-UpdaterBanner/1.0';'Accept'='application/vnd.github+json'}
 if($Channel -ceq 'Stable'){
  $release=Invoke-RestMethod -Uri ('https://api.github.com/repos/'+$repo+'/releases/latest') -Headers $header -TimeoutSec 30
  if($release.draft -or $release.prerelease -or
     [string]$release.tag_name -cnotmatch '^v[0-9]+\.[0-9]+\.[0-9]+$'){
   throw 'Untrusted Stable launcher release.'
  }
 }else{
  $all=@(Invoke-RestMethod -Uri ('https://api.github.com/repos/'+$repo+'/releases?per_page=40') -Headers $header -TimeoutSec 30)
  $eligible=@($all|Where-Object{
    $_.draft -eq $false -and $_.prerelease -eq $true -and
    [string]$_.tag_name -cmatch '^ci-[1-9][0-9]{0,8}$' -and
    [string]$_.target_commitish -cmatch '^[0-9a-f]{40}$'
   }|Sort-Object @{Expression={[long](([string]$_.tag_name).Substring(3))};Descending=$true})
  if($eligible.Count -eq 0){throw 'No public Development release found.'}
  $release=$eligible[0]
 }
 $asset=@($release.assets|Where-Object{$_.name -ceq $launcherZipName})
 $sums=@($release.assets|Where-Object{$_.name -ceq $sumsName})
 if($asset.Count -gt 1 -or $sums.Count -gt 1){
  throw 'Ambiguous launcher release assets.'
 }
 if($asset.Count -ne 1 -or $sums.Count -ne 1){
  return [pscustomobject]@{Tag=[string]$release.tag_name;Published=$false}
 }
 Validate-Asset $asset[0] $release.tag_name $launcherZipName
 Validate-Asset $sums[0] $release.tag_name $sumsName
 if([long]$asset[0].size -gt 2MB){throw 'Launcher release ZIP exceeds size budget.'}
 return [pscustomobject]@{Tag=[string]$release.tag_name;Published=$true}
}
