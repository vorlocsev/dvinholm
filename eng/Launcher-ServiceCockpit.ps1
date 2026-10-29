#requires -Version 5.1
<#
Service Cockpit v2. Implements the user-approved three-section launcher layout.
All displayed version/driver states are populated from real worker probes.
This file only builds WinForms UI; it never installs or connects an ECU.
#>

# Keep the existing Aurora banner/WIC WebP fallback, but discard the old
# sidebar/cards and replace them with a two-column service cockpit.
foreach($old in @($form.Controls)){
 if($old -is [Windows.Forms.Panel] -and $old -ne $hero -and
    $old -ne $sidebar){$old.Visible=$false}
}
$sidebar.Visible=$false
foreach($hidden in @($stableRadio,$developmentRadio,$refresh,$dot,$win,$vc,
 $rollback,$repair,$repairInstall,$channelTitle,$progressTitle,
 $dependenciesTitle,$foot,$title,$strap,$downloadPercent)){
 $hidden.Visible=$false
}
$choice.Visible=$true

function New-ServiceCard([int]$left,[int]$top,[int]$width,[int]$height) {
 $p=New-Object Windows.Forms.Panel
 $p.SetBounds($left,$top,$width,$height)
 $p.BackColor=[Drawing.Color]::FromArgb(11,35,58)
 $p.BorderStyle=[Windows.Forms.BorderStyle]::FixedSingle
 $form.Controls.Add($p)
 $p.SendToBack()
 return $p
}
function New-ServiceLabel([string]$content,[int]$x,[int]$y,
 [int]$width,[int]$height,[int]$fontSize=11,[bool]$strong=$false,
 [string]$color='#B6CBDF') {
 $l=New-Object Windows.Forms.Label
 $l.Text=$content
 $l.SetBounds($x,$y,$width,$height)
 $l.Font=[Drawing.Font]::new('Segoe UI',[single]$fontSize,
  $(if($strong){[Drawing.FontStyle]::Bold}else{[Drawing.FontStyle]::Regular}))
 $l.ForeColor=[Drawing.ColorTranslator]::FromHtml($color)
 $l.BackColor=[Drawing.Color]::Transparent
 $form.Controls.Add($l)
 return $l
}
function New-ServiceButton([string]$content,[int]$x,[int]$y,
 [int]$width,[int]$height,[bool]$primary=$false) {
 $b=New-Object Windows.Forms.Button
 $b.Text=$content
 $b.SetBounds($x,$y,$width,$height)
 $b.Font=[Drawing.Font]::new('Segoe UI',11,[Drawing.FontStyle]::Bold)
 $b.ForeColor=[Drawing.Color]::White
 $b.BackColor=if($primary){
  [Drawing.Color]::FromArgb(6,112,211)
 }else{
  [Drawing.Color]::FromArgb(21,65,100)
 }
 $b.FlatStyle=[Windows.Forms.FlatStyle]::Flat
 $b.FlatAppearance.BorderSize=1
 $b.FlatAppearance.BorderColor=[Drawing.Color]::FromArgb(42,100,140)
 $b.Cursor=[Windows.Forms.Cursors]::Hand
 $form.Controls.Add($b)
 return $b
}

$script:serviceCardLeft=New-ServiceCard 14 194 405 482
$script:serviceCardUpdates=New-ServiceCard 430 194 736 155
$script:serviceCardWindows=New-ServiceCard 430 357 736 155
$script:serviceCardDrivers=New-ServiceCard 430 520 736 156

$serviceHeading=New-ServiceLabel 'DVINHOLM  •  READY' 88 215 300 38 20 $true '#EAF6FF'
$serviceStatus=New-ServiceLabel 'Windows x86  |  J2534 diagnostics' 36 258 362 29 11 $false
$channelLabel=New-ServiceLabel 'Канал обновлений' 35 300 355 24 11 $true '#A8CADC'
$releaseHint=New-ServiceLabel 'Stable: release  /  Development: CI' 35 373 368 25 10 $false
$startNote=New-ServiceLabel 'Launcher closes after successful start.' 35 615 357 44 10 $false

