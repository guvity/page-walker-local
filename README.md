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
  "localBrain": {
    "enabled": false,
    "provider": "LLamaSharp",
    "modelPath": "models/llm/qwen2.5-0.5b-instruct-q4_k_m.gguf"
  }
}
```

Place GGUF files manually in `models/llm/`. No model files are downloaded or committed by this project.

OCR uses bundled `RapidOcrNet` defaults when no custom files are present. To override OCR models, place a detector ONNX, classifier ONNX, recognizer ONNX, and matching dictionary text file under `models/ocr/`.

## Safety Boundaries

PageWalkerLocal refuses or stops on CAPTCHA-like text, low confidence, blocked/payment-like text, focus changes, out-of-bounds targets, and disallowed processes. Popup handling runs before page walking. Accept buttons are not clicked unless `allowAcceptButtons=true`.

The program closes only tabs it has explicitly tracked as its own. It uses UIA tab counts as a best-effort signal and does not assume unknown tabs belong to it.
