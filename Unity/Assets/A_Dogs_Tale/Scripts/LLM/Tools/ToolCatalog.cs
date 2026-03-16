#nullable enable
namespace DogGame.LLM.Tools
{
    public static class ToolCatalog
    {
        /*
        public static string PlanIntentionTypeListText =>
@"ALLOWED intention.type (PlanIntentionType):
- noop
- set_goal
- add_task
- propose_trap
- propose_dialogue
- request_observation
- update_beliefs

Guidelines:
- Prefer 1–4 intentions.
- Use noop only when truly no action is appropriate.
- Keep rationale short (or omit it).";

    public static string AvailableIntentionsText =>
@"AVAILABLE ACTIONS (intention.type):

Movement:
- move_to_location
  Parameters:
    - target: string (description of location or object)
    - stopDistanceMeters?: number

- move_to_object
  Parameters:
    - objectId: string

- wait
  Parameters:
    - durationSeconds?: number

- wait_until
  Parameters:
    - condition: string (short, observable condition)

Perception:
- look_at
  Parameters:
    - target: string

- sniff
  Parameters:
    - focus?: string (what scent to prioritize)

- follow_scent_trail
  Parameters:
    - scentType?: string

Communication / Expression:
- speak
  Parameters:
    - message: string
    - tone?: string

- bark
  Parameters:
    - intensity?: string (low | normal | urgent)

- emote
  Parameters:
    - kind: string (tail_wag, growl, whine, etc.)

Items:
- take_item
  Parameters:
    - itemId: string

- drop_item
  Parameters:
    - itemId: string

- bury_item
  Parameters:
    - itemId: string
    - location?: string

Goals / Planning:
- push_goal
  Parameters:
    - goal: string
    - priority?: number

Control / Safety:
- abort
  Parameters: none

Rules:
- Use only the actions listed above.
- Do NOT invent new actions.
- If no action is appropriate, return a single noop intention.";
        */

    }
}
