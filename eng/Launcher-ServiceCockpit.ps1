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

# Pure size plan for the actual Northern Aurora / Service Cockpit window.
# Inputs are physical Windows working-area and nonclient-frame pixel sizes.
# Tests may supply equivalent *effective* work areas for DPI scenarios.
# Never rerun WinForms Scale on a live window, which would shrink it twice.
function Get-NorthernCompactGeometry(
 [int]$WorkWidth,[int]$WorkHeight,[int]$BorderWidth,[int]$BorderHeight){
 if($WorkWidth -lt 1 -or $WorkHeight -lt 1 -or
    $BorderWidth -lt 0 -or $BorderHeight -lt 0){
  throw 'Northern work-area dimensions must be positive and borders nonnegative.'
 }
 $referenceWidth=1180
 $referenceHeight=840
 $maxWidth=[int][Math]::Max(1,$WorkWidth-32-$BorderWidth)
 $maxHeight=[int][Math]::Max(1,$WorkHeight-32-$BorderHeight)
 $fit=[Math]::Min([double]$maxWidth/$referenceWidth,
                  [double]$maxHeight/$referenceHeight)
 $factor=[Math]::Max(0.65,[Math]::Min(0.80,$fit))
 $fullW=[int][Math]::Round($referenceWidth*$factor)
 $fullH=[int][Math]::Round($referenceHeight*$factor)
 $needsScroll=($fullW -gt $maxWidth -or $fullH -gt $maxHeight)
 return [pscustomobject]@{
  Scale=$factor
  FullWidth=$fullW
  FullHeight=$fullH
  ViewWidth=[int][Math]::Min($fullW,$maxWidth)
  ViewHeight=[int][Math]::Min($fullH,$maxHeight)
  MaxWidth=$maxWidth
  MaxHeight=$maxHeight
  RequiresScroll=[bool]$needsScroll
 }
}

# Reference-look home uses a 1180x840 canvas, with a 944x672 normal maximum.
# On small or high-DPI effective work areas scroll rather than clip actions.
function Set-AuroraCompactLayout {
 $referenceWidth=1180
 $heroHeight=184
 $working=[Windows.Forms.Screen]::FromControl($form).WorkingArea
 $borderW=[int][Math]::Max(0,$form.Width-$form.ClientSize.Width)
 $borderH=[int][Math]::Max(0,$form.Height-$form.ClientSize.Height)
 $plan=Get-NorthernCompactGeometry $working.Width $working.Height $borderW $borderH
 $factor=$plan.Scale
 $script:auroraScale=$factor
 $form.SuspendLayout()
 try {
  $form.MinimumSize=[Drawing.Size]::Empty
  $form.MaximumSize=[Drawing.Size]::Empty
  $form.AutoScaleMode=[Windows.Forms.AutoScaleMode]::None
  $hero.SetBounds(0,0,$referenceWidth,$heroHeight)
  Resize-AuroraControlTree $form $factor
  $fullW=$plan.FullWidth
  $fullH=$plan.FullHeight
  $needsScroll=$plan.RequiresScroll
  if($needsScroll){
   $form.AutoScrollMinSize=[Drawing.Size]::new($fullW,$fullH)
  }else{$form.AutoScrollMinSize=[Drawing.Size]::Empty}
  $form.AutoScroll=$needsScroll
  $form.ClientSize=[Drawing.Size]::new($plan.ViewWidth,$plan.ViewHeight)
  $form.MinimumSize=$form.Size
  $form.MaximumSize=$form.Size
 }finally{$form.ResumeLayout($true)}
 $hero.Invalidate()
 $script:northernHome.Invalidate()
}


