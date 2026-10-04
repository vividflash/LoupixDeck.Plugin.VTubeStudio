# Draws the expression button icons: src/.../Icons/<name>.png (144x144, transparent, 2x of the 72 px draw size)
# and the contact sheet tools/emote-icons-sheet.png (109 icons at 96 px on a dark background).
# One function per icon (Draw-<name>), all coordinates are in the 144x144 box. Colours are the variables below.
Add-Type -AssemblyName System.Drawing

function Col($r, $g, $b, $a = 255) { [System.Drawing.Color]::FromArgb($a, $r, $g, $b) }

$White   = Col 255 255 255
$Gold    = Col 255 200 40
$Pink    = Col 255 120 165
$HeartRed = Col 240 70 110
$HeartLight = Col 255 160 190
$Red     = Col 235 45 45
$Blue    = Col 80 165 255
$LightBlue = Col 150 195 255
$Yellow  = Col 255 220 50
$Dark    = Col 40 40 70
$Grey    = Col 175 180 200
$Gloom   = Col 125 135 215
$Orange  = Col 255 150 50
$SlateText = Col 70 80 100
$Green   = Col 70 215 120
$Amber   = Col 255 176 40
$Black   = Col 30 30 38
$Edge    = Col 150 156 178

$Size = 144

# ---------- helpers ----------
function Br($c) { New-Object System.Drawing.SolidBrush($c) }
function Pn($c, $w) {
    $p = New-Object System.Drawing.Pen($c, [single]$w)
    $p.StartCap = 'Round'; $p.EndCap = 'Round'; $p.LineJoin = 'Round'
    return $p
}
function P($x, $y) { New-Object System.Drawing.PointF([single]$x, [single]$y) }
function New-Path { $p = New-Object System.Drawing.Drawing2D.GraphicsPath; $p.FillMode = 'Winding'; return $p }

function New-RoundedRect($x, $y, $w, $h, $r) {
    $p = New-Path
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Fill-Circle($g, $cx, $cy, $r, $c) { $b = Br $c; $g.FillEllipse($b, [single]($cx - $r), [single]($cy - $r), [single](2 * $r), [single](2 * $r)); $b.Dispose() }
function Fill-Ellipse($g, $cx, $cy, $w, $h, $c) { $b = Br $c; $g.FillEllipse($b, [single]($cx - $w / 2), [single]($cy - $h / 2), [single]$w, [single]$h); $b.Dispose() }
function Stroke-Circle($g, $cx, $cy, $r, $w, $c) { $p = Pn $c $w; $g.DrawEllipse($p, [single]($cx - $r), [single]($cy - $r), [single](2 * $r), [single](2 * $r)); $p.Dispose() }
function Stroke-Arc($g, $cx, $cy, $rx, $ry, $start, $sweep, $w, $c) {
    $p = Pn $c $w
    $g.DrawArc($p, [single]($cx - $rx), [single]($cy - $ry), [single](2 * $rx), [single](2 * $ry), [single]$start, [single]$sweep)
    $p.Dispose()
}
function Stroke-Line($g, $x1, $y1, $x2, $y2, $w, $c) { $p = Pn $c $w; $g.DrawLine($p, [single]$x1, [single]$y1, [single]$x2, [single]$y2); $p.Dispose() }
function Fill-Path($g, $path, $c) { $b = Br $c; $g.FillPath($b, $path); $b.Dispose() }
# fill and trace with the same colour so corners come out rounded
function Fill-Soft($g, $path, $c, $w) { Fill-Path $g $path $c; $p = Pn $c $w; $g.DrawPath($p, $path); $p.Dispose() }

# Fill-Soft with a line in $edge around it (for dark fills)
function Fill-Edged($g, $path, $c, $w, $edge, $ew) {
    $p = Pn $edge ($w + 2 * $ew); $g.DrawPath($p, $path); $p.Dispose()
    Fill-Soft $g $path $c $w
}

# run $body with the origin moved to ($cx,$cy), rotated and scaled
function With-Transform($g, $cx, $cy, $scale, $angle, [scriptblock]$body) {
    $state = $g.Save()
    $g.TranslateTransform([single]$cx, [single]$cy)
    $g.RotateTransform([single]$angle)
    $g.ScaleTransform([single]$scale, [single]$scale)
    & $body
    $g.Restore($state)
}

# text outline, scaled to fit the box and centred on ($cx,$cy)
function Fill-Glyph($g, $text, $cx, $cy, $maxW, $maxH, $c) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $ff = New-Object System.Drawing.FontFamily('Arial Black')
    $path.AddString($text, $ff, 0, [single]100, (P 0 0), [System.Drawing.StringFormat]::GenericTypographic)
    $b = $path.GetBounds()
    $s = [Math]::Min($maxW / $b.Width, $maxH / $b.Height)
    $m1 = New-Object System.Drawing.Drawing2D.Matrix
    $m1.Translate([single](-($b.X + $b.Width / 2)), [single](-($b.Y + $b.Height / 2)))
    $path.Transform($m1)
    $m2 = New-Object System.Drawing.Drawing2D.Matrix
    $m2.Scale([single]$s, [single]$s)
    $path.Transform($m2)
    $m3 = New-Object System.Drawing.Drawing2D.Matrix
    $m3.Translate([single]$cx, [single]$cy)
    $path.Transform($m3)
    Fill-Soft $g $path $c 2
    $path.Dispose()
}

# unit shapes, drawn inside With-Transform
function Unit-Heart($c) {
    $p = New-Path
    $p.AddBezier((P 0 0.95), (P -1.45 0.0), (P -0.95 -1.1), (P 0 -0.4))
    $p.AddBezier((P 0 -0.4), (P 0.95 -1.1), (P 1.45 0.0), (P 0 0.95))
    $p.CloseFigure()
    Fill-Soft $script:G $p $c 0.04
    $p.Dispose()
}
function Unit-Drop($c) {
    $p = New-Path
    $p.AddPolygon([System.Drawing.PointF[]]@((P 0 -1), (P 0.59 -0.08), (P -0.59 -0.08)))
    $p.AddEllipse([single]-0.7, [single]-0.4, [single]1.4, [single]1.4)
    Fill-Path $script:G $p $c
    $p.Dispose()
    Fill-Ellipse $script:G -0.3 0.3 0.2 0.38 (Col 255 255 255 190)
}
function Unit-Star($c) {
    $pts = @()
    for ($i = 0; $i -lt 10; $i++) {
        $r = if ($i % 2 -eq 0) { 1.0 } else { 0.47 }
        $a = (-90 + $i * 36) * [Math]::PI / 180
        $pts += (P ($r * [Math]::Cos($a)) ($r * [Math]::Sin($a)))
    }
    $p = New-Path
    $p.AddPolygon([System.Drawing.PointF[]]$pts)
    Fill-Soft $script:G $p $c 0.1
    $p.Dispose()
}

function Draw-Heart1($cx, $cy, $s, $c) { With-Transform $script:G $cx ($cy - 0.1 * $s) $s 0 { Unit-Heart $c } }
function Draw-Drop1($cx, $cy, $s, $angle) { With-Transform $script:G $cx $cy $s $angle { Unit-Drop $Blue } }
function Draw-Bubble($cx, $cy, $r, $w) {
    Fill-Circle $script:G $cx $cy $r (Col 200 230 255 90)
    Stroke-Circle $script:G $cx $cy $r $w $White
    Stroke-Arc $script:G $cx $cy ($r * 0.66) ($r * 0.66) 195 55 ($w * 0.9) $White
}
function Draw-AngryMark($cx, $cy, $s) {
    With-Transform $script:G $cx $cy $s 0 {
        $off = -72
        foreach ($q in @(@(24, 24, 0), @(120, 24, 90), @(120, 120, 180), @(24, 120, 270))) {
            Stroke-Arc $script:G ($q[0] + $off) ($q[1] + $off) 40 40 $q[2] 90 15 $Red
        }
    }
}
function Draw-Cheek($cx, $cy, $w, $h) {
    Fill-Ellipse $script:G $cx $cy $w $h (Col 255 120 160)
    foreach ($dx in @(-0.2, 0.0, 0.2)) {
        $x = $cx + $dx * $w
        Stroke-Line $script:G ($x - $h * 0.14) ($cy + $h * 0.24) ($x + $h * 0.14) ($cy - $h * 0.24) ($h * 0.11) $White
    }
}
function Draw-Eye($cx, $cy, $w, $h) {
    Fill-Ellipse $script:G $cx $cy $w $h $White
}

# ---------- icons ----------
function Draw-cash { Fill-Glyph $script:G '$' 72 72 80 118 $Gold }

function Draw-heart {
    Draw-Heart1 62 82 50 $HeartRed
    With-Transform $script:G 42 62 1 -30 { Fill-Ellipse $script:G 0 0 8 18 (Col 255 255 255 170) }
    Draw-Heart1 110 36 22 $HeartLight
}

function Draw-angry { Draw-AngryMark 72 72 1.0 }

function Draw-angryshy {
    Draw-Cheek 44 100 70 44
    Draw-AngryMark 92 48 0.62
}

function Draw-shy {
    Draw-Cheek 38 72 64 56
    Draw-Cheek 106 72 64 56
}

function Draw-star {
    With-Transform $script:G 38 72 32 0 { Unit-Star $Yellow }
    With-Transform $script:G 106 72 32 0 { Unit-Star $Yellow }
}

function Draw-tears {
    foreach ($x in @(38, 106)) {
        # eye squeezed shut, then a large and a small tear below it
        Stroke-Arc $script:G $x 26 23 14 0 180 12 $White
        Draw-Drop1 $x 80 26 0
        Draw-Drop1 $x 124 13 0
    }
}

function Draw-pleading {
    foreach ($x in @(38, 106)) {
        Fill-Ellipse $script:G $x 74 60 84 $Dark
        $p = Pn $White 6
        $script:G.DrawEllipse($p, [single]($x - 30), [single]32, [single]60, [single]84)
        $p.Dispose()
        Fill-Circle $script:G ($x - 9) 54 11 $White
        Fill-Circle $script:G ($x + 10) 84 5 $White
        Stroke-Arc $script:G $x 74 21 31 40 100 6 $LightBlue
    }
}

function Draw-sulking {
    $p = New-Path
    $p.AddEllipse(22, 36, 54, 42)
    $p.AddEllipse(50, 14, 52, 52)
    $p.AddEllipse(84, 30, 44, 46)
    $p.AddPath((New-RoundedRect 28 46 92 32 16), $false)
    Fill-Path $script:G $p $Grey
    $p.Dispose()
    $ends = @(120, 134, 116, 132, 122)
    for ($i = 0; $i -lt 5; $i++) {
        Stroke-Line $script:G (32 + $i * 20) 92 (32 + $i * 20) $ends[$i] 8 $Gloom
    }
}

function Draw-swirly {
    $pts = @()
    $turns = 2.25
    for ($t = 0.0; $t -le $turns * 2 * [Math]::PI; $t += 0.12) {
        $r = 6 + (58 - 6) * $t / ($turns * 2 * [Math]::PI)
        $pts += (P (72 + $r * [Math]::Cos($t)) (72 + $r * [Math]::Sin($t)))
    }
    $p = Pn $White 12
    $script:G.DrawLines($p, [System.Drawing.PointF[]]$pts)
    $p.Dispose()
}

function Draw-xd {
    # scrunched > < eyes and a wailing open mouth (curve pointing down)
    Stroke-Line $script:G 16 20 62 46 15 $White
    Stroke-Line $script:G 62 46 16 72 15 $White
    Stroke-Line $script:G 128 20 82 46 15 $White
    Stroke-Line $script:G 82 46 128 72 15 $White
    $m = New-Path
    $m.AddBezier((P 32 132), (P 38 62), (P 106 62), (P 112 132))
    $m.CloseFigure()
    Fill-Soft $script:G $m $White 4
    $m.Dispose()
    $in = New-Path
    $in.AddBezier((P 48 124), (P 54 80), (P 90 80), (P 96 124))
    $in.CloseFigure()
    Fill-Soft $script:G $in $Dark 3
    $in.Dispose()
    Fill-Ellipse $script:G 72 114 32 16 $Pink
}

function Draw-avoid {
    foreach ($x in @(38, 106)) {
        Draw-Eye $x 74 62 76
        Fill-Circle $script:G ($x - 14) 74 18 $Dark
        Fill-Circle $script:G ($x - 19) 68 5 $White
    }
}

function Draw-mouth3 {
    Stroke-Arc $script:G 44 62 26 30 0 180 15 $White
    Stroke-Arc $script:G 100 62 26 30 0 180 15 $White
}

function Draw-pout {
    # puffed cheeks, a small sideways "3" mouth between them, angry eyebrows above
    Fill-Circle $script:G 30 88 26 $Pink
    Fill-Circle $script:G 114 88 26 $Pink
    Stroke-Line $script:G 22 34 54 46 10 $White
    Stroke-Line $script:G 122 34 90 46 10 $White
    Stroke-Arc $script:G 66 76 13 12 -90 180 10 $White
    Stroke-Arc $script:G 66 100 13 12 -90 180 10 $White
}

function Draw-sweat { Draw-Drop1 72 74 58 0 }

function Draw-sweatdrops {
    Draw-Drop1 48 92 36 -10
    Draw-Drop1 98 70 30 12
    Draw-Drop1 66 34 22 -5
}

function Draw-nosebubble {
    Draw-Bubble 76 66 48 8
    Draw-Bubble 20 120 13 5
}

function Draw-sleepbubble {
    Draw-Bubble 84 58 46 8
    Fill-Glyph $script:G 'z' 84 60 34 36 $LightBlue
    Draw-Bubble 30 108 15 5
    Draw-Bubble 14 132 8 4
}

function Draw-sleep {
    Fill-Glyph $script:G 'Z' 28 118 30 28 $LightBlue
    Fill-Glyph $script:G 'Z' 62 82 42 40 $LightBlue
    Fill-Glyph $script:G 'Z' 104 42 56 54 $White
}

function Draw-loading {
    for ($i = 0; $i -lt 8; $i++) {
        $a = (-90 - $i * 45) * [Math]::PI / 180
        Fill-Circle $script:G (72 + 48 * [Math]::Cos($a)) (72 + 48 * [Math]::Sin($a)) (16 - $i * 1.2) (Col 255 255 255 (255 - $i * 28))
    }
}

