Dog Agility Course Set
======================

This ZIP contains a matching set of low-poly Unity-friendly dog agility obstacles in OBJ format.
All pieces share the same material palette and visual style.

Included models
---------------
1. dog_agility_hoop.obj      - hoop / ring jump
2. dog_teeter_totter.obj     - teeter / seesaw obstacle
3. dog_bar_jump.obj          - bar jump
4. dog_weave_poles.obj       - 12-pole weave set
5. dog_tunnel.obj            - straight tunnel
6. dog_pause_table.obj       - pause table / platform
7. dog_a_frame.obj           - A-frame ramp obstacle
8. dog_dog_walk.obj          - dog walk with center bridge and ramps
9. agility_course_materials.mtl - shared materials file

Suggested Unity folders
-----------------------
Models:
Assets/A_Dogs_Tale/Resources/Models/Agility/CourseSet/

Prefabs:
Assets/A_Dogs_Tale/Resources/Prefabs/Agility/

General style
-------------
- White frames / standards
- Blue feet and support bases
- Yellow contact zones / highlighted obstacle elements
- Red accents
- Brown board surfaces with darker grip areas

Recommended import workflow
---------------------------
1. Copy all OBJ files plus agility_course_materials.mtl into the same Unity folder.
2. Drag each OBJ into the scene once to verify scale and materials.
3. Create prefabs for each obstacle.
4. Add simple BoxColliders / CapsuleColliders instead of relying on MeshCollider when possible.
5. For animated obstacles (such as the teeter), create a clean child pivot object and animate that.

Suggested obstacle-specific notes
---------------------------------
Hoop:
- Add a trigger volume through the ring opening.

Teeter:
- Separate the board under a BoardPivot child and rotate around local Z.

Bar Jump:
- Make the bar its own child if you want it removable or knockable.

Weave Poles:
- Use trigger regions between poles for training progress tracking if desired.

Tunnel:
- Use a long trigger or pathing assist volume through the center.

Pause Table:
- Simple top collider; can be used for "wait" or "stay" training.

A-Frame:
- Add contact zone triggers near the yellow sections.

Dog Walk:
- Similar to A-Frame, with contact triggers at ramp ends.

Suggested gameplay uses in A Dog's Tale
---------------------------------------
- Backyard training course
- Formal agility challenge
- Pack training / obstacle familiarization
- Scored course runs
- Skill progression minigames

Possible next steps
-------------------
- Add matching scripts / module stubs for each obstacle.
- Make break-apart or animated versions.
- Add decorative signs, cones, timing gates, and course number markers.
- Make a sample combined layout prefab.

Approximate style target
------------------------
These models are intentionally simple, modular, and game-friendly rather than highly detailed.
They should be easy to import, recolor, collider-fit, and convert into prefabs.
