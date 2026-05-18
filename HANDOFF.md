# PageWalkerLocal Handoff

Date: 2026-05-18  
Repository: `guvity/page-walker-local`  
Working branch: `codex/phase-2-real-ocr-llm`  
Latest Phase 2 code commit with successful build: `e6f8208adedad28b337dedbf67a48d143b2fc477`

## 1. Git Status

Local command attempted:

```powershell
git status --short --branch
```

Result: failed because `git` is not installed or not available in `PATH` in the current Codex workspace.

Because local `git status` is unavailable, branch state was checked through GitHub compare:

- `codex/phase-2-real-ocr-llm` is ahead of `main` by 3 commits.
- `codex/phase-2-real-ocr-llm` is behind `main` by 0 commits.
- Compare base: `3f87648984b28ade87a755c8e8bd2720bac75d95`
- Successful Phase 2 head: `e6f8208adedad28b337dedbf67a48d143b2fc477`

## 2. What Has Been Done

Phase 1 created the portable Windows x64 .NET 8 application skeleton with dry-run-first safety, active-window/rectangle modes, Win32 `SendInput`, basic rule planning, screenshots, runtime paths, logs, diagnostics, and a GitHub Actions publish workflow.

Phase 2 added real buildable components for:

- Offline OCR through `RapidOcrNet`.
- Deeper FlaUI UI Automation tree traversal.
- Local LLamaSharp GGUF decision brain with fallback to deterministic rules.
- Allowed-action generation so the model cannot invent coordinates.
- Form filling from configured `testFormData` only.
- Best-effort URL/domain safety.
- Navigation memory and loop prevention.
- Best-effort tab tracking through UIA tab counts.
- Safer human-like mouse path validation and scroll positioning.

No local build was run. The project was built only through GitHub Actions.

## 3. Changed Files And Why

Compared with `main`, these files changed in `codex/phase-2-real-ocr-llm`:

- `README.md` - updated status/docs for Phase 2 OCR, UIA, local LLM, and tab tracking behavior.
- `models/README.md` - documented bundled RapidOcrNet defaults and custom OCR/LLM model placement.
- `src/PageWalkerLocal/PageWalkerLocal.csproj` - added `RapidOcrNet`, `LLamaSharp.Backend.Cpu`, and `SkiaSharp.NativeAssets.Win32` packages.
- `src/PageWalkerLocal/Windowing/Bounds.cs` - added point clamping for safe mouse target/path handling.
- `src/PageWalkerLocal/Perception/CandidateElement.cs` - added `Text`, `AddressBar`, and `TabItem` candidate kinds.
- `src/PageWalkerLocal/Perception/RapidOcrEngine.cs` - replaced placeholder with real RapidOcrNet OCR returning text, line bounds, and confidence.
- `src/PageWalkerLocal/Perception/UiaReader.cs` - replaced root-only reader with FlaUI descendant traversal and candidate extraction.
- `src/PageWalkerLocal/Perception/PageClassifier.cs` - improved OCR line classification into link/button/text candidates.
- `src/PageWalkerLocal/Brain/ActionPlan.cs` - added `MemoryKey` for loop prevention.
- `src/PageWalkerLocal/Brain/AllowedActionGenerator.cs` - added generated allowed actions for local LLM/rules.
- `src/PageWalkerLocal/Brain/BrainJsonParser.cs` - added JSON extraction and snake-case/kebab-case action parsing.
- `src/PageWalkerLocal/Brain/BrainPromptBuilder.cs` - added allowed action details for local model prompts.
- `src/PageWalkerLocal/Brain/IBrain.cs` - extended planner context with URL/domain information.
- `src/PageWalkerLocal/Brain/LocalLlmBrain.cs` - implemented real LLamaSharp local GGUF loading/inference with fallback.
- `src/PageWalkerLocal/Brain/PlannerRules.cs` - tightened forbidden text list and kept hard planner rejection authority.
- `src/PageWalkerLocal/Brain/RuleBasedBrain.cs` - now uses generated allowed actions and supports configured form filling.
- `src/PageWalkerLocal/Browser/BrowserStateTracker.cs` - added URL/domain extraction from UIA address bar candidates and page keys.
- `src/PageWalkerLocal/Browser/BrowserTabTracker.cs` - added UIA tab counting and own-tab detection by count increase.
- `src/PageWalkerLocal/Browser/NavigationMemory.cs` - added action memory marking for loop prevention.
- `src/PageWalkerLocal/Core/Runner.cs` - wired browser snapshot, domain checks, navigation memory, tab tracking, and post-click tab count.
- `src/PageWalkerLocal/HumanInput/HumanInteractionEngine.cs` - added safe path clamping and guaranteed cursor-in-bounds before scroll.
- `src/PageWalkerLocal/HumanInput/SafeInputController.cs` - stopped validating the out-of-bounds starting mouse point as a target action.
- `HANDOFF.md` - this transfer note.

