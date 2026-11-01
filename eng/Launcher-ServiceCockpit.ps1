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


# Approved Northern Compact home (2026-11-01, final/fourth concept).
# Use only the existing GDI+/WinForms runtime and the bundled panorama.
# All updates, drivers, safe launch and recovery remain handled above.
# Service Cockpit becomes the detailed Updates view behind this home.
$script:northernHome=New-Object Windows.Forms.Panel
$script:northernHome.SetBounds(0,0,1180,690)
$script:northernHome.BackColor=[Drawing.Color]::FromArgb(8,22,39)
$form.Controls.Add($script:northernHome)
$script:northernHome.BringToFront()
$script:northernDetailOpen=$false

function New-NorthernLabel([string]$value,[int]$x,[int]$y,
 [int]$width,[int]$height,[int]$size=11,[bool]$bold=$false){
 $label=New-Object Windows.Forms.Label
 $label.Text=$value
 $label.SetBounds($x,$y,$width,$height)
 $label.Font=[Drawing.Font]::new('Segoe UI',[single]$size,
  $(if($bold){[Drawing.FontStyle]::Bold}else{[Drawing.FontStyle]::Regular}))
 $label.ForeColor=[Drawing.Color]::FromArgb(227,242,253)
 $label.BackColor=[Drawing.Color]::Transparent
 $label.AutoEllipsis=$true
 $script:northernHome.Controls.Add($label)
 return $label
}
function New-NorthernButton([string]$value,[int]$x,[int]$y,
 [int]$width,[int]$height,[bool]$primary=$false){
 $button=New-Object Windows.Forms.Button
 $button.Text=$value
 $button.SetBounds($x,$y,$width,$height)
 $button.Font=[Drawing.Font]::new('Segoe UI',$(if($primary){17}else{11}),
  [Drawing.FontStyle]::Bold)
 $button.BackColor=if($primary){
  [Drawing.Color]::FromArgb(8,118,113)
 }else{[Drawing.Color]::FromArgb(17,49,76)}
 $button.ForeColor=[Drawing.Color]::White
 $button.FlatStyle=[Windows.Forms.FlatStyle]::Flat
 $button.FlatAppearance.BorderSize=2
 $button.FlatAppearance.BorderColor=if($primary){
  [Drawing.Color]::FromArgb(42,218,189)
 }else{[Drawing.Color]::FromArgb(51,121,168)}
 $button.Cursor=[Windows.Forms.Cursors]::Hand
 $script:northernHome.Controls.Add($button)
 return $button
}
# Northern identity is drawn locally, not a copied automaker mark.
# Gear + ring/arrow + white-gull silhouette + three White Sea waves.
$script:northernHome.Add_Paint({
 $g=$_.Graphics
 $g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
 $factor=[single]$script:auroraScale
 if($factor -le 0){$factor=1}
 $g.ScaleTransform($factor,$factor)
 $background=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(8,22,39))
 try{$g.FillRectangle($background,0,0,1180,690)}finally{$background.Dispose()}
 if($script:heroBackdrop){
  $picture=$script:heroBackdrop
  # Crop to the right-hand panorama to avoid reprinting baked-in old titles.
  $source=[Drawing.Rectangle]::new(
   [int]($picture.Width*0.24),0,
   [int]($picture.Width*0.76),[int]$picture.Height)
  $g.DrawImage($picture,[Drawing.Rectangle]::new(0,0,1180,330),
   $source,[Drawing.GraphicsUnit]::Pixel)
 }
 # Darkened typography area and compact Aurora edge treatment.
 $veil=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(135,5,20,37))
 $lower=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(242,8,23,40))
 $line=[Drawing.Pen]::new([Drawing.Color]::FromArgb(75,43,198,203),2)
 try{
  $g.FillRectangle($veil,0,0,1180,329)
  $g.FillRectangle($lower,0,330,1180,360)
  $g.DrawLine($line,42,324,1137,324)
  $g.DrawLine($line,42,655,1137,655)
  $cardFill=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(223,15,45,69))
  try{
   foreach($x in @(55,412,773)){
    $g.FillRectangle($cardFill,$x,337,342,64)
    $g.DrawRectangle($line,$x,337,342,64)
   }
  }finally{$cardFill.Dispose()}
 }finally{$veil.Dispose();$lower.Dispose();$line.Dispose()}
 $silver=[Drawing.Pen]::new([Drawing.Color]::FromArgb(216,227,240,250),7)
 $cyan=[Drawing.Pen]::new([Drawing.Color]::FromArgb(212,36,224,217),5)
 $bird=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(246,238,248,255))
 try{
  # Volvo-inspired ring and arrow merged with the Severodvinsk gear motif.
  $g.DrawArc($silver,85,64,170,170,38,305)
  $g.DrawLine($silver,215,94,272,38)
  $g.DrawLine($silver,272,38,269,81)
  $g.DrawLine($silver,272,38,229,38)
  for($i=0;$i -lt 10;$i++){
   $a=([double]$i*2*[Math]::PI/10)
   $x1=170+[Math]::Cos($a)*89
   $y1=148+[Math]::Sin($a)*89
   $x2=170+[Math]::Cos($a)*102
   $y2=148+[Math]::Sin($a)*102
   $g.DrawLine($silver,[single]$x1,[single]$y1,[single]$x2,[single]$y2)
  }
  $g.FillPolygon($bird,[Drawing.Point[]]@(
   [Drawing.Point]::new(104,141),[Drawing.Point]::new(142,128),
   [Drawing.Point]::new(164,141),[Drawing.Point]::new(210,114),
   [Drawing.Point]::new(192,151),[Drawing.Point]::new(233,154),
   [Drawing.Point]::new(181,159),[Drawing.Point]::new(160,174),
   [Drawing.Point]::new(139,153)))
  $g.DrawArc($cyan,111,176,117,26,8,166)
  $g.DrawArc($cyan,120,191,112,24,8,166)
  $g.DrawArc($cyan,130,206,104,21,8,166)
 }finally{$silver.Dispose();$cyan.Dispose();$bird.Dispose()}
})
$script:northTitle=New-NorthernLabel 'DVINHOLM' 329 66 670 76 37 $true
$script:northSubtitle=New-NorthernLabel 'LAUNCHER   /   СЕВЕРНАЯ ДВИНА · БЕЛОЕ МОРЕ' 333 153 690 40 13
$script:northCaption=New-NorthernLabel 'Volvo · Windows x86 · J2534 диагностика' 334 206 690 30 12
$script:northSource=New-NorthernLabel 'АВТОМОБИЛЬ · СЕВЕР · ТЕХНОЛОГИИ' 333 268 780 24 11
$script:northRuntime=New-NorthernLabel '.NET x86: не проверено' 67 342 328 53 12 $true
$script:northWinApp=New-NorthernLabel 'WinApp: не проверено' 426 342 328 53 12 $true
$script:northDriver=New-NorthernLabel 'J2534: не проверено' 791 342 328 53 12 $true
$script:northLaunch=New-NorthernButton '▶   ЗАПУСТИТЬ DVINHOLM' 260 413 660 76 $true
$script:northUpdates=New-NorthernButton '↻  Обновления' 60 514 247 65
$script:northComponents=New-NorthernButton '◇  Компоненты' 323 514 247 65
$script:northDrivers=New-NorthernButton '▣  Драйверы J2534' 586 514 247 65
$script:northSettings=New-NorthernButton '⚙  Настройки' 849 514 247 65
$script:northVersion=New-NorthernLabel 'Версия: не проверена' 61 603 335 35 10
$script:northChannel=New-NorthernLabel 'Канал: не проверен' 399 603 320 35 10
$script:northActivity=New-NorthernLabel 'Ожидание проверки компонентов' 722 600 403 44 10
$script:northReturn=New-ServiceButton '← Главная' 25 133 176 36
$script:northReturn.Visible=$false
function Show-NorthernHome {
 $script:northernDetailOpen=$false
 $script:northernHome.Visible=$true
 $script:northReturn.Visible=$false
 $script:northernHome.BringToFront()
}
$script:northReturn.Add_Click({Show-NorthernHome})
function Show-NorthernUpdates {
 # Advanced service cockpit retains its genuine Check, Install, worker
 # progress, actual versions and confirmation-gated driver controls.
 $script:northernDetailOpen=$true
 $script:northernHome.Visible=$false
 $script:northReturn.Visible=$true
 $script:northReturn.BringToFront()
}
$script:northLaunch.Add_Click({
 if($launch.Enabled){$launch.PerformClick()}
})
$script:northUpdates.Add_Click({Show-NorthernUpdates})
$script:northComponents.Add_Click({Show-MicrosoftComponents})
$script:northDrivers.Add_Click({Show-J2534Drivers})
$script:northSettings.Add_Click({Show-LauncherSettings})
function Update-NorthernCompactSnapshot {
 if($null -eq $script:northLaunch -or $script:northLaunch.IsDisposed){return}
 # Copy actual worker and inventory readings. No fabricated version or
 # successful J2534/DiCE hardware connection is shown.
 $script:northLaunch.Enabled=$launch.Enabled
 $script:northComponents.Enabled=$depsInstallQuick.Enabled
 $script:northDrivers.Enabled=$driverQuick.Enabled
 $script:northSettings.Enabled=$settingsQuick.Enabled
 $script:northVersion.Text=$installed.Text
 $script:northChannel.Text='Канал: '+[string]$choice.SelectedItem
 $script:northActivity.Text=$status.Text
 $parts=$deps.Text
 foreach($spec in @(
  @($script:northRuntime,'\.NET x86:\s*([^ ]+)',' .NET x86: '),
  @($script:northWinApp,'WinApp:\s*([^ ]+)',' WinApp: ')
  )){
  $value=if($parts -match $spec[1]){$Matches[1]}else{'не проверено'}
  $spec[0].Text=$spec[2]+$value
 }
 if($script:driversWereProbed){
  $script:northDriver.Text='J2534: '+$driverStatus.Text
 }else{
  $script:northDriver.Text=if($script:launcherLanguage -eq 'RU'){
   'J2534: не проверено'
  }else{'J2534: not checked'}
 }
 if($script:launcherLanguage -eq 'EN'){
  $script:northTitle.Text='DVINHOLM'
  $script:northSubtitle.Text='LAUNCHER   /   NORTHERN DVINA · WHITE SEA'
  $script:northCaption.Text='Volvo · Windows x86 · J2534 diagnostics'
  $script:northSource.Text='VEHICLE · NORTH · TECHNOLOGY'
  $script:northLaunch.Text='▶   START DVINHOLM'
  $script:northUpdates.Text='↻  Updates'
  $script:northComponents.Text='◇  Components'
  $script:northDrivers.Text='▣  J2534 Drivers'
  $script:northSettings.Text='⚙  Settings'
  $script:northReturn.Text='← Home'
  $script:northChannel.Text='Channel: '+[string]$choice.SelectedItem
 }else{
  $script:northTitle.Text='DVINHOLM'
  $script:northSubtitle.Text='LAUNCHER   /   СЕВЕРНАЯ ДВИНА · БЕЛОЕ МОРЕ'
  $script:northCaption.Text='Volvo · Windows x86 · J2534 диагностика'
  $script:northSource.Text='АВТОМОБИЛЬ · СЕВЕР · ТЕХНОЛОГИИ'
  $script:northLaunch.Text='▶   ЗАПУСТИТЬ DVINHOLM'
  $script:northUpdates.Text='↻  Обновления'
  $script:northComponents.Text='◇  Компоненты'
  $script:northDrivers.Text='▣  Драйверы J2534'
  $script:northSettings.Text='⚙  Настройки'
  $script:northReturn.Text='← Главная'
 }
}
# Show the compact approved screen on normal launch. Existing Service
# Cockpit remains accessible with all real actions from Updates.
