#requires -Version 5.1
<#
Approved Aurora concept for the Dvinholm Launcher.
Presentation only: PowerShell 5.1 + inbox WinForms/System.Drawing.
The updater, dependency inventory, and transaction engine remain unchanged.
#>
$form.Text='Dvinholm Launcher'
# Layout coordinates below describe the 1180 x 790 reference canvas.
# Apply compact bounds after ALL controls (including Drivers/Versions) exist.
$script:auroraScale=1.0
$form.MinimumSize=[Drawing.Size]::Empty
$form.MaximumSize=[Drawing.Size]::Empty
$form.ClientSize=[Drawing.Size]::new(1180,790)
$form.MinimumSize=$form.Size;$form.MaximumSize=$form.Size
$form.BackColor=[Drawing.Color]::FromArgb(8,19,34)
$form.ForeColor=[Drawing.Color]::FromArgb(228,239,250)
$form.MaximizeBox=$false
$hero.SetBounds(0,0,1180,232)
# This is the original repository banner, bundled for offline use. The Win11
# WIC decoder may be absent on LTSC systems, so keep the painted Aurora
# panorama as a fully functional dependency-free fallback.
$script:heroBackdrop=$null
$script:heroBackdropStream=$null
$script:serviceBackdropIsApproved=$false
# The exact upper banner from the approved Service Cockpit reference is a
# compact local JPEG. GDI+ decodes it without .NET 10, WinApp or WebP codecs.
$approvedBanner=Join-Path $PSScriptRoot 'launcher-service-cockpit-banner.jpg'
if(Test-Path -LiteralPath $approvedBanner -PathType Leaf){
 try {
  $script:heroBackdrop=[Drawing.Image]::FromFile($approvedBanner)
  $script:serviceBackdropIsApproved=$true
 }catch{$script:heroBackdrop=$null}
}
# Older launcher packages retain their approved repository WebP banner.
$bannerPath=Join-Path $PSScriptRoot 'dvinholm-banner.webp'
if(-not $script:heroBackdrop -and
   -not(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Launcher-ServiceCockpit.ps1') -PathType Leaf) -and
   (Test-Path -LiteralPath $bannerPath -PathType Leaf)){
 try {
  Add-Type -AssemblyName PresentationCore -ErrorAction Stop
  Add-Type -AssemblyName WindowsBase -ErrorAction Stop
  $source=[Windows.Media.Imaging.BitmapImage]::new()
  $source.BeginInit()
  $source.CacheOption=[Windows.Media.Imaging.BitmapCacheOption]::OnLoad
  $source.UriSource=[Uri]::new($bannerPath)
  $source.EndInit()
  $encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new()
  $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($source))
  $script:heroBackdropStream=New-Object IO.MemoryStream
  $encoder.Save($script:heroBackdropStream)
  $script:heroBackdropStream.Position=0
  $script:heroBackdrop=[Drawing.Image]::FromStream($script:heroBackdropStream)
 }catch{
  $script:heroBackdrop=$null
  if($script:heroBackdropStream){$script:heroBackdropStream.Dispose()}
  $script:heroBackdropStream=$null
 }
}
$hero.Add_Paint({
 $g=$_.Graphics
 $g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
 if($script:heroBackdrop){
  $image=$script:heroBackdrop
  $scale=[Math]::Max([double]$hero.Width/$image.Width,[double]$hero.Height/$image.Height)
  $width=[single]($image.Width*$scale)
  $height=[single]($image.Height*$scale)
  $g.DrawImage($image,[Drawing.RectangleF]::new(
   [single](($hero.Width-$width)/2),[single](($hero.Height-$height)/2),$width,$height))
  return
 }
 $g.ScaleTransform([single]$script:auroraScale,[single]($script:auroraScale*(182.0/232.0)))
 $sky=[Drawing.Drawing2D.LinearGradientBrush]::new(
  [Drawing.Rectangle]::new(0,0,1180,232),
  [Drawing.Color]::FromArgb(10,30,61),
  [Drawing.Color]::FromArgb(9,67,93),0.0)
 try{$g.FillRectangle($sky,0,0,1180,232)}finally{$sky.Dispose()}
 foreach($r in @(
  @{Color=[Drawing.Color]::FromArgb(55,35,228,182);Offset=0},
  @{Color=[Drawing.Color]::FromArgb(48,42,150,235);Offset=23},
  @{Color=[Drawing.Color]::FromArgb(36,101,235,195);Offset=45})){
  $o=$r.Offset
  $points=[Drawing.Point[]]@(
   [Drawing.Point]::new(430,58+$o),
   [Drawing.Point]::new(670,4+$o),
   [Drawing.Point]::new(880,32+$o),
   [Drawing.Point]::new(1110,0+$o),
   [Drawing.Point]::new(1180,122+$o),
   [Drawing.Point]::new(893,105+$o),
   [Drawing.Point]::new(684,140+$o))
  $brush=[Drawing.SolidBrush]::new($r.Color)
  try{$g.FillPolygon($brush,$points)}finally{$brush.Dispose()}
 }
 $star=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(140,215,235,252))
 try{
  for($i=0;$i -lt 45;$i++){
   $g.FillEllipse($star,350+(($i*83)%810),7+(($i*37)%116),1.6,1.6)
  }
 }finally{$star.Dispose()}
 $far=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(34,61,93))
 $near=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(8,27,48))
 $water=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(16,57,82))
 try{
  $g.FillPolygon($far,[Drawing.Point[]]@(
   [Drawing.Point]::new(0,183),[Drawing.Point]::new(125,116),
   [Drawing.Point]::new(254,172),[Drawing.Point]::new(369,107),
   [Drawing.Point]::new(510,179),[Drawing.Point]::new(623,126),
   [Drawing.Point]::new(794,172),[Drawing.Point]::new(946,130),
   [Drawing.Point]::new(1180,182),[Drawing.Point]::new(1180,232),
   [Drawing.Point]::new(0,232)))
  $g.FillRectangle($water,0,178,1180,54)
  $g.FillPolygon($near,[Drawing.Point[]]@(
   [Drawing.Point]::new(0,195),[Drawing.Point]::new(143,153),
   [Drawing.Point]::new(263,191),[Drawing.Point]::new(392,150),
   [Drawing.Point]::new(520,208),[Drawing.Point]::new(645,175),
   [Drawing.Point]::new(795,215),[Drawing.Point]::new(930,184),
   [Drawing.Point]::new(1100,222),[Drawing.Point]::new(1180,201),
   [Drawing.Point]::new(1180,232),[Drawing.Point]::new(0,232)))
 }finally{$far.Dispose();$near.Dispose();$water.Dispose()}
 # Minimal automotive silhouette on the right edge of the panoramic fjord.
 $carBody=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(5,17,29))
 $glass=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(24,76,106))
 $rim=[Drawing.Pen]::new([Drawing.Color]::FromArgb(114,166,202),1)
 try{
  $g.FillPolygon($carBody,[Drawing.Point[]]@(
   [Drawing.Point]::new(917,192),[Drawing.Point]::new(934,170),
   [Drawing.Point]::new(962,159),[Drawing.Point]::new(990,139),
   [Drawing.Point]::new(1098,139),[Drawing.Point]::new(1127,162),
   [Drawing.Point]::new(1164,172),[Drawing.Point]::new(1174,195),
   [Drawing.Point]::new(1170,208),[Drawing.Point]::new(918,208)))
  $g.FillPolygon($glass,[Drawing.Point[]]@(
   [Drawing.Point]::new(992,147),[Drawing.Point]::new(1040,145),
   [Drawing.Point]::new(1040,166),[Drawing.Point]::new(968,166)))
  $g.FillPolygon($glass,[Drawing.Point[]]@(
   [Drawing.Point]::new(1048,145),[Drawing.Point]::new(1091,146),
   [Drawing.Point]::new(1120,166),[Drawing.Point]::new(1048,166)))
  $g.FillEllipse($carBody,962,196,35,35)
  $g.FillEllipse($carBody,1116,196,35,35)
  $g.DrawEllipse($rim,968,203,21,21)
  $g.DrawEllipse($rim,1123,203,21,21)
  $g.DrawLine($rim,918,190,1171,190)
 }finally{$carBody.Dispose();$glass.Dispose();$rim.Dispose()}
})
$brandTitle.Text='DVINHOLM'
$brandTitle.Font=[Drawing.Font]::new('Segoe UI',39,[Drawing.FontStyle]::Bold)
$brandTitle.SetBounds(220,66,700,76)
$brandSub.Text='DIAGNOSTICS  ·  CEM  ·  CONFIGURATION'
$brandSub.Font=[Drawing.Font]::new('Segoe UI',11)
$brandSub.SetBounds(225,144,700,32)
if($null -ne $logo){$logo.SetBounds(45,42,145,143)}

