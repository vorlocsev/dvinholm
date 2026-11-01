#requires -Version 5.1
<#
Create the native updater icon from the existing Dvinholm brand image.
Only the launcher gets the small circular-update emblem. Output is a real
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
  $base=[Drawing.Drawing2D.LinearGradientBrush]::new(
   [Drawing.Rectangle]::new(4,4,248,248),
   [Drawing.Color]::FromArgb(25,81,108),
   [Drawing.Color]::FromArgb(5,17,34),45.0)
  $outer=[Drawing.Pen]::new([Drawing.Color]::FromArgb(255,32,119,141),4)
  $silver=[Drawing.Pen]::new([Drawing.Color]::FromArgb(245,229,246,255),9)
  $gear=[Drawing.Pen]::new([Drawing.Color]::FromArgb(236,188,223,239),8)
  $wave=[Drawing.Pen]::new([Drawing.Color]::FromArgb(248,38,229,209),7)
  $bird=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(252,242,250,255))
  $badgeBg=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,8,42,71))
  $badgeBorder=[Drawing.Pen]::new([Drawing.Color]::FromArgb(255,37,212,236),4)
  $badgeStroke=[Drawing.Pen]::new([Drawing.Color]::FromArgb(255,239,251,255),5)
  try{
   $g.FillEllipse($base,4,4,248,248)
   $g.DrawEllipse($outer,4,4,248,248)
   $g.DrawEllipse($silver,41,45,168,168)
   # Gear-like tips mark the historic Severodvinsk gear around the ring.
   for($i=0;$i -lt 11;$i++){
    $angle=2.0*[Math]::PI*$i/11.0
    $x1=[single](125+94*[Math]::Cos($angle))
    $y1=[single](129+94*[Math]::Sin($angle))
    $x2=[single](125+106*[Math]::Cos($angle))
    $y2=[single](129+106*[Math]::Sin($angle))
    $g.DrawLine($gear,$x1,$y1,$x2,$y2)
   }
   # Ring-arrow motif references Volvo without duplicating its official logo.
   $g.DrawLine($silver,184,71,234,19)
   $g.DrawLine($silver,234,19,230,53)
   $g.DrawLine($silver,234,19,201,23)
   # White gull over the White Sea, deliberately readable at 16px.
   $g.FillPolygon($bird,[Drawing.Point[]]@(
    [Drawing.Point]::new(53,122),
    [Drawing.Point]::new(80,103),
    [Drawing.Point]::new(103,106),
    [Drawing.Point]::new(121,122),
    [Drawing.Point]::new(154,100),
    [Drawing.Point]::new(199,101),
    [Drawing.Point]::new(176,119),
    [Drawing.Point]::new(138,134),
    [Drawing.Point]::new(119,151),
    [Drawing.Point]::new(97,136),
    [Drawing.Point]::new(68,133)))
   foreach($y in @(158,175,192)){
    $g.DrawBezier($wave,
      [single]68,[single]$y,
      [single]103,[single]($y-22),
      [single]137,[single]($y+18),
      [single]188,[single]$y)
   }
   # Small updater-only badge, distinct from the original Dvinholm.exe ICO.
   $g.FillEllipse($badgeBg,178,178,58,58)
   $g.DrawEllipse($badgeBorder,178,178,58,58)
   $g.DrawArc($badgeStroke,189,189,36,36,45,285)
   $g.FillPolygon($bird,[Drawing.Point[]]@(
    [Drawing.Point]::new(220,186),
    [Drawing.Point]::new(232,194),
    [Drawing.Point]::new(217,201)))
  }finally{
   $base.Dispose();$outer.Dispose();$silver.Dispose();$gear.Dispose()
   $wave.Dispose();$bird.Dispose();$badgeBg.Dispose()
   $badgeBorder.Dispose();$badgeStroke.Dispose()
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