function Draw-speech {
    $p = New-RoundedRect 12 20 120 82 26
    $p.AddPolygon([System.Drawing.PointF[]]@((P 30 90), (P 30 130), (P 70 96)))
    Fill-Soft $script:G $p $White 4
    $p.Dispose()
    foreach ($x in @(44, 72, 100)) { Fill-Circle $script:G $x 60 9 $SlateText }
}

function Draw-eating {
    # fork
    foreach ($x in @(28, 44, 60)) { Stroke-Line $script:G $x 16 $x 56 10 $White }
    $p = New-RoundedRect 23 46 42 28 13
    Fill-Path $script:G $p $White
    $p.Dispose()
    Stroke-Line $script:G 44 70 44 126 13 $White
    # knife
    $k = New-Path
    $k.AddBezier((P 94 14), (P 126 22), (P 126 66), (P 114 84))
    $k.AddLine((P 114 84), (P 94 84))
    $k.CloseFigure()
    Fill-Soft $script:G $k $White 4
    $k.Dispose()
    Stroke-Line $script:G 104 82 104 126 13 $White
}

function Draw-fish {
    $tail = New-Path
    $tail.AddPolygon([System.Drawing.PointF[]]@((P 96 74), (P 134 42), (P 134 106)))
    Fill-Soft $script:G $tail $Orange 6
    $tail.Dispose()
    $fin = New-Path
    $fin.AddPolygon([System.Drawing.PointF[]]@((P 40 52), (P 62 26), (P 84 52)))
    Fill-Soft $script:G $fin $Orange 6
    $fin.Dispose()
    Fill-Ellipse $script:G 56 74 100 62 $White
    Fill-Circle $script:G 30 68 8 $Dark
    Stroke-Arc $script:G 46 74 16 22 -60 120 6 $Orange
}

function Draw-white3 {
    Fill-Circle $script:G 72 72 60 $White
    Fill-Circle $script:G 48 62 8 $Dark
    Fill-Circle $script:G 96 62 8 $Dark
    Stroke-Arc $script:G 62 82 10 11 0 180 6 $Dark
    Stroke-Arc $script:G 82 82 10 11 0 180 6 $Dark
}

function Draw-controller {
    $Detail = Col 32 32 40
    # grips, tilted outwards, then the wide body on top
    With-Transform $script:G 36 100 1 14 {
        $p = New-RoundedRect (-19) (-30) 38 66 18
        Fill-Path $script:G $p $White
        $p.Dispose()
    }
    With-Transform $script:G 108 100 1 -14 {
        $p = New-RoundedRect (-19) (-30) 38 66 18
        Fill-Path $script:G $p $White
        $p.Dispose()
    }
    $body = New-RoundedRect 10 26 124 70 30
    Fill-Path $script:G $body $White
    $body.Dispose()
    # d-pad
    $v = New-RoundedRect 31 41 11 34 3
    Fill-Path $script:G $v $Detail
    $v.Dispose()
    $h = New-RoundedRect 20 52 33 12 3
    Fill-Path $script:G $h $Detail
    $h.Dispose()
    # four buttons
    foreach ($d in @(@(0, -12), @(0, 12), @(-12, 0), @(12, 0))) {
        Fill-Circle $script:G (108 + $d[0]) (58 + $d[1]) 5.5 $Detail
    }
    # analog sticks
    foreach ($x in @(58, 86)) {
        Stroke-Circle $script:G $x 82 9 4 $Detail
        Fill-Circle $script:G $x 82 3.5 $Detail
    }
}

function Draw-microphone {
    $cap = New-RoundedRect 52 8 40 68 20
    Fill-Path $script:G $cap $White
    $cap.Dispose()
    Stroke-Arc $script:G 72 62 33 30 0 180 9 $White
    Stroke-Line $script:G 72 92 72 118 9 $White
    Stroke-Line $script:G 48 122 96 122 9 $White
}

function Draw-Plug($left, $right, $body, $prong) {
    # left plug (cable, body, two prongs) and right socket (body, cable); $left/$right are the x positions of the bodies
    $y = 76
    Stroke-Line $script:G ($left - 8) $y $left $y 10 $body
    $pb = New-RoundedRect $left ($y - 24) 34 48 10
    Fill-Path $script:G $pb $body
    $pb.Dispose()
    Stroke-Line $script:G ($left + 34) ($y - 11) ($left + 56) ($y - 11) 9 $prong
    Stroke-Line $script:G ($left + 34) ($y + 11) ($left + 56) ($y + 11) 9 $prong
    $sb = New-RoundedRect $right ($y - 24) 34 48 10
    Fill-Path $script:G $sb $body
    $sb.Dispose()
    Stroke-Line $script:G ($right + 34) $y ($right + 42) $y 10 $body
}

function Draw-connected {
    Draw-Plug 31 75 $White $Green
}

function Draw-disconnected {
    Draw-Plug 14 88 $Grey $Grey
    Stroke-Line $script:G 60 14 84 38 10 $Red
    Stroke-Line $script:G 84 14 60 38 10 $Red
}

function Draw-locked {
    Stroke-Arc $script:G 72 62 24 30 180 180 12 $White
    Stroke-Line $script:G 48 62 48 76 12 $White
    Stroke-Line $script:G 96 62 96 76 12 $White
    $b = New-RoundedRect 28 68 88 62 14
    Fill-Path $script:G $b $Amber
    $b.Dispose()
    Fill-Circle $script:G 72 92 9 (Col 32 32 40)
    $k = New-RoundedRect 68 94 8 22 3
    Fill-Path $script:G $k (Col 32 32 40)
    $k.Dispose()
}

function Draw-pen {
    # drawn along the x axis (tip at the left), then turned so the tip points to the lower left
    With-Transform $script:G 67 77 1.45 -45 {
        $tip = New-Path
        $tip.AddPolygon([System.Drawing.PointF[]]@((P (-14) (-11)), (P (-14) 11), (P (-44) 0)))
        Fill-Soft $script:G $tip $Orange 3
        $tip.Dispose()
        $body = New-RoundedRect (-16) (-11) 72 22 10
        Fill-Path $script:G $body $White
        $body.Dispose()
        Stroke-Line $script:G 6 (-11) 30 (-11) 5 $Orange
    }
}

# crop top and pleated skirt; $edge outlines the pieces (for dark fills)
function Outfit-Set($fill, $line, $edge = $null) {
    # crop top: two straps, scooped neck, cut off above the waist
    $t = New-Path
    $t.AddLine((P 46 8), (P 56 8))
    $t.AddBezier((P 56 8), (P 58 34), (P 86 34), (P 88 8))
    $t.AddLine((P 88 8), (P 98 8))
    $t.AddBezier((P 98 8), (P 98 24), (P 104 32), (P 112 36))
    $t.AddLine((P 112 36), (P 104 62))
    $t.AddLine((P 104 62), (P 40 62))
    $t.AddLine((P 40 62), (P 32 36))
    $t.AddBezier((P 32 36), (P 40 32), (P 46 24), (P 46 8))
    $t.CloseFigure()
    if ($edge) { Fill-Edged $script:G $t $fill 4 $edge 2.5 } else { Fill-Soft $script:G $t $fill 4 }
    $t.Dispose()
    # flared skirt with pleats, a small gap below the top
    $s = New-Path
    $s.AddLine((P 42 76), (P 102 76))
    $s.AddLine((P 102 76), (P 130 130))
    $s.AddBezier((P 130 130), (P 100 138), (P 44 138), (P 14 130))
    $s.CloseFigure()
    if ($edge) { Fill-Edged $script:G $s $fill 4 $edge 2.5 } else { Fill-Soft $script:G $s $fill 4 }
    $s.Dispose()
    Stroke-Line $script:G 44 86 100 86 3 $line
    Stroke-Line $script:G 58 94 46 130 3 $line
    Stroke-Line $script:G 72 94 72 132 3 $line
    Stroke-Line $script:G 86 94 98 130 3 $line
}

function Draw-outfit { Outfit-Set $White (Col 200 204 212) }
function Draw-outfitred { Outfit-Set (Col 225 50 70) (Col 150 26 44) }
function Draw-outfitblack { Outfit-Set $Black $Edge $Edge }

function Draw-heels {
    $Shoe = Col 232 56 79
    $Sole = Col 160 30 50
    # stiletto heel behind the shoe
    $h = New-Path
    $h.AddPolygon([System.Drawing.PointF[]]@((P 102 92), (P 124 88), (P 120 136), (P 114 136)))
    Fill-Soft $script:G $h $Shoe 3
    $h.Dispose()
    # upper: toe, vamp, throat, collar, back
    $u = New-Path
    $u.AddBezier((P 8 120), (P 4 100), (P 30 82), (P 58 82))
    $u.AddBezier((P 58 82), (P 76 104), (P 100 96), (P 118 40))
    $u.AddBezier((P 118 40), (P 132 62), (P 128 84), (P 116 98))
    $u.AddBezier((P 112 98), (P 90 112), (P 56 128), (P 28 128))
    $u.AddBezier((P 28 128), (P 16 128), (P 10 126), (P 8 120))
    $u.CloseFigure()
    Fill-Soft $script:G $u $Shoe 4
    $u.Dispose()
    # sole line
    $p = Pn $Sole 6
    $g = $script:G
    $g.DrawBezier($p, (P 8 120), (P 12 126), (P 20 129), (P 30 129))
    $g.DrawBezier($p, (P 30 129), (P 58 129), (P 92 113), (P 118 98))
    $p.Dispose()
}

function Draw-school {
    $Navy = Col 39 53 107
    $Ribbon = Col 232 56 79
    # white top with short cap sleeves
    $t = New-Path
    $t.AddLine((P 54 8), (P 28 20))
    $t.AddLine((P 28 20), (P 14 38))
    $t.AddLine((P 14 38), (P 31 50))
    $t.AddLine((P 31 50), (P 36 45))
    $t.AddLine((P 36 45), (P 36 136))
    $t.AddLine((P 36 136), (P 108 136))
    $t.AddLine((P 108 136), (P 108 45))
    $t.AddLine((P 108 45), (P 113 50))
    $t.AddLine((P 113 50), (P 130 38))
    $t.AddLine((P 130 38), (P 116 20))
    $t.AddLine((P 116 20), (P 90 8))
    $t.CloseFigure()
    Fill-Soft $script:G $t $White 4
    $t.Dispose()
    # sailor collar: V at the neck, white stripe along its edge
    $c = New-Path
    $c.AddLine((P 54 8), (P 30 22))
    $c.AddLine((P 30 22), (P 72 88))
    $c.AddLine((P 72 88), (P 114 22))
    $c.AddLine((P 114 22), (P 90 8))
    $c.AddLine((P 90 8), (P 72 54))
    $c.CloseFigure()
    Fill-Soft $script:G $c $Navy 3
    $c.Dispose()
    Stroke-Line $script:G 38 26 72 78 3.5 $White
    Stroke-Line $script:G 72 78 106 26 3.5 $White
    # ribbon: two loops, knot and two tails at the bottom of the V
    foreach ($d in @(-1, 1)) {
        $l = New-Path
        $l.AddPolygon([System.Drawing.PointF[]]@((P 72 60), (P (72 + $d * 24) 48), (P (72 + $d * 24) 74)))
        Fill-Soft $script:G $l $Ribbon 3
        $l.Dispose()
        $tl = New-Path
        $tl.AddPolygon([System.Drawing.PointF[]]@((P (72 + $d * 3) 62), (P (72 + $d * 17) 108), (P (72 + $d * 5) 100)))
        Fill-Soft $script:G $tl $Ribbon 3
        $tl.Dispose()
    }
    Fill-Circle $script:G 72 60 8 $Ribbon
}

function Unit-Ear($c, $baseHalf, $height, $bend) {
    # local box: base centre at (0,0), tip straight up; curved sides
    $p = New-Path
    $p.AddBezier((P (-$baseHalf) 0), (P (-$baseHalf) (-$height * 0.5)), (P (-$bend) (-$height * 0.8)), (P 0 (-$height)))
    $p.AddBezier((P 0 (-$height)), (P $bend (-$height * 0.8)), (P $baseHalf (-$height * 0.5)), (P $baseHalf 0))
    $p.CloseFigure()
    Fill-Soft $script:G $p $c 3
    $p.Dispose()
}

function Draw-foxears {
    $Orange2 = Col 242 140 40
    foreach ($e in @(@(40, -14), @(104, 14))) {
        With-Transform $script:G $e[0] 132 1 $e[1] {
            Unit-Ear $White 28 116 14
            With-Transform $script:G 0 -6 1 0 { Unit-Ear $Orange2 15 66 8 }
        }
    }
}

function Draw-bunnyears {
    $Pink2 = Col 255 150 185
    foreach ($e in @(@(48, -7, 0), @(98, 9, 1))) {
        With-Transform $script:G $e[0] 134 1 $e[1] {
            Fill-Ellipse $script:G 0 -60 36 122 $White
            Fill-Ellipse $script:G 0 -56 16 88 $Pink2
        }
    }
}

function Draw-deviltail {
    $Tail = Col 232 56 79
    $p = Pn $Tail 9
    $script:G.DrawBezier($p, (P 26 134), (P 16 74), (P 104 112), (P 96 62))
    $p.Dispose()
    With-Transform $script:G 96 78 1 -6 {
        $t = New-Path
        $t.AddBezier((P 0 -52), (P 6 -34), (P 22 -26), (P 22 -10))
        $t.AddLine((P 22 -10), (P 0 -16))
        $t.AddLine((P 0 -16), (P -22 -10))
        $t.AddBezier((P -22 -10), (P -22 -26), (P -6 -34), (P 0 -52))
        $t.CloseFigure()
        Fill-Soft $script:G $t $Tail 3
        $t.Dispose()
    }
}

function Draw-horns {
    $Horn = Col 232 56 79
    $Base = Col 160 30 50
    foreach ($mirror in @($false, $true)) {
        $state = $script:G.Save()
        if ($mirror) { $script:G.TranslateTransform(144, 0); $script:G.ScaleTransform(-1, 1) }
        $h = New-Path
        $h.AddBezier((P 14 132), (P 4 88), (P 18 46), (P 54 14))
        $h.AddBezier((P 54 14), (P 38 54), (P 36 96), (P 52 132))
        $h.CloseFigure()
        Fill-Path $script:G $h $Horn
        $script:G.SetClip($h)
        $b = Br $Base
        $script:G.FillRectangle($b, 0, 108, 144, 40)
        $b.Dispose()
        $script:G.ResetClip()
        $h.Dispose()
        $script:G.Restore($state)
    }
}