# The approved screenshot is supplied as a local, versioned JPEG atlas.
# Source rows 0..275 contain the illustrative hero, rows 276..355 the
# Northern Dvina / White Sea footer; no fake statuses or baked buttons.
# A missing/corrupt asset falls back to inbox GDI+ with no download.
$script:approvedAtlas=$null
$atlasPath=Join-Path $PSScriptRoot 'launcher-approved-reference-atlas.jpg'
if(-not(Test-Path -LiteralPath $atlasPath -PathType Leaf)){
 $sourceAtlas=Join-Path (Split-Path -Parent $PSScriptRoot) 'assets\launcher-approved-reference-atlas.jpg'
 if(Test-Path -LiteralPath $sourceAtlas -PathType Leaf){$atlasPath=$sourceAtlas}
}
if(Test-Path -LiteralPath $atlasPath -PathType Leaf){
 try{
  $item=Get-Item -LiteralPath $atlasPath -Force
  if($item.Length -ge 4096 -and $item.Length -le 1024KB -and
     ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0){
   $candidate=[Drawing.Image]::FromFile($atlasPath)
   if($candidate.Width -eq 960 -and $candidate.Height -eq 356){
    $script:approvedAtlas=$candidate
   }else{$candidate.Dispose()}
  }
 }catch{$script:approvedAtlas=$null}
}

