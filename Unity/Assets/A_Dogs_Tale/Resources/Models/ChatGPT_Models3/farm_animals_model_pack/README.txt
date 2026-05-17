Farm Animal Model Pack
======================

Included models
---------------
- goose.obj
- sheep.obj
- dairy_cow.obj
- brown_bull.obj
- farm_animals_materials.mtl

Style
-----
These are simple low-poly / game-friendly farm animal models made from primitive shapes.
They are intended to be easy to import into Unity and easy to turn into prefabs.

Suggested Unity import folder
-----------------------------
Assets/A_Dogs_Tale/Resources/Models/Animals/Farm/

Suggested prefab folder
-----------------------
Assets/A_Dogs_Tale/Resources/Prefabs/Animals/Farm/

General notes
-------------
- Forward direction is +X for all models.
- Scale is roughly meters-ish, suitable for Unity.
- Materials are shared across the whole pack.
- Geometry is intentionally simple for easy use and editing.

Approximate sizes
-----------------
Goose:
- Length about 1.3 m
- Height about 1.6 m

Sheep:
- Length about 1.2 m
- Height about 1.1 m

Dairy Cow:
- Length about 2.0 m
- Height about 1.3 m

Brown Bull:
- Length about 2.2 m
- Height about 1.35 m

Suggested collider setup
------------------------
For each animal:
- 1 BoxCollider or CapsuleCollider for body
- 1 smaller BoxCollider for head if needed
- Keep colliders simple unless you need precise interaction

Suggested prefab hierarchy
--------------------------
PF_Goose
  ModelRoot
  WorldObject
  InteractionModule
  optional Animal modules

Likewise for sheep, dairy cow, and brown bull.

Possible uses
-------------
- decorative farmyard animals
- scent or interaction targets
- NPC animals
- training distractions or environmental life

Tips
----
If you want cleaner transforms:
1. Drag OBJ into the scene
2. Create an empty parent GameObject
3. Make the imported mesh a child
4. Adjust rotation/scale on the child if needed
5. Save as a prefab
