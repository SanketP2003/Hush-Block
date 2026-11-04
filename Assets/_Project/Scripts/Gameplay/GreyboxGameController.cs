using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace HushBlocks.Gameplay
{
    /// <summary>Phase 1 interaction controller for selecting and muting resonance blocks.</summary>
    [RequireComponent(typeof(RhythmManager))]
    public sealed class GreyboxGameController : MonoBehaviour
    {
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private LayerMask blockLayer = ~0;
        [SerializeField] private float interactionDepth = 30f;
        [SerializeField] private string statusPrefix = "VELVET SEAL";

        private ResonanceBlock selectedBlock;
        private readonly List<ResonanceBlock> noisyBlocks = new();
        private RhythmManager rhythmManager;
        private float chainStartTime;
        private int silenceCount;
        private string status = "Tap a cyan block to stabilize it.";
        private float pulseUntil;
        private bool wasPressed;

        public string Status => status;
        public ResonanceBlock SelectedBlock => selectedBlock;
        public int SilenceCount => silenceCount;
        public event Action<ResonanceBlock> OnVelvetSealApplied;

        private void Awake()
        {
            rhythmManager = GetComponent<RhythmManager>();
            if (gameplayCamera == null) gameplayCamera = Camera.main;
        }

        private void OnEnable()
        {
            noisyBlocks.Clear();
            foreach (ResonanceBlock block in FindObjectsByType<ResonanceBlock>())
            {
                if (!block.IsStabilized && !block.IsFalling) noisyBlocks.Add(block);
            }
        }

        private void Update()
        {
            bool pressed = Mouse.current != null && Mouse.current.leftButton.isPressed;
            if (pressed && !wasPressed) TryInteract(Mouse.current.position.ReadValue());
            wasPressed = pressed;

            if (Touchscreen.current != null)
            {
                foreach (TouchControl touch in Touchscreen.current.touches)
                {
                    if (touch.press.wasPressedThisFrame)
                    {
                        TryInteract(touch.position.ReadValue());
                        break;
                    }
                }
            }

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && selectedBlock != null)
            {
                ApplySeal(selectedBlock);
            }

            if (rhythmManager != null && rhythmManager.DifficultyProfile != null &&
                Time.time > chainStartTime + rhythmManager.DifficultyProfile.ChainDecaySeconds && silenceCount > 0)
            {
                silenceCount = 0;
            }
        }

        private void TryInteract(Vector2 screenPosition)
        {
            if (gameplayCamera == null) return;
            Ray ray = gameplayCamera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, interactionDepth, blockLayer))
            {
                ResonanceBlock block = hit.collider.GetComponentInParent<ResonanceBlock>();
                if (block != null)
                {
                    selectedBlock = block;
                    ApplySeal(block);
                }
            }
        }

        private void ApplySeal(ResonanceBlock block)
        {
            if (block == null) return;

            if (block.IsStabilized || block.IsFalling)
            {
                status = "Already stabilized — silence holds.";
                pulseUntil = Time.time + 0.2f;
                return;
            }

            if (rhythmManager != null)
            {
                double inputTime = rhythmManager.TimeSinceStart;
                TimingResult result = rhythmManager.EvaluateInput(inputTime, out long beatIndex);

                switch (result)
                {
                    case TimingResult.Accurate:
                        // Perfect timing applies a full mute and harmonic pulse.
                        block.RequestStabilize(fromInput: true);
                        HushBlock hushBlock = block.GetComponent<HushBlock>();
                        if (hushBlock != null) hushBlock.MuteBlock(isPerfect: true);
                        OnVelvetSealApplied?.Invoke(block);
                        noisyBlocks.Remove(block);
                        silenceCount++;
                        chainStartTime = Time.time;
                        status = $"{statusPrefix}: block stabilized on beat {beatIndex}. Chain x{silenceCount}.";
                        pulseUntil = Time.time + 0.45f;
                        break;
                    case TimingResult.Good:
                        block.RequestStabilize(fromInput: true);
                        HushBlock goodHushBlock = block.GetComponent<HushBlock>();
                        if (goodHushBlock != null) goodHushBlock.MuteBlock(isPerfect: false);
                        OnVelvetSealApplied?.Invoke(block);
                        noisyBlocks.Remove(block);
                        silenceCount++;
                        chainStartTime = Time.time;
                        status = $"{statusPrefix}: block muted on beat {beatIndex}. Chain x{silenceCount}.";
                        pulseUntil = Time.time + 0.35f;
                        break;
                    case TimingResult.Early:
                    case TimingResult.Late:
                        block.RequestFail();
                        HushBlock mistimedHushBlock = block.GetComponent<HushBlock>();
                        if (mistimedHushBlock != null) mistimedHushBlock.TriggerAntiGravity();
                        status = result == TimingResult.Early ? "Too early — wait for the beat." : "Too late — wait for the next beat.";
                        pulseUntil = Time.time + 0.3f;
                        break;
                    default:
                        // Missed the beat — the block begins to destabilize.
                        block.RequestFail();
                        HushBlock missedHushBlock = block.GetComponent<HushBlock>();
                        if (missedHushBlock != null) missedHushBlock.TriggerAntiGravity();
                        status = "Missed the beat — silence broken.";
                        pulseUntil = Time.time + 0.4f;
                        break;
                }
            }
            else
            {
                // Rhythm system unavailable: fallback behavior.
                block.RequestStabilize(fromInput: true);
                OnVelvetSealApplied?.Invoke(block);
                noisyBlocks.Remove(block);
                silenceCount++;
                chainStartTime = Time.time;
                status = $"{statusPrefix}: block stabilized.";
                pulseUntil = Time.time + 0.45f;
            }
        }

        private void OnGUI()
        {
            GUIStyle title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.71f, 0.28f) } };
            GUIStyle body = new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.white } };
            GUI.Label(new Rect(28, 24, 700, 40), "HUSH BLOCKS // STATIC TERRACES", title);
            GUI.Label(new Rect(30, 72, 760, 30), "Velvet Seal  •  Click a cyan block or press Space", body);
            GUI.Label(new Rect(30, 112, 760, 30), status, body);
            GUI.Label(new Rect(30, 152, 760, 30), $"Silence chain: {silenceCount}  |  Noisy remaining: {noisyBlocks.Count}", body);
            if (rhythmManager != null)
            {
                GUI.Label(new Rect(30, 192, 760, 30), $"Tempo: {rhythmManager.DifficultyProfile?.Bpm ?? 0f:F0} BPM  |  Next beat in: {rhythmManager.SecondsUntilNextBeat():F2}s", body);
            }
            if (Time.time < pulseUntil)
            {
                GUI.Label(new Rect(30, 232, 760, 30), "SILENCE POCKET FORMED", title);
            }
        }
    }
}