using System;
using Oncologie.IRL;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Toont een ColonQuestion als zwevend paneel voor de speler en laat kiezen door te KIJKEN:
/// blijf even naar een antwoord kijken (balkje loopt vol) om het te kiezen. Werkt zonder controllers,
/// ook in de editor met de XR Interaction Simulator. In de editor kan het ook met de toetsen 1-4 en Enter.
/// Na het antwoord volgt uitleg: bij een fout antwoord vriendelijk waarom het niet klopt en waarom het
/// juiste antwoord wel klopt. Geen punten, geen afkeuring.
/// Het paneel wordt bij de start zelf opgebouwd (geen prefab of textures nodig) en tekent over de darmwand heen.
/// </summary>
public class ColonQuestionPanel : MonoBehaviour
{
    [Header("Plaatsing")]
    public float distance = 1.4f;
    [Tooltip("Negatief = paneel iets lager, zodat je bij het openen naar de vraag kijkt en niet naar een antwoord")]
    public float heightOffset = -0.22f;
    [Tooltip("Volgt de speler pas als die verder dan zoveel graden wegkijkt")]
    public float followAngle = 40f;
    public float metersPerPixel = 0.0011f;

    [Header("Kiezen")]
    [Tooltip("Seconden kijken om een antwoord te kiezen")]
    public float dwellSeconds = 1.6f;
    [Tooltip("Pauze na het tonen voor er gekozen kan worden (tegen per ongeluk kiezen)")]
    public float inputDelay = 1.0f;
    public bool allowKeyboard = true;

    [Header("Kleuren")]
    public Color background = new Color(0.06f, 0.03f, 0.05f, 0.9f);
    public Color answerIdle = new Color(1f, 1f, 1f, 0.08f);
    public Color answerHover = new Color(1f, 1f, 1f, 0.2f);
    public Color correctColor = new Color(0.35f, 0.8f, 0.5f, 0.55f);
    public Color otherColor = new Color(0.95f, 0.68f, 0.3f, 0.5f);
    public Color accent = new Color(1f, 0.82f, 0.35f);

    [Header("Events")]
    [Tooltip("Vraag, gekozen index, juist? Handig om later anoniem te loggen.")]
    public UnityEvent<ColonQuestion, int, bool> onAnswered;

    const int MaxAnswers = 4;
    const float W = 1100f, H = 860f;

    Transform head;
    Canvas canvas;
    Text topicText, questionText, titleText, bodyText;
    readonly RectTransform[] answerRects = new RectTransform[MaxAnswers];
    readonly Image[] answerBg = new Image[MaxAnswers];
    readonly Text[] answerText = new Text[MaxAnswers];
    readonly RectTransform[] answerFill = new RectTransform[MaxAnswers];
    RectTransform continueRect, continueFill, reticle;
    Image continueBg;
    Text continueLabel;

    ColonQuestion current;
    Action onDone;
    bool showingFeedback;
    bool infoMode;           // uitlegkaart zonder antwoorden, alleen een knop

    // geanimeerde demo op de instructiekaart (niet klikbaar)
    GameObject[] demoRoot;
    RectTransform demoRow, demoFill, demoDot;
    Image demoBg;
    Text demoText;
    bool demoOn;
    float demoStart;
    const string DemoIdleText = "Kijk naar een antwoord tot het balkje vol is";
    int hover = -1;
    int blocked = -1;        // waar je al naar keek toen kiezen mogelijk werd: telt pas na even wegkijken
    bool blockSet;
    float hoverTime, readyAt;

    public bool IsOpen => current != null || infoMode;

