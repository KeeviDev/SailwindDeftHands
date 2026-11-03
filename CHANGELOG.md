# Changelog

### Added

- Controls hints for the mod's features
- "Item Rotation" config section:
  - Keep turning level — big items turn around the vertical, or the deck's vertical on a boat, so they stay level
  - Keep rolling level — big items roll around a level axis, even when you look up or down
  - Keep tilting level — on a boat, big items tilt around an axis level with the deck
  - Lock tilt — up/down mouse movement doesn't tilt big items while you hold only the modifier key or also the axis swap key
- "Item Carrying" config section:
  - Horizontal stabilization — big items you carry keep facing the same way when you look up or down, in the world or on the deck of your boat
  - Heading stabilization — big items you carry keep facing the same way when you turn around, in the world or on the deck of your boat

### Changed

- **Reworked item holding system:** by default only horizontal stabilization is on, which keeps the held item facing the same way when you look up or down, in the world or relative to the deck on a boat ("Horizontal stabilization"); keeping its facing when you turn around stays off by default ("Heading stabilization")
- **New axis orientation for more predictable item rotation:** items now turn, roll and tilt around level axes, following the deck on a boat, instead of depending on your view; turn off the new "Keep turning level", "Keep rolling level" and "Keep tilting level" options for the old behaviour
- Configuration file is reorganized: the main options now sit in the "General Features" and "Wall Attachment Features" sections; options that moved start from their defaults, so if you changed them, set them again
- "Mouse rotation control" now affects only items in your hand: items you hang on walls turn with their own options
- "Legacy rotation control" turned off now blocks the game's own rotation only where mouse rotation is enabled and available, so with "Mouse rotation control" off the game's rotation stays under your control
- "Push/pull on scroll wheel" is now "Preferred push/pull control" with "Off", "Scroll" and "Scroll With Modifier Key Only" options; "Scroll" also needs the modifier key while mouse rotation is off and legacy rotation is on, so plain scroll is available for vanilla rotation
- "Alternative rotation axis" is now called "Switch turn/roll axes" and affects only big items, so hooks, maps and other small wall-attachable items roll with left/right mouse movement however it is set
- Turning big items on the alternative axis now goes the other way by default
- Mouse rotation now starts a moment after you press the modifier key, so the item no longer turns the wrong way when you press the axis swap key just after it
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
