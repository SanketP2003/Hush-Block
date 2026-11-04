# Hush Blocks Development Log

## Current phase

Phase 6 — Character and player feedback implementation prepared; Phase 5 runtime verification and Phase 6 Unity integration verification pending for this session.

## Phase 2 completed

Created `Assets/_Project/Scripts/Gameplay/RhythmManager.cs`, `Assets/_Project/ScriptableObjects/DifficultyProfile.cs`, and `Assets/_Project/Scripts/Gameplay/RhythmGameplayIntegration.cs`. `GreyboxGameController` now evaluates each input through the rhythm clock instead of applying the seal immediately, and the builder wires the three components together and creates `Assets/_Project/Settings/DefaultDifficultyProfile.asset`.

The clock is deterministic: it advances by `Time.deltaTime`, fires `OnBeat` per beat, and only reports a missed beat after the accurate plus late timing window has fully closed, so a beat cannot be marked missed in the same frame a player inputs accurately. Tempo changes take effect immediately for subsequent beats. The HUD shows BPM, next-beat countdown, silence chain, and remaining noisy blocks.

## Phase 1 — Playable greybox prototype (implementation prepared; Unity Editor execution pending).

## Completed

Phase 0 environment inspection and initialization were completed. The project is an existing Unity `6000.4.7f1` URP project with the Input System package available, one original sample scene, and no project-authored gameplay systems. The GDD was inspected from the attached one-page PDF and is treated as authoritative for the core loop and visual direction.

Phase 1 implementation files were added under `Assets/_Project/`: `Scripts/Gameplay/ResonanceBlock.cs`, `Scripts/Gameplay/GreyboxGameController.cs`, and `Editor/HushBlocksPhase1Builder.cs`. The builder creates `Assets/_Project/Scenes/MainGameplay.unity` with an orthographic side-view camera, midnight background, directional lighting, ground platform, cyan noisy blocks, amber stabilized block, and a HUD/controller. The controller supports mouse, keyboard Space, and Unity Input System touchscreen taps.

`README.md` was added with project setup, controls, and milestone notes. `CLAUDE.md` documents the repository architecture and development commands.

## Verification

Static inspection completed: project version and package manifest were read; the GDD PDF has an extractable text layer; the GDD confirms mobile, voxel art, side-scroll perspective, timed taps, cyan-to-amber state change, physics tower consequences, and the Midnight Voxel Nocturne palette. Package manifest and lockfile JSON validation passed before this milestone.

The Unity Editor was not directly available for this session, so the builder menu command, scene import, C# compilation, Play Mode, Console output, and Android build were not executed here. Unity must run the builder once to materialize the generated scene and `.meta` files.

## Known issues and limitations

The Phase 1 prototype is intentionally a greybox. Rhythm timing, deterministic beat clock, block sound rings, Rigidbody stacking/collapse, silence chains, level objectives, Noa visuals, enemies, audio, settings, persistence, and Android build configuration remain for later phases. The current controller applies the seal on selection rather than evaluating a beat window; this is deliberate Phase 1 scaffolding and must be replaced by the Phase 2 rhythm system.

`Assets/Editor/HubForceResolve.cs` remains existing bootstrap residue and may self-delete after Unity package resolution. `omniroute` was not installed on the available PATH.

## Asset research milestone

A free-first asset sourcing pass was completed and recorded in `ASSET_REGISTER.md`. Candidate official sources include Kenney, Quaternius, Poly Haven, Mixamo, the Unity Asset Store free catalog, and Meshy. No paid asset was purchased and no external asset was imported. The current greybox remains project-authored so resonance-block dimensions, pivots, colliders, and cyan/amber transitions remain reliable.

Official web pages and the Unity Editor/Asset Store were not accessible from this environment, so exact pack-level license, price, download, import, URP conversion, rig, and performance verification remain manual blockers. The register explicitly avoids treating search-result summaries as sufficient licensing evidence.

## Phase 4 — Stacking and collapse milestone

