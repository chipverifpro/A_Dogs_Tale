<H1>Dungeon Generators in Unity</H1>

Here are a few algorithms I've been trying out to make some nice random dungeons for a Rogue-like game.
</P>
Simplest is the square rooms with orthoganal corridors.
<br>
<img width="401" height="385" alt="Screenshot 2025-08-11 at 5 54 08 PM" src="https://github.com/user-attachments/assets/fae1c4cf-4a38-4c83-94ee-315831d5102c" />
<br>
Next, replace those square rooms with ovals that are allowed to overlap.  Connect them with straight corridors.
<br>
<img width="400" height="394" alt="Screenshot 2025-08-11 at 5 52 51 PM" src="https://github.com/user-attachments/assets/af1f3bb2-15bc-4c71-b4f2-7e78e5f27917" />
<br>
For some twisty little tunnels, try a Cellular Automata algorithm (random noise starting pattern, grown with rules like the game of Life.
<br>
<img width="402" height="383" alt="Screenshot 2025-08-11 at 5 56 58 PM" src="https://github.com/user-attachments/assets/13b2c5da-63be-4eb5-910e-ccb6649c28bf" />
<br>
Then add a low frequency Perlin Noise to the Cellular Automata algorithm.  Connect these bigger rooms with Bezier Curve corridors.
<br>
<img width="405" height="381" alt="Celular_Automata_Perlin" src="https://github.com/user-attachments/assets/f8c33f6c-9bac-44ce-9f24-91aef4b14450" />
<br>
Give the user lots of options to tweak the settings of all these algorithms and a large variety of dungeon styles can be created.
<br>
<img width="885" height="799" alt="Unity_Settings" src="https://github.com/user-attachments/assets/eae93658-3abe-461f-90d9-f9e396ba0c0e" />
<br>
Use two Perlin Noise layers added together with different XY scales to get both large rooms, and rough wall textures.  Delete tiny rooms and tiny rocks inside rooms for clean but natural looking rooms.
<br>
<img width="467" height="457" alt="Screenshot 2025-08-21 at 2 15 14 PM" src="https://github.com/user-attachments/assets/26e98846-3b38-4dbd-8c8b-4a2729c9b9be" />
<br>
Add thin 3D walls to the room perimeters.  Option for diagonal walls enabled.  Shown here with a Cellular Automata generated dungeon.
<img width="611" height="427" alt="Screenshot 2025-08-21 at 2 05 58 PM" src="https://github.com/user-attachments/assets/6d572f41-b311-4fa1-8101-63b33da1becc" />
<br>
Adding some elevation to the map.  Here, the circular rooms are at different heights, and the corridors contain ramps to connect them.
<img width="744" height="435" alt="Screenshot 2025-08-22 at 4 45 59 PM" src="https://github.com/user-attachments/assets/5ca11a8d-4a97-427e-a560-86ec9929bf6f" />
<br>
Here I added some Perlin noise to the elevation of the floors.
<img width="553" height="354" alt="Screenshot 2025-09-08 at 11 34 48 PM" src="https://github.com/user-attachments/assets/9ea1ebdb-1b75-4dcc-86df-cc291af34ddd" />
<br>
Extreme Perlin noise floors with tilted floor panels to smooth out the surface.
<img width="548" height="365" alt="Screenshot 2025-09-08 at 11 39 59 PM" src="https://github.com/user-attachments/assets/b78d95da-d687-4d84-88d9-e794d6718a43" />
<br>
Three views of a packed room algorithm containing multiple player agents...
<img width="828" height="468" alt="Screenshot 2025-10-04 at 3 41 30 AM" src="https://github.com/user-attachments/assets/84633fcf-cfc0-4398-b480-7ac4e2f9539a" />
<br>
<img width="826" height="520" alt="Screenshot 2025-10-04 at 3 39 48 AM" src="https://github.com/user-attachments/assets/0ff4446c-8ff6-42d9-96d2-66e69dd12471" />
<br>
<img width="815" height="462" alt="Screenshot 2025-10-04 at 3 40 42 AM" src="https://github.com/user-attachments/assets/eacfcefa-e4c7-4984-a1a2-899c97c8b272" />
<br>
<p>
I am transforming this into a <b>game</b>.  I'm excited to see everything come together to help tell a story.  The plot involves bravery, discovery, and a yearning to be more.  Face challenges together with friends made along the way. Discover you can be more than you ever dreamed of, and finally be able to decide which path you truly want to to follow.
