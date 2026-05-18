# Models

Model files are not committed to this repository.

OCR models, when Phase 2 RapidOCR support is enabled, should be placed under:

```text
models/ocr/
```

Optional local LLM GGUF files should be placed manually under:

```text
models/llm/
```

Suggested small local models:

- `qwen2.5-0.5b-instruct-q4_k_m.gguf`
- `smollm2-360m-instruct-q4_k_m.gguf`

The runtime must remain in-process only. Do not use Ollama, llama-server, MCP servers, browser-use servers, external AI APIs, or local HTTP servers for PageWalkerLocal decisions.
