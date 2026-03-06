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
                        "description": "How long to flee. Use at least 1.0 seconds."
                    }
                }
            }
        }
    }
]