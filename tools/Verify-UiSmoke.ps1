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
public static class MyDrawSmokeInput
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
$previousDpiContext = [MyDrawSmokeInput]::SetThreadDpiAwarenessContext([IntPtr]::new(-4))
$settingsPath = Join-Path $env:LOCALAPPDATA 'MyDraw\settings.json'
$hadSettings = Test-Path -LiteralPath $settingsPath
$settingsBackup = if ($hadSettings) { [System.IO.File]::ReadAllBytes($settingsPath) } else { $null }
$process = Start-Process -FilePath $ExecutablePath -PassThru

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
    if ($null -eq $found) { throw "UI element not found: $name" }
    return $found
}

function Find-ElementByName($root, $name) {
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::NameProperty, $name)
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
    throw "UI element not found by name: $name"
}

function Invoke-OrExpand($element) {
    if ($null -eq $element) { throw 'Expected UI element was not found.' }
    if ($element.Current.AutomationId -like '*MenuBarItem') {
        $menuPattern = $null
        if ($element.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$menuPattern)) {
            $menuPattern.Expand()
            return
        }
        $rect = $element.Current.BoundingRectangle
        $null = [MyDrawSmokeInput]::SetCursorPos([int]($rect.Left + $rect.Width / 2), [int]($rect.Top + $rect.Height / 2))
        [MyDrawSmokeInput]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
        [MyDrawSmokeInput]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
        return
    }
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
    [MyDrawSmokeInput]::keybd_event($key, 0, 0, [UIntPtr]::Zero)
    [MyDrawSmokeInput]::keybd_event($key, 0, 2, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 700
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

try {
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) { throw "Application exited with code $($process.ExitCode)." }
        if ($process.MainWindowHandle -ne 0) { break }
    }
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
    $null = [MyDrawSmokeInput]::SetForegroundWindow($process.MainWindowHandle)
    Start-Sleep -Seconds 3

    # 0. Default canvas must be 1080 x 1080.
    $canvasSizeButton = Find-ElementById $root 'CanvasSizeButton'
    $canvasSizeText = $canvasSizeButton.Current.HelpText
    if ($canvasSizeText -notmatch '1080') { throw "Default canvas size was not 1080 x 1080: $canvasSizeText" }

    # Favorite brushes are placed below Eraser and persist complete brush snapshots.
    $favoriteButton = Find-ElementById $root 'FavoriteBrushesButton'
    $eraserButton = Find-ElementById $root 'EraserToolButton'
    $panButton = Find-ElementById $root 'PanToolButton'
    if ($favoriteButton.Current.BoundingRectangle.Top -le $eraserButton.Current.BoundingRectangle.Top -or
        $favoriteButton.Current.BoundingRectangle.Top -ge $panButton.Current.BoundingRectangle.Top) {
        throw 'Favorite Brushes button is not between Eraser and Pan.'
    }
    Invoke-OrExpand $favoriteButton
    Start-Sleep -Milliseconds 400
    Invoke-OrExpand (Find-ElementById $root 'SaveCurrentFavoriteBrushButton')
    Start-Sleep -Milliseconds 400
    $nameEditor = Find-ElementById $root 'FavoriteBrushNameEditor'
    $nameEditor.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('UI Smoke Favorite')
    Invoke-OrExpand (Find-ElementById $root 'PrimaryButton')
    Start-Sleep -Milliseconds 500
    if (!(Test-Path -LiteralPath $settingsPath) -or
        (Get-Content -LiteralPath $settingsPath -Raw) -notmatch 'UI Smoke Favorite') {
        throw 'Favorite brush was not persisted to settings.json.'
    }
    Send-Key 221
    Send-Key 77
    Invoke-OrExpand $favoriteButton
    Start-Sleep -Milliseconds 400
    Invoke-OrExpand (Find-ElementById $root 'FavoriteApplyButton')
    Start-Sleep -Milliseconds 400
    Invoke-OrExpand $favoriteButton
    Start-Sleep -Milliseconds 400
    Invoke-OrExpand (Find-ElementById $root 'FavoriteRenameButton')
    Start-Sleep -Milliseconds 400
    $nameEditor = Find-ElementById $root 'FavoriteBrushNameEditor'
    $nameEditor.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('UI Smoke Renamed')
    Invoke-OrExpand (Find-ElementById $root 'PrimaryButton')
    Start-Sleep -Milliseconds 500
    Invoke-OrExpand $favoriteButton
    Start-Sleep -Milliseconds 400
    $null = Find-ElementByName $root 'UI Smoke Renamed'
    Invoke-OrExpand (Find-ElementById $root 'FavoriteDeleteButton')
    Start-Sleep -Milliseconds 400
    Invoke-OrExpand (Find-ElementById $root 'PrimaryButton')
    Start-Sleep -Milliseconds 500
    if ((Get-Content -LiteralPath $settingsPath -Raw) -match 'UI Smoke Renamed') {
        throw 'Deleted favorite brush remained in settings.json.'
    }

    # 1. Open appearance settings from the menu bar.
    Invoke-OrExpand (Find-ElementById $root 'SettingsMenuBarItem')
    Start-Sleep -Milliseconds 500
    Invoke-OrExpand (Find-ElementById $root 'AppearanceSettingsMenuItem')
    Start-Sleep -Seconds 1
    $dialog = Find-ElementById $root 'AppearanceSettingsDialog'
    if ($null -eq $dialog) { throw 'Settings dialog did not open from MenuBar.' }
    Invoke-OrExpand (Find-ElementById $dialog 'PrimaryButton')
    Start-Sleep -Milliseconds 500

    # 2. Add a layer through its flyout.
    Invoke-OrExpand (Find-ElementById $root 'LayersFlyoutButton')
    Start-Sleep -Milliseconds 600
    Invoke-OrExpand (Find-ElementById $root 'AddLayerButton')
    Start-Sleep -Milliseconds 500
    $layerList = Find-ElementById $root 'LayerList'
    $itemCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::ListItem)
    $items = $layerList.FindAll([System.Windows.Automation.TreeScope]::Descendants, $itemCondition)
    if ($items.Count -ne 2) { throw 'New layer did not appear in Layers flyout.' }

    # 3. Dismiss the flyout and draw an undoable circle.
    $null = [MyDrawSmokeInput]::SetForegroundWindow($process.MainWindowHandle)
    $bounds = $root.Current.BoundingRectangle
    $centerX = [int]($bounds.Left + $bounds.Width / 2)
    $centerY = [int]($bounds.Top + $bounds.Height / 2)
    $null = [MyDrawSmokeInput]::SetCursorPos($centerX, $centerY)
    Start-Sleep -Milliseconds 200
    [MyDrawSmokeInput]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    [MyDrawSmokeInput]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 400
    Invoke-OrExpand (Find-ElementById $root 'PencilToolButton')
    Start-Sleep -Milliseconds 300

    $null = [MyDrawSmokeInput]::SetCursorPos(($centerX + 40), $centerY)
    Start-Sleep -Milliseconds 250
    [MyDrawSmokeInput]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    for ($step = 1; $step -le 100; $step++) {
        $angle = 2 * [Math]::PI * $step / 100
        $null = [MyDrawSmokeInput]::SetCursorPos(
            [int]($centerX + 40 * [Math]::Cos($angle)),
            [int]($centerY + 40 * [Math]::Sin($angle)))
        Start-Sleep -Milliseconds 8
    }
    [MyDrawSmokeInput]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Seconds 1
    $process.Refresh()
    if ($process.HasExited) { throw 'Application exited during circle rendering.' }
    $undoButton = Find-ElementById $root 'UndoButton'
    if (!$undoButton.Current.IsEnabled) { throw 'Circle did not produce an undoable stroke.' }
    Invoke-OrExpand $undoButton
    # Self-crossing stroke must not produce a white hole at its intersection.
    $crossX = $centerX + 120
    $crossY = $centerY + 60
    $null = [MyDrawSmokeInput]::SetCursorPos($crossX, $crossY)
    [MyDrawSmokeInput]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    for ($step = 1; $step -le 160; $step++) {
        $angle = 2 * [Math]::PI * $step / 160
        $null = [MyDrawSmokeInput]::SetCursorPos(
            [int]($crossX + 65 * [Math]::Sin($angle)),
            [int]($crossY + 45 * [Math]::Sin(2 * $angle)))
        Start-Sleep -Milliseconds 8
    }
    [MyDrawSmokeInput]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    $null = [MyDrawSmokeInput]::SetCursorPos(($crossX + 120), ($crossY + 100))
    Start-Sleep -Milliseconds 700
    $intersection = [System.Drawing.Color]::FromArgb((Read-ScreenPixel $crossX $crossY))
    if ($intersection.R -gt 180 -and $intersection.G -gt 180 -and $intersection.B -gt 180) {
        throw 'Self-crossing stroke contains a light/white hole.'
    }
    Invoke-OrExpand $undoButton
    # Selected pencil keeps its fill while hovered.
    $pencil = Find-ElementById $root 'PencilToolButton'
    $pencilBounds = $pencil.Current.BoundingRectangle
    $sampleX = [int]$pencilBounds.Left + 8
    $sampleY = [int]$pencilBounds.Top + 8
    $normalColor = Read-ScreenPixel $sampleX $sampleY
    $null = [MyDrawSmokeInput]::SetCursorPos([int]($pencilBounds.Left + 20), [int]($pencilBounds.Top + 20))
    Start-Sleep -Milliseconds 300
    if ((Read-ScreenPixel $sampleX $sampleY) -ne $normalColor) { throw 'Selected brush changed color on hover.' }
    # Right-button drag must not create a stroke (undo history stays empty).
    while ($undoButton.Current.IsEnabled) {
        Invoke-OrExpand $undoButton
        Start-Sleep -Milliseconds 200
    }
    $null = [MyDrawSmokeInput]::SetCursorPos($crossX, $crossY)
    [MyDrawSmokeInput]::mouse_event(8, 0, 0, 0, [UIntPtr]::Zero)
    $null = [MyDrawSmokeInput]::SetCursorPos(($crossX + 30), ($crossY + 20))
    Start-Sleep -Milliseconds 300
    [MyDrawSmokeInput]::mouse_event(16, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 300
    if ($undoButton.Current.IsEnabled) { throw 'Right drag incorrectly created a stroke.' }
    # Middle-button drag is no longer a pan gesture and must not create a stroke.
    $null = [MyDrawSmokeInput]::SetCursorPos($centerX, $centerY)
    [MyDrawSmokeInput]::mouse_event(32, 0, 0, 0, [UIntPtr]::Zero)
    $null = [MyDrawSmokeInput]::SetCursorPos(($centerX + 30), ($centerY + 20))
    Start-Sleep -Milliseconds 300
    [MyDrawSmokeInput]::mouse_event(64, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 300
    if ($undoButton.Current.IsEnabled) { throw 'Middle drag incorrectly created a stroke.' }
    # Airbrush must be visible, consistent across input speed, and erasable.
    Invoke-OrExpand (Find-ElementById $root 'MoreBrushesButton')
    Start-Sleep -Milliseconds 400
    Invoke-OrExpand (Find-ElementById $root 'AirbrushToolButton')
    Start-Sleep -Milliseconds 400
    $null = [MyDrawSmokeInput]::SetForegroundWindow($process.MainWindowHandle)
    $airY1 = $centerY + 120
    $airY2 = $centerY + 170
    $null = [MyDrawSmokeInput]::SetCursorPos(($centerX - 180), $airY1)
    [MyDrawSmokeInput]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    for ($step = 1; $step -le 24; $step++) {
        $null = [MyDrawSmokeInput]::SetCursorPos(
            [int]($centerX - 180 + (360 * $step / 24)), $airY1)
        Start-Sleep -Milliseconds 12
    }
    [MyDrawSmokeInput]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 500
    $null = [MyDrawSmokeInput]::SetCursorPos(($centerX - 180), $airY2)
    [MyDrawSmokeInput]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    $null = [MyDrawSmokeInput]::SetCursorPos(($centerX + 180), $airY2)
    [MyDrawSmokeInput]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 500
    $slowAirbrush = [System.Drawing.Color]::FromArgb((Read-ScreenPixel $centerX $airY1))
    $fastAirbrush = [System.Drawing.Color]::FromArgb((Read-ScreenPixel $centerX $airY2))
    if ($slowAirbrush.R -gt 245 -and $slowAirbrush.G -gt 245 -and $slowAirbrush.B -gt 245) {
        throw 'Airbrush stroke is too faint.'
    }
    if ($fastAirbrush.R -gt 245 -and $fastAirbrush.G -gt 245 -and $fastAirbrush.B -gt 245) {
        throw 'Fast airbrush stroke is too faint.'
    }
    if ([Math]::Abs($slowAirbrush.R - $fastAirbrush.R) -gt 80) {
        throw 'Airbrush density depends on input speed.'
    }
    Invoke-OrExpand (Find-ElementById $root 'BrushSettingsButton')
    Start-Sleep -Milliseconds 400
    $null = Find-ElementById $root 'SoftnessSlider'
    $airbrushMode = Find-ElementById $root 'AirbrushModeComboBox'
    $null = Find-ElementById $root 'PressureCurveComboBox'
    $airbrushModeExpand = $null
    if ($airbrushMode.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$airbrushModeExpand)) {
        $airbrushModeExpand.Expand()
        Start-Sleep -Milliseconds 300
        $grainItem = Find-ElementById $root 'AirbrushModeGrainItem'
        $grainItem.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
        Start-Sleep -Milliseconds 300
    }
    Send-Key 27
    Invoke-OrExpand (Find-ElementById $root 'EraserToolButton')
    Start-Sleep -Milliseconds 400
    $null = [MyDrawSmokeInput]::SetForegroundWindow($process.MainWindowHandle)
    $eraseX = $centerX + 60
    $null = [MyDrawSmokeInput]::SetCursorPos($eraseX, ($airY1 - 40))
    [MyDrawSmokeInput]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    for ($step = 1; $step -le 16; $step++) {
        $null = [MyDrawSmokeInput]::SetCursorPos($eraseX, [int]($airY1 - 40 + (150 * $step / 16)))
        Start-Sleep -Milliseconds 10
    }
    [MyDrawSmokeInput]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 500
    $erasedStroke = [System.Drawing.Color]::FromArgb((Read-ScreenPixel $eraseX $airY1))
    if ($erasedStroke.R -lt 240 -or $erasedStroke.G -lt 240 -or $erasedStroke.B -lt 240) {
        throw 'Eraser did not erase the airbrush stroke.'
    }
    Send-Key 27
    # Each brush should remember its own size across tool switches.
    Send-Key 80
    $statusText = Find-ElementById $root 'ActiveToolStatusText'
    $sizeBefore = $statusText.Current.Name
    Send-Key 221
    $sizeAfter = (Find-ElementById $root 'ActiveToolStatusText').Current.Name
    if ($sizeAfter -eq $sizeBefore) { throw 'Brush size shortcut did not change the pen size.' }
    Send-Key 77
    Send-Key 80
    $sizeRestored = (Find-ElementById $root 'ActiveToolStatusText').Current.Name
    if ($sizeRestored -ne $sizeAfter) { throw "Brush size was not remembered per tool. expected=$sizeAfter actual=$sizeRestored" }
    Send-Key 122
    Send-Key 27
    if ((Find-ElementById $root 'UndoButton').Current.IsOffscreen) { throw 'Esc did not restore the toolbar.' }
    Invoke-OrExpand (Find-ElementById $root 'SettingsMenuBarItem')
    Start-Sleep -Milliseconds 300
    Invoke-OrExpand (Find-ElementById $root 'AboutMenuItem')
    Start-Sleep -Milliseconds 500
    $about = Find-ElementById $root 'AboutDialog'
    if ($null -eq $about) { throw 'About dialog did not open.' }
    Invoke-OrExpand (Find-ElementById $about 'CloseButton')
    # Switch toolbar to the right, check placement, then restore its original side.
    $originalRight = $penBounds.Left -gt ($bounds.Left + $bounds.Width / 2)
    foreach ($right in @(!$originalRight, $originalRight)) {
        Invoke-OrExpand (Find-ElementById $root 'SettingsMenuBarItem')
        Start-Sleep -Milliseconds 300
        Invoke-OrExpand (Find-ElementById $root 'AppearanceSettingsMenuItem')
        Start-Sleep -Milliseconds 500
        $dialog = Find-ElementById $root 'AppearanceSettingsDialog'
        Invoke-OrExpand (Find-ElementById $dialog 'ToolbarPositionComboBox')
        Start-Sleep -Milliseconds 300
        $choiceId = if ($right) { 'ToolbarRightChoice' } else { 'ToolbarLeftChoice' }
        $choice = Find-ElementById $dialog $choiceId
        $choice.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
        Invoke-OrExpand (Find-ElementById $dialog 'PrimaryButton')
        Start-Sleep -Milliseconds 500
        $actualRight = (Find-ElementById $root 'UndoButton').Current.BoundingRectangle.Left -gt ($bounds.Left + $bounds.Width / 2)
        if ($actualRight -ne $right) { throw 'Toolbar side did not change.' }
    }
    # Completed strokes are raster-cached; a burst of strokes must not make the UI unresponsive.
    for ($stroke = 0; $stroke -lt 150; $stroke++) {
        $x = $centerX - 100 + (($stroke % 20) * 10)
        $y = $centerY - 80 + ([Math]::Floor($stroke / 20) * 12)
        $null = [MyDrawSmokeInput]::SetCursorPos($x, $y)
        [MyDrawSmokeInput]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
        $null = [MyDrawSmokeInput]::SetCursorPos(($x + 6), ($y + 4))
        [MyDrawSmokeInput]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 3
    }
    Start-Sleep -Milliseconds 500
    $responsiveness = [System.Diagnostics.Stopwatch]::StartNew()
    Invoke-OrExpand (Find-ElementById $root 'FavoriteBrushesButton')
    $null = Find-ElementById $root 'SaveCurrentFavoriteBrushButton'
    $responsiveness.Stop()
    if ($responsiveness.ElapsedMilliseconds -gt 3000) {
        throw "UI became unresponsive after repeated strokes: $($responsiveness.ElapsedMilliseconds) ms"
    }
    Send-Key 27
    [pscustomobject]@{
        DefaultCanvasSize     = 'Passed'
        FavoriteBrushes       = 'Passed'
        MenuBarSettingsDialog = 'Passed'
        LayersFlyoutAddLayer  = 'Passed'
        CircleStrokeAndUndo   = 'Passed'
        SelfCrossingFill      = 'Passed'
        SelectedBrushHover    = 'Passed'
        RightDragNoStroke     = 'Passed'
        MiddleDragNoStroke    = 'Passed'
        AirbrushAndEraser     = 'Passed'
        BrushSettingsMemory   = 'Passed'
        FullScreenEscape      = 'Passed'
        AboutDialog           = 'Passed'
        ToolbarBothSides      = 'Passed'
        RepeatedStrokeLatency = "$($responsiveness.ElapsedMilliseconds) ms"
        ProcessId             = $process.Id
    }
}
catch {
    $_.ScriptStackTrace
    throw
}
finally {
    [MyDrawSmokeInput]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    [MyDrawSmokeInput]::mouse_event(16, 0, 0, 0, [UIntPtr]::Zero)
    if (!$process.HasExited) { Stop-Process -Id $process.Id }
    $process.Dispose()
    if ($hadSettings) {
        [System.IO.File]::WriteAllBytes($settingsPath, $settingsBackup)
    }
    elseif (Test-Path -LiteralPath $settingsPath) {
        Remove-Item -LiteralPath $settingsPath -Force
    }
    $null = [MyDrawSmokeInput]::SetThreadDpiAwarenessContext($previousDpiContext)
}


