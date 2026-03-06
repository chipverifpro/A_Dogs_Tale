from pydantic import BaseModel
from typing import List, Optional


class TriggerV2(BaseModel):
    type: str
    location: Optional[List[float]] = None
    intensity: Optional[float] = None


class WorldStateV2(BaseModel):
    position: Optional[List[float]] = None
    pack_members_nearby: Optional[int] = None


class ConstraintsV2(BaseModel):
    max_plan_steps: Optional[int] = 4
    max_latency_ms: Optional[int] = 1000


class PlanRequestV2(BaseModel):
    schema: str
    agent_id: str
    trigger: TriggerV2
    world_state: Optional[WorldStateV2] = None
    constraints: Optional[ConstraintsV2] = None


class PlanIntentionV2(BaseModel):
    type: str
    count: Optional[int] = None
    seconds: Optional[float] = None


class DebugInfoV2(BaseModel):
    model: str
    latency_ms: int
    request_schema: Optional[str] = None
    trigger_type: Optional[str] = None


class PlanResponseV2(BaseModel):
    schema: str
    intentions: List[PlanIntentionV2]
    debug: Optional[DebugInfoV2] = None