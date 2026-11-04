using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HushBlocks.FullGame
{
    public enum TimingGrade
    {
        Perfect = 0,
        Good = 1,
        Offbeat = 2,
        Miss = 3
    }

    public struct PlacementFeedback
    {
        public TimingGrade Grade;
        public int Points;
        public bool Centered;
        public int Floor;
        public int Combo;
        public int Multiplier;
        public Vector3 WorldPosition;
        public bool Attuned;
    }

    public struct RunResult
    {
        public int DistrictIndex;
        public bool Success;
        public int Score;
        public int Floors;
        public int Target;
        public int Perfects;
        public int Goods;
        public int Offbeats;
        public int Misses;
        public int MaxCombo;
        public int Lives;
        public int Stars;
        public int Best;
        public bool NewBest;
        public float Accuracy;
        public float Duration;
    }

    /// <summary>
    /// One district run. A block slides above the tower on a beat-synced
    /// triangle wave; tapping drops it. Timing controls score and resonance,
    /// overlap controls how much width survives, and three misses end the run.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StackGame : MonoBehaviour
    {
        public event Action<PlacementFeedback> Placed;
        public event Action<int> LifeLost;
        public event Action<RunResult> Finished;

        private struct Layer
        {
            public float Center;
            public float Width;
            public float TopY;
            public BlockView View;
        }

        private const float BlockHeight = 0.55f;
        private const float BlockDepth = 1.9f;
        private const float BaseBottom = 0.18f;
        private const float HoverHeight = 0.95f;
        private const int StartingLives = 3;
        private const int IntroBeats = 4;

        private readonly List<Layer> layers = new();

        private HushStage stage;
        private DistrictDefinition district;
        private Layer top;
        private BlockView moving;
        private GameObject guide;
        private GameObject beam;
        private Renderer guideRenderer;
        private Renderer beamRenderer;
        private MaterialPropertyBlock guideBlock;

        private double startDsp;
        private double pausedDsp;
        private double pauseStartDsp;
        private bool paused;
        private float beatDuration;
        private int lastBeatIndex = -1;
        private bool accepting;
        private bool dropping;
        private float dropProgress;
        private float dropStartY;
        private float dropTargetY;
        private float dropX;
        private float dropBeatOffset;
        private int lastIntroBeat = -1;

        private int score;
        private int lives = StartingLives;
        private int floors;
        private int combo;
        private int maxCombo;
        private int perfects;
        private int goods;
        private int offbeats;
        private int misses;
        private float guideAlign;

        public DistrictDefinition District => district;
        public int DistrictIndex { get; private set; }
        public int Score => score;
        public int Lives => lives;
        public int Floors => floors;
        public int TargetFloors => district != null ? district.TargetFloors : 1;
        public int Combo => combo;
        public int MaxCombo => maxCombo;
        public int Multiplier => 1 + Mathf.Min(combo, 20) / 5;
        public int Perfects => perfects;
        public int Goods => goods;
        public int Offbeats => offbeats;
        public int Misses => misses;
        public bool IsRunning { get; private set; }
        public bool IsFinished { get; private set; }
        public bool IsResolving { get; private set; }
        public bool IsPaused => paused;
        public bool IntroActive => BeatSeconds < IntroBeats * beatDuration;
        public float TowerTopY => top.TopY;
        public float Accuracy => floors <= 0 ? 0f : Mathf.Clamp01((perfects + goods) / (float)floors);
        public float BeatDuration => beatDuration;
        public int BeatIndex => beatDuration <= 0f ? 0 : Mathf.FloorToInt(BeatSeconds / beatDuration);
        public float RunDuration => BeatSeconds;

        public float BeatSeconds
        {
            get
            {
                double now = AudioSettings.dspTime;
                double elapsed = now - startDsp - pausedDsp;
                if (paused) elapsed -= now - pauseStartDsp;
                return (float)Math.Max(0d, elapsed);
            }
        }

        public float BeatPulse
        {
            get
            {
                if (beatDuration <= 0f) return 0f;
                float phase = Mathf.Repeat(BeatSeconds / beatDuration, 1f);
                return Mathf.Clamp01(1f - phase * 3.2f);
            }
        }

        public string IntroLabel
        {
            get
            {
                if (district == null) return string.Empty;
                float beats = BeatSeconds / beatDuration;
                if (beats >= IntroBeats && beats < IntroBeats + 0.9f) return "GO";
                if (beats >= IntroBeats) return string.Empty;
                if (beats < 1f) return "LISTEN";
                return Mathf.CeilToInt(IntroBeats - beats + 1f).ToString();
            }
        }

        public string CurrentHint
        {
            get
            {
                if (district == null) return string.Empty;

                // Priority 1: Howler threat alert
                if (stage != null && stage.Howler != null && stage.Howler.activeSelf && stage.HowlerAI != null)
                {
                    if (stage.HowlerAI.CurrentState == Gameplay.FeedbackHowlerAI.HowlerState.PreparingShriek)
                    {
                        return "⚠️ HOWLER ATTACK! TAP OFF-BEAT TO SHATTER!";
                    }
                    if (stage.HowlerAI.CurrentState == Gameplay.FeedbackHowlerAI.HowlerState.ProwlingLedges)
                    {
                        return "FEEDBACK HOWLER DETECTED ON LEDGE";
                    }
                }

                // Priority 2: Critical heart warning
                if (lives == 1) return "CRITICAL: ONE HEART LEFT — BREATHE & FOCUS";

                // Priority 3: Combo streak recognition
                if (combo >= 5) return $"HARMONIC HUSH ACTIVE • {combo}× SILENCE STREAK!";

                // Priority 4: Progressive District Hints
                if (floors == 0) return district.Tips[0];
                if (floors == 1) return district.Tips[1];
                if (floors == 2) return district.Tips[2];
                if (floors == 3 && district.Tips.Length > 3) return district.Tips[3];

                if (floors >= TargetFloors - 2) return "SUMMIT NEAR — SECURE FINAL LAYERS!";

                return string.Empty;
            }
        }

        public void Begin(int districtIndex, HushStage hushStage)
        {
            StopAllCoroutines();
            IsResolving = false;
            stage = hushStage;
            DistrictIndex = Mathf.Clamp(districtIndex, 0, DistrictCatalog.Count - 1);
            district = DistrictCatalog.Get(DistrictIndex);
            stage.SetDistrict(DistrictIndex);
            stage.ClearTower();
            layers.Clear();

            score = 0;
            lives = StartingLives;
            floors = 0;
            combo = 0;
            maxCombo = 0;
            perfects = 0;
            goods = 0;
            offbeats = 0;
            misses = 0;
            paused = false;
            pausedDsp = 0d;
            IsRunning = true;
            IsFinished = false;
            accepting = false;
            dropping = false;
            lastBeatIndex = -1;
            lastIntroBeat = -1;
            beatDuration = HushAudio.DistrictBeatDuration(DistrictIndex);

            float baseWidth = district.StartWidth + 0.28f;
            var foundation = BlockView.Create(stage.BlockRoot, 0f, BaseBottom, baseWidth, BlockHeight, BlockDepth, true);
            foundation.Attune(true);
            layers.Add(new Layer { Center = 0f, Width = baseWidth, TopY = BaseBottom + BlockHeight, View = foundation });
            top = layers[layers.Count - 1];

            BuildGuide();
            startDsp = HushAudio.PlayDistrict(DistrictIndex);
            SpawnMovingBlock();

            stage.ResetCamera(top.TopY, 0f);

            if (stage.NoaController != null)
            {
                stage.NoaController.ResetToBase(top.Center, top.TopY, top.Width);
                stage.NoaController.SetBpm(district.Bpm);
            }

            if (stage.Howler != null)
            {
                stage.Howler.SetActive(false);
            }
        }

        public void End()
        {
            StopAllCoroutines();
            IsRunning = false;
            IsFinished = true;
            IsResolving = false;
        }

        public void SetPaused(bool value)
        {
            if (paused == value) return;
            if (value && (IsResolving || IsFinished)) return;
            paused = value;
            if (value)
            {
                pauseStartDsp = AudioSettings.dspTime;
            }
            else
            {
                pausedDsp += AudioSettings.dspTime - pauseStartDsp;
            }
            HushAudio.PauseGameplay(value);
        }

        private void Update()
        {
            if (!IsRunning || IsFinished || paused) return;

            float beats = BeatSeconds / beatDuration;
            int beatIndex = Mathf.FloorToInt(beats);
            if (beatIndex != lastBeatIndex)
            {
                lastBeatIndex = beatIndex;
                OnBeat(beatIndex);
            }

            if (!accepting && beats >= IntroBeats)
            {
                accepting = true;
            }

            if (!dropping)
            {
                UpdateMovingBlock(beats);

                // Priority Howler Counter-Strike: When the Howler is preparing a shriek,
                // taps are dedicated to countering the beast without accidentally dropping the block!
                bool howlerAttacking = stage.Howler != null && stage.Howler.activeSelf &&
                                       stage.HowlerAI != null && stage.HowlerAI.CurrentState == Gameplay.FeedbackHowlerAI.HowlerState.PreparingShriek;

                if (HushInput.TapDown())
                {
                    if (howlerAttacking)
                    {
                        if (stage.HowlerAI.TryCounterStrike())
                        {
                            score += 500;
                            combo++;
                            maxCombo = Mathf.Max(maxCombo, combo);
                            HushAudio.Sfx(HushAudio.SfxId.Star, 0.9f);
                            stage.NoaController?.TriggerTrowelStrike(true);
                            stage.Burst(stage.Howler.transform.position, HushPalette.Amber, 24, 2.2f);
                            return;
                        }
                        else
                        {
                            // Struck Howler off-timing: Noa stumbles, but block does not drop!
                            HushAudio.Sfx(HushAudio.SfxId.Offbeat, 0.7f);
                            stage.NoaController?.TriggerStumble();
                            return;
                        }
                    }

                    if (accepting && moving != null)
                    {
                        stage.NoaController?.TriggerTrowelStrike(false);
                        Drop();
                    }
                }
            }
            else
            {
                UpdateDrop();
            }

            UpdateGuideAlignment();
        }

        private void OnBeat(int beatIndex)
        {
            if (beatIndex < IntroBeats)
            {
                if (beatIndex >= 1 && beatIndex != lastIntroBeat)
                {
                    lastIntroBeat = beatIndex;
                    HushAudio.Sfx(HushAudio.SfxId.Countdown, 0.7f);
                    stage.Pulse(0.5f, false);
                }
                return;
            }

            if (beatIndex == IntroBeats)
            {
                HushAudio.Sfx(HushAudio.SfxId.Go, 0.8f);
            }

            bool strong = beatIndex % 4 == 0;
            stage.Pulse(strong ? 1.15f : 0.62f, strong);
            if (DistrictIndex == 2 && strong && !HushProgress.Data.reducedMotion)
            {
                stage.CameraRig.Shake(0.07f);
            }
        }

        // ------------------------------------------------------------------ movement

        private void SpawnMovingBlock()
        {
            if (top.View == null) return;
            float bottom = top.TopY + HoverHeight;
            moving = BlockView.Create(stage.BlockRoot, top.Center, bottom, top.Width, BlockHeight, BlockDepth, false, true);
            moving.SetMoving(true);
            dropping = false;
            accepting = BeatSeconds >= IntroBeats * beatDuration;
        }

        private void UpdateMovingBlock(float beats)
        {
            if (moving == null) return;

            // Harmonically locked sway anchored to the active tower top center
            float range = district.SwayRange + Mathf.Min(0.7f, floors * district.SwayGrowth);

            // Sine pendulum: x == top.Center on every integer beat (1.0, 2.0, 3.0, 4.0...)
            // x == top.Center +- range on every off-beat half-beat (0.5, 1.5, 2.5...)
            float x = top.Center + range * Mathf.Sin(beats * Mathf.PI);

            float bob = Mathf.Sin(Time.unscaledTime * 2.2f) * 0.03f;
            moving.MoveTo(x, top.TopY + HoverHeight + BlockHeight * 0.5f + bob);
            moving.SetMoving(true);

            // Landing reticle stays stationary on the uppermost stabilized block
            if (guide != null)
            {
                guide.transform.position = new Vector3(top.Center, top.TopY + 0.015f, 0f);
                guide.transform.localScale = new Vector3(top.Width, 0.035f, BlockDepth * 1.02f);
            }

            // Drop plumb laser hangs vertically beneath the moving block
            if (beam != null)
            {
                float height = Mathf.Max(0.1f, HoverHeight + bob);
                beam.transform.position = new Vector3(x, top.TopY + height * 0.5f, 0f);
                beam.transform.localScale = new Vector3(0.04f, height, 0.04f);
            }
        }

        private void UpdateGuideAlignment()
        {
            if (guideRenderer == null || moving == null) return;
            float dist = Mathf.Abs(moving.CenterX - top.Center);
            bool inSweetSpot = dist <= (top.Width * 0.28f);
            float targetAlign = inSweetSpot ? 1f : 0f;
            guideAlign = Mathf.MoveTowards(guideAlign, targetAlign, Time.deltaTime * 12f);

            float pulse = BeatPulse;
            Color color = Color.Lerp(HushPalette.Cyan, HushPalette.Amber, guideAlign);
            float emissionBoost = Mathf.Lerp(0.85f, 3.6f, guideAlign) + pulse * 1.6f;

            guideBlock.SetColor("_BaseColor", color);
            guideBlock.SetColor("_EmissionColor", color * emissionBoost);
            guideRenderer.SetPropertyBlock(guideBlock);
            if (beamRenderer != null) beamRenderer.SetPropertyBlock(guideBlock);
        }

        private void BuildGuide()
        {
            guide = GameObject.CreatePrimitive(PrimitiveType.Cube);
            guide.name = "Landing Guide";
            Destroy(guide.GetComponent<Collider>());
            guide.transform.SetParent(stage.BlockRoot, false);
            guide.transform.position = new Vector3(top.Center, top.TopY + 0.015f, 0f);
            guide.transform.localScale = new Vector3(top.Width, 0.035f, BlockDepth * 1.02f);
            guideRenderer = guide.GetComponent<MeshRenderer>();
            guideRenderer.sharedMaterial = HushMaterials.Band;
            guideRenderer.shadowCastingMode = ShadowCastingMode.Off;
            guideRenderer.receiveShadows = false;

            guideBlock = new MaterialPropertyBlock();
            guideBlock.SetColor("_BaseColor", HushPalette.Cyan);
            guideBlock.SetColor("_EmissionColor", HushPalette.Cyan * 0.7f);
            guideRenderer.SetPropertyBlock(guideBlock);

            beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "Drop Beam";
            Destroy(beam.GetComponent<Collider>());
            beam.transform.SetParent(stage.BlockRoot, false);
            beamRenderer = beam.GetComponent<MeshRenderer>();
            beamRenderer.sharedMaterial = HushMaterials.Band;
            beamRenderer.shadowCastingMode = ShadowCastingMode.Off;
            beamRenderer.receiveShadows = false;
            beamRenderer.SetPropertyBlock(guideBlock);
        }

        // ------------------------------------------------------------------ drop

        private void Drop()
        {
            if (moving == null) return;
            dropping = true;
            accepting = false;
            dropProgress = 0f;
            dropX = moving.CenterX;
            dropStartY = moving.transform.position.y;
            dropTargetY = top.TopY + BlockHeight * 0.5f;

            float beats = BeatSeconds / beatDuration;
            float phase = Mathf.Repeat(beats, 1f);
            dropBeatOffset = Mathf.Min(phase, 1f - phase) * beatDuration;
            if (guide != null) guide.SetActive(false);
            if (beam != null) beam.SetActive(false);
        }

        private void UpdateDrop()
        {
            if (moving == null)
            {
                dropping = false;
                return;
            }

            dropProgress += Time.deltaTime / 0.085f;
            float t = Mathf.Clamp01(dropProgress);
            float eased = t * t;
            float y = Mathf.Lerp(dropStartY, dropTargetY, eased);

            // Acoustic Mortar Snapping: Perfect timing smoothly aligns into true center
            float dist = Mathf.Abs(dropX - top.Center);
            bool willCenter = (dropBeatOffset <= 0.085f && dist <= top.Width * 0.35f)
                           || (dist <= 0.20f)
                           || (DistrictIndex == 0 && floors < 4 && dropBeatOffset <= 0.165f && dist <= top.Width * 0.45f);

            float currentX = Mathf.Lerp(dropX, willCenter ? top.Center : dropX, eased);
            moving.MoveTo(currentX, y);

            if (t >= 1f)
            {
                dropping = false;
                ResolveDrop(willCenter ? top.Center : dropX, dropBeatOffset);
            }
        }

        private void ResolveDrop(float x, float beatOffset)
        {
            if (moving == null) return;

            float centerDist = Mathf.Abs(x - top.Center);
            bool isPerfectBeat = beatOffset <= 0.085f;
            bool isGoodBeat = beatOffset <= 0.165f;

            TimingGrade grade;
            if (isPerfectBeat) grade = TimingGrade.Perfect;
            else if (isGoodBeat) grade = TimingGrade.Good;
            else grade = TimingGrade.Offbeat;

            // Generous rhythmic attunement: Perfect beat snaps to full width!
            bool centered = (isPerfectBeat && centerDist <= top.Width * 0.35f)
                         || (centerDist <= 0.20f)
                         || (DistrictIndex == 0 && floors < 4 && isGoodBeat && centerDist <= top.Width * 0.45f);

            float topLeft = top.Center - top.Width * 0.5f;
            float topRight = top.Center + top.Width * 0.5f;
            float moveLeft = x - top.Width * 0.5f;
            float moveRight = x + top.Width * 0.5f;
            float left = Mathf.Max(topLeft, moveLeft);
            float right = Mathf.Min(topRight, moveRight);
            float overlap = right - left;

            if (!centered && overlap < 0.4f)
            {
                MissDrop(x);
                return;
            }

            float newWidth = centered ? top.Width : Mathf.Max(0.5f, overlap);
            float newCenter = centered ? top.Center : (left + right) * 0.5f;

            // 5-Combo Harmonic Hush Restoration: restore +0.25m lost width on high combos!
            if (combo >= 4 && grade == TimingGrade.Perfect && newWidth < district.StartWidth)
            {
                newWidth = Mathf.Min(district.StartWidth, newWidth + 0.25f);
            }

            int basePoints = grade == TimingGrade.Perfect ? 150 : grade == TimingGrade.Good ? 80 : 30;
            if (grade != TimingGrade.Offbeat)
            {
                combo++;
                maxCombo = Mathf.Max(maxCombo, combo);
            }
            else
            {
                combo = 0;
            }

            int multiplier = Multiplier;
            int centerBonus = centered ? 30 : 0;
            int floorBonus = 25 * (floors + 1);
            int points = Mathf.RoundToInt(basePoints * multiplier) + centerBonus + floorBonus;
            score += points;
            floors++;

            if (grade == TimingGrade.Perfect) perfects++;
            else if (grade == TimingGrade.Good) goods++;
            else offbeats++;

            bool attuned = grade != TimingGrade.Offbeat;
            moving.SetMoving(false);
            moving.Resize(newWidth, newCenter);
            moving.MoveTo(newCenter, top.TopY + BlockHeight * 0.5f);
            if (attuned) moving.Attune(false);
            else moving.Pulse(0.4f);

            var layer = new Layer
            {
                Center = newCenter,
                Width = newWidth,
                TopY = top.TopY + BlockHeight,
                View = moving
            };

            // Spawn the severed overhang before the layer becomes the new top.
            float overhangWidth = centered ? 0f : top.Width - newWidth;
            if (overhangWidth > 0.03f)
            {
                bool leftSide = moveLeft < topLeft;
                float cutWidth = Mathf.Min(overhangWidth, top.Width);
                float cutCenter = leftSide ? topLeft - cutWidth * 0.5f : topRight + cutWidth * 0.5f;
                HushDebris.Chunk(
                    stage.DebrisRoot,
                    new Vector3(cutCenter, top.TopY + BlockHeight * 0.5f, 0f),
                    new Vector3(cutWidth, BlockHeight, BlockDepth),
                    attuned,
                    new Vector3(leftSide ? -2.4f : 2.4f, 1.3f, UnityEngine.Random.Range(-0.6f, 0.6f)));
            }

            moving = null;
            top = layer;
            layers.Add(layer);

            // 1. Noa step hop up to new floor
            if (stage.NoaController != null)
            {
                stage.NoaController.StepUpFloor(newCenter, top.TopY, layer.Width, BlockHeight);
                if (combo == 5 || combo == 10)
                {
                    stage.NoaController.TriggerHarmonicChannel();
                    stage.Burst(new Vector3(newCenter, top.TopY, 0f), HushPalette.Amber, 20, 1.8f);
                    HushAudio.Sfx(HushAudio.SfxId.Star, 0.85f);
                }
            }

            // 2. Feedback Howler Hazard Spawning in District 2/3
            if ((DistrictIndex == 1 && floors == 7) || (DistrictIndex == 2 && floors == 10))
            {
                if (stage.Howler != null && !stage.Howler.activeSelf && stage.HowlerAI != null)
                {
                    stage.HowlerAI.InitializeForStackGame(this, stage, side: -1);
                }
            }

            Color color = attuned ? HushPalette.Amber : HushPalette.Cyan;
            stage.Burst(new Vector3(newCenter, top.TopY - BlockHeight * 0.5f, 0f), color,
                grade == TimingGrade.Perfect ? 14 : 8,
                grade == TimingGrade.Perfect ? 1.6f : 1f);
            stage.CameraRig.Shake(grade == TimingGrade.Perfect ? 0.035f : 0.06f);
            stage.SetFocus(top.TopY, floors / (float)TargetFloors);

            HushAudio.Sfx(HushAudio.SfxId.Place, 0.75f);
            if (grade == TimingGrade.Perfect) HushAudio.Sfx(HushAudio.SfxId.Perfect, 0.9f);
            else if (grade == TimingGrade.Good) HushAudio.Sfx(HushAudio.SfxId.Good, 0.65f);
            else HushAudio.Sfx(HushAudio.SfxId.Offbeat, 0.55f);

            Placed?.Invoke(new PlacementFeedback
            {
                Grade = grade,
                Points = points,
                Centered = centered,
                Floor = floors,
                Combo = combo,
                Multiplier = multiplier,
                WorldPosition = new Vector3(newCenter, top.TopY, 0f),
                Attuned = attuned
            });

            if (floors >= TargetFloors)
            {
                StartCoroutine(SuccessRoutine());
                return;
            }

            StartCoroutine(NextBlockRoutine(0.24f));
        }

        private void MissDrop(float x)
        {
            if (moving != null)
            {
                HushDebris.Chunk(
                    stage.DebrisRoot,
                    new Vector3(moving.CenterX, moving.transform.position.y, 0f),
                    new Vector3(top.Width, BlockHeight, BlockDepth),
                    false,
                    new Vector3((x < top.Center ? -1.6f : 1.6f), -1.2f, UnityEngine.Random.Range(-0.6f, 0.6f)));
                Destroy(moving.gameObject);
                moving = null;
            }

            misses++;
            combo = 0;
            lives--;
            stage.Burst(new Vector3(x, top.TopY + 0.25f, 0f), HushPalette.Danger, 12, 1.8f);
            stage.CameraRig.Shake(0.65f);
            HushAudio.Sfx(HushAudio.SfxId.Miss, 1f);
            HushAudio.Sfx(HushAudio.SfxId.HeartLost, 0.8f);
            LifeLost?.Invoke(lives);

            if (stage.NoaController != null)
            {
                stage.NoaController.TriggerStumble();
            }
            ApplyTowerTremor(1.5f);

            Placed?.Invoke(new PlacementFeedback
            {
                Grade = TimingGrade.Miss,
                Points = 0,
                Centered = false,
                Floor = floors,
                Combo = 0,
                Multiplier = 1,
                WorldPosition = new Vector3(x, top.TopY, 0f),
                Attuned = false
            });

            if (lives <= 0)
            {
                StartCoroutine(FailureRoutine());
                return;
            }

            StartCoroutine(NextBlockRoutine(0.5f));
        }

        private IEnumerator NextBlockRoutine(float delay)
        {
            if (guide != null) guide.SetActive(false);
            if (beam != null) beam.SetActive(false);
            yield return WaitGameplay(delay);
            if (IsFinished || !IsRunning) yield break;
            if (guide != null)
            {
                guide.SetActive(true);
                guide.transform.position = new Vector3(top.Center, top.TopY + 0.012f, 0f);
                guide.transform.localScale = new Vector3(top.Width, 0.014f, BlockDepth * 1.01f);
            }
            if (beam != null) beam.SetActive(true);
            SpawnMovingBlock();
            stage.SetFocus(top.TopY, floors / (float)TargetFloors);
        }

        public void ApplyTowerTremor(float intensity)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                float delay = (layers.Count - 1 - i) * 0.035f;
                layers[i].View?.ApplyTremor(intensity, delay);
            }
        }

        public void DamageLife()
        {
            if (IsFinished || !IsRunning) return;
            lives--;
            combo = 0;
            stage.CameraRig.Shake(0.35f);
            HushAudio.Sfx(HushAudio.SfxId.Miss, 1f);
            HushAudio.Sfx(HushAudio.SfxId.HeartLost, 0.9f);
            LifeLost?.Invoke(lives);

            if (stage.NoaController != null)
            {
                stage.NoaController.TriggerStumble();
            }
            ApplyTowerTremor(1.5f);

            if (lives <= 0)
            {
                StartCoroutine(FailureRoutine());
            }
        }

        private IEnumerator WaitGameplay(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (!paused) elapsed += Time.deltaTime;
                yield return null;
            }
        }

        // ------------------------------------------------------------------ outcomes

        private IEnumerator SuccessRoutine()
        {
            if (IsFinished) yield break;
            IsFinished = true;
            IsResolving = true;
            accepting = false;
            dropping = false;
            HushAudio.StopClicks();
            HushAudio.Sfx(HushAudio.SfxId.LevelClear, 1f);
            stage.SetFocus(top.TopY + 0.4f, 1f);

            // 4. Rooftop Garden Sanctuary Bloom atop cleared tower
            stage.SpawnSanctuary(new Vector3(top.Center, top.TopY, 0f), top.Width);
            if (stage.NoaController != null)
            {
                stage.NoaController.ResetToBase(top.Center - 0.7f, top.TopY + 0.15f, top.Width);
            }

            for (int i = 0; i < layers.Count; i++)
            {
                layers[i].View.Attune(true);
                layers[i].View.Pulse(0.9f);
                if (i % 2 == 0) stage.Burst(new Vector3(layers[i].Center, layers[i].TopY, 0f), HushPalette.Amber, 4, 0.7f);
                yield return WaitGameplay(0.055f);
            }

            yield return WaitGameplay(0.85f);

            float accuracy = Accuracy;
            int bonusLives = lives * 400;
            int bonusAccuracy = Mathf.RoundToInt(accuracy * 600f);
            int bonusCombo = maxCombo * 15;
            int finalScore = score + bonusLives + bonusAccuracy + bonusCombo;
            int stars = 1;
            if (finalScore >= district.TwoStarScore) stars = 2;
            if (finalScore >= district.ThreeStarScore) stars = 3;

            bool newBest = HushProgress.SubmitRun(DistrictIndex, finalScore, stars, true);
            var result = new RunResult
            {
                DistrictIndex = DistrictIndex,
                Success = true,
                Score = finalScore,
                Floors = floors,
                Target = TargetFloors,
                Perfects = perfects,
                Goods = goods,
                Offbeats = offbeats,
                Misses = misses,
                MaxCombo = maxCombo,
                Lives = lives,
                Stars = stars,
                Best = HushProgress.Data.bestScores[DistrictIndex],
                NewBest = newBest,
                Accuracy = accuracy,
                Duration = BeatSeconds
            };
            Finished?.Invoke(result);
            IsResolving = false;
        }

        private IEnumerator FailureRoutine()
        {
            if (IsFinished) yield break;
            IsFinished = true;
            IsResolving = true;
            accepting = false;
            dropping = false;
            HushAudio.StopClicks();
            HushAudio.Sfx(HushAudio.SfxId.GameOver, 1f);
            stage.CameraRig.Shake(1f);

            if (moving != null)
            {
                HushDebris.Chunk(stage.DebrisRoot, moving.transform.position,
                    new Vector3(top.Width, BlockHeight, BlockDepth), false, new Vector3(0f, -2f, 0f));
                Destroy(moving.gameObject);
                moving = null;
            }

            int topple = Mathf.Min(6, layers.Count);
            for (int i = layers.Count - topple; i < layers.Count; i++)
            {
                var layer = layers[i];
                layer.View.Topple(new Vector3(UnityEngine.Random.Range(-2.5f, 2.5f), 0.5f, UnityEngine.Random.Range(-1f, 1f)));
            }

            yield return WaitGameplay(1.6f);

            bool newBest = HushProgress.SubmitRun(DistrictIndex, score, 0, false);
            var result = new RunResult
            {
                DistrictIndex = DistrictIndex,
                Success = false,
                Score = score,
                Floors = floors,
                Target = TargetFloors,
                Perfects = perfects,
                Goods = goods,
                Offbeats = offbeats,
                Misses = misses,
                MaxCombo = maxCombo,
                Lives = 0,
                Stars = 0,
                Best = HushProgress.Data.bestScores[DistrictIndex],
                NewBest = newBest,
                Accuracy = Accuracy,
                Duration = BeatSeconds
            };
            Finished?.Invoke(result);
            IsResolving = false;
        }

        // ------------------------------------------------------------------ debug

        public void DebugDrop(bool perfect)
        {
            if (!IsRunning || IsFinished || moving == null || dropping) return;
            if (perfect)
            {
                moving.MoveTo(top.Center, moving.transform.position.y);
                dropX = top.Center;
            }
            if (!accepting) accepting = true;
            Drop();
        }

        public void DebugComplete()
        {
            if (!IsRunning || IsFinished) return;
            while (floors < TargetFloors)
            {
                float width = top.Width;
                var view = BlockView.Create(stage.BlockRoot, top.Center, top.TopY, width, BlockHeight, BlockDepth, true);
                view.Attune(true);
                top = new Layer { Center = top.Center, Width = width, TopY = top.TopY + BlockHeight, View = view };
                layers.Add(top);
                floors++;
                score += 250;
                perfects++;
                combo++;
                maxCombo = Mathf.Max(maxCombo, combo);
                HushAudio.Sfx(HushAudio.SfxId.Place, 0.3f);
            }
            if (moving != null)
            {
                Destroy(moving.gameObject);
                moving = null;
            }
            StartCoroutine(SuccessRoutine());
        }

        public void DebugFail()
        {
            if (!IsRunning || IsFinished) return;
            lives = 0;
            StartCoroutine(FailureRoutine());
        }
    }
}
