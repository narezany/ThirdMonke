# Third Monke

A BepInEx plugin for **Gorilla Tag** that adds a client-side Third-Person view camera. 

It offsets your VR headset rendering viewpoint backward and upward relative to your headset orientation, allowing you to play the game while viewing your own monkey from behind.

## Controls

- **Keyboard**: Press the **`T`** key to toggle between first-person and third-person view.
- **VR Controller**: Press the **Primary Button on the Left Controller** (Button **`X`** on Oculus controllers) to toggle the view.

## Features

- **Smooth Orbiting**: The camera orbits around your character automatically as you turn your head.
- **Head & Body Visibility**: Forces the local player's head mesh, skinned body meshes, and cosmetics to render in third person (which are normally hidden by the game in first person).
- **Safe & Client-Side**: Only alters your local headset rendering viewpoint. Your physical hands, colliders, and network synchronization remain exactly the same. Safe from anti-cheat bans.

## Installation

1. Make sure you have **BepInEx** installed in your Gorilla Tag folder.
2. Download the latest release of `ThirdMonke.dll`.
3. Copy `ThirdMonke.dll` into your `<Gorilla Tag>/BepInEx/plugins/` folder.
4. Launch the game and press **`T`** or the Left Controller Primary Button to toggle the view!

---

## Disclaimers
* This mod is not affiliated with, sponsored by, or associated with Another Axiom or Gorilla Tag.
