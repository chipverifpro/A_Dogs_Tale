Camping Set Model Pack
======================

Included OBJ models:
- small_dome_tent_open.obj
- small_triangle_tent_open.obj
- big_four_sided_pointed_tent_open.obj
- campfire.obj
- sitting_log.obj
- camp_chair.obj
- camp_table.obj
- sleeping_bag.obj
- backpacker_man.obj
- backpacker_woman.obj
- backpacker_boy.obj
- backpacker_girl.obj
- bear.obj
- deer.obj
- camping_set_materials.mtl
- manifest.json

Style:
Rounded low-poly / game-friendly camping models. I avoided blocky cube-built bodies where possible:
characters and animals use ellipsoids/tapered cylinders, props use cylinders/rounded forms, and the tents use
open shell-style geometry.

Tent interiors:
The tents are intentionally open to the inside:
- small dome tent has an open front doorway and no floor
- small triangle tent has open front/rear ends and no floor
- big four-sided pointed tent has posts and roof only, with open sides

Suggested Unity folder:
Assets/A_Dogs_Tale/Resources/Models/Camping/

Suggested prefab folder:
Assets/A_Dogs_Tale/Resources/Prefabs/Camping/

Unity setup notes:
- Put all OBJ files and camping_set_materials.mtl in the same folder.
- Make an empty prefab root for each model and put the imported mesh under ModelRoot.
- Use the prefab root for WorldObject, InteractionModule, AgentModule, etc.
- Use ModelRoot for scale/rotation tweaks.
- For tents, use simple post/roof colliders and leave doorway/interior unblocked.
- For campfire, use a trigger zone if you want heat/danger behavior.
- For log/chair/table, add simple Box/Capsule colliders for sit/interact zones.

A Dog's Tale ideas:
- tent_enter / tent_exit
- sleep_in_sleeping_bag
- sit_on_log / sit_on_chair
- sniff_campfire
- react_to_bear
- react_to_deer
- backpacker NPC quest givers or campers