function Draw-undies {
    # contour of a body without head and shoulders, drawn as the left half and mirrored:
    # the breasts are the top edge, legs end at knee height
    foreach ($mirror in @($false, $true)) {
        $state = $script:G.Save()
        if ($mirror) { $script:G.TranslateTransform(144, 0); $script:G.ScaleTransform(-1, 1) }
        $p = Pn $White 6
        $g = $script:G
        # breast: lower half of a circle from the side to the middle
        $g.DrawArc($p, [single]30, [single]-11, [single]42, [single]42, [single]0, [single]180)
        $g.DrawBezier($p, (P 30 10), (P 30 36), (P 46 46), (P 48 64))
        $g.DrawBezier($p, (P 48 64), (P 48 82), (P 25 86), (P 25 106))
        $g.DrawLine($p, [single]25, [single]106, [single]30, [single]136)
        $g.DrawBezier($p, (P 72 102), (P 71 114), (P 69 126), (P 68 136))
        $p.Dispose()
        $script:G.Restore($state)
    }
}

function Bikini-Set($suit, $string, $edge = $null) {
    # strings first, the pieces on top
    Stroke-Line $script:G 38 32 58 6 4 $string
    Stroke-Line $script:G 106 32 86 6 4 $string
    Stroke-Line $script:G 20 70 4 76 4 $string
    Stroke-Line $script:G 124 70 140 76 4 $string
    Stroke-Line $script:G 60 70 84 70 4 $string
    Stroke-Line $script:G 24 98 4 90 4 $string
    Stroke-Line $script:G 120 98 140 90 4 $string
    foreach ($d in @(-1, 1)) {
        $c = New-Path
        $c.AddPolygon([System.Drawing.PointF[]]@((P (72 + $d * 34) 32), (P (72 + $d * 54) 72), (P (72 + $d * 12) 72)))
        if ($edge) { Fill-Edged $script:G $c $suit 8 $edge 2.5 } else { Fill-Soft $script:G $c $suit 8 }
        $c.Dispose()
    }
    $b = New-Path
    $b.AddPolygon([System.Drawing.PointF[]]@((P 24 94), (P 120 94), (P 72 134)))
    if ($edge) { Fill-Edged $script:G $b $suit 8 $edge 2.5 } else { Fill-Soft $script:G $b $suit 8 }
    $b.Dispose()
}

function Draw-bikini { Bikini-Set (Col 46 196 182) $White }
function Draw-bikiniwhite { Bikini-Set $White (Col 175 180 200) }
function Draw-bikiniblack { Bikini-Set $Black $White $Edge }

# one-piece: straps, scooped neck, waist, high-cut legs
function Swimsuit($fill, $edge = $null) {
    $p = New-Path
    $p.AddLine((P 46 8), (P 57 8))
    $p.AddBezier((P 57 8), (P 59 46), (P 85 46), (P 87 8))
    $p.AddLine((P 87 8), (P 98 8))
    $p.AddBezier((P 98 8), (P 98 28), (P 108 36), (P 114 46))
    $p.AddBezier((P 114 46), (P 100 66), (P 100 80), (P 116 98))
    $p.AddBezier((P 116 98), (P 98 104), (P 86 118), (P 82 134))
    $p.AddLine((P 82 134), (P 62 134))
    $p.AddBezier((P 62 134), (P 58 118), (P 46 104), (P 28 98))
    $p.AddBezier((P 28 98), (P 44 80), (P 44 66), (P 30 46))
    $p.AddBezier((P 30 46), (P 36 36), (P 46 28), (P 46 8))
    $p.CloseFigure()
    if ($edge) { Fill-Edged $script:G $p $fill 4 $edge 2.5 } else { Fill-Soft $script:G $p $fill 4 }
    $p.Dispose()
}

function Draw-swimsuit { Swimsuit (Col 64 104 224) }
function Draw-swimsuitwhite { Swimsuit $White }
function Draw-swimsuitblack { Swimsuit $Black $Edge }

function Draw-tshirt {
    $Shorts = Col 64 104 224
    # t-shirt: crew neck, short sleeves
    $t = New-Path
    $t.AddLine((P 52 8), (P 18 22))
    $t.AddLine((P 18 22), (P 8 48))
    $t.AddLine((P 8 48), (P 30 56))
    $t.AddLine((P 30 56), (P 38 44))
    $t.AddLine((P 38 44), (P 38 80))
    $t.AddLine((P 38 80), (P 106 80))
    $t.AddLine((P 106 80), (P 106 44))
    $t.AddLine((P 106 44), (P 114 56))
    $t.AddLine((P 114 56), (P 136 48))
    $t.AddLine((P 136 48), (P 126 22))
    $t.AddLine((P 126 22), (P 92 8))
    $t.AddBezier((P 92 8), (P 88 26), (P 56 26), (P 52 8))
    $t.CloseFigure()
    Fill-Soft $script:G $t $White 4
    $t.Dispose()
    # shorts
    $s = New-Path
    $s.AddPolygon([System.Drawing.PointF[]]@((P 38 92), (P 106 92), (P 114 136), (P 80 136), (P 72 112), (P 64 136), (P 30 136)))
    Fill-Soft $script:G $s $Shorts 4
    $s.Dispose()
}

function Draw-hoodie {
    $Body = Col 120 132 170
    $Shade = Col 84 94 128
    $Jeans = Col 56 76 150
    # hood behind the neck
    Fill-Ellipse $script:G 72 24 60 40 $Shade
    # body with long sleeves and a V opening
    $t = New-Path
    $t.AddLine((P 50 16), (P 22 28))
    $t.AddLine((P 22 28), (P 6 84))
    $t.AddLine((P 6 84), (P 26 92))
    $t.AddLine((P 26 92), (P 38 56))
    $t.AddLine((P 38 56), (P 38 96))
    $t.AddLine((P 38 96), (P 106 96))
    $t.AddLine((P 106 96), (P 106 56))
    $t.AddLine((P 106 56), (P 118 92))
    $t.AddLine((P 118 92), (P 138 84))
    $t.AddLine((P 138 84), (P 122 28))
    $t.AddLine((P 122 28), (P 94 16))
    $t.AddLine((P 94 16), (P 72 40))
    $t.CloseFigure()
    Fill-Soft $script:G $t $Body 4
    $t.Dispose()
    # drawstrings and front pocket
    Stroke-Line $script:G 64 36 62 56 3 $White
    Stroke-Line $script:G 80 36 82 56 3 $White
    $k = New-Path
    $k.AddPolygon([System.Drawing.PointF[]]@((P 54 70), (P 90 70), (P 98 92), (P 46 92)))
    Fill-Soft $script:G $k $Shade 3
    $k.Dispose()
    # trousers
    $s = New-Path
    $s.AddPolygon([System.Drawing.PointF[]]@((P 40 106), (P 104 106), (P 108 138), (P 79 138), (P 72 120), (P 65 138), (P 36 138)))
    Fill-Soft $script:G $s $Jeans 3
    $s.Dispose()
}

function Draw-suit {
    $Jacket = Col 70 86 150
    $Lapel = Col 44 56 108
    # blazer: long sleeves, nipped waist, flared hem
    $t = New-Path
    $t.AddLine((P 54 8), (P 26 20))
    $t.AddLine((P 26 20), (P 8 88))
    $t.AddLine((P 8 88), (P 27 94))
    $t.AddLine((P 27 94), (P 40 50))
    $t.AddBezier((P 40 50), (P 42 64), (P 48 70), (P 46 78))
    $t.AddBezier((P 46 78), (P 42 86), (P 38 92), (P 36 100))
    $t.AddLine((P 36 100), (P 108 100))
    $t.AddBezier((P 108 100), (P 106 92), (P 102 86), (P 98 78))
    $t.AddBezier((P 98 78), (P 96 70), (P 102 64), (P 104 50))
    $t.AddLine((P 104 50), (P 117 94))
    $t.AddLine((P 117 94), (P 136 88))
    $t.AddLine((P 136 88), (P 118 20))
    $t.AddLine((P 118 20), (P 90 8))
    $t.CloseFigure()
    Fill-Soft $script:G $t $Jacket 4
    $t.Dispose()
    # white top in the deep V
    $v = New-Path
    $v.AddPolygon([System.Drawing.PointF[]]@((P 54 8), (P 90 8), (P 72 72)))
    Fill-Soft $script:G $v $White 2
    $v.Dispose()
    # lapels
    foreach ($d in @(-1, 1)) {
        $l = New-Path
        $l.AddPolygon([System.Drawing.PointF[]]@((P (72 + $d * 18) 8), (P (72 + $d * 34) 36), (P (72 + $d * 23) 44), (P 72 76)))
        Fill-Soft $script:G $l $Lapel 2
        $l.Dispose()
    }
    Fill-Circle $script:G 72 86 4 $Lapel
    # pencil skirt
    $s = New-Path
    $s.AddPolygon([System.Drawing.PointF[]]@((P 42 108), (P 102 108), (P 98 138), (P 46 138)))
    Fill-Soft $script:G $s $Lapel 3
    $s.Dispose()
}

function Draw-trunks {
    $Stripe = Col 255 236 200
    # swim trunks: wide shorts, white waistband with a drawstring, side stripes
    $s = New-Path
    $s.AddPolygon([System.Drawing.PointF[]]@((P 24 24), (P 120 24), (P 136 122), (P 82 122), (P 72 76), (P 62 122), (P 8 122)))
    Fill-Soft $script:G $s $Orange 5
    $s.Dispose()
    Stroke-Line $script:G 24 36 120 36 7 $White
    Stroke-Line $script:G 22 52 12 112 5 $Stripe
    Stroke-Line $script:G 122 52 132 112 5 $Stripe
    Stroke-Line $script:G 72 38 62 58 3.5 $White
    Stroke-Line $script:G 72 38 82 58 3.5 $White
}

function Draw-halo {
    # golden ring seen at an angle, a little glow ring inside and two sparkles
    With-Transform $script:G 72 80 1 -10 {
        $p = Pn $Gold 15
        $script:G.DrawEllipse($p, [single]-56, [single]-24, [single]112, [single]48)
        $p.Dispose()
        $q = Pn (Col 255 232 130) 5
        $script:G.DrawArc($q, [single]-56, [single]-24, [single]112, [single]48, [single]200, [single]70)
        $q.Dispose()
    }
    foreach ($s in @(@(24, 28, 14), @(120, 30, 10), @(112, 124, 12))) {
        With-Transform $script:G $s[0] $s[1] $s[2] 0 {
            $sp = New-Path
            $sp.AddBezier((P 0 -1), (P 0.1 -0.2), (P 0.2 -0.1), (P 1 0))
            $sp.AddBezier((P 1 0), (P 0.2 0.1), (P 0.1 0.2), (P 0 1))
            $sp.AddBezier((P 0 1), (P -0.1 0.2), (P -0.2 0.1), (P -1 0))
            $sp.AddBezier((P -1 0), (P -0.2 -0.1), (P -0.1 -0.2), (P 0 -1))
            $sp.CloseFigure()
            Fill-Path $script:G $sp $White
            $sp.Dispose()
        }
    }
}

function Draw-hat {
    $Ribbon = Col 232 56 79
    $Line = Col 200 204 212
    # wide brim
    Fill-Ellipse $script:G 72 92 136 52 $White
    Stroke-Arc $script:G 72 92 52 17 20 140 3 $Line
    # dome crown
    $c = New-Path
    $c.AddBezier((P 36 92), (P 30 28), (P 114 28), (P 108 92))
    $c.AddBezier((P 108 92), (P 90 102), (P 54 102), (P 36 92))
    $c.CloseFigure()
    Fill-Soft $script:G $c $White 3
    $c.Dispose()
    # ribbon band around the base of the crown with a bow
    $crown = New-Path
    $crown.AddBezier((P 36 92), (P 30 28), (P 114 28), (P 108 92))
    $crown.AddBezier((P 108 92), (P 90 102), (P 54 102), (P 36 92))
    $crown.CloseFigure()
    $script:G.SetClip($crown)
    $rb = Br $Ribbon
    $script:G.FillRectangle($rb, 20, 68, 110, 40)
    $rb.Dispose()
    $script:G.ResetClip()
    $crown.Dispose()
    # bow on the right: two loops, knot, one tail
    foreach ($d in @(-1, 1)) {
        $l = New-Path
        $l.AddPolygon([System.Drawing.PointF[]]@((P 96 88), (P (96 + $d * 22) 74), (P (96 + $d * 22) 104)))
        Fill-Soft $script:G $l $Ribbon 3
        $l.Dispose()
    }
    Fill-Circle $script:G 96 89 7 $Ribbon
}

function Draw-coffee {
    $Coffee = Col 150 88 48
    # steam
    foreach ($x in @(44, 66, 88)) {
        $p = Pn $Grey 6
        $script:G.DrawBezier($p, (P $x 42), (P ($x - 12) 30), (P ($x + 12) 20), (P $x 6))
        $p.Dispose()
    }
    # saucer, cup, handle
    Fill-Ellipse $script:G 66 122 124 22 $White
    Stroke-Arc $script:G 112 82 22 20 -90 180 10 $White
    $cup = New-Path
    $cup.AddLine((P 20 54), (P 112 54))
    $cup.AddBezier((P 112 54), (P 112 142), (P 20 142), (P 20 54))
    $cup.CloseFigure()
    Fill-Soft $script:G $cup $White 4
    $cup.Dispose()
    Fill-Ellipse $script:G 66 56 92 20 $White
    Fill-Ellipse $script:G 66 57 78 13 $Coffee
}

