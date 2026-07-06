# Third Monke

A BepInEx plugin for **Gorilla Tag** that adds a client-side Third-Person view camera. 

It offsets your VR headset rendering viewpoint backward and upward relative to your headset orientation, allowing you to play the game while viewing your own monkey from behind.

## Controls

- **Toggle View (First/Third Person)**:
  - **Keyboard**: Press the **`T`** key.
  - **VR Controller**: Press the **Primary Button on the Left Controller** (Button **`X`** on Oculus controllers).
- **Adjust Distance**:
  - **Keyboard**: Use **Up Arrow** (closer) and **Down Arrow** (further).
  - **VR Controller**: Push the **Right Controller Thumbstick** forward/backward (up/down).
- **Desktop GUI**: Click on the UI window at the bottom-left of the game mirror screen on your PC to toggle, adjust sliders, or join the Discord server.

## Features

- **Smooth Orbiting**: The camera orbits around your character automatically as you turn your head.
- **Shoulder Offset**: Position the camera to the left or right side "over the shoulder" using the GUI slider.
- **Head & Body Visibility**: Forces the local player's head mesh, skinned body meshes, and cosmetics to render in third person.
- **Anti-Clip Wall Collision**: Smooth Minecraft-style raycast camera positioning that prevents the camera from clipping inside walls or your own head.
- **Safe & Client-Side**: Only alters your local headset rendering viewpoint. Your physical hands, colliders, and network synchronization remain exactly the same. Safe from anti-cheat bans.

## Installation

1. Make sure you have **BepInEx** installed in your Gorilla Tag folder.
2. Download the latest release of `ThirdMonke.dll`.
3. Copy `ThirdMonke.dll` into your `<Gorilla Tag>/BepInEx/plugins/` folder.
4. Launch the game and press **`T`** or the Left Controller Primary Button to toggle the view!

---

## Disclaimers
* This mod is not affiliated with, sponsored by, or associated with Another Axiom or Gorilla Tag.
