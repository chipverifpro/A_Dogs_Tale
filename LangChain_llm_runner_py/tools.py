# tools.py
#
# This file defines the Python implementations of the planner tools.
#
# Its job is to convert a tool call into an internal intention object.
#
# For example:
#	•	bark(count=1) returns {"type": "Bark", "count": 1}
#	•	flee_from_noise(seconds=2) returns {"type": "FleeFromNoise", "seconds": 2}
#
# These are not Unity actions directly. They are intention builders.
#
# This is useful because it keeps the meaning of each tool centralized.
#
# LLM tool call
# → Python tool function
# → intention dict for Unity

import json
import os
from typing import Any, Dict

import requests


UNITY_BASE_URL = os.getenv("UNITY_BASE_URL", "http://127.0.0.1:8081")


def bark(count: int = 1) -> Dict[str, Any]:
    safe_count = max(1, int(count))
    return {
        "type": "Bark",
        "count": safe_count
    }


def flee_from_noise(seconds: float = 2.0) -> Dict[str, Any]:
    safe_seconds = max(1.0, float(seconds))
    return {
        "type": "FleeFromNoise",
        "seconds": safe_seconds
    }


def action_tool_result(action: Dict[str, Any]) -> str:
    action_type = action.get("type", "UnknownAction")
    return f"Action queued for Unity execution: {json.dumps(action)}"


def request_unity_world_state(agent_id: str, detail: str = "normal") -> str:
    """
    Calls back into Unity to request the current world state for one agent.

    Expected Unity endpoint:
        POST {UNITY_BASE_URL}/world_state

    Expected request body:
        {
          "agent_id": "dog_1",
          "detail": "brief" | "normal" | "detailed"
        }

    Supported response shapes from Unity:
        1)
        {
          "world_state_text": "..."
        }

        2)
        {
          "world_state": "..."
        }

        3)
        {
          "world_state_json": { ... }
        }

    The planner prefers text. If Unity returns JSON, we stringify it.
    """

    safe_detail = detail.strip().lower() if detail else "normal"
    if safe_detail not in {"brief", "normal", "detailed"}:
        safe_detail = "normal"

    payload = {
        "agent_id": agent_id,
        "detail": safe_detail
    }

    response = requests.post(
        f"{UNITY_BASE_URL}/world_state",
        json=payload,
        timeout=30
    )

    if response.status_code != 200:
        raise RuntimeError(
            f"Unity world_state error {response.status_code}: {response.text}"
        )

    data = response.json()

    if "world_state_text" in data and isinstance(data["world_state_text"], str):
        return data["world_state_text"]

    if "world_state" in data and isinstance(data["world_state"], str):
        return data["world_state"]

    if "world_state_json" in data:
        return json.dumps(data["world_state_json"], indent=2)

    raise RuntimeError(
        f"Unity world_state response missing expected field: {data}"
    )