# Hush Blocks — Senior Game Development & UI/UX Audit

**Project:** `D:\Game Project\Hush Block`  
**Engine:** Unity `6000.4.7f1`, URP `17.4.0`, Input System `1.19.0`  
**Scope:** `Assets/_Project/Scripts/FullGame`, project/build settings, generated UI, scene/build wiring  
**Method:** static source, scene, prefab, package, and settings inspection; deterministic UI-rect and score calculations. No play-mode session or Windows build was possible because the FullGame layer is not reachable from any scene.

---

## 0. Verdict

The FullGame layer is a promising, compile-clean vertical slice with a distinctive direction. It is **not currently a playable game**:

- The build still launches the legacy `MainGameplay` greybox.
- No scene or prefab references the FullGame scripts.
- The FullGame UI is constructed but never shown.
- The campaign flow has six P0 defects that break the loop even after a scene is added.
- The UI has one systemic layout bug plus several verified collisions.
- Audio is never initialized, so the game is silent.

Do **not** start with art or audio polish. First make the campaign loop launchable, then fix the flow, then repair the UI system, then tune gameplay.

---

## 1. What already works

These are worth preserving:

- `StackGame` is a clean separation of run simulation from presentation. Timing, width, lives, combo, and scoring live in one place.
- The DSP-based beat clock and pause accounting are the right foundation for rhythm (`StackGame.cs:137–146`, `:232–245`).
- `DistrictCatalog` is a single tuning source for BPM, floors, score thresholds, sway, palette, and tips.
- The procedural UI, sky, material, music, and SFX approach keeps the project self-contained.
- The visual identity is coherent: midnight/cyan/amber, condensed caps, strong HUD ring, rooftop diorama.
- `HushSaveData` already has versioning, best scores, stars, unlocks, volumes, and accessibility flags.
- The Double-Beat Block pipeline still exists and is intact; it is simply not wired into the new layer yet.

The problem is not the concept. It is the last mile: bootstrap, flow, layout, and build.

---

## 2. P0 blockers

### P0-1. The FullGame layer has no launch path

**Symptoms**

- Build settings contain only `Assets/_Project/Scenes/MainGameplay.unity`; `SampleScene` is disabled (`ProjectSettings/EditorBuildSettings.asset:7–13`).
- A GUID search across every `.unity` and `.prefab` found **zero** references to any `Assets/_Project/Scripts/FullGame/*.cs` script.
- `HushGameLoopManager` is never instantiated. Its `Awake` exists, but nothing adds it to a scene or creates it (`HushGameLoopManager.cs:18`, `:56`, `:72–80`). There is no `RuntimeInitializeOnLoadMethod` and no `AddComponent<HushGameLoopManager>`.
- `Game` is a static facade that delegates to `HushGameLoopManager.Instance?` (`Game.cs:13–27`). With no instance, every title button is a no-op.
- `HushGame` builds all screens in `Awake` (`HushGame.cs:35–46`, `:48–74`) but has no `Start` method and never shows `Boot`, `Title`, or any other screen. `HushUI.SetFade(0f)` is called once at build time (`:73`).
- `BootScreen.SetProgress` is never called; `HushAudio.Preload` is never called; nothing transitions from boot to title.
- `HushGameLoopManager` requires a `HushStage`; if you add the manager alone, `stage` is null, `HandleEnterActiveStacking` skips `StackGame.Begin`, and the HUD opens without gameplay (`HushGameLoopManager.cs:83`, `:360–378`).

**Impact:** The only thing a player can launch is the legacy prototype. The new game is unreachable code.

**Fix**

1. Add a clean scene `Assets/_Project/Scenes/HushBlocksFullGame.unity` with a single `GameBootstrap` object.
2. `GameBootstrap` creates, once and in order:
   - `HushGame` / UI root,
   - `HushAudio` (create + preload through the boot progress screen),
   - `HushStage` (build once),
   - one run/session owner (today `HushGameLoopManager`, preferably a renamed `HushSession`).
3. Show `BootScreen`, preload audio/art with progress, then fade to `TitleScreen`.
4. Add the scene to `EditorBuildSettings` at index 0; keep `MainGameplay` as a disabled legacy prototype.
5. Add an idempotence guard to `HushStage.Build` so the bootstrap and the session cannot build the camera, lights, dust, and `AudioListener` twice.