# The real channel ComboBox is the sole channel selector.
$choice.SetBounds(35,326,365,40)
$choice.Font=[Drawing.Font]::new('Segoe UI',12)
$choice.BackColor=[Drawing.Color]::FromArgb(22,71,105)
$choice.ForeColor=[Drawing.Color]::White
$choice.BringToFront()

$installed.SetBounds(35,412,365,29)
$installed.Font=[Drawing.Font]::new('Segoe UI',12,[Drawing.FontStyle]::Bold)
$available.SetBounds(35,445,365,29)
$available.Font=[Drawing.Font]::new('Segoe UI',12)
$status.SetBounds(35,481,365,44)
$status.AutoEllipsis=$true
$status.Font=[Drawing.Font]::new('Segoe UI',10)
$status.ForeColor=[Drawing.Color]::FromArgb(91,215,186)
# Keep recovery state below the primary Start action, not over its bottom edge.
$recovery.SetBounds(35,586,365,24)
$recovery.Font=[Drawing.Font]::new('Segoe UI',9)
$recovery.AutoEllipsis=$true
$launch.SetBounds(35,528,365,53)
$launch.Font=[Drawing.Font]::new('Segoe UI',15,[Drawing.FontStyle]::Bold)
$launch.BackColor=[Drawing.Color]::FromArgb(6,113,214)

$updateIndex=New-ServiceLabel '01' 454 213 53 35 21 $true '#2BA9F3'
$updateHeading=New-ServiceLabel 'Обновления Dvinholm' 515 212 623 40 20 $true '#EFF8FF'
$updateHint=New-ServiceLabel 'SHA-256, загрузка и безопасная установка' 515 254 620 25 11
$check.SetBounds(452,287,306,44)
$check.Font=[Drawing.Font]::new('Segoe UI',11,[Drawing.FontStyle]::Bold)
$install.SetBounds(773,287,370,44)
$install.Font=[Drawing.Font]::new('Segoe UI',12,[Drawing.FontStyle]::Bold)
$install.BackColor=[Drawing.Color]::FromArgb(5,105,221)
$bar.SetBounds(452,336,691,8)
$bar.Style=[Windows.Forms.ProgressBarStyle]::Continuous
$bar.Visible=$true

$windowsIndex=New-ServiceLabel '02' 454 377 53 35 21 $true '#2BA9F3'
$windowsHeading=New-ServiceLabel 'Требования Windows' 515 377 625 35 20 $true '#EFF8FF'
$windowsHint=New-ServiceLabel 'Версии компонентов для Windows x86' 515 414 630 25 11
$deps.SetBounds(453,446,682,27)
$deps.Font=[Drawing.Font]::new('Segoe UI',10)
$deps.AutoEllipsis=$true
$windowsQuick=New-ServiceButton 'Перепроверить' 452 471 219 34
$depsInstallQuick=New-ServiceButton 'Установить компоненты' 682 471 272 34
$versionsButton.SetBounds(965,471,178,34)
$versionsButton.Font=[Drawing.Font]::new('Segoe UI',9,[Drawing.FontStyle]::Bold)

$driversIndex=New-ServiceLabel '03' 454 539 53 35 21 $true '#2BA9F3'
$driversHeading=New-ServiceLabel 'Драйверы адаптеров' 515 539 625 35 20 $true '#EFF8FF'
$driversHint=New-ServiceLabel 'J2534 · Volvo DiCE · Mongoose JLR' 515 576 625 25 11
$driverStatus=New-ServiceLabel 'Состояние адаптеров ещё не проверено' 452 609 684 29 10 $false '#8DCABF'
$driverScanQuick=New-ServiceButton 'Проверить' 763 641 176 32
$driverQuick=New-ServiceButton 'Открыть драйверы' 950 641 193 32
$settingsQuick=New-ServiceButton '⚙ Настройки' 995 135 154 35

