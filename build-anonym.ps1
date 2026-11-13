# Baut die anonyme Ausgabe: StepRecorder.exe ohne Namen des Urhebers, ohne Projektseite und ohne Update-Suche.
# Aufruf:  powershell -ExecutionPolicy Bypass -File build-anonym.ps1 [-Out <Ordner>]
param([string]$Out = "$env:LOCALAPPDATA\StepRecorder-anonym")
$ErrorActionPreference = 'Stop'
dotnet publish "$PSScriptRoot\src\StepRecorder.App\StepRecorder.csproj" -c Release -p:Anonymous=true -o $Out -nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Fertig: $Out\StepRecorder.exe"