**Acceptance:** A fresh clone opens the FullGame scene, presses Play, and reaches the title screen with music. A Windows build starts in the same place. No legacy `MainGameplay` object is required.

---

### P0-2. Title PLAY skips Whisper Row and can skip districts

**Symptoms**

- `currentDistrictIndex` starts at `0` (`HushGameLoopManager.cs:107`).
- Title `PLAY` calls `Game.PlayNextDistrict()` (`HushScreens.cs:177–178`).
- `PlayNextDistrictInternal` increments **before** transitioning: `currentDistrictIndex++`, then `TransitionToState(DistrictIntro)` (`HushGameLoopManager.cs:551–558`).
- On a fresh profile, PLAY therefore starts at index `1` — Echo Terrace — not Whisper Row.
- `EnterDistrictIntro` does not hide the title screen or show an intro overlay. It only moves the camera for 2.5 s (`:317–357`). The title PLAY button remains visible and clickable.
- Every click during that intro increments the index again before `TransitionToState` rejects it because `isTransitioning` is true (`:236–238`). A double-click can skip a district.
- After the last district, `PlayNextDistrictInternal` falls back to the title instead of the ending (`:559–562`).
- `TitleScreen.Refresh` labels PLAY as `CONTINUE` whenever `runs > 0`, including after failures. It is not progress-aware (`HushScreens.cs:212–217`).

**Fix**

- Separate “start a new run at a chosen district” from “advance after a result.”
- Resolve the starting district explicitly: fresh profile `0`, otherwise `HushProgress.NextDistrict`, or the player’s district-select choice.
- Disable or cover title input during the intro.
- Put the index mutation inside the transition guard, or make the transition function return success before mutating state.
- Show a proper “DISTRICT 01 — WHISPER ROW / 96 BPM / 10 LAYERS” intro overlay, then start the run.
- After the last district, show `EndingScreen`.

**Acceptance:** Fresh save → PLAY → Whisper Row. Spamming PLAY cannot skip a district. Clearing Apex → ending, not title.

---

### P0-3. Results, retry, district select, and ending are all broken

**Symptoms**

- Victory sets `isTransitioning = true`, shows results, then waits for `ConfirmDown` or `TapDown` (`HushGameLoopManager.cs:391–444`).
- `TransitionToState` rejects **every** transition while `isTransitioning` (`:236–238`).
- Results buttons call `Game.AdvanceAfterResults`, `Game.RestartDistrict`, and `Game.OpenDistrictSelect` (`HushScreens.cs:1104–1114`). All three are rejected. The buttons look active but do nothing.
- The primary button increments `currentDistrictIndex` **before** the guard (`HushGameLoopManager.cs:551–557`, `:644–646`). A click increments the index without advancing. A later Enter ends the coroutine, clears the lock, and advances again — skipping a district.
- Defeat has the same lock and waits for `ConfirmDown || TapDown || BackDown` (`:483–487`). Any key/click restarts the district automatically, regardless of which button the player chose. “District Select” is ignored.
- Clicking a result button does **not** satisfy the coroutine’s `TapDown` wait because `TapDown` rejects pointer-over-UI (`HushInput.cs:16–18`). The screen feels frozen until a keyboard/gamepad confirm.
- `PlayNextDistrictInternal` on the last district returns to title (`:559–562`). The results button is labeled `EPILOGUE` but still calls the same advance path (`HushScreens.cs:1154–1159`).
- `ShowEnding` is never called anywhere in the project.

**Fix**

- Do not model “cinematic is playing” and “UI navigation is allowed” with one `isTransitioning` flag. Use separate `cinematicLock` and `navigationLock`, or an explicit state machine.
- Make results buttons the single source of truth. Remove the coroutine’s input wait.
- Make `AdvanceAfterResults` idempotent and guard the index mutation.
- Route the last district to `EndingScreen`.
- On defeat, let Retry / District Select / Title behave as labeled; do not auto-restart.

**Acceptance:** Victory → click Next / press Enter → exactly one district advance. Retry restarts the same district. District Select opens the select screen. Apex victory → ending. Defeat → only the chosen action happens.

---

### P0-4. Escape cannot pause; settings Back recurses infinitely

**Symptoms**

- Escape is mapped to **both** `PauseDown` and `BackDown` (`HushInput.cs:62–86`).
- In `HandleInput`, pressing Escape while active:
  1. `PauseDown` pauses → state becomes `Paused`.
  2. The same frame’s `BackDown` sees `Paused` and resumes (`HushGameLoopManager.cs:207–231`).
