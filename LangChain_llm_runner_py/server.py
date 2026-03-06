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