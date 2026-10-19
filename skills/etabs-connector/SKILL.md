---
name: etabs-connector
description: Use the local ETABS 23 MCP connector with read/write access to inspect models, optionally create working copies, modify the chosen open model, run analysis, and extract bounded structural results. Use whenever the user asks to connect to, read, modify, analyse, or extract data from an ETABS 23 model through this plugin.
---

# ETABS Connector

Use ETABS as a read/write calculation engine while keeping the selected file, units, model state, changes, and verification explicit.

## Workflow

1. Call `get_capabilities`.
2. Call `connect` with `start_if_missing=false` unless the user asks to start ETABS.
3. Call `get_model_info` and confirm ETABS 23, active model path, units, and lock state.
4. At the start of every task that may modify or analyse the model, ask once: create a new copy or reuse the open model?
5. If the user chooses a copy, call `save_working_copy` with a new authorised `.edb` path before changes.
6. If the user chooses reuse, apply authorised changes directly to the open model without creating a copy. State the active path before the first write.
7. If an authorised write or analysis operation requires an unlocked model, allow the connector to unlock it automatically. Report that unlocking occurred; do not treat unlocking as permission for unrelated changes.
8. Do not request repeated permission for ordinary changes already included in the task.
9. Apply only requested changes using exact ETABS object, group, property, load-case, and combination names.
10. Prefer purpose-built tools for their stronger read-back checks. For unsupported ETABS operations, call `list_api_methods`, then `describe_api_method`, then `invoke_api_method` with the exact object path, method, overload, parameters, and operation kind.
11. Treat generic methods conservatively: getters are read operations; setters and unknown methods are writes; solver creation or execution is analysis. Use `confirm_destructive=true` only when the task authorizes delete, replace, initialize, import, save, open, or application-lifecycle operations.
12. Verify each mutation by a corresponding read operation where ETABS exposes one. Require `verified=true` from purpose-built tools where supported.
13. Run analysis only when requested and analysis permission is enabled.
14. Extract only required cases, combinations, objects, and bounded result rows.
15. Report the active model path, copy/reuse choice, unlocking, changes, verification, analysis status, units, failures, and interpretation limitations.

Do not overwrite a different file unless explicitly requested. Never infer units from magnitude or describe solver completion as engineering validation. If an operation errors, stop the affected workflow and preserve the current model state.