- Net result: Escape never leaves gameplay paused. Only the HUD pause button works.
- Pause → Settings passes `Game.ResumeFromSettings` as the Back action (`HushScreens.cs:1016–1017`).
- `HushGame.ResumeFromSettings` invokes `Settings.BackAction` again (`HushGame.cs:233–236`), which is `Game.ResumeFromSettings` (`Game.cs:27`) → infinite recursion / stack overflow.
- Settings Back should return to Pause; instead it tries to “resume from settings” by re-invoking itself.
- `HandleInput` only handles Back on `DistrictSelect` and `Paused`. Settings, HowTo, Results, and Ending have no Esc/Back route. Pressing Esc while Settings is open resumes gameplay underneath it (`HushGameLoopManager.cs:209–231`).

**Fix**

- One Back action per frame. Remove Escape from `PauseDown` or make `BackDown` ignore the frame when `PauseDown` was consumed.
- Route Back by the topmost screen: Settings → previous screen (Pause or Title); HowTo → previous screen; Results → no-op or District Select; Ending → Title; gameplay → Pause.
- Replace the `Action` back-pointer with an explicit screen stack or `ShowSettings(returnTo: Pause)`.
- Add `OnApplicationFocus` / `OnApplicationPause` to pause gameplay and rebase the beat clock.

**Acceptance:** Esc toggles pause once. Esc in Settings returns to Pause. Back in Pause returns to gameplay only when Resume is chosen. No recursion.

---

### P0-5. Audio is never created, preloaded, or played

**Symptoms**

- `HushAudio.Create`, `Preload`, `PlayMenu`, and `StopGameplay` have **no call sites** in the project.
- `StackGame.Begin` calls `HushAudio.PlayDistrict` (`StackGame.cs:219`), but `PlayDistrict` returns `AudioSettings.dspTime` when the instance is null (`HushAudio.cs:159–161`). The beat clock runs, but there is no music and no click.
- The menu loop is never started. The district loop is never stopped when returning to the title, so if audio were initialized, district music would continue under the menu/results.
- `HushAudio` is not marked `DontDestroyOnLoad` and has no `OnDestroy` static reset (`HushAudio.cs:39–65`). A scene load destroys the audio object and leaves a stale static reference.
- `HushStage` creates an `AudioListener` (`HushStage.cs:46`), so a clean scene must not also contain one.

**Fix**

- Bootstrap: `HushAudio.Create()` → `BootScreen` progress → `Preload` → `PlayMenu` on title.
- On run start: `PlayDistrict`; on pause: `PauseGameplay`; on results: duck/stop; on title: `StopGameplay` then `PlayMenu`.
- Add `DontDestroyOnLoad` and `OnDestroy` reset.
- Add a calibration offset (audio/visual latency) and apply it to timing evaluation and click scheduling.
- Verify exactly one `AudioListener` in the scene.

**Acceptance:** Title has menu music. Starting a district crossfades to the district loop and click. Pause freezes audio and beat. Results duck. Return to title restores menu music. No duplicate listener warning.

---

### P0-6. Pausing during success/failure resolution destroys the result

**Symptoms**

- Success/failure coroutines use `WaitForSecondsRealtime` (`StackGame.cs:629`, `:632`, `:691`).
- If the player pauses during the 0.85 s success animation or 1.6 s failure animation, the coroutine continues while `HushGameLoopManager` is in `Paused`.
- `Finished` fires; `HandleStackFinished` ignores it unless the state is `ActiveStacking` or `HazardEncounter` (`HushGameLoopManager.cs:499–506`). The result is dropped.
- On resume, `HandleEnterActiveStacking` sees `IsFinished == true` and starts a **new run** (`:360–366`).

**Fix**

- Disallow pause during resolution (`isResolving`), or
- Queue the `RunResult` and process it on resume, or
- Replace `WaitForSecondsRealtime` in outcome routines with the existing `WaitGameplay` helper (`StackGame.cs:602–610`) so pause freezes the outcome too.

**Acceptance:** Pause during the victory animation → resume → the victory result still appears. No silent new run.

---

## 3. P1 — UI/UX

### 3.1 Systemic layout bug: text pivots are always vertical-center

