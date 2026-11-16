using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Oncologie.Dialogue
{
    /// <summary>
    /// Schaalbare dialoogbeheerder voor het hele Oncologie VR-project.
    /// Bevat configureerbare lijsten van dialoogregels met slots voor audiobestanden (.mp3/.wav)
    /// en toont ondertiteling via SubtitleUI.
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }

        [Header("Audio Output")]
        [SerializeField] private AudioSource voiceAudioSource;

        [Tooltip("Laad de dialogen altijd uit code (aanbevolen), zodat oude scènedata ze niet overschrijft.")]
        public bool useScriptLines = true;

        [Header("Verhaallijn Reeksen")]
        [Tooltip("Configureer hier alle dialogen. Je kunt regels toevoegen, bewerken en audioclips toewijzen in de Inspector.")]
        public List<DialogueSequence> sequences = new List<DialogueSequence>();

        [Header("Events")]
        public UnityEvent<DialogueLine> onLineStarted;
        public UnityEvent<string> onSequenceFinished;

        private Coroutine _currentDialogueRoutine;

        public bool IsPlaying => _currentDialogueRoutine != null;
        private bool _isSkipping = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (voiceAudioSource == null)
            {
                voiceAudioSource = gameObject.AddComponent<AudioSource>();
                voiceAudioSource.playOnAwake = false;
                voiceAudioSource.spatialBlend = 0f; // 2D voice-over / broadcast
            }

            if (useScriptLines || sequences == null || sequences.Count == 0)
            {
                InitializeDefaultStorylines();
            }
        }

        /// <summary>
        /// Start een dialoogreeks op basis van de sequence key.
        /// </summary>
        public void PlaySequence(string key, Action onComplete = null)
        {
            DialogueSequence seq = sequences.Find(s => s.sequenceKey.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (seq != null)
            {
                if (_currentDialogueRoutine != null) StopCoroutine(_currentDialogueRoutine);
                _currentDialogueRoutine = StartCoroutine(RunSequenceRoutine(seq, onComplete));
            }
            else
            {
                Debug.LogWarning($"[DialogueManager] Geen sequence gevonden met sleutel: {key}");
                onComplete?.Invoke();
            }
        }

        /// <summary>Totale duur van een reeks (zonder audio: displayDuration + pauzes).</summary>
        public float GetDuration(string key)
        {
            var seq = sequences.Find(x => x.sequenceKey.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (seq == null) return 0f;
            float d = 0f;
            foreach (var l in seq.lines) d += (l.voiceClip != null ? Mathf.Max(l.voiceClip.length, l.displayDuration) : l.displayDuration) + l.pauseAfter;
            return d;
        }

        /// <summary>
        /// Speelt een losse dialoogregel af met ondertiteling en eventuele audio.
        /// </summary>
        public void PlaySingleLine(DialogueLine line, Action onComplete = null)
        {
            if (line == null) return;
            var seq = new DialogueSequence("SingleLine", "");
            seq.lines.Add(line);
            if (_currentDialogueRoutine != null) StopCoroutine(_currentDialogueRoutine);
            _currentDialogueRoutine = StartCoroutine(RunSequenceRoutine(seq, onComplete));
        }

        public void StopCurrent()
        {
            if (_currentDialogueRoutine != null)
            {
                StopCoroutine(_currentDialogueRoutine);
                _currentDialogueRoutine = null;
            }

            if (voiceAudioSource != null && voiceAudioSource.isPlaying)
            {
                voiceAudioSource.Stop();
            }

            if (SubtitleUI.Instance != null)
            {
                SubtitleUI.Instance.ClearSubtitle();
            }
        }

        public void SkipCurrentLine()
        {
            _isSkipping = true;
            if (voiceAudioSource != null && voiceAudioSource.isPlaying)
            {
                voiceAudioSource.Stop();
            }
        }

        private IEnumerator RunSequenceRoutine(DialogueSequence seq, Action onComplete)
        {
            foreach (var line in seq.lines)
            {
                _isSkipping = false;

                // 1. Toon ondertiteling
                if (SubtitleUI.Instance != null)
                {
                    SubtitleUI.Instance.DisplayLine(line);
                }
                onLineStarted?.Invoke(line);

                // 2. Speel eventuele voice-over clip
                float duration = line.displayDuration;
                if (line.voiceClip != null && voiceAudioSource != null)
                {
                    voiceAudioSource.clip = line.voiceClip;
                    voiceAudioSource.Play();
                    duration = Mathf.Max(line.voiceClip.length, line.displayDuration);
                }

                // 3. Wacht tot de lijn klaar is (of geskipt wordt)
                float elapsed = 0f;
                while (elapsed < duration && !_isSkipping)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                // Korte rustpauze tussen zinnen
                if (SubtitleUI.Instance != null)
                {
                    SubtitleUI.Instance.ClearSubtitle();
                }
                yield return new WaitForSeconds(line.pauseAfter);
            }

            onSequenceFinished?.Invoke(seq.sequenceKey);
            onComplete?.Invoke();
            _currentDialogueRoutine = null;
        }

        public static readonly Color LucColor = new Color(0.45f, 0.8f, 1.0f);
        public static readonly Color PlayerColor = new Color(0.92f, 0.92f, 0.92f);
        public static readonly Color NarratorColor = new Color(1.0f, 0.82f, 0.35f);
        public static readonly Color TipColor = new Color(0.55f, 1.0f, 0.6f);

        private void Add(string key, string desc, params DialogueLine[] lines)
        {
            var seq = new DialogueSequence(key, desc);
            seq.lines.AddRange(lines);
            sequences.Add(seq);
        }

        private static DialogueLine Luc(string text, float d) => new DialogueLine("Luc", LucColor, text, d);
        private static DialogueLine Me(string text, float d) => new DialogueLine("Jij", PlayerColor, text, d);
        private static DialogueLine Narrator(string text, float d) => new DialogueLine("Verteller", NarratorColor, text, d);

        /// <summary>
        /// Alle dialogen van de IRL-keuken (Vlaamse tussentaal). Audio kan later per regel in de Inspector
        /// gekoppeld worden; codes zoals LUC_01 kunnen als bestandsnaam dienen.
        /// </summary>
        public void InitializeDefaultStorylines()
        {
            sequences.Clear();

            Add("Intro", "Verjaardag van Luc, de speler komt binnen",
                Luc("Ah, daar zijt ge! Ik dacht al dat ge mijn verjaardag vergeten waart.", 3.8f),
                Luc("Achtenvijftig, hè. En nog geen enkele dag ziek geweest. Doe mij dat maar eens na.", 4.6f),
                Luc("Allez, gij hebt de vaste hand. Steekt gij de kaarskes eens aan? De aansteker ligt op tafel.", 5.2f));

            Add("Kaarsjes_Herinnering", "Speler steekt de kaarsjes niet aan",
                Luc("Toe, de kaarskes. Anders smelt die taart nog.", 3.5f));

            Add("Kaarsjes_ZelfDoen", "Luc steekt de kaarsjes zelf aan",
                Luc("Laat maar, ik doe het zelf wel. Als ik het doe, staat de taart in brand, maar bon.", 4.5f));

            Add("Kaarsjes_Klaar", "Alle kaarsjes branden",
                Luc("Kijk eens aan. Professioneel!", 2.8f),
                Luc("Eerst nog een tas koffie, dan snijden we de taart aan.", 3.8f));

            Add("Marc", "Speler kijkt naar het rouwkaartje van Marc",
                Luc("Marc… Veertig jaar samen gevoetbald. Die was ook nooit ziek, hè.", 5.0f),
                Luc("Allez. Vandaag is 't feest.", 3.0f));

            Add("Envelop_Intro", "Luc wijst zelf op de brief op de koelkast",
                Luc("Ah, en die brief daar op de frigo… die van het bevolkingsonderzoek.", 4.5f),
                Luc("Die hangt daar al sinds de zomer. Er is zelfs al een herinnering gekomen.", 4.5f),
                Luc("Ik voel mij kiplekker. Wat moet ik daarmee?", 3.5f));

            Add("Envelop_Herinnering", "Speler pakt de envelop niet",
                Narrator("De envelop hangt nog altijd op de koelkast. Jij kan er iets mee doen.", 4.5f));

            Add("Envelop_Gepakt", "Speler pakt de envelop",
                Luc("Hé, wat zijt gij daar aan het doen? Dat is mijn post, hè.", 3.8f),
                Luc("Gooi hem gerust in de vuilbak. Of… geef hem dan maar hier, als 't moet.", 5.0f));

            Add("Brief_Gelezen", "Speler opent de brief",
                Luc("Ja, ja. 't Is gratis en ge doet het thuis. Dat weet ik ook wel.", 4.2f));

            Add("Keuze_Timeout", "Speler beslist niet",
                Luc("Kom, leg die brief maar weg. Taart!", 3.2f));

            Add("Keuze_A_Negeren", "Envelop in de vuilbak",
                Luc("Zie ge wel. Weg ermee. Nog een jaar kiplekker!", 3.8f),
                Narrator("De brief belandt in de vuilbak. Luc voelt niets, dus hij denkt dat er niets is.", 5.5f));

            Add("Keuze_A_Niets", "Speler deed niets",
                Luc("Laat die brief maar hangen. Taart!", 3.0f),
                Narrator("De brief blijft hangen. Niets doen is ook een keuze.", 4.5f));

            Add("Keuze_B_Aandringen", "Envelop aan Luc gegeven",
                Me("Papa, doe die test. Het is gratis en het duurt maar vijf minuten.", 4.5f),
                Luc("Gij laat het niet los, hè… Goed dan. Voor u. Ik doe hem deze week nog.", 5.2f),
                Narrator("Luc doet de test. Maar wat zou die test eigenlijk kunnen vinden?", 4.8f));

            Add("Einde_Slecht", "Drie jaar later: de brief bleef liggen",
                Narrator("Luc had nergens last van. Tot hij buikpijn kreeg die niet meer overging.", 5.5f),
                Narrator("Toen de dokter de tumor vond, was de kanker al uitgezaaid.", 5.0f),
                Narrator("Darmkanker geeft in het begin geen klachten. De stoelgangtest vindt het vóór je iets voelt.", 6.0f));

            Add("Einde_Goed", "Twee jaar later: Luc deed de test",
                Luc("Die test vond een beetje bloed dat ik zelf nooit had gezien.", 4.5f),
                Luc("Bij de coloscopie hebben ze dat poliepje er meteen uitgehaald. Weg. Gewoon weg.", 5.0f),
                Luc("Nooit ziek geweest… Bijna nooit, hè.", 3.5f),
                Luc("En kijk: de nieuwe brief is er al. Om de twee jaar. Deze keer doe ik hem direct.", 5.5f),
                Narrator("Het bevolkingsonderzoek dikkedarmkanker is gratis voor iedereen van 50 tot 74 jaar. Om de twee jaar, gewoon thuis.", 6.5f));
        }
    }
}