function AuroraCard([int]$x,[int]$y,[int]$w,[int]$h){
 $p=New-Object Windows.Forms.Panel
 $p.SetBounds($x,$y,$w,$h)
 $p.BackColor=[Drawing.Color]::FromArgb(14,34,57)
 # The old advanced-recovery card is not part of the new compact dashboard.
 $p.Visible=($y -ne 713)
 $p.BorderStyle=[Windows.Forms.BorderStyle]::FixedSingle
 $form.Controls.Add($p);$p.SendToBack()
}
AuroraCard 239 246 391 150
AuroraCard 641 246 252 150
AuroraCard 903 246 261 150
AuroraCard 239 407 925 172
AuroraCard 239 591 925 110
AuroraCard 239 713 925 62

$sidebar=New-Object Windows.Forms.Panel
$sidebar.SetBounds(0,233,218,557)
$sidebar.BackColor=[Drawing.Color]::FromArgb(8,27,46)
$form.Controls.Add($sidebar)
function AuroraNav([string]$text,[int]$y){
 $b=New-Object Windows.Forms.Button
 $b.Text=$text;$b.SetBounds(12,$y,194,46)
 $b.FlatStyle=[Windows.Forms.FlatStyle]::Flat
 $b.FlatAppearance.BorderSize=0
 $b.TextAlign=[Drawing.ContentAlignment]::MiddleLeft
 $b.ForeColor=[Drawing.Color]::FromArgb(214,231,244)
 $b.BackColor=$sidebar.BackColor
 $b.Cursor=[Windows.Forms.Cursors]::Hand
 $sidebar.Controls.Add($b)
 return $b
}
$navHome=AuroraNav '⌂   Главная' 50
$navUpdates=AuroraNav '↻   Обновления' 105
$navDependencies=AuroraNav '◈   Зависимости' 160
$navSettings=AuroraNav '☷   Настройки' 215
$navAbout=AuroraNav 'ⓘ   О программе' 270
$script:auroraNav=@($navHome,$navUpdates,$navDependencies,$navSettings,$navAbout)
function SelectAuroraNav([Windows.Forms.Button]$item){
 foreach($nav in $script:auroraNav){
  $nav.BackColor=[Drawing.Color]::FromArgb(8,27,46)
  $nav.ForeColor=[Drawing.Color]::FromArgb(214,231,244)
 }
 $item.BackColor=[Drawing.Color]::FromArgb(16,80,155)
 $item.ForeColor=[Drawing.Color]::White
}
SelectAuroraNav $navHome
# Every navigation entry performs a real, named action. No focus-only links.
$navHome.Add_Click({SelectAuroraNav $navHome;Begin-Action 'Launch'})
$navUpdates.Add_Click({SelectAuroraNav $navUpdates;Begin-Action 'Check'})
$navDependencies.Add_Click({SelectAuroraNav $navDependencies;Begin-Action 'Probe'})
$navSettings.Add_Click({SelectAuroraNav $navSettings;Show-LauncherSettings})
$navAbout.Add_Click({
 SelectAuroraNav $navAbout
 [void][Windows.Forms.MessageBox]::Show($form,
  (Translate 'aboutMessage'),
  'Dvinholm Launcher',[Windows.Forms.MessageBoxButtons]::OK)
})
$sidebarFooter=New-Object Windows.Forms.Label
$sidebarFooter.Text='DVINHOLM LAUNCHER'+[Environment]::NewLine+'WINDOWS X86'
$sidebarFooter.ForeColor=[Drawing.Color]::FromArgb(112,161,193)
$sidebarFooter.SetBounds(22,502,180,45)
$sidebar.Controls.Add($sidebarFooter)

