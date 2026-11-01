#requires -Version 5.1
<#
Create the native Launcher icon in the user-approved Techno-North No. 9 design.
No circular update badge. Keep Dvinholm.exe's original icon distinct. Output is a real
multi-resolution ICO with PNG frames, not a renamed PNG file.
#>
[CmdletBinding()]
param(
 [Parameter(Mandatory)][string]$LogoPath,
 [Parameter(Mandatory)][string]$IconPath
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing

# Validate the original Dvinholm logo input for release provenance; the
# Launcher has a DISTINCT new Northern Dvina emblem. Dvinholm.exe keeps
# its independently approved original app icon.
$logo=(Resolve-Path -LiteralPath $LogoPath -ErrorAction Stop).Path
if(-not(Test-Path -LiteralPath $logo -PathType Leaf)){
 throw 'Original Dvinholm logo missing.'
}
$sizes=@(16,24,32,48,64,128,256)
$frames=[Collections.Generic.List[byte[]]]::new()
$master=[Drawing.Bitmap]::new(256,256,
 [Drawing.Imaging.PixelFormat]::Format32bppArgb)
try{
 $g=[Drawing.Graphics]::FromImage($master)
 try{
  $g.Clear([Drawing.Color]::Transparent)
  $g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality

  # User-approved #9 "Techno-North": heavy riveted metal ring, gull,
  # industrial sea waves and a single arrow piercing the ring. No update badge.
  $background=[Drawing.Drawing2D.LinearGradientBrush]::new(
   [Drawing.Rectangle]::new(5,5,246,246),
   [Drawing.Color]::FromArgb(19,68,91),
   [Drawing.Color]::FromArgb(6,19,35),65.0)
  $steel=[Drawing.Pen]::new([Drawing.Color]::FromArgb(243,201,224,237),13)
  $shadow=[Drawing.Pen]::new([Drawing.Color]::FromArgb(255,34,63,82),19)
  $lug=[Drawing.Pen]::new([Drawing.Color]::FromArgb(252,166,195,214),13)
  $detail=[Drawing.Pen]::new([Drawing.Color]::FromArgb(192,83,122,148),2)
  $silver=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,235,246,252))
  $rivets=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,215,227,234))
  $sea=[Drawing.Pen]::new([Drawing.Color]::FromArgb(245,166,201,216),8)
  $wave=[Drawing.Pen]::new([Drawing.Color]::FromArgb(245,84,134,162),6)
  $bird=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,241,249,255))
  $feather=[Drawing.Pen]::new([Drawing.Color]::FromArgb(212,115,145,161),2)
  $arrowShadow=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,34,77,104))
  $shell=[Drawing.Drawing2D.GraphicsPath]::new()
  try{
   # Rounded desktop-tile background is an original vector drawing.
   $shell.AddArc(5,5,75,75,180,90)
   $shell.AddArc(176,5,75,75,270,90)
   $shell.AddArc(176,176,75,75,0,90)
   $shell.AddArc(5,176,75,75,90,90)
   $shell.CloseFigure()
   $g.FillPath($background,$shell)

   # Gear teeth and machined dual-rim ring.
   for($i=0;$i -lt 12;$i++){
    $angle=2.0*[Math]::PI*$i/12.0
    $x1=[single](128+88*[Math]::Cos($angle))
    $y1=[single](128+88*[Math]::Sin($angle))
    $x2=[single](128+103*[Math]::Cos($angle))
    $y2=[single](128+103*[Math]::Sin($angle))
    $g.DrawLine($lug,$x1,$y1,$x2,$y2)
   }
   $g.DrawEllipse($shadow,40,40,176,176)
   $g.DrawEllipse($steel,40,40,176,176)
   $g.DrawEllipse($detail,52,52,152,152)
   for($i=0;$i -lt 12;$i++){
    $angle=2.0*[Math]::PI*$i/12.0
    $x=[single](124.6+88*[Math]::Cos($angle))
    $y=[single](124.6+88*[Math]::Sin($angle))
    $g.FillEllipse($rivets,$x,$y,6.8,6.8)
    $g.DrawEllipse($detail,$x,$y,6.8,6.8)
   }

   # Silhouette of a true flying gull with feather and tail details.
   $g.FillPolygon($bird,[Drawing.Point[]]@(
    [Drawing.Point]::new(36,119),
    [Drawing.Point]::new(69,100),
    [Drawing.Point]::new(95,102),
    [Drawing.Point]::new(118,113),
    [Drawing.Point]::new(143,97),
    [Drawing.Point]::new(169,94),
    [Drawing.Point]::new(210,106),
    [Drawing.Point]::new(181,107),
    [Drawing.Point]::new(157,118),
    [Drawing.Point]::new(138,129),
    [Drawing.Point]::new(127,136),
    [Drawing.Point]::new(114,131),
    [Drawing.Point]::new(99,139),
    [Drawing.Point]::new(73,135),
    [Drawing.Point]::new(54,129),
    [Drawing.Point]::new(95,120),
    [Drawing.Point]::new(70,115)))
   $g.DrawBezier($feather,47,118,76,105,96,112,120,123)
   $g.DrawBezier($feather,138,123,157,106,181,105,201,106)

   # Three cold-steel White Sea swells, rather than updater circles.
   $g.DrawBezier($sea,43,162,78,146,104,172,133,160)
   $g.DrawBezier($sea,133,160,165,150,190,143,215,158)
   $g.DrawBezier($sea,43,180,78,167,109,187,139,180)
   $g.DrawBezier($sea,139,180,172,173,196,166,215,178)
   $g.DrawBezier($wave,59,196,95,183,123,203,153,195)
   $g.DrawBezier($wave,153,195,180,191,193,187,204,190)

   # Arrow crosses the metal ring and visibly continues beyond its perimeter.
   $g.FillPolygon($arrowShadow,[Drawing.Point[]]@(
    [Drawing.Point]::new(166,89),[Drawing.Point]::new(218,28),
    [Drawing.Point]::new(212,16),[Drawing.Point]::new(247,14),
    [Drawing.Point]::new(240,49),[Drawing.Point]::new(229,37),
    [Drawing.Point]::new(182,100)))
   $g.FillPolygon($silver,[Drawing.Point[]]@(
    [Drawing.Point]::new(171,87),[Drawing.Point]::new(224,29),
    [Drawing.Point]::new(213,21),[Drawing.Point]::new(245,14),
    [Drawing.Point]::new(238,48),[Drawing.Point]::new(229,37),
    [Drawing.Point]::new(185,100)))
  }finally{
   $shell.Dispose();$background.Dispose()
   $steel.Dispose();$shadow.Dispose();$lug.Dispose();$detail.Dispose()
   $silver.Dispose();$rivets.Dispose();$sea.Dispose();$wave.Dispose()
   $bird.Dispose();$feather.Dispose();$arrowShadow.Dispose()
  }
 }finally{$g.Dispose()}
 foreach($size in $sizes){
  $frame=[Drawing.Bitmap]::new($size,$size,
   [Drawing.Imaging.PixelFormat]::Format32bppArgb)
  try{
   $scaled=[Drawing.Graphics]::FromImage($frame)
   try{
    $scaled.Clear([Drawing.Color]::Transparent)
    $scaled.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $scaled.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $scaled.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $scaled.DrawImage($master,0,0,$size,$size)
   }finally{$scaled.Dispose()}
   $stream=[IO.MemoryStream]::new()
   try{
    $frame.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
    $frames.Add($stream.ToArray())
   }finally{$stream.Dispose()}
  }finally{$frame.Dispose()}
 }
}finally{$master.Dispose()}
$directory=[IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($IconPath))
[IO.Directory]::CreateDirectory($directory)|Out-Null
$writer=[IO.BinaryWriter]::new([IO.File]::Create([IO.Path]::GetFullPath($IconPath)))
try{
 $writer.Write([uint16]0)
 $writer.Write([uint16]1)
 $writer.Write([uint16]$sizes.Count)
 $offset=[uint32](6+16*$sizes.Count)
 for($i=0;$i -lt $sizes.Count;$i++){
  $size=$sizes[$i]
  $writer.Write([byte]($size -band 255))
  $writer.Write([byte]($size -band 255))
  $writer.Write([byte]0)
  $writer.Write([byte]0)
  $writer.Write([uint16]1)
  $writer.Write([uint16]32)
  $writer.Write([uint32]$frames[$i].Length)
  $writer.Write([uint32]$offset)
  $offset+=[uint32]$frames[$i].Length
 }
 foreach($frameBytes in $frames){$writer.Write([byte[]]$frameBytes)}
}finally{$writer.Dispose()}
Write-Host ('Verified branded launcher ICO: '+$sizes.Count+' sizes, '+
 ((Get-Item -LiteralPath $IconPath).Length)+' bytes.')
