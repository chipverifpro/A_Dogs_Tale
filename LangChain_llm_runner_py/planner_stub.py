# planner_stub.py
#
# This is the safe fallback planner.
#
# Its job is to:
#	•	return a known-good hardcoded response
#	•	keep the pipeline working if the LLM fails
#	•	give you a reliable debug mode
#	•	help isolate whether a bug is in Unity/networking or in the LLM
#
# This is very useful because it lets you answer:
#
# “Is the system broken, or is only the model broken?”
#
# If LLM fails → use stub → Unity still receives a valid plan

import time
from typing import Dict, Any
from schemas import PlanRequestV2


def stub_generate_plan(request: PlanRequestV2) -> Dict[str, Any]:
    start = time.time()

    return {
        "schema": "plan_response_v2",
        "intentions": [
            {
                "type": "Bark",
                "count": 1
            },
            {
                "type": "FleeFromNoise",
                "seconds": 2.0
            }
        ],
        "debug": {
            "model": "stub",
            "latency_ms": int((time.time() - start) * 1000),
            "request_schema": request.schema_name,
            "trigger_type": request.trigger.type
        }
    }