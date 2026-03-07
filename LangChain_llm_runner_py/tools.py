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

from typing import Dict, Any


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