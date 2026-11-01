$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase
$assetRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'src\AiGuardLock\Assets'
[void][IO.Directory]::CreateDirectory($assetRoot)

# One geometric design, rendered at each icon size to keep small sizes sharp.
$shield = 'M256,78 C216,105 159,122 112,128 L112,238 C112,325 173,391 256,434 C339,391 400,325 400,238 L400,128 C353,122 296,105 256,78 Z'
$keyhole = 'M256,176 C228,176 206,198 206,226 C206,245 217,262 233,271 L222,326 L290,326 L279,271 C295,262 306,245 306,226 C306,198 284,176 256,176 Z'
$highlight = 'M140,149 C181,142 223,126 256,107 C289,126 331,142 372,149'
$svg = @"
<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 512 512">
  <title>AI Guard Lock — teal shield and keyhole</title>
  <defs>
    <linearGradient id="tile" x2="1" y2="1"><stop stop-color="#173B4C"/><stop offset="1" stop-color="#0B192A"/></linearGradient>
    <linearGradient id="shield" x2="0.8" y2="1"><stop stop-color="#9CF0DF"/><stop offset="1" stop-color="#0AA89F"/></linearGradient>
  </defs>
  <rect x="20" y="20" width="472" height="472" rx="108" fill="url(#tile)"/>
  <path d="$shield" fill="url(#shield)"/>
  <path d="$highlight" fill="none" stroke="#D4FFF3" stroke-width="6" stroke-linecap="round" opacity="0.65"/>
  <path d="$keyhole" fill="#102C3C"/>
</svg>
"@
[IO.File]::WriteAllText((Join-Path $assetRoot 'AppIcon.svg'), $svg, [Text.UTF8Encoding]::new($false))
function Gradient($first, $last, $endX) {
    $brush = [Windows.Media.LinearGradientBrush]::new()
    $brush.StartPoint = [Windows.Point]::new(0, 0)
    $brush.EndPoint = [Windows.Point]::new($endX, 1)
    $brush.GradientStops.Add([Windows.Media.GradientStop]::new([Windows.Media.ColorConverter]::ConvertFromString($first), 0))
    $brush.GradientStops.Add([Windows.Media.GradientStop]::new([Windows.Media.ColorConverter]::ConvertFromString($last), 1))
    return $brush
}
function Render-Icon([int]$size) {
    $visual = [Windows.Media.DrawingVisual]::new()
    $drawing = $visual.RenderOpen()
    $drawing.PushTransform([Windows.Media.ScaleTransform]::new($size / 512.0, $size / 512.0))
    $drawing.DrawRoundedRectangle((Gradient '#173B4C' '#0B192A' 1), $null, [Windows.Rect]::new(20, 20, 472, 472), 108, 108)
    $drawing.DrawGeometry((Gradient '#9CF0DF' '#0AA89F' 0.8), $null, [Windows.Media.Geometry]::Parse($shield))
    $highlightBrush = [Windows.Media.SolidColorBrush]::new([Windows.Media.ColorConverter]::ConvertFromString('#D4FFF3'))
    $highlightBrush.Opacity = 0.65
    $pen = [Windows.Media.Pen]::new($highlightBrush, 6)
    $pen.StartLineCap = $pen.EndLineCap = [Windows.Media.PenLineCap]::Round
    $drawing.DrawGeometry($null, $pen, [Windows.Media.Geometry]::Parse($highlight))
    $drawing.DrawGeometry([Windows.Media.BrushConverter]::new().ConvertFromString('#102C3C'), $null, [Windows.Media.Geometry]::Parse($keyhole))
    $drawing.Pop()
    $drawing.Close()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.MemoryStream]::new()
    $encoder.Save($stream)
    $bytes = $stream.ToArray()
    $stream.Dispose()
    return ,$bytes
}
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = @($sizes | ForEach-Object { ,(Render-Icon $_) })
$icoPath = Join-Path $assetRoot 'AppIcon.ico'
$file = [IO.File]::Create($icoPath)
$writer = [IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
        $offset += $frames[$i].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
} finally { $writer.Dispose(); $file.Dispose() }
[IO.File]::WriteAllBytes((Join-Path $assetRoot 'AppIcon.png'), (Render-Icon 512))
[IO.File]::WriteAllBytes((Join-Path $assetRoot 'AppIcon-preview.png'), (Render-Icon 256))
Write-Output $icoPath
