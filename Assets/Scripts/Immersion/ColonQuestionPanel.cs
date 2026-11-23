using System;
using Oncologie.IRL;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Toont een ColonQuestion als zwevend paneel voor de speler en laat kiezen door te KIJKEN:
/// blijf even naar een antwoord kijken (de kaart vult zich) om het te kiezen. Werkt zonder controllers,
/// ook in de editor met de XR Interaction Simulator. In de editor kan het ook met de toetsen 1-4 en Enter.
/// Na het antwoord volgt uitleg: bij een fout antwoord vriendelijk waarom het niet klopt en wat wel klopt.
/// Geen punten, geen afkeuring.
///
/// Uitzicht: afgeronde kaarten met letter-badges (A, B, C), een gele knop, zachte schaduw en een korte
/// open-animatie. De afgeronde vormen worden één keer in code getekend (enkele kleine sprites, geen
/// texture-assets). Tekst via TextMeshPro (scherp in VR) met een overlay-materiaal, zodat het paneel
/// altijd boven de darmwand zichtbaar blijft.
/// </summary>
public class ColonQuestionPanel : MonoBehaviour
{
    [Header("Plaatsing")]
    public float distance = 1.4f;
    [Tooltip("Hoogte (m) van de BOVENRAND van het paneel t.o.v. de ogen. Het paneel groeit naar beneden mee met de inhoud.")]
    public float topHeight = 0.2f;
    [Tooltip("Volgt de speler pas als die verder dan zoveel graden wegkijkt")]
    public float followAngle = 40f;
    public float metersPerPixel = 0.0011f;

    [Header("Kiezen")]
    [Tooltip("Seconden kijken om een antwoord te kiezen")]
    public float dwellSeconds = 1.6f;
    [Tooltip("Pauze na het tonen voor er gekozen kan worden (tegen per ongeluk kiezen)")]
    public float inputDelay = 1.0f;
    public bool allowKeyboard = true;

    [Header("Tekst")]
    [Tooltip("Inter-Regular SDF (VRTemplateAssets). Leeg = TMP-standaardfont.")]
    public TMP_FontAsset font;
    [Tooltip("Overlay-materiaal van dat font, zodat tekst over de darmwand heen tekent")]
    public Material textMaterial;

    [Header("Stijl")]
    public Color panelColor = new Color(0.06f, 0.035f, 0.05f, 0.97f);
    public Color accentColor = new Color(1f, 0.8f, 0.38f);
    public Color correctTint = new Color(0.38f, 0.86f, 0.58f);
    public Color otherTint = new Color(1f, 0.62f, 0.32f);

    [Header("Events")]
    [Tooltip("Vraag, gekozen index, juist? Handig om later anoniem te loggen.")]
    public UnityEvent<ColonQuestion, int, bool> onAnswered;

    const int MaxAnswers = 4;
    const float W = 1100f, H = 860f, Pad = 56f;
    static readonly string[] Letters = { "A", "B", "C", "D" };
    static readonly Color TextMain = new Color(0.97f, 0.95f, 0.96f);
    static readonly Color TextSoft = new Color(0.82f, 0.78f, 0.81f);
    static readonly Color Dark = new Color(0.1f, 0.05f, 0.08f);
    static readonly Color CardIdle = new Color(0.135f, 0.085f, 0.115f, 1f);
    static readonly Color BadgeIdle = new Color(0.25f, 0.18f, 0.22f, 1f);

    // ---- één kaart (antwoord of demo) ----
    class Card
    {
        public RectTransform rt;
        public Image bg, rim, fill, badge;
        public TextMeshProUGUI letter, label;
    }

    Transform head;
    Canvas canvas;
    CanvasGroup group;
    RectTransform topicChip;
    TextMeshProUGUI topicText, questionText, titleText, bodyText;
    readonly Card[] cards = new Card[MaxAnswers];
    RectTransform continueRect, continueFill, reticle;
    Image continueBg;
    TextMeshProUGUI continueLabel;

