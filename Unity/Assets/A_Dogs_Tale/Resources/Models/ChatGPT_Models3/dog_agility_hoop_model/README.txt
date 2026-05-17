Dog Agility Hoop / Ring Jump - Import Guide
===========================================

Files:
- dog_agility_hoop.obj
- dog_agility_hoop.mtl

Suggested Unity import folder:
Assets/A_Dogs_Tale/Resources/Models/Agility/DogAgilityHoop/

Suggested prefab folder:
Assets/A_Dogs_Tale/Resources/Prefabs/Agility/PF_DogAgilityHoop.prefab


Model dimensions
----------------
Approximate dimensions:
- Width: 1.55 m
- Height: 1.62 m
- Depth: 0.52 m
- Hoop center height: 0.95 m
- Hoop opening diameter: about 0.82 m

This is a static jump-through hoop suitable for a dog agility course.


Named parts
-----------
The OBJ contains separate named pieces, including:
- left_upright / right_upright
- main_hoop_ring
- base feet and cross pieces
- top caps
- support brackets
- rear_stability_bar
- paw markers

This makes it easier to inspect, reparent, or replace pieces in Blender or Unity.


Recommended prefab structure
----------------------------
PF_DogAgilityHoop
  BaseAndStandards
  HoopRing

If you want the hoop to be removable or animated, place the ring under its own child:
- HoopRing at world position near (0, 0.95, 0)


Collider setup
--------------
Use simple colliders instead of a mesh collider where possible.

Suggested collider approach:
1. Two BoxColliders for the left and right upright/stand areas.
2. Two or more BoxColliders for the base feet.
3. Optional thin trigger collider in the hoop opening area to detect a successful jump through.

Recommended trigger for success:
- Center: (0, 0.95, 0)
- Size: (0.70, 0.70, 0.25)

That gives a good "passed through the hoop" detection volume.


Gameplay ideas
--------------
Possible uses in A Dog's Tale:
- Training obstacle in a backyard or park
- Scored agility course segment
- InteractionModule commands such as:
  - examine_hoop
  - jump_through_hoop
  - train_hoop
  - reward_jump

Possible scoring/events:
- +1 approaches hoop on command
- +1 jumps cleanly through opening
- +1 lands and continues forward
- +1 completes without touching the frame


Optional animation ideas
------------------------
This model is static by default, but you could animate:
- A removable hoop ring that falls out if hit
- A gentle idle wobble on the ring
- A glowing highlight when the hoop is the current training target

For a wobble:
- Put main_hoop_ring under a child GameObject named HoopPivot
- Rotate HoopPivot slightly around Z or X by a few degrees


Suggested module idea
---------------------
PF_DogAgilityHoop
  WorldObject
  InteractionModule
  HoopObstacleModule

HoopObstacleModule could:
- Detect entry into the trigger volume
- Detect direction of travel
- Distinguish a real jump from just standing in the trigger
- Emit BottomBanner log messages
- Award training progress or score
