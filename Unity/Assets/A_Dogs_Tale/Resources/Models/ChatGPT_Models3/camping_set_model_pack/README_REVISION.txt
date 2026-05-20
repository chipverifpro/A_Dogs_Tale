Camping Set Revision / Add-on Pack
===================================

This revision pack replaces or adds the pieces you called out from the first camping set.

Included:
- big_wall_tent_open.obj
- flat_sleeping_bag.obj
- tree_stump_seat.obj
- comfy_camp_chair.obj
- camping_lantern.obj
- camping_revision_materials.mtl
- manifest.json

Changes:
1. Four-sided tent:
   - Now closer to a pop-up canopy / wall tent.
   - Has grey walls, dark pointed roof, red trim, black corner poles, lower skirt panels.
   - Includes a large front doorway and side openings.
   - Still open/walkable inside; use simple colliders only on posts/walls/roof and keep the doorway clear.

2. Sleeping bag:
   - Rebuilt flat to the ground.
   - Red body with grey hood/head panel.
   - Low height so it reads like a sleeping bag lying on the floor/ground.

3. Tree stump:
   - Cylindrical stump seat with bark sides, cut top, growth rings, and a small moss patch.

4. Camp chair:
   - Wider, padded-looking seat and back.
   - Arm rests and cup holder.
   - Folding metal frame.

5. Camping lantern:
   - Green base/cap, translucent-looking globe material, warm glow core, side supports, and handle.

Suggested Unity folder:
Assets/A_Dogs_Tale/Resources/Models/Camping/Revisions/

Suggested prefab folder:
Assets/A_Dogs_Tale/Resources/Prefabs/Camping/

Collider suggestions:
- big_wall_tent_open: separate colliders for walls/posts/roof; no floor collider; leave doorway/interior open.
- flat_sleeping_bag: one low BoxCollider, or trigger-only if dogs/people can lie on it.
- tree_stump_seat: CapsuleCollider or Cylinder-like MeshCollider if needed.
- comfy_camp_chair: simplified BoxColliders for seat/back and trigger for sit interaction.
- camping_lantern: CapsuleCollider; optional Light component parented near warm_light_core.

Lantern Unity tip:
Replace LanternGlow with an emissive material and add a small Point Light. If using URP bloom, it will look much better.
