# Hush Blocks - Phase-by-Phase Fix & Improvement Prompt Pack

**Project:** `D:\Game Project\Hush Block`
**Engine:** Unity `6000.4.7f1`, URP `17.4.0`, Input System `1.19.0`
**Source audit:** `FULLGAME_AUDIT.md`
**Purpose:** Turn the audit into seven gated implementation phases that an AI coding agent can execute one at a time.

---

## How to use this pack

1. Run **one phase per session, branch, or pull request**. Do not let the agent start the next phase in the same run.
2. Paste everything inside that phase's prompt block into the agent.
3. The agent must implement, compile, and verify in Play mode before reporting.
4. Run the phase gate yourself. If it fails, iterate on that phase only.
5. Commit with the suggested message. Then start the next phase from a clean state.
6. Keep `FULLGAME_AUDIT.md` open as the evidence source. Line numbers may drift; behavior is the contract.

If the agent cannot run Unity, it must still make the code and scene changes, state exactly what it could not verify, and must not mark the phase complete.

---

## Phase map

| Phase | Outcome | Gate |
|---|---|---|
| 0 | Launchable FullGame | Play mode and a Windows build both open the boot screen, then the title, with menu music |
| 1 | Correct campaign loop | Fresh save to Whisper Row to Echo to Apex to ending; retry, select, pause, settings, and back all work |
| 2 | Layout system and screens | No overlapping or clipped UI at 1920x1080, 1280x720, 2560x1080, and 1080x1920 |
| 3 | Input and accessibility | Full keyboard, gamepad, mouse, and touch parity; visible focus; AA text; reduced motion |
| 4 | Gameplay mastery | All-Good run earns at most 2 stars; all-Perfect earns 3; calibration changes timing |
| 5 | Audio lifecycle | Menu, district, clicks, pause, results, and title transitions are correct and drift-free |
| 6 | Ship quality | Clean Windows build, accurate docs, no missing shaders or fonts |

---

## Global rules for every phase

- Work only in the FullGame layer unless the phase explicitly lists another file.
- Preserve the legacy `MainGameplay` scene, legacy gameplay scripts, the Double-Beat Block importer, `HushBlock_DoubleBeat.prefab`, and its materials.
- Do not add external packages, services, or art/audio assets. A single OFL-licensed font is allowed only in Phase 3 if the existing TMP font cannot represent the intended headings; record the license.
- Use the new Input System. Do not add legacy `Input.GetKey` calls.
- After every script change: refresh assets, wait for compilation, read the Console, and fix all compile errors and new warnings.
- Verify behavior in Play mode. A phase is not done because the code compiles.
- Do not silently swallow missing references with `?.` or null-conditional no-ops.
- Do not start later-phase work. If a later-phase dependency is unavoidable, implement the smallest stub and record it in the report.
- End every phase with the report format below.

### Required report format

```
FILES CHANGED
- path - what changed

BEHAVIOR VERIFIED
- Play mode: ...
- Build: ...
- Console: ...

EVIDENCE
- screenshot / log path

OPEN RISKS
- ...

NEXT PHASE READY
- yes / no, with reason
```

---

## Phase 0 - Launchable FullGame

**Gate before:** none.
**Gate after:** opening `HushBlocksFullGame.unity` and pressing Play reaches the title with menu music; the Windows build does the same; one AudioListener; no console errors.

````text
PHASE 0 - LAUNCHABLE FULLGAME

You are the senior Unity engineer for Hush Blocks at D:\Game Project\Hush Block (Unity 6000.4.7f1, URP 17.4, Input System 1.19, Windows 64-bit). Read FULLGAME_AUDIT.md first. Implement the changes; do not produce a plan or documentation instead of the work.

CURRENT STATE
- Build settings launch Assets/_Project/Scenes/MainGameplay.unity, the legacy greybox.
- No scene or prefab references any script under Assets/_Project/Scripts/FullGame.
- HushGame builds every screen in Awake but never shows one.
- HushGameLoopManager is never instantiated.
- HushAudio.Create, Preload, and PlayMenu have no call sites.
- HushStage.Build is not idempotent and creates the only Camera and AudioListener at runtime.

GOAL
Make the FullGame layer the default entry point in Play mode and in a Windows build: boot screen -> audio preload -> title screen with menu music. Do not fix campaign flow, layout, scoring, or gameplay in this phase.

TASKS
1. Make HushStage.Build idempotent.
   - Add a built flag and an IsBuilt property.
   - Return immediately if already built. Set the flag before building.
   - After building, verify exactly one AudioListener exists and log an error if not.
   - Do not create a second camera or EventSystem.

2. Make HushAudio safe to create once.
   - Create() returns the existing instance if present; otherwise create one root object, add HushAudio, call DontDestroyOnLoad, and initialize.
   - Add OnDestroy that clears the static instance and the preloaded flag only if this is the active instance.
   - Make Preload idempotent: if already preloaded, report 1.0 and yield break.
   - Do not change the synthesis or the public sound API.

3. Add Assets/_Project/Scripts/FullGame/GameBootstrap.cs.
   - [DefaultExecutionOrder(-1000)] and [DisallowMultipleComponent].
   - Awake, in this order:
     a. HushAudio.Create();
     b. HushGame game = HushGame.EnsureInstance();
     c. create a child "Hush Stage" with HushStage and call Build() once;
     d. add HushGameLoopManager to the bootstrap object and call Initialize(stage, game, bootControlled: true);
     e. validate one AudioListener, one EventSystem, one HushGame, one HushGameLoopManager, one HushStage.
   - Start runs a boot coroutine:
     a. game.ShowBootScreen();
     b. yield return HushAudio.Preload(p => game.Boot.SetProgress(p));
     c. wait 0.4 seconds realtime;
     d. HushAudio.PlayMenu();
     e. game.ShowTitleScreen();
     f. set the fade overlay to black, then fade it out over 0.35 seconds.
   - Expose the stage and session as read-only properties.

