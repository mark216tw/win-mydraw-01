param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class BrushEffectInput
{
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")]
    public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
    [DllImport("user32.dll")]
    public static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extraInfo);
    [DllImport("user32.dll")]
    public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
}
'@

$previousDpiContext = [BrushEffectInput]::SetThreadDpiAwarenessContext([IntPtr]::new(-4))
$settingsPath = Join-Path $env:LOCALAPPDATA 'MyDraw\settings.json'
$settingsBackup = Join-Path $env:TEMP "MyDraw-brush-effect-settings-$PID.json"
$hadSettings = Test-Path -LiteralPath $settingsPath
$process = $null

function Find-ElementById($root, $name) {
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $name)
    $found = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    if ($null -ne $found) { return $found }
    $processCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $process.Id)
    $windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
        [System.Windows.Automation.TreeScope]::Children, $processCondition)
    foreach ($window in $windows) {
        $found = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($null -ne $found) { return $found }
    }
    throw "UI element not found: $name"
}

function Invoke-OrExpand($element) {
    $expandPattern = $null
    if ($element.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$expandPattern)) {
        $expandPattern.Expand()
        return
    }
    $invokePattern = $null
    if ($element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invokePattern)) {
        $invokePattern.Invoke()
        return
    }
    throw 'Element supports neither Invoke nor ExpandCollapse pattern.'
}

function Send-Key($key) {
    [BrushEffectInput]::keybd_event($key, 0, 0, [UIntPtr]::Zero)
    [BrushEffectInput]::keybd_event($key, 0, 2, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 80
}

function Read-ScreenPixel($x, $y) {
    $bitmap = [System.Drawing.Bitmap]::new(1, 1)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($x, $y, 0, 0, [System.Drawing.Size]::new(1, 1))
        return $bitmap.GetPixel(0, 0).ToArgb()
    }
    finally { $graphics.Dispose(); $bitmap.Dispose() }
}

function Get-Brightness($argb) {
    $color = [System.Drawing.Color]::FromArgb($argb)
    return [int](($color.R + $color.G + $color.B) / 3)
}

function Get-MinBrightness($centerX, $centerY, $radius) {
    $minimum = 255
    for ($x = -$radius; $x -le $radius; $x += 4) {
        for ($y = -$radius; $y -le $radius; $y += 4) {
            $brightness = Get-Brightness (Read-ScreenPixel ($centerX + $x) ($centerY + $y))
            if ($brightness -lt $minimum) { $minimum = $brightness }
        }
    }
    return $minimum
}

function Draw-Line($x1, $y1, $x2, $y2, $steps, $delayMilliseconds) {
    $null = [BrushEffectInput]::SetCursorPos($x1, $y1)
    Start-Sleep -Milliseconds 80
    [BrushEffectInput]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    for ($step = 1; $step -le $steps; $step++) {
        $t = $step / $steps
        $x = [int]($x1 + (($x2 - $x1) * $t))
        $y = [int]($y1 + (($y2 - $y1) * $t))
        $null = [BrushEffectInput]::SetCursorPos($x, $y)
        if ($delayMilliseconds -gt 0) { Start-Sleep -Milliseconds $delayMilliseconds }
    }
    [BrushEffectInput]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 400
}

