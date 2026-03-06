import os
import time
import json
import requests
from typing import Dict, Any

from schemas import PlanRequestV2
from planner_stub import stub_generate_plan


OLLAMA_URL = os.getenv("OLLAMA_URL", "http://127.0.0.1:11434/api/generate")
OLLAMA_MODEL = os.getenv("OLLAMA_MODEL", "gemma3")


SYSTEM_PROMPT = """
You are a dog behavior planner for a simulation.

You must return ONLY valid JSON.

The format MUST be:

{
  "schema": "plan_response_v2",
  "intentions": [
    {"type": "Bark", "count": 1},
    {"type": "FleeFromNoise", "seconds": 2.0}
  ]
}

Do not include explanation.
Do not include markdown.
Only return JSON.
"""


def build_prompt(request: PlanRequestV2) -> str:
    return f"""
Dog Agent Planning Request

Trigger: {request.trigger.type}

Return a plan_response_v2 JSON plan for the dog.
"""


def call_ollama(prompt: str) -> str:

    payload = {
        "model": OLLAMA_MODEL,
        "prompt": SYSTEM_PROMPT + "\n" + prompt,
        "stream": False
    }

    r = requests.post(OLLAMA_URL, json=payload, timeout=20)

    if r.status_code != 200:
        raise RuntimeError(f"Ollama error: {r.status_code}")

    data = r.json()

    return data["response"]


def llm_generate_plan(request: PlanRequestV2) -> Dict[str, Any]:

    start = time.time()

    try:

        prompt = build_prompt(request)

        response_text = call_ollama(prompt)

        parsed = json.loads(response_text)

        parsed["debug"] = {
            "model": OLLAMA_MODEL,
            "latency_ms": int((time.time() - start) * 1000),
            "trigger_type": request.trigger.type
        }

        return parsed

    except Exception as e:

        print("LLM planner failed, using stub:", e)

        return stub_generate_plan(request)