4. Refactor HushGameLoopManager startup, without changing its state machine yet.
   - Remove the automatic OpenTitleScreen call from Start.
   - Add public void Initialize(HushStage stage, HushGame game, bool bootControlled).
   - Subscribe to events exactly once (unsubscribe first).
   - InitializeStage only calls stage.Build() if stage.IsBuilt is false.
   - If Start runs without Initialize, log a clear error and do nothing.
   - Keep DontDestroyOnLoad and the singleton guard.

5. Add HushGame.ShowBootScreen() and make Boot reachable. Add HushGame.ShowEndingScreen() only if it is trivial; the ending flow is Phase 1.

6. Add Assets/_Project/Editor/HushBlocksFullGameBuilder.cs.
   - Menu item: Hush Blocks/Build Full Game Scene.
   - Create a new empty scene, add one GameObject named "Game Bootstrap" with GameBootstrap, and save it to Assets/_Project/Scenes/HushBlocksFullGame.unity.
   - Update EditorBuildSettings: HushBlocksFullGame enabled at index 0; MainGameplay retained but disabled; preserve any other entries.
   - Idempotent: overwrite the scene file, never duplicate build entries.

7. Add Assets/_Project/Editor/HushBlocksBuild.cs.
   - Menu item: Hush Blocks/Build/Windows x64.
   - Build all enabled scenes to Builds/Windows/HushBlocks.exe for StandaloneWindows64.
   - Create the output directory, log the BuildReport result, and throw on failure.
   - Also expose a static BuildWindows method usable from the Unity command line.

8. Verify.
   - Run Hush Blocks/Build Full Game Scene.
   - Enter Play mode. Confirm boot progress, title, menu music, and no console errors.
   - Build and run the exe. Confirm the same flow.
   - Open MainGameplay manually and confirm the legacy prototype still loads.
   - Capture a title screenshot.

ACCEPTANCE
- Play mode and the built exe both open the FullGame boot then title.
- Menu music plays and the boot progress advances.
- Exactly one AudioListener, one EventSystem, one HushGame, one HushGameLoopManager, one HushStage.
- No NullReferenceException, no missing script, no duplicate singleton warning.
- MainGameplay remains present and disabled in build settings, and still opens manually.
- No legacy script, prefab, material, or importer is modified.

OUT OF SCOPE
Campaign flow, results, Escape routing, layout, focus navigation, scoring, star thresholds, shader stripping, and final player settings.

STOP CONDITION
Stop after the Phase 0 acceptance evidence. Report files changed, play/build results, console state, and any remaining risk. Do not start Phase 1.
````

---

## Phase 1 - Campaign Flow and Navigation

**Gate before:** Phase 0 is green.
**Gate after:** a fresh profile can complete Whisper Row, Echo Terrace, and Resonant Apex, reach the ending, and use pause, settings, retry, district select, and back without skipped districts or lost results.

````text
PHASE 1 - CAMPAIGN FLOW AND NAVIGATION

You are the senior Unity engineer for Hush Blocks at D:\Game Project\Hush Block. Phase 0 is complete: the FullGame scene opens to the title in Play mode and in a Windows build. Read FULLGAME_AUDIT.md sections P0-2, P0-3, P0-4, and P0-6 before editing.

GOAL
Make the campaign loop correct with mouse input: title -> district intro -> run -> results -> next/retry/select -> ending. Fix pause and back routing. Make outcome resolution pause-safe. Do not polish layout, focus navigation, scoring formulas, star thresholds, or the audio mix in this phase.

TASKS
1. Make transitions explicit and non-mutating on reject.
   - Change TransitionToState to return bool and check all locks before mutating currentState or currentDistrictIndex.
   - Remove the single isTransitioning flag that rejects every result-button transition.
   - Separate cinematic presentation from navigation. A presentation coroutine must not own the input lock.
   - Add GameState.Results and GameState.Ending if that makes the flow clearer.

2. Fix the title start index.
   - Fresh save (runs == 0 and all stars 0): start district 0.
   - Otherwise start HushProgress.NextDistrict, or the card chosen in district select.
   - Remove the pre-increment in PlayNextDistrict. PLAY must not start at Echo Terrace on a fresh profile.
   - Label the title action PLAY for a fresh save and CONTINUE when progress exists. Do not use runs > 0 alone.

3. Add a district intro screen.
   - New DistrictIntroScreen in HushScreens.cs: dim background, "DISTRICT 01", district name, BPM, layer count, one tip, and "TAP / SPACE TO BEGIN" or an auto-advance after 2.5 seconds.
   - Add HushGame.ShowDistrictIntro(DistrictDefinition), build it in BuildScreens, and hide it initially.
   - While the intro is visible, hide the title and district select. Only skip/confirm is accepted.
   - Start the run only after the intro completes. Repeated clicks must not mutate the district index.

4. Make one run owner.
   - Add HushGameLoopManager.StartRun(int index), RetryDistrict(), AdvanceAfterResults(), ReturnToDistrictSelect(), ReturnToTitle(), and ShowEnding().
   - StartRun:
     a. validate unlock and clamp the index;
     b. destroy the previous StackGame and clear HUD/tower;
     c. show the intro;
     d. after the intro, create StackGame, call Begin(index, stage), subscribe Finished, call SetCurrentRun, show the HUD, and enter ActiveStacking.
   - AdvanceAfterResults:
     a. only valid in Results with a successful result;
     b. consume the result once;
     c. last district -> ShowEnding;
     d. otherwise StartRun(index + 1).
   - RetryDistrict -> StartRun(current index).
   - ReturnToDistrictSelect and ReturnToTitle -> end and destroy the run, stop clicks, clear HUD, show the screen.

