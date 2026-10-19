$ErrorActionPreference = "Stop"
try {
    $PluginRoot = Split-Path -Parent $PSScriptRoot
    $Settings = Join-Path $PluginRoot "connector.settings.ps1"
    $Executable = Join-Path $PluginRoot "dist\EtabsConnector.exe"

    if (Test-Path $Settings) {
        . $Settings
    }

    if (-not (Test-Path $Executable)) {
        throw "ETABS Connector is not built. Run scripts\install-etabs-connector.cmd first."
    }

    & $Executable
    exit $LASTEXITCODE
}
catch {
    Write-Error "ETABS Connector failed to start: $($_.Exception.Message)"
    exit 1
}
