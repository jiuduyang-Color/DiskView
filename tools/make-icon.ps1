# 生成应用图标 Assets\app.ico（多尺寸 PNG 封装）
Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$outDir = Join-Path $root 'assets'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function New-RoundedPath([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

$sizes = @(256, 128, 64, 48, 32, 24, 16)
$pngs = @()

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $pad = [single]($s * 0.035)
    $bg = New-RoundedPath $pad $pad ($s - 2 * $pad) ($s - 2 * $pad) ([single]($s * 0.22))
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF(0, 0)),
        (New-Object System.Drawing.PointF($s, $s)),
        [System.Drawing.Color]::FromArgb(255, 66, 133, 244),
        [System.Drawing.Color]::FromArgb(255, 26, 74, 196))
    $g.FillPath($grad, $bg)

    # 白色矩形树图图案
    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(242, 255, 255, 255))
    $x0 = [single]($s * 0.19)
    $y0 = [single]($s * 0.21)
    $w = [single]($s * 0.62)
    $h = [single]($s * 0.58)
    $gap = [single]($s * 0.035)
    $rad = [single]($s * 0.03)

    $leftW = [single]($w * 0.54)
    $g.FillPath($white, (New-RoundedPath $x0 $y0 $leftW $h $rad))

    $rx = [single]($x0 + $leftW + $gap)
    $rw = [single]($w - $leftW - $gap)
    $topH = [single](($h - $gap) * 0.56)
    $g.FillPath($white, (New-RoundedPath $rx $y0 $rw $topH $rad))

    $by = [single]($y0 + $topH + $gap)
    $bh = [single]($h - $topH - $gap)
    $halfW = [single](($rw - $gap) / 2)
    $g.FillPath($white, (New-RoundedPath $rx $by $halfW $bh $rad))
    $g.FillPath($white, (New-RoundedPath ($rx + $halfW + $gap) $by $halfW $bh $rad))

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , @($s, $ms.ToArray())

    $g.Dispose(); $bmp.Dispose(); $ms.Dispose()
    $white.Dispose(); $grad.Dispose(); $bg.Dispose()
}

$icoPath = Join-Path $outDir 'app.ico'
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$pngs.Count)
$offset = 6 + 16 * $pngs.Count
foreach ($item in $pngs) {
    $size = $item[0]; $data = $item[1]
    $dim = if ($size -ge 256) { 0 } else { [byte]$size }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim)
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$data.Length); $bw.Write([UInt32]$offset)
    $offset += $data.Length
}
foreach ($item in $pngs) { $bw.Write($item[1]) }
$bw.Flush(); $bw.Close(); $fs.Close()

Write-Host "icon generated: $icoPath ($((Get-Item $icoPath).Length) bytes)"