`HushLayout.Text` always sets pivot Y to `0.5f` (`HushScreens.cs:37–42`). Many call sites pass top/bottom anchors and offsets as if the position were an edge. The result is text boxes centered on the offset, not aligned to it. This is the root cause of several collisions.

**Fix:** add an explicit `pivot`/`verticalAlignment` parameter, or stop hand-positioning text. Build screens from vertical stacks with padding and spacing tokens:

- spacing: 8 / 12 / 16 / 24 / 32
- type: 12 / 14 / 16 / 20 / 24 / 32 / 48 / 158
- button heights: 56 / 64
- panel padding: 32
- content width: 1240 max at 1920×1080

### 3.2 Verified collisions

| Screen | Problem | Evidence |
|---|---|---|
| Title | PLAY, DISTRICTS, HOW TO PLAY, SETTINGS are anchored from the content top and sit over the HUSH/BLOCKS wordmarks. QUIT is at `y=+20` from the top and is effectively off-screen above the logo. | `HushScreens.cs:151–154`, `:177–193`, `:199–203` |
| Pause | RESUME (y 64, h 58) and RESTART DISTRICT (y 42, h 58) overlap by 36 px. | `HushScreens.cs:1012–1015` |
| Settings | Header (center −48, h 70) and sub (center −88, h 28) overlap by 9 px. Status line at bottom y 118 (h 28) sits across the Reset button (center −300, h 54). | `HushScreens.cs:497–500`, `:532–537` |
| How To | Step title (center −220, h 40) and body (center −250, h 110) boxes overlap by 40 px. | `HushScreens.cs:448–451` |
| HUD | SCORE cap (center −40, h 24) and score value (center −68, h 74) overlap by 21 px. | `HushScreens.cs:727–730` |
| Results | NEW BEST (center −318, h 24) overlaps the score (center −266, h 100) by 10 px. | `HushScreens.cs:1086–1089` |
| Portrait | District cards span 1370 px (`x = −470/0/470`, width 430); How To panel is 1240 px; title scrim is 1320 px. At 1080×1920 the canvas is 1080 reference px wide, so these clip. | `HushScreens.cs:287–289`, `:391`, `:139–143` |

Notes:

- The settings **toggle rows do not overlap the volume rows**. The real settings collision is the status line over Reset.
- The source strings are valid UTF-8 (middle dot, multiplication sign, em dash, arrows). The `A?` seen in some console output is terminal encoding, not source damage. Do not "fix" it as mojibake.

### 3.3 Interaction and focus

- `HushButton` and `HushToggle` implement pointer interfaces only; they are not `Selectable` and have no focus/navigation (`HushUI.cs:355`, `:529`). There is no keyboard/gamepad navigation, no focus ring, and no default selected button.
- `HushInput.TapDown` checks `IsPointerOverUi()` **before** keyboard/gamepad handling (`HushInput.cs:16–18`). If the cursor rests over the pause button or any raycast-target UI, Space/Enter/gamepad South is ignored.
- Toggle hit target is the 72×36 track only; the label is not clickable (`HushUI.cs:232–269`, `:578–584`). Volume +/- buttons are 44×44 (`HushScreens.cs:552–561`).
- No `EventSystem.SetSelectedGameObject` is used. `InputSystemUIInputModule` is created, but without selectable controls it cannot navigate.
- Results/Ending/HowTo/Settings have no Esc/Back handling.

**Fix**

- Make `HushButton`/`HushToggle` derive from `Selectable` (or add a `HushSelectable` wrapper), implement `ISubmitHandler`, and expose a focus visual.
- Add a focus controller per screen: set the primary action selected on show, restore focus on back, and support D-pad/stick navigation.
- Gate only pointer/touch taps on `IsPointerOverUi`; let keyboard/gamepad through.
- Make the whole toggle row clickable; use at least 48×48 px targets (56×56 for primary actions).
- Add Esc/Back handling to every screen.

### 3.4 Typography, contrast, accessibility

