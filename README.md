# Third Monke

A BepInEx plugin for **Gorilla Tag** that adds a client-side Third-Person view camera. 

It offsets your VR headset rendering viewpoint backward and upward, allowing you to play the game while viewing your own monkey from behind.

## Controls

All camera settings, toggles, and adjustments are configured purely through the **Desktop GUI** overlay:
* Look at your PC monitor / game mirror window.
* Click on the UI window labeled **"THIRD MONKE CONFIG"** at the bottom-left corner of the screen.
* Check the toggle to enable/disable the camera, select options, adjust sliders, or click to join the Discord server.

## Features

- **Smooth Orbiting**: The camera orbits around your character automatically as you turn your head.
- **Shoulder Offset**: Position the camera to the left or right side "over the shoulder" using the GUI slider.
- **Head & Body Visibility**: Forces the local player's head mesh, skinned body meshes, and cosmetics to render in third person.
- **Anti-Clip Wall Collision**: Smooth Minecraft-style raycast camera positioning that prevents the camera from clipping inside walls or your own head.
- **Hand Indicator Dots**: Draw small helper dots on your hands that are visible through walls to make third-person jumping easier.
- **Safe & Client-Side**: Only alters your local headset rendering viewpoint. Your physical hands, colliders, and network synchronization remain exactly the same. Safe from anti-cheat bans.

## Installation

1. Make sure you have **BepInEx** installed in your Gorilla Tag folder.
2. Download the latest release of `ThirdMonke.dll`.
3. Copy `ThirdMonke.dll` into your `<Gorilla Tag>/BepInEx/plugins/` folder.
4. Launch the game, use the Desktop GUI to enable the third-person view, and play!

---

> [!NOTE]
> This mod has only been tested on Linux (via Steam / Proton). Contributions, edits, and PRs are welcome!

---

## Disclaimers
* This mod is not affiliated with, sponsored by, or associated with Another Axiom or Gorilla Tag.
