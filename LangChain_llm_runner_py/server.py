from fastapi import FastAPI
from pydantic import BaseModel
from typing import Any, Dict, List, Optional
import time

app = FastAPI()


class TriggerV2(BaseModel):
    type: str
    location: Optional[List[float]] = None
    intensity: Optional[float] = None


class PlanRequestV2(BaseModel):
    schema: str
    agent_id: str
    trigger: TriggerV2
    world_state: Optional[Dict[str, Any]] = None
    constraints: Optional[Dict[str, Any]] = None


@app.get("/health")
async def health() -> Dict[str, str]:
    return {"status": "ok"}

@app.post("/plan")
async def plan(request: PlanRequestV2) -> Dict[str, Any]:
    start_time = time.time()

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
            "latency_ms": int((time.time() - start_time) * 1000),
            "request_schema": request.schema,
            "trigger_type": request.trigger.type
        }
    }

    return response