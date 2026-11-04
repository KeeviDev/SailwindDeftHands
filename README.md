<p align="left"><img src="https://github.com/user-attachments/assets/b4d87404-7104-41db-a84b-189034016fcf" width="192" alt="Deft Hands logo"></p> 

# Deft Hands

Stop wrestling with the scroll wheel to turn that crate the right way up. **Deft Hands** lets you rotate whatever you're holding by simply moving the mouse, like you'd turn it in your actual hands.

https://github.com/user-attachments/assets/606b6498-2157-4809-b052-3694c6b663d3

## What you get

- **Rotate with the mouse.** Hold **Left Alt** and move the mouse — the item turns whichever way you move it. Click the **mouse wheel** to set it straight again.
- **Big items, full freedom.** Crates, barrels and friends spin on any axis. Hold **right mouse button** while rotating to swap left/right movement between rolling and turning. Turning keeps them level, on deck too, and they can keep facing the same way while you look around.
- **Things feel heavy.** A bundle of logs takes a moment to get going and drifts to a stop; a light tobacco crate just spins. Prefer it subtle? Pick the **Light** preset, or switch it off for crisp 1:1 control.
- **Hang things your way.** Tilt hooks, maps, shelves and other wall-attachable items with the mouse and they hang at that angle — on walls, ceilings and floors. They hang where you look, and while you aim, the item steps aside and faces you, turning only the way it will hang, so you can see the spot.
- **Scroll to push and pull.** The scroll wheel smoothly moves your held item closer or farther. You can also set it to push and pull with **Left Alt** + scroll instead, in case you want to leave plain scroll for the game's own rotation.
- **Old-school still works.** The game's original scroll rotation stays available — or turn it off if mouse rotation is all you need.

https://github.com/user-attachments/assets/24d5d3af-5c76-4b30-b9fd-e4f86cddca80

## HooksHangMore

Got **HooksHangMore**? Mouse rotation swings its hooks too — smoothly, no more notch-by-notch scrolling.

https://github.com/user-attachments/assets/a25d296d-15f6-44d5-8fb3-f6dc2bb4f8cb

## Installation

**With a mod manager:** grab Deft Hands from Thunderstore with r2modman or Thunderstore Mod Manager — BepInEx comes along automatically.

**By hand:**

1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx/releases).
2. Extract the release zip into `Sailwind/BepInEx/plugins`.
3. Set sail.

Also plays nicely with **NANDTweaks** — its rotate and push/pull keys keep working as usual.

## Settings

Tweak everything in `BepInEx/config/com.keevi.defthands.cfg`, or in-game with [Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager).

| Setting | Default | What it does |
|---|---|---|
| **General Features** | | |
| Mouse rotation control | On | Rotate the item in your hand by moving the mouse. |
| Preferred push/pull control | Scroll | Scroll moves the held item closer or farther; Scroll With Modifier Key Only does it only with the modifier key, and so does Scroll while mouse rotation is off and legacy rotation is on. |
| Legacy rotation control | On | Keeps the game's own scroll rotation alongside mouse rotation in your hand. |
| **Wall Attachment Features** | | |
| Easy wall hanging | On | Items hang where you look, and the held item steps aside so you can see the spot. |
| Wall attachment rotation | On | Roll hooks, shelves and other wall-attachable items to hang them at an angle. |
| **Item Carrying** | | |
| Horizontal stabilization | On | Big items keep facing the same way, in the world or on deck, when you look up or down. |
| Heading stabilization | Off | Big items keep facing the same way, in the world or on deck, when you turn around. |
| **Item Rotation** | | |
| Rotation sensitivity | 2 | How fast items turn (0.5–10). |
| Switch turn/roll axes | Off | Left/right turns big items around the vertical axis instead of rolling them. |
| Lock tilt | On axis swap | Up/down movement doesn't tilt big items while you hold the modifier key alone (Without axis swap) or with the axis swap key (On axis swap). |
| Keep turning level | On | Big items turn around the vertical (the deck's on a boat), so they stay level. |
| Keep rolling level | On | Big items roll around a level axis, even when you look up or down. |
| Keep tilting level | On | On a boat, big items tilt around an axis level with the deck. |
| **Keys** | | |
| Rotation modifier key | Left Alt | Hold it to rotate. |
| Axis swap key | Right mouse button | Hold while rotating to flip the rotation axis. |
| Reset orientation key | Middle mouse button | Resets the held item's orientation. |
| Reset orientation with modifier key | Off | The reset key works only while you hold the modifier key. |
| **Axis Inversion** | | |
| Invert vertical | Off | Reverses which way up/down movement tilts the item. |
| Invert roll | Off | Reverses left/right rolling of big and wall-attachable items. |
| Invert turn | Off | Reverses left/right turning of big items, with Switch turn/roll axes or the axis swap key. |
| **Weight Simulation** | | |
| Rotation inertia | On | Heavy items take a moment to spin up and drift to a stop. |
| Weight preset | Standard | How heavy things feel: Standard or the subtler Light. |
