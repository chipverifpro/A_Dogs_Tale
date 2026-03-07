TOOLS = [
    {
        "type": "function",
        "function": {
            "name": "bark",
            "description": "Dog barks loudly",
            "parameters": {
                "type": "object",
                "properties": {
                    "count": {
                        "type": "integer",
                        "minimum": 1,
                        "description": "Number of barks"
                    }
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
                    "seconds": {
                        "type": "number",
                        "minimum": 1.0,
                        "description": "How long to flee. Use at least 1 second."
                    }
                }
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "get_world_state",
            "description": "Request the current curated world state for this dog from Unity. Use this when you need more situational information before deciding what to do next.",
            "parameters": {
                "type": "object",
                "properties": {
                    "detail": {
                        "type": "string",
                        "enum": ["brief", "normal", "detailed"],
                        "description": "How much world detail to request."
                    }
                }
            }
        }
    }
]