    /// <summary>
    /// Toon een uitlegkaart met één knop (bv. de instructie hoe je antwoordt).
    /// De speler kiest de knop op dezelfde manier als een antwoord, dus het is meteen een oefening.
    /// </summary>
    public void ShowInfo(string topic, string title, string body, string button, Action done, bool showDemo = false)
    {
        current = null; infoMode = true; onDone = done; showingFeedback = true;
        if (head == null) { var cam = WorldUI.MainCamera(); if (cam != null) head = cam.transform; }

        topicText.text = topic;
        questionText.text = title;
        titleText.text = "";
        bodyText.text = body;
        for (int i = 0; i < MaxAnswers; i++) answerRects[i].gameObject.SetActive(false);
        titleText.rectTransform.offsetMin = new Vector2(50, -210); titleText.rectTransform.offsetMax = new Vector2(-50, -200);
        bodyText.rectTransform.offsetMax = new Vector2(-50, -200);
        bodyText.rectTransform.offsetMin = new Vector2(50, showDemo ? 300 : 120);   // ruimte voor de demo
        SetDemoVisible(showDemo);
        demoStart = Time.time;

        continueLabel.text = string.IsNullOrEmpty(button) ? "Verder  ›" : button;
        continueRect.gameObject.SetActive(true);
        SetFill(continueFill, 0f);
        PlaceInFront(true);
        canvas.gameObject.SetActive(true);
        hover = -1; hoverTime = 0f; readyAt = Time.time + inputDelay; blockSet = false;
    }

    void Awake()
    {
        Build();
        canvas.gameObject.SetActive(false);
    }

    /// <summary>Stel een vraag. onDone wordt aangeroepen als de speler na de uitleg op 'Verder' kiest.</summary>
    public void Ask(ColonQuestion q, Action done)
    {
        if (q == null || q.answers.Count == 0) { done?.Invoke(); return; }
        current = q; onDone = done; showingFeedback = false; infoMode = false;
        SetDemoVisible(false);
        bodyText.rectTransform.offsetMin = new Vector2(50, 120);
        continueLabel.text = "Verder  ›";
        if (head == null) { var cam = WorldUI.MainCamera(); if (cam != null) head = cam.transform; }

        topicText.text = string.IsNullOrEmpty(q.topic) ? "Even nadenken" : q.topic;
        questionText.text = q.question;
        titleText.text = ""; bodyText.text = "";
        LayoutAnswers();
        for (int i = 0; i < MaxAnswers; i++)
        {
            bool used = i < q.answers.Count;
            answerRects[i].gameObject.SetActive(used);
            if (!used) continue;
            answerText[i].text = (i + 1) + ".  " + q.answers[i].text;
            answerBg[i].color = answerIdle;
            SetFill(answerFill[i], 0f);
        }
        continueRect.gameObject.SetActive(false);
        PlaceInFront(true);
        canvas.gameObject.SetActive(true);
        hover = -1; hoverTime = 0f; readyAt = Time.time + inputDelay; blockSet = false;

        if (q.questionVoice != null)
        {
            var src = GetComponent<AudioSource>();
            if (src == null) { src = gameObject.AddComponent<AudioSource>(); src.spatialBlend = 0f; src.playOnAwake = false; }
            src.PlayOneShot(q.questionVoice);
        }
    }

    void Update()
    {
        if (!IsOpen) return;
        PlaceInFront(false);
        if (demoOn) AnimateDemo(Time.time - demoStart);

        int keyPick = allowKeyboard ? KeyboardPick() : -1;
        int target = -1;
        if (Time.time >= readyAt)
        {
            target = GazeTarget();
            // niet per ongeluk kiezen: wat al onder je blik lag bij het openen telt pas als je eerst even wegkijkt
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
            {
                answerBg[i].color = i == hover ? answerHover : answerIdle;
                SetFill(answerFill[i], i == hover ? hoverTime / dwellSeconds : 0f);
            }
            if (keyPick >= 0 && keyPick < current.answers.Count) Choose(keyPick);
            else if (hover >= 0 && hoverTime >= dwellSeconds) Choose(hover);
        }
        else
        {
            bool onContinue = hover == 100;
            continueBg.color = onContinue ? answerHover : answerIdle;
            SetFill(continueFill, onContinue ? hoverTime / dwellSeconds : 0f);
            if (keyPick == 99 || (onContinue && hoverTime >= dwellSeconds)) Close();
        }
    }

