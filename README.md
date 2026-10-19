# ETABS Connector

Connect an AI assistant to a local ETABS 23 model through MCP. Includes a Codex skill, a Windows .NET server, installation scripts, and source code.

Inspect models, read assignments, change sections and restraints, run analysis, and extract selected results. The generic API gateway discovers and invokes additional methods exposed by the locally installed ETABS API.

## Requirements

- Windows x64 with a licensed installation of ETABS 23.
- .NET 8 SDK and runtime.
- An MCP client that supports local stdio servers.

ETABS runs on your computer. This package does not supply ETABS, a license, or CSI assemblies. Building locally copies the installed interop assembly into the local build output; do not redistribute that output with this source package.

## Easy installation (Windows)

1. [Download ZIP](https://github.com/melkadygo-sudo/etabs-connector/archive/refs/heads/main.zip) and choose **Extract all**.
2. Open the extracted folder and double-click **INSTALL.cmd**.
3. Follow any Windows/.NET prompts, then restart Codex and open ETABS 23.

No terminal commands or configuration editing are needed for a new Codex setup when its CLI is available. The installer detects ETABS 23 (or opens a file picker), installs the .NET 8 SDK through Microsoft's WinGet package if needed, builds into `%LOCALAPPDATA%\ETABS-Connector`, checks server startup, registers the `etabs23` MCP server, and installs the user skill. Internet access is needed for missing dependencies and build restore. ETABS must already be installed and licensed.

New installations start read-only. Existing settings, skills, and an existing `etabs23` registration are preserved. If Codex CLI is unavailable or an existing server points elsewhere, setup reports the remaining manual step and provides `mcp-config.json` in the installation folder for your client. Windows security or administrator prompts may still require a click; this is not a silent installer. Run only the extracted package, not INSTALL.cmd alone inside a ZIP.

To enable model edits later, open `%LOCALAPPDATA%\ETABS-Connector\connector.settings.ps1`, change the write and analysis values to `true`, and restart the client.

Ask: **Use ETABS Connector to inspect my model without changes.**

## Manual installation / other MCP clients

1. Download this repository as a ZIP and extract it, or clone it.
2. Open PowerShell in the extracted repository and install:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\install-etabs-connector.ps1 -AllowedRoots "C:\ETABS-MCP"
```

This command leaves writes and analysis disabled. On a first manual installation, add `-EnableWrites -EnableAnalysis` to enable them. Existing settings are preserved; edit `connector.settings.ps1` to change permissions. The older `scripts/install-etabs-connector.cmd` enables both on a first install; the recommended root `INSTALL.cmd` starts read-only.

3. Register the server in your MCP client using the settings below, replacing the sample absolute path with your extracted repository path:

```json
{
  "mcpServers": {
    "etabs23": {
      "command": "powershell.exe",
      "args": ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "C:\\tools\\etabs-connector\\scripts\\start-etabs-connector.ps1"]
    }
  }
}
```

Client configuration formats differ; the command and arguments above describe the local stdio server. The included `.mcp.json` and `.codex-plugin/plugin.json` provide the existing plugin packaging. The skill is in [skills/etabs-connector/SKILL.md](skills/etabs-connector/SKILL.md); it requires the server.

4. Open ETABS 23 with a disposable model, then run `scripts\test-etabs-connector.cmd`.
5. Ask your assistant: **Use ETABS Connector to report capabilities and inspect the active model. Do not make changes.**

See [setup and troubleshooting](docs/SETUP.md) for custom ETABS paths and permissions.

## Example requests

- Inspect the active model and report its path, ETABS version, units, and load definitions.
- Save a working copy, change the section of the specified frame, and verify the assignment.
- Run analysis and extract joint reactions for the specified load combination, with units.
- Find and describe an API method before invoking it.

## Model handling

The skill asks whether to create a working copy or reuse the open model before changes. With writes enabled, the server can modify the open model directly and automatically unlock it when needed. Unlocking can invalidate analysis results.

Allowed roots constrain supported file operations. They are not a sandbox for every possible ETABS API side effect. Generic API classification and path checks use naming heuristics; engineers must inspect the chosen method, parameters, and results. Generic calls do not provide the same read-back verification as purpose-built tools.

Solver completion is not engineering validation. Review assignments, units, loads, results, and design assumptions in ETABS before relying on them.

## Development and contributions

Version 0.1.2 includes a real-Codex installer integration test. See [validation results and remaining clean-machine checks](docs/VALIDATION.md).

```powershell
dotnet publish .\src\EtabsConnector\EtabsConnector.csproj -c Release -r win-x64 --self-contained false -o .\dist
Get-Content .\tests\mcp-smoke.ndjson | .\dist\EtabsConnector.exe
```

The smoke fixture checks initialization, tool listing, and capabilities without connecting to or changing a model. ETABS-dependent workflows need separate testing on disposable models. No claim is made that every discoverable API method has been tested.

When reporting an issue, include the ETABS build, Windows version, connector version, tool name, and a redacted error. Do not upload confidential models, personal paths, credentials, or licensed CSI binaries.

Independent project by Mohamed Elkady; not affiliated with or endorsed by Computers and Structures, Inc. or OpenAI.

## License

Connector source and skill: [MIT](LICENSE). ETABS and CSI assemblies remain subject to their own licenses and are not included.
