Canine Tow Clip - Detailed Latch Revision
=========================================

Files:
- CanineTowClip_DetailedLatch.obj
- CanineTowClip_DetailedLatch.mtl

What changed from the first version:
- The latch is now a curved pivoting self-locking gate, closer to the original infographic.
- Added a visible hinge pin with alternating hinge knuckles.
- Added a torsion spring wrapped around the hinge.
- Added spring tails so the closing mechanism reads visually.
- Added a gate tip / hooked nose that engages a catch shelf.
- Added a stop block and stop pin.
- Added a beveled press surface, suggesting that an anchor ring can push the gate inward during placement.
- Added Optional_Ghost_Open_Gate_Position_Reference so you can see/preview the open swing path. Delete or hide this part for final gameplay.

Suggested gameplay interpretation:
1. Dog carries the tool by Blue_Padded_Bite_Grip.
2. When the hook mouth touches an AnchorPoint ring/bar, the ring presses the gate's beveled surface.
3. The Orange_Pivoting_Self_Locking_Gate_Curved_Closed rotates inward briefly.
4. After the ring passes inside the hook throat, the Visible_Torsion_Spring_Around_Hinge closes the gate.
5. The Gate_Tip_Hooked_Nose_Engages_Catch rests against Inner_Catch_Shelf_For_Gate_Tip.
6. Pull force goes through the main hook body and rear swivel ring, not through a fragile latch.

Unity setup:
1. Put the OBJ and MTL in:
   Assets/A_Dogs_Tale/Resources/Models/Tools/CanineTowClip/
2. Drag the OBJ into the scene.
3. Create prefab:
   Assets/A_Dogs_Tale/Resources/Prefabs/Tools/PF_CanineTowClip.prefab
4. Delete or hide:
   - Optional_Target_Ring_Demo_Attachment_Point
   - Pull_Direction_Arrow_* helpers
   - Optional_Ghost_Open_Gate_Position_Reference, unless you want it as an editor-only visual
5. Suggested colliders:
   - Capsule Collider on Blue_Padded_Bite_Grip.
   - Simplified convex Mesh Collider or capsule/box compound on Main_U_Shaped_Hook_Body_Rounded_Metal.
   - Trigger collider in the hook throat for detecting AnchorPoint/LeashAttachPoint.
6. Suggested animation:
   - Rotate Orange_Pivoting_Self_Locking_Gate_Curved_Closed around Gate_Hinge_Pin_Steel_Core.
   - Use the ghost gate part as a rough guide for the open pose.
   - Snap closed after overlap + placement success.

Design note:
This model is still intentionally chunky and readable for gameplay. The latch is visually more detailed, but the actual final connection can be assisted by game logic: snap radius, alignment correction, and a locked state once the anchor point enters the hook throat.
