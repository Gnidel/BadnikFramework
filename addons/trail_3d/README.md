# Trail3D — simple Godot 4 trail addon

A drop-in ribbon-trail node for Godot 4, for effects like a hoverboard,
sword swing, spaceship exhaust, etc.

## Install

1. Copy the `addons/trail_3d` folder into your project's `addons/` folder.
2. In Godot: **Project → Project Settings → Plugins**, enable **Trail3D**.
3. Add a **Trail3D** node anywhere in the scene tree (search for it like any
   other node type). Parent it to whatever should leave a trail — e.g. as a
   child of your hoverboard's mesh, offset to sit under the board.

That's it — by default it will immediately start leaving a plain white
fading ribbon behind it as it moves. Everything else below is optional
styling.

## Configuring the look

All properties are in the Inspector, no code required:

- **Emitting** — turn recording on/off. Existing trail still fades out when off.
- **Lifetime** — how many seconds a point survives before disappearing.
- **Min Vertex Distance** — how far the node must move before a new segment
  is added. Lower = smoother trail, more geometry.
- **Max Points** — hard cap on trail length regardless of lifetime.

**Shape**
- **Width Curve** — a `Curve` resource mapping age (0 = newest/head,
  1 = oldest/tail) to a width multiplier. E.g. start at 1.0 and taper to 0
  for a trail that tapers to a point at the tail.
- **Base Width** — width in meters, multiplied by the curve above.
- **Use Local Up / Up Vector** — which "up" direction is used to build the
  ribbon's width direction. World up by default; switch to local up if you
  want the ribbon to bank with a tilting parent.

**Look**
- **Texture** — stretched along the trail's length (tiled by Texture Tiling).
- **Color Gradient** — a `Gradient` resource sampled by age; drives the
  ribbon's color (and its own alpha channel) from head to tail.
- **Alpha Curve** — an extra alpha multiplier by age, for fine control over
  fade-out independent of the color gradient.
- **Unshaded** — usually you want this on for glowing/emissive-looking trails.
- **Double Sided** — disables backface culling so the ribbon is visible from
  both sides (recommended, on by default).

## Scripting

```gdscript
$Trail3D.emitting = false   # stop recording new points
$Trail3D.clear_trail()      # instantly clear the trail (e.g. after teleporting)
```

## Notes

- The node works with `MeshInstance3D` under the hood and rebuilds a small
  `ArrayMesh` ribbon every frame from its own recent-position history — no
  extra nodes, particles, or setup required.
- Trail points are recorded in world space and converted back to the current
  local transform each frame, so you can freely parent the node to a moving
  object without double-transforming the ribbon.
- For a Sonic-Riders-style hoverboard trail: use two `Trail3D` nodes (one
  per side of the board), a width curve that stays fairly flat then tapers
  at the very end, and a bright color gradient (e.g. cyan → transparent) with
  `Unshaded` on.
