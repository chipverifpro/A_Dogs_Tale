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


### How they all fit together
#
# Unity
#   ↓
# server.py
#   ↓
# schemas.py validates request
#   ↓
# planner_llm.py asks LLM what to do
#   ↓
# mcp_tools.py tells LLM what tools exist
#   ↓
# LLM chooses tool calls
#   ↓
# tools.py converts tool calls into intentions
#   ↓
# planner_llm.py builds plan_response_v2
#   ↓
# server.py returns JSON to Unity


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