    void Choose(int index)
    {
        var q = current;
        int correct = q.CorrectIndex;
        bool ok = q.answers[index].correct;
        showingFeedback = true;

        for (int i = 0; i < q.answers.Count && i < MaxAnswers; i++)
        {
            SetFill(answerFill[i], 0f);
            bool show = i == index || i == correct;
            answerRects[i].gameObject.SetActive(show);
            if (show) answerBg[i].color = q.answers[i].correct ? correctColor : otherColor;
        }

        // feedback: altijd uitleggen, nooit afkeuren
        // (het juiste antwoord staat in het groen erboven, het gekozen antwoord in het oranje)
        var sb = new System.Text.StringBuilder();
        if (ok)
        {
            titleText.text = "Klopt!";
            titleText.color = new Color(0.55f, 0.95f, 0.65f);
        }
        else
        {
            titleText.text = "Niet helemaal. Het groene antwoord klopt.";
            titleText.color = accent;
        }
        if (!string.IsNullOrEmpty(q.answers[index].explanation)) sb.Append(q.answers[index].explanation);
        if (!string.IsNullOrEmpty(q.explanation)) { if (sb.Length > 0) sb.Append("\n\n"); sb.Append(q.explanation); }
        bodyText.text = sb.ToString();

        LayoutFeedback(index, correct);
        continueRect.gameObject.SetActive(true);
        SetFill(continueFill, 0f);
        hover = -1; hoverTime = 0f; readyAt = Time.time + 2.5f; blockSet = false;   // eerst even lezen

        onAnswered?.Invoke(q, index, ok);
    }

    void SetDemoVisible(bool on)
    {
        demoOn = on;
        if (demoRoot == null) return;
        foreach (var g in demoRoot) g.SetActive(on);
    }