- `Oswald Bold SDF` is missing. `HushArt.HeadingFont` silently falls back to `LiberationSans SDF` (`HushArt.cs:35–36`), so the intended condensed arcade identity is not present in the build.
- Many labels are 11–13 px. Muted `#8A97AD` at 0.7 alpha over `#0E1220` is about **3.69:1**; at 0.75 it is **4.07:1**; at 0.55 it is **2.74:1**. Full-opacity muted is 6.31:1. Small muted text at 0.55–0.75 fails WCAG AA for normal text.
- Reduced motion only affects the camera and title bars (`HushCameraRig.cs:65`, `HushScreens.cs:226`). The HUD ring still spins/pulses, the countdown still pops, the star reveal still animates, and the life flash still flashes (`HushScreens.cs:924–932`, `:960`, `:1180–1204`, `:985–986`).
- Amber/cyan block state is color-only. The feedback title helps timing, but the block state itself needs a shape/pattern/icon cue.
- `tutorialSeen` exists but `MarkTutorialSeen` is never called (`HushProgress.cs:19`, `:204–209`). There is no first-run onboarding gate.

**Fix**

- Bundle Oswald Bold SDF (OFL) or deliberately choose a shipped TMP font; add a startup validation warning if the heading font is missing.
- Raise body text to 16–18 px; keep 11–13 px only for non-essential caps at 4.5:1 or better.
- Make the reduced-motion flag cover HUD pulses, countdown, star reveal, and flash.
- Add a non-color cue: solid/outlined block, center marker, grade icon, or pattern.
- Gate the first run through How To Play, or add a 3-beat interactive tutorial.

---

## 4. P1 — Gameplay, scoring, progression

### 4.1 Accuracy formula is misleading

`Accuracy => (perfects + goods) / floors` (`StackGame.cs:132`). Misses are excluded from the denominator; offbeats are included in the denominator but not the numerator. A successful run with 10 goods and 2 misses can display **100%**. A failed run with 2 goods, 0 perfects, and 3 misses can also display 100%.

**Fix:** weighted accuracy over attempts:

```
attempts = floors + misses
accuracy = (perfects * 1.0 + goods * 0.7 + offbeats * 0.3) / attempts
```

Or at minimum `(perfects + goods) / (floors + misses)`. Show the breakdown on results: Perfect / Good / Offbeat / Miss.

### 4.2 Star thresholds are too low

With full lives, all drops centered, and no misses, approximate final scores are:

| District | All-Good | All-Perfect | Current 3★ | Current 2★ |
|---|---:|---:|---:|---:|
| Whisper Row | 5,305 | 6,775 | 4,500 | 2,800 |
| Echo Terrace | 7,855 | 10,305 | 6,500 | 4,200 |
| Resonant Apex | 11,061 | 14,715 | 8,500 | 5,600 |

Every all-Good run earns 3★. Even all-Good with one life lost still clears 3★ in Whisper Row (≈4,905 vs 4,500). The thresholds do not differentiate timing mastery.

**Fix:** set 3★ above the all-Good score and 2★ near a clean all-Good run with one life lost. Example targets: Whisper 5,800 / 4,800; Echo 8,800 / 7,200; Apex 12,500 / 10,200. Tune after playtest, but derive them from the score budget rather than arbitrary values.

### 4.3 “Tap on the beat” and “stay centered” conflict

`UpdateMovingBlock` uses an 8-beat triangle wave. At integer beats:

- t=0 → x=0
- t=1 → x=+range/2
- t=2 → x=+range
- t=3 → x=+range/2
- t=4 → x=0
- t=5 → x=−range/2
- t=6 → x=−range
- t=7 → x=−range/2

So the block is centered only on beats 0, 4, 8… (`StackGame.cs:318–326`). Tapping exactly on beats 1/2/3/5/6/7 is perfectly timed but not centered, and shrinks the block.

The center tolerance is an absolute `abs(x - top.Center) <= 0.09` (`StackGame.cs:443`). At Apex speed that is roughly a 42–50 ms window, tighter than the 85 ms Perfect window (`:448`). The game asks the player to do two incompatible things.

**Fix options**

- Make the block cross center on every beat (e.g., a triangle wave with a 2-beat period, or phase-align the sway to the beat).
- Or decouple the skills: timing determines grade/score; a separate “sweet spot” marker determines width, and explain the distinction.
- Or widen the center window and show a landing preview.
- Do not use an absolute world-space tolerance independent of block width and sway speed.

### 4.4 Timing feedback and calibration

