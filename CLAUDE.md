# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working in this repository.

## Project overview

Hush Blocks is a small Unity project created from Unity's Universal Render Pipeline (URP) blank template. It targets Unity `6000.4.7f1` and now has the original sample scene plus the generated Phase 1 scene `Assets/_Project/Scenes/MainGameplay.unity`. The authored gameplay layer is an early greybox: `ResonanceBlock` handles noisy/stabilized visual state, `GreyboxGameController` handles mouse, keyboard, and touchscreen interaction, and `HushBlocksPhase1Builder` creates the greybox scene from the Unity Editor menu. The tutorial Readme and temporary package-resolution helper remain in the repository; there is not yet a rhythm, physics, or test systems layer.

The project uses Unity's package manager with the Input System, URP 17.4, AI Navigation, Timeline, UI, Visual Scripting, and Unity AI packages. `Packages/manifest.json` is the source-level dependency declaration and `Packages/packages-lock.json` records the resolved package graph. The generated `.csproj`/`.slnx` files are editor outputs and are ignored by Git.

## Repository structure

- `Assets/` contains source assets. `Assets/Scenes/SampleScene.unity` is the current entry scene; `Assets/Settings/` contains URP assets/renderers and volume profiles; `Assets/InputSystem_Actions.inputactions` is the project input-actions asset.
- `ProjectSettings/` contains Unity editor/player/build configuration. `EditorBuildSettings.asset` is the authoritative list of scenes included in builds and currently includes only `SampleScene`.
- `Packages/` contains package declarations and the lockfile.
- `.vscode/` contains Unity debugging/editor settings. `launch.json` is configured for Visual Studio Tools for Unity attach debugging.
- `Library/`, `Temp/`, `Logs/`, `UserSettings/`, generated solution/project files, and build outputs are local/generated artifacts and are ignored.

Unity assets are YAML-serialized and normally have paired `.meta` files. Preserve GUIDs and do not hand-edit serialized assets unless the change is intentional and the resulting YAML remains valid.

## Common commands

There is no project-local `omniroute` command, package-manager script, lint script, or standalone build script available in this checkout. `omniroute` was checked from the project directory and is not installed on the current PATH.

Run Unity commands with the Unity 6000.4.7f1 editor executable, passing the project directory explicitly. The executable path is machine-specific; use the Unity Hub/editor installation on the host rather than adding one to the repository. Typical headless forms are:

```text
Unity.exe -batchmode -quit -projectPath "D:\Game Project\Hush Block" -logFile "D:\Game Project\Hush Block\Logs\editor.log"
Unity.exe -batchmode -quit -projectPath "D:\Game Project\Hush Block" -executeMethod BuildScript.Build -buildTarget StandaloneWindows64 -quit -logFile -
Unity.exe -batchmode -quit -projectPath "D:\Game Project\Hush Block" -runTests -testPlatform EditMode -testResults "D:\Game Project\Hush Block\TestResults\editmode.xml" -logFile -
```

The build command above is only a template: this repository does not currently contain a `BuildScript.Build` method, so a build entry point must be added before using it. Likewise, there are no project-authored tests at present. When tests are added, use Unity Test Framework test filters for a single test or fixture, for example:

```text
Unity.exe -batchmode -quit -projectPath "D:\Game Project\Hush Block" -runTests -testPlatform EditMode -testFilter "Namespace.TestFixture.TestName" -testResults "TestResults\single-test.xml" -logFile -
```

For normal development, open the project in Unity Hub using the pinned editor version, allow package/import resolution to finish, then enter Play mode from `SampleScene`. Use the VS Code `Attach to Unity` launch configuration for debugger attachment.

Validation that does not require opening Unity can check that the manifest and lockfile are valid JSON and that every tracked Unity asset has its expected `.meta` file. Do not use `Library/` package-cache files as project source when searching or editing.

## Architecture and workflow notes

The current architecture is scene-first and asset-driven rather than code-driven. `SampleScene` is the original template scene; the Phase 1/2 greybox is `Assets/_Project/Scenes/MainGameplay.unity`. Global/player configuration lives in `ProjectSettings`; render configuration is selected through the URP assets in `Assets/Settings`; input is centralized in the generated Input System asset and referenced by `EditorBuildSettings.asset`.

The gameplay layer now contains:

- `ResonanceBlock` — noisy/stabilized visual state and vibration.
- `RhythmManager` — deterministic beat clock driven by a `DifficultyProfile` ScriptableObject. Tempo and timing windows are configurable without code changes. Beats are the source of truth; visual effects do not drive timing.
- `GreyboxGameController` — interaction controller. Inputs are timestamped and evaluated through `RhythmManager.EvaluateInput`. Accurate inputs stabilize the selected block; early/late/missed inputs provide feedback but do not stabilize.
- `RhythmGameplayIntegration` — event bridge that fires `OnAccurateInput`, `OnEarlyInput`, `OnLateInput`, `OnMissedBeat` for other systems to consume.
- `HushBlocksPhase1Builder` — editor menu command that generates the greybox scene, camera, lighting, blocks, controller with rhythm components, and the default `DifficultyProfile`.

There are no assembly definition files, namespaces, gameplay managers, prefabs, or runtime services beyond the above yet. New gameplay code should be organized under clear feature folders and should introduce assembly definitions only when there is a meaningful separation to maintain. Keep editor-only automation under an `Editor` folder or editor-only assembly so it is not included in player builds.

`Assets/Editor/HubForceResolve.cs` is a one-shot editor utility. On the first idle editor pass it calls `Client.Resolve()`, waits for compilation/package updating to settle, then deletes itself and the now-empty editor folder. Treat it as migration/bootstrap residue rather than a permanent gameplay dependency; do not build new systems around it.

When adding a scene, update `ProjectSettings/EditorBuildSettings.asset` through Unity so the scene GUID/path is serialized correctly. When changing packages, edit `Packages/manifest.json` through Unity's Package Manager where possible and keep the lockfile consistent. Avoid committing `Library/` or other generated output even if Unity recreates it locally.

## Known limitations

- No repository README, Cursor rules, or Copilot instructions were found.
- No project-authored lint configuration or test assemblies were found.
- No build automation entry point exists yet.
- The project still contains template identifiers in player settings (`DefaultCompany` and the URP template application identifiers); treat those as pending product configuration rather than assuming they are final.

## Unity MCP

Claude Code is connected to the Unity Editor through the project-local `unityMCP` server, pinned to the `Hush Block@8cf5e8f2` instance (Unity 6000.4.7f1, port 6400).

Use the Unity MCP tools instead of editing serialized assets or `.unity` YAML by hand:

- Read `mcpforunity://editor/state` and `mcpforunity://project/info` before making changes.
- `manage_scene` — open and save scenes, and query the hierarchy with `get_hierarchy`.
- `manage_gameobject` and `manage_components` — create and modify objects and components in the open scene.
- `manage_script` — create or edit C# scripts; run `read_console` afterward to catch compile errors before continuing.
- `manage_editor` — play, pause, stop, undo, redo, tags, layers, and package deploy/restore.
- `manage_asset` — search, import, and configure project assets.

If more than one Unity instance is open, call `set_active_instance` with `Hush Block@8cf5e8f2` before other tools.

Keep `Library/`, `Temp/`, and generated project files out of edits. Make scene and prefab changes intentionally through Unity APIs so GUIDs and `.meta` files stay consistent.