5. Make results button-driven.
   - HandleStackFinished stores the RunResult and calls HushGame.ShowResults(result). It must not wait for TapDown or ConfirmDown.
   - Delete the input waits in ExecuteVictorySequence and ExecuteDefeatSequence. Keep only presentation delays if needed.
   - Wire ResultsScreen buttons to session methods:
     primary Next/Epilogue, secondary Retry, tertiary District Select, plus a Title action on defeat. Add a fourth button if needed; Phase 2 will lay it out.
   - Remove ShowLegacyVictory and ShowDefeatScreen duplicate result construction. Use the real RunResult.
   - Make AdvanceAfterResults idempotent: a second click or Enter must not advance twice.

6. Wire the ending.
   - Last district victory -> primary button label EPILOGUE -> EndingScreen.
   - Add HushGame.ShowEndingScreen().
   - Ending buttons: Back to Title and District Select.
   - Ending stats read HushProgress. Phase 4 fills the totals; do not fake them.

7. Fix back and pause routing.
   - In HushInput, remove Escape from PauseDown. Escape is Back only. PauseDown is gamepad Start, the P key, and the HUD pause button.
   - Add an explicit overlay/back stack in the session: Settings, HowTo, Pause, Results, Ending, DistrictSelect, Title.
   - HandleBack priority:
     Settings -> close to its return screen;
     HowTo -> close to its return screen;
     Results -> District Select;
     Ending -> Title;
     Paused -> Resume;
     Active or Hazard -> Pause;
     DistrictSelect -> Title;
     Title -> no-op.
   - Replace ShowSettings(Action back) and ShowHowTo(Action back) with a HushReturnTarget enum: Title or Pause.
   - Delete the ResumeFromSettings recursion. Settings opened from Pause must return to Pause, not resume gameplay underneath.
   - Escape on Settings, HowTo, Results, and Ending must never leak into gameplay.

8. Make outcome resolution pause-safe.
   - Add StackGame.IsResolving. Set it true at the start of SuccessRoutine and FailureRoutine and false at the end.
   - Pause() must ignore requests while IsResolving or IsFinished.
   - Replace WaitForSecondsRealtime in both outcome routines with the existing WaitGameplay helper.
   - If Finished arrives while Paused, queue the result and show it on resume. This is a fallback, not the primary path.

9. Add the basic audio lifecycle.
   - Do not double-start district music: StackGame.Begin already calls PlayDistrict.
   - On results: StopClicks and DuckGameplay(0.35f).
   - On title: StopGameplay(0.6f) then PlayMenu().
   - On pause/resume: PauseGameplay(true/false).
   - Do not redesign the synth or add a calibration UI in this phase.

10. Remove silent no-ops.
   - HushGameLoopManager static methods must not use Instance?. and silently do nothing. Resolve the live instance or log an error.
   - HushGame.OpenX methods must call the live session.
   - You may keep Game.cs as a compatibility facade, but it must delegate to a live session and log an error if missing. Do not delete it yet.

ACCEPTANCE
- Fresh profile -> PLAY -> District 01 Whisper Row intro -> run. It must not start at Echo Terrace.
- Spamming PLAY or clicking during the intro cannot skip a district.
- Victory -> Next advances exactly one district, exactly once per click or Enter.
- Victory -> Retry restarts the same district; District Select opens the select screen.
- Defeat -> Retry, District Select, and Title do exactly what they say. No auto-restart.
- Apex victory -> EPILOGUE -> ending -> title.
- Escape toggles pause once. Settings back returns to Pause. No stack overflow. Escape in Settings does not resume gameplay underneath.
- Pause during the victory or defeat animation: the result still appears after resume.
- Mouse-only full loop. Console clean.

OUT OF SCOPE
Layout collisions, focus navigation, keyboard/gamepad traversal, accuracy formula, star thresholds, sway tuning, latency calibration, shader stripping, final player settings.

STOP CONDITION
Stop after the Phase 1 acceptance evidence. Report files changed, the full-loop path, screenshots, console state, and remaining risks. Do not start Phase 2.
````

---

## Phase 2 - Layout System and Screen Rebuild

**Gate before:** Phase 1 is green.
**Gate after:** the layout validator reports no unintended overlaps at 1920x1080, 1280x720, 2560x1080, and 1080x1920; every screen is readable and fully inside the safe area.

````text
PHASE 2 - LAYOUT SYSTEM AND SCREEN REBUILD

You are the senior Unity UI engineer for Hush Blocks at D:\Game Project\Hush Block. Phase 1 is complete: the campaign loop is correct. Read FULLGAME_AUDIT.md sections 3.1 and 3.2 before editing.

GOAL
Remove the systemic text-pivot bug and every verified collision. Rebuild the screens on layout tokens so they hold at 1920x1080, 1280x720, 2560x1080, and 1080x1920. Do not add focus navigation, scoring changes, or gameplay tuning in this phase.

TASKS
1. Add HushLayoutTokens in the FullGame namespace.
   - Spacing: 8, 12, 16, 24, 32, 48.
   - Type: caption 12, small 14, body 16, subtitle 20, title 28, h1 40, display 64, wordmark 158.
   - Buttons: primary 64, secondary 56, icon 56, minimum touch 48.
   - Panel padding 32, content max width 1240, screen margin 48.

2. Fix HushLayout.Text.
   - Add an explicit vertical pivot or alignment parameter. Derive the default pivot from the alignment: Top/TopLeft/TopRight -> y = 1; Bottom/BottomLeft/BottomRight -> y = 0; Middle/Center -> y = 0.5.
   - Add a top-anchored text helper and a bottom-anchored text helper.
   - Migrate every call site that passes a top or bottom anchor to the correct pivot. Do not leave a mix of interpretations.
   - Keep the old signature working only if it forwards to the new behavior.

