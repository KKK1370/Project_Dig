# Born to Dig Visual Standard v1

## Intent

BTD Visual Look v1 keeps the low-poly silhouettes readable while adding stronger form,
warmer key light, cooler atmospheric distance, grounded contact shadows, and restrained
post processing. It is a baseline, not a rule that every area must be orange.

## Shared baseline

- Use ACES tonemapping for consistent highlight rolloff.
- Keep Bloom subtle. The outdoor v1 profile uses intensity `0.18`, threshold `1.1`,
  and scatter `0.55`; Bloom should support emissive accents, not soften the whole image.
- Start area contrast near `+14`. Preserve readable shadow detail and avoid crushed blacks.
- Use restrained White Balance and Color Filter adjustments. Warm the key light while
  keeping sky, fog, and distant silhouettes slightly cooler.
- Use a light vignette only (`0.10` in the outdoor profile). UI and gameplay targets must
  remain clearly readable at the screen edge.
- PC URP uses its existing Screen Space Ambient Occlusion renderer feature. v1 keeps the
  shared renderer setting unchanged (`Intensity 0.4`, `Radius 0.3`) to avoid changing every
  Scene during the Map01 pilot.

## Outdoor rule

- Warm directional key light, cool sky/fog, and neutral-green terrain form the main palette.
- Prefer stronger shadow strength over globally dark albedo.
- Use linear fog to separate foreground, middle ground, and background without hiding landmarks.
- Keep ambient sky brighter than ambient ground, but lower ambient fill enough for low-poly
  facets and contact shadows to remain visible.
- Outdoor baseline asset: `Profiles/BTD_OutdoorWarm_v1.asset`.
- Outdoor sky asset: `Lighting/BTD_OutdoorWarmSky_v1.mat`.

## Interior rule

- Keep ACES, contact AO, contrast range, and restrained Bloom consistent with the outdoor look.
- Use warm practical lights as focal points and cooler/darker ambient fill for separation.
- Do not reuse the outdoor fog distances. Prefer local Volumes and room-specific exposure.
- Reserve strong emission for windows, lamps, ore, objectives, and other intentional accents.

## Cave and dark-area rule

- Preserve readable blacks; do not create darkness only by lowering exposure.
- Use cool low-level ambient fill plus warm lamps, ore, or machinery for focal contrast.
- Increase fog density or shorten fog distance locally, while keeping the player route readable.
- Bloom may rise slightly for emissive ore, but threshold should stay high enough to avoid haze.

## Material rule

- Terrain, rock, wood, metal, furniture, and thick geometry: URP/Lit or Simple Lit, back-face culling on.
- Leaves, cloth, paper, and genuinely thin cards: evaluate DoubleSided case by case.
- Never batch-convert all purchased materials to DoubleSided.
- Never overwrite vendor materials just to tune one Scene. Prefer a BTD-owned material variant
  under `VisualLook/Materials` and assign it only where required.
- Pink objects must be diagnosed by missing/unsupported shader first. Do not treat DoubleSided as
  a pink-material fix.

## Area rollout

1. Duplicate the closest shared Volume Profile only when an area needs materially different tuning.
2. Keep ACES, restrained Bloom, contact AO, and the foreground/background separation principle.
3. Change key-light temperature, ambient palette, and fog for the area identity.
4. Apply to one validation Scene and capture before/after views.
5. Check Console, missing references, pink materials, one Camera/AudioListener, and Play Mode.
6. Expand to additional Scenes only after the pilot look is approved.

## Map01 Area01 v1 values

- Sun: intensity `1.05`, temperature `5200 K`, shadow strength `0.84`.
- Ambient intensity `0.82`; sky remains cool, ground ambient is darker and neutral.
- Linear fog: start `42`, end `120`, cool blue-green fog color.
- Volume: ACES, Bloom `0.18`, contrast `+14`, saturation `-4`, temperature `+6`, vignette `0.10`.
- Existing PC renderer SSAO remains enabled; no ProjectSettings or global URP asset changes are part of v1.
