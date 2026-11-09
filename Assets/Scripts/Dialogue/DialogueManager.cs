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

        [Header("Verhaallijn Reeksen")]
        [Tooltip("Configureer hier alle dialogen. Je kunt regels toevoegen, bewerken en audioclips toewijzen in de Inspector.")]
        public List<DialogueSequence> sequences = new List<DialogueSequence>();

        [Header("Events")]
        public UnityEvent<DialogueLine> onLineStarted;
        public UnityEvent<string> onSequenceFinished;

        private Coroutine _currentDialogueRoutine;
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

            if (sequences == null || sequences.Count == 0)
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

        /// <summary>
        /// Vult direct alle standaard medische dialogen in voor het project.
        /// </summary>
        public void InitializeDefaultStorylines()
        {
            sequences.Clear();

            Color lucColor = new Color(0.35f, 0.78f, 1.0f);   // Cyaan / Blauw
            Color playerColor = new Color(0.9f, 0.9f, 0.9f);  // Lichtgrijs / Wit
            Color docColor = new Color(1.0f, 0.82f, 0.35f);   // Goud / Geel
            Color warningColor = new Color(1.0f, 0.45f, 0.4f);// Zacht rood

            // 1. INTRO IN DE KEUKEN
            var intro = new DialogueSequence("Intro_Keuken", "Vader en kind bij het aanrecht");
            intro.lines.Add(new DialogueLine("Vader Luc (58)", lucColor, "Zo, de koffie pruttelt. Gezellig dat je er bent vandaag!", 3.5f));
            intro.lines.Add(new DialogueLine("Vader Luc (58)", lucColor, "Kijk, er lag vanochtend weer zo'n paarse brief van het Bevolkingsonderzoek op de mat.", 4.5f));
            intro.lines.Add(new DialogueLine("Vader Luc (58)", lucColor, "Ach joh, ik ben pas 58 en kerngezond. Die test doe ik volgend jaar wel.", 4.5f));
            intro.lines.Add(new DialogueLine("Vader Luc (58)", lucColor, "Ik ga even de hond uitlaten. Leg die envelop maar ergens neer.", 4.0f));
            intro.lines.Add(new DialogueLine("Jij (Perspectief)", playerColor, "Wat doe je met de brief? Leg je hem bij het oud papier (A), of leg je hem bij zijn autosleutels (B)?", 5.0f));
            sequences.Add(intro);

            // 2. KEUZE A: GENEGEERD
            var choiceA = new DialogueSequence("Keuze_A_Negeren", "Brief bij oud papier gelegd");
            choiceA.lines.Add(new DialogueLine("Jij (Perspectief)", playerColor, "Hij voelt zich prima... ik leg de brief wel bij het oud papier.", 3.5f));
            choiceA.lines.Add(new DialogueLine("Dr. Verteller", warningColor, "De brief belandt ongebruikt bij het afval. Drie cruciale jaren beginnen nu geruisloos te tikken...", 4.5f));
            sequences.Add(choiceA);

            // 3. KEUZE B: AANGEDRONGEN
            var choiceB = new DialogueSequence("Keuze_B_Aandringen", "Brief bij autosleutels gelegd");
            choiceB.lines.Add(new DialogueLine("Jij (Perspectief)", playerColor, "Nee pap, 58 is precies de doelgroep. Geen uitstel meer, doe deze test.", 4.0f));
            choiceB.lines.Add(new DialogueLine("Vader Luc (58)", lucColor, "Nou vooruit dan maar... omdat jij zo aandringt. Ik stuur de test vanavond nog op.", 4.0f));
            choiceB.lines.Add(new DialogueLine("Dr. Verteller", docColor, "De testkit wordt verzonden naar het laboratorium voor analyse.", 3.5f));
            sequences.Add(choiceB);

            // 4. DARM INTRO: GEZOND WEEFSEL & POLIEP
            var colonIntro = new DialogueSequence("Darm_Intro", "Binnenkomst in de dikke darm");
            colonIntro.lines.Add(new DialogueLine("Dr. Verteller", docColor, "Welkom in het binnenste van de dikke darm, op cellulair micro-niveau.", 4.0f));
            colonIntro.lines.Add(new DialogueLine("Dr. Verteller", docColor, "Kijk om je heen. Het slijmvlies is vochtig, roze en volkomen intact.", 4.0f));
            colonIntro.lines.Add(new DialogueLine("Dr. Verteller", docColor, "Maar kijk hier naar deze kleine zwelling aan de wand: een adenoom, een goedaardige poliep.", 5.0f));
            colonIntro.lines.Add(new DialogueLine("Dr. Verteller", docColor, "Luc merkt hier helemaal niets van. Geen pijn, geen bloed in het toilet. Maar de iFOBT-test ontdekt microscopische sporen wél!", 5.5f));
            sequences.Add(colonIntro);

            // 5. DARM ROUTE A: 3 JAAR LATER & KANKERGROEI
            var colonA = new DialogueSequence("Darm_Route_A_Kanker", "Onbehandelde poliep muteert en groeit");
            colonA.lines.Add(new DialogueLine("Dr. Verteller", warningColor, "3 jaar later...", 3.0f));
            colonA.lines.Add(new DialogueLine("Dr. Verteller", warningColor, "Omdat de brief werd genegeerd, hebben de cellen zich ongehinderd kunnen delen.", 4.5f));
            colonA.lines.Add(new DialogueLine("Dr. Verteller", warningColor, "De kleine poliep is nu gemuteerd tot een kwaadaardige tumor (Stadium 4).", 4.5f));
            colonA.lines.Add(new DialogueLine("Dr. Verteller", warningColor, "De tumor breekt door de darmwand heen en infiltreert de bloedvaten. Uitzaaiing.", 5.0f));
            colonA.lines.Add(new DialogueLine("Dr. Verteller", warningColor, "Pas nu krijgt Luc klachten... maar nu is genezing bijzonder moeilijk.", 5.0f));
            sequences.Add(colonA);

            // 6. DARM ROUTE B: VROEGE INTERVENTIE & VERWIJDERING
            var colonB = new DialogueSequence("Darm_Route_B_Genezing", "Coloscopie verwijdert poliep");
            colonB.lines.Add(new DialogueLine("Dr. Verteller", docColor, "Dankzij de tijdige stoelgangtest sloeg het laboratorium alarm.", 4.0f));
            colonB.lines.Add(new DialogueLine("Dr. Verteller", docColor, "Tijdens een eenvoudige coloscopie wordt de poliep met een lisje pijnloos weggesneden.", 4.5f));
            colonB.lines.Add(new DialogueLine("Dr. Verteller", docColor, "De darmwand heelt snel. Kanker heeft hier nooit een kans gekregen.", 4.0f));
            sequences.Add(colonB);

            // 7. KEUKEN OUTRO SLECHT
            var outroBad = new DialogueSequence("Einde_Slecht", "Gevolgen van negeren");
            outroBad.lines.Add(new DialogueLine("Dr. Verteller", warningColor, "De keuken is stil en koud. Vader is er niet meer.", 4.0f));
            outroBad.lines.Add(new DialogueLine("Dr. Verteller", warningColor, "Darmkanker geeft pas laat merkbare klachten. Wachten kost levens.", 4.5f));
            outroBad.lines.Add(new DialogueLine("Systeem", playerColor, "Druk op [R] op je toetsenbord of richt op de knop om de tijd terug te spoelen.", 5.0f));
            sequences.Add(outroBad);

            // 8. KEUKEN OUTRO GOED
            var outroGood = new DialogueSequence("Einde_Goed", "Gevolgen van vroege actie");
            outroGood.lines.Add(new DialogueLine("Vader Luc (61)", lucColor, "Wat ben ik dankbaar dat je me die dag overtuigd hebt om die test te doen!", 4.5f));
            outroGood.lines.Add(new DialogueLine("Dr. Verteller", docColor, "Vroege opsporing redt jaarlijks duizenden levens. Doe mee aan het bevolkingsonderzoek.", 5.0f));
            sequences.Add(outroGood);
        }
    }
}
