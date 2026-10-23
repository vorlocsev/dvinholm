#requires -Version 5.1
# Build-only branded PNGs: reuse the existing icon, do not commit binary files.
$ErrorActionPreference='Stop'
$assets=Join-Path $PSScriptRoot '..\src\Dvinholm.App\Assets'
$ico=Join-Path $assets 'Dvinholm.ico'
Add-Type -AssemblyName System.Drawing
$icon=New-Object System.Drawing.Icon($ico)
try {
  $source=$icon.ToBitmap()
  try {
    $list=@(
      @('MsixSquare44x44Logo.png',44,44),
      @('MsixSquare150x150Logo.png',150,150),
      @('MsixWide310x150Logo.png',310,150),
      @('MsixSplash620x300.png',620,300),
      @('MsixStoreLogo.png',50,50)
    )
    foreach($entry in $list) {
      $w=[int]$entry[1]; $h=[int]$entry[2]
      $bitmap=New-Object System.Drawing.Bitmap($w,$h)
      try {
        $g=[Drawing.Graphics]::FromImage($bitmap)
        try {
          $g.Clear([Drawing.Color]::Transparent)
          $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
          $side=[Math]::Min($w,$h)
          $g.DrawImage($source,[int](($w-$side)/2),[int](($h-$side)/2),$side,$side)
          $bitmap.Save((Join-Path $assets $entry[0]),[Drawing.Imaging.ImageFormat]::Png)
        } finally { $g.Dispose() }
      } finally { $bitmap.Dispose() }
    }
  } finally { $source.Dispose() }
} finally { $icon.Dispose() }
Write-Host 'Generated five small branded MSIX images from Dvinholm.ico.'
