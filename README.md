# ETABS Connector

Connect an AI assistant to a local ETABS 23 model through MCP. Includes a Codex skill, a Windows .NET server, installation scripts, and source code.

Inspect models, read assignments, change sections and restraints, run analysis, and extract selected results. The generic API gateway discovers and invokes additional methods exposed by the locally installed ETABS API.

## Requirements

- Windows x64 with a licensed installation of ETABS 23.
- .NET 8 SDK and runtime.
- An MCP client that supports local stdio servers.

ETABS runs on your computer. This package does not supply ETABS, a license, or CSI assemblies. Building locally copies the installed interop assembly into the local build output; do not redistribute that output with this source package.

## Quick start

1. Download this repository as a ZIP and extract it, or clone it.
2. Open PowerShell in the extracted repository and install:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\install-etabs-connector.ps1 -AllowedRoots "C:\ETABS-MCP"
```

This command leaves writes and analysis disabled. To enable them, rerun with `-EnableWrites -EnableAnalysis`. The double-click `.cmd` installer enables both.

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

```powershell
dotnet publish .\src\EtabsConnector\EtabsConnector.csproj -c Release -r win-x64 --self-contained false -o .\dist
Get-Content .\tests\mcp-smoke.ndjson | .\dist\EtabsConnector.exe
```

The smoke fixture checks initialization, tool listing, and capabilities without connecting to or changing a model. ETABS-dependent workflows need separate testing on disposable models. No claim is made that every discoverable API method has been tested.

When reporting an issue, include the ETABS build, Windows version, connector version, tool name, and a redacted error. Do not upload confidential models, personal paths, credentials, or licensed CSI binaries.

Independent project by Mohamed Elkady; not affiliated with or endorsed by Computers and Structures, Inc. or OpenAI.

## License

Connector source and skill: [MIT](LICENSE). ETABS and CSI assemblies remain subject to their own licenses and are not included.