3. Add a simple column helper.
   - A HushColumn value that starts at a top Y, returns the next item's center or top position for a given height, and advances by height + spacing.
   - Use it for menus, settings rows, results stats, and card content. Do not hand-place every row with unrelated offsets.

4. Rebuild the screens to this contract at 1920x1080.
   - Title: wordmark block at left 96, top 96; menu column at right 96, top 180, width 420; buttons 64 high with 16 gap; stats bottom-left; version bottom-right; QUIT inside the menu column or bottom-right. No button may overlap HUSH or BLOCKS. When the canvas is narrower than 1400, stack the menu below the wordmark.
   - District select: header top 72; three cards 380x520 with 24 gap, centered; footer bottom 48. At width below 1280, stack cards vertically inside a ScrollRect.
   - How To: panel 1100x820 centered; header top 40; three steps, each 120 high, 24 gap; footer buttons 64 high.
   - Settings: panel 900x900 centered; header top 40; sub 16; volume rows 72 high; toggle rows 64 high; reset row 64; status toast above the footer, never over the reset button; back button 56.
   - Pause: panel 720x600 centered; header top 48; four buttons 520x64 with 16 gap; Esc hint bottom 32. RESUME and RESTART must not overlap.
   - HUD: identity plate 380x116 at top-left 36; score panel 300x100 at top-right 36; district label, name, and floor inside the plate with real gaps; hint bottom-center 48; countdown center; combo above center. SCORE caption must not overlap the score value.
   - Results: panel 960x840 centered; header 48; subtitle 14; stars row 3 x 64; score 64; NEW BEST badge with its own row; stats grid 2 columns x 3 rows; three or four buttons in a row at the bottom with 16 gap.
   - Ending: header; total stars; total layers; total perfects; district clears; two buttons. All in one column with 24 gap.

5. Make the layout responsive.
   - The CanvasScaler reference is 1920x1080. At 16:9 the canvas stays 1920x1080 in UI units even on a 1280x720 window; at 21:9 the canvas height shrinks; at 1080x1920 the canvas becomes 1080x1920 in UI units. Use the canvas rect, not raw Screen.width/height, for breakpoints.
   - Clamp every panel width to min(design width, canvas width - 2 * screen margin).
   - Below 1400 canvas width, stack the title menu below the wordmark and collapse horizontal groups to vertical. Enable vertical scrolling where content exceeds the height.
   - At portrait 1080x1920, use a single-column layout for title, district select, how-to, settings, results, and ending. HUD collapses to a top bar plus a bottom hint.
   - At canvas heights below 900, clamp panel height to canvas height - 96 and use a ScrollRect.
   - Do not rely on the canvas scaler alone to hide clipping.

6. Add a layout validator.
   - Add HushLayoutIgnore and put it on dim, glow, fullscreen, and decorative images.
   - Add HushLayoutValidator: on each screen Show in development builds, collect active RectTransforms with a Graphic or TMP text, compute canvas-space rects, and log any pair whose intersection area is greater than 1 px and that is not ancestor/descendant.
   - Include screen name, full hierarchy path, and both rects in the log. Bind F9 to rerun on the current screen.
   - Do not ship the validator in release builds; guard it with Debug.isDebugBuild or UNITY_EDITOR.

7. Verify at four resolutions.
   - 1920x1080, 1280x720, 2560x1080, 1080x1920.
   - For each screen: title, district select, how-to, settings, pause, HUD, results, ending.
   - Capture screenshots. Fix every validator hit that is not an intentional decorative overlap.

ACCEPTANCE
- No unintended overlapping rects at the four resolutions.
- No text clipped by its panel or the safe area.
- No button below 48x48; primary actions are 64 high.
- The title menu no longer covers the wordmark; QUIT is visible.
- Pause RESUME and RESTART are separated.
- Settings header/sub and status/reset are separated.
- How To title and body are separated.
- HUD SCORE caption and value are separated.
- Results NEW BEST and score are separated.
- District select and how-to remain usable at 1080x1920.
- Screenshots attached for all screens.

OUT OF SCOPE
Selectable/focus navigation, keyboard/gamepad input, contrast changes, font replacement, scoring, sway, audio, shader stripping.

STOP CONDITION
Stop after the Phase 2 acceptance evidence. Report files changed, validator output, screenshots, and remaining risks. Do not start Phase 3.
````

---

## Phase 3 - Input, Focus, and Accessibility

**Gate before:** Phase 2 is green.
**Gate after:** every screen can be driven with keyboard only and gamepad only; focus is visible; touch targets pass; small text meets contrast; reduced motion stops all listed animations.

````text
PHASE 3 - INPUT, FOCUS, AND ACCESSIBILITY

You are the senior Unity UI/UX engineer for Hush Blocks at D:\Game Project\Hush Block. Phase 2 is complete: the layout is stable. Read FULLGAME_AUDIT.md sections 3.3 and 3.4 before editing.

GOAL
Add full keyboard, gamepad, mouse, and touch parity with visible focus and accessible defaults. Do not change scoring, sway, star thresholds, or audio synthesis in this phase.

TASKS
1. Make HushButton a Selectable.
   - Derive from Selectable and implement ISubmitHandler in addition to the existing pointer handlers.
   - Set targetGraphic to the background image and transition to None; keep the existing custom hover/press animation.
   - OnSelect and OnDeselect drive a visible focus state: brighter border, 2 px accent outline, and a small scale change.
   - OnSubmit invokes Clicked when interactable.
   - SetInteractable must update Selectable.interactable and the CanvasGroup together.
   - Keep pointer click behavior and the UI click sound.

2. Make HushToggle a Selectable row.
   - Put HushToggle on the full row root, not only the 72x36 track.
   - Add a transparent hit graphic that stretches the row and has raycastTarget true, or make the row background the target graphic.
   - Implement ISubmitHandler: Submit toggles the value and invokes the callback.
   - Clicking the label or anywhere in the row toggles the value.
   - Show a focus outline around the whole row.
   - Keep the animated knob and the current colors.

