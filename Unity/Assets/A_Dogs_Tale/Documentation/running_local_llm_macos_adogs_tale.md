# Running a Local LLM on macOS for *A Dog’s Tale*

*A quick setup sheet for running Gemma 3 locally with Ollama and connecting it to the game through the **LLM Bridge**.*

---

## Big Picture

```text
A Dog’s Tale in Unity
        ↓
LLM Bridge
        ↓
Ollama local model server
        ↓
Gemma 3
```

**Unity** should talk to the **LLM Bridge**, not directly to Ollama.  
The LLM Bridge handles prompt building, model selection, JSON validation, tool/MCP calls, timeouts, and debug logging.

---

## 1. Start Ollama

Install or open the Ollama desktop app on macOS.

Then check that the command-line tool is available:

```bash
ollama --version
```

Pull the Gemma 3 model:

```bash
ollama pull gemma3
```

Optional quick test:

```bash
ollama run gemma3
```

Type a short prompt, then exit with:

```text
/bye
```

---

## 2. Run Ollama as a Local Service

For local game development on the same Mac, bind Ollama to localhost:

```bash
launchctl setenv OLLAMA_HOST "127.0.0.1:11434"
```

Then quit and restart the Ollama app.

Use this Ollama API address:

```text
http://127.0.0.1:11434
```

Recommended: keep Ollama set to start at login:

```text
System Settings → General → Login Items → Open at Login
```

---

## 3. Verify Ollama Is Running

List installed Ollama models:

```bash
curl http://127.0.0.1:11434/api/tags
```

Test a Gemma 3 generation request:

```bash
curl http://127.0.0.1:11434/api/generate \
  -H "Content-Type: application/json" \
  -d '{
    "model": "gemma3",
    "prompt": "Say hello from the local LLM for A Dog’s Tale.",
    "stream": false
  }'
```

A working setup should return JSON with a `response` field.

---

## 4. Start the LLM Bridge

From your project sidecar folder:

```bash
cd /Users/markpontius/Desktop/Programming/Unity/A_Dogs_Tale/llm_runner_py
```

Activate the Python virtual environment:

```bash
source .venv/bin/activate
```

Point the LLM Bridge at Ollama:

```bash
export OLLAMA_BASE_URL="http://127.0.0.1:11434"
```

Start the LLM Bridge server:

```bash
python -m uvicorn server:app --reload --host 127.0.0.1 --port 8000
```

The LLM Bridge should now be available at:

```text
http://127.0.0.1:8000
```

---

## 5. Verify the LLM Bridge

Check the health endpoint:

```bash
curl http://127.0.0.1:8000/health
```

If your Unity world-state server is running, verify it too:

```bash
curl -X POST http://127.0.0.1:8081/world_state \
  -H "Content-Type: application/json" \
  -d '{"agent_id":"Cur","detail":"normal"}'
```

---

## 6. Unity Settings

In Unity, point the game to the LLM Bridge:

```csharp
public string plannerServerUrl = "http://127.0.0.1:8000/plan";
public string ollamaModelName = "gemma3";
public float requestTimeoutSeconds = 60f;
```

Suggested responsibility split:

| Piece | Job |
|---|---|
| Unity | Sends agent/game state and receives validated plans |
| LLM Bridge | Builds prompts, calls Ollama, validates responses, manages tools |
| Ollama | Runs the local Gemma 3 model |
| Gemma 3 | Generates planning / behavior responses |

---

## 7. Useful Troubleshooting Commands

Check whether Ollama is reachable:

```bash
curl http://127.0.0.1:11434/api/tags
```

Check whether the LLM Bridge is reachable:

```bash
curl http://127.0.0.1:8000/health
```

Check whether Ollama’s port is already in use:

```bash
lsof -i :11434
```

Check whether the LLM Bridge port is already in use:

```bash
lsof -i :8000
```

Free a stuck LLM Bridge port:

```bash
lsof -ti :8000 | xargs kill
```

---

## Optional: One-Command LLM Bridge Launcher

Create a file named:

```text
run_llm_bridge.sh
```

with this content:

```bash
#!/bin/zsh

cd /Users/markpontius/Desktop/Programming/Unity/A_Dogs_Tale/llm_runner_py || exit 1

source .venv/bin/activate

export OLLAMA_BASE_URL="http://127.0.0.1:11434"

python -m uvicorn server:app --reload --host 127.0.0.1 --port 8000
```

Make it executable:

```bash
chmod +x run_llm_bridge.sh
```

Run it:

```bash
./run_llm_bridge.sh
```

---

## Hardware Footnote

For reasonable local LLM performance, use an Apple Silicon Mac where possible.

Recommended baseline:

```text
Apple M-series Mac, preferably M2/M3/M4 or newer
16 GB RAM minimum for comfortable development
24–32 GB+ RAM preferred if Unity, Ollama, Python, browser tabs, and tools are open together
```

Gemma 3 is a good default local model for experimentation. On weaker machines, use a smaller model variant or another lightweight Ollama model such as:

```bash
ollama pull gemma3:1b
```

or a compact Qwen model:

```bash
ollama pull qwen3
```

For gameplay iteration, favor responsiveness over maximum reasoning quality. A smaller model that answers quickly is often more useful than a larger model that stalls the simulation.

---

## Dog-Eared Summary

```text
1. Start Ollama.
2. Pull Gemma 3.
3. Verify Ollama at http://127.0.0.1:11434.
4. Start the LLM Bridge on port 8000.
5. Point Unity at http://127.0.0.1:8000/plan.
6. Let Unity → LLM Bridge → Ollama → Gemma 3 do the work.
```
