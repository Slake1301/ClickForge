# Generates the multiresolution Windows icon from the ClickForge mark.
Add-Type -AssemblyName System.Drawing
$sizes = 16, 24, 32, 48, 64, 128, 256
$images = foreach ($size in $sizes) {
    $bitmap = [Drawing.Bitmap]::new($size, $size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $tile = [Drawing.Drawing2D.GraphicsPath]::new()
    $ink = [Drawing.Pen]::new([Drawing.Color]::White, 3.5)
    $accent = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(83, 14, 145))
    $button = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(245, 28, 219))
    $mouse = [Drawing.Drawing2D.GraphicsPath]::new()
    $stream = [IO.MemoryStream]::new()
    try {
        $graphics.SmoothingMode = 'AntiAlias'
        $graphics.ScaleTransform($size / 64.0, $size / 64.0)
        $tile.AddArc(3, 3, 24, 24, 180, 90)
        $tile.AddArc(37, 3, 24, 24, 270, 90)
        $tile.AddArc(37, 37, 24, 24, 0, 90)
        $tile.AddArc(3, 37, 24, 24, 90, 90)
        $tile.CloseFigure()
        $graphics.FillPath($accent, $tile)
        $mouse.AddArc(17, 14, 32, 32, 180, 180)
        $mouse.AddArc(17, 22, 32, 32, 0, 180)
        $mouse.CloseFigure()
        $graphics.SetClip($mouse)
        $graphics.FillRectangle($button, 17, 14, 16, 19)
        $graphics.ResetClip()
        $graphics.DrawPath($ink, $mouse)
        $graphics.DrawLine($ink, 17, 33, 49, 33)
        $graphics.DrawLine($ink, 33, 14, 33, 33)
        $graphics.FillEllipse($accent, 29, 19, 8, 11)
        $graphics.DrawEllipse($ink, 29, 19, 8, 11)
        $sparkle = [Drawing.Point[]]@(
            [Drawing.Point]::new(16, 7), [Drawing.Point]::new(18, 12),
            [Drawing.Point]::new(23, 14), [Drawing.Point]::new(18, 16),
            [Drawing.Point]::new(16, 21), [Drawing.Point]::new(14, 16),
            [Drawing.Point]::new(9, 14), [Drawing.Point]::new(14, 12)
        )
        $graphics.FillPolygon([Drawing.Brushes]::White, $sparkle)
        $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
        ,$stream.ToArray()
    } finally {
        $stream.Dispose()
        $accent.Dispose()
        $button.Dispose()
        $mouse.Dispose()
        $ink.Dispose()
        $tile.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}
$path = Join-Path $PSScriptRoot '..\src\ClickForge.App\ClickForge.ico'
$writer = [IO.BinaryWriter]::new([IO.File]::Create($path))
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = [byte]($sizes[$i] % 256)
        $writer.Write($dimension)
        $writer.Write($dimension)
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$i].Length)
        $writer.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($bytes in $images) { $writer.Write([byte[]]$bytes) }
} finally { $writer.Dispose() }
