param([string]$Output = (Join-Path $PSScriptRoot 'AraControl.exe'))
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $compiler = Join-Path ([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'csc.exe'
    if (!(Test-Path $compiler)) {
        $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    }
    $options = @('/nologo', '/target:winexe', '/optimize+', '/utf8output',
        '/win32manifest:AraControl.manifest', '/win32icon:assets\AraControl.ico', "/out:$Output")
    foreach ($icon in 'AraControl','TrayConnected','TrayPaused','TrayDisconnected') {
        $options += "/resource:assets\$icon.ico,$icon.ico"
    }
    & $compiler @options Core.cs MainWindow.cs AraIcons.cs Appearance.cs Rounded.cs ProfileFields.cs
    if ($LASTEXITCODE -ne 0) {throw 'Ara Control build failed.'}
} finally {Pop-Location}