- `Drop()` records `dropBeatOffset` at tap time (`StackGame.cs:398–400`), then the block falls for 0.085 s (`:413–421`). The visual landing happens 85 ms after the graded input. If the fantasy is “the block lands on the beat,” grade the predicted landing or shorten the drop animation.
- There is no audio latency calibration. On Windows, output latency can consume a large part of the 85 ms Perfect window.
- Results show only Perfect count, not Good/Offbeat/Miss, and no early/late indicator.
- Add a calibration screen (tap 8–16 times, compute median offset), a “timing offset” setting, and early/late ms feedback in practice.

### 4.5 Progression totals and labels

- `HushProgress.SubmitRun` updates best scores, stars, unlocks, `completed`, and `runs`, but never updates `totalFloors` or `totalPerfects` (`HushProgress.cs:187–201`). The ending displays `totalFloors` as 0 (`HushScreens.cs:1267`).
- Title stats hardcode `9` stars and `3` districts (`HushScreens.cs:216`), and sum three best scores. `HushProgress.BestScore` returns the max single-district score. The same word “BEST” means different things on different screens.
- `HushProgress.Changed` has no subscribers; live updates rely on screens refreshing in `Show`.
- `completed` is set but never used to trigger the ending.

**Fix:** extend `SubmitRun` with floors/perfects, update totals, use `DistrictCatalog.Count * 3` and `DistrictCatalog.Count`, and label `BEST RUN` vs `BEST TOTAL` explicitly.

---

## 5. P1 — Build and runtime robustness

### 5.1 Player settings

| Setting | Current | Recommended |
|---|---|---|
| Product name | `Hush Block` (`ProjectSettings.asset:16`) | `Hush Blocks` |
| Version | `0.1.0` (`:146`) | Match UI (`1.0.0`) or update UI |
| Default resolution | 1024×768 (`:44–45`) | 1920×1080, resizable window |
| Fullscreen | FullScreenWindow (`:110`) | Windowed for dev; FullScreenWindow for ship |
| Run in background | 0 (`:85`) | Keep 0, but add focus pause and clock rebase |
| vSync | 0 (`QualitySettings.asset:32`, `:86`) | vSync 1 or `Application.targetFrameRate = 60/120` |
| Input handler | Both (`:924`) | New only after legacy retirement |
| Company | DefaultCompany (`:15`) | Real studio name |

### 5.2 Shader stripping risk

`HushMaterials` obtains URP Lit/Unlit and `Sprites/Default` at runtime with `Shader.Find` (`HushMaterials.cs:21–25`, `:93`). `GraphicsSettings.asset:29–36` contains only the default always-included shaders. A player build may strip the URP shaders or `Sprites/Default`, producing pink materials or null shaders.

**Fix:** verify in a Windows build; if needed, add the shaders to Always Included Shaders or serialize materials that reference them. Do this before the final build, not after.

### 5.3 Audio listener and UI persistence

- `HushStage` creates its own camera and `AudioListener` (`HushStage.cs:46`). The clean scene must contain no other camera/listener. `HushStage.Build` is not idempotent; `HushGameLoopManager.InitializeStage` calls `stage.Build()` (`:199–205`), so a bootstrap that also builds the stage will duplicate it.
- `HushGame` is `DontDestroyOnLoad` (`HushGame.cs:44`), but the `HushUI` canvas and `EventSystem` are separate root objects (`HushUI.cs:28`, `:263–272`). A scene reload destroys the canvas while `HushGame.UI` still holds a C# reference; `BuildScreens` returns early because `UI != null`.
- `HushUI.EnsureEventSystem` returns if **any** EventSystem exists, even one with a legacy `StandaloneInputModule`.

**Fix:** make the UI/EventSystem persistent with the game root, or keep one scene and never reload; replace the input module if the old one is found; add a `built` guard to `HushStage`.

### 5.4 Boot cost and performance

`HushGame.BuildScreens` constructs every screen synchronously in `Awake` (`HushGame.cs:48–74`), including all procedural sprites and TMP objects. `HushAudio.Preload` generates menu + three district loops. A boot screen exists but is unused. This will cause a visible startup hitch.

**Fix:** use the boot screen; build screens in stages (title first, gameplay screens on demand), preload audio asynchronously, and keep a loading budget. Add a target frame rate and test 60/120/144 Hz.

### 5.5 Tests and assembly boundaries

- No `.asmdef` files exist under `Assets/_Project/Scripts`; everything compiles into `Assembly-CSharp`.
- No project-authored tests exist.
- There is no build script / `BuildScript.Build` entry point.

