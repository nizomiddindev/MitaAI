using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;
using Coffee.UIEffects;
using Object = UnityEngine.Object;

namespace MitaAI.Plugin;

internal sealed class NativePresentation
{
    private readonly SubtitleTurnState turn = new();
    private readonly ScreenSubtitleState playback = new();
    private MitaPerson? turnMita;
    private PlayerMove? turnPlayer;
    private Camera? turnCamera;
    private GameController? controller;
    private float nextScan;
    private string uiInventoryStamp = "";
    private GameObject? ui;
    private RectTransform? prompt;
    private RectTransform? composerPanel;
    private CanvasGroup? promptGroup;
    private CanvasGroup? composerGroup;
    private Text? draft;
    private Text? promptKey;
    private RectTransform? subtitlePanel;
    private CanvasGroup? subtitleGroup;
    private Text? subtitle;
    private float composerAlpha;
    private float subtitleAlpha;
    private bool awaitingReply;
    private float awaitingExpires;
    private Sprite? roundedSprite;
    private Texture2D? roundedTexture;
    private Sprite? roundedBorderSprite;
    private Texture2D? roundedBorderTexture;

    internal bool HasUi => ui != null;
    internal bool HasActiveTurn => playback.Active || turn.Pending || awaitingReply;
    internal bool IsPaused => (controller != null && controller.isPaused) || !Application.isFocused || Time.timeScale <= 0;
    internal Font? NativeFont => draft != null && draft.font != null ? draft.font : subtitle != null ? subtitle.font : null;

    internal void Tick(bool composing, string message, float promptAlpha, string key, bool historyOpen = false)
    {
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 2;
            Discover();
        }
        bool isPaused = IsPaused;
        if (isPaused)
        {
            playback.PauseTick(Time.unscaledDeltaTime);
            turn.PauseTick(Time.unscaledDeltaTime);
            if (awaitingReply) awaitingExpires += Time.unscaledDeltaTime;
        }

