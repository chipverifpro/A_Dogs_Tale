# Running a local LLM on Windows for *A Dog's Tale*

> **Goal:** Run **Gemma 3** locally with **Ollama**, then connect the game to it through the **LLM Bridge**.

---

## Big picture

```text
A Dog's Tale in Unity
        ↓
LLM Bridge   http://127.0.0.1:8000
        ↓
Ollama       http://127.0.0.1:11434
        ↓
Gemma 3
```

The Unity game should normally talk to the **LLM Bridge**, not directly to Ollama. The bridge handles prompt building, model selection, timeouts, tool calls, JSON validation, and debug logging.

---

## 1. Install Ollama for Windows

1. Download and install Ollama for Windows from the official Ollama site.
2. Launch **Ollama** from the Start menu.
3. Confirm Ollama is running in the Windows system tray.
4. Open **PowerShell**.

Check that Ollama is available:

```powershell
ollama --version
```

---

## 2. Pull Gemma 3

Use Gemma 3 as the default local model:

```powershell
ollama pull gemma3
```

Quick interactive test:

```powershell
ollama run gemma3
```

Try a short prompt such as:

```text
Say hello from the local LLM for A Dog's Tale.
```

Exit the chat with:

```text
/bye
```

---

## 3. Verify Ollama's local API

Ollama normally listens on:

```text
http://127.0.0.1:11434
```

Check installed models:

```powershell
curl http://127.0.0.1:11434/api/tags
```

Test generation from PowerShell:

```powershell
$body = @{
  model = "gemma3"
  prompt = "Say hello from Gemma 3 running locally on Windows."
  stream = $false
} | ConvertTo-Json

Invoke-RestMethod `
  -Uri "http://127.0.0.1:11434/api/generate" `
  -Method Post `
  -ContentType "application/json" `
  -Body $body
```

If you see a response field with generated text, Ollama is working.

---

## 4. Configure Ollama environment variables, if needed

For local development, the default is usually correct:

```text
OLLAMA_HOST=127.0.0.1:11434
```

To set this explicitly on Windows:

1. Quit Ollama from the system tray.
2. Open **Start** and search for **environment variables**.
3. Choose **Edit environment variables for your account**.
4. Under **User variables**, create or edit:

```text
OLLAMA_HOST
```

with value:

```text
127.0.0.1:11434
```

5. Click **OK / Apply**.
6. Restart Ollama.

Only use this LAN-accessible version if another computer needs to call your Windows machine:

```text
OLLAMA_HOST=0.0.0.0:11434
```

For normal Unity + LLM Bridge development on one PC, prefer `127.0.0.1`.

---

## 5. Start the LLM Bridge

Example project layout:

```text
C:\Users\Mark\Desktop\Programming\Unity\A_Dogs_Tale\llm_runner_py
```

Open PowerShell and go to your bridge folder:

```powershell
cd C:\Users\Mark\Desktop\Programming\Unity\A_Dogs_Tale\llm_runner_py
```

Activate the Python virtual environment:

```powershell
.\.venv\Scripts\Activate.ps1
```

If PowerShell blocks script activation, run this once for your user account:

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

Then activate again:

```powershell
.\.venv\Scripts\Activate.ps1
```

Point the bridge at Ollama:

```powershell
$env:OLLAMA_BASE_URL = "http://127.0.0.1:11434"
```

Start the bridge:

```powershell
python -m uvicorn server:app --reload --host 127.0.0.1 --port 8000
```

The bridge should now be available at:

```text
http://127.0.0.1:8000
```

Health check:

```powershell
curl http://127.0.0.1:8000/health
```

---

## 6. Optional: create a one-click bridge launcher

Create a file named:

```text
run_llm_bridge.ps1
```

Put this inside it, adjusting the folder path if needed:

```powershell
cd "C:\Users\Mark\Desktop\Programming\Unity\A_Dogs_Tale\llm_runner_py"

.\.venv\Scripts\Activate.ps1

$env:OLLAMA_BASE_URL = "http://127.0.0.1:11434"

python -m uvicorn server:app --reload --host 127.0.0.1 --port 8000
```

Run it from PowerShell:

```powershell
.\run_llm_bridge.ps1
```

---

## 7. Unity settings

In Unity, expose these somewhere simple, such as `LLMConfigModule`, `LLMSettings`, or a ScriptableObject:

```csharp
public string plannerServerUrl = "http://127.0.0.1:8000/plan";
public string ollamaModelName = "gemma3";
public float requestTimeoutSeconds = 60f;
```

Recommended connection pattern:

```text
Unity → LLM Bridge → Ollama → Gemma 3
```

Avoid calling Ollama directly from Unity unless you are doing a quick experiment. The LLM Bridge is the right place for prompts, model routing, tool calls, schema validation, and fallbacks.

---

## 8. Useful diagnostics

Check Ollama:

```powershell
curl http://127.0.0.1:11434/api/tags
```

Check the LLM Bridge:

```powershell
curl http://127.0.0.1:8000/health
```

Check what is using Ollama's port:

```powershell
netstat -ano | findstr :11434
```

Check what is using the bridge port:

```powershell
netstat -ano | findstr :8000
```

Kill a stuck bridge process by process ID:

```powershell
taskkill /PID <PID_NUMBER> /F
```

Example:

```powershell
taskkill /PID 12345 /F
```

---

## 9. Recommended hardware footnote

For reasonable local LLM performance on Windows:

- **Good baseline:** modern Windows PC with **16 GB RAM** and a recent NVIDIA GPU with **8 GB+ VRAM**.
- **Better:** **32 GB RAM** and **12 GB+ VRAM**, especially for larger context windows or multiple game agents.
- **Gemma 3 default recommendation:** start with `gemma3`, but choose a smaller variant if generation is too slow.
- **Weaker machines:** try `gemma3:4b` or `gemma3:1b` instead of a larger Gemma 3 model.
- **Very weak machines / laptop CPU only:** use `gemma3:1b` for quick tests, then upgrade once the full Unity + LLM Bridge loop is working.

Suggested fallback commands:

```powershell
ollama pull gemma3:4b
ollama pull gemma3:1b
```

Then update your Unity or bridge model setting:

```text
ollamaModelName = gemma3:4b
```

or:

```text
ollamaModelName = gemma3:1b
```

---

## Hacker's Summary

```powershell
ollama pull gemma3
curl http://127.0.0.1:11434/api/tags

cd C:\Users\Mark\Desktop\Programming\Unity\A_Dogs_Tale\llm_runner_py
.\.venv\Scripts\Activate.ps1
$env:OLLAMA_BASE_URL = "http://127.0.0.1:11434"
python -m uvicorn server:app --reload --host 127.0.0.1 --port 8000

curl http://127.0.0.1:8000/health
```

Use this runtime path:

```text
A Dog's Tale → LLM Bridge → Ollama → Gemma 3
```
