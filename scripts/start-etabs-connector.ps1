$ErrorActionPreference = "Stop"
try {
    $PluginRoot = Split-Path -Parent $PSScriptRoot
    $Settings = Join-Path $PluginRoot "connector.settings.ps1"
    $Executable = Join-Path $PluginRoot "dist\EtabsConnector.exe"

    if (Test-Path $Settings) {
        . $Settings
    }

    if (-not (Test-Path $Executable)) {
        throw "StructLink for ETABS is not built. Run scripts\install-etabs-connector.cmd first."
    }

    $LocalDotNet = Join-Path $PluginRoot 'runtime\dotnet.exe'
    $RuntimePathFile = Join-Path $PluginRoot 'runtime-path.txt'
    if (-not (Test-Path -LiteralPath $LocalDotNet) -and (Test-Path -LiteralPath $RuntimePathFile)) {
        $LocalDotNet = (Get-Content -Raw -LiteralPath $RuntimePathFile).Trim()
        if (-not (Test-Path -LiteralPath $LocalDotNet)) { throw 'Configured .NET runtime is missing. Run INSTALL.cmd again.' }
    }
    if (Test-Path -LiteralPath $LocalDotNet) {
        & $LocalDotNet (Join-Path $PluginRoot 'dist\EtabsConnector.dll')
    } else {
        & $Executable
    }
    exit $LASTEXITCODE
}
catch {
    Write-Error "StructLink for ETABS failed to start: $($_.Exception.Message)"
    exit 1
}