# Approved Northern Compact home (2026-11-01, final/fourth concept).
# Use only the existing GDI+/WinForms runtime and the bundled panorama.
# All updates, drivers, safe launch and recovery remain handled above.
# Service Cockpit becomes the detailed Updates view behind this home.
$script:northernHome=New-Object Windows.Forms.Panel
$script:northernHome.SetBounds(0,0,1180,840)
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
 # Full 1180x840 scene: aurora sky, panoramic motor scene, three compact
 # prerequisite cards, broad central Start, 3+2 actions and river/seacoast.
 $base=[Drawing.Drawing2D.LinearGradientBrush]::new(
  [Drawing.Rectangle]::new(0,0,1180,840),
  [Drawing.Color]::FromArgb(9,23,47),
  [Drawing.Color]::FromArgb(7,30,52),90.0)
 try{$g.FillRectangle($base,0,0,1180,840)}finally{$base.Dispose()}
 if($script:heroBackdrop){
  # Old JPEG contains its own wordmark on the left; display only the
  # right-hand automotive panorama, with dark glazing for new text.
  $picture=$script:heroBackdrop
  $source=[Drawing.Rectangle]::new(
   [int]($picture.Width*0.46),0,
   [int]($picture.Width*0.54),[int]$picture.Height)
  $g.DrawImage($picture,[Drawing.Rectangle]::new(0,0,1180,338),
   $source,[Drawing.GraphicsUnit]::Pixel)
 }
 $night=[Drawing.Drawing2D.LinearGradientBrush]::new(
  [Drawing.Rectangle]::new(0,0,1180,348),
  [Drawing.Color]::FromArgb(245,5,21,44),
  [Drawing.Color]::FromArgb(105,7,28,52),0.0)
 $veil=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(62,6,24,47))
 $metal=[Drawing.Pen]::new([Drawing.Color]::FromArgb(218,200,227,240),7)
 $gear=[Drawing.Pen]::new([Drawing.Color]::FromArgb(227,122,169,191),12)
 $bird=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(245,237,249,255))
 $wave=[Drawing.Pen]::new([Drawing.Color]::FromArgb(204,63,174,191),5)
 $hairline=[Drawing.Pen]::new([Drawing.Color]::FromArgb(133,74,203,202),2)
 try{
  $g.FillRectangle($night,0,0,620,348)
  $g.FillRectangle($veil,620,0,560,348)
  # Northern aurora ribbons are local and offline, not a downloaded visual.
  foreach($band in @(
   @{Y=25;C=[Drawing.Color]::FromArgb(64,19,215,145)},
   @{Y=45;C=[Drawing.Color]::FromArgb(60,116,94,236)},
   @{Y=65;C=[Drawing.Color]::FromArgb(54,27,192,220)})){
   $ribbon=[Drawing.Pen]::new($band.C,32)
   try{
    $g.DrawBezier($ribbon,310,[single]$band.Y,580,[single]($band.Y-78),
     760,[single]($band.Y+90),1100,[single]($band.Y-64))
   }finally{$ribbon.Dispose()}
  }
  $g.DrawEllipse($metal,58,46,226,226)
  for($i=0;$i -lt 12;$i++){
   $a=[Math]::PI*$i/6.0
   $g.DrawLine($gear,
    [single](171+117*[Math]::Cos($a)),
    [single](159+117*[Math]::Sin($a)),
    [single](171+130*[Math]::Cos($a)),
    [single](159+130*[Math]::Sin($a)))
  }
  $rivet=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(230,232,241,247))
  try{
   for($i=0;$i -lt 12;$i++){
    $a=[Math]::PI*$i/6.0
    $g.FillEllipse($rivet,
     [single](167+113*[Math]::Cos($a)),
     [single](155+113*[Math]::Sin($a)),8,8)
   }
  }finally{$rivet.Dispose()}
  # Gull, steel sea waves and a real through-arrow, not an update badge.
  $g.FillPolygon($bird,[Drawing.Point[]]@(
   [Drawing.Point]::new(83,153),[Drawing.Point]::new(119,129),
   [Drawing.Point]::new(148,131),[Drawing.Point]::new(169,149),
   [Drawing.Point]::new(195,129),[Drawing.Point]::new(227,125),
   [Drawing.Point]::new(265,141),[Drawing.Point]::new(236,141),
   [Drawing.Point]::new(207,153),[Drawing.Point]::new(186,170),
   [Drawing.Point]::new(171,168),[Drawing.Point]::new(154,178),
   [Drawing.Point]::new(125,169),[Drawing.Point]::new(143,155),
   [Drawing.Point]::new(113,147)))
  $g.DrawBezier($wave,88,211,143,182,166,226,220,204)
  $g.DrawBezier($wave,90,229,148,205,181,246,245,224)
  $g.DrawLine($metal,222,106,305,25)
  $g.DrawLine($metal,305,25,300,72)
  $g.DrawLine($metal,305,25,259,29)
  # Fine separator, as on the approved reference.
  $g.DrawLine($hairline,28,345,1152,345)
  $g.DrawLine($hairline,28,829,1152,829)
  # Three framed status cards. Native labels carry real worker evidence.
  $cardBack=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(222,13,43,66))
  try{
   foreach($left in @(35,413,791)){
    $g.FillRectangle($cardBack,$left,361,355,77)
    $g.DrawRectangle($hairline,$left,361,355,77)
   }
  }finally{$cardBack.Dispose()}
  # A purple runtime tile, four WinApp panes, and J2534 connector.
  $purple=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(104,53,183))
  $blue=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(37,164,234))
  try{
   $g.FillRectangle($purple,51,373,53,53)
   foreach($left in @(433,463)){
    foreach($top in @(376,405)){$g.FillRectangle($blue,$left,$top,24,24)}
   }
   $g.DrawRectangle($metal,810,379,66,39)
   for($i=0;$i -lt 4;$i++){
    $g.FillEllipse($bird,822+$i*13,388,5,5)
    $g.FillEllipse($bird,822+$i*13,400,5,5)
   }
  }finally{$purple.Dispose();$blue.Dispose()}
  # Lower background ribbons, far riverbanks and the White Sea.
  $ribbon=[Drawing.Pen]::new([Drawing.Color]::FromArgb(80,42,200,194),18)
  $violet=[Drawing.Pen]::new([Drawing.Color]::FromArgb(56,121,82,220),13)
  try{
   $g.DrawBezier($ribbon,0,566,220,505,540,617,1180,548)
   $g.DrawBezier($violet,0,580,300,535,750,603,1180,548)
  }finally{$ribbon.Dispose();$violet.Dispose()}
  $coast=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(24,90,110))
  try{
   $g.FillPolygon($coast,[Drawing.Point[]]@(
    [Drawing.Point]::new(0,818),[Drawing.Point]::new(0,790),
    [Drawing.Point]::new(90,763),[Drawing.Point]::new(170,800),
    [Drawing.Point]::new(260,785),[Drawing.Point]::new(380,840)))
   $g.FillPolygon($coast,[Drawing.Point[]]@(
    [Drawing.Point]::new(910,840),[Drawing.Point]::new(1035,779),
    [Drawing.Point]::new(1095,788),[Drawing.Point]::new(1140,758),
    [Drawing.Point]::new(1180,782),[Drawing.Point]::new(1180,840)))
  }finally{$coast.Dispose()}
  # The supplied screenshot's *art-only* layers override the vector fallback
  # in RU, after scenery painting but before real WinForms controls are drawn.
  # They contain no fictitious green "Ready" tiles or clickable buttons.
  if($script:approvedAtlas -and $script:launcherLanguage -eq 'RU'){
   $g.DrawImage($script:approvedAtlas,
    [Drawing.Rectangle]::new(0,0,1180,348),
    [Drawing.Rectangle]::new(0,0,960,276),
    [Drawing.GraphicsUnit]::Pixel)
   $g.DrawImage($script:approvedAtlas,
    [Drawing.Rectangle]::new(0,778,1180,62),
    [Drawing.Rectangle]::new(0,276,960,80),
    [Drawing.GraphicsUnit]::Pixel)
  }
  $footerFont=[Drawing.Font]::new('Segoe UI',[single]12,[Drawing.FontStyle]::Regular)
  $footerBrush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(195,220,236))
  try{
   if(-not ($script:approvedAtlas -and $script:launcherLanguage -eq 'RU')){
    $footer='СЕВЕРНАЯ ДВИНА  ·  БЕЛОЕ МОРЕ  ·  НАСЛЕДИЕ ВИКИНГОВ'
    $sz=$g.MeasureString($footer,$footerFont)
    $g.DrawString($footer,$footerFont,$footerBrush,[single]((1180-$sz.Width)/2),799)
   }
  }finally{$footerFont.Dispose();$footerBrush.Dispose()}
 }finally{
  $night.Dispose();$veil.Dispose();$metal.Dispose()
  $gear.Dispose();$bird.Dispose();$wave.Dispose();$hairline.Dispose()
 }
})
# The user-approved screenshot is the visual contract: a tall panoramic
# northern header, 3 equal status cards, a dominant launch action, five
# working controls in a 3+2 grid, and White Sea / Northern Dvina footer.
$script:northTitle=New-NorthernLabel 'DVINHOLM' 366 70 770 65 43 $true
$script:northSubtitle=New-NorthernLabel 'LAUNCHER' 368 137 735 64 39 $true
$script:northCaption=New-NorthernLabel 'СЕРВИСЫ  ·  ИНСТРУМЕНТЫ  ·  ДРАЙВЕРЫ' 370 218 770 32 13
$script:northSource=New-NorthernLabel 'ДЛЯ VOLVO' 371 253 650 27 13
$script:northRuntimeTitle=New-NorthernLabel '.NET Runtime' 117 373 260 29 17 $true
$script:northWinAppTitle=New-NorthernLabel 'WinApp Runtime' 505 373 262 29 17 $true
$script:northDriverTitle=New-NorthernLabel 'J2534' 892 373 237 29 17 $true
$script:northRuntime=New-NorthernLabel '.NET x86: не проверено' 117 405 265 25 11 $true
$script:northWinApp=New-NorthernLabel 'WinApp: не проверено' 505 405 261 25 11 $true
$script:northDriver=New-NorthernLabel 'J2534: не проверено' 892 405 241 25 11 $true
$script:northLaunch=New-NorthernButton '▶  ЗАПУСТИТЬ DVINHOLM' 272 467 636 81 $true
$script:northUpdates=New-NorthernButton '↻  ОБНОВЛЕНИЯ' 44 577 351 68
$script:northComponents=New-NorthernButton '◇  КОМПОНЕНТЫ' 414 577 351 68
$script:northDrivers=New-NorthernButton '▣  ДРАЙВЕРЫ J2534' 785 577 351 68
$script:northSettings=New-NorthernButton '⚙  НАСТРОЙКИ' 44 660 541 68
$script:northAbout=New-NorthernButton 'ⓘ  О ПРОГРАММЕ' 595 660 541 68
$script:northVersion=New-NorthernLabel 'Версия: не проверена' 44 750 336 28 9
$script:northChannel=New-NorthernLabel 'Канал: не проверен' 393 750 333 28 9
$script:northActivity=New-NorthernLabel 'Ожидание проверки компонентов' 724 746 430 32 9

