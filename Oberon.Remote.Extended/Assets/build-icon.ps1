# Generates a Windows multi-resolution ICO from the same geometric Extended brand mark
# used by Assets/oberon-extended.svg. No external packages or services required.
param([string]$Output = (Join-Path $PSScriptRoot 'oberon-extended.ico'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function RoundRect([System.Drawing.RectangleF]$r, [float]$radius) {
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $d = $radius * 2
  $path.AddArc($r.X,$r.Y,$d,$d,180,90)
  $path.AddArc($r.Right-$d,$r.Y,$d,$d,270,90)
  $path.AddArc($r.Right-$d,$r.Bottom-$d,$d,$d,0,90)
  $path.AddArc($r.X,$r.Bottom-$d,$d,$d,90,90)
  $path.CloseFigure()
  return $path
}
$canvas = New-Object System.Drawing.Bitmap(256, 256)
$g = [System.Drawing.Graphics]::FromImage($canvas)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
$g.Clear([System.Drawing.Color]::Transparent)
$g.ScaleTransform(2,2)
$bg = RoundRect ([System.Drawing.RectangleF]::new(0,0,128,128)) 28
$frame = RoundRect ([System.Drawing.RectangleF]::new(8,8,112,112)) 22
$bgBrush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#0b1210'))
$frameBrush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#13241c'))
$framePen = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#284e3b'),2)
$g.FillPath($bgBrush, $bg)
$g.FillPath($frameBrush, $frame)
$g.DrawPath($framePen, $frame)
$green = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.PointF]::new(32,31),[System.Drawing.PointF]::new(96,97),[System.Drawing.ColorTranslator]::FromHtml('#b6ffbe'),[System.Drawing.ColorTranslator]::FromHtml('#40aa64'))
$stroke = [System.Drawing.Pen]::new($green,11)
$stroke.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$stroke.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
foreach ($y in @(31,64,97)) { $length = if($y -eq 64) { 50 } else { 64 }; $g.DrawLine($stroke,32,$y,(32+$length),$y) }
$spine = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#eaf6ed'),10)
$spine.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$spine.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawLine($spine,32,31,32,97)
$dot = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#b6ffbe'))
$g.FillEllipse($dot,91,59,10,10)
$g.Dispose()
$dot.Dispose(); $spine.Dispose(); $stroke.Dispose(); $green.Dispose()
$framePen.Dispose(); $frameBrush.Dispose(); $bgBrush.Dispose(); $bg.Dispose(); $frame.Dispose()

$sizes = @(16,24,32,48,64,128,256)
$encoded = New-Object 'System.Collections.Generic.List[byte[]]'
foreach($side in $sizes) {
  $raster = New-Object System.Drawing.Bitmap($side,$side)
  $draw = [System.Drawing.Graphics]::FromImage($raster)
  $draw.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
  $draw.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $draw.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $draw.DrawImage($canvas,0,0,$side,$side)
  $draw.Dispose()
  $memory = New-Object System.IO.MemoryStream
  $raster.Save($memory,[System.Drawing.Imaging.ImageFormat]::Png)
  $encoded.Add($memory.ToArray())
  $memory.Dispose(); $raster.Dispose()
}
$canvas.Dispose()
$parent = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($Output))
[System.IO.Directory]::CreateDirectory($parent) | Out-Null
$fs = [System.IO.File]::Create($Output)
try {
  $writer = New-Object System.IO.BinaryWriter($fs)
  $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
  $offset = 6 + (16 * $sizes.Count)
  for($i = 0; $i -lt $sizes.Count; $i++) {
    $size = [int]$sizes[$i]
    $writer.Write([byte]$(if($size -eq 256) { 0 } else { $size }))
    $writer.Write([byte]$(if($size -eq 256) { 0 } else { $size }))
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$encoded[$i].Length); $writer.Write([uint32]$offset)
    $offset += $encoded[$i].Length
  }
  foreach($png in $encoded) { $writer.Write([byte[]]$png) }
  $writer.Flush()
} finally { $fs.Dispose() }
Write-Host "Generated Extended icon: $Output"