# Display the logo in the actual service panel, not over the panoramic art.
if($null -ne $script:brandImage){
 $serviceLogo=New-Object Windows.Forms.PictureBox
 $serviceLogo.Image=$script:brandImage
 $serviceLogo.SizeMode=[Windows.Forms.PictureBoxSizeMode]::Zoom
 $serviceLogo.SetBounds(35,212,45,45)
 $form.Controls.Add($serviceLogo)
 $serviceLogo.BringToFront()
}
$brandTitle.Text='DVINHOLM'
$brandTitle.SetBounds(41,48,780,67)
$brandTitle.Font=[Drawing.Font]::new('Segoe UI',40,[Drawing.FontStyle]::Bold)
$brandTitle.ForeColor=[Drawing.Color]::White
$brandSub.Text='DIAGNOSTICS  ·  CEM  ·  CONFIGURATION'
$brandSub.SetBounds(45,121,775,30)
$brandSub.Font=[Drawing.Font]::new('Segoe UI',13,[Drawing.FontStyle]::Regular)
$brandSub.ForeColor=[Drawing.Color]::FromArgb(177,223,243)
if($script:serviceBackdropIsApproved){
 # Artwork already contains Dvinholm branding; do not double-print it.
 $brandTitle.Visible=$false
 $brandSub.Visible=$false
}
if($null -ne $logo){$logo.Visible=$false}

# A real action for every button. Installations remain confirmation-gated.
$windowsQuick.Add_Click({Begin-Action 'Probe'})
$depsInstallQuick.Add_Click({Show-MicrosoftComponents})
$driverQuick.Add_Click({Show-J2534Drivers})
$driverScanQuick.Add_Click({Update-ServiceDriverInventory})
$settingsQuick.Add_Click({Show-LauncherSettings})
function Update-ServiceDriverInventory {
 if($script:child -or $script:vendor){return}
 try{
  $inventory=Get-DriverInventory
  $records=@($inventory.Devices|Where-Object{$null -ne $_}).Count
  $dice=[string]$inventory.Targets.VolvoDice.Status
  $mongoose=[string]$inventory.Targets.MongooseJlr.Status
  $script:driversWereProbed=$true
  $driverStatus.Text=('J2534: '+$records+'  |  DiCE: '+$dice+
    '  |  Mongoose JLR: '+$mongoose)
 }catch{
  $driverStatus.Text=if($script:launcherLanguage -eq 'RU'){
   'Не удалось проверить драйверы. Откройте подробности.'
  }else{'Driver scan failed. Open details.'}
 }
}

# Work-area sizing: at common desktop scales the whole form is <=944x552.
# On smaller/high-DPI screens scroll instead of clipping the bottom card.
function Set-AuroraCompactLayout {
 $referenceWidth=1180
 $referenceHeight=690
 $heroHeight=184
 $working=[Windows.Forms.Screen]::FromControl($form).WorkingArea
 $borderW=[int][Math]::Max(0,$form.Width-$form.ClientSize.Width)
 $borderH=[int][Math]::Max(0,$form.Height-$form.ClientSize.Height)
 $maxWidth=[Math]::Max(1,$working.Width-32-$borderW)
 $maxHeight=[Math]::Max(1,$working.Height-32-$borderH)
 $fit=[Math]::Min([double]$maxWidth/$referenceWidth,
                  [double]$maxHeight/$referenceHeight)
 $factor=[Math]::Max(0.65,[Math]::Min(0.80,$fit))
 $script:auroraScale=$factor
 $form.SuspendLayout()
 try {
  $form.MinimumSize=[Drawing.Size]::Empty
  $form.MaximumSize=[Drawing.Size]::Empty
  $form.AutoScaleMode=[Windows.Forms.AutoScaleMode]::None
  $hero.SetBounds(0,0,$referenceWidth,$heroHeight)
  Resize-AuroraControlTree $form $factor
  $fullW=[int][Math]::Round($referenceWidth*$factor)
  $fullH=[int][Math]::Round($referenceHeight*$factor)
  $needsScroll=($fullW -gt $maxWidth -or $fullH -gt $maxHeight)
  if($needsScroll){
   $form.AutoScrollMinSize=[Drawing.Size]::new($fullW,$fullH)
  }else{$form.AutoScrollMinSize=[Drawing.Size]::Empty}
  $form.AutoScroll=$needsScroll
  $form.ClientSize=[Drawing.Size]::new(
   [int][Math]::Min($fullW,$maxWidth),[int][Math]::Min($fullH,$maxHeight))
  $form.MinimumSize=$form.Size
  $form.MaximumSize=$form.Size
 }finally{$form.ResumeLayout($true)}
 $hero.Invalidate()
}
