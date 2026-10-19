# Installer validation — 0.1.3

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
- UTF-8 input prefixed with a byte-order mark is accepted (regression discovered in Windows Sandbox).

## Windows Sandbox checks

On 2026-10-19, a fresh Windows Sandbox session reported no .NET and no WinGet. With locally installed ETABS 23 files and Codex CLI supplied through read-only mapped folders, the installer downloaded Microsoft's .NET SDK 8.0.426 ZIP, verified its SHA-512 checksum, extracted it locally, compiled the connector, passed the MCP capabilities check, and registered the real Codex CLI. No CSI binaries were published. The full integration suite also passed inside the guest: isolated registration, exact registered-command launch, skill installation, read-only defaults, UTF-8 BOM input, repeat-install preservation, and conflicting-registration preservation.

The first attempt encountered a guest DNS startup failure. A subsequent attempt after DNS became available completed the download. The test also exposed a Windows PowerShell UTF-8 byte-order-mark parse failure; version 0.1.3 fixes it and includes a regression test.

The verified SDK download, extraction, compilation, and startup were also repeated in an isolated host installation using `-UseLocalSdk`. The launcher supports a local runtime and remembers externally discovered runtime paths.

The test does not connect to an ETABS model or run structural analysis. It uses the locally licensed ETABS interop assembly for compilation and server startup.

## Reproduce

Install the prerequisites and ensure `codex` is available on PATH. From the repository, run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\test-installation.ps1 -TestRoot "C:\ETABS-Installer-Test-New"
```

The test requires a new directory. It sets `CODEX_HOME` only in child processes so the user's normal Codex configuration is not modified. Test logs and local build outputs stay inside the test directory; do not publish them because they contain local paths and licensed interop binaries.

## Scope and remaining checks

Sandbox tests cover dependency setup, server startup, and Codex CLI registration. ETABS binaries were supplied from the host, not installed or activated in the guest; COM attachment, licensing, structural analysis, and a fresh Codex desktop installation were not tested there. The WinGet/UAC/reboot branch and custom ETABS file picker remain untested. These results are not certification for every Windows configuration.

The installer now rechecks both SDK and runtime after WinGet returns and stops with guidance if a reboot is needed or prerequisites remain missing.