# The worker still reads the same ComboBox; these two visible radios are
# mirrored so the current channel cannot differ from the actual worker target.
$choice.Visible=$false
$stableRadio=New-Object Windows.Forms.RadioButton
$stableRadio.Text='Stable  ·  официальный релиз'
$stableRadio.SetBounds(259,288,350,38)
$stableRadio.ForeColor=[Drawing.Color]::White
$stableRadio.BackColor=[Drawing.Color]::FromArgb(18,54,84)
$stableRadio.Font=[Drawing.Font]::new('Segoe UI',11)
$form.Controls.Add($stableRadio)
$developmentRadio=New-Object Windows.Forms.RadioButton
$developmentRadio.Text='Development  ·  последние CI'
$developmentRadio.SetBounds(259,338,350,38)
$developmentRadio.ForeColor=[Drawing.Color]::White
$developmentRadio.BackColor=[Drawing.Color]::FromArgb(18,54,84)
$developmentRadio.Font=[Drawing.Font]::new('Segoe UI',11)
$form.Controls.Add($developmentRadio)
$stableRadio.Add_CheckedChanged({
 if($stableRadio.Checked -and $choice.SelectedIndex -ne 1){$choice.SelectedIndex=1}
})
$developmentRadio.Add_CheckedChanged({
 if($developmentRadio.Checked -and $choice.SelectedIndex -ne 0){$choice.SelectedIndex=0}
})
$choice.Add_SelectedIndexChanged({
 $stableRadio.Checked=($choice.SelectedIndex -eq 1)
 $developmentRadio.Checked=($choice.SelectedIndex -eq 0)
})

