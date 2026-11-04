using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HushBlocks.UI;
using HushBlocks.Gameplay;

namespace HushBlocks.Editor
{
    public static class HushBlocksHUDBuilder
    {
        private const string GlowMaterialPath = "Assets/_Project/Materials/M_DiegeticUI_VoxelGlow.mat";

        [MenuItem("Hush Blocks/UI/Build Complete Diegetic HUD Canvas")]
        public static void BuildHUDCanvas()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                camera = UnityEngine.Object.FindAnyObjectByType<Camera>();
            }

            var rhythmManager = UnityEngine.Object.FindAnyObjectByType<RhythmManager>();
            var towerManager = UnityEngine.Object.FindAnyObjectByType<TowerManager>();
            var audioManager = UnityEngine.Object.FindAnyObjectByType<AcousticAudioManager>();

            Material glowMat = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath);

            // 1. Root Canvas GameObject
            var existingCanvas = GameObject.Find("HUD_DiegeticCanvas");
            if (existingCanvas != null)
            {
                Undo.DestroyObjectImmediate(existingCanvas);
            }

            GameObject canvasObj = new GameObject("HUD_DiegeticCanvas");
            Undo.RegisterCreatedObjectUndo(canvasObj, "Create Diegetic HUD Canvas");

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 2.0f;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            AcousticHUDController hudController = canvasObj.AddComponent<AcousticHUDController>();

            // --- 2. The Beat Compass (Concentric Timing Ring) ---
            GameObject compassRoot = new GameObject("Beat_Compass_Reticle");
            compassRoot.transform.SetParent(canvasObj.transform, false);
            RectTransform compassRect = compassRoot.AddComponent<RectTransform>();
            compassRect.sizeDelta = new Vector2(120, 120);

            // Inner Frame
            GameObject innerFrameObj = new GameObject("Inner_Voxel_Frame");
            innerFrameObj.transform.SetParent(compassRoot.transform, false);
            RectTransform innerRect = innerFrameObj.AddComponent<RectTransform>();
            innerRect.sizeDelta = new Vector2(70, 70);
            Image innerFrameImg = innerFrameObj.AddComponent<Image>();
            innerFrameImg.color = new Color(0f, 0.941f, 1f, 0.85f);
            if (glowMat != null) innerFrameImg.material = glowMat;

            // Outer Contracting Ring
            GameObject outerRingObj = new GameObject("Contracting_Timing_Ring");
            outerRingObj.transform.SetParent(compassRoot.transform, false);
            RectTransform outerRect = outerRingObj.AddComponent<RectTransform>();
            outerRect.sizeDelta = new Vector2(110, 110);
            Image outerRingImg = outerRingObj.AddComponent<Image>();
            outerRingImg.color = new Color(0f, 0.941f, 1f, 1f);
            if (glowMat != null) outerRingImg.material = glowMat;

            // --- 3. The Acoustic Mortar Vial (SilenceChainMeter) ---
            GameObject vialRoot = new GameObject("Acoustic_Mortar_Vial");
            vialRoot.transform.SetParent(canvasObj.transform, false);
            RectTransform vialRect = vialRoot.AddComponent<RectTransform>();
            vialRect.anchorMin = new Vector2(0f, 1f);
            vialRect.anchorMax = new Vector2(0f, 1f);
            vialRect.pivot = new Vector2(0f, 1f);
            vialRect.anchoredPosition = new Vector2(40f, -40f);
            vialRect.sizeDelta = new Vector2(50f, 220f);

            SilenceChainMeter chainMeter = vialRoot.AddComponent<SilenceChainMeter>();

            // Vial Casing
            GameObject casingObj = new GameObject("Vial_Casing_Frame");
            casingObj.transform.SetParent(vialRoot.transform, false);
            RectTransform casingRect = casingObj.AddComponent<RectTransform>();
            casingRect.sizeDelta = new Vector2(50f, 220f);
            Image casingImg = casingObj.AddComponent<Image>();
            casingImg.color = new Color(0.11f, 0.118f, 0.141f, 0.85f); // Velvety Charcoal

            // Liquid Fill
            GameObject liquidObj = new GameObject("Liquid_Amber_Column");
            liquidObj.transform.SetParent(vialRoot.transform, false);
            RectTransform liquidRect = liquidObj.AddComponent<RectTransform>();
            liquidRect.sizeDelta = new Vector2(38f, 208f);
            Image liquidImg = liquidObj.AddComponent<Image>();
            liquidImg.type = Image.Type.Filled;
            liquidImg.fillMethod = Image.FillMethod.Vertical;
            liquidImg.fillOrigin = (int)Image.OriginVertical.Bottom;
            liquidImg.color = new Color(1.0f, 0.702f, 0.0f, 1f); // Honey-Amber
            if (glowMat != null) liquidImg.material = glowMat;

            // Vial Glow Overlay
            GameObject glowOverlayObj = new GameObject("Vial_Glow_Overlay");
            glowOverlayObj.transform.SetParent(vialRoot.transform, false);
            RectTransform glowRect = glowOverlayObj.AddComponent<RectTransform>();
            glowRect.sizeDelta = new Vector2(44f, 214f);
            Image glowImg = glowOverlayObj.AddComponent<Image>();
            glowImg.color = new Color(1.0f, 0.88f, 0.5f, 0f);

            // Chain Counter Text
            GameObject chainTextObj = new GameObject("Chain_Counter_Text");
            chainTextObj.transform.SetParent(vialRoot.transform, false);
            RectTransform chainTextRect = chainTextObj.AddComponent<RectTransform>();
            chainTextRect.anchoredPosition = new Vector2(0f, -235f);
            chainTextRect.sizeDelta = new Vector2(80f, 30f);
            TextMeshProUGUI chainTmp = chainTextObj.AddComponent<TextMeshProUGUI>();
            chainTmp.fontSize = 24;
            chainTmp.alignment = TextAlignmentOptions.Center;
            chainTmp.fontStyle = FontStyles.Bold;
            chainTmp.color = new Color(1.0f, 0.702f, 0.0f, 1f);
            chainTmp.text = "x0";

            // Harmonic Hush Glyph
            GameObject glyphObj = new GameObject("Harmonic_Hush_Glyph");
            glyphObj.transform.SetParent(vialRoot.transform, false);
            RectTransform glyphRect = glyphObj.AddComponent<RectTransform>();
            glyphRect.anchoredPosition = new Vector2(65f, -110f);
            glyphRect.sizeDelta = new Vector2(48f, 48f);
            CanvasGroup glyphGroup = glyphObj.AddComponent<CanvasGroup>();
            Image glyphImg = glyphObj.AddComponent<Image>();
            glyphImg.color = new Color(1.0f, 0.88f, 0.5f, 1f);
            if (glowMat != null) glyphImg.material = glowMat;

            // Wire SilenceChainMeter fields via reflection
            SetField(chainMeter, "liquidFillImage", liquidImg);
            SetField(chainMeter, "vialCasingImage", casingImg);
            SetField(chainMeter, "vialGlowOverlay", glowImg);
            SetField(chainMeter, "chainCountText", chainTmp);
            SetField(chainMeter, "punchTargetRect", vialRect);
            SetField(chainMeter, "harmonicHushGlyph", glyphObj);
            SetField(chainMeter, "glyphCanvasGroup", glyphGroup);

            // --- 4. The Tremor Seismograph (Bottom Bar) ---
            GameObject seismoRoot = new GameObject("Tremor_Seismograph");
            seismoRoot.transform.SetParent(canvasObj.transform, false);
            RectTransform seismoRect = seismoRoot.AddComponent<RectTransform>();
            seismoRect.anchorMin = new Vector2(0.5f, 0f);
            seismoRect.anchorMax = new Vector2(0.5f, 0f);
            seismoRect.pivot = new Vector2(0.5f, 0f);
            seismoRect.anchoredPosition = new Vector2(0f, 25f);
            seismoRect.sizeDelta = new Vector2(480f, 36f);

            // Waveform bar
            GameObject waveObj = new GameObject("Seismograph_Waveform");
            waveObj.transform.SetParent(seismoRoot.transform, false);
            RectTransform waveRect = waveObj.AddComponent<RectTransform>();
            waveRect.sizeDelta = new Vector2(470f, 6f);
            Image waveImg = waveObj.AddComponent<Image>();
            waveImg.color = new Color(1.0f, 0.702f, 0.0f, 0.85f);
            if (glowMat != null) waveImg.material = glowMat;

            // Alert banner
            GameObject alertObj = new GameObject("Hazard_Alert_Banner");
            alertObj.transform.SetParent(seismoRoot.transform, false);
            RectTransform alertRect = alertObj.AddComponent<RectTransform>();
            alertRect.anchoredPosition = new Vector2(0f, 28f);
            alertRect.sizeDelta = new Vector2(380f, 26f);
            CanvasGroup alertGroup = alertObj.AddComponent<CanvasGroup>();
            TextMeshProUGUI alertTmp = alertObj.AddComponent<TextMeshProUGUI>();
            alertTmp.fontSize = 16;
            alertTmp.alignment = TextAlignmentOptions.Center;
            alertTmp.fontStyle = FontStyles.Bold;
            alertTmp.color = new Color(0f, 0.941f, 1f, 1f);
            alertTmp.text = "STACK STABLE";

            // --- 5. The Elevation Altimeter (Right Margin Ladder) ---
            GameObject altimeterRoot = new GameObject("Elevation_Altimeter");
            altimeterRoot.transform.SetParent(canvasObj.transform, false);
            RectTransform altimeterRect = altimeterRoot.AddComponent<RectTransform>();
            altimeterRect.anchorMin = new Vector2(1f, 0.5f);
            altimeterRect.anchorMax = new Vector2(1f, 0.5f);
            altimeterRect.pivot = new Vector2(1f, 0.5f);
            altimeterRect.anchoredPosition = new Vector2(-40f, 0f);
            altimeterRect.sizeDelta = new Vector2(60f, 320f);

            GameObject ladderObj = new GameObject("Altimeter_Ladder_Ticks");
            ladderObj.transform.SetParent(altimeterRoot.transform, false);
            RectTransform ladderRect = ladderObj.AddComponent<RectTransform>();
            ladderRect.sizeDelta = new Vector2(24f, 260f);

            var vlg = ladderObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.LowerCenter;
            vlg.childControlHeight = false;
            vlg.childControlWidth = false;

            GameObject floorTextObj = new GameObject("Floor_Count_Text");
            floorTextObj.transform.SetParent(altimeterRoot.transform, false);
            RectTransform floorTextRect = floorTextObj.AddComponent<RectTransform>();
            floorTextRect.anchoredPosition = new Vector2(0f, -160f);
            floorTextRect.sizeDelta = new Vector2(90f, 30f);
            TextMeshProUGUI floorTmp = floorTextObj.AddComponent<TextMeshProUGUI>();
            floorTmp.fontSize = 18;
            floorTmp.alignment = TextAlignmentOptions.Center;
            floorTmp.fontStyle = FontStyles.Bold;
            floorTmp.color = new Color(1.0f, 0.702f, 0.0f, 1f);
            floorTmp.text = "00 / 12F";

            // --- Wire AcousticHUDController Fields via Reflection ---
            SetField(hudController, "dioramaCamera", camera);
            SetField(hudController, "hudCanvas", canvas);
            SetField(hudController, "reticleRootRect", compassRect);
            SetField(hudController, "contractingRingRect", outerRect);
            SetField(hudController, "contractingRingImage", outerRingImg);
            SetField(hudController, "innerFrameImage", innerFrameImg);
            SetField(hudController, "silenceChainMeter", chainMeter);
            SetField(hudController, "seismographBarRect", seismoRect);
            SetField(hudController, "seismographWaveformImage", waveImg);
            SetField(hudController, "seismographAlertText", alertTmp);
            SetField(hudController, "seismographAlertCanvasGroup", alertGroup);
            SetField(hudController, "altimeterLadderRect", ladderRect);
            SetField(hudController, "floorCountText", floorTmp);
            SetField(hudController, "rhythmManager", rhythmManager);
            SetField(hudController, "towerManager", towerManager);
            SetField(hudController, "audioManager", audioManager);

            EditorUtility.SetDirty(canvasObj);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log("[HushBlocksHUDBuilder] Diegetic HUD Canvas successfully built and configured in the scene!");
        }

        private static void SetField(object target, string fieldName, object value)
        {
            if (target == null) return;
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}