function Draw-boba {
    $Tea = Col 226 182 134
    $Pearl = Col 84 50 36
    # straw first, the lid is drawn over its foot
    Stroke-Line $script:G 74 34 98 8 13 $Pink
    # tapered cup
    $c = New-Path
    $c.AddPolygon([System.Drawing.PointF[]]@((P 28 56), (P 116 56), (P 102 132), (P 42 132)))
    Fill-Soft $script:G $c $Tea 8
    $c.Dispose()
    Stroke-Line $script:G 44 70 54 118 5 (Col 255 255 255 120)
    # pearls
    foreach ($b in @(@(54, 118), @(72, 118), @(90, 118), @(63, 102), @(81, 102), @(72, 86))) {
        Fill-Circle $script:G $b[0] $b[1] 8.5 $Pearl
    }
    # domed lid and rim
    $d = New-Path
    $d.AddBezier((P 30 54), (P 30 22), (P 114 22), (P 114 54))
    $d.CloseFigure()
    Fill-Soft $script:G $d $White 3
    $d.Dispose()
    $rim = New-RoundedRect 22 48 100 14 7
    Fill-Path $script:G $rim $White
    $rim.Dispose()
}

function Draw-sunglasses {
    $Lens = Col 255 140 95
    # bridge and short temples first, then the framed lenses
    Stroke-Arc $script:G 72 60 11 9 180 180 8 $White
    Stroke-Line $script:G 8 52 2 44 8 $White
    Stroke-Line $script:G 136 52 142 44 8 $White
    foreach ($x in @(8, 78)) {
        $l = New-Path
        $l.AddBezier((P $x 48), (P ($x + 28) 46), (P ($x + 56) 46), (P ($x + 58) 48))
        $l.AddBezier((P ($x + 58) 48), (P ($x + 58) 98), (P ($x + 44) 108), (P ($x + 30) 108))
        $l.AddBezier((P ($x + 30) 108), (P ($x + 10) 108), (P $x 90), (P $x 48))
        $l.CloseFigure()
        Fill-Path $script:G $l $Lens
        $p = Pn $White 8
        $script:G.DrawPath($p, $l)
        $p.Dispose()
        $l.Dispose()
        Stroke-Line $script:G ($x + 12) 74 ($x + 24) 58 5 (Col 255 255 255 170)
    }
}

function Draw-baseballcap {
    # side view facing right; crown in accent colour, white visor and button
    With-Transform $script:G 0 8 1 0 {
        $c = New-Path
        $c.AddBezier((P 18 102), (P 12 40), (P 46 20), (P 70 20))
        $c.AddBezier((P 70 20), (P 100 20), (P 120 50), (P 118 102))
        $c.CloseFigure()
        Fill-Soft $script:G $c $Pink 3
        $c.Dispose()
        $p = Pn (Col 255 255 255 200) 3.5
        $script:G.DrawBezier($p, (P 70 24), (P 52 44), (P 48 74), (P 50 102))
        $script:G.DrawBezier($p, (P 70 24), (P 88 40), (P 98 70), (P 98 100))
        $p.Dispose()
        $v = New-Path
        $v.AddBezier((P 78 96), (P 112 84), (P 136 92), (P 142 106))
        $v.AddBezier((P 142 106), (P 116 114), (P 88 112), (P 64 106))
        $v.CloseFigure()
        Fill-Soft $script:G $v $White 3
        $v.Dispose()
        Fill-Circle $script:G 70 20 8 $White
    }
}

function Draw-blanket {
    $Wool = Col 255 150 185
    $Under = Col 255 208 222
    $Stripe = Col 255 255 255 210
    # a throw laid flat: fringe at the bottom, plaid stripes, top right corner folded over
    for ($x = 22; $x -le 122; $x += 12.5) { Stroke-Line $script:G $x 108 $x 134 5 $Wool }
    $m = New-Path
    $m.AddPolygon([System.Drawing.PointF[]]@((P 14 16), (P 88 16), (P 130 58), (P 130 112), (P 14 112)))
    Fill-Soft $script:G $m $Wool 6
    $script:G.SetClip($m)
    Stroke-Line $script:G 42 10 42 118 6 $Stripe
    Stroke-Line $script:G 98 10 98 118 6 $Stripe
    Stroke-Line $script:G 8 46 136 46 6 $Stripe
    Stroke-Line $script:G 8 84 136 84 6 $Stripe
    $script:G.ResetClip()
    $m.Dispose()
    $f = New-Path
    $f.AddPolygon([System.Drawing.PointF[]]@((P 88 16), (P 130 58), (P 88 58)))
    Fill-Soft $script:G $f $Under 6
    $f.Dispose()
}

# ---------- tail helpers ----------
function Bez-Pt($a, $b, $c, $d, $t) {
    $u = 1 - $t
    P ($u * $u * $u * $a.X + 3 * $u * $u * $t * $b.X + 3 * $u * $t * $t * $c.X + $t * $t * $t * $d.X) ($u * $u * $u * $a.Y + 3 * $u * $u * $t * $b.Y + 3 * $u * $t * $t * $c.Y + $t * $t * $t * $d.Y)
}
# unit tangent of the bezier at $t, as @(dx, dy)
function Bez-Dir($a, $b, $c, $d, $t) {
    $u = 1 - $t
    $dx = 3 * $u * $u * ($b.X - $a.X) + 6 * $u * $t * ($c.X - $b.X) + 3 * $t * $t * ($d.X - $c.X)
    $dy = 3 * $u * $u * ($b.Y - $a.Y) + 6 * $u * $t * ($c.Y - $b.Y) + 3 * $t * $t * ($d.Y - $c.Y)
    $l = [Math]::Sqrt($dx * $dx + $dy * $dy)
    if ($l -lt 0.0001) { $l = 1 }
    return @(($dx / $l), ($dy / $l))
}
# outline of a tail along the bezier a-b-c-d from t0 to t1; $w is a scriptblock { param($t) width }, $ws scales it,
# $off shifts the centre line sideways by that fraction of the half width
function Tail-Path($a, $b, $c, $d, [scriptblock]$w, $t0 = 0.0, $t1 = 1.0, $off = 0.0, $ws = 1.0) {
    $n = 48
    $left = @(); $right = @()
    for ($i = 0; $i -le $n; $i++) {
        $t = $t0 + ($t1 - $t0) * $i / $n
        $pt = Bez-Pt $a $b $c $d $t
        $dir = Bez-Dir $a $b $c $d $t
        $nx = -$dir[1]; $ny = $dir[0]
        $half = $ws * [double](& $w $t) / 2
        $cx = $pt.X + $nx * $off * $half; $cy = $pt.Y + $ny * $off * $half
        $left += (P ($cx + $nx * $half) ($cy + $ny * $half))
        $right += (P ($cx - $nx * $half) ($cy - $ny * $half))
    }
    [array]::Reverse($right)
    $path = New-Path
    $path.AddPolygon([System.Drawing.PointF[]]($left + $right))
    return ,$path
}

# same as Tail-Path, but along a list of centre points (for shapes a single bezier cannot do)
function Tail-PathPts($pts, [scriptblock]$w, $t0 = 0.0, $t1 = 1.0, $off = 0.0, $ws = 1.0) {
    $n = $pts.Count - 1
    $left = @(); $right = @()
    for ($i = [int][Math]::Round($t0 * $n); $i -le [int][Math]::Round($t1 * $n); $i++) {
        $i0 = [Math]::Max(0, $i - 1); $i1 = [Math]::Min($n, $i + 1)
        $dx = $pts[$i1].X - $pts[$i0].X; $dy = $pts[$i1].Y - $pts[$i0].Y
        $l = [Math]::Sqrt($dx * $dx + $dy * $dy)
        if ($l -lt 0.0001) { $l = 1 }
        $nx = -$dy / $l; $ny = $dx / $l
        $half = $ws * [double](& $w ($i / $n)) / 2
        $cx = $pts[$i].X + $nx * $off * $half; $cy = $pts[$i].Y + $ny * $off * $half
        $left += (P ($cx + $nx * $half) ($cy + $ny * $half))
        $right += (P ($cx - $nx * $half) ($cy - $ny * $half))
    }
    [array]::Reverse($right)
    $path = New-Path
    $path.AddPolygon([System.Drawing.PointF[]]($left + $right))
    return ,$path
}

function Draw-dragontail {
    $Spike = Col 255 215 80
    $a = P 44 136; $b = P 28 70; $c = P 72 92; $d = P 124 26
    $wf = { param($t) 8 + 28 * [Math]::Pow(1 - $t, 1.1) }
    # back plates on the outer side, behind the body
    foreach ($t in @(0.14, 0.28, 0.42, 0.56, 0.7, 0.84)) {
        $pt = Bez-Pt $a $b $c $d $t
        $dir = Bez-Dir $a $b $c $d $t
        $nx = $dir[1]; $ny = -$dir[0]
        $half = (& $wf $t) / 2
        $h = 10 + 18 * (1 - $t)
        $bw = 9 + 6 * (1 - $t)
        $tri = New-Path
        $tri.AddPolygon([System.Drawing.PointF[]]@(
            (P ($pt.X + $nx * ($half - 3) - $dir[0] * $bw) ($pt.Y + $ny * ($half - 3) - $dir[1] * $bw)),
            (P ($pt.X + $nx * ($half + $h) + $dir[0] * 10) ($pt.Y + $ny * ($half + $h) + $dir[1] * 10)),
            (P ($pt.X + $nx * ($half - 3) + $dir[0] * $bw) ($pt.Y + $ny * ($half - 3) + $dir[1] * $bw))))
        Fill-Soft $script:G $tri $Spike 3
        $tri.Dispose()
    }
    $body = Tail-Path $a $b $c $d $wf
    Fill-Soft $script:G $body $Green 3
    $body.Dispose()
    # belly bands
    foreach ($t in @(0.17, 0.31, 0.45, 0.59, 0.73)) {
        $pt = Bez-Pt $a $b $c $d $t
        $dir = Bez-Dir $a $b $c $d $t
        $nx = -$dir[1]; $ny = $dir[0]
        $half = (& $wf $t) / 2 * 0.8
        Stroke-Line $script:G ($pt.X - $nx * $half) ($pt.Y - $ny * $half) ($pt.X + $nx * $half) ($pt.Y + $ny * $half) 3 (Col 30 150 85)
    }
}

function Draw-foxtail {
    $Fur = Col 255 140 40
    $a = P 34 134; $b = P 22 88; $c = P 50 36; $d = P 122 10
    $wf = { param($t) 8 + 50 * [Math]::Pow([Math]::Sin([Math]::PI * [Math]::Pow($t, 1.1)), 0.8) }
    $body = Tail-Path $a $b $c $d $wf
    Fill-Soft $script:G $body $Fur 4
    $body.Dispose()
    $tip = Tail-Path $a $b $c $d $wf 0.72 1.0
    Fill-Soft $script:G $tip $White 4
    $tip.Dispose()
}

function Draw-horsetail {
    $Light = Col 186 120 66
    $Mid = Col 128 76 38
    # long hair falling from the dock: arches out to the right, swells in the middle, ends in a wispy point
    $a = P 30 36; $b = P 96 4; $c = P 136 68; $d = P 80 138
    $wf = { param($t) 3 + 13 * (1 - $t) + 34 * [Math]::Pow([Math]::Sin([Math]::PI * $t), 0.85) }
    $body = Tail-Path $a $b $c $d $wf
    Fill-Soft $script:G $body $Light 3
    $body.Dispose()
    # strands following the flow
    foreach ($o in @(-0.5, 0.0, 0.5)) {
        $pts = @()
        for ($i = 0; $i -le 24; $i++) {
            $t = 0.14 + 0.8 * $i / 24
            $pt = Bez-Pt $a $b $c $d $t
            $dir = Bez-Dir $a $b $c $d $t
            $half = (& $wf $t) / 2
            $pts += (P ($pt.X - $dir[1] * $o * $half) ($pt.Y + $dir[0] * $o * $half))
        }
        $p = Pn $Mid 4
        $script:G.DrawLines($p, [System.Drawing.PointF[]]$pts)
        $p.Dispose()
    }
    # dock
    With-Transform $script:G 28 37 1 -28 {
        $band = New-RoundedRect (-11) (-14) 22 28 8
        Fill-Path $script:G $band $Mid
        $band.Dispose()
    }
}

function Draw-liontail {
    $Tan = Col 245 195 120
    $Tuft = Col 235 150 25
    $a = P 20 138; $b = P 28 88; $c = P 112 112; $d = P 98 68
    $p = Pn $Tan 9
    $script:G.DrawBezier($p, $a, $b, $c, $d)
    $p.Dispose()
    With-Transform $script:G 98 74 1 -4 {
        $t = New-Path
        $t.AddBezier((P 0 8), (P -30 -4), (P -26 -34), (P -16 -52))
        $t.AddLine((P -16 -52), (P -6 -40))
        $t.AddLine((P -6 -40), (P -2 -66))
        $t.AddLine((P -2 -66), (P 5 -42))
        $t.AddLine((P 5 -42), (P 14 -58))
        $t.AddLine((P 14 -58), (P 13 -48))
        $t.AddBezier((P 13 -48), (P 28 -32), (P 30 -4), (P 0 8))
        $t.CloseFigure()
        Fill-Soft $script:G $t $Tuft 3
        $t.Dispose()
        Stroke-Arc $script:G 0 -4 14 10 20 140 4 (Col 255 205 90)
    }
}

function Draw-longhairtail {
    $Cream = Col 255 232 195
    $TipCol = Col 160 168 190
    $a = P 32 134; $b = P 38 72; $c = P 86 96; $d = P 124 14
    # smooth plume with a sawtooth edge: every barb points towards the tip
    $wf = { param($t)
        $base = 10 + 34 * [Math]::Sin([Math]::PI * [Math]::Pow($t, 0.85))
        $saw = ($t * 8) % 1.0
        $base * (0.8 + 0.4 * $saw) + 4 }
    $body = Tail-Path $a $b $c $d $wf
    Fill-Soft $script:G $body $Cream 2
    $body.Dispose()
    $tip = Tail-Path $a $b $c $d $wf 0.8 1.0
    Fill-Soft $script:G $tip $TipCol 2
    $tip.Dispose()
}

