# Installer validation — 0.1.2

Tested on 2026-10-19 using Windows 11 Pro, Codex CLI 0.145.0, .NET SDK 9.0.306 with the .NET 8 runtime, and locally installed ETABS 23.

## Passed

- Fresh installation into a new folder, including a path containing spaces.
- Automatic registration with the real Codex CLI using an empty, isolated configuration directory.
- Read-back comparison of the registered command and every argument.
- User skill installation into an isolated skill directory.
- Launching the exact command retrieved from Codex and checking MCP initialization, tool discovery, and capabilities.
- Read-only defaults (writes and analysis disabled).
- Repeat installation preserves settings, user skill, and Codex configuration byte-for-byte.
- A pre-existing server with a different command is preserved and a warning is emitted.

The test does not connect to an ETABS model or run structural analysis. It uses the locally licensed ETABS interop assembly for compilation and server startup.

## Reproduce

Install the prerequisites and ensure `codex` is available on PATH. From the repository, run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\test-installation.ps1 -TestRoot "C:\ETABS-Installer-Test-New"
```

The test requires a new directory. It sets `CODEX_HOME` only in child processes so the user's normal Codex configuration is not modified. Test logs and local build outputs stay inside the test directory; do not publish them because they contain local paths and licensed interop binaries.

## Not yet verified on a clean Windows installation

The host already has .NET and ETABS. The checks above are not clean-OS certification. Missing-.NET download/installation through WinGet, UAC/reboot handling, a custom ETABS file-picker selection, and a fresh Codex desktop installation still require a clean Windows VM with legitimately installed ETABS 23. Windows Sandbox provisioning was requested for that follow-up; a full clean-environment result is not claimed.

The installer now rechecks both SDK and runtime after WinGet returns and stops with guidance if a reboot is needed or prerequisites remain missing.
