# Hush Blocks

Hush Blocks is a Unity 6 URP greybox prototype based on the attached Hush Blocks GDD. The first milestone establishes the Midnight Voxel Nocturne direction and the core Phase 1 interaction: click or tap a cyan noisy block to apply Noa's Velvet Seal and settle it into warm amber stillness.

## Open the project

Open `D:\Game Project\Hush Block` in Unity Hub with Unity `6000.4.7f1`. Let package resolution and asset import finish before entering Play mode.

To build the Phase 1 scene, open the Unity Editor menu and choose `Hush Blocks > Build Phase 1 Greybox`. The command is idempotent for the generated scene path: it recreates `Assets/_Project/Scenes/MainGameplay.unity` and adds it to the build settings if absent. It does not alter the original template scene except for the build-settings entry.

Open `Assets/_Project/Scenes/MainGameplay.unity` and press Play. Click a cyan block, tap it on a touchscreen, or press Space after selecting a block. The HUD reports the Velvet Seal result. The current prototype uses generated cubes and materials; it does not yet include the rhythm clock, physical tower collapse, audio, enemies, or progression systems. External asset research is recorded in `ASSET_REGISTER.md`; the project uses no third-party assets yet, and no paid assets may be purchased without explicit approval.

## Controls

Editor: left mouse click selects and stabilizes a block; Space reapplies the seal to the selected block. Mobile: touch a block to stabilize it.

## Current milestone

Phase 0 is complete: Unity version, package setup, project structure, and GDD requirements were inspected and documented. Phase 1 greybox assets and scripts are present under `Assets/_Project/`. See `DEVELOPMENT_LOG.md` for the evidence and remaining limitations.