$installed.SetBounds(661,289,215,66)
$available.SetBounds(922,289,227,66)
$installed.Font=[Drawing.Font]::new('Segoe UI',12,[Drawing.FontStyle]::Bold)
$available.Font=[Drawing.Font]::new('Segoe UI',12,[Drawing.FontStyle]::Bold)
$check.SetBounds(256,446,178,37)
$install.SetBounds(448,446,237,37)
$launch.SetBounds(699,446,205,37)
$refresh.SetBounds(918,446,225,37)
$install.BackColor=[Drawing.Color]::FromArgb(10,108,219)
$launch.BackColor=[Drawing.Color]::FromArgb(15,107,134)
$status.SetBounds(257,486,870,39)
$status.AutoEllipsis=$true
$bar.SetBounds(257,531,867,16)
$recovery.SetBounds(260,552,718,23)
$deps.SetBounds(258,630,869,28)
$dot.SetBounds(257,663,270,34)
$win.SetBounds(543,663,270,34)
$vc.SetBounds(828,663,310,34)
$rollback.SetBounds(257,730,265,35)
$repair.SetBounds(535,730,285,35)
$repairInstall.SetBounds(833,730,300,35)
# Recovery is an advanced action in the working Settings dialog, not an
# always-visible row stealing space from the approved Aurora banner.
$rollback.Visible=$false
$repair.Visible=$false
$repairInstall.Visible=$false
$foot.Visible=$false;$title.Visible=$false;$strap.Visible=$false

function AuroraText([string]$text,[int]$x,[int]$y,[int]$w){
 $l=New-Object Windows.Forms.Label
 $l.Text=$text;$l.SetBounds($x,$y,$w,25)
 $l.Font=[Drawing.Font]::new('Segoe UI',10,[Drawing.FontStyle]::Bold)
 $l.ForeColor=[Drawing.Color]::FromArgb(142,202,236)
 $form.Controls.Add($l)
 return $l
}
$channelTitle=AuroraText 'КАНАЛ ОБНОВЛЕНИЙ' 258 256 344
$progressTitle=AuroraText 'ЗАГРУЗКА И УСТАНОВКА' 258 408 370
$dependenciesTitle=AuroraText 'ЗАВИСИМОСТИ WINDOWS' 258 601 365
$downloadPercent=New-Object Windows.Forms.Label
$downloadPercent.Text=''
$downloadPercent.SetBounds(1021,550,106,25)
$downloadPercent.TextAlign=[Drawing.ContentAlignment]::MiddleRight
$downloadPercent.ForeColor=[Drawing.Color]::FromArgb(106,203,237)
$form.Controls.Add($downloadPercent)
foreach($c in @($installed,$available,$stableRadio,$developmentRadio,$check,
 $install,$launch,$refresh,$status,$bar,$recovery,$deps,$dot,$win,$vc,
 $rollback,$repair,$repairInstall,$channelTitle,$progressTitle,
 $dependenciesTitle,$downloadPercent)){
 $c.BringToFront()
}
# The existing Aurora artwork remains the prominent top banner. Language
# selection stays inside the hero so it never creates another bottom row.
$languageChoice=New-Object Windows.Forms.ComboBox
$languageChoice.DropDownStyle=[Windows.Forms.ComboBoxStyle]::DropDownList
[void]$languageChoice.Items.Add('Русский')
[void]$languageChoice.Items.Add('English')
$languageChoice.SetBounds(1026,17,134,32)
$languageChoice.BackColor=[Drawing.Color]::FromArgb(18,46,70)
$languageChoice.ForeColor=[Drawing.Color]::White
$hero.Controls.Add($languageChoice)
$hero.BringToFront()
$sidebar.BringToFront()

