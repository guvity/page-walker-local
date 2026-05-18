# Models

Model files are not committed to this repository.

OCR uses automatic read-only discovery when `ocr.modelsPath` is `auto`. Custom OCR models can be placed under any `models` subfolder, for example:

```text
models/v5/
```

For a custom OCR model set, provide:

- detector ONNX containing `det` in the file name;
- classifier ONNX containing `cls` in the file name;
- recognizer ONNX containing `rec` in the file name;
- dictionary text file containing `dict` or `keys` in the file name.

Optional local LLM GGUF files should be placed manually under:

```text
models/llm/
```

The program may read these files from a read-only portable program directory. It must not write indexes, caches, or generated files next to models; writable runtime data belongs under `%LOCALAPPDATA%\PageWalkerLocal\`.

Suggested small local models:

- `qwen2.5-0.5b-instruct-q4_k_m.gguf`
- `smollm2-360m-instruct-q4_k_m.gguf`

The runtime must remain in-process only. Do not use Ollama, llama-server, MCP servers, browser-use servers, external AI APIs, or local HTTP servers for PageWalkerLocal decisions.