## 4. Requirements Status

Implemented and built successfully in GitHub Actions:

- Real offline OCR dependency and `RapidOcrEngine` implementation.
- `OcrResult.Text` and `OcrResult.Lines` with bounds/confidence.
- UIA descendant reading for buttons, links, inputs, text, address bar candidates, and tab items.
- Mouse path bug fix for live click paths starting outside allowed bounds.
- Cursor is moved inside allowed bounds before scroll.
- LLamaSharp local brain path with strict JSON parsing and fallback.
- Allowed-action generator.
- RuleBasedBrain form filling from `testFormData`.
- Basic tab tracking by UIA tab count delta.
- Best-effort URL/domain safety through UIA address bar candidates.
- Navigation memory and loop prevention keys.
- GitHub Actions build for Windows x64 portable artifact.

Implemented as best-effort but not runtime-proven in this environment:

- RapidOCR runtime behavior on a real Windows desktop/RDP session.
- Chromium UIA completeness for address bar, tabs, and page elements.
- Local GGUF inference with actual `qwen2.5-0.5b` or `SmolLM2` model files.
- New-tab detection when Chromium UIA does not expose tab items reliably.
- Domain safety when URL cannot be read from UIA.

Not yet fully implemented:

- Ctrl+L copy/restore URL fallback.
- Robust browser-specific tab ownership model.
- Advanced debug overlay.
- Full HTML report enrichment beyond action history.
- Automated unit/integration tests.
- Runtime smoke test on an actual Windows UI session.

## 5. Build And Test Commands Run

Local commands:

- `git status --short --branch` - failed: `git` command not found.
- No local `dotnet restore`, `dotnet build`, `dotnet test`, or `dotnet publish` was run.

GitHub Actions workflow:

```text
.github/workflows/build-win-x64.yml
```

Workflow command run by GitHub Actions:

```powershell
dotnet publish src/PageWalkerLocal/PageWalkerLocal.csproj -c Release -r win-x64 --self-contained true -o artifacts/PageWalkerLocal-win-x64
```

Phase 2 workflow runs:

- `26042695081` - failed on compile errors:
  - FlaUI numeric conversion ambiguity.
  - `ModelParams.Seed` not available in LLamaSharp 0.27.
  - RapidOcrNet box point type mismatch.
- `26042889167` - failed on RapidOCR bounds numeric conversion ambiguity.
- `26043063441` - success.

Successful artifact:

- Name: `PageWalkerLocal-win-x64`
- Workflow run: `https://github.com/guvity/page-walker-local/actions/runs/26043063441`
- Head SHA: `e6f8208adedad28b337dedbf67a48d143b2fc477`
- Size: about 100 MB
- Digest: `sha256:f93224fbaec11932e996c1b7c3fcdb824e44497b7fa043a518cb7ea6b675ed92`

## 6. Known Issues And Suspicions

- Local workspace is not a normal Git checkout, or `git` is unavailable, so ordinary local git workflow cannot be used here.
- OCR compiles and packages, but OCR runtime quality/latency has not been tested against real screenshots.
- `RapidOcrEngine` custom model discovery is heuristic: it looks for filenames containing `det`, `cls`, `rec`, and `dict`/`keys`.
- UIA for Chromium can be incomplete; OCR remains the main perception source.
- URL/domain safety currently depends on UIA exposing the address bar. If it does not, external navigation blocking is weaker.
- Tab tracking is best-effort by visible UIA tab count. It may miss popups/windows or Chromium profiles that hide tabs from UIA.
- LLamaSharp local brain compiles, but no GGUF model was present for runtime validation.
- The local model prompt and JSON parsing are conservative but should be tested with the actual target GGUF models.
- `ConsoleKey.BrowserBack` compiled in CI, but browser back behavior should be verified in live mode.
- No automated tests exist yet.

## 7. Suggested Next Steps

1. Run the artifact on a Windows desktop/RDP session in dry-run mode with a normal Chromium page.
2. Verify OCR text and bounds are written into decision logs.
3. Verify UIA candidates include address bar and tab items on Chrome, Edge, SunBrowser, and AdsPower profiles.
4. Add a safe URL fallback using Ctrl+L/copy/restore only when config allows clipboard interaction.
5. Add unit tests around `AllowedActionGenerator`, `BrainJsonParser`, domain matching, and planner rejection.
6. Add a tiny local test page/manual QA script for dry-run perception validation.
