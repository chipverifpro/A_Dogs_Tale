# server.py
#
# This is the web entry point.
#
# Its job is to:
#	•	start the FastAPI app
#	•	expose routes like /health and /plan
#	•	receive the incoming request from Unity
#	•	pass that request to the planner
#	•	return the planner’s result back to Unity
#
# It should stay thin. It is basically the HTTP wrapper around your planner.
#
# Unity → server.py → planner → Unity

from fastapi import FastAPI
from typing import Dict, Any

from schemas import PlanRequestV2
from planner_llm import llm_generate_plan

app = FastAPI()


@app.get("/health")
async def health() -> Dict[str, str]:
    return {"status": "ok"}


@app.post("/plan")
async def plan(request: PlanRequestV2) -> Dict[str, Any]:

    print("Received plan request:", request.trigger.type)

    response = llm_generate_plan(request)

    return response