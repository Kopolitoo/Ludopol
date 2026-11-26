# Makes the installer Ludopol-<version>-setup.exe with Inno Setup (Install/Ludopol.iss), from
# the published program (dotnet publish, self-contained).
#
#   pwsh .github/scripts/make-setup.ps1 <published folder> <version> <destination folder>
#
# Inno Setup is installed on the GitHub machine if it is not there yet (with Chocolatey, a fixed
# version, as for Prunel). It is only needed to MAKE the installer: nothing to install on the
# user's computer. Used by the automatic build (build.yml) and by the release (release.yml).

param(
    [Parameter(Mandatory = $true)][string]$ProgramFolder,
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$Destination
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')

if (-not (Test-Path (Join-Path $ProgramFolder 'Ludopol.exe'))) {
    Write-Host "::error::Ludopol.exe not found in $ProgramFolder"
    exit 1
}

$iscc = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
if (-not (Test-Path $iscc)) {
    Write-Host 'Installing Inno Setup...'
    choco install innosetup --version=6.4.3 --yes --no-progress
    if ($LASTEXITCODE -ne 0) { Write-Host '::error::Inno Setup could not be installed.'; exit 1 }
}
if (-not (Test-Path $iscc)) { Write-Host "::error::ISCC.exe not found: $iscc"; exit 1 }

New-Item -ItemType Directory -Force $Destination | Out-Null
$programPath = (Resolve-Path $ProgramFolder).Path.TrimEnd('\')
$destinationPath = (Resolve-Path $Destination).Path.TrimEnd('\')

& $iscc "/DAppVersion=$Version" "/DProgramFolder=$programPath" "/DOutputFolder=$destinationPath" (Join-Path $root 'Install\Ludopol.iss')
if ($LASTEXITCODE -ne 0) { Write-Host '::error::Inno Setup could not make the installer.'; exit 1 }

$setup = Join-Path $destinationPath "Ludopol-$Version-setup.exe"
if (-not (Test-Path $setup)) { Write-Host "::error::Installer not found: $setup"; exit 1 }
Write-Host "OK: installer ready: $setup"