    // demo op de instructiekaart (niet klikbaar)
    Card demo;
    TextMeshProUGUI demoCaption;
    RectTransform demoDot;
    bool demoOn;
    float demoStart;
    const string DemoIdleText = "Kijk naar een antwoord tot de kaart vol is";

    ColonQuestion current;
    Action onDone;
    bool showingFeedback;
    bool infoMode;           // uitlegkaart zonder antwoorden, alleen een knop
    int hover = -1;
    int blocked = -1;        // waar je al naar keek toen kiezen mogelijk werd: telt pas na even wegkijken
    bool blockSet;
    float hoverTime, readyAt, openedAt;
    int chosenIndex = -1, correctIndex = -1;

    public bool IsOpen => current != null || infoMode;

    Material runtimeTextMaterial;

    void Awake()
    {
        Build();
        canvas.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (runtimeTextMaterial != null) Destroy(runtimeTextMaterial);
    }

    // ================= publieke functies =================

    /// <summary>Stel een vraag. onDone wordt aangeroepen als de speler na de uitleg op 'Verder' kiest.</summary>
    public void Ask(ColonQuestion q, Action done)
    {
        if (q == null || q.answers.Count == 0) { done?.Invoke(); return; }
        current = q; onDone = done; showingFeedback = false; infoMode = false;
        chosenIndex = correctIndex = -1;
        FindHead();
        SetDemoVisible(false);

        SetTopic(string.IsNullOrEmpty(q.topic) ? "Even nadenken" : q.topic);
        questionText.text = q.question;
        titleText.text = ""; bodyText.text = "";
        LayoutAnswers();
        int n = Mathf.Min(q.answers.Count, MaxAnswers);
        SetHeight(AnswersTop + n * AnswerH + (n - 1) * AnswerGap + 52f);
        for (int i = 0; i < MaxAnswers; i++)
        {
            bool used = i < q.answers.Count;
            cards[i].rt.gameObject.SetActive(used);
            if (!used) continue;
            cards[i].label.text = q.answers[i].text;
            StyleCard(cards[i], CardState.Idle, 0f);
        }
        continueRect.gameObject.SetActive(false);
        Open();

        if (q.questionVoice != null)
        {
            var src = GetComponent<AudioSource>();
            if (src == null) { src = gameObject.AddComponent<AudioSource>(); src.spatialBlend = 0f; src.playOnAwake = false; }
            src.PlayOneShot(q.questionVoice);
        }
    }

    /// <summary>
    /// Toon een uitlegkaart met één knop (bv. de instructie hoe je antwoordt).
    /// De speler kiest de knop op dezelfde manier als een antwoord, dus het is meteen een oefening.
    /// </summary>
    public void ShowInfo(string topic, string title, string body, string button, Action done, bool showDemo = false)
    {
        current = null; infoMode = true; onDone = done; showingFeedback = true;
        FindHead();

        SetTopic(topic);
        questionText.text = title;
        titleText.text = "";
        bodyText.text = body;
        for (int i = 0; i < MaxAnswers; i++) cards[i].rt.gameObject.SetActive(false);
        float bottom = showDemo ? 330f : 150f;
        SetHeight(250f + BodyHeight(body) + 30f + bottom);
        SetRect(bodyText.rectTransform, Pad, bottom, Pad, 250f);
        SetDemoVisible(showDemo);
        demoStart = Time.time;

        continueLabel.text = string.IsNullOrEmpty(button) ? "Verder  ›" : button;
        continueRect.gameObject.SetActive(true);
        Open();
    }

    // ================= logica =================

    void FindHead()
    {
        if (head != null) return;
        var cam = WorldUI.MainCamera();
        if (cam != null) head = cam.transform;
    }

    void Open()
    {
        PlaceInFront(true);
        canvas.gameObject.SetActive(true);
        openedAt = Time.time;
        group.alpha = 0f;
        hover = -1; hoverTime = 0f; readyAt = Time.time + inputDelay; blockSet = false;
        StyleButton(false, 0f);
    }

