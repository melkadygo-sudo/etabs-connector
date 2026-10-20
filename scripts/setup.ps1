param(
    [string]$EtabsPath,
    [string]$InstallRoot = "$env:LOCALAPPDATA\ETABS-Connector",
    [string]$SkillRoot = "$env:USERPROFILE\.agents\skills\etabs-connector",
    # Accepted for compatibility with older commands; no longer used.
    [string]$AllowedRoots,
    [switch]$SkipCodex,
    [switch]$UseLocalSdk,
    [switch]$NoDependencyInstall
)
$ErrorActionPreference = 'Stop'
$InstallRoot = [IO.Path]::GetFullPath($InstallRoot)
function Test-DotNetReady([string]$Executable) {
    if (-not (Test-Path -LiteralPath $Executable)) { return $false }
    $sdks = @(& $Executable --list-sdks)
    if ($LASTEXITCODE -ne 0) { return $false }
    $runtimes = @(& $Executable --list-runtimes)
    if ($LASTEXITCODE -ne 0) { return $false }
    return (@($sdks | Where-Object { $_ -match '^(8|9|[1-9][0-9]+)\.' }).Count -gt 0) -and
        (@($runtimes | Where-Object { $_ -match '^Microsoft.NETCore.App 8\.' }).Count -gt 0)
}
try {
    Write-Host 'StructLink for ETABS - guided setup' -ForegroundColor Cyan
    Write-Host 'Installs locally, builds the server, and connects it to Codex when available.'
    Write-Host 'New installations start read-only. Existing settings are preserved.'
    if (-not [Environment]::Is64BitOperatingSystem) { throw 'Windows x64 is required.' }
    if (-not $EtabsPath) {
        $candidates = @("$env:ProgramFiles\Computers and Structures\ETABS 23")
        foreach ($process in @(Get-Process ETABS -ErrorAction SilentlyContinue)) {
            try { if ($process.Path) { $candidates += Split-Path -Parent $process.Path } } catch {}
        }
        $EtabsPath = $candidates | Where-Object {
            (Test-Path -LiteralPath (Join-Path $_ 'ETABS.exe')) -and
            (Test-Path -LiteralPath (Join-Path $_ 'ETABSv1.dll')) -and
            ([Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $_ 'ETABS.exe')).FileMajorPart -eq 23)
        } | Select-Object -First 1
    }
    if (-not $EtabsPath) {
        Add-Type -AssemblyName System.Windows.Forms
        $picker = New-Object System.Windows.Forms.OpenFileDialog
        $picker.Title = 'Select ETABS.exe from your ETABS 23 installation'
        $picker.Filter = 'ETABS executable|ETABS.exe'
        if ($picker.ShowDialog() -ne 'OK') { throw 'ETABS selection cancelled. Nothing was installed.' }
        $EtabsPath = Split-Path -Parent $picker.FileName
        $picker.Dispose()
    }
    $etabsExe = Join-Path $EtabsPath 'ETABS.exe'
    if (-not (Test-Path -LiteralPath $etabsExe) -or -not (Test-Path -LiteralPath (Join-Path $EtabsPath 'ETABSv1.dll'))) { throw 'Choose a folder containing ETABS.exe and ETABSv1.dll.' }
    if ([Diagnostics.FileVersionInfo]::GetVersionInfo($etabsExe).FileMajorPart -ne 23) { throw 'This connector requires ETABS 23.' }
    Write-Host "ETABS found: $EtabsPath"

    $localSdk = Join-Path $InstallRoot 'runtime'
    $dotnet = Join-Path $localSdk 'dotnet.exe'
    if (-not $UseLocalSdk -and -not (Test-DotNetReady $dotnet)) { $dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe' }
    if (-not $UseLocalSdk -and -not (Test-Path -LiteralPath $dotnet)) {
        $found = Get-Command dotnet -ErrorAction SilentlyContinue
        if ($found) { $dotnet = $found.Source }
    }
    $ready = Test-DotNetReady $dotnet
    if (-not $ready) {
        if ($NoDependencyInstall) { throw '.NET SDK and .NET 8 runtime are missing.' }
        if ($UseLocalSdk -or -not (Get-Command winget -ErrorAction SilentlyContinue)) {
            Write-Host 'Downloading Microsoft .NET 8 SDK for this connector only.'
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            $metadata = Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/8.0/releases.json'
            $sdk = @($metadata.releases | ForEach-Object { $_.sdks } | Where-Object { $_.version -eq $metadata.'latest-sdk' }) | Select-Object -First 1
            $asset = $sdk.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.zip' } | Select-Object -First 1
            $uri = [Uri]$asset.url
            if (-not $asset -or $asset.hash -notmatch '^[a-fA-F0-9]{128}$' -or $uri.Scheme -ne 'https' -or $uri.Host -ne 'builds.dotnet.microsoft.com') { throw 'Unexpected Microsoft SDK metadata; download stopped.' }
            New-Item -ItemType Directory -Force -Path $localSdk | Out-Null
            $archive = Join-Path $localSdk ('sdk-' + [Guid]::NewGuid().ToString('N') + '.zip')
            try {
                $ProgressPreference = 'SilentlyContinue'
                Invoke-WebRequest -UseBasicParsing -Uri $uri.AbsoluteUri -OutFile $archive
                if ((Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash -ne $asset.hash) { throw '.NET SDK SHA-512 verification failed.' }
                Write-Host 'SDK checksum verified. Extracting local SDK...'
                Expand-Archive -LiteralPath $archive -DestinationPath $localSdk -Force
            } finally { if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive } }
            $dotnet = Join-Path $localSdk 'dotnet.exe'
        } else {
        Write-Host 'Installing Microsoft .NET 8 SDK through Windows Package Manager. Review any Windows or package prompts.'
        & winget install --id Microsoft.DotNet.SDK.8 --exact --source winget --architecture x64
        if ($LASTEXITCODE -eq 3010) { throw '.NET installation requires a Windows restart. Restart and run INSTALL.cmd again.' }
        if ($LASTEXITCODE -ne 0) { throw '.NET installation did not complete. Install the .NET 8 SDK and run setup again.' }
        $dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
        }
        if (-not (Test-DotNetReady $dotnet)) { throw '.NET SDK/runtime verification failed after installation. Restart Windows, check the .NET 8 SDK installation, then run setup again.' }
    }
    $env:PATH = (Split-Path -Parent $dotnet) + ';' + $env:PATH
    $env:DOTNET_ROOT = Split-Path -Parent $dotnet
    $env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
    $source = Split-Path -Parent $PSScriptRoot
    $InstallRoot = [IO.Path]::GetFullPath($InstallRoot)
    New-Item -ItemType Directory -Force -Path $InstallRoot | Out-Null
    if ($source.TrimEnd('\') -ne $InstallRoot.TrimEnd('\')) {
        foreach ($folder in @('scripts','skills','docs','.codex-plugin')) {
            Copy-Item -LiteralPath (Join-Path $source $folder) -Destination $InstallRoot -Recurse -Force
        }
        $targetSource = Join-Path $InstallRoot 'src\EtabsConnector'
        New-Item -ItemType Directory -Force -Path $targetSource | Out-Null
        Get-ChildItem -LiteralPath (Join-Path $source 'src\EtabsConnector') -File | Where-Object { $_.Extension -in @('.cs','.csproj') } | Copy-Item -Destination $targetSource -Force
        foreach ($file in @('README.md','LICENSE','.mcp.json','INSTALL.cmd')) {
            Copy-Item -LiteralPath (Join-Path $source $file) -Destination $InstallRoot -Force
        }
    }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $InstallRoot 'scripts\install-etabs-connector.ps1') -EtabsPath $EtabsPath
    if ($LASTEXITCODE -ne 0) { throw 'Build failed. Correct the error above and run setup again.' }
    # Persist the chosen runtime so later client launches do not depend on PATH.
    Set-Content -LiteralPath (Join-Path $InstallRoot 'runtime-path.txt') -Value $dotnet -Encoding UTF8
    $launcher = Join-Path $InstallRoot 'scripts\start-etabs-connector.ps1'
    $response = '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"get_capabilities","arguments":{}}}' | & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $launcher
    if ($LASTEXITCODE -ne 0) { throw 'Connector startup failed.' }
    $capabilities = $response | ConvertFrom-Json
    if ($capabilities.error -or -not $capabilities.result -or $capabilities.result.isError) { throw ('Connector capabilities check failed: ' + ($response -join ' ')) }

    $config = @{ mcpServers = @{ etabs23 = @{ command = 'powershell.exe'; args = @('-NoProfile','-ExecutionPolicy','Bypass','-File',$launcher) } } }
    $config | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $InstallRoot 'mcp-config.json') -Encoding UTF8
    if (-not $SkipCodex) {
        $codexCommand = Get-Command codex -ErrorAction SilentlyContinue
        $codexPath = if ($codexCommand) { $codexCommand.Source } else { Join-Path $env:LOCALAPPDATA 'Programs\OpenAI\Codex\bin\codex.exe' }
        if (Test-Path -LiteralPath $codexPath) {
            $ErrorActionPreference = 'Continue'
            $existingText = & $codexPath mcp get etabs23 --json 2>$null
            $existingExit = $LASTEXITCODE
            $ErrorActionPreference = 'Stop'
            if ($existingExit -eq 0) {
                $existing = $existingText | ConvertFrom-Json
                if ($existing.transport.command -eq 'powershell.exe' -and
                    (($existing.transport.args | ConvertTo-Json -Compress) -eq ($config.mcpServers.etabs23.args | ConvertTo-Json -Compress))) {
                    Write-Host 'Codex registration already points to this installation; verified.'
                } else {
                    Write-Warning 'An etabs23 server with different settings already exists in Codex; it was preserved. Use mcp-config.json to replace it before using this installation.'
                }
            } else {
                & $codexPath mcp add etabs23 -- powershell.exe -NoProfile -ExecutionPolicy Bypass -File $launcher
                if ($LASTEXITCODE -ne 0) { throw "Codex registration failed. Connector is built; use $InstallRoot\mcp-config.json for manual setup." }
                $registeredText = & $codexPath mcp get etabs23 --json
                if ($LASTEXITCODE -ne 0) { throw 'Could not verify Codex registration.' }
                $registered = $registeredText | ConvertFrom-Json
                if ($registered.transport.command -ne 'powershell.exe' -or
                    (($registered.transport.args | ConvertTo-Json -Compress) -ne ($config.mcpServers.etabs23.args | ConvertTo-Json -Compress))) {
                    throw 'Codex registration read-back did not match the intended launcher.'
                }
                Write-Host 'Codex registration command and arguments verified.'
            }
        } else {
            Write-Warning "Codex CLI was not found. Add the server in your MCP client using $InstallRoot\mcp-config.json."
        }
        if (-not (Test-Path -LiteralPath $SkillRoot)) {
            New-Item -ItemType Directory -Force -Path $SkillRoot | Out-Null
            Copy-Item -LiteralPath (Join-Path $InstallRoot 'skills\etabs-connector\SKILL.md') -Destination $SkillRoot
        } else { Write-Host 'Existing user skill preserved.' }
    }
    Write-Host "Connector installed and startup verified: $InstallRoot" -ForegroundColor Green
    Write-Host 'Restart Codex, open ETABS 23, and ask: Use StructLink for ETABS to inspect my model without changes.'
    Write-Host "Permissions: edit $InstallRoot\connector.settings.ps1 to enable writes and analysis, then restart your client."
    exit 0
} catch {
    Write-Host "SETUP FAILED: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
