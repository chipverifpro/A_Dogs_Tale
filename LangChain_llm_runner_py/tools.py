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