function Draw-mermaidtail {
    $Fin = Col 255 130 190
    $Skin = Col 55 220 200
    $Scale = Col 160 248 238
    $a = P 26 130; $b = P 28 108; $c = P 46 100; $d = P 68 88
    $wf = { param($t) 14 + 30 * [Math]::Pow(1 - $t, 1.2) }
    With-Transform $script:G 68 88 0.85 -48 {
        $f = New-Path
        $f.AddBezier((P -6 -7), (P 20 -10), (P 36 -34), (P 56 -58))
        $f.AddBezier((P 56 -58), (P 46 -32), (P 46 -12), (P 38 0))
        $f.AddBezier((P 38 0), (P 46 12), (P 46 32), (P 56 58))
        $f.AddBezier((P 56 58), (P 36 34), (P 20 10), (P -6 7))
        $f.CloseFigure()
        Fill-Soft $script:G $f $Fin 4
        $f.Dispose()
        foreach ($y in @(-30, -14, 14, 30)) { Stroke-Line $script:G 12 ($y * 0.35) 36 $y 3 (Col 255 190 220) }
    }
    $body = Tail-Path $a $b $c $d $wf
    Fill-Soft $script:G $body $Skin 4
    $script:G.SetClip($body)
    foreach ($t in @(0.1, 0.3, 0.5, 0.7, 0.9)) {
        $pt = Bez-Pt $a $b $c $d $t
        $half = (& $wf $t) / 2
        foreach ($k in @(-0.5, 0.5)) {
            $dir = Bez-Dir $a $b $c $d $t
            Stroke-Arc $script:G ($pt.X - $dir[1] * $half * $k) ($pt.Y + $dir[0] * $half * $k) 8 8 0 180 3 $Scale
        }
    }
    $script:G.ResetClip()
    $body.Dispose()
}

function Draw-ninetail {
    $Tips = @((Col 255 130 190), (Col 110 200 255))
    $Gap = Col 32 32 40
    $order = @(-4, 4, -3, 3, -2, 2, -1, 1, 0)
    foreach ($k in $order) {
        $len = 124 - 3.7 * $k * $k
        $curl = $k * 4
        With-Transform $script:G 72 140 1 ($k * 19) {
            # leaf with its tip bent outwards
            $leaf = New-Path
            $leaf.AddBezier((P 0 0), (P (-28) (-$len * 0.25)), (P (-30 + $curl * 0.5) (-$len * 0.7)), (P $curl (-$len)))
            $leaf.AddBezier((P $curl (-$len)), (P (30 + $curl * 0.5) (-$len * 0.7)), (P 28 (-$len * 0.25)), (P 0 0))
            $leaf.CloseFigure()
            Fill-Soft $script:G $leaf $Gap 8
            Fill-Path $script:G $leaf $White
            $script:G.SetClip($leaf)
            $b = Br $Tips[[Math]::Abs($k) % 2]
            $script:G.FillRectangle($b, -50, (-$len - 4), 100, ($len * 0.3 + 4))
            $b.Dispose()
            $script:G.ResetClip()
            $leaf.Dispose()
        }
    }
}

function Draw-raccoontail {
    $Light = Col 222 222 232
    $Ring = Col 128 132 152
    $a = P 28 134; $b = P 20 84; $c = P 56 56; $d = P 116 18
    $wf = { param($t) (18 + 46 * [Math]::Min(1.0, $t * 2.5)) * [Math]::Sqrt([Math]::Max(0.0, 1 - [Math]::Pow($t, 6))) }
    $body = Tail-Path $a $b $c $d $wf
    Fill-Soft $script:G $body $Light 3
    $script:G.SetClip($body)
    foreach ($r in @(@(0.16, 0.3), @(0.44, 0.58), @(0.72, 0.86), @(0.95, 1.1))) {
        $band = Tail-Path $a $b $c $d $wf $r[0] ([Math]::Min(1.0, $r[1])) 0.0 1.5
        Fill-Path $script:G $band $Ring
        $band.Dispose()
    }
    $script:G.ResetClip()
    $body.Dispose()
}

function Draw-squirreltail {
    $Fur = Col 235 115 65
    $Stripe = Col 255 185 120
    # centre line: a short stem up from the lower left, then a big arc over the top that curls back down
    $pts = @()
    for ($i = 0; $i -le 60; $i++) {
        $u = $i / 60.0
        if ($u -lt 0.3) {
            $s = $u / 0.3
            $pts += (P (46 - 4 * $s) (136 - 74 * $s))
        } else {
            $ang = (180 + 240 * (($u - 0.3) / 0.7)) * [Math]::PI / 180
            $pts += (P (76 + 34 * [Math]::Cos($ang)) (62 + 34 * [Math]::Sin($ang)))
        }
    }
    $wf = { param($t) (22 + 18 * [Math]::Min(1.0, $t * 3)) * [Math]::Sqrt([Math]::Max(0.0, 1 - [Math]::Pow($t, 12))) }
    $body = Tail-PathPts $pts $wf
    Fill-Soft $script:G $body $Fur 4
    $script:G.SetClip($body)
    foreach ($o in @(-0.5, 0.15)) {
        $s = Tail-PathPts $pts { param($t) 5 } 0.1 0.9 $o 1.0
        Fill-Path $script:G $s $Stripe
        $s.Dispose()
    }
    $under = Tail-PathPts $pts $wf 0.78 1.0 0.0 1.0
    Fill-Path $script:G $under $White
    $under.Dispose()
    $script:G.ResetClip()
    $body.Dispose()
}

function Draw-thicktail {
    $Skin = Col 150 225 80
    $BandCol = Col 100 190 60
    $Spot = Col 215 250 150
    # short and very fat: a thick round pen stroke with stripes across it
    $a = P 24 130; $b = P 32 100; $c = P 52 66; $d = P 100 40
    $wf = { param($t) (30 + 40 * [Math]::Pow($t, 0.6)) * [Math]::Sqrt([Math]::Max(0.0, 1 - [Math]::Pow($t, 10))) }
    $body = Tail-Path $a $b $c $d $wf
    Fill-Soft $script:G $body $Skin 5
    $script:G.SetClip($body)
    foreach ($r in @(@(0.3, 0.4), @(0.58, 0.68), @(0.84, 0.94))) {
        $bandP = Tail-Path $a $b $c $d $wf $r[0] $r[1] 0.0 1.5
        Fill-Path $script:G $bandP $BandCol
        $bandP.Dispose()
    }
    $script:G.ResetClip()
    $body.Dispose()
}

function Draw-sharktail {
    $Blue = Col 110 165 225
    $f = New-Path
    $f.AddLine((P 6 64), (P 6 88))
    $f.AddBezier((P 6 88), (P 40 90), (P 70 108), (P 118 132))
    $f.AddBezier((P 118 132), (P 92 112), (P 78 96), (P 80 78))
    $f.AddBezier((P 80 78), (P 82 52), (P 100 30), (P 132 6))
    $f.AddBezier((P 132 6), (P 80 24), (P 36 56), (P 6 64))
    $f.CloseFigure()
    Fill-Soft $script:G $f $Blue 4
    $script:G.SetClip($f)
    foreach ($s in @(@(34, 76, 5), @(52, 70, 4.5), @(60, 86, 6), @(78, 60, 5), @(80, 82, 4.5), @(94, 48, 4.5), @(96, 98, 4.5), @(112, 26, 4), @(24, 82, 3.5), @(70, 74, 3.5))) {
        Fill-Circle $script:G $s[0] $s[1] $s[2] $White
    }
    $script:G.ResetClip()
    $f.Dispose()
}

function Draw-cattail {
    $Fur = Col 255 218 232
    $TipCol = Col 255 120 165
    $a = P 34 138; $b = P 118 108; $c = P 8 56; $d = P 100 12
    $wf = { param($t) 15 }
    $body = Tail-Path $a $b $c $d $wf
    Fill-Soft $script:G $body $Fur 4
    $body.Dispose()
    $tip = Tail-Path $a $b $c $d $wf 0.76 1.0
    Fill-Soft $script:G $tip $TipCol 4
    $tip.Dispose()
}

function Draw-nightcap {
    # floppy sleeping cap: the point hangs down on the right, pom-pom at its end, white band
    $c = New-Path
    $c.AddBezier((P 24 108), (P 22 52), (P 56 18), (P 90 24))
    $c.AddBezier((P 90 24), (P 118 30), (P 132 56), (P 126 84))
    $c.AddBezier((P 126 84), (P 114 66), (P 104 60), (P 94 62))
    $c.AddBezier((P 94 62), (P 104 76), (P 108 92), (P 108 108))
    $c.CloseFigure()
    Fill-Soft $script:G $c $LightBlue 4
    $c.Dispose()
    $b = New-RoundedRect 14 100 104 30 14
    Fill-Path $script:G $b $White
    $b.Dispose()
    Fill-Circle $script:G 124 92 14 $White
}

# ---------- wings ----------
# one feather from (x0, y0) to (x1, y1), $w is its half width; filled with $c, outlined with $edge
function Wing-Feather($x0, $y0, $x1, $y1, $w, $c, $edge, $ew = 3) {
    $len = [Math]::Sqrt(($x1 - $x0) * ($x1 - $x0) + ($y1 - $y0) * ($y1 - $y0))
    $ang = [Math]::Atan2($y1 - $y0, $x1 - $x0) * 180 / [Math]::PI
    With-Transform $script:G $x0 $y0 1 $ang {
        $f = New-Path
        $f.AddBezier((P 0 0), (P ($len * 0.25) (-$w)), (P ($len * 0.8) (-$w * 0.9)), (P $len 0))
        $f.AddBezier((P $len 0), (P ($len * 0.8) ($w * 0.7)), (P ($len * 0.25) $w), (P 0 0))
        $f.CloseFigure()
        Fill-Path $script:G $f $c
        $p = Pn $edge $ew
        $script:G.DrawPath($p, $f)
        $p.Dispose()
        $f.Dispose()
    }
}

function Draw-fairywings {
    # see-through dragonfly wing: long upper lobe, shorter lower lobes, a vein in each
    $Fill = Col 150 235 245 105
    $Edge = Col 195 250 255
    foreach ($l in @(@(74, 136, 9), @(132, 92, 17), @(126, 12, 25))) {
        Wing-Feather 16 126 $l[0] $l[1] $l[2] $Fill $Edge 4
        $p = Pn (Col 255 255 255 190) 2
        $script:G.DrawLine($p, (P 16 126), (P (16 + ($l[0] - 16) * 0.82) (126 + ($l[1] - 126) * 0.82)))
        $p.Dispose()
    }
}

function Draw-featherwings {
    # bird wing in three layers: long primaries, shorter secondaries, coverts on top
    $Prim = Col 225 80 160
    $PrimEdge = Col 170 45 115
    $Sec = Col 235 50 50
    $SecEdge = Col 165 25 30
    $Cov = Col 150 195 95
    foreach ($t in @(@(124, 92), @(136, 70), @(140, 46), @(138, 22))) { Wing-Feather 62 62 $t[0] $t[1] 12 $Prim $PrimEdge }
    foreach ($t in @(@(48, 132), @(70, 126), @(90, 114), @(106, 98))) { Wing-Feather 34 78 $t[0] $t[1] 12 $Sec $SecEdge }
    $c = New-Path
    $c.AddBezier((P 8 132), (P 8 70), (P 44 20), (P 110 16))
    $c.AddBezier((P 110 16), (P 96 40), (P 86 56), (P 70 70))
    $c.AddBezier((P 70 70), (P 52 84), (P 30 96), (P 8 132))
    $c.CloseFigure()
    Fill-Soft $script:G $c $Cov 4
    $c.Dispose()
}

function Draw-impwings {
    # small sharp bat wing
    $Wing = Col 232 56 79
    $w = New-Path
    $w.AddBezier((P 14 104), (P 26 66), (P 40 44), (P 62 42))
    $w.AddBezier((P 62 42), (P 92 42), (P 116 44), (P 136 26))
    $w.AddBezier((P 136 26), (P 114 44), (P 102 58), (P 98 78))
    $w.AddBezier((P 98 78), (P 86 66), (P 70 70), (P 62 92))
    $w.AddBezier((P 62 92), (P 50 80), (P 34 86), (P 14 104))
    $w.CloseFigure()
    Fill-Soft $script:G $w $Wing 4
    $w.Dispose()
}

function Draw-littlewings {
    # small white wing like on a winged helmet: smooth leading edge, three rounded feathers behind it
    $Shade = Col 190 200 225
    $w = New-Path
    $w.AddBezier((P 26 120), (P 14 62), (P 52 18), (P 120 14))
    $w.AddBezier((P 120 14), (P 134 26), (P 122 50), (P 102 56))
    $w.AddBezier((P 102 56), (P 118 68), (P 106 90), (P 86 90))
    $w.AddBezier((P 86 90), (P 98 104), (P 84 120), (P 66 114))
    $w.AddBezier((P 66 114), (P 56 128), (P 36 130), (P 26 120))
    $w.CloseFigure()
    Fill-Soft $script:G $w $White 3
    $w.Dispose()
    $p = Pn $Shade 5
    $script:G.DrawBezier($p, (P 102 56), (P 86 58), (P 70 56), (P 58 60))
    $script:G.DrawBezier($p, (P 86 90), (P 72 88), (P 60 84), (P 50 86))
    $p.Dispose()
}

function Draw-membranedwings {
    # large bat wing: membrane between three long fingers, bones on top, claw at the joint
    $Skin = Col 172 152 208
    $Bone = Col 96 74 140
    $m = New-Path
    $m.AddBezier((P 40 34), (P 74 14), (P 110 14), (P 140 30))
    $m.AddBezier((P 140 30), (P 108 46), (P 104 66), (P 122 92))
    $m.AddBezier((P 122 92), (P 92 80), (P 76 94), (P 76 128))
    $m.AddBezier((P 76 128), (P 60 96), (P 40 92), (P 12 108))
    $m.AddBezier((P 12 108), (P 26 84), (P 34 58), (P 40 34))
    $m.CloseFigure()
    Fill-Soft $script:G $m $Skin 3
    $m.Dispose()
    $p = Pn $Bone 6
    $script:G.DrawBezier($p, (P 12 108), (P 26 84), (P 34 58), (P 40 34))
    $script:G.DrawBezier($p, (P 40 34), (P 74 14), (P 110 14), (P 140 30))
    $script:G.DrawBezier($p, (P 40 34), (P 72 40), (P 102 62), (P 122 92))
    $script:G.DrawBezier($p, (P 40 34), (P 56 60), (P 70 92), (P 76 128))
    $p.Dispose()
    $claw = New-Path
    $claw.AddPolygon([System.Drawing.PointF[]]@((P 32 36), (P 30 10), (P 48 30)))
    Fill-Soft $script:G $claw $Bone 3
    $claw.Dispose()
}