try {
    if ($hadSettings) {
        Move-Item -LiteralPath $settingsPath -Destination $settingsBackup -Force
    }
    $process = Start-Process -FilePath $ExecutablePath -PassThru
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) { throw "Application exited with code $($process.ExitCode)." }
        if ($process.MainWindowHandle -ne 0) { break }
    }
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
    $null = [BrushEffectInput]::SetForegroundWindow($process.MainWindowHandle)
    Start-Sleep -Seconds 3
    $bounds = $root.Current.BoundingRectangle
    $centerX = [int]($bounds.Left + $bounds.Width / 2)
    $centerY = [int]($bounds.Top + $bounds.Height / 2)

    # Airbrush: slow and fast strokes must both be visible with similar density.
    Invoke-OrExpand (Find-ElementById $root 'MoreBrushesButton')
    Start-Sleep -Milliseconds 400
    Invoke-OrExpand (Find-ElementById $root 'AirbrushToolButton')
    Start-Sleep -Milliseconds 400
    Send-Key 27
    $null = [BrushEffectInput]::SetForegroundWindow($process.MainWindowHandle)
    Draw-Line ($centerX - 220) ($centerY - 130) ($centerX + 220) ($centerY - 130) 30 25
    Draw-Line ($centerX - 220) ($centerY + 30) ($centerX + 220) ($centerY + 30) 2 0
    $slowAirbrush = Get-Brightness (Read-ScreenPixel $centerX ($centerY - 130))
    $fastAirbrush = Get-Brightness (Read-ScreenPixel $centerX ($centerY + 30))
    if ($slowAirbrush -gt 245 -or $fastAirbrush -gt 245) {
        throw "Airbrush stroke is too faint. slow=$slowAirbrush fast=$fastAirbrush"
    }
    if ([Math]::Abs($slowAirbrush - $fastAirbrush) -gt 70) {
        throw "Airbrush density depends on input speed. slow=$slowAirbrush fast=$fastAirbrush"
    }

    # Brush settings must expose softness, airbrush mode, and pressure curve.
    Invoke-OrExpand (Find-ElementById $root 'BrushSettingsButton')
    Start-Sleep -Milliseconds 400
    $null = Find-ElementById $root 'SoftnessSlider'
    $airbrushMode = Find-ElementById $root 'AirbrushModeComboBox'
    $null = Find-ElementById $root 'PressureCurveComboBox'
    $modeExpand = $null
    if ($airbrushMode.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$modeExpand)) {
        $modeExpand.Expand()
        Start-Sleep -Milliseconds 300
        $grainItem = Find-ElementById $root 'AirbrushModeGrainItem'
        $grainItem.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
        Start-Sleep -Milliseconds 300
    }
    Send-Key 27
    $null = [BrushEffectInput]::SetForegroundWindow($process.MainWindowHandle)

    # Grain mode should still produce a visible spray.
    Draw-Line ($centerX - 140) ($centerY + 230) ($centerX + 140) ($centerY + 230) 18 18
    $grainBrightness = Get-MinBrightness $centerX ($centerY + 230) 12
    if ($grainBrightness -gt 245) {
        throw "Grain airbrush stroke is too faint. brightness=$grainBrightness"
    }

    # Eraser: erase the slow airbrush stroke and verify the hole becomes white.
    Invoke-OrExpand (Find-ElementById $root 'EraserToolButton')
    Start-Sleep -Milliseconds 400
    $null = [BrushEffectInput]::SetForegroundWindow($process.MainWindowHandle)
    $eraseX = $centerX - 70
    Draw-Line $eraseX ($centerY - 180) $eraseX ($centerY - 80) 10 20
    $erasedAirbrush = Get-Brightness (Read-ScreenPixel $eraseX ($centerY - 130))
    if ($erasedAirbrush -lt 240) {
        throw "Eraser did not erase the airbrush stroke. brightness=$erasedAirbrush"
    }

    # Pencil uses the per-point opacity mask path; it must draw and be erasable.
    Invoke-OrExpand (Find-ElementById $root 'PencilToolButton')
    Start-Sleep -Milliseconds 400
    for ($press = 0; $press -lt 12; $press++) { Send-Key 221 }
    $null = [BrushEffectInput]::SetForegroundWindow($process.MainWindowHandle)
    Draw-Line ($centerX - 160) ($centerY - 210) ($centerX + 160) ($centerY - 210) 24 12
    $pencilBrightness = Get-Brightness (Read-ScreenPixel $centerX ($centerY - 210))
    if ($pencilBrightness -gt 245) {
        throw "Pencil stroke is too faint. brightness=$pencilBrightness"
    }
    Invoke-OrExpand (Find-ElementById $root 'EraserToolButton')
    Start-Sleep -Milliseconds 400
    $null = [BrushEffectInput]::SetForegroundWindow($process.MainWindowHandle)
    Draw-Line ($centerX + 60) ($centerY - 240) ($centerX + 60) ($centerY - 180) 6 20
    $erasedPencil = Get-Brightness (Read-ScreenPixel ($centerX + 60) ($centerY - 210))
    if ($erasedPencil -lt 240) {
        throw "Eraser did not erase the pencil stroke. brightness=$erasedPencil"
    }

    [pscustomobject]@{
        AirbrushDensity     = 'Passed'
        AirbrushSettings    = 'Passed'
        AirbrushGrain       = 'Passed'
        Eraser              = 'Passed'
        PencilOpacityMask   = 'Passed'
        SlowAirbrush        = $slowAirbrush
        FastAirbrush        = $fastAirbrush
        GrainAirbrush       = $grainBrightness
        Pencil              = $pencilBrightness
        ErasedAirbrush      = $erasedAirbrush
        ErasedPencil        = $erasedPencil
        ProcessId           = $process.Id
    }
}
catch {
    $_.ScriptStackTrace
    throw
}
finally {
    [BrushEffectInput]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    if ($null -ne $process) {
        if (-not $process.HasExited) {
            $process.CloseMainWindow() | Out-Null
            Start-Sleep -Milliseconds 800
            if (-not $process.HasExited) { $process.Kill() }
        }
        $process.Dispose()
    }
    if ($hadSettings) {
        if (Test-Path -LiteralPath $settingsBackup) {
            Move-Item -LiteralPath $settingsBackup -Destination $settingsPath -Force
        }
    }
    elseif (Test-Path -LiteralPath $settingsPath) {
        Remove-Item -LiteralPath $settingsPath -Force
    }
    $null = [BrushEffectInput]::SetThreadDpiAwarenessContext($previousDpiContext)
}