    // Lus van ~4,5 s: puntje beweegt naar het antwoord, balkje loopt vol, antwoord wordt gekozen, opnieuw.
    void AnimateDemo(float t)
    {
        const float move = 1.0f, pause = 0.3f, chosen = 1.1f, rest = 0.6f;
        float fillTime = dwellSeconds;
        float period = move + pause + fillTime + chosen + rest;
        t %= period;

        float w = demoRow.rect.width;
        Vector2 start = new Vector2(-w * 0.42f, 110f);   // boven het antwoord, links
        Vector2 end = new Vector2(-w * 0.05f, 0f);       // midden op het antwoord

        if (t < move)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / move);
            demoDot.anchoredPosition = Vector2.Lerp(start, end, k);
            demoBg.color = answerIdle; SetFill(demoFill, 0f); demoText.text = DemoIdleText;
        }
        else if (t < move + pause + fillTime)
        {
            demoDot.anchoredPosition = end;
            demoBg.color = answerHover;
            SetFill(demoFill, Mathf.Clamp01((t - move - pause) / fillTime));
        }
        else if (t < move + pause + fillTime + chosen)
        {
            demoDot.anchoredPosition = end;
            demoBg.color = correctColor; SetFill(demoFill, 0f);
            demoText.text = "Gekozen!";
        }
        else
        {
            demoDot.anchoredPosition = start;
            demoBg.color = answerIdle; SetFill(demoFill, 0f); demoText.text = DemoIdleText;
        }
    }

    void Close()
    {
        SetDemoVisible(false);
        canvas.gameObject.SetActive(false);
        current = null; infoMode = false;
        var cb = onDone; onDone = null;
        cb?.Invoke();
    }

    // ---------- kijken ----------
    int GazeTarget()
    {
        if (head == null) return -1;
        if (!GazePoint(out Vector3 p)) return -1;
        if (!showingFeedback)
        {
            for (int i = 0; i < MaxAnswers; i++)
                if (answerRects[i].gameObject.activeSelf && Contains(answerRects[i], p)) return i;
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

    // ---------- plaatsen ----------
    void PlaceInFront(bool snap)
    {
        if (head == null) return;
        Vector3 fwd = head.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        fwd.Normalize();

        Vector3 target = head.position + fwd * distance + Vector3.up * heightOffset;
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

    // ---------- opbouwen ----------
    void Build()
    {
        canvas = WorldUI.CreateCanvas("QuestionCanvas", transform, new Vector2(W, H), metersPerPixel);
        Transform root = canvas.transform;
        WorldUI.AddPanel(root, background, Vector2.zero, Vector2.one);

        topicText = WorldUI.AddText(root, "", 26, accent, TextAnchor.UpperLeft,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(50, -70), new Vector2(-50, -30), FontStyle.Bold);
        questionText = WorldUI.AddText(root, "", 40, Color.white, TextAnchor.UpperLeft,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(50, -200), new Vector2(-50, -80));

        for (int i = 0; i < MaxAnswers; i++)
        {
            var bg = WorldUI.AddPanel(root, answerIdle, new Vector2(0, 1), new Vector2(1, 1));
            var rt = bg.rectTransform;
            answerRects[i] = rt; answerBg[i] = bg;
            answerText[i] = WorldUI.AddText(rt, "", 32, Color.white, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.one, new Vector2(24, 8), new Vector2(-24, -8));
            var fill = WorldUI.AddPanel(rt, accent, Vector2.zero, new Vector2(0, 0), Vector2.zero, new Vector2(0, 6));
            answerFill[i] = fill.rectTransform;
            fill.rectTransform.anchorMax = new Vector2(0, 0);
            fill.rectTransform.offsetMax = new Vector2(0, 6);
        }
        LayoutAnswers();

        titleText = WorldUI.AddText(root, "", 34, accent, TextAnchor.UpperLeft,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(50, -400), new Vector2(-50, -355), FontStyle.Bold);
        bodyText = WorldUI.AddText(root, "", 30, new Color(0.92f, 0.92f, 0.92f), TextAnchor.UpperLeft,
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(50, 120), new Vector2(-50, -410));

        continueBg = WorldUI.AddPanel(root, answerIdle, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-170, 30), new Vector2(170, 100));
        continueRect = continueBg.rectTransform;
        continueLabel = WorldUI.AddText(continueRect, "Verder  ›", 32, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, FontStyle.Bold);
        var cf = WorldUI.AddPanel(continueRect, accent, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(0, 6));
        continueFill = cf.rectTransform;

        // demo voor de instructiekaart: een voorbeeldantwoord met een puntje dat ernaar "kijkt"
        var demoLabel = WorldUI.AddText(root, "Voorbeeld", 24, accent, TextAnchor.LowerLeft,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(160, 250), new Vector2(-160, 285), FontStyle.Bold);
        var dRow = WorldUI.AddPanel(root, answerIdle, new Vector2(0, 0), new Vector2(1, 0), new Vector2(160, 150), new Vector2(-160, 245));
        demoRow = dRow.rectTransform; demoBg = dRow;
        demoText = WorldUI.AddText(demoRow, "Kijk naar een antwoord tot het balkje vol is", 30, Color.white, TextAnchor.MiddleLeft,
            Vector2.zero, Vector2.one, new Vector2(24, 8), new Vector2(-24, -8));
        var dFill = WorldUI.AddPanel(demoRow, accent, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(0, 6));
        demoFill = dFill.rectTransform;
        var dDot = WorldUI.AddPanel(demoRow, new Color(1f, 1f, 1f, 0.95f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-8, -8), new Vector2(8, 8));
        demoDot = dDot.rectTransform;
        demoRoot = new GameObject[] { demoLabel.gameObject, dRow.gameObject };
        SetDemoVisible(false);

        var ret = WorldUI.AddPanel(root, new Color(1f, 1f, 1f, 0.85f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-7, -7), new Vector2(7, 7));
        reticle = ret.rectTransform;
        ret.raycastTarget = false;

        WorldUI.MakeOverlay(canvas);   // altijd zichtbaar, ook als de darmwand ervoor zit
    }

    void LayoutAnswers()
    {
        for (int i = 0; i < MaxAnswers; i++)
        {
            float top = -220f - i * 112f;
            answerRects[i].offsetMin = new Vector2(50, top - 96f);
            answerRects[i].offsetMax = new Vector2(-50, top);
        }
    }

    // In de uitleg-stand: gekozen en juiste antwoord bovenaan, compact
    void LayoutFeedback(int chosen, int correct)
    {
        float top = -210f;
        for (int i = 0; i < MaxAnswers; i++)
        {
            if (!answerRects[i].gameObject.activeSelf) continue;
            answerRects[i].offsetMin = new Vector2(50, top - 64f);
            answerRects[i].offsetMax = new Vector2(-50, top);
            top -= 72f;
        }
        float titleTop = top - 10f;
        titleText.rectTransform.offsetMax = new Vector2(-50, titleTop);
        titleText.rectTransform.offsetMin = new Vector2(50, titleTop - 45f);
        bodyText.rectTransform.offsetMax = new Vector2(-50, titleTop - 55f);
    }

    static void SetFill(RectTransform fill, float k)
    {
        fill.anchorMax = new Vector2(Mathf.Clamp01(k), 0f);
    }
}
