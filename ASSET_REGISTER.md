# Hush Blocks Asset Register

Production Register for Hush Blocks (Unity 6000.4.7f1, URP 17.4, Standalone Windows x64 & Android target).

Art Direction: **Midnight Voxel Nocturne**  
Visual Palette: Midnight charcoal, cyan, amber, dark blue, warm architectural lighting.

---

## Authored 3D Voxel Models & Prefabs

All 3D assets in Hush Blocks have been custom-authored to adhere strictly to the GDD's architectural and character specifications, low-poly mobile vertex budgets, and deterministic collision geometry.

| Model Asset Path | Unity Prefab Path | Poly / Vertex Budget | Texture / Materials | Intended Role & Systems Integration | License / Status |
|---|---|---|---|---|---|
| `Assets/Art/Models/Environment/BedrockFoundation.fbx` | `Assets/Prefabs/Environment/BedrockFoundation.prefab` | ~1.2k tris | URP Lit (`HushPalette.Amber` glyph glow, midnight slate base) | Foundation base of the tower. Displays glowing ancient amber acoustic glyphs. Instantiated at `(0, 0, 0)` in `HushStage`. | Project-Authored / Verified |
| `Assets/Art/Models/Environment/BrownstoneDiorama.fbx` | `Assets/Prefabs/Environment/BrownstoneDiorama.prefab` | ~3.8k tris | URP Lit (Charcoal brick, glowing warm amber interior windows) | Midnight cityscape backdrop. Positioned behind the rising tower with subtle rhythmic parallax. | Project-Authored / Verified |
| `Assets/Art/Models/Characters/Noa.fbx` | `Assets/Prefabs/Characters/Noa.prefab` | ~2.1k tris | URP Lit with Amber crystal trowel emission | Player avatar Noa. Step-hops upward (`NoaController.StepUpFloor`) as blocks are placed; swings resonant amber trowel on tap. | Project-Authored / Verified |
| `Assets/Art/Models/Characters/FeedbackHowler.fbx` | `Assets/Prefabs/Characters/FeedbackHowler.prefab` | ~1.8k tris | URP Lit (Dark charcoal voxels with pulsed cyan acoustic distortion) | Hazard creature in District 02 and 03. Climbs tower ledges and shrieks on syncopated off-beats. | Project-Authored / Verified |
| `Assets/Art/Models/Environment/RooftopSanctuary.fbx` | `Assets/Prefabs/Environment/RooftopSanctuary.prefab` | ~2.4k tris | URP Lit (Warm stone planters, blooming nocturnal flora, spire) | District completion sanctuary. Spawns atop the completed tower upon reaching target height. | Project-Authored / Verified |
| `Assets/Art/Models/DoubleBeatBlock/DoubleBeatBlock.fbx` | `Assets/Prefabs/Blocks/HushBlock_DoubleBeat.prefab` | ~960 tris | URP Lit (`_BaseColor`, `_EmissionColor` dual-phase materials) | Special acoustic resonance chamber block requiring 2-beat rhythmic strike to lock into amber ballast. | Project-Authored / Verified |
| `Assets/Art/Models/Props/CharcoalDebris.fbx` | `Assets/Prefabs/Props/CharcoalDebris.prefab` | ~420 tris | URP Lit (`HushPalette.Panel` charcoal) | Severed overhang fragments and shattered voxels spawned via `HushDebris`. | Project-Authored / Verified |
| `Assets/Art/Models/FX/SoundWaveRings.fbx` | Procedural / Mesh | ~640 tris | Additive URP Unlit (`SoundWaveRing`) | Expanding acoustic rings emitted on trowel strike, harmonic channeling, and double-beat lock. | Project-Authored / Verified |
| `Assets/Art/Models/Props/CrystallineEncasement.fbx` | Mesh / Visual | ~880 tris | Transparent URP Lit (`HushPalette.Cyan`) | Anti-resonance acoustic field encasing unmuted moving blocks before placement. | Project-Authored / Verified |

---

## Material & Shader Architecture

All models utilize Universal Render Pipeline (URP 17.4) shaders:
- `Universal Render Pipeline/Lit` for structural geometry, character bodies, and diorama architecture.
- `MaterialPropertyBlock` instances for zero-allocation dynamic color swapping (`_BaseColor`) and rhythmic emission pulsing (`_EmissionColor`) driven by DSP beat tracking.
- Zero paid assets, zero unverified third-party packages.