# Frame and caption are rendered by WinForms; each button remains a real
# accessible button with a functional Click event. Owner painting handles
# round geometry without introducing a browser or graphics dependency.
function Set-NorthernButtonArtwork([Windows.Forms.Button]$button,[bool]$primary){
 $button.Tag=if($primary){'Primary'}else{'Secondary'}
 $button.FlatAppearance.BorderSize=0
 $button.Add_Paint({
  param($sender,$event)
  $graphics=$event.Graphics
  $graphics.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $w=$sender.ClientSize.Width;$h=$sender.ClientSize.Height
  if($w -lt 12 -or $h -lt 12){return}
  $radius=[Math]::Min([int]($h/2),$(if($sender.Tag -eq 'Primary'){38}else{14}))
  $p=[Drawing.Drawing2D.GraphicsPath]::new()
  try{
   $d=$radius*2
   $p.AddArc(1,1,$d,$d,180,90)
   $p.AddArc($w-$d-2,1,$d,$d,270,90)
   $p.AddArc($w-$d-2,$h-$d-2,$d,$d,0,90)
   $p.AddArc(1,$h-$d-2,$d,$d,90,90)
   $p.CloseFigure()
   $isPrimary=($sender.Tag -eq 'Primary')
   $hot=($isPrimary -and $sender.Enabled)
   $top=if($hot){[Drawing.Color]::FromArgb(20,185,142)}elseif($isPrimary){
    [Drawing.Color]::FromArgb(14,99,91)}else{[Drawing.Color]::FromArgb(35,65,94)}
   $bottom=if($hot){[Drawing.Color]::FromArgb(8,106,113)}elseif($isPrimary){
    [Drawing.Color]::FromArgb(9,58,74)}else{[Drawing.Color]::FromArgb(17,37,62)}
   $brush=[Drawing.Drawing2D.LinearGradientBrush]::new(
    [Drawing.Rectangle]::new(0,0,$w,$h),$top,$bottom,90.0)
   $stroke=[Drawing.Pen]::new(
    $(if($hot){[Drawing.Color]::FromArgb(64,241,193)}else{
    [Drawing.Color]::FromArgb(76,151,181)}),2)
   try{
    $graphics.FillPath($brush,$p)
    $graphics.DrawPath($stroke,$p)
    $color=if($sender.Enabled){[Drawing.Color]::White}else{
     [Drawing.Color]::FromArgb(204,220,231)}
    $flags=[Windows.Forms.TextFormatFlags]::HorizontalCenter -bor
     [Windows.Forms.TextFormatFlags]::VerticalCenter -bor
     [Windows.Forms.TextFormatFlags]::SingleLine -bor
     [Windows.Forms.TextFormatFlags]::EndEllipsis
    [Windows.Forms.TextRenderer]::DrawText($graphics,[string]$sender.Text,
     $sender.Font,[Drawing.Rectangle]::new(10,3,$w-20,$h-6),$color,$flags)
   }finally{$brush.Dispose();$stroke.Dispose()}
  }finally{$p.Dispose()}
 })
}
Set-NorthernButtonArtwork $script:northLaunch $true
foreach($action in @($script:northUpdates,$script:northComponents,
                      $script:northDrivers,$script:northSettings,$script:northAbout)){
 Set-NorthernButtonArtwork $action $false
}
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
$script:northAbout.Add_Click({
 [void][Windows.Forms.MessageBox]::Show($form,(Translate 'aboutMessage'),
  'Dvinholm Launcher',[Windows.Forms.MessageBoxButtons]::OK,
  [Windows.Forms.MessageBoxIcon]::Information)
})
function Update-NorthernCompactSnapshot {
 if($null -eq $script:northLaunch -or $script:northLaunch.IsDisposed){return}
 # Copy actual worker and inventory readings. No fabricated version or
 # successful J2534/DiCE hardware connection is shown.
 $script:northLaunch.Enabled=$launch.Enabled
 $script:northComponents.Enabled=$depsInstallQuick.Enabled
 $script:northDrivers.Enabled=$driverQuick.Enabled
 $script:northSettings.Enabled=$settingsQuick.Enabled
 $script:northAbout.Enabled=($null -eq $script:child -and $null -eq $script:vendor)
 $script:northVersion.Text=[string]$installed.Text
 if($script:launcherLanguage -eq 'RU'){
  $script:northVersion.Text=$script:northVersion.Text.Replace('Not checked','не проверено')
 }
 $script:northChannel.Text='Канал: '+[string]$choice.SelectedItem
 # Until recovery and prerequisites are verified, do not repeat the worker's
 # generic 'Ready.' phrase: Start remains disabled and the footer must agree.
 $script:northActivity.Text=if($script:recoveryStatus -ceq 'Ready'){
  $status.Text
 }else{$recovery.Text}
 $parts=$deps.Text
 foreach($spec in @(
  @($script:northRuntime,'\.NET x86:\s*([^ ]+)',' .NET x86: '),
  @($script:northWinApp,'WinApp:\s*([^ ]+)',' WinApp: ')
  )){
  $value=if($parts -match $spec[1]){$Matches[1]}else{''}
  if([string]::IsNullOrWhiteSpace($value) -or $value -ceq '?'){
   $value=if($script:launcherLanguage -eq 'RU'){'не проверено'}else{'not checked'}
  }
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
  $script:northSubtitle.Text='LAUNCHER'
  $script:northCaption.Text='SERVICES  ·  TOOLS  ·  DRIVERS'
  $script:northSource.Text='FOR VOLVO'
  $script:northLaunch.Text='▶  START DVINHOLM'
  $script:northUpdates.Text='↻  UPDATES'
  $script:northComponents.Text='◇  COMPONENTS'
  $script:northDrivers.Text='▣  J2534 DRIVERS'
  $script:northSettings.Text='⚙  SETTINGS'
  $script:northAbout.Text='ⓘ  ABOUT'
  $script:northReturn.Text='← Home'
  $script:northChannel.Text='Channel: '+[string]$choice.SelectedItem
 }else{
  $script:northTitle.Text='DVINHOLM'
  $script:northSubtitle.Text='LAUNCHER'
  $script:northCaption.Text='СЕРВИСЫ  ·  ИНСТРУМЕНТЫ  ·  ДРАЙВЕРЫ'
  $script:northSource.Text='ДЛЯ VOLVO'
  $script:northLaunch.Text='▶  ЗАПУСТИТЬ DVINHOLM'
  $script:northUpdates.Text='↻  ОБНОВЛЕНИЯ'
  $script:northComponents.Text='◇  КОМПОНЕНТЫ'
  $script:northDrivers.Text='▣  ДРАЙВЕРЫ J2534'
  $script:northSettings.Text='⚙  НАСТРОЙКИ'
  $script:northAbout.Text='ⓘ  О ПРОГРАММЕ'
  $script:northReturn.Text='← Главная'
 }
 # Text and exact user artwork are mutually exclusive: no double printing.
 $useReference=($null -ne $script:approvedAtlas -and
                $script:launcherLanguage -eq 'RU')
 foreach($label in @($script:northTitle,$script:northSubtitle,
                     $script:northCaption,$script:northSource)){
  $label.Visible=-not $useReference
 }
 $script:northernHome.Invalidate()

}
# The hidden back button is created after the home panel and would otherwise
# take the top z-order before the window becomes visible.
$script:northernHome.BringToFront()
$form.Add_FormClosed({
 if($script:approvedAtlas){$script:approvedAtlas.Dispose();$script:approvedAtlas=$null}
})
# Show the compact approved screen on normal launch. Existing Service
# Cockpit remains accessible with all real actions from Updates.
