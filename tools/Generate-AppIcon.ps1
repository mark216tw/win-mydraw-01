param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

Add-Type -AssemblyName System.Drawing

function New-RoundedPath {
    param(
        [single]$X,
        [single]$Y,
        [single]$Width,
        [single]$Height,
        [single]$Radius
    )

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = $Radius * 2
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

$sizes = @(16, 20, 24, 32, 48, 64, 128, 256)
$images = [System.Collections.Generic.List[byte[]]]::new()

foreach ($size in $sizes) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $scale = $size / 256.0

    $backgroundPath = New-RoundedPath (8 * $scale) (8 * $scale) (240 * $scale) (240 * $scale) (52 * $scale)
    $backgroundBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new(8 * $scale, 8 * $scale),
        [System.Drawing.PointF]::new(248 * $scale, 248 * $scale),
        [System.Drawing.Color]::FromArgb(255, 23, 59, 83),
        [System.Drawing.Color]::FromArgb(255, 11, 29, 42))
    $graphics.FillPath($backgroundBrush, $backgroundPath)

    $borderPen = [System.Drawing.Pen]::new(
        [System.Drawing.Color]::FromArgb(70, 255, 255, 255),
        [Math]::Max(1.0, 2 * $scale))
    $graphics.DrawPath($borderPen, $backgroundPath)

    $state = $graphics.Save()
    $graphics.TranslateTransform(128 * $scale, 132 * $scale)
    $graphics.RotateTransform(-42)
    $graphics.TranslateTransform(-128 * $scale, -132 * $scale)

    $bodyPath = New-RoundedPath (112 * $scale) (44 * $scale) (32 * $scale) (126 * $scale) (14 * $scale)
    $bodyBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new(112 * $scale, 44 * $scale),
        [System.Drawing.PointF]::new(144 * $scale, 170 * $scale),
        [System.Drawing.Color]::FromArgb(255, 247, 222, 131),
        [System.Drawing.Color]::FromArgb(255, 217, 154, 38))
    $graphics.FillPath($bodyBrush, $bodyPath)

    $band = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 228, 154, 58))
    $graphics.FillRectangle($band, 112 * $scale, 66 * $scale, 32 * $scale, 14 * $scale)

    $collar = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 86, 184, 207))
    $graphics.FillRectangle($collar, 112 * $scale, 156 * $scale, 32 * $scale, 12 * $scale)

    $nib = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 229, 194, 157))
    $graphics.FillPolygon($nib, [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(112 * $scale, 170 * $scale),
        [System.Drawing.PointF]::new(144 * $scale, 170 * $scale),
        [System.Drawing.PointF]::new(128 * $scale, 210 * $scale)))

    $ink = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 31, 41, 55))
    $graphics.FillPolygon($ink, [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(123 * $scale, 200 * $scale),
        [System.Drawing.PointF]::new(133 * $scale, 200 * $scale),
        [System.Drawing.PointF]::new(128 * $scale, 216 * $scale)))

    $highlightPath = New-RoundedPath (117 * $scale) (56 * $scale) (5 * $scale) (92 * $scale) (2.5 * $scale)
    $highlight = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(110, 255, 255, 255))
    $graphics.FillPath($highlight, $highlightPath)

    $graphics.Restore($state)

    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $images.Add($stream.ToArray())
    if ($size -eq 256) {
        $bitmap.Save((Join-Path $OutputDirectory "MyDraw-256.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    }

    $stream.Dispose()
    $highlight.Dispose()
    $highlightPath.Dispose()
    $ink.Dispose()
    $nib.Dispose()
    $collar.Dispose()
    $band.Dispose()
    $bodyBrush.Dispose()
    $bodyPath.Dispose()
    $borderPen.Dispose()
    $backgroundBrush.Dispose()
    $backgroundPath.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

$iconPath = Join-Path $OutputDirectory "MyDraw.ico"
$file = [System.IO.File]::Create($iconPath)
$writer = [System.IO.BinaryWriter]::new($file)
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)
$offset = 6 + (16 * $sizes.Count)

for ($index = 0; $index -lt $sizes.Count; $index++) {
    $size = $sizes[$index]
    $image = $images[$index]
    $writer.Write([byte]($(if ($size -eq 256) { 0 } else { $size })))
    $writer.Write([byte]($(if ($size -eq 256) { 0 } else { $size })))
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$image.Length)
    $writer.Write([uint32]$offset)
    $offset += $image.Length
}

foreach ($image in $images) {
    $writer.Write($image)
}

$writer.Dispose()
$file.Dispose()
