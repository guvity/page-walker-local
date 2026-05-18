# PageWalkerLocal Handoff

Date: 2026-05-18 21:10:28 +02:00
Repository: `guvity/page-walker-local`
Working branch: `codex/phase-2-fix-ocr-runtime-auto-models`
Base branch: `codex/phase-2-real-ocr-llm`

## Summary

This branch keeps the Phase 2 architecture and focuses on runtime robustness:

- Added read-only model discovery for RapidOCR ONNX sets and LLamaSharp GGUF models.
- Added ONNX Runtime native diagnostics and runtime permission diagnostics.
- Added `--model-discovery-test` and `--ocr-self-test`.
- Expanded writable runtime directories under `%LOCALAPPDATA%\PageWalkerLocal\`.
- Updated the Windows x64 GitHub Actions workflow to run on `codex/**`, print native DLLs, fail if `onnxruntime*.dll` is missing, and run the new self-tests.
- Updated README, sample config, schema, and model docs.

## Files Added

- `src/PageWalkerLocal/Core/FileSystemAccess.cs`
- `src/PageWalkerLocal/Core/ModelDiscovery.cs`
- `src/PageWalkerLocal/Core/CliSelfTests.cs`
- `src/PageWalkerLocal/Diagnostics/NativeDependencyDiagnostics.cs`
- `src/PageWalkerLocal/Diagnostics/RuntimePermissionDiagnostics.cs`

## Files Changed

- `.github/workflows/build-win-x64.yml`
- `README.md`
- `models/README.md`
- `samples/appsettings.sample.json`
- `src/PageWalkerLocal/appsettings.json`
- `src/PageWalkerLocal/appsettings.schema.json`
- `src/PageWalkerLocal/Core/AppConfig.cs`
- `src/PageWalkerLocal/Core/RuntimePaths.cs`
- `src/PageWalkerLocal/Core/Runner.cs`
- `src/PageWalkerLocal/Perception/RapidOcrEngine.cs`
- `src/PageWalkerLocal/Perception/UiaReader.cs`
- `src/PageWalkerLocal/Brain/LocalLlmBrain.cs`
- `src/PageWalkerLocal/Diagnostics/DecisionLogWriter.cs`
- `src/PageWalkerLocal/Diagnostics/HtmlReportWriter.cs`
- `src/PageWalkerLocal/Program.cs`

## OCR Model Discovery

Default config is now:

```json
"ocr": {
  "enabled": true,
  "engine": "RapidOCR",
  "modelsPath": "auto"
}
```

`ModelDiscovery` searches read-only under:

- `AppContext.BaseDirectory\models`
- `Directory.GetCurrentDirectory()\models`
- `%LOCALAPPDATA%\PageWalkerLocal\models`
- configured `modelsRoot`, if not `auto` or empty

A valid RapidOCR set requires all files in the same directory:

- `*.onnx` filename containing `det`
- `*.onnx` filename containing `cls`
- `*.onnx` filename containing `rec`
- `*.txt` filename containing `dict` or `keys`

Selection prefers paths or filenames with `v5`, then `PP-OCRv5`/`ppocrv5`, then `latin`, then deterministic path order. The selected det/cls/rec/dict paths are logged explicitly. If no set is found, RapidOcrNet bundled/default init is tried; failure falls back to limited OCR mode without crashing.

## LLM Model Discovery

Default config is now:

```json
"localBrain": {
  "enabled": false,
  "provider": "LLamaSharp",
  "modelPath": "auto",
  "maxTokens": 160,
  "temperature": 0.2,
  "strictJson": true,
  "minConfidence": 0.65
}
```

When `localBrain.enabled=true` and `modelPath` is `auto`, empty, missing, or a relative path that does not exist, `ModelDiscovery` searches for `*.gguf` under:

- `AppContext.BaseDirectory\models\llm`
- `AppContext.BaseDirectory\models`
- `Directory.GetCurrentDirectory()\models\llm`
- `%LOCALAPPDATA%\PageWalkerLocal\models\llm`
- configured `modelsRoot` and `modelsRoot\llm`

Selection prefers `qwen2.5-0.5b`, then `qwen2.5`, `smollm`, `tinyllama`, and quant names `q4_k_m`, `q4_0`, `q3_k_m`, `q5_k_m`, `q5_0`, `q8_0`, `fp16`. `fp16` is avoided when any readable non-fp16 candidate exists. If no readable GGUF is selected, the app falls back to `RuleBasedBrain`.

## ONNX Runtime Diagnostics

`NativeDependencyDiagnostics.CheckOnnxRuntime()` logs:

- process architecture and OS version;
- `AppContext.BaseDirectory` and current directory;
- relevant `PATH` entries;
- whether `onnxruntime.dll` exists in the output root;
- found `onnxruntime*.dll` files under output/current directory;
- readability of found DLL files;
- whether `Microsoft.ML.OnnxRuntime` is loaded;
- whether `new Microsoft.ML.OnnxRuntime.SessionOptions()` succeeds;
- exception type, message, HResult, and stack trace on failure.

When RapidOCR init fails because ONNX Runtime native initialization failed, the log now says this usually means `onnxruntime.dll` or a native dependency could not load and points at VC++ Redistributable x64, bundled DLLs, permissions, and CPU/OS compatibility.

## Read-only Program Directory

`AppContext.BaseDirectory` and model directories are treated as read-only. The app may read from the portable program directory and `models`, but it must not write indexes, caches, logs, reports, temp files, or debug output there.

Writable runtime data goes under:

- `%LOCALAPPDATA%\PageWalkerLocal\logs`
- `%LOCALAPPDATA%\PageWalkerLocal\debug`
- `%LOCALAPPDATA%\PageWalkerLocal\cache`
- `%LOCALAPPDATA%\PageWalkerLocal\temp`
- `%LOCALAPPDATA%\PageWalkerLocal\reports`
- `%LOCALAPPDATA%\PageWalkerLocal\model-cache`
- `%LOCALAPPDATA%\PageWalkerLocal\decision-logs`

If the program directory is read-only, this is logged as supported. If `%LOCALAPPDATA%\PageWalkerLocal` cannot be created or written, startup stops with a clear critical error.

## CLI Test Commands

```cmd
PageWalkerLocal.exe --model-discovery-test
```

Runs runtime permission checks, lists model roots, OCR sets, GGUF models, selected OCR set, and selected LLM model. It does not require a browser, move the mouse, use UIA, run OCR inference, or load an LLM. It exits non-zero only if runtime user storage is not writable or discovery crashes.

```cmd
PageWalkerLocal.exe --ocr-self-test
```

Runs permission diagnostics, OCR discovery, ONNX Runtime diagnostics, RapidOCR initialization, and one tiny in-memory OCR call. Exit codes:

- `0`: OCR initialized and test completed.
- `2`: no custom OCR model set was found and bundled/default init path was used or tried.
- `10`: ONNX Runtime native dependency failed.
- `11`: RapidOCR `InitModels` failed.
- `12`: OCR `Detect` failed.

## UIA Runtime Behavior

FlaUI `RPC_E_SERVERFAULT` and related UIA read failures remain non-fatal. The app now logs:

```text
UIA failed for this Chromium window. Continuing with OCR-only perception.
```

If OCR is also unavailable:

```text
Both OCR and UIA are unavailable; perception is limited to window title and static defaults.
```

## Validation Notes

Local `dotnet build` could not run in this Codex environment because only the .NET runtime is installed and no SDK is available. `git diff --check` passed locally, and the branch was validated through GitHub Actions.

GitHub Actions validation succeeded:

- Workflow run: `https://github.com/guvity/page-walker-local/actions/runs/26054563925`
- Job: `Publish portable win-x64 folder`
- Result: success
- Validated code commit: `85797436a992ef8474154e2ee30d9a658db8f133`
- Artifact: `PageWalkerLocal-win-x64`
- Artifact URL: `https://github.com/guvity/page-walker-local/actions/runs/26054563925/artifacts/7066874892`
- Artifact size: `100670176` bytes
- Artifact SHA256 digest: `affe1dbcbee0b52c05dc1306f4aadc3618ae90964162010367403576f5c9121c`

Workflow checks completed:

- `dotnet restore`: success
- `dotnet publish` self-contained `win-x64`: success
- native DLL listing: success
- `onnxruntime*.dll` artifact check: success
- `PageWalkerLocal.exe --model-discovery-test`: success
- `PageWalkerLocal.exe --ocr-self-test`: success
- artifact upload: success

Native DLLs confirmed in artifact logs:

- `onnxruntime.dll` - `14718776` bytes
- `onnxruntime_providers_shared.dll` - `21856` bytes
- `SkiaSharp.dll` - `490016` bytes

OCR self-test selected the bundled/published RapidOCR v5 model set under `artifacts\PageWalkerLocal-win-x64\models\v5`, confirmed all four files readable, `SessionOptions` succeeded, RapidOCR initialized with custom models, and one tiny bitmap OCR call completed with text length `15` and line count `1`.

## Remaining Work and Risks

- Confirm target Windows machines have VC++ Redistributable x64 if ONNX Runtime native init fails.
- Runtime OCR quality and Chromium UIA behavior still need live Windows desktop/RDP validation.
- `ModelDiscovery` is deterministic but heuristic; unusual OCR model naming may still require explicit paths.
- No unit tests exist yet for model selection scoring, permission probes, or CLI exit code behavior.
- Future phase should add Ctrl+L URL fallback, richer reports, and focused unit tests around model discovery, planner safety, and domain handling.