Added `Assets/_Project/Scripts/Gameplay/TowerStabilitySystem.cs`. The system discovers resonance blocks, keeps a deterministic support graph using the existing 1.35-unit grid, treats stabilized blocks and the ground row as valid supports, and releases unsupported stabilized blocks with a short controlled delay. Missed beats now target the lowest eligible noisy block and request destabilization. Rigidbody settings are controlled centrally to keep stable blocks kinematic and fallen blocks gravity-driven with frozen rotation, interpolation, drag, and continuous speculative collision detection.

Updated `ResonanceBlock.cs` with controlled Rigidbody transitions, deterministic fall start data, delayed release, grid recovery, and per-frame material transition refreshes. Updated the existing builder to add `TowerStabilitySystem` and create a supported amber upper block for the generated test layout. No external assets were imported and existing project-authored dimensions/material distinctions were preserved.

Verification status: source inspection and code-level stabilization completed. Fixed support semantics so only stabilized blocks or the ground row count as supports; fixed repeated delayed-release scheduling with a per-block guard; reset the guard during block refresh; removed the unused System import and unused delay token parameter. Unity compilation, builder execution, Play Mode stacking/collapse behavior, and automated Unity tests were not available in this session. A shell probe for Unity/.NET tooling timed out and did not produce a usable compiler result, so no tests are claimed as passed. Remaining risks to check in Unity include component initialization order, Rigidbody state during scene load, support detection tolerance, and whether the generated ground-row Y coordinate matches the configured grid origin.

## Phase 5 — Core gameplay loop and level objectives

Added `Assets/_Project/Scripts/Gameplay/LevelGameplayLoop.cs` and wired it into the existing generated controller from `HushBlocksPhase1Builder.cs`. The first level tracks the generated resonance-block set and completes when all tracked blocks reach the amber `Stabilized` state. The loop displays a clear objective, stabilized progress, silence-chain counter, success feedback, failure feedback when a tracked block falls, and a Retry button/`R` shortcut that reloads the active scene. Accurate rhythm events increment the chain; missed-beat events reset it. Existing rhythm, block, and tower systems remain the owners of timing, state transitions, and physics.

Event subscriptions are guarded and removed on disable. Objective block subscriptions are cleared before refresh to avoid duplicates. The implementation uses the existing block lifecycle events and does not rewrite the verified Phase 4 physics system or import assets.

Verification status: implementation and static inspection completed in this session. Unity compilation, generated-scene rebuild, Play Mode success/failure/retry tests, and automated tests were not run here. Do not treat Phase 5 as runtime-verified until Unity `6000.4.7f1` confirms the Console is clean and the manual scenarios pass.

## Phase 6 — Character and player feedback

Added `Assets/_Project/Scripts/Gameplay/NoaFeedbackCharacter.cs`, a project-authored lightweight voxel-style placeholder with fixed side-view placement, idle bobbing, and a short amber pulse when the existing controller emits `OnVelvetSealApplied`. Added that event to `GreyboxGameController` without changing rhythm timing, block dimensions, physics, or objective ownership. Updated the existing builder to create a collider-free Noa placeholder and connect it to the controller. The character feedback layer is independent of `ResonanceBlock` state logic and can later host Harmonic Hush feedback.

Verification status: Phase 5 remains not runtime-verified. The log records source review only; Unity compilation, scene rebuild, Play Mode tests, and automated tests have not been run in this session. During reinspection, a null-safe guard was added to the legacy controller's difficulty-profile chain-decay access, and objective stabilization counting was changed to recount actual block states instead of incrementing blindly. Phase 6 source integration is also not Unity-verified. No external assets or dependencies were added.

## Next steps

Open Unity `6000.4.7f1`, compile and inspect the Console, run `Hush Blocks > Build Phase 1 Greybox`, and verify Phase 5 objective initialization, chain increment/reset, all-four-block completion, tower failure, Retry button, `R` restart, and event cleanup. Then verify Noa appears in the fixed side-view plane, idles without affecting colliders or physics, pulses on an accurate Velvet Seal, and is recreated correctly after retry. Record actual results before beginning the next milestone.