    void Update()
    {
        if (!IsOpen) return;
        PlaceInFront(false);

        // korte open-animatie: vervagen en licht inzoomen
        float a = Mathf.Clamp01((Time.time - openedAt) / 0.28f);
        float e = 1f - (1f - a) * (1f - a) * (1f - a);
        group.alpha = e;
        canvas.transform.localScale = Vector3.one * metersPerPixel * Mathf.Lerp(0.94f, 1f, e);

        if (demoOn) AnimateDemo(Time.time - demoStart);

        int keyPick = allowKeyboard ? KeyboardPick() : -1;
        int target = -1;
        if (Time.time >= readyAt)
        {
            target = GazeTarget();
            if (!blockSet) { blocked = target; blockSet = true; }
            if (target != blocked) blocked = -1;
            else if (blocked >= 0) target = -1;
        }
        UpdateReticle();

        if (target != hover) { hover = target; hoverTime = 0f; }
        else if (hover >= 0) hoverTime += Time.deltaTime;

        if (!showingFeedback)
        {
            for (int i = 0; i < current.answers.Count && i < MaxAnswers; i++)
                StyleCard(cards[i], i == hover ? CardState.Hover : CardState.Idle, i == hover ? hoverTime / dwellSeconds : 0f);
            if (keyPick >= 0 && keyPick < current.answers.Count) Choose(keyPick);
            else if (hover >= 0 && hoverTime >= dwellSeconds) Choose(hover);
        }
        else
        {
            bool onContinue = hover == 100;
            StyleButton(onContinue, onContinue ? hoverTime / dwellSeconds : 0f);
            if (keyPick == 99 || (onContinue && hoverTime >= dwellSeconds)) Close();
        }
    }

    void Choose(int index)
    {
        var q = current;
        chosenIndex = index;
        correctIndex = q.CorrectIndex;
        bool ok = q.answers[index].correct;
        showingFeedback = true;

        for (int i = 0; i < q.answers.Count && i < MaxAnswers; i++)
        {
            bool show = i == index || i == correctIndex;
            cards[i].rt.gameObject.SetActive(show);
            if (show) StyleCard(cards[i], q.answers[i].correct ? CardState.Correct : CardState.Other, 0f);
        }

        // feedback: altijd uitleggen, nooit afkeuren
        if (ok) { titleText.text = "Klopt!"; titleText.color = correctTint; }
        else { titleText.text = "Niet helemaal. Het groene antwoord klopt."; titleText.color = otherTint; }

        var sb = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(q.answers[index].explanation)) sb.Append(q.answers[index].explanation);
        if (!string.IsNullOrEmpty(q.explanation)) { if (sb.Length > 0) sb.Append("\n\n"); sb.Append(q.explanation); }
        bodyText.text = sb.ToString();

        LayoutFeedback();
        continueLabel.text = "Verder  ›";
        continueRect.gameObject.SetActive(true);
        StyleButton(false, 0f);
        hover = -1; hoverTime = 0f; readyAt = Time.time + 2.5f; blockSet = false;   // eerst even lezen