function Draw-petitwings {
    # white wing: three long feathers out of a curled root
    $Shade = Col 190 200 225
    Wing-Feather 46 98 126 100 15 $White $Shade
    Wing-Feather 46 92 138 56 16 $White $Shade
    Wing-Feather 44 88 126 10 17 $White $Shade
    Fill-Circle $script:G 42 98 27 $White
    Stroke-Arc $script:G 42 98 15 15 30 280 5 $Shade
    Stroke-Arc $script:G 44 100 6 6 200 250 5 $Shade
}

# ---------- run ----------
function Food-Rect($x, $y, $w, $h, $c) {
    $r = New-Path
    $r.AddRectangle((New-Object System.Drawing.RectangleF($x, $y, $w, $h)))
    Fill-Path $script:G $r $c
    $r.Dispose()
}

function Draw-can {
    $Silver = Col 205 212 225
    $CanBlue = Col 70 140 255
    # body with a white diagonal band and a red stripe, clipped to the can
    $body = New-RoundedRect 36 26 72 106 10
    Fill-Soft $script:G $body $CanBlue 4
    $script:G.SetClip($body)
    $band = New-Path
    $band.AddPolygon([System.Drawing.PointF[]]@((P 20 96), (P 124 56), (P 124 84), (P 20 124)))
    Fill-Path $script:G $band $White
    $band.Dispose()
    Food-Rect 30 44 84 10 $Red
    $script:G.ResetClip()
    $body.Dispose()
    # metal top and bottom rims
    $top = New-RoundedRect 32 20 80 14 7
    Fill-Path $script:G $top $Silver
    $top.Dispose()
    $bot = New-RoundedRect 34 124 76 12 6
    Fill-Path $script:G $bot $Silver
    $bot.Dispose()
    # pull tab
    Stroke-Arc $script:G 72 14 12 8 180 180 5 $Silver
    Stroke-Line $script:G 60 14 84 14 5 $Silver
}

function Draw-water {
    $Cap = Col 40 90 220
    $Water = Col 70 190 255
    # cap, neck ring
    $c = New-RoundedRect 56 6 32 20 5
    Fill-Soft $script:G $c $Cap 4
    $c.Dispose()
    $ring = New-RoundedRect 52 26 40 8 4
    Fill-Path $script:G $ring $White
    $ring.Dispose()
    # bottle with shoulders
    $b = New-Path
    $b.AddPolygon([System.Drawing.PointF[]]@((P 58 34), (P 86 34), (P 106 58), (P 106 130), (P 38 130), (P 38 58)))
    Fill-Soft $script:G $b $Water 8
    $script:G.SetClip($b)
    $lab = New-Path
    $lab.AddRectangle((New-Object System.Drawing.RectangleF(30, 72, 90, 30)))
    Fill-Path $script:G $lab (Col 30 80 200)
    $lab.Dispose()
    $script:G.ResetClip()
    $b.Dispose()
    # label wave and shine
    Stroke-Line $script:G 48 87 96 87 4 $White
    Stroke-Line $script:G 50 62 50 118 5 (Col 255 255 255 150)
}

function Draw-wine {
    $Glass = Col 215 232 255
    $Wine = Col 200 20 55
    # bowl
    $bowl = New-Path
    $bowl.AddBezier((P 38 8), (P 28 96), (P 116 96), (P 106 8))
    $bowl.CloseFigure()
    $script:G.SetClip($bowl)
    Fill-Path $script:G $bowl (Col 255 255 255 40)
    $w = New-Path
    $w.AddRectangle((New-Object System.Drawing.RectangleF(20, 44, 110, 70)))
    Fill-Path $script:G $w $Wine
    $w.Dispose()
    $script:G.ResetClip()
    $p = Pn $Glass 5
    $script:G.DrawPath($p, $bowl)
    $p.Dispose()
    $bowl.Dispose()
    # wine surface highlight
    Stroke-Line $script:G 46 44 98 44 4 (Col 255 110 130)
    # stem and foot
    Stroke-Line $script:G 72 76 72 122 6 $Glass
    Fill-Ellipse $script:G 72 128 62 14 $Glass
}

function Draw-juice {
    $Juice = Col 255 165 30
    $Glass = Col 215 232 255
    # straw behind the glass
    Stroke-Line $script:G 78 70 104 8 8 $Pink
    # glass with juice
    $g = New-Path
    $g.AddPolygon([System.Drawing.PointF[]]@((P 28 42), (P 110 42), (P 100 130), (P 38 130)))
    Fill-Path $script:G $g (Col 255 255 255 40)
    $script:G.SetClip($g)
    $j = New-Path
    $j.AddRectangle((New-Object System.Drawing.RectangleF(20, 56, 100, 80)))
    Fill-Path $script:G $j $Juice
    $j.Dispose()
    $t = New-Path
    $t.AddRectangle((New-Object System.Drawing.RectangleF(20, 56, 100, 16)))
    Fill-Path $script:G $t (Col 255 210 60)
    $t.Dispose()
    $script:G.ResetClip()
    $p = Pn $Glass 5
    $script:G.DrawPath($p, $g)
    $p.Dispose()
    $g.Dispose()
    Stroke-Line $script:G 42 78 47 114 5 (Col 255 255 255 150)
    # orange slice on the rim
    Fill-Circle $script:G 112 46 19 (Col 255 130 20)
    Fill-Circle $script:G 112 46 14 (Col 255 200 70)
    foreach ($a in @(0, 60, 120)) {
        $r = $a * [Math]::PI / 180
        Stroke-Line $script:G (112 - 13 * [Math]::Cos($r)) (46 - 13 * [Math]::Sin($r)) (112 + 13 * [Math]::Cos($r)) (46 + 13 * [Math]::Sin($r)) 3 (Col 255 130 20)
    }
}

function Draw-cocoa {
    $Mug = Col 235 45 45
    $Cocoa = Col 110 60 35
    # steam
    foreach ($x in @(44, 66, 88)) {
        $p = Pn $Grey 6
        $script:G.DrawBezier($p, (P $x 34), (P ($x - 10) 24), (P ($x + 10) 14), (P $x 4))
        $p.Dispose()
    }
    # handle
    Stroke-Arc $script:G 108 86 20 22 -90 180 11 $Mug
    # mug body
    $m = New-Path
    $m.AddLine((P 22 56), (P 108 56))
    $m.AddLine((P 108 56), (P 102 124))
    $m.AddBezier((P 102 124), (P 100 134), (P 30 134), (P 28 124))
    $m.CloseFigure()
    Fill-Soft $script:G $m $Mug 6
    $m.Dispose()
    # white band
    Food-Rect 25 88 80 14 $White
    # cocoa and marshmallows
    Fill-Ellipse $script:G 65 58 92 18 $Mug
    Fill-Ellipse $script:G 65 58 80 12 $Cocoa
    foreach ($m2 in @(@(46, 44), @(68, 40), @(88, 46))) {
        $r = New-RoundedRect ($m2[0] - 11) ($m2[1] - 10) 22 20 7
        Fill-Soft $script:G $r $White 2
        $r.Dispose()
    }
}

function Food-Slice($c) {
    $s = New-Path
    $s.AddRectangle((New-Object System.Drawing.RectangleF(-38, -6, 76, 54)))
    $s.AddEllipse([single]-46, [single]-46, [single]92, [single]54)
    Fill-Soft $script:G $s $c 8
    $s.Dispose()
}

function Draw-bread {
    $Crust = Col 205 125 70
    $Crumb = Col 255 228 150
    With-Transform $script:G 72 74 1.2 0 {
        Food-Slice $Crust
        With-Transform $script:G 0 3 0.78 0 { Food-Slice $Crumb }
    }
}

function Draw-egg {
    $pts = [System.Drawing.PointF[]]@((P 14 70), (P 28 38), (P 66 20), (P 108 28), (P 132 56), (P 126 96), (P 94 124), (P 52 122), (P 24 102))
    $ew = New-Path
    $ew.AddClosedCurve($pts, [single]0.6)
    Fill-Soft $script:G $ew $White 6
    $ew.Dispose()
    Fill-Circle $script:G 76 72 28 (Col 255 165 20)
    Fill-Ellipse $script:G 68 62 16 12 (Col 255 245 150)
}

function Draw-pudding {
    $Flan = Col 255 215 80
    $Caramel = Col 185 95 25
    # plate
    Fill-Ellipse $script:G 72 126 124 20 $White
    # flan body
    $b = New-Path
    $b.AddPolygon([System.Drawing.PointF[]]@((P 42 56), (P 102 56), (P 114 120), (P 30 120)))
    Fill-Soft $script:G $b $Flan 6
    $b.Dispose()
    # caramel cap with drips
    $cap = New-Path
    $cap.AddBezier((P 38 56), (P 38 26), (P 106 26), (P 106 56))
    $cap.AddBezier((P 106 56), (P 106 74), (P 94 74), (P 92 64))
    $cap.AddBezier((P 92 64), (P 90 84), (P 76 84), (P 76 66))
    $cap.AddBezier((P 76 66), (P 74 74), (P 58 74), (P 58 64))
    $cap.AddBezier((P 58 64), (P 56 80), (P 40 80), (P 40 64))
    $cap.CloseFigure()
    Fill-Soft $script:G $cap $Caramel 4
    $cap.Dispose()
    Fill-Ellipse $script:G 60 42 16 8 (Col 235 150 60)
    # cherry on top
    Fill-Circle $script:G 72 20 10 $Red
}

function Draw-candycane {
    $path = New-Path
    $path.AddLine((P 52 134), (P 52 56))
    $path.AddArc(52, 20, 44, 72, 180, 180)
    $path.AddLine((P 96 56), (P 96 64))
    With-Transform $script:G 72 76 0.92 12 {
        $script:G.TranslateTransform(-72, -76)
        $wide = $path.Clone()
        $wp = Pn $White 24
        $wide.Widen($wp)
        $wp.Dispose()
        $script:G.SetClip($wide)
        Fill-Path $script:G $wide $White
        for ($y = -20; $y -lt 150; $y += 26) {
            Stroke-Line $script:G 30 ($y + 20) 120 ($y - 14) 11 $Red
        }
        $script:G.ResetClip()
        $wide.Dispose()
    }
    $path.Dispose()
}

function Prop-Poly($pts, $c, $w) {
    $p = New-Path
    $arr = New-Object 'System.Collections.Generic.List[System.Drawing.PointF]'
    for ($i = 0; $i -lt $pts.Count; $i += 2) { $arr.Add((P $pts[$i] $pts[$i + 1])) }
    $p.AddPolygon($arr.ToArray())
    Fill-Soft $script:G $p $c $w
    $p.Dispose()
}
function Prop-RR($x, $y, $w, $h, $r, $c) {
    $p = New-RoundedRect $x $y $w $h $r
    Fill-Soft $script:G $p $c 2
    $p.Dispose()
}
function Prop-Wisp($x, $y0, $y1, $amp, $w, $c) {
    # s-shaped wisp rising from y0 to y1
    $m = ($y0 + $y1) / 2
    $p = New-Path
    $p.AddBezier((P $x $y0), (P ($x - $amp) ($y0 - ($y0 - $m) * 0.6)), (P ($x - $amp) ($m + ($y0 - $m) * 0.3)), (P $x $m))
    $p.AddBezier((P $x $m), (P ($x + $amp) ($m - ($m - $y1) * 0.3)), (P ($x + $amp) ($y1 + ($m - $y1) * 0.4)), (P $x $y1))
    $pn = Pn $c $w
    $script:G.DrawPath($pn, $p)
    $pn.Dispose()
    $p.Dispose()
}

function Draw-bed {
    $Wood = Col 225 165 95
    $Sheet = Col 255 226 150
    $Fold = Col 255 120 165
    # headboard
    Prop-RR 30 10 84 50 6 $Wood
    # mattress, wider at the foot
    Prop-Poly @(28, 44, 116, 44, 134, 130, 10, 130) $Sheet 6
    # pink blanket band at the foot
    Prop-Poly @(18, 92, 126, 92, 134, 130, 10, 130) $Fold 6
    # pillow
    Prop-RR 42 30 60 30 12 $Pink
}

function Draw-couch {
    $Brown = Col 215 130 70
    $Light = Col 245 175 105
    $Deep = Col 165 90 45
    # back with two cushions
    Prop-RR 24 12 96 64 22 $Brown
    Prop-RR 32 20 38 50 10 $Light
    Prop-RR 74 20 38 50 10 $Light
    # feet
    Prop-RR 24 120 14 14 3 $Black
    Prop-RR 106 120 14 14 3 $Black
    # seat cushions
    Prop-RR 20 72 52 38 10 $Light
    Prop-RR 72 72 52 38 10 $Light
    Stroke-Line $script:G 72 76 72 108 4 $Deep
    # base and arms
    Prop-RR 14 104 116 20 6 $Deep
    Prop-RR 6 46 28 70 14 $Brown
    Prop-RR 110 46 28 70 14 $Brown
}

