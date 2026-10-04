# Draws the plugin icon: icon.png (256) and src/.../icon128.png (128, drawn at that size).
# A white bunny-ears hairband on a purple-to-magenta rounded square.
# Everything is in units of 1/256 of the icon, so tweak the values below and re-run.
Add-Type -AssemblyName System.Drawing

$ColorTopLeft     = [System.Drawing.Color]::FromArgb(255, 58, 20, 110)
$ColorBottomRight = [System.Drawing.Color]::FromArgb(255, 205, 40, 150)
$BandColor  = [System.Drawing.Color]::White
$EarColor   = [System.Drawing.Color]::White
$InnerEarColor = [System.Drawing.Color]::FromArgb(255, 248, 170, 215)   # light pink
$CornerRadius = 52

# hairband: arc of a circle, opening downward
$BandCx = 128; $BandCy = 190; $BandR = 70; $BandThick = 20
$BandSpan = 200            # degrees of arc drawn (centred on the top)
# ears: stand on the arc at +-EarAngle degrees from the top, tilted outward by EarTilt degrees
$EarAngle = 36; $EarTilt = 14
$EarLen = 112; $EarW = 46  # ear length and width (ellipse, rounded)
$InnerLen = 74; $InnerW = 20; $InnerShift = 8   # inner ear, shifted along the ear towards the base

function New-RoundedRect($x, $y, $w, $h, $r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Draw-Icon($size, $file) {
    $k = $size / 256.0
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)

    $bg = New-RoundedRect 0 0 $size $size ($CornerRadius * $k)
    $rect = New-Object System.Drawing.RectangleF(0, 0, $size, $size)
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $ColorTopLeft, $ColorBottomRight, 45.0)
    $g.FillPath($grad, $bg)
    $g.SetClip($bg)

    $earBrush = New-Object System.Drawing.SolidBrush($EarColor)
    $innerBrush = New-Object System.Drawing.SolidBrush($InnerEarColor)

    # ears: drawn in a local frame (origin at the ear base on the arc, y up the ear), then rotated
    foreach ($s in @(-1, 1)) {
        $a = $EarAngle * [Math]::PI / 180
        $bx = $BandCx + $s * $BandR * [Math]::Sin($a)
        $by = $BandCy - $BandR * [Math]::Cos($a)
        $m = $g.Transform
        $g.TranslateTransform([single]($bx * $k), [single]($by * $k))
        # ear axis follows the radial direction of the arc, plus extra outward tilt
        $g.RotateTransform([single]($s * ($EarAngle * 0.35 + $EarTilt)))
        $g.ScaleTransform([single]$k, [single]$k)
        $g.FillEllipse($earBrush, -$EarW / 2, -$EarLen + 6, $EarW, $EarLen)
        $g.FillEllipse($innerBrush, -$InnerW / 2, -$InnerLen - $InnerShift, $InnerW, $InnerLen)
        $g.Transform = $m
    }

    # hairband
    $pen = New-Object System.Drawing.Pen($BandColor, [single]($BandThick * $k))
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
    $start = -90 - $BandSpan / 2
    $g.DrawArc($pen, [single](($BandCx - $BandR) * $k), [single](($BandCy - $BandR) * $k), [single](2 * $BandR * $k), [single](2 * $BandR * $k), [single]$start, [single]$BandSpan)

    $g.ResetClip()
    $g.Dispose()
    $bmp.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

$root = Split-Path -Parent $PSScriptRoot
Draw-Icon 256 (Join-Path $root 'icon.png')
Draw-Icon 128 (Join-Path $root 'src\LoupixDeck.Plugin.VTubeStudio\icon128.png')
