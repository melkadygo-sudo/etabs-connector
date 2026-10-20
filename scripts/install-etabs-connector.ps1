param(
    [string]$EtabsPath = "C:\Program Files\Computers and Structures\ETABS 23",
    [string]$AllowedRoots = "$env:USERPROFILE\Documents\ETABS-MCP",
    [switch]$EnableWrites,
    [switch]$EnableAnalysis,
    [switch]$Pause
)

$ErrorActionPreference = "Stop"
try {
    Write-Host ""
    Write-Host "StructLink for ETABS (0.1.3) Installation" -ForegroundColor Cyan
    Write-Host "====================================" -ForegroundColor Cyan

    $PluginRoot = Split-Path -Parent $PSScriptRoot
    $Project = Join-Path $PluginRoot "src\EtabsConnector\EtabsConnector.csproj"
    $Output = Join-Path $PluginRoot "dist"
    $Settings = Join-Path $PluginRoot "connector.settings.ps1"

    Write-Host "[1/5] Checking ETABS (23)..."
    if (-not (Test-Path (Join-Path $EtabsPath "ETABS.exe"))) {
        throw "ETABS 23 was not found at: $EtabsPath"
    }
    Write-Host "      Found: $EtabsPath" -ForegroundColor Green

    Write-Host "[2/5] Checking the .NET SDK..."
    $DotNet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $DotNet) {
        throw ".NET 8 SDK is required. Install the SDK, not only the runtime, then run this installer again."
    }

    $SdkVersion = & dotnet --version
    if ($LASTEXITCODE -ne 0) {
        throw "The dotnet command failed with exit code $LASTEXITCODE."
    }
    Write-Host "      Found SDK: $SdkVersion" -ForegroundColor Green
    if (-not $SdkVersion.StartsWith("8.")) {
        Write-Warning "The active SDK is $SdkVersion. The project targets .NET 8."
    }

    Write-Host "[3/5] Preparing the allowed model folder..."
    New-Item -ItemType Directory -Force -Path $AllowedRoots | Out-Null
    Write-Host "      Allowed root: $AllowedRoots" -ForegroundColor Green

    Write-Host "[4/5] Building the connector..."
    & dotnet publish $Project -c Release -r win-x64 --self-contained false -o $Output "-p:EtabsPath=$EtabsPath"
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    Write-Host "[5/5] Writing connector settings..."
    $WriteValue = if ($EnableWrites) { "true" } else { "false" }
    $AnalysisValue = if ($EnableAnalysis) { "true" } else { "false" }
    $SettingsContent = @"
`$env:ETABS_CONNECTOR_ALLOWED_ROOTS = '$($AllowedRoots.Replace("'", "''"))'
`$env:ETABS_CONNECTOR_ALLOW_WRITES = '$WriteValue'
`$env:ETABS_CONNECTOR_ALLOW_ANALYSIS = '$AnalysisValue'
`$env:ETABS_CONNECTOR_MAX_RESULT_ROWS = '10000'
"@
    if (Test-Path -LiteralPath $Settings) {
        Write-Host 'Existing connector.settings.ps1 preserved. Edit it to change permissions.'
    } else {
        Set-Content -Path $Settings -Value $SettingsContent -Encoding UTF8
    }

    Write-Host ""
    Write-Host "Installation completed successfully." -ForegroundColor Green
    Write-Host "Permission settings: $Settings"
    Write-Host ""
    Write-Host "Next: run test-etabs-connector.cmd while ETABS (23) is open."
    exit 0
}
catch {
    Write-Host ""
    Write-Host "INSTALLATION FAILED" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "The connector has not been confirmed as installed."
    exit 1
}
finally {
    if ($Pause) {
        Write-Host ""
        Read-Host "Press Enter to close"
    }
}