function Draw-gamerchair {
    $Shell = Col 240 80 130
    $Seat = Col 255 130 170
    $Cush = Col 255 175 205
    $legs = @(@(72, 114, 18, 124), @(72, 114, 126, 124), @(72, 114, 46, 132), @(72, 114, 98, 132))
    # five-star base
    foreach ($a in $legs) { Stroke-Line $script:G $a[0] $a[1] $a[2] $a[3] 11 $Edge }
    foreach ($a in $legs) { Stroke-Line $script:G $a[0] $a[1] $a[2] $a[3] 6 $Black }
    # casters
    foreach ($c in @(@(16, 128), @(128, 128), @(44, 137), @(100, 137))) {
        Fill-Circle $script:G $c[0] $c[1] 7 $Edge
        Fill-Circle $script:G $c[0] $c[1] 4.5 $Black
    }
    # centre stem
    Stroke-Line $script:G 72 96 72 116 13 $Edge
    Stroke-Line $script:G 72 96 72 116 8 $Black
    # armrests
    foreach ($x in @(14, 130)) {
        Stroke-Line $script:G $x 66 $x 90 11 $Edge
        Stroke-Line $script:G $x 66 $x 90 6 $Black
    }
    Prop-RR 3 56 22 12 5 $Cush
    Prop-RR 119 56 22 12 5 $Cush
    # tall racing back with headrest
    $b = New-Path
    $b.AddBezier((P 36 84), (P 26 56), (P 34 32), (P 48 26))
    $b.AddBezier((P 48 26), (P 50 2), (P 94 2), (P 96 26))
    $b.AddBezier((P 96 26), (P 110 32), (P 118 56), (P 108 84))
    $b.CloseFigure()
    Fill-Soft $script:G $b $Shell 4
    $b.Dispose()
    Prop-RR 53 10 38 18 7 $Cush
    Stroke-Line $script:G 48 40 52 72 6 $White
    Stroke-Line $script:G 96 40 92 72 6 $White
    # seat
    Prop-RR 24 80 96 22 11 $Seat
}
function Draw-table {
    $Top = Col 255 195 120
    $Front = Col 200 125 65
    # perspective top, front apron, legs
    Prop-RR 22 70 10 62 3 $Front
    Prop-RR 112 70 10 62 3 $Front
    Prop-Poly @(34, 26, 110, 26, 138, 62, 6, 62) $Top 5
    Prop-RR 8 60 128 18 4 $Front
    Stroke-Line $script:G 12 66 132 66 3 $Top
}

function Draw-tablet {
    $Body = Col 140 150 255
    $Screen = Col 40 50 110
    # stand legs
    # body, side button and screen
    Prop-RR 8 14 128 116 14 $Body
    Fill-Circle $script:G 126 72 4 $White
    $s = New-RoundedRect 18 24 96 96 6
    Fill-Edged $script:G $s $Screen 2 $White 1.5
    $s.Dispose()
    # a drawn curve on the screen
    Stroke-Arc $script:G 50 92 22 22 200 150 6 $Pink
    # stylus
    With-Transform $script:G 78 62 1 40 {
        Prop-RR (-5) (-30) 10 52 4 $White
        $t = New-Path
        $t.AddPolygon([System.Drawing.PointF[]]@((P (-5) 22), (P 5 22), (P 0 34)))
        Fill-Soft $script:G $t $Gold 2
        $t.Dispose()
    }
}

function Draw-hammer {
    # rubber mallet, head dark with light edge, yellow handle
    With-Transform $script:G 68 70 1.02 35 {
        Prop-RR (-7) (-6) 14 74 5 $Gold
        $h = New-RoundedRect (-46) (-40) 92 44 12
        Fill-Edged $script:G $h $Black 3 $Edge 2.5
        $h.Dispose()
        Prop-RR (-34) (-32) 28 8 4 $Grey
        Prop-RR (-4) (-32) 12 8 4 $Grey
        Prop-RR (-8) (-52) 16 12 5 $Gold
    }
}

function Draw-keys {
    $Silver = Col 210 215 230
    $Leaf = Col 90 190 120
    # key ring
    Stroke-Circle $script:G 78 34 24 7 $Pink
    # key 1 (left, tilted)
    With-Transform $script:G 40 78 1 22 {
        Stroke-Line $script:G 0 4 0 56 10 $Silver
        Stroke-Line $script:G 0 44 12 44 8 $Silver
        Stroke-Line $script:G 0 56 12 56 8 $Silver
        Fill-Circle $script:G 0 (-14) 20 $Silver
        Fill-Circle $script:G 0 (-14) 7 $Dark
    }
    # leaf charm
    Prop-Poly @(70, 60, 98, 56, 108, 80, 90, 100, 72, 84) $Leaf 6
    # flower charm
    foreach ($a in 0..4) {
        $r = ($a * 72 - 90) * [Math]::PI / 180
        Fill-Circle $script:G (104 + 15 * [Math]::Cos($r)) (98 + 15 * [Math]::Sin($r)) 11 $Pink
    }
    Fill-Circle $script:G 104 98 10 $Yellow
    Stroke-Line $script:G 92 50 98 62 3 $White
}

function Draw-xmaslights {
    $cols = @($Red, $Green, $Gold, $Blue, $Red)
    $xs = @(16, 44, 72, 100, 128)
    # wire
    $pts = New-Object 'System.Collections.Generic.List[System.Drawing.PointF]'
    for ($x = 4; $x -le 140; $x += 4) { $pts.Add((P $x (26 + 12 * [Math]::Sin(($x - 4) / 136.0 * 2 * [Math]::PI * 1.0)))) }
    $wp = Pn (Col 160 175 170) 5
    $script:G.DrawLines($wp, $pts.ToArray())
    $wp.Dispose()
    for ($i = 0; $i -lt 5; $i++) {
        $x = $xs[$i]
        $y = 26 + 12 *[Math]::Sin(($x - 4) / 136.0 * 2 * [Math]::PI)
        # bulb, then socket on top
        Fill-Ellipse $script:G $x ($y + 42) 30 56 $cols[$i]
        Fill-Ellipse $script:G ($x - 6) ($y + 34) 8 18 (Col 255 255 255 170)
        $s = New-RoundedRect ($x - 8) ($y - 2) 16 16 4
        Fill-Edged $script:G $s $Black 2 $Edge 2
        $s.Dispose()
    }
}

function Draw-headpat {
    $Skin = Col 255 205 175
    $Shade = Col 225 150 120
    # hand from the side, palm down and cupped: wrist from the left, fingers curve down at the right, thumb below
    $p = New-Path
    $p.AddLine((P 6 50), (P 40 46))
    $p.AddBezier((P 40 46), (P 74 36), (P 108 44), (P 124 68))
    $p.AddBezier((P 124 68), (P 134 84), (P 132 102), (P 122 104))
    $p.AddBezier((P 122 104), (P 112 106), (P 110 94), (P 102 86))
    $p.AddBezier((P 102 86), (P 94 80), (P 86 82), (P 78 84))
    $p.AddBezier((P 78 84), (P 82 98), (P 76 114), (P 64 114))
    $p.AddBezier((P 64 114), (P 52 114), (P 50 98), (P 44 88))
    $p.AddLine((P 44 88), (P 6 88))
    $p.CloseFigure()
    Fill-Soft $script:G $p $Skin 5
    $p.Dispose()
    # finger lines along the curve
    $pen = Pn $Shade 3.5
    $script:G.DrawBezier($pen, (P 86 56), (P 100 60), (P 110 72), (P 116 90))
    $script:G.DrawBezier($pen, (P 82 68), (P 92 70), (P 100 76), (P 106 86))
    $pen.Dispose()
    # motion marks above the hand
    Stroke-Arc $script:G 78 44 40 22 215 110 6 $Pink
    Stroke-Arc $script:G 78 44 58 36 225 90 6 $Pink
    # pat marks below the fingers
    Stroke-Line $script:G 96 118 92 132 5 $Pink
    Stroke-Line $script:G 114 120 116 134 5 $Pink
    Stroke-Line $script:G 132 116 138 128 5 $Pink
}
function Draw-steam {
    $Steam = Col 215 230 255
    Prop-Wisp 36 134 40 12 11 $Steam
    Prop-Wisp 72 126 8 14 12 $Steam
    Prop-Wisp 108 134 40 12 11 $Steam
}

function Wear-Mirror([scriptblock]$body) {
    $state = $script:G.Save()
    $script:G.TranslateTransform(144, 0)
    $script:G.ScaleTransform(-1, 1)
    & $body
    $script:G.Restore($state)
}

# bow loop pointing left from the knot at (cx,cy); mirrored by the caller
function Wear-BowLoop($cx, $cy, $c, $fold) {
    $l = New-Path
    $l.AddBezier((P ($cx - 2) $cy), (P ($cx - 20) ($cy - 34)), (P ($cx - 54) ($cy - 42)), (P ($cx - 64) ($cy - 26)))
    $l.AddBezier((P ($cx - 64) ($cy - 26)), (P ($cx - 74) ($cy - 8)), (P ($cx - 72) ($cy + 16)), (P ($cx - 62) ($cy + 28)))
    $l.AddBezier((P ($cx - 62) ($cy + 28)), (P ($cx - 50) ($cy + 38)), (P ($cx - 26) ($cy + 22)), (P ($cx - 2) ($cy + 4)))
    $l.CloseFigure()
    Fill-Soft $script:G $l $c 4
    $l.Dispose()
    $p = Pn $fold 4
    $script:G.DrawBezier($p, (P ($cx - 8) ($cy + 2)), (P ($cx - 30) ($cy - 6)), (P ($cx - 46) ($cy - 8)), (P ($cx - 58) ($cy - 4)))
    $p.Dispose()
}

function Wear-Flower($cx, $cy, $r, $petal, $mid) {
    for ($i = 0; $i -lt 5; $i++) {
        $a = (-90 + $i * 72) * [Math]::PI / 180
        Fill-Circle $script:G ($cx + $r * 0.62 * [Math]::Cos($a)) ($cy + $r * 0.62 * [Math]::Sin($a)) ($r * 0.5) $petal
    }
    Fill-Circle $script:G $cx $cy ($r * 0.34) $mid
}

function Draw-swimring {
    $Ring = Col 255 95 160
    $RingDark = Col 205 50 120
    $Hole = Col 32 32 32
    # thickness under the ring
    Fill-Ellipse $script:G 72 82 130 90 $RingDark
    # top of the ring
    Fill-Ellipse $script:G 72 70 130 92 $Ring
    # white stripes cut to the ring
    $clip = New-Path
    $clip.AddEllipse([single]7, [single]24, [single]130, [single]92)
    $script:G.SetClip($clip)
    foreach ($a in @(45, 135)) {
        With-Transform $script:G 72 70 1 $a {
            $b = Br $White
            $script:G.FillRectangle($b, -8, -90, 16, 180)
            $b.Dispose()
        }
    }
    $script:G.ResetClip()
    $clip.Dispose()
    # hole
    Fill-Ellipse $script:G 72 66 62 34 $RingDark
    Fill-Ellipse $script:G 72 64 54 26 $Hole
    # shine
    Stroke-Arc $script:G 72 70 52 32 195 60 6 (Col 255 210 230)
}

function Draw-heartglasses {
    $Frame = Col 255 120 165
    $Lens = Col 120 45 150
    # bridge and short temples first
    Stroke-Line $script:G 52 56 92 56 7 (Col 190 195 210)
    Stroke-Line $script:G 8 56 2 40 6 (Col 190 195 210)
    Stroke-Line $script:G 136 56 142 40 6 (Col 190 195 210)
    foreach ($h in @(@(38, 62, -8), @(106, 62, 8))) {
        With-Transform $script:G $h[0] $h[1] 33 $h[2] { Unit-Heart $Frame }
        With-Transform $script:G $h[0] ($h[1] + 1) 24 $h[2] { Unit-Heart $Lens }
        With-Transform $script:G $h[0] $h[1] 1 $h[2] {
            Stroke-Line $script:G (-22) (-8) (-12) (-20) 5 (Col 255 190 215)
        }
    }
}

function Draw-catears {
    $Fur = Col 52 52 66
    $In = Col 255 130 175
    foreach ($m in @($false, $true)) {
        $state = $script:G.Save()
        if ($m) { $script:G.TranslateTransform(144, 0); $script:G.ScaleTransform(-1, 1) }
        # outer ear: straight sides, pointed tip leaning outwards
        $o = New-Path
        $o.AddPolygon([System.Drawing.PointF[]]@((P 22 14), (P 8 118), (P 66 104)))
        Fill-Edged $script:G $o $Fur 6 $Edge 3
        $o.Dispose()
        # pink inner ear
        $i = New-Path
        $i.AddPolygon([System.Drawing.PointF[]]@((P 25 46), (P 20 100), (P 52 94)))
        Fill-Soft $script:G $i $In 5
        $i.Dispose()
        $script:G.Restore($state)
    }
}

function Draw-moustache {
    $Hair = Col 72 72 90
    # handlebar: thick belly, tips curling up
    $h = New-Path
    $h.AddBezier((P 72 58), (P 62 40), (P 36 40), (P 24 56))
    $h.AddBezier((P 24 56), (P 16 64), (P 8 60), (P 6 44))
    $h.AddBezier((P 6 44), (P 6 78), (P 30 108), (P 56 94))
    $h.AddBezier((P 56 94), (P 64 90), (P 72 84), (P 72 80))
    $h.AddBezier((P 72 80), (P 72 84), (P 80 90), (P 88 94))
    $h.AddBezier((P 88 94), (P 114 108), (P 138 78), (P 138 44))
    $h.AddBezier((P 138 44), (P 136 60), (P 128 64), (P 120 56))
    $h.AddBezier((P 120 56), (P 108 40), (P 82 40), (P 72 58))
    $h.CloseFigure()
    Fill-Edged $script:G $h $Hair 4 $Edge 2.5
    $h.Dispose()
}
function Draw-eyepatch {
    # thin strap with a light edge, diagonal across the box
    Stroke-Line $script:G 4 24 140 112 13 $Edge
    Stroke-Line $script:G 4 24 140 112 6 $Black
    # black shield patch with light outline
    With-Transform $script:G 76 68 0.88 12 {
        $o = New-Path
        $o.AddBezier((P 0 (-42)), (P 40 (-42)), (P 52 (-10)), (P 42 18))
        $o.AddBezier((P 42 18), (P 34 42), (P 14 48), (P 0 48))
        $o.AddBezier((P 0 48), (P (-14) 48), (P (-34) 42), (P (-42) 18))
        $o.AddBezier((P (-42) 18), (P (-52) (-10)), (P (-40) (-42)), (P 0 (-42)))
        $o.CloseFigure()
        Fill-Edged $script:G $o $Black 4 $Edge 3
        $o.Dispose()
    }
}

function Draw-headband {
    $Hair = Col 150 96 62
    $Skin = Col 255 214 186
    $Band = Col 255 120 165
    $Blue2 = Col 60 140 255
    # head with hair, the striped sweatband across the forehead
    Fill-Circle $script:G 72 74 60 $Hair
    Fill-Ellipse $script:G 72 90 92 84 $Skin
    $g = $script:G
    $i = 0
    foreach ($c in @($Band, $White, $Blue2)) {
        $y = 40 + $i * 10
        $pen = Pn $c 11
        $pen.StartCap = 'Flat'; $pen.EndCap = 'Flat'
        $g.DrawBezier($pen, (P 10 ($y + 2)), (P 44 ($y + 10)), (P 100 ($y + 10)), (P 134 ($y + 2)))
        $pen.Dispose()
        $i++
    }
    # eyes and smile
    Fill-Ellipse $script:G 52 96 10 14 $Dark
    Fill-Ellipse $script:G 92 96 10 14 $Dark
    Stroke-Arc $script:G 72 108 12 8 20 140 4 $Dark
}