        onAnswered?.Invoke(q, index, ok);
    }

    void Close()
    {
        SetDemoVisible(false);
        canvas.gameObject.SetActive(false);
        current = null; infoMode = false;
        var cb = onDone; onDone = null;
        cb?.Invoke();
    }

    // ================= uitzicht =================

    enum CardState { Idle, Hover, Correct, Other }

    void StyleCard(Card c, CardState s, float progress)
    {
        Color bg, rim, badge, letter;
        switch (s)
        {
            // (project staat in Linear: doorschijnende kleuren vallen veel lichter uit, dus mengen we
            //  de tint zelf met de donkere kaartkleur en houden we de kaart ondoorzichtig)
            case CardState.Hover:
                bg = Color.Lerp(CardIdle, accentColor, 0.14f); rim = WithA(accentColor, 0.9f); badge = accentColor; letter = Dark; break;
            case CardState.Correct:
                bg = Color.Lerp(CardIdle, correctTint, 0.16f); rim = correctTint; badge = correctTint; letter = Dark; break;
            case CardState.Other:
                bg = Color.Lerp(CardIdle, otherTint, 0.14f); rim = otherTint; badge = otherTint; letter = Dark; break;
            default:
                bg = CardIdle; rim = new Color(1f, 1f, 1f, 0.07f); badge = BadgeIdle; letter = TextMain; break;
        }
        bg.a = 1f;
        c.bg.color = bg; c.rim.color = rim; c.badge.color = badge; c.letter.color = letter;
        float k = Mathf.Clamp01(progress);
        c.fill.gameObject.SetActive(k > 0.001f);
        c.fill.rectTransform.anchorMax = new Vector2(k, 1f);
        float scale = s == CardState.Hover ? 1.015f : 1f;
        c.rt.localScale = Vector3.Lerp(c.rt.localScale, Vector3.one * scale, 0.35f);
    }

    void StyleButton(bool hovered, float progress)
    {
        continueBg.color = hovered ? Color.Lerp(accentColor, Color.white, 0.25f) : accentColor;
        float k = Mathf.Clamp01(progress);
        continueFill.gameObject.SetActive(k > 0.001f);
        continueFill.anchorMax = new Vector2(k, 1f);
        continueRect.localScale = Vector3.one * (hovered ? 1.04f : 1f);
    }

    void SetTopic(string t)
    {
        topicText.text = string.IsNullOrEmpty(t) ? "" : t.ToUpperInvariant();
        topicChip.gameObject.SetActive(!string.IsNullOrEmpty(t));
        float w = topicText.GetPreferredValues(topicText.text, 1000f, 44f).x + 44f;
        topicChip.sizeDelta = new Vector2(w, 44f);
    }

    void SetDemoVisible(bool on)
    {
        demoOn = on;
        if (demo == null) return;
        demo.rt.gameObject.SetActive(on);
        demoCaption.gameObject.SetActive(on);
    }

    // Lus van ~4,5 s: puntje beweegt naar het antwoord, de kaart vult zich, antwoord wordt gekozen, opnieuw.
    void AnimateDemo(float t)
    {
        const float move = 1.0f, pause = 0.3f, chosen = 1.1f, rest = 0.6f;
        float fillTime = dwellSeconds;
        float period = move + pause + fillTime + chosen + rest;
        t %= period;

        float w = demo.rt.rect.width;
        Vector2 start = new Vector2(-w * 0.38f, 120f);
        Vector2 end = new Vector2(w * 0.05f, 0f);

        if (t < move)
        {
            demoDot.anchoredPosition = Vector2.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t / move));
            StyleCard(demo, CardState.Idle, 0f); demo.label.text = DemoIdleText;
        }
        else if (t < move + pause + fillTime)
        {
            demoDot.anchoredPosition = end;
            StyleCard(demo, CardState.Hover, (t - move - pause) / fillTime);
        }
        else if (t < move + pause + fillTime + chosen)
        {
            demoDot.anchoredPosition = end;
            StyleCard(demo, CardState.Correct, 0f); demo.label.text = "Gekozen!";
        }
        else
        {
            demoDot.anchoredPosition = start;
            StyleCard(demo, CardState.Idle, 0f); demo.label.text = DemoIdleText;
        }
    }

    // ================= kijken =================

    int GazeTarget()
    {
        if (head == null || !GazePoint(out Vector3 p)) return -1;
        if (!showingFeedback)
        {
            for (int i = 0; i < MaxAnswers; i++)
                if (cards[i].rt.gameObject.activeSelf && Contains(cards[i].rt, p)) return i;
        }
        else if (continueRect.gameObject.activeSelf && Contains(continueRect, p)) return 100;
        return -1;
    }

    bool GazePoint(out Vector3 p)
    {
        p = Vector3.zero;
        var plane = new Plane(canvas.transform.forward, canvas.transform.position);
        var ray = new Ray(head.position, head.forward);
        if (!plane.Raycast(ray, out float d)) return false;
        p = ray.GetPoint(d);
        return true;
    }

    static bool Contains(RectTransform rt, Vector3 world)
    {
        Vector3 local = rt.InverseTransformPoint(world);
        return rt.rect.Contains(new Vector2(local.x, local.y));
    }

    void UpdateReticle()
    {
        if (head != null && GazePoint(out Vector3 p) && Contains((RectTransform)canvas.transform, p))
        {
            reticle.gameObject.SetActive(true);
            reticle.position = p;
        }
        else reticle.gameObject.SetActive(false);
    }

    int KeyboardPick()
    {
        var kb = Keyboard.current;
        if (kb == null) return -1;
        if (!showingFeedback)
        {
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) return 0;
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) return 1;
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) return 2;
            if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) return 3;
        }
        else if (Time.time >= readyAt && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            return 99;
        return -1;
    }

    // ================= plaatsen =================

    void PlaceInFront(bool snap)
    {
        if (head == null) return;
        Vector3 fwd = head.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        fwd.Normalize();

        Vector3 target = head.position + fwd * distance + Vector3.up * topHeight;
        Transform t = canvas.transform;
        if (!snap)
        {
            Vector3 toPanel = t.position - head.position; toPanel.y = 0f;
            if (Vector3.Angle(toPanel, fwd) < followAngle) return;
            target = Vector3.Lerp(t.position, target, 1f - Mathf.Exp(-3f * Time.deltaTime));
        }
        t.position = target;
        Vector3 look = t.position - head.position; look.y = 0f;
        if (look.sqrMagnitude > 1e-4f) t.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
    }

    // ================= opbouwen =================

    void Build()
    {
        if (font == null) font = TMP_Settings.defaultFontAsset;
        if (textMaterial == null && font != null) textMaterial = font.material;
        // tekst moet NA de vlakken getekend worden (die zitten op wachtrij 4010, zie WorldUI.OverlayUIMaterial)
        if (textMaterial != null)
        {
            textMaterial = new Material(textMaterial) { name = textMaterial.name + " (Panel)" };
            textMaterial.renderQueue = WorldUI.OverlayUIMaterial.renderQueue + 10;
            runtimeTextMaterial = textMaterial;
        }

        canvas = WorldUI.CreateCanvas("QuestionCanvas", transform, new Vector2(W, H), metersPerPixel);
        group = canvas.GetComponent<CanvasGroup>();
        ((RectTransform)canvas.transform).pivot = new Vector2(0.5f, 1f);   // bovenrand blijft op zijn plaats, paneel groeit naar beneden
        var cs = canvas.GetComponent<CanvasScaler>();
        cs.referencePixelsPerUnit = 100f;
        Transform root = canvas.transform;

        // schaduw, rand en achtergrond
        Img(root, UISprites.Soft, new Color(0f, 0f, 0f, 0.55f), Vector2.zero, Vector2.one, new Vector2(-34, -48), new Vector2(34, 20), 1f);
        Img(root, UISprites.Rounded, panelColor, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 0.8f);
        Img(root, UISprites.Outline, new Color(1f, 1f, 1f, 0.1f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 0.8f);
        // dun accentlijntje bovenaan
        Img(root, UISprites.Rounded, WithA(accentColor, 0.9f), new Vector2(0, 1), new Vector2(0, 1), new Vector2(Pad, -14), new Vector2(Pad + 90, -8), 6f);

        // onderwerp-label (pil)
        var chip = Img(root, UISprites.Rounded, WithA(accentColor, 0.14f), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, Vector2.zero, 2.5f);
        topicChip = chip.rectTransform;
        topicChip.pivot = new Vector2(0, 1);
        topicChip.anchoredPosition = new Vector2(Pad, -40);
        topicText = Txt(topicChip, "", 21, accentColor, TextAlignmentOptions.Center, FontStyles.Bold);
        topicText.characterSpacing = 8f;
        Stretch(topicText.rectTransform, 0, 0, 0, 0);

        questionText = Txt(root, "", 42, TextMain, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        questionText.lineSpacing = -4f;
        SetRect(questionText.rectTransform, Pad, -1f, Pad, 108f, 130f);

        for (int i = 0; i < MaxAnswers; i++) cards[i] = MakeCard(root, Letters[i], 31);
        LayoutAnswers();

        titleText = Txt(root, "", 34, accentColor, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        bodyText = Txt(root, "", 29, TextSoft, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        bodyText.lineSpacing = 6f;

        // knop
        continueBg = Img(root, UISprites.Rounded, accentColor, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-180, 40), new Vector2(180, 116), 1.4f);
        continueRect = continueBg.rectTransform;
        var cf = Img(continueRect, UISprites.Rounded, new Color(1f, 1f, 1f, 0.45f), Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero, 1.4f);
        continueFill = cf.rectTransform;
        continueLabel = Txt(continueRect, "Verder  ›", 32, Dark, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(continueLabel.rectTransform, 0, 0, 0, 0);

        // demo (instructiekaart)
        demoCaption = Txt(root, "VOORBEELD", 20, WithA(accentColor, 0.9f), TextAlignmentOptions.BottomLeft, FontStyles.Bold);
        demoCaption.characterSpacing = 8f;
        var dc = demoCaption.rectTransform;
        dc.anchorMin = new Vector2(0, 0); dc.anchorMax = new Vector2(1, 0);
        dc.offsetMin = new Vector2(Pad + 40, 266); dc.offsetMax = new Vector2(-Pad - 40, 296);
        demo = MakeCard(root, "A", 29);
        demo.rt.anchorMin = new Vector2(0, 0); demo.rt.anchorMax = new Vector2(1, 0);
        demo.rt.offsetMin = new Vector2(Pad + 40, 156); demo.rt.offsetMax = new Vector2(-Pad - 40, 252);
        demoDot = MakeDot(demo.rt, 0.9f);
        SetDemoVisible(false);

        // kijkpuntje
        reticle = MakeDot(root, 1f);
    }

    Card MakeCard(Transform parent, string letter, int fontSize)
    {
        var c = new Card();
        c.bg = Img(parent, UISprites.Rounded, Color.white, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero, 1.6f);
        c.rt = c.bg.rectTransform;
        c.fill = Img(c.rt, UISprites.Rounded, WithA(accentColor, 0.16f), Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero, 1.6f);
        c.rim = Img(c.rt, UISprites.Outline, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 1.6f);
        c.badge = Img(c.rt, UISprites.Circle, Color.white, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, -28), new Vector2(78, 28), 1f);
        c.letter = Txt(c.badge.rectTransform, letter, 27, TextMain, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(c.letter.rectTransform, 0, 0, 0, 0);
        c.label = Txt(c.rt, "", fontSize, TextMain, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
        Stretch(c.label.rectTransform, 100, 10, 28, 10);
        return c;
    }

    RectTransform MakeDot(Transform parent, float alpha)
    {
        var outer = Img(parent, UISprites.Circle, new Color(0f, 0f, 0f, 0.45f * alpha), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-13, -13), new Vector2(13, 13), 1f);
        Img(outer.rectTransform, UISprites.Circle, new Color(1f, 1f, 1f, alpha), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-8, -8), new Vector2(8, 8), 1f);
        return outer.rectTransform;
    }

    const float AnswersTop = 262f, AnswerH = 96f, AnswerGap = 16f;

    void LayoutAnswers()
    {
        for (int i = 0; i < MaxAnswers; i++)
        {
            float top = -AnswersTop - i * (AnswerH + AnswerGap);
            cards[i].rt.offsetMin = new Vector2(Pad, top - AnswerH);
            cards[i].rt.offsetMax = new Vector2(-Pad, top);
            cards[i].rt.localScale = Vector3.one;
        }
    }

    void SetHeight(float h)
    {
        ((RectTransform)canvas.transform).sizeDelta = new Vector2(W, Mathf.Clamp(h, 420f, 1400f));
    }

    float BodyHeight(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0f;
        return bodyText.GetPreferredValues(text, W - 2f * Pad, 0f).y;
    }

    // In de uitleg-stand: gekozen en juiste antwoord bovenaan (compact), daaronder titel en uitleg
    void LayoutFeedback()
    {
        float top = -258f;
        const float h = 80f, gap = 12f;
        for (int i = 0; i < MaxAnswers; i++)
        {
            if (!cards[i].rt.gameObject.activeSelf) continue;
            cards[i].rt.offsetMin = new Vector2(Pad, top - h);
            cards[i].rt.offsetMax = new Vector2(-Pad, top);
            cards[i].rt.localScale = Vector3.one;
            top -= h + gap;
        }
        float t = -top + 14f;
        SetHeight(t + 54f + BodyHeight(bodyText.text) + 36f + 150f);
        SetRect(titleText.rectTransform, Pad, -1f, Pad, t, 46f);
        SetRect(bodyText.rectTransform, Pad, 150f, Pad, t + 54f);
    }

    // ================= hulpjes =================

    Image Img(Transform parent, Sprite sprite, Color color, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, float ppuMultiplier)
    {
        var go = new GameObject("Img", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = ppuMultiplier;
        img.color = color;
        img.raycastTarget = false;
        img.material = WorldUI.OverlayUIMaterial;   // altijd zichtbaar, ook als de darmwand ervoor zit
        return img;
    }

    TextMeshProUGUI Txt(Transform parent, string text, float size, Color color, TextAlignmentOptions align, FontStyles style)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        if (textMaterial != null) t.fontSharedMaterial = textMaterial;
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        t.richText = true;
        return t;
    }

    static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
    }

    /// <summary>Rechthoek ten opzichte van de randen van het paneel. bottom &lt; 0 = vaste hoogte vanaf top.</summary>
    static void SetRect(RectTransform rt, float left, float bottom, float right, float top, float height = 0f)
    {
        if (bottom < 0f)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(left, -top - height); rt.offsetMax = new Vector2(-right, -top);
        }
        else Stretch(rt, left, bottom, right, top);
    }

    static Color WithA(Color c, float a) { c.a = a; return c; }

    /// <summary>
    /// Kleine, in code getekende sprites (afgeronde rechthoek, rand, cirkel, zachte schaduw).
    /// Eén keer aangemaakt en gedeeld; samen een paar honderd KB, geen texture-assets nodig.
    /// </summary>
    static class UISprites
    {
        static Sprite rounded, outline, circle, soft;
        public static Sprite Rounded => rounded != null ? rounded : (rounded = Make(128, 40f, 0f, 1.2f, 44));
        public static Sprite Outline => outline != null ? outline : (outline = Make(128, 40f, 3f, 1.2f, 44));
        public static Sprite Circle => circle != null ? circle : (circle = Make(64, 32f, 0f, 1.2f, 0));
        public static Sprite Soft => soft != null ? soft : (soft = Make(128, 40f, 0f, 28f, 60));

        // radius: hoekstraal (px), ring > 0: alleen een rand van die dikte, feather: zachte rand (px)
        static Sprite Make(int size, float radius, float ring, float feather, int border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float half = size * 0.5f;
            float inset = feather > 2f ? feather : 0f;   // schaduw: ruimte voor de zachte rand
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // afstand tot een afgeronde rechthoek (negatief = binnen)
                float qx = Mathf.Abs(x + 0.5f - half) - (half - radius - inset);
                float qy = Mathf.Abs(y + 0.5f - half) - (half - radius - inset);
                float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                float a = Mathf.Clamp01(0.5f - outside / feather);
                if (ring > 0f) a *= Mathf.Clamp01(0.5f + (outside + ring) / feather);
                if (feather > 2f) a = a * a;   // schaduw: zachter verloop
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }
    }
}
