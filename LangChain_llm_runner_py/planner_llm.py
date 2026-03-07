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

import json
import os
import time
from typing import Any, Dict, List

import requests

from schemas import PlanRequestV2
from planner_stub import stub_generate_plan
from mcp_tools import TOOLS
from tools import (
    bark,
    flee_from_noise,
    action_tool_result,
    request_unity_world_state,
)


OLLAMA_URL = os.getenv("OLLAMA_URL", "http://127.0.0.1:11434/api/chat")
OLLAMA_MODEL = os.getenv("OLLAMA_MODEL", "functiongemma")


ACTION_TOOL_NAMES = {
    "bark",
    "flee_from_noise",
}

INFO_TOOL_NAMES = {
    "get_world_state",
}


def build_prompt(request: PlanRequestV2) -> str:
    return f"""
Dog planning request.

Agent ID: {request.agent_id}
Trigger: {request.trigger.type}

You control a dog in a simulation.

Rules:
- Use tools to decide actions.
- If you already know enough, call action tools.
- If you need situational awareness, call get_world_state(detail).
- Prefer detail="normal" first.
- Use detail="brief" only for very quick checks.
- Use detail="detailed" only if you need more than the normal summary.
- You may call multiple tools.
- Call one or more tools until the plan is complete.
- Do not produce explanatory prose unless no tool call is needed.
"""


def llm_generate_plan(request: PlanRequestV2) -> Dict[str, Any]:
    start = time.time()

    try:
        messages: List[Dict[str, Any]] = [
            {
                "role": "system",
                "content": """
You control a dog in a simulation.

Decide what the dog should do by calling tools.

Available tool categories:
- Action tools: these produce actions for Unity to execute later.
- Information tools: these query Unity immediately for current world information.

When you need more context, call get_world_state(detail).

Do not explain actions.
Do not produce normal text if a tool call is appropriate.
Prefer tool calls over prose.
""".strip()
            },
            {
                "role": "user",
                "content": build_prompt(request).strip()
            }
        ]

        intentions: List[Dict[str, Any]] = []
        max_steps = 6

        for step_index in range(max_steps):
            payload = {
                "model": OLLAMA_MODEL,
                "messages": messages,
                "tools": TOOLS,
                "stream": False
            }

            response = requests.post(
                OLLAMA_URL,
                json=payload,
                timeout=120
            )

            if response.status_code != 200:
                raise RuntimeError(
                    f"Ollama error {response.status_code}: {response.text}"
                )

            data = response.json()
            print(json.dumps(data, indent=2))

            assistant_message = data.get("message", {})
            tool_calls = assistant_message.get("tool_calls", [])

            if not tool_calls:
                break

            messages.append(assistant_message)

            for call in tool_calls:
                function_block = call.get("function", {})
                tool_name = function_block.get("name", "")
                tool_args = function_block.get("arguments", {}) or {}

                if tool_name == "bark":
                    action = bark(**tool_args)
                    intentions.append(action)

                    messages.append({
                        "role": "tool",
                        "content": action_tool_result(action)
                    })

                elif tool_name == "flee_from_noise":
                    action = flee_from_noise(**tool_args)
                    intentions.append(action)

                    messages.append({
                        "role": "tool",
                        "content": action_tool_result(action)
                    })

                elif tool_name == "get_world_state":
                    requested_detail = tool_args.get("detail", "normal")
                    world_state_text = request_unity_world_state(
                        agent_id=request.agent_id,
                        detail=requested_detail
                    )

                    messages.append({
                        "role": "tool",
                        "content": f"WORLD STATE ({requested_detail}):\n{world_state_text}"
                    })

                else:
                    print(f"Unknown tool call ignored: {tool_name}")
                    messages.append({
                        "role": "tool",
                        "content": f"Unknown tool ignored: {tool_name}"
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

    except Exception as exception:
        print("LLM planner failed:", exception)
        return stub_generate_plan(request)