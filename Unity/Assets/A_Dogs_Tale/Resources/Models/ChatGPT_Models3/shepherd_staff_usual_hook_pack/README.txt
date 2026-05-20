Shepherd Staff with Usual Hook - Revised Pack
=============================================

Included:
- shepherd_staff_usual_hook.obj
- shepherd_boy_holding_staff_usual_hook.obj
- shepherd_staff_usual_hook_materials.mtl
- manifest.json

This revision replaces the previous curled/spiral-looking hook with a more typical
shepherd's crook:
- long straight shaft
- smooth rounded U-shaped hook at the top
- open downward-facing tip
- no spiral curl

Approximate dimensions:
- Standalone staff: about 1.95 m tall
- Boy model: about 1.55 m to top of hair
- Held staff: about 1.95 m tall, clearly taller than the boy

Suggested Unity folder:
Assets/A_Dogs_Tale/Resources/Models/Characters/Shepherd/

Suggested prefab folder:
Assets/A_Dogs_Tale/Resources/Prefabs/Characters/

Forward direction:
- The boy faces +X.
- The staff is centered vertically with the hook in the X/Y plane.

Prefab tips:
1. Import OBJ and MTL into the same Unity folder.
2. Create an empty root GameObject for each prefab.
3. Put the imported OBJ under ModelRoot.
4. Use the root for WorldObject / InteractionModule / AgentModule.
5. Use ModelRoot for visual rotation/scale adjustments.

Collider suggestions:
- Staff: thin CapsuleCollider or BoxCollider along the shaft.
- Boy: CapsuleCollider around the body.
- Keep the hook collider optional unless you need precise staff interactions.
