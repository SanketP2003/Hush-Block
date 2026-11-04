using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace HushBlocks.FullGame
{
    /// <summary>
    /// Builds and owns the runtime uGUI canvas. All screens are constructed in
    /// code with the procedural sprites from <see cref="HushArt"/>.
    /// </summary>
    public sealed class HushUI
    {
        public readonly Canvas Canvas;
        public readonly RectTransform Root;
        public readonly RectTransform ScreenRoot;
        public readonly RectTransform SafeArea;
        public readonly CanvasGroup FadeGroup;
        public readonly Image FadeImage;

        public HushUI()
        {
            var canvasObject = new GameObject("Hush UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (HushGame.Instance != null)
            {
                canvasObject.transform.SetParent(HushGame.Instance.transform, false);
            }
            Canvas = canvasObject.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 100;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            Root = canvasObject.GetComponent<RectTransform>();
            Stretch(Root, "Root");

            var frame = Stretch(Root, "Frame");
            var vignette = Image(frame, "Vignette", HushArt.Vignette, Color.white);
            Fill(vignette.rectTransform);
            vignette.preserveAspect = false;
            vignette.raycastTarget = false;

            ScreenRoot = Stretch(Root, "Screens");
            SafeArea = Stretch(ScreenRoot, "Safe Area");
            SafeArea.gameObject.AddComponent<SafeAreaFitter>();

            var fade = Stretch(Root, "Fade");
            FadeImage = Image(fade, "Black", null, Color.black);
            Fill(FadeImage.rectTransform);
            FadeImage.raycastTarget = false;
            FadeGroup = fade.gameObject.AddComponent<CanvasGroup>();
            FadeGroup.alpha = 1f;
            FadeGroup.blocksRaycasts = false;
            FadeGroup.interactable = false;

            EnsureEventSystem();
        }

        public void SetFade(float alpha)
        {
            if (FadeGroup == null) return;
            FadeGroup.alpha = Mathf.Clamp01(alpha);
            FadeGroup.blocksRaycasts = FadeGroup.alpha > 0.98f;
        }

        // ------------------------------------------------------------------ factories

        public static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(100f, 100f);
            return rect;
        }

        public static RectTransform NewRect(Transform parent, string name, Vector2 size, Vector2 anchor, Vector2 pivot)
        {
            var rect = NewRect(parent, name);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        public static RectTransform Stretch(Transform parent, string name, float inset = 0f)
        {
            var rect = NewRect(parent, name);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        public static void Fill(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            if (sprite != null)
            {
                image.type = sprite.border.sqrMagnitude > 0f ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            }
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(100f, 100f);
            return image;
        }

        public static TextMeshProUGUI Text(
            Transform parent,
            string name,
            string content,
            float size,
            Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left,
            bool heading = false,
            bool wrap = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = heading ? HushArt.HeadingFont : HushArt.BodyFont;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.text = content;
            text.raycastTarget = false;
            text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.characterSpacing = heading ? 3f : 0f;
            text.lineSpacing = heading ? 4f : 8f;
            text.enableAutoSizing = false;

            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(500f, size * 1.5f);
            rect.anchoredPosition = Vector2.zero;
            return text;
        }

        public static RectTransform Panel(
            Transform parent,
            string name,
            Color fill,
            Color border,
            float borderWidth = 2f,
            bool topAccent = false,
            Color accent = default)
        {
            var root = NewRect(parent, name);
            var borderImage = Image(root, "Border", HushArt.Rounded, border);
            Fill(borderImage.rectTransform);
            borderImage.type = UnityEngine.UI.Image.Type.Sliced;

            var fillImage = Image(root, "Fill", HushArt.Rounded, fill);
            Fill(fillImage.rectTransform, borderWidth);
            fillImage.type = UnityEngine.UI.Image.Type.Sliced;

            if (topAccent)
            {
                if (accent == default) accent = HushPalette.Amber;
                var bar = Image(root, "Accent", HushArt.RoundedSmall, accent);
                bar.type = UnityEngine.UI.Image.Type.Sliced;
                var barRect = bar.rectTransform;
                barRect.anchorMin = new Vector2(0f, 1f);
                barRect.anchorMax = new Vector2(1f, 1f);
                barRect.pivot = new Vector2(0.5f, 1f);
                barRect.offsetMin = new Vector2(26f, -4f);
                barRect.offsetMax = new Vector2(-26f, 0f);
            }

            return root;
        }

        public static HushButton Button(Transform parent, string label, Vector2 size, HushButton.Style style, float fontSize = 26f)
        {
            // Ensure minimum 48x48 touch target
            Vector2 minSize = new Vector2(Mathf.Max(48f, size.x), Mathf.Max(48f, size.y));
            var root = NewRect(parent, "Button " + label, minSize, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            var button = root.gameObject.AddComponent<HushButton>();

            var selectable = root.gameObject.AddComponent<Selectable>();
            selectable.transition = Selectable.Transition.None;

            var glow = Image(root, "Glow", HushArt.Rounded, Color.clear);
            Fill(glow.rectTransform, -7f);
            glow.type = UnityEngine.UI.Image.Type.Sliced;
            glow.raycastTarget = false;

            var background = Image(root, "Background", HushArt.Rounded, Color.white);
            Fill(background.rectTransform);
            background.type = UnityEngine.UI.Image.Type.Sliced;
            background.raycastTarget = true;
            selectable.targetGraphic = background;

            var border = Image(root, "Border", HushArt.Rounded, Color.white);
            Fill(border.rectTransform, 1.5f);
            border.type = UnityEngine.UI.Image.Type.Sliced;
            border.raycastTarget = false;

            var text = Text(root, "Label", label, fontSize, Color.white, TextAlignmentOptions.Center, false, false);
            Fill(text.rectTransform, 6f);

            button.Configure(background, border, glow, text, style);
            return button;
        }

        public static HushToggle Toggle(Transform parent, string label, bool value, Action<bool> changed)
        {
            var row = NewRect(parent, "Row " + label, new Vector2(640f, 58f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            var text = Text(row, "Label", label, 24f, HushPalette.Text, TextAlignmentOptions.Left);
            var textRect = text.rectTransform;
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(4f, 0f);
            textRect.offsetMax = new Vector2(-100f, 0f);

            var toggleRoot = NewRect(row, "Toggle", new Vector2(72f, 48f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            toggleRoot.anchoredPosition = new Vector2(-4f, 0f);

            var selectable = toggleRoot.gameObject.AddComponent<Selectable>();
            selectable.transition = Selectable.Transition.None;

            var track = Image(toggleRoot, "Track", HushArt.Rounded, HushPalette.Border);
            Fill(track.rectTransform);
            track.type = UnityEngine.UI.Image.Type.Sliced;
            track.raycastTarget = true;
            selectable.targetGraphic = track;

            var knob = Image(toggleRoot, "Knob", HushArt.Circle, HushPalette.Text);
            var knobRect = knob.rectTransform;
            knobRect.anchorMin = new Vector2(0.5f, 0.5f);
            knobRect.anchorMax = new Vector2(0.5f, 0.5f);
            knobRect.sizeDelta = new Vector2(28f, 28f);
            knobRect.anchoredPosition = new Vector2(-18f, 0f);
            knob.raycastTarget = false;

            var toggle = toggleRoot.gameObject.AddComponent<HushToggle>();
            toggle.Configure(track, knob, value, changed);
            return toggle;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            if (HushGame.Instance != null)
            {
                go.transform.SetParent(HushGame.Instance.transform, false);
            }
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        // ------------------------------------------------------------------ tweens

        public static IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
        {
            if (group == null) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration)));
                yield return null;
            }
            group.alpha = to;
        }

        public static IEnumerator Pop(RectTransform rect, float from, float to, float duration)
        {
            if (rect == null) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
                t = 1f - Mathf.Pow(1f - t, 3f);
                float scale = Mathf.LerpUnclamped(from, to, t);
                rect.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            rect.localScale = new Vector3(to, to, 1f);
        }
    }

    /// <summary>Scale/slide reveal for a whole screen.</summary>
    public abstract class HushScreen
    {
        protected readonly HushGame Runtime;
        public RectTransform Root { get; private set; }
        public CanvasGroup CanvasGroup { get; private set; }

        protected HushScreen(HushGame game)
        {
            Runtime = game;
        }

        public void Build(RectTransform parent)
        {
            if (Root != null) return;
            Root = HushUI.Stretch(parent, GetType().Name.Replace("Screen", string.Empty) + " Screen");
            CanvasGroup = Root.gameObject.AddComponent<CanvasGroup>();
            CanvasGroup.alpha = 1f;
            OnBuild();
            Root.gameObject.SetActive(false);
        }

        protected abstract void OnBuild();

        public virtual void Show(bool instant = false)
        {
            if (Root == null) return;
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            CanvasGroup.alpha = instant ? 1f : 0f;
            Root.localScale = Vector3.one;
            if (!instant) Runtime.AnimateScreenIn(this);
            OnShow();
        }

        public virtual void Hide()
        {
            if (Root != null) Root.gameObject.SetActive(false);
            OnHide();
        }

        protected virtual void OnShow() { }
        protected virtual void OnHide() { }
        public virtual void Tick() { }
        public bool IsVisible => Root != null && Root.gameObject.activeSelf;
    }

    /// <summary>Interactive button drawn from procedural sprites.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class HushButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
        ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        public enum Style { Primary, Secondary, Ghost, Danger, Icon }

        public Action Clicked;

        private Image background;
        private Image border;
        private Image glow;
        private TextMeshProUGUI label;
        private CanvasGroup canvasGroup;
        private Style style;
        private Color baseFill;
        private Color hoverFill;
        private Color baseText;
        private Color baseBorder;
        private Color accent;
        private bool hovered;
        private bool pressed;
        private bool isFocused;
        private bool interactable = true;
        private float scale = 1f;

        public string Label => label != null ? label.text : string.Empty;

        public void Configure(Image backgroundImage, Image borderImage, Image glowImage, TextMeshProUGUI labelText, Style buttonStyle)
        {
            background = backgroundImage;
            border = borderImage;
            glow = glowImage;
            label = labelText;
            style = buttonStyle;
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

            switch (style)
            {
                case Style.Primary:
                    baseFill = HushPalette.Amber;
                    hoverFill = HushPalette.AmberSoft;
                    baseText = HushPalette.Ink;
                    baseBorder = HushPalette.AmberSoft;
                    accent = HushPalette.Amber;
                    if (glow != null) glow.color = new Color(accent.r, accent.g, accent.b, 0.16f);
                    break;
                case Style.Secondary:
                    baseFill = HushPalette.PanelLight;
                    hoverFill = HushPalette.Hex("#1D2740");
                    baseText = HushPalette.Text;
                    baseBorder = HushPalette.Border;
                    accent = HushPalette.Cyan;
                    if (glow != null) glow.color = Color.clear;
                    break;
                case Style.Danger:
                    baseFill = HushPalette.WithAlpha(HushPalette.Panel, 0.35f);
                    hoverFill = HushPalette.WithAlpha(HushPalette.Danger, 0.12f);
                    baseText = HushPalette.Danger;
                    baseBorder = HushPalette.WithAlpha(HushPalette.Danger, 0.55f);
                    accent = HushPalette.Danger;
                    if (glow != null) glow.color = Color.clear;
                    break;
                case Style.Icon:
                    baseFill = HushPalette.WithAlpha(HushPalette.Panel, 0.82f);
                    hoverFill = HushPalette.PanelLight;
                    baseText = HushPalette.Text;
                    baseBorder = HushPalette.Border;
                    accent = HushPalette.Amber;
                    if (glow != null) glow.color = Color.clear;
                    break;
                default:
                    baseFill = new Color(1f, 1f, 1f, 0.001f);
                    hoverFill = new Color(1f, 1f, 1f, 0.055f);
                    baseText = HushPalette.Muted;
                    baseBorder = new Color(1f, 1f, 1f, 0f);
                    accent = HushPalette.Cyan;
                    if (glow != null) glow.color = Color.clear;
                    break;
            }

            background.color = baseFill;
            border.color = baseBorder;
            label.color = baseText;
        }

        public void SetLabel(string value)
        {
            if (label != null) label.text = value;
        }

        public void SetAccent(Color color)
        {
            accent = color;
            if (style == Style.Primary)
            {
                baseFill = color;
                hoverFill = Color.Lerp(color, Color.white, 0.22f);
                baseBorder = hoverFill;
                label.color = HushPalette.Ink;
                if (glow != null) glow.color = new Color(color.r, color.g, color.b, 0.18f);
            }
            else if (style == Style.Secondary)
            {
                baseBorder = HushPalette.WithAlpha(color, 0.45f);
            }
        }

        public bool IsInteractable => interactable;

        public void SetInteractable(bool value)
        {
            interactable = value;
            var selectable = GetComponent<Selectable>();
            if (selectable != null) selectable.interactable = value;
            if (canvasGroup != null) canvasGroup.alpha = value ? 1f : 0.35f;
            if (canvasGroup != null) canvasGroup.interactable = value;
            if (!value)
            {
                hovered = false;
                pressed = false;
                isFocused = false;
            }
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        private void Update()
        {
            if (background == null) return;
            bool highlighted = (hovered || isFocused) && interactable;
            float targetScale = interactable ? (pressed ? 0.96f : highlighted ? 1.028f : 1f) : 1f;
            scale = Mathf.Lerp(scale, targetScale, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(scale, scale, 1f);

            Color targetFill = highlighted ? hoverFill : baseFill;
            background.color = Color.Lerp(background.color, targetFill, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));

            Color targetBorder = isFocused ? accent : baseBorder;
            border.color = Color.Lerp(border.color, targetBorder, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));

            if (glow != null)
            {
                float targetAlpha = isFocused ? 0.55f : highlighted ? 0.3f : (style == Style.Primary ? 0.16f : 0f);
                Color targetGlow = new Color(accent.r, accent.g, accent.b, targetAlpha);
                glow.color = Color.Lerp(glow.color, targetGlow, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            }

            if (label != null)
            {
                Color targetText = interactable ? baseText : HushPalette.Muted;
                label.color = Color.Lerp(label.color, targetText, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (interactable) hovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (interactable) pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!interactable || eventData.button != PointerEventData.InputButton.Left) return;
            HushAudio.Sfx(HushAudio.SfxId.UiClick, 0.65f);
            Clicked?.Invoke();
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (interactable) isFocused = true;
        }

        public void OnDeselect(BaseEventData eventData)
        {
            isFocused = false;
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!interactable) return;
            HushAudio.Sfx(HushAudio.SfxId.UiClick, 0.65f);
            Clicked?.Invoke();
        }
    }

    /// <summary>Pill switch used by the settings screen.</summary>
    public sealed class HushToggle : MonoBehaviour, IPointerClickHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        private Image track;
        private Image knob;
        private RectTransform knobRect;
        private Action<bool> changed;
        private bool value;
        private float visual;
        private bool isFocused;

        public bool Value => value;
        public RectTransform RowRect => transform.parent as RectTransform;

        public void Configure(Image trackImage, Image knobImage, bool initial, Action<bool> onChanged)
        {
            track = trackImage;
            knob = knobImage;
            knobRect = knob.rectTransform;
            changed = onChanged;
            value = initial;
            visual = initial ? 1f : 0f;
            Apply();
        }

        public void SetValue(bool next, bool notify = false)
        {
            if (value == next) return;
            value = next;
            Apply();
            if (notify) changed?.Invoke(value);
        }

        private void Apply()
        {
            if (track != null)
            {
                Color baseColor = value ? HushPalette.Amber : HushPalette.Border;
                track.color = isFocused ? Color.Lerp(baseColor, HushPalette.Cyan, 0.35f) : baseColor;
            }
            if (knob != null)
            {
                knob.color = value ? HushPalette.Ink : HushPalette.Muted;
            }
        }

        private void Update()
        {
            if (knobRect == null) return;
            visual = Mathf.Lerp(visual, value ? 1f : 0f, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            knobRect.anchoredPosition = new Vector2(Mathf.Lerp(-18f, 18f, visual), 0f);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            ToggleValue();
        }

        public void OnSelect(BaseEventData eventData)
        {
            isFocused = true;
            Apply();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            isFocused = false;
            Apply();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            ToggleValue();
        }

        private void ToggleValue()
        {
            HushAudio.Sfx(HushAudio.SfxId.UiClick, 0.5f);
            value = !value;
            Apply();
            changed?.Invoke(value);
        }
    }

    /// <summary>Keeps the UI clear of notches and platform insets.</summary>
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rect;
        private Rect lastSafeArea;
        private Vector2Int lastScreen;

        private void Awake()
        {
            rect = (RectTransform)transform;
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != lastSafeArea || Screen.width != lastScreen.x || Screen.height != lastScreen.y)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (rect == null) return;
            Rect safe = Screen.safeArea;
            lastSafeArea = safe;
            lastScreen = new Vector2Int(Screen.width, Screen.height);

            if (Screen.width <= 0 || Screen.height <= 0) return;
            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