# Scale existing WinForms controls once, after themed and auxiliary controls
# have been added.  Parent/child coordinates are scaled independently so
# nav buttons, labels, cards, and the runtime-version action keep their layout.
function Resize-AuroraControlTree([Windows.Forms.Control]$parent,[double]$factor){
 foreach($control in @($parent.Controls)){
  $bounds=$control.Bounds
  $control.SetBounds(
   [int][Math]::Round($bounds.Left*$factor),
   [int][Math]::Round($bounds.Top*$factor),
   [int][Math]::Max(1,[Math]::Round($bounds.Width*$factor)),
   [int][Math]::Max(1,[Math]::Round($bounds.Height*$factor))
  )
  # Avoid chopping text by keeping small text readable while retaining
  # the original proportional font hierarchy (including the brand title).
  if($null -ne $control.Font){
   $oldFont=$control.Font
   $points=[single][Math]::Max(8.0,$oldFont.Size*$factor)
   $control.Font=[Drawing.Font]::new(
    $oldFont.FontFamily,$points,$oldFont.Style,[Drawing.GraphicsUnit]::Point)
  }
  if($control.Controls.Count -gt 0){
   Resize-AuroraControlTree $control $factor
  }
 }
}
function Set-AuroraCompactLayout {
 # The 232-pixel hero left too little room for status and repair controls.
 # Shorten it before scaling; the painted scene is vertically fitted too.
 # Shift only top-level controls below the hero (nested sidebar links retain
 # their own coordinates). This executes once when the Launcher is built.
 $heroHeight=182
 $verticalSaving=232-$heroHeight
 $referenceWidth=1180
 # Remove the old 50px bottom recovery row to give the banner real height.
 $referenceHeight=790-$verticalSaving-50
 $form.SuspendLayout()
 try {
  $hero.Height=$heroHeight
  # The original Aurora panorama and Dvinholm emblem remain visible.
  # Leave a small inset: independent rounding of sidebar Top and Height
  # otherwise gives a one-pixel overflow at 80% scale.
  $sidebar.Top=183
  $sidebar.Height=502
  $brandTitle.SetBounds(210,30,700,62)
  $brandTitle.Font=[Drawing.Font]::new('Segoe UI',34,[Drawing.FontStyle]::Bold)
  $brandSub.SetBounds(215,101,700,26)
  if($null -ne $logo){$logo.SetBounds(44,17,118,119)}
  foreach($control in @($form.Controls)){
   if($control -ne $hero -and $control.Top -ge 233){
    $control.Top-=$verticalSaving
   }
  }
  # Keep the footer clear of working driver navigation at 80% scaling.
  $sidebarFooter.Text='DVINHOLM'+[Environment]::NewLine+'Windows x86'
  $sidebarFooter.SetBounds(22,392,180,58)
 }finally{$form.ResumeLayout($false)}
 # Account for the Windows taskbar and non-client frame before selecting
 # final dimensions. Preserve the compact maximum at normal DPI.
 $working=[Windows.Forms.Screen]::FromControl($form).WorkingArea
 $borderW=[int][Math]::Max(0,$form.Width-$form.ClientSize.Width)
 $borderH=[int][Math]::Max(0,$form.Height-$form.ClientSize.Height)
 $maxWidth=[Math]::Max(1,$working.Width-32-$borderW)
 $maxHeight=[Math]::Max(1,$working.Height-32-$borderH)
 $fit=[Math]::Min([double]$maxWidth/$referenceWidth,[double]$maxHeight/$referenceHeight)
 # 944 x 571 client pixels is the normal compact maximum.
 # Very small work areas scroll instead of squeezing text to unreadability.
 $factor=[Math]::Max(0.65,[Math]::Min(0.80,$fit))
 $script:auroraScale=$factor
 $fullW=[int][Math]::Round($referenceWidth*$factor)
 $fullH=[int][Math]::Round($referenceHeight*$factor)
 $form.SuspendLayout()
 try{
  $form.MinimumSize=[Drawing.Size]::Empty
  $form.MaximumSize=[Drawing.Size]::Empty
  $form.AutoScaleMode=[Windows.Forms.AutoScaleMode]::None
  Resize-AuroraControlTree $form $factor
  $needsScroll=($fullW -gt $maxWidth -or $fullH -gt $maxHeight)
  # WinForms AutoScrollMinSize implicitly turns AutoScroll back on.
  # Assign it only for genuinely constrained work areas, then explicitly
  # set AutoScroll last so ordinary screens never get phantom scrollbars.
  if($needsScroll){
   $form.AutoScrollMinSize=[Drawing.Size]::new($fullW,$fullH)
  }else{
   $form.AutoScrollMinSize=[Drawing.Size]::Empty
  }
  $form.AutoScroll=$needsScroll
  $form.ClientSize=[Drawing.Size]::new(
   [int][Math]::Min($fullW,$maxWidth),[int][Math]::Min($fullH,$maxHeight))
  $form.MinimumSize=$form.Size
  $form.MaximumSize=$form.Size
 }finally{$form.ResumeLayout($true)}
 $hero.Invalidate()
}