function Draw-helmet {
    $Hat = Col 255 222 40
    $Brim = Col 240 175 20
    $Gr = Col 40 190 70
    $d = New-Path
    $d.AddBezier((P 20 100), (P 12 6), (P 132 6), (P 124 100))
    $d.CloseFigure()
    Fill-Soft $script:G $d $Hat 4
    # green marks cut to the dome
    $script:G.SetClip($d)
    $b = Br $Gr
    $script:G.FillRectangle($b, 60, 22, 24, 62)
    $script:G.FillRectangle($b, 44, 40, 56, 24)
    $script:G.FillRectangle($b, 10, 58, 28, 16)
    $script:G.FillRectangle($b, 106, 58, 28, 16)
    $b.Dispose()
    $script:G.ResetClip()
    $d.Dispose()
    # brim
    $r = New-RoundedRect 8 92 128 34 12
    Fill-Soft $script:G $r $Brim 4
    $r.Dispose()
}

function Draw-clown {
    $Hair = Col 255 140 40
    $Skin = Col 255 245 240
    # curly hair tufts behind the face
    foreach ($d in @(-1, 1)) {
        foreach ($c in @(@(52, 58, 17), @(60, 78, 16), @(50, 98, 15))) {
            Fill-Circle $script:G (72 + $d * $c[0]) $c[1] $c[2] $Hair
        }
    }
    # face
    Fill-Ellipse $script:G 72 84 104 96 (Col 190 195 215)
    Fill-Ellipse $script:G 72 84 100 92 $Skin
    # party hat with pompom
    $h = New-Path
    $h.AddPolygon([System.Drawing.PointF[]]@((P 50 42), (P 94 42), (P 72 6)))
    Fill-Soft $script:G $h $Blue 4
    $h.Dispose()
    Stroke-Line $script:G 60 30 82 30 5 $Yellow
    Fill-Circle $script:G 72 8 7 $Yellow
    # eyes with brows
    foreach ($d in @(-1, 1)) {
        Fill-Ellipse $script:G (72 + $d * 24) 70 16 20 $Dark
        Fill-Ellipse $script:G (72 + $d * 24 - 2) 66 5 6 $White
        Stroke-Arc $script:G (72 + $d * 24) 60 12 8 200 140 4 $Red
        Fill-Ellipse $script:G (72 + $d * 36) 90 16 10 (Col 255 150 175)
    }
    # wide smile and big nose
    Stroke-Arc $script:G 72 92 34 24 15 150 9 $Red
    Fill-Circle $script:G 72 86 15 $Red
    Fill-Ellipse $script:G 67 80 8 6 (Col 255 190 190)
}

function Draw-pacifier {
    $Blue = Col 60 150 255
    $Deep = Col 25 95 215
    $Ring = Col 225 215 255
    $Nip = Col 255 228 150
    $Hole = Col 32 32 32
    # ring behind the nipple
    $r = New-RoundedRect 48 76 48 56 22
    $pn = Pn $Ring 8
    $script:G.DrawPath($pn, $r)
    $pn.Dispose()
    $r.Dispose()
    # bean shaped shield
    $s = New-Path
    $s.AddBezier((P 10 60), (P 8 28), (P 40 20), (P 58 38))
    $s.AddBezier((P 58 38), (P 66 46), (P 78 46), (P 86 38))
    $s.AddBezier((P 86 38), (P 104 20), (P 136 28), (P 134 60))
    $s.AddBezier((P 134 60), (P 132 96), (P 100 100), (P 72 100))
    $s.AddBezier((P 72 100), (P 44 100), (P 12 96), (P 10 60))
    $s.CloseFigure()
    Fill-Soft $script:G $s $Blue 4
    $s.Dispose()
    Fill-Circle $script:G 36 58 11 $Deep
    Fill-Circle $script:G 108 58 11 $Deep
    Fill-Circle $script:G 36 58 7 $Hole
    Fill-Circle $script:G 108 58 7 $Hole
    # nipple
    Fill-Ellipse $script:G 72 84 40 46 $Nip
    Fill-Ellipse $script:G 64 74 10 16 (Col 255 250 220)
}

function Draw-bandaid {
    $Tan = Col 245 185 110
    $Pad = Col 255 232 190
    $Dot = Col 225 160 95
    With-Transform $script:G 72 72 1 (-35) {
        $b = New-RoundedRect (-66) (-22) 132 44 22
        Fill-Soft $script:G $b $Tan 4
        $b.Dispose()
        $p = New-RoundedRect (-24) (-22) 48 44 3
        Fill-Soft $script:G $p $Pad 2
        $p.Dispose()
        foreach ($d in @(@(-12, -10), @(2, -12), @(14, -6), @(-6, 4), @(8, 8), @(-16, 10), @(20, 10))) {
            Fill-Circle $script:G $d[0] $d[1] 3.6 $Dot
        }
    }
}

function Draw-crown {
    $Gold2 = Col 240 165 20
    $Light = Col 255 228 120
    # body with five points
    $c = New-Path
    $c.AddPolygon([System.Drawing.PointF[]]@((P 18 104), (P 16 40), (P 30 76), (P 44 30), (P 58 76), (P 72 18), (P 86 76), (P 100 30), (P 114 76), (P 128 40), (P 126 104)))
    Fill-Soft $script:G $c $Gold 8
    $c.Dispose()
    # round tips
    foreach ($t in @(@(16, 38), @(44, 28), @(72, 16), @(100, 28), @(128, 38))) { Fill-Circle $script:G $t[0] $t[1] 9 $Light }
    # band with gems
    $b = New-RoundedRect 12 92 120 36 10
    Fill-Soft $script:G $b $Gold2 4
    $b.Dispose()
    Fill-Circle $script:G 36 110 9 $Red
    Fill-Circle $script:G 72 110 11 (Col 70 150 255)
    Fill-Circle $script:G 108 110 9 $Red
}

function Draw-earring {
    # pair of gold hoops, each with a gem at the bottom
    foreach ($e in @(@(40, 98, (Col 100 215 60), (Col 50 150 40)), @(104, 98, (Col 255 100 170), (Col 200 50 120)))) {
        Stroke-Circle $script:G $e[0] 50 28 9 $Gold
        Stroke-Arc $script:G $e[0] 50 24 24 200 50 3 (Col 255 235 150)
        $g = New-Path
        $g.AddPolygon([System.Drawing.PointF[]]@((P $e[0] 74), (P ($e[0] + 16) 98), (P $e[0] 132), (P ($e[0] - 16) 98)))
        Fill-Soft $script:G $g $e[2] 4
        $g.Dispose()
        $f = New-Path
        $f.AddPolygon([System.Drawing.PointF[]]@((P $e[0] 74), (P ($e[0] + 16) 98), (P $e[0] 132)))
        Fill-Soft $script:G $f $e[3] 2
        $f.Dispose()
    }
}

function Draw-bow {
    $Rb = Col 255 150 195
    $Fold = Col 225 100 150
    $Bell = Col 255 195 40
    # tails
    foreach ($d in @(-1, 1)) {
        $t = New-Path
        $t.AddPolygon([System.Drawing.PointF[]]@((P (72 + $d * 4) 54), (P (72 + $d * 34) 112), (P (72 + $d * 22) 120), (P (72 + $d * 10) 112)))
        Fill-Soft $script:G $t $Rb 4
        $t.Dispose()
    }
    # loops
    Wear-BowLoop 72 50 $Rb $Fold
    Wear-Mirror { Wear-BowLoop 72 50 $Rb $Fold }
    # knot
    $k = New-RoundedRect 58 34 28 34 9
    Fill-Soft $script:G $k (Col 255 120 175) 3
    $k.Dispose()
    # bell
    Fill-Circle $script:G 72 106 16 $Bell
    Stroke-Line $script:G 60 108 84 108 3.5 (Col 200 130 20)
    Fill-Circle $script:G 72 114 3.5 (Col 200 130 20)
    Fill-Ellipse $script:G 66 98 8 5 (Col 255 240 170)
}

function Draw-miku {
    $Teal = Col 57 197 187
    $TealD = Col 30 150 145
    $Skin = Col 255 230 215
    $Pk = Col 255 80 160
    # twin tails: narrow at the tie, flaring outwards, tip at the bottom
    foreach ($m in @($false, $true)) {
        $state = $script:G.Save()
        if ($m) { $script:G.TranslateTransform(144, 0); $script:G.ScaleTransform(-1, 1) }
        $t = New-Path
        $t.AddBezier((P 26 40), (P 2 62), (P 0 100), (P 8 122))
        $t.AddBezier((P 8 122), (P 12 132), (P 16 138), (P 18 140))
        $t.AddBezier((P 18 140), (P 30 128), (P 48 112), (P 46 90))
        $t.AddBezier((P 46 90), (P 44 68), (P 42 54), (P 40 40))
        $t.CloseFigure()
        Fill-Soft $script:G $t $Teal 4
        $t.Dispose()
        Stroke-Line $script:G 24 70 18 110 4 $TealD
        $script:G.Restore($state)
    }
    # head: hair dome, face, bangs
    Fill-Ellipse $script:G 72 44 86 76 $Teal
    Fill-Ellipse $script:G 72 72 48 46 $Skin
    $bg = New-Path
    $bg.AddPolygon([System.Drawing.PointF[]]@((P 44 36), (P 100 36), (P 96 66), (P 86 56), (P 72 70), (P 58 56), (P 48 66)))
    Fill-Soft $script:G $bg $Teal 4
    $bg.Dispose()
    Fill-Ellipse $script:G 62 78 6 9 $TealD
    Fill-Ellipse $script:G 82 78 6 9 $TealD
    # square black and pink hair ties
    foreach ($x in @(34, 110)) {
        $r = New-RoundedRect ($x - 13) 32 26 26 4
        Fill-Edged $script:G $r $Black 3 $Edge 2.5
        $r.Dispose()
        $s = New-Path
        $s.AddRectangle((New-Object System.Drawing.RectangleF(([single]($x - 11)), [single]42, [single]22, [single]6)))
        Fill-Path $script:G $s $Pk
        $s.Dispose()
    }
}

$names = 'cash', 'heart', 'angry', 'angryshy', 'shy', 'star', 'tears', 'pleading', 'sulking', 'swirly', 'xd', 'avoid',
         'mouth3', 'pout', 'sweat', 'sweatdrops', 'nosebubble', 'sleepbubble', 'sleep', 'loading', 'speech', 'eating', 'fish', 'white3', 'controller', 'microphone', 'pen', 'outfit', 'heels', 'school', 'foxears', 'bunnyears', 'deviltail', 'horns', 'undies', 'bikini', 'bikiniwhite', 'bikiniblack', 'swimsuit', 'swimsuitwhite', 'swimsuitblack', 'outfitred', 'outfitblack', 'tshirt', 'hoodie', 'suit', 'trunks', 'halo', 'hat', 'coffee', 'boba', 'sunglasses', 'baseballcap', 'blanket', 'dragontail', 'foxtail', 'horsetail', 'liontail', 'longhairtail', 'mermaidtail', 'ninetail', 'raccoontail', 'squirreltail', 'thicktail', 'sharktail', 'cattail', 'nightcap', 'fairywings', 'featherwings', 'impwings', 'littlewings', 'membranedwings', 'petitwings', 'can', 'water', 'wine', 'juice', 'cocoa', 'bread', 'egg', 'pudding', 'candycane', 'bed', 'couch', 'gamerchair', 'table', 'tablet', 'hammer', 'keys', 'xmaslights', 'headpat', 'steam', 'swimring', 'heartglasses', 'catears', 'moustache', 'eyepatch', 'headband', 'helmet', 'clown', 'pacifier', 'bandaid', 'crown', 'earring', 'bow', 'miku', 'connected', 'disconnected', 'locked'

$root = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $root 'src\LoupixDeck.Plugin.VTubeStudio\Icons'
New-Item -ItemType Directory -Force $outDir | Out-Null

$bitmaps = @{}
foreach ($name in $names) {
    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $script:G = [System.Drawing.Graphics]::FromImage($bmp)
    $script:G.SmoothingMode = 'AntiAlias'
    $script:G.PixelOffsetMode = 'HighQuality'
    $script:G.Clear([System.Drawing.Color]::Transparent)
    & "Draw-$name"
    $script:G.Dispose()
    $bmp.Save((Join-Path $outDir "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmaps[$name] = $bmp
}

# contact sheet: 7 columns, 96 px icons, name below
$cols = 7; $cellW = 140; $cellH = 136; $icon = 96
$sheet = New-Object System.Drawing.Bitmap(($cols * $cellW), ([Math]::Ceiling($names.Count / $cols) * $cellH), [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$sg = [System.Drawing.Graphics]::FromImage($sheet)
$sg.SmoothingMode = 'AntiAlias'
$sg.InterpolationMode = 'HighQualityBicubic'
$sg.TextRenderingHint = 'AntiAlias'
$sg.Clear((Col 32 32 32))
$font = New-Object System.Drawing.Font('Segoe UI', 9)
$fmt = New-Object System.Drawing.StringFormat
$fmt.Alignment = 'Center'
$textBrush = Br $White
for ($i = 0; $i -lt $names.Count; $i++) {
    $x = ($i % $cols) * $cellW
    $y = [Math]::Floor($i / $cols) * $cellH
    $sg.DrawImage($bitmaps[$names[$i]], [single]($x + ($cellW - $icon) / 2), [single]($y + 8), [single]$icon, [single]$icon)
    $sg.DrawString($names[$i], $font, $textBrush, (New-Object System.Drawing.RectangleF([single]$x, [single]($y + $icon + 14), [single]$cellW, [single]20)), $fmt)
}
$sg.Dispose()
$sheet.Save((Join-Path $PSScriptRoot 'emote-icons-sheet.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$sheet.Dispose()
foreach ($b in $bitmaps.Values) { $b.Dispose() }
