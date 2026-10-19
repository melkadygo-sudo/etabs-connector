param(
    [switch]$Pause
)

$ErrorActionPreference = "Stop"
$Failed = $false

function Show-Check {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Detail
    )

    if ($Passed) {
        Write-Host "[PASS] $Name" -ForegroundColor Green
    }
    else {
        Write-Host "[FAIL] $Name" -ForegroundColor Red
        $script:Failed = $true
    }
    if ($Detail) {
        Write-Host "       $Detail"
    }
}

try {
    Write-Host ""
    Write-Host "ETABS Connector (0.1.1) Diagnostic Test" -ForegroundColor Cyan
    Write-Host "=======================================" -ForegroundColor Cyan

    $PluginRoot = Split-Path -Parent $PSScriptRoot
    $Settings = Join-Path $PluginRoot "connector.settings.ps1"
    $Executable = Join-Path $PluginRoot "dist\EtabsConnector.exe"
    $EtabsExecutable = "C:\Program Files\Computers and Structures\ETABS 23\ETABS.exe"

    $DotNet = Get-Command dotnet -ErrorAction SilentlyContinue
    Show-Check ".NET command" ($null -ne $DotNet) $(if ($DotNet) { (& dotnet --version) } else { "Install the .NET 8 SDK." })

    Show-Check "ETABS (23) installation" (Test-Path $EtabsExecutable) $EtabsExecutable

    $HelperType = [System.Type]::GetTypeFromProgID("ETABSv1.Helper")
    Show-Check "ETABS API registration" ($null -ne $HelperType) "COM class: ETABSv1.Helper"

    Show-Check "Connector settings" (Test-Path $Settings) $Settings
    if (Test-Path $Settings) {
        . $Settings
        Write-Host "       Allowed roots: $env:ETABS_CONNECTOR_ALLOWED_ROOTS"
        Write-Host "       Writes enabled: $env:ETABS_CONNECTOR_ALLOW_WRITES"
        Write-Host "       Analysis enabled: $env:ETABS_CONNECTOR_ALLOW_ANALYSIS"
    }

    Show-Check "Built connector" (Test-Path $Executable) $Executable

    if (Test-Path $Executable) {
        $Request = '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"get_capabilities","arguments":{}}}'
        $ResponseText = $Request | & $Executable
        $ExitCode = $LASTEXITCODE
        $ValidResponse = $false
        if ($ExitCode -eq 0 -and $ResponseText) {
            try {
                $Response = $ResponseText | ConvertFrom-Json
                $ValidResponse = ($Response.jsonrpc -eq "2.0" -and $null -ne $Response.result)
            }
            catch {
                $ValidResponse = $false
            }
        }
        Show-Check "MCP response" $ValidResponse $(if ($ValidResponse) { "The connector returned a valid capabilities response." } else { "Connector exit code: $ExitCode" })
    }

    $EtabsProcess = Get-Process ETABS -ErrorAction SilentlyContinue
    Show-Check "Running ETABS process" ($null -ne $EtabsProcess) $(if ($EtabsProcess) { "ETABS is running." } else { "Open ETABS (23) before the first connection test." })

    Write-Host ""
    if ($Failed) {
        Write-Host "One or more checks failed. Correct the items shown above." -ForegroundColor Red
        exit 1
    }

    Write-Host "All local diagnostic checks passed." -ForegroundColor Green
    Write-Host "The next step is to enable the plugin and inspect a disposable ETABS model."
    exit 0
}
catch {
    Write-Host ""
    Write-Host "DIAGNOSTIC TEST FAILED" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
finally {
    if ($Pause) {
        Write-Host ""
        Read-Host "Press Enter to close"
    }
}
