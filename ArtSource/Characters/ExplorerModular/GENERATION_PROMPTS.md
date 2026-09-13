# Explorer modular character generation prompts

The front torso (`TorsoDown.png`) is the user-provided reference. Every generated image used it as the
character/style reference. Source sheets intentionally use `#FF00FF`; Unity's setup command removes the
background, crops each part, and produces the transparent runtime PNGs.

## Limb sheets (revised overlap-ready specification)

Use one image per direction, 1536×1024, exact 3×2 grid. Replace `{VIEW}` and `{BOOT}` for each pass:

- Down: `front-facing / front view`, `boot seen from the front`
- Left: `left-facing / strict left profile`, `boot toe points left`
- Right: `right-facing / strict right profile`, `boot toe points right`
- Up: `back-facing / strict rear view`, `boot heel seen from behind`

```text
Create the {VIEW} companion sheet for this exact modular 2D astronaut game character. Production sprite
asset, identical clean ceramic white, titanium gray, graphite black, subtle cyan emissive accents, almost
no yellow or orange. Match the supplied torso reference in proportions, rendering, outline weight,
lighting, materials and detail density. Exactly six isolated components: top row from left to right one
upper arm, one forearm, one separate closed gripping glove/hand; bottom row from left to right one thigh,
one lower leg/shin, one separate {BOOT}. The hand forms a neutral cylindrical tool grip without a tool.
Every part is straight, neutral and centered in its equal cell with generous margin and no overlaps.
Every connection endpoint is a solid, fully opaque, narrow graphite flexible-fabric tab that extends
12–18% beyond the armor shell and tucks beneath the neighboring sprite. Never show a hollow opening,
tube mouth, inner rim, socket, circular hole, exposed cut surface, or detached connector ring. Arm parts
are visibly slimmer than leg parts. Upper-arm length is about 80% of forearm length; thigh length is about
110% of lower-leg length; hand height is about 42% of forearm height; boot height is about 48% of
lower-leg height and no wider than 125% of the lower-leg armor. Keep all six parts in one coherent scale.
Exclude torso, helmet, backpack, shoulder pauldron, hip socket, weapon, pickaxe, labels, guides, shadows,
floor, borders, duplicates, assembled limbs and extra objects. Canvas 1536x1024, exact 3 columns by 2 rows.
Entire background is perfectly flat uniform chroma-key magenta RGB #FF00FF, without texture, noise,
gradient, vignette, checkerboard, lighting or cast shadow.
```

## Torso directions

Generate Left, Right and Up separately on a 1024×1536 portrait canvas. Replace `{VIEW}` and `{VISIBLE}`:

- Left: `strict orthographic left profile`, `helmet and dark blue visor in left profile`
- Right: `strict orthographic right profile`, `helmet and dark blue visor in right profile`
- Up: `strict orthographic rear view`, `back of helmet; no visor or front face`

```text
Create the {VIEW} torso module for the exact same clean sci-fi astronaut in the supplied front torso and
directional limb references. Production 2D game sprite. Show one isolated torso assembly only: {VISIBLE},
neck ring, chest/abdomen or rear suit shell, belt/hip shell, compact integrated life-support backpack,
shoulder socket armor and hip socket armor integrated into the torso. No upper arms, forearms, hands,
thighs, lower legs, feet, tools, weapons, floor, shadow, text, labels, border or extra objects. Preserve
the reference proportions, clean ceramic white panels, titanium gray and graphite joints, restrained cyan
emissive details, almost no yellow or orange. Keep connections readable for overlapping modular limbs.
Center the torso with generous empty margin. Portrait canvas 1024x1536. Entire background is perfectly
flat uniform chroma-key magenta RGB #FF00FF, without texture, noise, gradient, vignette or cast shadow.
```