**Fix:** add `HushBlocks.FullGame.asmdef` (runtime) and `HushBlocks.FullGame.Editor.asmdef`; add EditMode tests for scoring, accuracy, star thresholds, save migration, and layout overlap; add a PlayMode smoke test for title → run → results. Add a Windows build method and a menu item.

---

## 6. P2 — Architecture and product polish

### 6.1 One orchestrator, not three facades

Today the flow is split across:

- `HushGame` — UI construction and screen switching,
- `HushGameLoopManager` — a second state machine and run flow,
- `Game` — a static facade over the loop,
- `HushScreens` — static facade over `HushGame`,
- legacy `VerticalSliceGameManager`, `DistrictProgression`, `TowerManager`, `RhythmManager`, and `AcousticInputController` — still referenced by the loop (`HushGameLoopManager.cs:33–38`, `:84–88`).

`HushGameLoopManager` is never instantiated, so its duplicate state machine is dead weight. `Game` delegates to null. `HushGame` exposes the same methods again. This is the main reason the flow is hard to reason about.

**Recommended split**

- `HushApp` / `GameBootstrap`: scene lifetime, audio creation, boot flow.
- `HushSession` (rename of the loop): district selection, run lifecycle, results, progression. No UI construction.
- `HushUI` / screens: views only; raise intents (`PlayRequested`, `RetryRequested`, `BackRequested`).
- `StackGame`: pure run simulation.
- `HushStage`: presentation, camera, lighting, audio listener.
- `HushProgress`: persistence.

Then delete `Game.cs` and the duplicate facade methods on `HushGame`; remove legacy manager dependencies from the new flow. Keep the legacy scene as a disabled prototype.

### 6.2 Use the Double-Beat Block art pipeline

`BlockView` builds the tower from primitive cubes and runtime materials (`BlockView.cs:66–81`). The repaired `HushBlock_DoubleBeat.prefab` and materials are not used by the FullGame layer, so the art pipeline work is invisible in the playable build.

**Recommendation:** surface the Double-Beat Block where it matters:

- use it for the foundation/hero block and perfect placements,
- use a lower-cost variant or LOD for stacked layers,
- keep the primitive fallback for low-end/mobile,
- add one screenshot/validation scene showing the prefab in the tower.

If the intent is to retire the prefab, document that decision explicitly and remove the unused pipeline from the shipping scope. Do not leave it ambiguous.

### 6.3 Result and ending presentation

- Results show score, stars, floors, accuracy, perfects, max combo, and best. They do not show Good/Offbeat/Miss, early/late, or the next unlock.
- The ending exists visually but is unreachable and shows `totalFloors = 0`.
- `EndingScreen` sets all three stars amber unconditionally (`HushScreens.cs:1241–1246`); it does not reflect the player’s actual star total.
- `HushScreens.ShowLegacyVictory` builds `Stars = 3` and uses `GetCurrentRunScore()`, which returns tower height, not score (`HushGameLoopManager.cs:187`). This duplicate path is inconsistent.

**Fix:** make results a readable scorecard: outcome, score, stars, breakdown, delta vs best, next action. Make the ending reflect `TotalStars`, `totalFloors`, `totalPerfects`, and district clears. Delete the legacy victory path.

### 6.4 Onboarding and settings

- `tutorialSeen` is dead. First-run players are dropped into a rhythm game with only an optional How To screen.
- Settings has no “audio latency,” “text size,” “high contrast,” “screen shake,” “reduced motion” coverage beyond the toggle, or “mute when unfocused.”
- Reset Progress is a two-click confirmation inside Settings; good direction, but the status text overlaps the button.
- Pause “Quit to District Select” discards the run without confirmation. Add a confirm or label it “Abandon Run.”

### 6.5 Documentation drift

`README.md` and `CLAUDE.md` still describe the Phase 1 greybox and say there is no rhythm/audio/progression layer. The FullGame layer is not mentioned. Update the README with the actual entry scene, controls, build command, and current limitations; add a short “FullGame architecture” section.

---

## 7. Fix sequence

### Pass 0 — Make it launch (highest priority)

1. Create `HushBlocksFullGame.unity` and `GameBootstrap`.
2. Create/preload audio, build stage once, show Boot, transition to Title.
3. Add the scene to build settings; keep `MainGameplay` disabled.
4. Add a Windows build method.

**Exit criteria:** Play mode and a Windows build both open the FullGame title. No legacy scene required. One AudioListener. No console errors.

