# PageWalkerLocal Handoff

Date: 2026-05-19 01:40:09 +02:00
Repository: `guvity/page-walker-local`
Working branch: `codex/phase-2-fix-ocr-runtime-auto-models`
Base branch: `codex/phase-2-real-ocr-llm`

## Summary

This branch keeps the Phase 2 architecture and focuses on runtime robustness:

- Added read-only model discovery for RapidOCR ONNX sets and LLamaSharp GGUF models.
- Added ONNX Runtime native diagnostics and runtime permission diagnostics.
- Added app-local MSVC runtime DLL packaging to fix ONNX Runtime native load failures on target machines without a suitable VC++ runtime.
- Added `--model-discovery-test` and `--ocr-self-test`.
- Expanded writable runtime directories under `%LOCALAPPDATA%\PageWalkerLocal\`.
- Fixed a target-machine LLM runtime failure where the GGUF model loaded successfully but first inference failed with `ContextOverflowException`.
- Added compact LLM prompt generation, configurable context size/prompt budget, and safer handling of untargeted actions returned with a spurious `targetId`.
- Added tracking and cleanup for new browser windows opened during a run, while preserving the original browser window.
- Made additional FlaUI window/element availability failures non-fatal.
- Updated the Windows x64 GitHub Actions workflow to run on `codex/**`, print native DLLs, fail if `onnxruntime*.dll` is missing, and run the new self-tests.
- Updated README, sample config, schema, and model docs.

## Files Added

- `src/PageWalkerLocal/Core/FileSystemAccess.cs`
- `src/PageWalkerLocal/Core/ModelDiscovery.cs`
- `src/PageWalkerLocal/Core/CliSelfTests.cs`
- `src/PageWalkerLocal/Browser/BrowserWindowTracker.cs`
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
- `src/PageWalkerLocal/Brain/BrainJsonParser.cs`
- `src/PageWalkerLocal/Brain/BrainPromptBuilder.cs`
- `src/PageWalkerLocal/Perception/RapidOcrEngine.cs`
- `src/PageWalkerLocal/Perception/UiaReader.cs`
- `src/PageWalkerLocal/Brain/LocalLlmBrain.cs`
- `src/PageWalkerLocal/Diagnostics/DecisionLogWriter.cs`
- `src/PageWalkerLocal/Diagnostics/HtmlReportWriter.cs`
- `src/PageWalkerLocal/Diagnostics/NativeDependencyDiagnostics.cs`
- `src/PageWalkerLocal/Diagnostics/RuntimePermissionDiagnostics.cs`
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
  "contextSize": 4096,
  "maxPromptChars": 8000,
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

## LLM Runtime Fix

The 2026-05-19 target-machine log showed the local LLM model was discovered and loaded:

```text
Loaded local GGUF model through LLamaSharp: 'C:\PageWalkerLocal-win-x64\models\llm\qwen2.5-0.5b-instruct-q4_k_m.gguf'.
```

The failure was not model discovery. The first inference failed with `LLama.Exceptions.ContextOverflowException` because the prompt exceeded the model context window. The fix keeps `LocalLlmBrain` enabled by:

- setting `ModelParams.ContextSize` from `localBrain.contextSize` (default `4096`);
- setting `InferenceParams.OverflowStrategy` to `ContextOverflowStrategy.TruncateAndReprefill`;
- using `BrainPromptBuilder.BuildCompact(...)` with `localBrain.maxPromptChars` (default `8000`);
- reducing visible text, candidate count, and candidate text length while preserving allowed action details;
- telling the model to return `targetId: null` for untargeted actions such as `Scroll`, `HumanRead`, and `Stop`;
- accepting an allowed untargeted action even if the model incorrectly returns a non-null `targetId`.

If LLM loading itself fails or no readable GGUF is available, the app still falls back to `RuleBasedBrain`.

## ONNX Runtime Diagnostics

`NativeDependencyDiagnostics.CheckOnnxRuntime()` logs:

- process architecture and OS version;
- `AppContext.BaseDirectory` and current directory;
- relevant `PATH` entries;
- whether `onnxruntime.dll` exists in the output root;
- found `onnxruntime*.dll` files under output/current directory;
- found app-local MSVC runtime DLLs such as `msvcp140*.dll`, `vcruntime140*.dll`, and `concrt140*.dll`;
- readability of found DLL files;
- whether `Microsoft.ML.OnnxRuntime` is loaded;
- whether `new Microsoft.ML.OnnxRuntime.SessionOptions()` succeeds;
- exception type, message, HResult, and stack trace on failure.

When RapidOCR init fails because ONNX Runtime native initialization failed, the log now says this usually means `onnxruntime.dll` or a native dependency could not load and points at VC++ Redistributable x64, bundled DLLs, permissions, and CPU/OS compatibility.

The 2026-05-18 target-machine log showed OCR models were found/readable and `onnxruntime.dll` existed/readable, but `SessionOptions` still failed with `DllNotFoundException` and `0x8007045A`. That points at a missing or incompatible transitive native dependency, most likely the MSVC runtime. The workflow now copies the x64 `Microsoft.VC143.CRT` DLLs into the artifact root next to `PageWalkerLocal.exe` and fails the build if `msvcp140.dll`, `vcruntime140.dll`, or `vcruntime140_1.dll` are absent.

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

`ElementNotAvailableException` and similar FlaUI runtime/window-lifetime failures are now also logged and treated as non-fatal. This prevents a spawned or closed browser window from crashing the run during perception.

## New Window Cleanup

`BrowserWindowTracker` captures the baseline top-level browser windows after the target browser window is selected. During the run it observes new allowed browser windows that appear outside the original target, such as OAuth or social-login popups.

At cleanup, only windows that appeared after the baseline are closed. The original target browser window is never closed by this cleanup path. In dry-run mode the app only logs which spawned windows would be closed; in live mode it posts `WM_CLOSE` to the tracked spawned windows.

## Validation Notes

Local `dotnet build` could not run in this Codex environment because only the .NET runtime is installed and no SDK is available. `git diff --check` passed locally, and the branch was validated through GitHub Actions.

Latest GitHub Actions validation succeeded:

- Workflow run: `https://github.com/guvity/page-walker-local/actions/runs/26066753413`
- Job: `Publish portable win-x64 folder`
- Result: success
- Validated code commit: `3cc30b119cbd0f05747db5066e1ecfc87b1a7ef7`
- Artifact: `PageWalkerLocal-win-x64`
- Artifact URL: `https://github.com/guvity/page-walker-local/actions/runs/26066753413/artifacts/7071548023`
- Artifact size: `101395957` bytes
- Artifact SHA256 digest: `ef51fe19abfd1304824d8d61c4f2b598d1aa035179ee0244233c1afee4becc1d`

Workflow checks completed:

- `dotnet restore`: success
- `dotnet publish` self-contained `win-x64`: success
- native DLL listing: success
- `onnxruntime*.dll` artifact check: success
- app-local MSVC runtime copy/check: success
- `PageWalkerLocal.exe --model-discovery-test`: success
- `PageWalkerLocal.exe --ocr-self-test`: success
- artifact upload: success

Native DLLs confirmed in artifact logs:

- `onnxruntime.dll` - `14718776` bytes
- `onnxruntime_providers_shared.dll` - `21856` bytes
- `SkiaSharp.dll` - `490016` bytes
- `msvcp140.dll` - `557728` bytes
- `vcruntime140.dll` - `124544` bytes
- `vcruntime140_1.dll` - `49792` bytes
- additional MSVC CRT companion DLLs: `concrt140.dll`, `msvcp140_1.dll`, `msvcp140_2.dll`, `msvcp140_atomic_wait.dll`, `msvcp140_codecvt_ids.dll`, `vcruntime140_cor3.dll`, `vcruntime140_threads.dll`

OCR self-test selected the bundled/published RapidOCR v5 model set under `artifacts\PageWalkerLocal-win-x64\models\v5`, confirmed all four files readable, confirmed app-local MSVC runtime files readable, `SessionOptions` succeeded, RapidOCR initialized with custom models, and one tiny bitmap OCR call completed with text length `15` and line count `1`.

Local validation for the LLM/window cleanup fix:

- `git diff --check`: success.
- Local `dotnet build`: not available in this environment because no .NET SDK is installed.
- GitHub Actions run `26066753413`: success.

## Remaining Work and Risks

- If ONNX Runtime still fails on a target machine with the new artifact, inspect the logged app-local MSVC runtime DLLs and then check OS/CPU compatibility or corrupted system VC++ runtime state.
- Runtime OCR quality and Chromium UIA behavior still need live Windows desktop/RDP validation.
- The compact prompt fix prevents context overflow, but real LLM decision quality should still be tested on several live sites with the chosen GGUF model.
- New-window cleanup uses Win32 top-level browser window tracking and `WM_CLOSE`; unusual browser shells or windows with custom close handling may need follow-up tuning.
- `ModelDiscovery` is deterministic but heuristic; unusual OCR model naming may still require explicit paths.
- No unit tests exist yet for model selection scoring, permission probes, or CLI exit code behavior.
- Future phase should add Ctrl+L URL fallback, richer reports, live LLM regression scenarios, and focused unit tests around model discovery, planner safety, prompt compaction, spawned-window cleanup, and domain handling.