        if (playback.Active || turn.Pending || awaitingReply)
        {
            bool bindingLost = controller == null
                || turnMita == null || !turnMita.gameObject.activeInHierarchy || !Loaded(turnMita)
                || turnPlayer == null || !turnPlayer.gameObject.scene.isLoaded || !turnPlayer.gameObject.activeInHierarchy;

            if (bindingLost || StoryBusy)
            {
                Plugin.Logger.LogInfo($"MITAAI_TURN_CANCELLED reason={(StoryBusy ? "story-dialogue-priority" : "binding-lost")}");
                ClearReply();
                CancelTurn();
            }
            else if (!isPaused)
            {
                if (turn.TryTake(Time.unscaledTime, true, out string line, out float duration))
                    ShowScreenLine(line, duration, false);
                else if (playback.Finished(Time.unscaledTime))
                {
                    if (awaitingReply)
                    {
                        if (Time.unscaledTime >= awaitingExpires)
                        {
                            Plugin.Logger.LogWarning("MITAAI_TURN_TIMEOUT reason=reply-deadline-exceeded");
                            ClearReply();
                            CancelTurn();
                        }
                    }
                    else
                    {
                        ClearReply();
                        CancelTurn();
                    }
                }
            }
        }
        if (ui == null) return;
        subtitleAlpha = Mathf.MoveTowards(subtitleAlpha, playback.Active && !composing && !historyOpen ? 1 : 0, Time.unscaledDeltaTime / 0.14f);
        subtitlePanel!.gameObject.SetActive(subtitleAlpha > 0);
        subtitleGroup!.alpha = subtitleAlpha;
        if (playback.Active) subtitle!.text = playback.Visible(Time.unscaledTime);
        composerAlpha = Mathf.MoveTowards(composerAlpha, composing ? 1 : 0, Time.unscaledDeltaTime / 0.18f);
        float eased = composerAlpha * composerAlpha * (3 - 2 * composerAlpha);
        composerPanel!.gameObject.SetActive(composerAlpha > 0);
        composerGroup!.alpha = eased;
        composerPanel.anchoredPosition = new Vector2(0, -(1 - eased) * 8);
        if (composing) draft!.text = message;
        prompt!.gameObject.SetActive(!composing && promptAlpha > 0.001f);
        promptGroup!.alpha = promptAlpha;
        prompt.anchoredPosition = new Vector2(0, -110 - (1 - promptAlpha) * 10);
        prompt.localScale = Vector3.one * (0.97f + 0.03f * promptAlpha);
        promptKey!.text = key;
    }

    internal bool StoryBusy => controller != null &&
        (controller.cutsceneRun
        || (controller.dialogueTextNow != null && controller.dialogueTextNow.isActiveAndEnabled)
        || (controller.dialogueChanger != null && controller.dialogueChanger.play));

    internal bool TryBeginPlayerTurn(PlayerMove player, MitaPerson mita, Camera camera, string message, float timeoutSeconds = 30f)
    {
        CancelTurn();
        ClearReply();
        Discover();
        if (ui == null || controller == null || StoryBusy || !PlayerInputLease.CanAcquire(player))
        {
            Plugin.Logger.LogWarning("MITAAI_SCREEN_SUBTITLE_UNAVAILABLE reason=visual-assets-story-control");
            return false;
        }
        string excerpt = SubtitleTurnState.DisplayExcerpt(message);
        const float hold = 1.5f;
        composerAlpha = 0;
        composerPanel!.gameObject.SetActive(false);
        ShowScreenLine(excerpt, hold, true);
        turnMita = mita;
        turnPlayer = player;
        turnCamera = camera;
        awaitingReply = true;
        awaitingExpires = Time.unscaledTime + timeoutSeconds;
        return true;
    }

    internal bool TryShowMitaReply(string reply, float duration)
    {
        if (controller == null || StoryBusy || turnMita == null || !turnMita.gameObject.activeInHierarchy || !Loaded(turnMita)
            || turnPlayer == null || !turnPlayer.gameObject.scene.isLoaded || !turnPlayer.gameObject.activeInHierarchy
            || string.IsNullOrWhiteSpace(reply) || !float.IsFinite(duration) || duration <= 0)
        {
            awaitingReply = false;
            return false;
        }

        awaitingReply = false;
        if (playback.Active)
        {
            float delay = Math.Max(0, playback.EndsAt - Time.unscaledTime);
            turn.Begin(reply, duration, Time.unscaledTime, delay);
        }
        else
        {
            ShowScreenLine(reply, duration, false);
        }
        return true;
    }

    internal bool TryShowTurn(PlayerMove player, MitaPerson mita, Camera camera, string message, string fixture, float duration)
    {
        if (!TryBeginPlayerTurn(player, mita, camera, message)) return false;
        return TryShowMitaReply(fixture, duration);
    }

    private void ShowScreenLine(string line, float duration, bool player)
    {
        playback.Begin(line, Time.unscaledTime, duration);
        ApplySubtitleStyle(subtitle!, player ? new Color(1, 0.6f, 0) : Color.white,
            player ? Color.white : new Color(1, 0.35f, 0.75f));
        subtitle!.text = "";
        Plugin.Logger.LogInfo($"MITAAI_SCREEN_SUBTITLE_STARTED role={(player ? "Player" : "Mita")} characters={line.Length} mode={(player ? "player-echo" : "fixed-fixture")} anchor=bottom-center assets=game-font-effects animation=mod-typewriter questDriver=False");
    }

    private void Discover()
    {
        if (controller == null)
        {
            foreach (var item in Resources.FindObjectsOfTypeAll(Il2CppType.Of<GameController>()))
            {
                var candidate = item.TryCast<GameController>();
                if (candidate != null && Loaded(candidate) && candidate.gameObject.activeInHierarchy)
                { controller = candidate; break; }
            }
        }
        if (ui != null || controller == null) return;
        int choiceCount = 0;
        foreach (var item in Resources.FindObjectsOfTypeAll(Il2CppType.Of<DialogueChange_Button>()))
        {
            var button = item.TryCast<DialogueChange_Button>();
            // Visual prefab assets are valid sources even when their gameplay scene is not loaded.
            if (button == null) continue;
            choiceCount++;
            if (button.img == null || button.txt == null || button.txtKey == null || button.txt.font == null) continue;
            if (TryBuildUi(button.img, button.txt, button.txtKey, button.imgFrameIcon)) return;
        }
        int hintCount = 0;
        foreach (var item in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Interface_KeyHint_Key>()))
        {
            var hint = item.TryCast<Interface_KeyHint_Key>();
            if (hint == null) continue;
            hintCount++;
            if (hint.frameAddon == null || hint.textDescription == null || hint.textKey == null || hint.textKey.font == null) continue;
            if (TryBuildUi(hint.frameAddon, hint.textDescription, hint.textKey, hint.caseKey)) return;
        }
        string uiStamp = $"controller={Path(controller.transform)} frame={controller.imageFrameDialogue != null} text={controller.textDialogueA != null} choices={choiceCount} hints={hintCount}";
        if (uiStamp != uiInventoryStamp)
        {
            uiInventoryStamp = uiStamp;
            Plugin.Logger.LogWarning($"MITAAI_NATIVE_UI_INVENTORY {uiStamp}");
        }
    }

    private bool TryBuildUi(Image choiceFrame, Text choiceText, Text keyText, Image? keyFrame)
    {
        var subtitleText = controller!.textDialogueA;
        if (subtitleText == null || subtitleText.font == null)
            subtitleText = controller.textDialogueB;
        if (controller.imageFrameDialogue == null || subtitleText == null || subtitleText.font == null) return false;
        BuildUi(choiceFrame, choiceText, keyText, keyFrame, subtitleText);
        Plugin.Logger.LogInfo($"MITAAI_NATIVE_UI_READY source={Path(choiceFrame.transform)} font={subtitleText.font.name} prompt=translucent-rounded subtitle=screen-bottom-center animations=mod-fade-typewriter questDriver=False");
        return true;
    }

    private void BuildUi(Image choiceFrame, Text choiceText, Text keyText, Image? keyFrame, Text subtitleText)
    {
        ui = new GameObject("MitaAI native visual UI");
        var canvas = ui.AddComponent(Il2CppType.Of<Canvas>()).TryCast<Canvas>()!;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = ui.AddComponent(Il2CppType.Of<CanvasScaler>()).TryCast<CanvasScaler>()!;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        // Construct only visual components. No native Button, dialogue driver, or story event is copied.
        CreateRoundedSprite();
        prompt = Rect("Nearby Mita", ui.transform, new Vector2(0.5f, 0.5f), new Vector2(390, 72));
        RoundedPanel(prompt, new Color(1, 0.36f, 0.69f, 0.60f), new Color(0.76f, 0.36f, 0.96f, 0.60f), 5);
        prompt.localRotation = Quaternion.Euler(0, 0, -3);
        promptGroup = prompt.gameObject.AddComponent(Il2CppType.Of<CanvasGroup>()).TryCast<CanvasGroup>();
        promptGroup!.blocksRaycasts = false;
        var keyRect = Rect("Key frame", prompt, new Vector2(0.12f, 0.5f), new Vector2(76, 62));
        RoundedImage(keyRect.gameObject, new Color(0.82f, 0.01f, 0.83f, 0.80f), new Color(0.98f, 0.13f, 0.72f, 0.80f));
        promptKey = CopyText("Key", keyRect, keyText, new Vector2(0.5f, 0.5f), new Vector2(70, 60), "E", 34);
        CopyText("Action", prompt, keyText, new Vector2(0.6f, 0.5f), new Vector2(272, 64), "TALK TO MITA", 32);
        composerPanel = Rect("Player subtitle input", ui.transform, new Vector2(0.5f, 0.16f), new Vector2(1800, 164));
        composerGroup = composerPanel.gameObject.AddComponent(Il2CppType.Of<CanvasGroup>()).TryCast<CanvasGroup>();
        composerGroup!.blocksRaycasts = false;
        draft = CopyText("Draft", composerPanel, subtitleText, new Vector2(0.5f, 0.5f), new Vector2(1760, 154), "", 60);
        draft.alignment = TextAnchor.MiddleCenter;
        draft.fontStyle = subtitleText.fontStyle;
        // Use the game's own language-dependent font selection on our editable Text, not a key-hint font.
        var languageFont = draft.gameObject.AddComponent(Il2CppType.Of<UI_TextFontLanguage>()).TryCast<UI_TextFontLanguage>()!;
        languageFont.FontUpdate();
        draft.fontStyle = FontStyle.Normal;
        draft.material = draft.font.material;
        Plugin.Logger.LogInfo($"MITAAI_DRAFT_FONT source=native-language-selector id={draft.font.GetInstanceID()} names={string.Join(",", draft.font.fontNames)} style={draft.fontStyle}");
        ApplySubtitleStyle(draft, new Color(1, 0.6f, 0), Color.white);
        draft.horizontalOverflow = HorizontalWrapMode.Wrap;
        draft.verticalOverflow = VerticalWrapMode.Truncate;
        draft.resizeTextForBestFit = true;
        draft.resizeTextMinSize = 30;
        draft.resizeTextMaxSize = 60;
        subtitlePanel = Rect("Screen subtitle", ui.transform, new Vector2(0.5f, 0.16f), new Vector2(1800, 164));
        subtitleGroup = subtitlePanel.gameObject.AddComponent(Il2CppType.Of<CanvasGroup>()).TryCast<CanvasGroup>();
        subtitleGroup!.blocksRaycasts = false;
        subtitle = CopyText("Subtitle", subtitlePanel, subtitleText, new Vector2(0.5f, 0.5f), new Vector2(1760, 154), "", 60);
        subtitle.font = draft.font;
        subtitle.material = draft.font.material;
        subtitle.fontStyle = FontStyle.Normal;
        subtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
        subtitle.verticalOverflow = VerticalWrapMode.Truncate;
        subtitle.resizeTextForBestFit = true;
        subtitle.resizeTextMinSize = 30;
        subtitle.resizeTextMaxSize = 60;
        subtitleGroup.alpha = 0;
        subtitlePanel.gameObject.SetActive(false);
        promptGroup.alpha = 0;
        composerGroup.alpha = 0;
        prompt.gameObject.SetActive(false);
        composerPanel.gameObject.SetActive(false);
    }

    private static void ApplySubtitleStyle(Text text, Color color, Color outlineColor)
    {
        text.color = color;
        foreach (var item in text.gameObject.GetComponents(Il2CppType.Of<UIGradient>()))
        {
            var gradient = item.TryCast<UIGradient>();
            if (gradient != null) gradient.color1 = gradient.color2 = gradient.color3 = gradient.color4 = color;
        }
        foreach (var item in text.gameObject.GetComponents(Il2CppType.Of<Shadow>()))
        {
            var outline = item.TryCast<Shadow>();
            if (outline == null) continue;
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(2, -2);
        }
        foreach (var item in text.gameObject.GetComponents(Il2CppType.Of<UIShadow>()))
        {
            var outline = item.TryCast<UIShadow>();
            if (outline != null) outline.effectColor = outlineColor;
        }
    }

    private void CreateRoundedSprite()
    {
        roundedSprite = CreateMask(false, out var fillTexture);
        roundedTexture = fillTexture;
        roundedBorderSprite = CreateMask(true, out var borderTexture);
        roundedBorderTexture = borderTexture;
    }

    private static Sprite CreateMask(bool border, out Texture2D texture)
    {
        texture = new Texture2D(RoundedMask.Size, RoundedMask.Size, TextureFormat.RGBA32, false);
        texture.name = border ? "MitaAI rounded border alpha mask" : "MitaAI rounded fill alpha mask";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        var pixels = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Color>(RoundedMask.Size * RoundedMask.Size);
        for (int y = 0; y < RoundedMask.Size; y++)
            for (int x = 0; x < RoundedMask.Size; x++) pixels[y * RoundedMask.Size + x] = new Color(1, 1, 1,
                border ? RoundedMask.BorderAlpha(x, y) : RoundedMask.Alpha(x, y));
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, RoundedMask.Size, RoundedMask.Size),
            new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect,
            new Vector4(RoundedMask.Radius, RoundedMask.Radius, RoundedMask.Radius, RoundedMask.Radius));
    }

    private Image RoundedImage(GameObject obj, Color left, Color right)
    {
        var image = obj.AddComponent(Il2CppType.Of<Image>()).TryCast<Image>()!;
        image.sprite = roundedSprite;
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;
        image.color = Color.white;
        var gradient = obj.AddComponent(Il2CppType.Of<UIGradient>()).TryCast<UIGradient>()!;
        gradient.direction = UIGradient.Direction.Horizontal;
        gradient.color1 = left;
        gradient.color2 = right;
        return image;
    }

    private void RoundedPanel(RectTransform rect, Color left, Color right, float border)
    {
        var outline = RoundedImage(rect.gameObject, new Color(0.77f, 0.27f, 0.96f, 0.78f), new Color(0.85f, 0.45f, 1, 0.78f));
        outline.sprite = roundedBorderSprite;
        var fill = Rect("Fill", rect, new Vector2(0.5f, 0.5f), rect.sizeDelta - new Vector2(border * 2, border * 2));
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = new Vector2(border, border);
        fill.offsetMax = new Vector2(-border, -border);
        RoundedImage(fill.gameObject, left, right);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 size)
    {
        var obj = new GameObject(name);
        var rect = obj.AddComponent(Il2CppType.Of<RectTransform>()).TryCast<RectTransform>()!;
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.sizeDelta = size;
        return rect;
    }

    private static void CopyImage(GameObject obj, Image source)
    {
        var image = obj.AddComponent(Il2CppType.Of<Image>()).TryCast<Image>()!;
        image.sprite = source.sprite;
        image.overrideSprite = source.overrideSprite;
        image.type = source.type;
        var color = source.color;
        if (color.a < 0.01f) color.a = 1;
        image.color = color;
        image.material = source.material;
        image.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        image.raycastTarget = false;
        CopyEffects(obj, source.gameObject);
    }

    private static Text CopyText(string name, Transform parent, Text source, Vector2 anchor, Vector2 size, string value, int fontSize)
    {
        var rect = Rect(name, parent, anchor, size);
        var text = rect.gameObject.AddComponent(Il2CppType.Of<Text>()).TryCast<Text>()!;
        text.font = source.font;
        text.material = source.material;
        text.fontStyle = FontStyle.Bold;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.supportRichText = false;
        text.raycastTarget = false;
        text.text = value;
        foreach (var item in source.gameObject.GetComponents(Il2CppType.Of<Shadow>()))
        {
            var effect = item.TryCast<Shadow>();
            if (effect == null) continue;
            var copy = rect.gameObject.AddComponent(item.GetIl2CppType()).TryCast<Shadow>()!;
            copy.effectColor = effect.effectColor;
            copy.effectDistance = effect.effectDistance;
            copy.useGraphicAlpha = effect.useGraphicAlpha;
        }
        CopyEffects(rect.gameObject, source.gameObject);
        foreach (var item in rect.gameObject.GetComponents(Il2CppType.Of<UIGradient>()))
        {
            var gradient = item.TryCast<UIGradient>();
            if (gradient == null) continue;
            gradient.color1 = gradient.color2 = gradient.color3 = gradient.color4 = Color.white;
        }
        foreach (var item in rect.gameObject.GetComponents(Il2CppType.Of<UIShadow>()))
        {
            var shadow = item.TryCast<UIShadow>();
            if (shadow != null) shadow.effectColor = new Color(0.27f, 0.04f, 0.29f, 0.75f);
        }
        var outline = rect.gameObject.AddComponent(Il2CppType.Of<Outline>()).TryCast<Outline>()!;
        outline.effectColor = new Color(0.30f, 0.05f, 0.28f, 0.5f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);
        return text;
    }

    private static void CopyEffects(GameObject target, GameObject source)
    {
        foreach (var item in source.GetComponents(Il2CppType.Of<UIGradient>()))
        {
            var original = item.TryCast<UIGradient>();
            if (original == null) continue;
            var copy = target.AddComponent(Il2CppType.Of<UIGradient>()).TryCast<UIGradient>()!;
            copy.direction = original.direction;
            copy.color1 = original.color1;
            copy.color2 = original.color2;
            copy.color3 = original.color3;
            copy.color4 = original.color4;
            copy.rotation = original.rotation;
            copy.offset2 = original.offset2;
            copy.gradientStyle = original.gradientStyle;
            copy.colorSpace = original.colorSpace;
            copy.ignoreAspectRatio = original.ignoreAspectRatio;
        }
        foreach (var item in source.GetComponents(Il2CppType.Of<UIShadow>()))
        {
            var original = item.TryCast<UIShadow>();
            if (original == null) continue;
            var copy = target.AddComponent(Il2CppType.Of<UIShadow>()).TryCast<UIShadow>()!;
            copy.style = original.style;
            copy.effectColor = original.effectColor;
            copy.effectDistance = original.effectDistance;
            copy.useGraphicAlpha = original.useGraphicAlpha;
            copy.blurFactor = original.blurFactor;
        }
    }

    internal void ClearReply()
    {
        playback.Clear();
        subtitleAlpha = 0;
        if (subtitlePanel != null) subtitlePanel.gameObject.SetActive(false);
        if (subtitle != null) subtitle.text = "";
    }

    internal void HideComposer()
    {
        composerAlpha = 0;
        if (composerPanel != null) composerPanel.gameObject.SetActive(false);
    }

    internal void Hide()
    {
        CancelTurn();
        ClearReply();
        HideComposer();
        if (prompt != null) prompt.gameObject.SetActive(false);
    }

    internal void Reset()
    {
        CancelTurn();
        ClearReply();
        if (ui != null) Object.Destroy(ui);
        if (roundedSprite != null) Object.Destroy(roundedSprite);
        if (roundedTexture != null) Object.Destroy(roundedTexture);
        if (roundedBorderSprite != null) Object.Destroy(roundedBorderSprite);
        if (roundedBorderTexture != null) Object.Destroy(roundedBorderTexture);
        roundedSprite = null;
        roundedTexture = null;
        roundedBorderSprite = null;
        roundedBorderTexture = null;
        ui = null;
        controller = null;
        subtitle = null;
        subtitlePanel = null;
        subtitleGroup = null;
        composerAlpha = 0;
        nextScan = 0;
    }

    internal void CancelReplyWait()
    {
        awaitingReply = false;
        awaitingExpires = 0;
    }

    private void CancelTurn()
    {
        turn.Cancel();
        turnMita = null;
        turnPlayer = null;
        turnCamera = null;
        awaitingReply = false;
        awaitingExpires = 0;
    }

    private static bool Loaded(Component component) => component.gameObject.scene.IsValid() && component.gameObject.scene.isLoaded;
    private static string Path(Transform transform)
    {
        var names = new List<string>();
        for (var cursor = transform; cursor != null; cursor = cursor.parent) names.Add(cursor.name);
        names.Reverse();
        return string.Join("/", names);
    }
}
