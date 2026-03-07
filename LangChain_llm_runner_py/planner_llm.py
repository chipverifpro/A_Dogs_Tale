# planner_llm.py
#
# This is the real planner.
#
# Its job is to:
#	•	build the prompt
#	•	call the LLM
#	•	handle tool calls or structured output
#	•	translate the model’s answer into your plan_response_v2
#	•	sanitize arguments
#	•	return safe intention dictionaries Unity can execute
#
# If server.py is the front door, planner_llm.py is the brain.
#
# PlanRequestV2
# → prompt / tools
# → LLM
# → intentions
# → PlanResponseV2-compatible dict

import time
import json
import requests
from typing import Dict, Any

from schemas import PlanRequestV2
from planner_stub import stub_generate_plan


OLLAMA_URL = "http://127.0.0.1:11434/api/chat"
OLLAMA_MODEL = "functiongemma"


TOOLS = [
    {
        "type": "function",
        "function": {
            "name": "bark",
            "description": "Dog barks loudly",
            "parameters": {
                "type": "object",
                "properties": {
                    "count": {"type": "integer"}
                }
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "flee_from_noise",
            "description": "Dog runs away from a loud noise",
            "parameters": {
                "type": "object",
                "properties": {
                    "seconds": {"type": "number"}
                }
            }
        }
    }
]


def bark(count: int = 1) -> Dict[str, Any]:
    return {
        "type": "Bark",
        "count": count
    }


def flee_from_noise(seconds: float = 2.0) -> Dict[str, Any]:
    return {
        "type": "FleeFromNoise",
        "seconds": seconds
    }

def build_prompt(request: PlanRequestV2) -> str:
    return f"""
Dog planning request.

Trigger: {request.trigger.type}

If the dog hears a loud noise it should usually:
1. bark
2. flee_from_noise for about 2 seconds

Use the available tools to represent the actions.

You may call multiple tools in sequence.
Call one tool at a time.
Stop calling tools when the plan is complete.

Do not explain actions.
Do not produce text responses.
Only call tools.
"""

def llm_generate_plan(request: PlanRequestV2):

    start = time.time()

    try:

        messages = [
            {
                "role": "system",
                "content": """
You control a dog in a simulation.

You must decide actions by calling tools.

Typical loud noise response:
1. bark
2. flee_from_noise for about 2 seconds

Call one tool at a time.
When no more actions are needed, stop calling tools.
"""
            },
            {
                "role": "user",
                "content": build_prompt(request)
            }
        ]

        intentions = []

        MAX_STEPS = 5

        for step in range(MAX_STEPS):

            payload = {
                "model": OLLAMA_MODEL,
                "messages": messages,
                "tools": TOOLS,
                "stream": False
            }

            r = requests.post(OLLAMA_URL, json=payload, timeout=120)

            if r.status_code != 200:
                raise RuntimeError(f"Ollama error {r.status_code}: {r.text}")

            data = r.json()

            print(json.dumps(data, indent=2))

            tool_calls = data.get("message", {}).get("tool_calls", [])

            if not tool_calls:
                break

            for call in tool_calls:

                function_block = call.get("function", {})

                name = function_block.get("name", "")
                args = function_block.get("arguments", {}) or {}

                if name == "bark":
                    result = bark(**args)
                    intentions.append(result)

                elif name == "flee_from_noise":
                    result = flee_from_noise(**args)
                    intentions.append(result)

                else:
                    print("Unknown tool:", name)
                    continue

                # feed tool result back into conversation
                messages.append(data["message"])

                messages.append({
                    "role": "tool",
                    "content": json.dumps(result)
                })

        if not intentions:
            intentions.append(bark())

        return {
            "schema": "plan_response_v2",
            "intentions": intentions,
            "debug": {
                "model": OLLAMA_MODEL,
                "latency_ms": int((time.time() - start) * 1000),
                "trigger_type": request.trigger.type
            }
        }

    except Exception as e:

        print("LLM planner failed:", e)

        return stub_generate_plan(request)