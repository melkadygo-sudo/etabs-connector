# StructLink for ETABS Setup

## Requirements

* Windows (64 bit)
* ETABS (23), installed and licensed
* .NET (8) SDK for installation
* Codex or another MCP client that supports local standard input and output servers

The connector uses ETABS COM automation supplied with ETABS (23). This source package does not redistribute CSI assemblies. Local compilation copies the installed interop assembly into the build output; do not publish that output.

## Recommended installation

Download and extract the repository ZIP, then double-click the root **INSTALL.cmd**. It installs into `%LOCALAPPDATA%\ETABS-Connector`, detects ETABS (or asks you to select it), handles missing .NET through WinGet or a verified SDK ZIP from Microsoft, builds and checks startup, and configures Codex when its CLI is available. Windows/package prompts remain interactive. New installations start read-only; existing settings and client registrations are preserved. See the README for exceptions and enabling changes.

## Manual install

If WinGet is unavailable, the guided installer downloads a .NET 8 SDK ZIP from Microsoft's release metadata and verifies its SHA-512 checksum before extraction. It stores the SDK in the installation's `runtime` folder. You can explicitly choose this per-installation SDK with `scripts\setup.ps1 -UseLocalSdk`; it does not replace system .NET. Internet access and working DNS are required. If a download fails, correct connectivity and rerun setup.

The generated `runtime-path.txt` records the selected runtime for future launches. Do not publish this file or the `runtime` folder. Keep an externally selected SDK installed while the connector uses it; otherwise rerun setup to select or install a working runtime.

The legacy launcher below enables writes and analysis on first installation:

```text
scripts\install-etabs-connector.cmd
```

This launcher enables read, write, and analysis access, displays each installation check, and keeps the window open if an error occurs.

For a customised installation, open PowerShell in the plugin folder and run:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\install-etabs-connector.ps1 `
  -AllowedRoots "C:\ETABS-MCP" `
  -EnableWrites `
  -EnableAnalysis
```

Choose an allowed root that contains the models you intend to open and the working copies you intend to create. Multiple roots can be set later in `connector.settings.ps1` using semicolon separation.

The installer publishes the .NET application into `dist` and creates a local settings file. Do not share `connector.settings.ps1` if it contains confidential paths. For a non-default ETABS installation, pass `-EtabsPath "D:\Applications\ETABS 23"`; that folder must contain ETABS.exe and ETABSv1.dll. The diagnostic script still checks the default ETABS executable location.

See the README for registering the local stdio server with your MCP client. Installing the skill alone does not install or configure the server.

## Confirm the installation

Open ETABS (23), then double click:

```text
scripts\test-etabs-connector.cmd
```

The diagnostic window remains open and checks:

* the .NET command and SDK
* the ETABS (23) executable
* ETABS API registration
* connector settings
* the built connector
* a basic MCP capabilities response
* whether ETABS is running

Do not double click `start-etabs-connector.ps1` as a normal application. It is a standard input and output server intended to be started by the plugin host, so it does not display a graphical interface.

## Safety model

The connector starts with writes and analysis disabled unless the installer switches enable them.

Model changes and analysis require all of the following:

1. Write permission is enabled.
2. The connector is attached to ETABS (23).
3. Analysis permission is also enabled when running analysis.

The skill asks whether to save a working copy or reuse the open model. Creating a copy is optional; the server does not enforce it as a write prerequisite.

For an authorized write or analysis operation, the connector automatically unlocks the selected model when required. It restricts file open and save operations to configured roots and limits extracted rows.

## First test

1. Open ETABS (23) manually.
2. Run `test-etabs-connector.cmd` and resolve any failed checks.
3. Open a noncritical test model.
4. Start a new Codex thread with the plugin enabled.
5. Ask: “Use StructLink for ETABS to report capabilities and inspect the active model. Do not make changes.”
6. Confirm the returned path, version, units, object counts, and load definitions.
7. Choose whether to save a working copy into the allowed root or reuse the open model.
8. Test one harmless assignment on the selected model, then confirm it manually in ETABS.

Do not begin with a live project model. Complete read, write, analysis, and result extraction tests on a disposable model first.

## Supported tools

* `get_capabilities`
* `connect`
* `open_model`
* `get_model_info`
* `list_load_definitions`
* `list_objects`
* `list_properties`
* `get_frame_section`
* `get_area_property`
* `get_point_restraint`
* `save_working_copy`
* `set_present_units`
* `set_frame_section`
* `set_area_property`
* `set_point_restraint`
* `run_analysis`
* `get_joint_reactions`
* `get_frame_forces`

## Known limitations

The generic gateway can expose operations beyond the purpose-built tools, including destructive operations. Method classification and file-path checks are heuristic, not a complete sandbox. Generic calls require corresponding read-back checks; the connector does not verify every API operation automatically.

Compile and test on the target ETABS computer. Exact API compatibility must be confirmed against the API help installed with that ETABS build. The included MCP smoke fixture checks startup and tool discovery, not model mutations or structural correctness.

The generic API gateway exposes `list_api_methods`, `describe_api_method`, and `invoke_api_method` over the installed ETABS 23 interop assembly. Purpose-built tools remain preferred where read-back verification is available.
