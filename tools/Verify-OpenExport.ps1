param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class MyDrawOpenExportInput
{
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")]
    public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
}
'@

$pngPath = Join-Path $env:TEMP "mydraw-export-$PID.png"
$projectPath = Join-Path $env:TEMP "mydraw-open-$PID.mydraw"
$previousClipboard = $null
try {
    $previousClipboard = Get-Clipboard -Raw -ErrorAction SilentlyContinue
}
catch {
    $previousClipboard = $null
}

$process = Start-Process -FilePath $ExecutablePath -PassThru

function Find-ElementById($root, $name) {
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $name)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Invoke-Element($element) {
    if ($null -eq $element) { throw 'Element not found.' }
    $expand = $null
    if ($element.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$expand)) {
        $expand.Expand()
        return
    }
    $invoke = $null
    if ($element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$invoke)) {
        $invoke.Invoke()
        return
    }
    throw 'Element cannot be invoked.'
}

function Wait-DialogWithFileName {
    $processCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $process.Id)
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        $foreground = [System.Windows.Automation.AutomationElement]::FromHandle(
            [MyDrawOpenExportInput]::GetForegroundWindow())
        if ($null -ne (Find-ElementById $foreground '1001')) {
            return $foreground
        }

        $windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
            [System.Windows.Automation.TreeScope]::Children, $processCondition)
        foreach ($window in $windows) {
            if ($null -ne (Find-ElementById $window '1001')) {
                return $window
            }
        }

        Start-Sleep -Milliseconds 250
    }

    throw 'File name control not found.'
}

function Set-FileName($dialog, $value) {
    $hostPane = Find-ElementById $dialog '1001'
    if ($null -eq $hostPane) { throw 'File name control not found.' }
    $rect = $hostPane.Current.BoundingRectangle
    $null = [MyDrawOpenExportInput]::SetCursorPos(
        [int]($rect.Left + $rect.Width / 2),
        [int]($rect.Top + $rect.Height / 2))
    [MyDrawOpenExportInput]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    [MyDrawOpenExportInput]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 300
    Set-Clipboard -Value $value
    [System.Windows.Forms.SendKeys]::SendWait('^a')
    Start-Sleep -Milliseconds 100
    [System.Windows.Forms.SendKeys]::SendWait('^v')
    Start-Sleep -Milliseconds 300
}

function Confirm-Dialog {
    [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
}

function Invoke-MenuItem($root, $menuId, $itemId) {
    Invoke-Element (Find-ElementById $root $menuId)
    Start-Sleep -Milliseconds 500
    Invoke-Element (Find-ElementById $root $itemId)
}

try {
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) { throw "Application exited with code $($process.ExitCode)." }
        if ($process.MainWindowHandle -ne 0) { break }
    }
    Start-Sleep -Seconds 3

    $root = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
    Invoke-MenuItem $root 'FileMenuBarItem' 'ExportPngMenuItem'
    Start-Sleep -Seconds 3
    $dialog = Wait-DialogWithFileName
    Set-FileName $dialog $pngPath
    Confirm-Dialog
    Start-Sleep -Seconds 6
    if (!(Test-Path -LiteralPath $pngPath)) { throw 'PNG export did not create a file.' }
    if ((Get-Item -LiteralPath $pngPath).Length -le 0) { throw 'PNG export file is empty.' }

    $root = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
    Invoke-MenuItem $root 'FileMenuBarItem' 'SaveProjectMenuItem'
    Start-Sleep -Seconds 3
    $dialog = Wait-DialogWithFileName
    Set-FileName $dialog $projectPath
    Confirm-Dialog
    Start-Sleep -Seconds 5
    if (!(Test-Path -LiteralPath $projectPath)) { throw 'Project save did not create a file.' }

    $root = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
    Invoke-MenuItem $root 'FileMenuBarItem' 'NewDocumentMenuItem'
    Start-Sleep -Seconds 2

    $root = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
    Invoke-MenuItem $root 'FileMenuBarItem' 'OpenProjectMenuItem'
    Start-Sleep -Seconds 3
    $dialog = Wait-DialogWithFileName
    Set-FileName $dialog $projectPath
    Confirm-Dialog
    Start-Sleep -Seconds 5

    $root = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
    $errorCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'OpenFailedDialog')
    $errorDialog = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $errorCondition)
    if ($null -ne $errorDialog) { throw 'Open project reported an error dialog.' }

    [pscustomobject]@{
        ExportPng    = 'Passed'
        SaveProject  = 'Passed'
        OpenProject  = 'Passed'
        PngBytes     = (Get-Item -LiteralPath $pngPath).Length
        ProjectBytes = (Get-Item -LiteralPath $projectPath).Length
    }
}
finally {
    if (!$process.HasExited) { Stop-Process -Id $process.Id -Force }
    $process.Dispose()
    if (Test-Path -LiteralPath $pngPath) { Remove-Item -LiteralPath $pngPath -Force }
    if (Test-Path -LiteralPath $projectPath) { Remove-Item -LiteralPath $projectPath -Force }
    if ($null -ne $previousClipboard) {
        try { Set-Clipboard -Value $previousClipboard } catch { }
    }
}

