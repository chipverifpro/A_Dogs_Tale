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