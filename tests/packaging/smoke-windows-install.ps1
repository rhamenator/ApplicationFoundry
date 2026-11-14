param([Parameter(Mandatory = $true)][string]$InstallerPath)

$installer = (Resolve-Path $InstallerPath).Path
$installPath = Join-Path $env:RUNNER_TEMP 'af-installed'
$install = Start-Process -FilePath $installer -ArgumentList @(
    '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/DIR=$installPath"
) -PassThru -Wait
if ($install.ExitCode -ne 0) {
    throw "Installer exited with code $($install.ExitCode)."
}

$app = Join-Path $installPath 'ApplicationFoundry.Desktop.exe'
$shortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Application Foundry\Application Foundry.lnk'
if (-not (Test-Path $app)) {
    throw "The installed desktop executable is missing."
}
if (-not (Test-Path $shortcut)) {
    throw "The Start menu shortcut is missing after install."
}
if (Test-Path (Join-Path $installPath 'ApplicationFoundry.exe')) {
    throw 'The legacy console/server executable was included in the desktop installer.'
}
& "$PSScriptRoot/assert-windows-gui-subsystem.ps1" $app

$errorPath = Join-Path $env:RUNNER_TEMP 'application-foundry-installed-self-test.stderr'
$selfTest = Start-Process -FilePath $app -ArgumentList '--self-test' `
    -RedirectStandardError $errorPath -PassThru -Wait
if ($selfTest.ExitCode -ne 0) {
    if (Test-Path $errorPath) { Get-Content $errorPath | Select-Object -First 30 }
    throw "Installed desktop startup self-test failed with code $($selfTest.ExitCode)."
}

$uninstaller = Get-ChildItem $installPath -Filter 'unins*.exe' | Select-Object -First 1
if ($null -eq $uninstaller) {
    throw "The installer did not provide an uninstaller."
}
$uninstall = Start-Process -FilePath $uninstaller.FullName -ArgumentList @(
    '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART'
) -PassThru -Wait
if ($uninstall.ExitCode -ne 0) {
    throw "Uninstaller exited with code $($uninstall.ExitCode)."
}
if (Test-Path $app) {
    throw "The desktop executable remains after uninstall."
}
if (Test-Path $shortcut) {
    throw "The Start menu shortcut remains after uninstall."
}
Write-Host 'Standard Windows desktop installer installed, started, and uninstalled successfully.'
