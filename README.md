# ETABS Connector

**Connect Codex to ETABS 23. Inspect models, make controlled changes, and retrieve analysis results through natural-language requests.**

Built for structural engineers working with ETABS on Windows. Includes a local MCP server, a Codex skill, a guided installer, and source code. MCP (Model Context Protocol) lets your AI assistant call tools exposed by the connector.

**Windows x64 · ETABS 23 · Version 0.1.3 · MIT license**

[**Download ZIP**](https://github.com/melkadygo-sudo/etabs-connector/archive/refs/heads/main.zip) · [Setup guide](docs/SETUP.md) · [Test results](docs/VALIDATION.md) · [Report an issue](https://github.com/melkadygo-sudo/etabs-connector/issues)

## What you can do

| Workflow | Examples |
| --- | --- |
| Inspect a model | Check model information, units, objects, properties, and load definitions. |
| Review assignments | Read frame sections, area properties, and point restraints. |
| Make controlled changes | Save a working copy and update supported assignments with write access enabled. |
| Run analysis | Start analysis with write and analysis permissions enabled. |
| Retrieve results | Extract selected joint reactions and frame forces for review. |
| Explore the ETABS API | Discover and describe methods exposed by your installed ETABS 23 API. |

**New installations start read-only.** Enable model changes and analysis explicitly when you need them.

## Get started in three steps

### 1. Download and extract

[**Download ETABS Connector**](https://github.com/melkadygo-sudo/etabs-connector/archive/refs/heads/main.zip), then choose **Extract all**.

Before installing, you need:

- **Windows x64** and a licensed installation of **ETABS 23**.
- **Codex**, with its CLI available for automatic registration, or another MCP client that supports local stdio servers.
- **Internet access** for dependency downloads and build restore.

The installer handles the .NET 8 SDK when it is missing. ETABS and its license are not included.

### 2. Run the installer

Open the extracted folder and double-click **`INSTALL.cmd`**. Follow any Windows or dependency installation prompts.

The installer detects ETABS, builds and checks the connector, registers it with Codex when its CLI is available, and installs the skill. No terminal commands or configuration editing are needed for a new Codex setup with its CLI available.

> Run `INSTALL.cmd` from the extracted folder, not from inside the ZIP. If Codex CLI is unavailable or an existing `etabs23` registration points elsewhere, setup explains the remaining manual step. Existing settings and skills are preserved.

### 3. Open ETABS and try your first request

Restart Codex, open ETABS 23 with a test model, and send:

```text
Use ETABS Connector to inspect the active model without making changes.
Report the model path, ETABS version, units, and load definitions.
```

Compare the response with the model open in ETABS before continuing.

## Example requests

**Review the model**

```text
List the frame sections assigned in the active model. Do not change anything.
```

**Change an assignment** — requires write permission

```text
Save a working copy of this model in my allowed folder. Ask me which frame
and section to use, update that assignment, and read it back to verify it.
```

**Retrieve analysis results** — requires write and analysis permissions

```text
Ask me which load combination to review, run analysis, and extract the
joint reactions for that combination. Include the units in the output.
```

## Enable editing and analysis

The guided installer saves the connector in:

```text
%LOCALAPPDATA%\ETABS-Connector
```

Open `connector.settings.ps1` in that folder. Set the permissions you need to `'true'`, then restart your MCP client:

```powershell
$env:ETABS_CONNECTOR_ALLOW_WRITES = 'true'
$env:ETABS_CONNECTOR_ALLOW_ANALYSIS = 'true'
```

Configure `ETABS_CONNECTOR_ALLOWED_ROOTS` for the folders containing your models and working copies. See the [setup guide](docs/SETUP.md) for custom paths and permissions.

## What has been tested

Version 0.1.3 passed installer and registration checks on the host and in a fresh Windows Sandbox session.

| Check | Result |
| --- | --- |
| Download and verify .NET when both .NET and WinGet are absent | Passed in Windows Sandbox |
| Build the connector and check server startup | Passed |
| Register with the real Codex CLI and verify the registered command | Passed |
| Install the skill and launch the registered MCP server | Passed |
| Preserve existing settings and conflicting registrations | Passed |
| Start with writes and analysis disabled | Passed |

**Test scope:** ETABS files and Codex CLI binaries were supplied to the Sandbox through read-only mapped folders. Fresh ETABS/Codex desktop installation, ETABS activation, COM attachment, and model analysis were not tested there. The WinGet/UAC/reboot path and custom ETABS file picker remain untested.

Read the [validation report](docs/VALIDATION.md) for the procedure and remaining checks.

## Working with engineering models

- Choose whether to save a working copy or use the open model before making changes. The connector can modify the open model directly when writes are enabled.
- The connector automatically unlocks the model when required. Unlocking can invalidate analysis results.
- Review assignments, units, loads, and results in ETABS. Successful solver completion does not establish engineering correctness.
- Allowed folders constrain supported file operations. Generic API classification and path checks use heuristics; they are not a complete sandbox for every API side effect. Review generic methods and verify their results explicitly.

ETABS automation runs locally. Model information returned to your AI client is handled under that client's data policies.

## Other MCP clients and manual setup

The connector supports clients that launch a local stdio MCP server. Installing the skill alone does not install or configure the server.

<details>
<summary><strong>Show manual installation and client configuration</strong></summary>

Install the .NET 8 SDK, extract this repository, and open PowerShell in the extracted folder:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\install-etabs-connector.ps1 -AllowedRoots "C:\ETABS-MCP"
```

This command starts with writes and analysis disabled. Existing settings are preserved. The legacy `scripts/install-etabs-connector.cmd` enables writes and analysis on first installation; use the root `INSTALL.cmd` for the recommended read-only setup.

Register the server in your client, replacing the sample path with your actual repository path:

```json
{
  "mcpServers": {
    "etabs23": {
      "command": "powershell.exe",
      "args": [
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", "C:\\tools\\etabs-connector\\scripts\\start-etabs-connector.ps1"
      ]
    }
  }
}
```

Client configuration formats vary. The command and arguments describe the local server. The package also includes `.mcp.json`, `.codex-plugin/plugin.json`, and the [Codex skill](skills/etabs-connector/SKILL.md).

Open ETABS with a disposable test model and run `scripts\test-etabs-connector.cmd`. Follow the [setup guide](docs/SETUP.md) for custom ETABS paths and troubleshooting.

</details>

## Contributing and support

[Open an issue](https://github.com/melkadygo-sudo/etabs-connector/issues) with your Windows version, ETABS build, connector version, tool name, and a redacted error message. Suggestions and pull requests are welcome.

Do not upload confidential models, credentials, personal paths, or licensed CSI binaries.

<details>
<summary><strong>Show developer build and smoke test</strong></summary>

```powershell
dotnet publish .\src\EtabsConnector\EtabsConnector.csproj -c Release -r win-x64 --self-contained false -o .\dist
Get-Content .\tests\mcp-smoke.ndjson | .\dist\EtabsConnector.exe
```

The smoke test checks initialization, tool discovery, and capabilities without connecting to a model. ETABS workflows require separate testing on disposable models. Not every discoverable API method has been tested.

Building locally copies the installed ETABS interop assembly into the build output. Do not redistribute that output with this source package.

</details>

## Author and license

Created by **Mohamed Elkady**. Connector source and skill are available under the [MIT License](LICENSE).

This is an independent project, not affiliated with or endorsed by Computers and Structures, Inc. or OpenAI. ETABS and CSI assemblies are subject to their own licenses and are not included.
