# Cutout animation system

The player presentation follows the same separation visible in Klei's published Don't Starve source
assets: a **build** supplies named replaceable image symbols, while an **animation** supplies transforms
and symbol-frame choices. The official Wilson build contains `torso`, `arm_upper`, `arm_lower`, `hand`,
`leg`, and `foot` folders, each with multiple sprite variants and individual pivots:

- https://github.com/kleientertainment/ds_assets/tree/master/builds/wilson
- https://github.com/kleientertainment/ds_assets/blob/master/builds/wilson/build.scml
- https://github.com/kleientertainment/ds_mod_tools

## Runtime ownership

- `ModularPlayerRigDefinition` is the symbol build. It selects directional artwork but contains no
  gameplay rules.
- `ModularPlayerRig` renders and transforms symbols. It does not solve limb targets with IK. The two
  leg armor images currently remain an internal composite and rotate together as one leg symbol.
- `PlayerActionAnimationDefinition` is an animation-bank entry. Besides body and equipment curves, it
  stores additive rotations for both upper arms, forearms, and hands.
- `PlayerToolAnimationDefinition` maps an item ID to one equipment symbol and one reusable action entry.
  Adding a tool therefore needs a tool sprite, grip data, and animation curves—not another full set of
  astronaut frames.

## Authoring rules

1. Draw every symbol in a neutral pose with deliberate overlap beyond its pivot. Never expose a cut face.
2. Keep the character-facing variants in one symbol family and preserve physical dimensions between views.
3. Animate the silhouette with authored keys. Do not make gameplay-time IK the default presentation.
4. Treat each leg as one animated symbol plus a separately stabilised foot. Avoid an independently bending
   knee unless the artwork was specifically drawn to support that joint.
5. Let an action change transforms and sorting. Add a sprite variant only when the silhouette truly changes,
   such as an open hand versus a gripping hand.
6. Keep tool visuals independent from character symbols so pickaxes, axes, repair tools, and weapons can
   reuse the same astronaut build.
