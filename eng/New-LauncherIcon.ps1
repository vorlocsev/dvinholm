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

$source=[Drawing.Image]::FromFile((Resolve-Path -LiteralPath $LogoPath).Path)
$sizes=@(16,24,32,48,64,128,256)
$frames=New-Object 'System.Collections.Generic.List[byte[]]'
try{
 # Draw once at high resolution; downsampling retains the neon strokes.
 $master=New-Object Drawing.Bitmap(256,256,
  [Drawing.Imaging.PixelFormat]::Format32bppArgb)
 try{
  $g=[Drawing.Graphics]::FromImage($master)
  try{
   $g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
   $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
   $g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
   $g.DrawImage($source,0,0,256,256)
   # Small badge on top of the established Dvinholm road-and-arrow mark.
   $badge=New-Object Drawing.Rectangle(163,162,80,80)
   $bg=New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(243,4,30,57))
   $border=New-Object Drawing.Pen([Drawing.Color]::FromArgb(255,23,206,246),6)
   $stroke=New-Object Drawing.Pen([Drawing.Color]::White,6)
   $fill=New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(255,72,233,251))
   try{
    $g.FillEllipse($bg,$badge)
    $g.DrawEllipse($border,$badge)
    $g.DrawArc($stroke,181,179,45,45,210,145)
    $g.DrawArc($stroke,181,179,45,45,30,145)
    $g.FillPolygon($fill,[Drawing.Point[]]@(
     [Drawing.Point]::new(177,199),[Drawing.Point]::new(190,198),
     [Drawing.Point]::new(183,211)))
    $g.FillPolygon($fill,[Drawing.Point[]]@(
     [Drawing.Point]::new(230,205),[Drawing.Point]::new(216,206),
     [Drawing.Point]::new(225,193)))
   }finally{$bg.Dispose();$border.Dispose();$stroke.Dispose();$fill.Dispose()}
  }finally{$g.Dispose()}
  foreach($size in $sizes){
   $frame=New-Object Drawing.Bitmap($size,$size,
    [Drawing.Imaging.PixelFormat]::Format32bppArgb)
   try{
    $scaled=[Drawing.Graphics]::FromImage($frame)
    try{
     $scaled.Clear([Drawing.Color]::Transparent)
     $scaled.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
     $scaled.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
     $scaled.DrawImage($master,0,0,$size,$size)
    }finally{$scaled.Dispose()}
    $ms=New-Object IO.MemoryStream
    try{
     $frame.Save($ms,[Drawing.Imaging.ImageFormat]::Png)
     $frames.Add($ms.ToArray())
    }finally{$ms.Dispose()}
   }finally{$frame.Dispose()}
  }
 }finally{$master.Dispose()}
}finally{$source.Dispose()}
$directory=[IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($IconPath))
[IO.Directory]::CreateDirectory($directory)|Out-Null
$writer=New-Object IO.BinaryWriter([IO.File]::Create([IO.Path]::GetFullPath($IconPath)))
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
