# Changelog

### Added

- "Lock tilt" option: up/down mouse movement doesn't tilt big items while you hold only the modifier key or also the axis swap key

### Changed

- The axis swap key and the "Alternative rotation axis" option now affect only big items; hooks, maps and other small wall-attachable items always roll with left/right mouse movement
- Turning big items on the alternative axis now goes the other way by default
- "Rotation activation key" is now called "Rotation modifier key"; if you changed it, set it again

### Fixed

- Holding the modifier key with a compass, spyglass, fishing rod or another item that uses the scroll wheel no longer freezes the camera
- Mouse rotation and push/pull no longer stop working on an item that another mod keeps in your hand when you try to drop it, such as a braced Tent and Plank pole

## [1.2.1] - 2026-10-04

### Fixed

- Big items pushed with the scroll wheel are no longer held back by the game's easy cargo placement in tight spots; scrolling hard now overpowers it, while gentle scrolling still lets it push the item away
- Big items pushed against a wall no longer get dragged into or through it when you look around

## [1.2.0] - 2026-09-30

### Added

- Wall attachment rotation: tilt hooks, maps, shelves and other wall-attachable items with the mouse to hang them at any angle on walls, ceilings and floors
  - Can be turned off with the new "Wall attachment rotation" config option

### Fixed

- Items attached to ceilings and floors are no longer turned to a fixed or skewed direction; they now face the way they look in your hand (vanilla bug)

## [1.1.0] - 2026-09-26

### Added

- "Axis Inversion" config section:
  - Invert vertical — reverses which way up/down mouse movement tilts the held item
  - Invert roll — reverses which way left/right mouse movement rolls items
  - Invert turn — reverses which way left/right mouse movement turns items around the vertical axis

### Changed

- Turning on the alternative axis now goes the other way by default

## [1.0.0] - 2026-09-25

### Initial release

- Mouse rotation control
- Push/pull on scroll wheel
- Ability to disable legacy rotation control
- Weight simulation with presets

[1.2.1]: https://github.com/KeeviDev/SailwindDeftHands/releases/tag/v1.2.1
[1.2.0]: https://github.com/KeeviDev/SailwindDeftHands/releases/tag/v1.2.0
[1.1.0]: https://github.com/KeeviDev/SailwindDeftHands/releases/tag/v1.1.0
[1.0.0]: https://github.com/KeeviDev/SailwindDeftHands/releases/tag/v1.0.0