### Pass 1 — Make the campaign loop correct

1. Fix title start index, intro overlay, and input lock.
2. Replace the results coroutine/`isTransitioning` flow with explicit navigation.
3. Wire the ending.
4. Fix Escape/Back routing and the settings back stack.
5. Fix pause during outcome resolution.
6. Wire menu/district/results audio lifecycle.

**Exit criteria:** Fresh save → Whisper Row → results → Echo → Apex → ending. Pause, settings, retry, district select, and quit all work with mouse, keyboard, and gamepad. No double advance. No lost result.

### Pass 2 — Repair the UI system

1. Add layout tokens and vertical stacks; fix the text pivot API.
2. Fix the collision table; rebuild title, pause, settings, how-to, HUD, and results.
3. Add selectable/focus navigation, default selection, focus ring, and 48 px targets.
4. Make the toggle row clickable; gate pointer-only taps.
5. Lock landscape for mobile or add a narrow layout; test 16:9, 16:10, 21:9, 1080×1920.

**Exit criteria:** No overlapping rects at reference resolutions. Full keyboard/gamepad traversal. Touch targets pass. Portrait either reflows or is explicitly unsupported.

### Pass 3 — Tune the game

1. Fix accuracy denominator and display breakdown.
2. Retune star thresholds; test all-Good vs all-Perfect runs.
3. Resolve the centering/beat conflict; retune sway, center window, and drop animation.
4. Add latency calibration and early/late feedback.
5. Add first-run tutorial gating and reduced-motion coverage.

**Exit criteria:** A skilled player can intentionally earn 1/2/3 stars. A clean all-Good run does not automatically earn 3★. Centering and timing can both be performed intentionally. Calibration changes the felt offset.

### Pass 4 — Ship quality

1. Product name/version/resolution/vSync/target frame rate.
2. Shader stripping and Always Included Shaders.
3. Font, contrast, text size, colorblind cues, reduced motion.
4. Save totals, ending stats, title labels.
5. Windows build, 30-minute playtest, screenshots, README/build docs.

**Exit criteria:** Build starts at FullGame title, all three districts completable, progression persists across restart, no missing shaders/fonts/listeners, and the ending reflects real totals.

---

## 8. Smoke-test checklist

- [ ] Fresh profile → title → PLAY → Whisper Row (not Echo Terrace).
- [ ] Spam PLAY during the district intro; no district skip.
- [ ] Pause with HUD button and Escape; resume, restart, settings, back.
- [ ] Settings back from pause returns to pause, not gameplay; no stack overflow.
- [ ] Victory → Next/Enter advances exactly one district.
- [ ] Victory → Retry restarts the same district.
- [ ] Victory → District Select opens the select screen.
- [ ] Defeat → Retry / District Select / Title behave as labeled.
- [ ] Apex victory → ending; totals are non-zero.
- [ ] Pause during victory/defeat animation; result is not lost.
- [ ] Menu music, district loop, click track, pause, results duck, title restore.
- [ ] Mouse, touch, keyboard, gamepad; focus visible; Enter/Space/Esc work on every screen.
- [ ] 16:9, 16:10, 21:9, 1080×1920; safe area; no clipping at 1920×1080.
- [ ] Reduced motion stops HUD pulses, countdown pop, star reveal, and flash.
- [ ] Windows build: correct scene, one listener, no pink materials, no missing font.

---

## 9. Evidence and assumptions

- No play-mode session or build was run. The FullGame layer has no scene/bootstrap, so a fair playtest is impossible until P0-1 is fixed. The flow defects above are logical consequences of the cited code and are deterministic.
- UI collisions were computed from the serialized rect positions/sizes in `HushScreens.cs`, using the 1920×1080 canvas reference and the `CanvasScaler` settings in `HushUI.cs:33–37`.
- Star-score examples assume full lives, every drop centered, no misses, and the current combo/center/floor bonuses. Exact values vary with lives and misses; the conclusion (all-Good earns 3★) holds across all three districts.
- The source files contain valid UTF-8 typographic characters. Do not treat the console’s garbled glyph rendering as source corruption.
- Line numbers refer to the current files under `Assets/_Project/Scripts/FullGame`.

**Bottom line:** the concept and core simulation are worth keeping. The next action is not more content; it is P0-1: create the FullGame scene and bootstrap, then fix the six flow defects before touching polish.
