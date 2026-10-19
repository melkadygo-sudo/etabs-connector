param(
    [Parameter(Mandatory=$true)][string]$TestRoot,
    [string]$EtabsPath = 'C:\Program Files\Computers and Structures\ETABS 23'
)
# Integration test: real ETABS interop assembly and Codex CLI; no model connection.
# CODEX_HOME is set only in child processes, never in the caller's environment.
$ErrorActionPreference = 'Stop'
$TestRoot = [IO.Path]::GetFullPath($TestRoot)
if (Test-Path -LiteralPath $TestRoot) { throw 'Choose a NEW empty test directory.' }
New-Item -ItemType Directory -Path $TestRoot | Out-Null
$repo = Split-Path -Parent $PSScriptRoot
$testConfig = Join-Path $TestRoot 'codex-home'
New-Item -ItemType Directory -Path $testConfig | Out-Null
$codex = (Get-Command codex -ErrorAction Stop).Source
function Quote-PS([string]$Value) { return "'" + $Value.Replace("'", "''") + "'" }
function Run-Isolated([string]$Code, [string]$Label) {
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = 'powershell.exe'
    $info.Arguments = '-NoProfile -OutputFormat Text -ExecutionPolicy Bypass -EncodedCommand ' + [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($Code))
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.EnvironmentVariables['CODEX_HOME'] = $testConfig
    $p = [Diagnostics.Process]::Start($info)
    $stdout = $p.StandardOutput.ReadToEndAsync()
    $stderr = $p.StandardError.ReadToEndAsync()
    if (-not $p.WaitForExit(180000)) { $p.Kill(); throw "$Label timed out." }
    $stdout.Result | Set-Content -LiteralPath (Join-Path $TestRoot "$Label.stdout.txt")
    $stderr.Result | Set-Content -LiteralPath (Join-Path $TestRoot "$Label.stderr.txt")
    if ($p.ExitCode -ne 0) { throw "$Label failed ($($p.ExitCode)): $($stdout.Result) $($stderr.Result)" }
    return $stdout.Result
}
$install = Join-Path $TestRoot 'installed connector'
$skill = Join-Path $TestRoot 'user skill'
$setup = '& ' + (Quote-PS (Join-Path $repo 'scripts\setup.ps1')) + ' -InstallRoot ' + (Quote-PS $install) + ' -SkillRoot ' + (Quote-PS $skill) + ' -AllowedRoots ' + (Quote-PS (Join-Path $TestRoot 'models')) + ' -EtabsPath ' + (Quote-PS $EtabsPath) + ' -NoDependencyInstall'
Run-Isolated $setup 'fresh-install' | Out-Null
$get = '& ' + (Quote-PS $codex) + ' mcp get etabs23 --json; exit $LASTEXITCODE'
$registration = (Run-Isolated $get 'registration') | ConvertFrom-Json
$expectedArgs = @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $install 'scripts\start-etabs-connector.ps1'))
if ($registration.transport.command -ne 'powershell.exe' -or
    (($registration.transport.args | ConvertTo-Json -Compress) -ne ($expectedArgs | ConvertTo-Json -Compress))) { throw 'Registered launcher differs from expected.' }
if (-not (Test-Path -LiteralPath (Join-Path $skill 'SKILL.md'))) { throw 'Skill missing.' }
$settings = Join-Path $install 'connector.settings.ps1'
Add-Content -LiteralPath $settings -Value '# preservation marker'
$beforeSettings = (Get-FileHash -LiteralPath $settings).Hash
$beforeSkill = (Get-FileHash -LiteralPath (Join-Path $skill 'SKILL.md')).Hash
$beforeConfig = (Get-FileHash -LiteralPath (Join-Path $testConfig 'config.toml')).Hash
Run-Isolated $setup 'repeat-install' | Out-Null
if ($beforeSettings -ne (Get-FileHash -LiteralPath $settings).Hash -or
    $beforeSkill -ne (Get-FileHash -LiteralPath (Join-Path $skill 'SKILL.md')).Hash -or
    $beforeConfig -ne (Get-FileHash -LiteralPath (Join-Path $testConfig 'config.toml')).Hash) { throw 'Repeat installation changed existing configuration.' }

# Launch the exact command and arguments read back from the real Codex configuration.
$launch = $registration.transport.command
$arguments = @($registration.transport.args)
$responses = @(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'mcp-smoke.ndjson') | & $launch @arguments)
if ($LASTEXITCODE -ne 0) { throw 'Registered launch failed.' }
$parsed = @($responses | ForEach-Object { $_ | ConvertFrom-Json })
foreach ($id in @(1,2,3)) {
    $response = $parsed | Where-Object id -eq $id
    if (-not $response -or $response.error -or $response.result.isError) { throw "MCP response $id failed." }
}
$capabilityResponse = $parsed | Where-Object id -eq 3
$capabilities = $capabilityResponse.result.content[0].text | ConvertFrom-Json
if ($capabilities.writes_enabled -ne $false -or $capabilities.analysis_enabled -ne $false) { throw 'Fresh installation is not read-only.' }
$savedEncoding = $OutputEncoding
try {
    $OutputEncoding = New-Object Text.UTF8Encoding($false)
    $bomRequest = [string][char]0xFEFF + '{"jsonrpc":"2.0","id":91,"method":"ping"}'
    $bomResponse = ($bomRequest | & $launch @arguments) | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $bomResponse.id -ne 91 -or $bomResponse.error) { throw 'UTF-8 BOM input was not accepted.' }
} finally { $OutputEncoding = $savedEncoding }

# A separately configured server must not be replaced by a repeat installation.
$conflict = '& ' + (Quote-PS $codex) + ' mcp add etabs23 -- powershell.exe -NoProfile -Command Write-Output; exit $LASTEXITCODE'
Run-Isolated $conflict 'create-conflict' | Out-Null
$conflictHash = (Get-FileHash -LiteralPath (Join-Path $testConfig 'config.toml')).Hash
$conflictOutput = Run-Isolated $setup 'preserve-conflict'
$conflictOutput += Get-Content -Raw -LiteralPath (Join-Path $TestRoot 'preserve-conflict.stderr.txt')
if ($conflictHash -ne (Get-FileHash -LiteralPath (Join-Path $testConfig 'config.toml')).Hash -or $conflictOutput -notmatch 'different settings') { throw 'Conflicting registration was not preserved and reported.' }
Write-Host 'PASS: fresh registration, exact read-back, installed skill, registered MCP launch, UTF-8 BOM input, read-only defaults, repeat install, conflicting registration preservation.'
Write-Host 'This is an isolated-configuration test on an existing Windows host, not a clean-OS certification.'
