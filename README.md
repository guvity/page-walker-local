# PageWalkerLocal

PageWalkerLocal is a portable Windows x64 C#/.NET 8 application for authorized browser QA, load checking, page walking, and statistics testing. It runs locally, starts from the currently focused Chromium-based browser window or a configured screen rectangle, reads local visual/UI signals, plans conservative actions, and can drive mouse/keyboard input with human-like timing when explicitly switched out of dry-run mode.

This is not a CAPTCHA solver, not an anti-bot bypass tool, not a stealth/fingerprint spoofing tool, not a fake account creator, and not a payment/paywall bypass tool. It does not use external AI APIs, local HTTP servers, Ollama, llama-server, MCP servers, Playwright servers, browser-use servers, WMI/CIM/WMIC, Windows services, installers, or admin rights.

## Status

Phase 1 MVP is implemented, and the Phase 2 branch adds real local perception/planning pieces:

- RuleBasedBrain only.
- ActiveWindow and Rectangle target modes.
- Per-user runtime paths under `%LOCALAPPDATA%\PageWalkerLocal\`.
- Dry-run enabled by default.
- Screenshot capture through GDI.
- Real `RapidOcrNet` OCR with bundled PP-OCRv5 latin models and safe `NullOcrEngine` fallback if initialization fails.
- FlaUI descendant traversal for buttons, links, inputs, text, address bar candidates, and tab items.
- Technical page, popup, and age-gate detectors.
- HumanInteractionEngine with Bezier-like mouse paths, jitter, dwell time, variable scrolls, and behavior profiles.
- SafeInputController with bounds, active-window, process, confidence, and dry-run checks.
- Ctrl+Alt+F12 emergency stop hotkey.
- Decision JSONL logs, screenshots, and HTML reports.
- GitHub Actions Windows x64 self-contained publish workflow.

The local LLamaSharp brain only chooses from generated allowed actions; the hard planner remains authoritative and can reject any model decision.

## Runtime Data

Each Windows user gets separate data:

```text
%LOCALAPPDATA%\PageWalkerLocal\
%LOCALAPPDATA%\PageWalkerLocal\logs\
%LOCALAPPDATA%\PageWalkerLocal\debug\
%LOCALAPPDATA%\PageWalkerLocal\cache\
%LOCALAPPDATA%\PageWalkerLocal\temp\
%LOCALAPPDATA%\PageWalkerLocal\reports\
%LOCALAPPDATA%\PageWalkerLocal\model-cache\
%LOCALAPPDATA%\PageWalkerLocal\decision-logs\
```

The app uses a per-user/per-session mutex, so one user/session cannot accidentally run conflicting copies while different Windows users can run their own copies in parallel.

## Build

Do not build locally. This repository is designed to build through GitHub Actions only.

The workflow is:

```text
.github/workflows/build-win-x64.yml
```

It runs on `windows-latest`, installs .NET 8, restores, publishes:

```text
dotnet publish src/PageWalkerLocal/PageWalkerLocal.csproj -c Release -r win-x64 --self-contained true
```

and uploads `PageWalkerLocal-win-x64.zip`.

To download the artifact, open the repository on GitHub, go to **Actions**, select the latest successful **Build Windows x64** run, and download **PageWalkerLocal-win-x64**.

## Running Dry-Run

Dry-run is the default and does not move the mouse, click, scroll, or type. It logs planned actions only.

```cmd
PageWalkerLocal.exe --config appsettings.json
```

Focus a Chrome, Edge, SunBrowser, AdsPower Chromium profile, or other allowed Chromium-based window before starting ActiveWindow mode.

## Enabling Live Mode

Review the config first, then set:

```json
{
  "dryRun": false
}
```

Live mode still checks:

- target point is inside allowed bounds;
- active window is still the target;
- target process is allowed;
- action is not forbidden by config;
- confidence is above the configured minimum.

If focus leaves the target window, the app stops or pauses for safety.

## Configuration

Important options:

- `targetMode`: `ActiveWindow` or `Rectangle`.
- `targetProcessNames`: browser process/title allow-list.
- `rectangle`: allowed screen area for Rectangle mode.
- `behaviorProfile`: `cautious`, `normal`, `fast`, or `load-test`.
- `randomSeed`: set for reproducible mouse/scroll plans.
- `maxDepth`, `maxSteps`, `maxScrollsPerPage`, `maxRuntimeSeconds`: hard limits.
- `allowExternalNavigation`: keep `false` unless your test explicitly allows it.
- `blockedTexts`: words that stop interaction, such as payment/deposit/subscribe.
- `allowAgeGate`, `allowSimpleConfirmations`, `allowedGateTexts`: simple confirmation gates.
- `allowForms`, `allowedFormFields`, `testFormData`: disabled by default.
- `technicalPageAction`: `stop`, `retry`, `back`, or `close_tab`.

Local LLM settings are disabled by default:

```json
{
  "modelsRoot": "models",
  "localBrain": {
    "enabled": false,
    "provider": "LLamaSharp",
    "modelPath": "auto"
  }
}
```

Place GGUF files manually in `models/llm/`. No model files are downloaded or committed by this project. When `modelPath` is `auto`, PageWalkerLocal searches for readable `.gguf` files under the portable `models` folder and the per-user `%LOCALAPPDATA%\PageWalkerLocal\models\llm` folder. If no model is found or the file cannot be read, the app falls back to `RuleBasedBrain`.

OCR is enabled by default but fails closed into `NullOcrEngine` if RapidOCR or ONNX Runtime cannot initialize:

```json
{
  "ocr": {
    "enabled": true,
    "engine": "RapidOCR",
    "modelsPath": "auto"
  }
}
```

When `modelsPath` is `auto`, PageWalkerLocal searches recursively for a complete RapidOCR set: one `det` ONNX, one `cls` ONNX, one `rec` ONNX, and one dictionary or keys text file. It prefers model sets under `v5`, with `PP-OCRv5` filenames, and then `latin` filenames.

## Phase 2 OCR Runtime Troubleshooting

Recommended OCR model layout:

```text
C:\PageWalkerLocal-win-x64\models\v5\ch_PP-OCRv5_mobile_det.onnx
C:\PageWalkerLocal-win-x64\models\v5\ch_ppocr_mobile_v2.0_cls_infer.onnx
C:\PageWalkerLocal-win-x64\models\v5\latin_PP-OCRv5_rec_mobile_infer.onnx
C:\PageWalkerLocal-win-x64\models\v5\ppocrv5_latin_dict.txt
```

Use this command to inspect model roots, OCR sets, and GGUF candidates without opening a browser, moving the mouse, starting OCR inference, or loading an LLM:

```cmd
PageWalkerLocal.exe --model-discovery-test
```

Use this command to diagnose ONNX Runtime and run one isolated RapidOCR smoke test without UIA, browser control, mouse movement, or LLM inference:

```cmd
PageWalkerLocal.exe --ocr-self-test
```

An ONNX Runtime native initialization failure usually means `onnxruntime.dll` or one of its native dependencies could not load. Check that `onnxruntime*.dll` files are present in the artifact, readable by the current user, compatible with Windows x64, and that the target machine has the Visual C++ Redistributable x64 installed.

To run in limited mode while diagnosing OCR, disable OCR:

```json
{
  "ocr": {
    "enabled": false
  }
}
```

With OCR disabled or unavailable, PageWalkerLocal continues with UI Automation, window title, and conservative static defaults.

## Running from admin-created read-only program directory

`C:\PageWalkerLocal-win-x64` may be created or extracted by an Administrator and then run by normal users. This is supported. PageWalkerLocal reads application files and models from the program directory, but does not write logs, caches, reports, temp files, model indexes, or debug output there.

All writable runtime files go under `%LOCALAPPDATA%\PageWalkerLocal\`. If the program directory or model directory is read-only, that is normal. If OCR or LLM fails, check read permissions for model files and native DLL files. Do not grant write permissions to the program directory unless you have another operational reason to do so.

## Safety Boundaries

PageWalkerLocal refuses or stops on CAPTCHA-like text, low confidence, blocked/payment-like text, focus changes, out-of-bounds targets, and disallowed processes. Popup handling runs before page walking. Accept buttons are not clicked unless `allowAcceptButtons=true`.

The program closes only tabs it has explicitly tracked as its own. It uses UIA tab counts as a best-effort signal and does not assume unknown tabs belong to it.
