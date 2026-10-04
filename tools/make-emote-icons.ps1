# Draws the expression button icons: src/.../Icons/<name>.png (144x144, transparent, 2x of the 72 px draw size)
# and the contact sheet tools/emote-icons-sheet.png (38 icons at 96 px on a dark background).
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

function Draw-outfit {
    $Line = Col 200 204 212
    # t-shirt with a round neck
    $t = New-Path
    $t.AddLine((P 56 8), (P 30 20))
    $t.AddLine((P 30 20), (P 10 44))
    $t.AddLine((P 10 44), (P 28 56))
    $t.AddLine((P 28 56), (P 38 46))
    $t.AddLine((P 38 46), (P 38 68))
    $t.AddLine((P 38 68), (P 106 68))
    $t.AddLine((P 106 68), (P 106 46))
    $t.AddLine((P 106 46), (P 116 56))
    $t.AddLine((P 116 56), (P 134 44))
    $t.AddLine((P 134 44), (P 114 20))
    $t.AddLine((P 114 20), (P 88 8))
    $t.AddBezier((P 88 8), (P 84 28), (P 60 28), (P 56 8))
    $t.CloseFigure()
    Fill-Soft $script:G $t $White 4
    $t.Dispose()
    Stroke-Line $script:G 40 62 104 62 3 $Line
    # shorts, a small gap below
    $s = New-Path
    $s.AddLine((P 38 82), (P 106 82))
    $s.AddLine((P 106 82), (P 114 134))
    $s.AddLine((P 114 134), (P 78 134))
    $s.AddLine((P 78 134), (P 72 106))
    $s.AddLine((P 72 106), (P 66 134))
    $s.AddLine((P 66 134), (P 30 134))
    $s.CloseFigure()
    Fill-Soft $script:G $s $White 4
    $s.Dispose()
    Stroke-Line $script:G 40 90 104 90 3 $Line
}

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
    # white short-sleeved top
    $t = New-Path
    $t.AddLine((P 54 8), (P 28 20))
    $t.AddLine((P 28 20), (P 6 48))
    $t.AddLine((P 6 48), (P 26 60))
    $t.AddLine((P 26 60), (P 36 50))
    $t.AddLine((P 36 50), (P 36 136))
    $t.AddLine((P 36 136), (P 108 136))
    $t.AddLine((P 108 136), (P 108 50))
    $t.AddLine((P 108 50), (P 118 60))
    $t.AddLine((P 118 60), (P 138 48))
    $t.AddLine((P 138 48), (P 116 20))
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
    # contour of a standing figure, drawn as the left half and mirrored; legs end at knee height
    Stroke-Circle $script:G 72 16 11 6 $White
    foreach ($mirror in @($false, $true)) {
        $state = $script:G.Save()
        if ($mirror) { $script:G.TranslateTransform(144, 0); $script:G.ScaleTransform(-1, 1) }
        $p = Pn $White 6
        $g = $script:G
        $g.DrawLine($p, [single]66, [single]28, [single]66, [single]36)
        $g.DrawBezier($p, (P 66 36), (P 56 38), (P 44 38), (P 38 44))
        $g.DrawBezier($p, (P 38 44), (P 42 62), (P 52 70), (P 54 84))
        $g.DrawBezier($p, (P 54 84), (P 54 96), (P 42 102), (P 42 116))
        $g.DrawLine($p, [single]42, [single]116, [single]50, [single]140)
        $g.DrawBezier($p, (P 72 112), (P 70 122), (P 66 130), (P 64 140))
        $p.Dispose()
        $script:G.Restore($state)
    }
}

function Draw-bikini {
    $Suit = Col 46 196 182
    # strings first (white), the pieces on top
    Stroke-Line $script:G 30 44 56 8 4 $White
    Stroke-Line $script:G 114 44 88 8 4 $White
    Stroke-Line $script:G 22 64 4 70 4 $White
    Stroke-Line $script:G 122 64 140 70 4 $White
    Stroke-Line $script:G 60 54 84 54 4 $White
    Stroke-Line $script:G 24 98 4 90 4 $White
    Stroke-Line $script:G 120 98 140 90 4 $White
    foreach ($d in @(-1, 1)) {
        $c = New-Path
        $c.AddPolygon([System.Drawing.PointF[]]@((P (72 + $d * 6) 36), (P (72 + $d * 52) 36), (P (72 + $d * 34) 78)))
        Fill-Soft $script:G $c $Suit 8
        $c.Dispose()
    }
    $b = New-Path
    $b.AddPolygon([System.Drawing.PointF[]]@((P 24 94), (P 120 94), (P 72 134)))
    Fill-Soft $script:G $b $Suit 8
    $b.Dispose()
}

# ---------- run ----------
$names = 'cash', 'heart', 'angry', 'angryshy', 'shy', 'star', 'tears', 'pleading', 'sulking', 'swirly', 'xd', 'avoid',
         'mouth3', 'pout', 'sweat', 'sweatdrops', 'nosebubble', 'sleepbubble', 'sleep', 'loading', 'speech', 'eating', 'fish', 'white3', 'controller', 'microphone', 'pen', 'outfit', 'heels', 'school', 'foxears', 'bunnyears', 'deviltail', 'horns', 'undies', 'bikini', 'connected', 'disconnected', 'locked'

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