3. Add focus management per screen.
   - HushScreen gets a focus list, a default focus, and FocusDefault(), ClearFocus(), and RestoreFocus().
   - HushGame.Show calls FocusDefault after the screen is active, using EventSystem.current.SetSelectedGameObject.
   - Opening Settings or HowTo stores the previous selection and restores it on close.
   - Configure explicit navigation for vertical menus and horizontal button rows. Do not rely on automatic navigation for irregular layouts.
   - No screen may open with no selected object.

4. Fix the EventSystem.
   - HushUI.EnsureEventSystem must replace a legacy StandaloneInputModule with InputSystemUIInputModule.
   - Assign the default UI actions, set move repeat delay to 0.35 and repeat rate to 0.08.
   - Guarantee exactly one EventSystem in the scene.

5. Fix HushInput routing.
   - TapDown: only the pointer branch checks IsPointerOverUi. Keyboard and gamepad taps must work even when the cursor rests over UI.
   - PauseDown: gamepad Start, P key, and the HUD button. Escape is not Pause.
   - BackDown: Escape and gamepad East/B.
   - Keep ConfirmDown for menus if it is used, but do not let gameplay taps fall through UI.
   - Do not add legacy Input calls.

6. Enforce touch targets.
   - Primary 64, secondary 56, icon 56, minimum 48.
   - Volume plus/minus become 56x56.
   - Toggle row is 64 high and fully clickable.
   - Increase spacing around small icon buttons so they cannot be mis-tapped.

7. Cover every screen with Back.
   - Title: Back no-op or quit prompt.
   - District select: Back to title.
   - How-to: Back to its return screen.
   - Settings: Back to its return screen.
   - Pause: Back to gameplay only through Resume; Back on Pause resumes.
   - Results: Back to district select.
   - Ending: Back to title.
   - Ensure Escape in Settings cannot resume gameplay underneath.

8. Make reduced motion complete.
   - Add HushMotion.Reduced that reads HushProgress.Data.reducedMotion.
   - Gate the HUD ring rotation and pulse, countdown pop, star reveal (instant fill), life flash (static heart, no flash), feedback rise/pop (fade only), title equalizer motion, camera shake, and debris particle count.
   - Keep timing feedback readable without motion.
   - Add a settings description that states what the toggle changes.

