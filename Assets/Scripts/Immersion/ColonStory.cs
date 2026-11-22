using System.Collections;
using System.Collections.Generic;
using Oncologie.Dialogue;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Vertelt het verhaal tijdens de wandeling door de darm, via de bestaande DialogueManager + SubtitleUI.
///  - een intro bij de start,
///  - bij elke poliep een eigen dialoog (gekoppeld per stadium),
///  - een slot aan het einde van de darm.
/// De teksten zelf staan in de DialogueManager van deze scène (Inspector), met per regel een slot voor
/// een voice-over clip. Zonder clip blijft de tekst 'displayDuration' seconden staan.
/// Elke dialoog speelt maar één keer. Komt de speler bij een volgende poliep terwijl er nog iets
/// verteld wordt, dan wacht die dialoog netjes zijn beurt af.
/// </summary>
public class ColonStory : MonoBehaviour
{
    public enum Trigger { Arrive, Inspected }

    [System.Serializable]
    public class StationLine
    {
        [Range(1, 4)] public int stage = 1;
        [Tooltip("Sleutel van de reeks in de DialogueManager")]
        public string sequenceKey = "";
        [Tooltip("Optioneel: leervraag na de dialoog (Create > Oncologie > Darm-vraag)")]
        public ColonQuestion question;
    }

    [Header("Referenties (leeg = automatisch zoeken)")]
    public ColonPathRider rider;
    public ColonQuestionPanel questionPanel;

    [Header("Intro en slot")]
    public string introKey = "Darm_Intro";
    [Tooltip("Seconden na de start voor de intro begint")]
    public float introDelay = 1.5f;
    public string endKey = "Darm_Einde";

    [Header("Poliepen")]
    public List<StationLine> stations = new List<StationLine>
    {
        new StationLine { stage = 1, sequenceKey = "Darm_Poliep1" },
        new StationLine { stage = 2, sequenceKey = "Darm_Poliep2" },
        new StationLine { stage = 3, sequenceKey = "Darm_Poliep3" },
        new StationLine { stage = 4, sequenceKey = "Darm_Poliep4" },
    };
    [Tooltip("Arrive = zodra je bij de poliep bent. Inspected = pas als je er even naar kijkt.")]
    public Trigger trigger = Trigger.Inspected;
    [Tooltip("Alleen bij Inspected: start toch na zoveel seconden als de speler niet kijkt")]
    public float inspectFallback = 4f;

    [Header("Beweging")]
    [Tooltip("Zet de speler stil zolang er bij een poliep verteld wordt")]
    public bool holdPlayerDuringStation = true;

    [Header("Events")]
    public UnityEvent<string> onStoryStarted;
    public UnityEvent<string> onStoryFinished;

    readonly Queue<string> queue = new Queue<string>();
    readonly HashSet<string> played = new HashSet<string>();
    readonly HashSet<string> holdKeys = new HashSet<string>();
    bool busy;

    void Start()
    {
        if (rider == null) rider = FindAnyObjectByType<ColonPathRider>();
        if (questionPanel == null) questionPanel = FindAnyObjectByType<ColonQuestionPanel>();
        if (DialogueManager.Instance == null)
            Debug.LogWarning("ColonStory: geen DialogueManager in de scène, er wordt niets verteld.");

        foreach (var st in FindObjectsByType<PolypStation>(FindObjectsInactive.Include))
        {
            string key = KeyForStage(st.stage);
            if (string.IsNullOrEmpty(key)) continue;
            holdKeys.Add(key);
            if (trigger == Trigger.Arrive)
                st.onArrive.AddListener(() => Request(key));
            else
            {
                st.onInspected.AddListener(() => Request(key));
                st.onArrive.AddListener(() => StartCoroutine(Fallback(key)));
            }
        }

        if (rider != null && !string.IsNullOrEmpty(endKey)) rider.onReachedEnd.AddListener(() => Request(endKey));
        if (!string.IsNullOrEmpty(introKey)) StartCoroutine(PlayIntro());
    }

    string KeyForStage(int stage)
    {
        foreach (var s in stations) if (s.stage == stage) return s.sequenceKey;
        return null;
    }

    ColonQuestion QuestionForKey(string key)
    {
        foreach (var s in stations) if (s.sequenceKey == key) return s.question;
        return null;
    }

    IEnumerator PlayIntro()
    {
        yield return new WaitForSeconds(introDelay);
        Request(introKey);
    }

    IEnumerator Fallback(string key)
    {
        yield return new WaitForSeconds(inspectFallback);
        Request(key);   // doet niets als hij al gespeeld of in de wachtrij staat
    }

    /// <summary>Vraag een dialoog aan. Speelt maar één keer; wacht als er al iets verteld wordt.</summary>
    public void Request(string key)
    {
        if (string.IsNullOrEmpty(key) || played.Contains(key)) return;
        played.Add(key);
        queue.Enqueue(key);
        if (!busy) StartCoroutine(RunQueue());
    }

    IEnumerator RunQueue()
    {
        busy = true;
        while (queue.Count > 0)
        {
            string key = queue.Dequeue();
            var dm = DialogueManager.Instance;
            if (dm == null) break;

            bool hold = holdPlayerDuringStation && holdKeys.Contains(key) && rider != null;
            if (hold) rider.SetInputEnabled(false);
            onStoryStarted?.Invoke(key);

            bool done = false;
            dm.PlaySequence(key, () => done = true);
            while (!done) yield return null;

            // daarna (optioneel) een leervraag; de speler blijft staan tot hij 'Verder' kiest
            var q = QuestionForKey(key);
            if (q != null && questionPanel != null)
            {
                bool answered = false;
                questionPanel.Ask(q, () => answered = true);
                while (!answered) yield return null;
            }

            if (hold) rider.SetInputEnabled(true);
            onStoryFinished?.Invoke(key);
        }
        busy = false;
    }
}