9. Fix typography and contrast.
   - If Oswald Bold SDF is not in the project, choose one: bundle an OFL-licensed font and generate the TMP asset, or remove the dead lookup and use the default TMP font deliberately. Do not leave a silent fallback.
   - Add a startup warning if the heading font asset is missing.
   - Raise body text to 16-18 px. Keep 12-13 px only for non-essential caps at 4.5:1 or better.
   - Add a stronger muted color (about #A7B2C7) for small text. Do not use 0.55-0.75 alpha on 12-13 px text.
   - Verify contrast with a script or manual calculation; include the ratios in the report.

10. Add non-color cues.
   - Amber versus cyan block state must have a shape, icon, or pattern cue, not color alone.
   - Timing grades need an icon or word, not only color.
   - The HUD ring and life state must remain distinguishable in grayscale.

11. Optional, only if cheap: add a text-size setting (1.0, 1.15, 1.3) stored in the save and applied at screen build. Do not attempt a full localization system.

ACCEPTANCE
- Keyboard-only: title -> district select -> run -> pause -> settings -> back -> results -> next -> ending.
- Gamepad-only: same path with D-pad/stick, South to submit, East/B to back, Start to pause.
- Focus is always visible and never lost.
- Mouse/touch: toggles respond anywhere on the row; all targets at least 48x48.
- Space/Enter/gamepad South works while the cursor rests over UI.
- Escape toggles pause once and never leaks through Settings.
- Reduced motion stops the listed animations.
- Small text contrast is at least 4.5:1; report the measured values.
- Block state and timing grade have a non-color cue.
- No compile errors, no new warnings.

OUT OF SCOPE
Scoring, accuracy, star thresholds, sway, latency calibration, audio mix, shader stripping, player settings.

STOP CONDITION
Stop after the Phase 3 acceptance evidence. Report files changed, keyboard/gamepad paths, contrast ratios, screenshots, and remaining risks. Do not start Phase 4.
````

---

## Phase 4 - Gameplay Tuning and Progression

**Gate before:** Phase 3 is green.
**Gate after:** a clean all-Good run earns at most 2 stars; an all-Perfect run earns 3; a miss can never display 100% accuracy; calibration changes the felt offset; the ending shows real totals.

````text
PHASE 4 - GAMEPLAY TUNING AND PROGRESSION

You are the senior game designer and Unity engineer for Hush Blocks at D:\Game Project\Hush Block. Phase 3 is complete: input and accessibility are solid. Read FULLGAME_AUDIT.md sections 4 and 6.3 before editing.

GOAL
Make timing mastery legible: honest accuracy, meaningful star thresholds, a centering curve that agrees with the beat, latency calibration, complete save totals, and a first-run tutorial. Do not change the UI layout or the audio mix in this phase.

TASKS
1. Fix accuracy.
   - attempts = floors + misses.
   - accuracy = (perfects * 1.0 + goods * 0.7 + offbeats * 0.3) / attempts.
   - A run with any miss must not display 100%.
   - Show Perfect, Good, Offbeat, and Miss counts on the results screen, plus accuracy.
   - Keep the value in RunResult.Accuracy and update the results layout only as much as needed; no full redesign.

2. Retune star thresholds from the score budget.
   - The audit's all-Good and all-Perfect estimates are the baseline. The weighted accuracy bonus lowers all-Good scores by about 180 points.
   - Start from these targets and adjust after a score audit:
     Whisper Row: 2 stars about 4,800, 3 stars about 5,800.
     Echo Terrace: 2 stars about 7,200, 3 stars about 8,800.
     Resonant Apex: 2 stars about 10,200, 3 stars about 12,500.
   - The gate is behavioral: all-Good with full lives earns 2 stars, all-Good with one life lost still earns 2 stars, all-Perfect earns 3 stars. If the numbers fail that, move the thresholds, not the formula.
   - Add a small editor or runtime score audit that prints all-Good and all-Perfect finals against the thresholds.

3. Resolve the beat/centering conflict.
   - Replace the 8-beat triangle with a 1-beat sway that dwells at center around each beat and moves to the edge at the half-beat.
   - Suggested curve: phase = Repeat(beats, 1); t = phase <= 0.5 ? phase : 1 - phase; dwell = 0.18 beats; travel = Clamp01((t - dwell) / (0.5 - dwell)); eased = SmoothStep(travel); x = range * eased * (phase <= 0.5 ? 1 : -1).
   - The dwell must be at least as long as the Perfect window at 128 BPM. At 128 BPM, 0.18 beats is about 84 ms, which covers the 85 ms Perfect window.
   - Define centered from the same curve and timing phase, not an absolute 0.09 world-space tolerance. A Perfect tap on the beat must be centered and preserve full width.
   - Keep the width loss from overlap. Show a center line or landing preview so the player can read the target.
   - Tune range and growth after the curve works. Document the resulting center window in milliseconds at 96, 112, and 128 BPM.

4. Improve timing feedback.
   - Record signed early/late offset at tap time.
   - Show "EARLY -32 ms" or "LATE +18 ms" on the HUD feedback and in the results breakdown.
   - Add a grade icon: Perfect star, Good circle, Offbeat triangle, Miss X.
   - Either shorten the 0.085 s drop animation to about 0.05 s or grade the predicted landing time. Do not leave an 85 ms visual delay unexplained.
   - Add a short practice hint on the first run only.

5. Add latency calibration.
   - New CalibrationScreen: a metronome click, 8 taps, median offset in milliseconds, clamped to +/-200 ms.
   - Store timingOffsetMs in HushSaveData and persist it.
   - Apply it in StackGame timing evaluation and in click scheduling.
   - Add a Settings entry "TIMING CALIBRATION" with the current offset and a Reset button.
   - Show a confirmation when the offset is applied.

6. Complete save totals.
   - HushProgress.SubmitRun must update totalFloors and totalPerfects in addition to best scores, stars, unlocks, completed, and runs.
   - Do not double-count a run; one SubmitRun call per completed or failed run.
   - Replace hardcoded 9 stars and 3 districts with DistrictCatalog.Count * 3 and DistrictCatalog.Count.
   - Title: label the max single-district value BEST RUN. If you show a sum, label it TOTAL BEST.
   - Ending: show total stars, total layers, total perfects, and districts cleared from real data.
   - The title and ending must update after a run without restarting the game.

7. Add first-run onboarding.
   - If tutorialSeen is false, the first PLAY opens How To with a BEGIN button instead of starting the run immediately.
   - MarkTutorialSeen when the first run starts.
   - Add "REPLAY TUTORIAL" to Settings.
   - Do not block the player again after the first run.

8. Improve results presentation.
   - Show outcome, score, stars, accuracy, Perfect/Good/Offbeat/Miss, max combo, lives left, and delta versus best.
   - Show the score breakdown: base, life bonus, accuracy bonus, combo bonus.
   - Show the next unlock or "NEW DISTRICT UNLOCKED" when applicable.
   - Keep the existing panel size; use the stats grid from Phase 2.

9. Verify.
   - Script or simulate an all-Good run and an all-Perfect run; record finals and stars.
   - Simulate a run with misses; confirm accuracy is below 100%.
   - Run calibration twice; confirm the offset applies and persists after restart.
   - Complete the campaign; confirm the ending totals are non-zero and match the save.
   - Confirm the tutorial appears only on the first run.

ACCEPTANCE
- All-Good full-lives run: at most 2 stars.
- All-Good one-life-lost run: 2 stars (or explicitly documented if a district differs).
- All-Perfect run: 3 stars.
- Misses reduce accuracy; 100% is impossible with a miss.
- A Perfect tap on the beat is centered and preserves width.
- Calibration stores an offset and changes the felt timing.
- Total floors and total perfects are non-zero at the ending.
- Title and ending use real counts, not hardcoded 9/3.
- Tutorial runs once, can be replayed, and does not block later runs.

OUT OF SCOPE
Layout redesign, focus navigation, audio mix, shader stripping, player settings, build packaging.

STOP CONDITION
Stop after the Phase 4 acceptance evidence. Report the score audit table, calibration results, save totals, screenshots, and remaining risks. Do not start Phase 5.
````

---

## Phase 5 - Audio Lifecycle and Mix

**Gate before:** Phase 4 is green.
**Gate after:** menu, district, clicks, pause, results, and title transitions are correct; the click track does not drift; no duplicate music or listeners.

````text
PHASE 5 - AUDIO LIFECYCLE AND MIX

You are the senior Unity audio engineer for Hush Blocks at D:\Game Project\Hush Block. Phase 4 is complete: timing, calibration, and progression are correct. Read FULLGAME_AUDIT.md section P0-5 and the HushAudio source before editing.

GOAL
Make the procedural soundtrack feel intentional: clean menu/district transitions, a stable click track, correct pause and results behavior, live volume changes, and no duplicate sources. Do not change the UI layout or gameplay rules in this phase.

TASKS
1. Harden HushAudio lifetime.
   - Keep one persistent instance with DontDestroyOnLoad and a duplicate guard.
   - OnDestroy clears the static instance and preloaded flag.
   - Create and Preload are idempotent.
   - If a second HushAudio is created, destroy it and log once.

2. Add clean music transitions.
   - Menu to district: crossfade over 0.6-1.2 seconds.
   - District to results: duck to about 0.35, stop clicks, keep a quiet bed or fade out.
   - Results to title: fade gameplay out, fade menu in.
   - Pause: pause music and clicks; resume from the same DSP position.
   - Do not stack two music sources at full volume.

3. Verify and fix the click track.
   - Schedule clicks on the DSP clock, not on frame time.
   - Accent the downbeat, keep the subdivision musical, and respect beatClicks.
   - Run a 3-minute district loop and confirm drift is under 10 ms. If it drifts, reschedule from the song start instead of accumulating.
   - The click volume must follow the SFX or music setting as designed; document which.
   - Calibration offset from Phase 4 must shift the click schedule consistently with timing evaluation.

4. Mix and SFX.
   - Master, music, and SFX volumes apply live to every source.
   - Add UI hover, click, back, star, level clear, game over, and heart-lost sounds where missing.
   - Use the existing SFX pool; add pitch variation only within a small range.
   - Do not clip; check the sum of music + click + SFX at default volumes.

5. Focus and lifecycle behavior.
   - OnApplicationFocus(false) and OnApplicationPause(true): pause gameplay and audio, freeze the beat clock, and show the pause screen if a run is active.
   - On regain: do not auto-resume; wait for the player. Rebase the DSP clock.
   - If the platform kills audio on focus loss, recover without restarting the track from zero.
   - Optional: "Mute when unfocused" setting; if added, persist it.

6. Audio device changes.
   - Handle AudioSettings.OnAudioConfigurationChanged by re-syncing the DSP clock and restarting the click schedule if needed.
   - Do not duplicate sources after a device change.

7. Verify.
   - Title: menu loop, no district bleed.
   - Start run: crossfade to district, click aligns with the beat.
   - Pause: music and clicks stop; resume continues in time.
   - Results: music ducks, clicks stop, UI sounds work.
   - Title return: menu loop restores.
   - 3-minute drift test.
   - Volume sliders and toggles apply live and persist across restart.
   - Focus loss and regain.
   - No duplicate AudioListener or music source warnings.

ACCEPTANCE
- No silent gameplay, no double music, no click drift over 3 minutes.
- Pause freezes audio and resume continues correctly.
- Results duck and title restore are audible and clean.
- Volumes and beat-click toggle apply live and persist.
- Focus loss pauses; focus regain does not auto-resume.
- Exactly one AudioListener.

OUT OF SCOPE
New music composition, UI layout, gameplay scoring, shader stripping, build packaging.

STOP CONDITION
Stop after the Phase 5 acceptance evidence. Report files changed, transition timings, drift measurement, focus test, screenshots or logs, and remaining risks. Do not start Phase 6.
````

---

## Phase 6 - Ship Quality and Cleanup

**Gate before:** Phase 5 is green.
**Gate after:** a clean clone opens the FullGame scene, builds a Windows executable, completes all three districts, and the docs match the build.

````text
PHASE 6 - SHIP QUALITY AND CLEANUP

You are the senior Unity engineer and technical director for Hush Blocks at D:\Game Project\Hush Block. Phases 0-5 are complete. Read FULLGAME_AUDIT.md sections 5 and 6 before editing.

GOAL
Produce a clean, reproducible Windows build and accurate documentation. Remove dead flow and duplicated facades. Do not add new gameplay features.

TASKS
1. Configure player settings.
   - Product name: Hush Blocks. Company: a real studio name or the project owner.
   - Version: 1.0.0, matching the UI. Remove the 0.1.0 conflict.
   - Default resolution 1920x1080, windowed, resizable.
   - vSync 1 or a target frame rate of 60/120; do not leave vSync 0 with no target.
   - Run in background off, with the focus pause from Phase 5.
   - Color space linear; confirm the URP asset matches.
   - Input handler: New only if the legacy scene is retired from the ship path; otherwise Both with a documented reason.
   - Scripting backend: choose Mono or IL2CPP, document the choice, and make the build reproducible.

2. Eliminate shader stripping risk.
   - Add Universal Render Pipeline/Lit, Universal Render Pipeline/Unlit, Sprites/Default, and the TMP shader to Always Included Shaders, or serialize materials that reference them.
   - Verify a player build has no pink materials and no missing shader errors.
   - Add an editor menu or build-time check that fails the build if a required shader is missing.

3. Profile and tune performance.
   - Target 60 fps at 1920x1080 on the development machine, with headroom for 120 Hz.
   - Remove per-frame Find calls, allocations, and unnecessary Canvas rebuilds.
   - Pool debris and block objects if allocation shows up in the profiler.
   - Warm TMP and the procedural sprites during boot.
   - If screen construction still causes a visible hitch, build title first and gameplay screens on demand.
   - Set Application.targetFrameRate and verify in the profiler.

4. Decide the Double-Beat Block integration.
   - Option A: use HushBlock_DoubleBeat.prefab for the foundation and perfect placements, with a primitive fallback for low-end or mobile.
   - Option B: explicitly retire it from the FullGame ship scope and state that in the README, while keeping the pipeline intact.
   - Do not leave it ambiguous. Implement one option and record the decision.

5. Build and test a clean Windows player.
   - Build to Builds/Windows/HushBlocks.exe.
   - Run from a clean folder, not the Editor.
   - Verify: entry scene, resolution, save path, progression persistence across restart, all three districts, pause/resume, settings, ending, quit, alt-tab, focus loss/regain.
   - Check the player log for missing shaders, fonts, listeners, and NullReferenceExceptions.

6. Run a 30-minute playtest.
   - Complete all three districts at least once.
   - Use mouse, keyboard, gamepad, and touch.
   - Exercise every button, toggle, and back path.
   - Confirm the tutorial, calibration, volume persistence, and reduced motion.
   - Record any bug that blocks a normal player.

7. Remove dead code and duplicate facades.
   - Delete Game.cs if nothing uses it; otherwise reduce it to one forwarding method with an error log.
   - Remove ShowLegacyVictory, unused legacy manager references, and duplicate result construction.
   - Remove unused screens, static wrappers, and dead fields.
   - Remove the legacy manager dependencies from HushGameLoopManager first. Then add HushBlocks.FullGame.asmdef and HushBlocks.FullGame.Editor.asmdef only if the FullGame layer no longer references Assembly-CSharp.
   - Add EditMode tests for scoring, accuracy, star thresholds, save totals, and save migration if time allows.

8. Update documentation.
   - README: entry scene, controls, build command, architecture, current limitations.
   - CLAUDE.md: project overview, FullGame architecture, commands, and the legacy scene status.
   - Add a short FULLGAME_STATUS.md or update FULLGAME_AUDIT.md with fixed/open items.
   - Include the exact Unity version, package versions, and build output path.
   - Do not claim features that are not in the build.

9. Capture final screenshots.
   - Title, district select, gameplay, pause, settings, results, ending.
   - Store them in Docs/Screenshots or deliverables/fullgame.
   - Use the real build, not the Editor, where possible.

ACCEPTANCE
- A clean clone opens HushBlocksFullGame and builds without manual scene setup.
- The Windows player starts at the title, plays all three districts, reaches the ending, and persists progress.
- No missing shader, font, listener, or script errors in the player log.
- No pink materials, no duplicate audio, no stuck input.
- README and CLAUDE.md match the actual entry scene, controls, and build path.
- Screenshots exist for all major screens.
- Dead facades and duplicate flow are removed or explicitly documented.

OUT OF SCOPE
New districts, new songs, new gameplay mechanics, online services, localization, console builds.

STOP CONDITION
Stop after the Phase 6 acceptance evidence. Report the build path, playtest results, player log state, screenshots, docs changed, and any remaining risk. This is the final phase.
````

---

## Appendix A - Phase gate checklist

Use this as the human sign-off sheet.

### Phase 0
- [ ] `HushBlocksFullGame.unity` exists and is the enabled build scene.
- [ ] Play mode: boot progress -> title -> menu music.
- [ ] Windows exe: same flow.
- [ ] One AudioListener, one EventSystem, one stage, one session.
- [ ] `MainGameplay` still opens manually and is disabled in build settings.
- [ ] Console clean.

### Phase 1
- [ ] Fresh save starts at Whisper Row.
- [ ] Spamming PLAY cannot skip a district.
- [ ] Victory -> Next advances exactly once.
- [ ] Retry and District Select work from victory and defeat.
- [ ] Apex victory reaches the ending.
- [ ] Escape toggles pause once; Settings back returns to Pause.
- [ ] Pause during resolution does not lose the result.
- [ ] Mouse-only full loop.

### Phase 2
- [ ] No unintended overlaps at 1920x1080.
- [ ] No unintended overlaps at 1280x720.
- [ ] No unintended overlaps at 2560x1080.
- [ ] No clipping at 1080x1920.
- [ ] Title, pause, settings, how-to, HUD, and results collisions are gone.
- [ ] Screenshots for every screen.

### Phase 3
- [ ] Keyboard-only full loop.
- [ ] Gamepad-only full loop.
- [ ] Focus is always visible.
- [ ] Space/Enter/gamepad South works over UI.
- [ ] Toggle rows are fully clickable.
- [ ] All targets at least 48x48.
- [ ] Small text contrast at least 4.5:1.
- [ ] Reduced motion stops all listed animations.
- [ ] Non-color cues exist for block state and grades.

### Phase 4
- [ ] Accuracy cannot be 100% with a miss.
- [ ] All-Good earns at most 2 stars.
- [ ] All-Perfect earns 3 stars.
- [ ] Perfect tap is centered and preserves width.
- [ ] Calibration persists and changes timing.
- [ ] Total floors and perfects are non-zero at the ending.
- [ ] Tutorial runs once and can be replayed.

### Phase 5
- [ ] No silent gameplay.
- [ ] No double music.
- [ ] Click drift under 10 ms over 3 minutes.
- [ ] Pause freezes and resumes audio.
- [ ] Results duck and title restore.
- [ ] Volumes persist.
- [ ] Focus loss pauses; focus regain does not auto-resume.
- [ ] One AudioListener.

### Phase 6
- [ ] Clean clone builds without scene setup.
- [ ] Windows player completes all three districts.
- [ ] Progress persists across restart.
- [ ] No missing shader, font, listener, or script errors.
- [ ] No pink materials.
- [ ] README and CLAUDE.md match the build.
- [ ] Screenshots exist for all major screens.

---

## Appendix B - Suggested commits

```
phase-0: bootstrap FullGame scene and build pipeline
phase-1: fix campaign flow, results, pause, and back routing
phase-2: add layout tokens and rebuild responsive screens
phase-3: add selectable focus, input parity, and accessibility
phase-4: fix accuracy, stars, centering, calibration, and save totals
phase-5: harden audio lifecycle, crossfades, and click sync
phase-6: ship Windows build, shader setup, docs, and cleanup
```

Keep one commit per phase, or one commit per task within a phase if the phase is large. Do not mix phase work in a single commit.

---

## Appendix C - Handoff template

When a phase is complete, hand off with:

```
PHASE N COMPLETE

Gate: pass / fail
Build: path or not run
Play mode: what was exercised
Console: errors / warnings
Screenshots: paths
Open risks: ...
Next phase: ready / blocked by ...
```

If the gate fails, do not start the next phase. Fix the failing items in the same phase and re-run the gate.
