using System.Collections;
using Oncologie.Dialogue;
using Oncologie.IRL;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Oncologie.Core
{
    public enum ExperienceState
    {
        Intro,
        Candles,
        Explore,
        Envelope,
        Choice,
        TransitionToColon,
        Outro
    }

    public enum ScreeningChoice
    {
        None,
        IgnoredTrash,   // Envelop genegeerd / weggegooid
        InsistedKeys    // Envelop aan Luc gegeven (aangedrongen)
    }

    /// <summary>
    /// Regisseur van de IRL-keukenscène:
    /// intro → kaarsjes aansteken → rondkijken → envelop van de koelkast → keuze → transitie naar de darm,
    /// en na terugkeer uit de darm het passende einde met de "klok terugdraaien"-optie.
    /// Elke stap heeft een fallback zodat het verhaal ook doorloopt als de speler niets doet.
    /// </summary>
    public class ExperienceFlowManager : MonoBehaviour
    {
        public static ExperienceFlowManager Instance { get; private set; }

        [Header("Personages & interactie")]
        public LucCharacter luc;
        public EnvelopeItem envelope;
        public LighterItem lighter;
        public CandleSet candles;
        public DropZone lucZone;
        public DropZone trashZone;
        public LookAtTrigger marcCard;
        public ColonTransition transition;
        public ClickableObject rewindClock;
        public Highlighter rewindClockHighlight;

        [Header("Sets (aan/uit per moment)")]
        public GameObject setBirthday;
        public GameObject setGrey;
        public GameObject setFuture;
        public GameObject infoCard;

        [Header("Licht")]
        public Light sun;
        public Light ceilingLamp;
        public Color warmSun = new Color(1f, 0.93f, 0.8f);
        public Color greySun = new Color(0.62f, 0.68f, 0.8f);

        [Header("Timing (seconden)")]
        public float candleReminderAfter = 22f;
        public float candleFallbackAfter = 45f;
        public float exploreTime = 10f;
        public float envelopeReminderAfter = 25f;
        public float envelopeFallbackAfter = 55f;
        public float choiceFallbackAfter = 50f;

        [Header("Events")]
        public UnityEvent<ScreeningChoice> onChoiceMade;
        public UnityEvent onOutroReached;

        [SerializeField] private ExperienceState currentState = ExperienceState.Intro;
        [SerializeField] private ScreeningChoice playerChoice = ScreeningChoice.None;

        public ExperienceState CurrentState => currentState;
        public ScreeningChoice PlayerChoice => playerChoice;

        private Coroutine _mainRoutine;
        private bool _marcSeen, _letterCommented;
        private bool _showDebug;

        private bool VR => PlayerRigSelector.IsVR;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (SubtitleUI.Instance != null) WorldUI.MakeOverlay(SubtitleUI.Instance.GetComponentInParent<Canvas>());
            if (envelope != null)
            {
                envelope.DeliveredTo += OnEnvelopeDelivered;
                envelope.LetterOpened += OnLetterOpened;
            }
            if (marcCard != null) marcCard.Looked += () => _marcSeen = true;
            if (rewindClock != null) rewindClock.onClicked.AddListener(RewindTime);

            if (SceneFlow.ReturningFromColon)
            {
                var choice = SceneFlow.PlayerChoice;
                SceneFlow.ConsumeReturn();
                _mainRoutine = StartCoroutine(OutroFlow(choice));
            }
            else
            {
                _mainRoutine = StartCoroutine(IntroFlow());
            }
        }

        // =====================================================================
        // DEEL 1: DE VERJAARDAG
        // =====================================================================

        private IEnumerator IntroFlow()
        {
            SetState(ExperienceState.Intro);
            ApplySet(birthday: true, grey: false, future: false);
            SetDaylight(true);
            if (envelope != null) envelope.SetInteractable(false);
            if (rewindClock != null) rewindClock.SetClickable(false);
            if (infoCard != null) infoCard.SetActive(false);
            if (marcCard != null) marcCard.armed = false;

            ScreenFader.Instance.SetImmediate(1f);
            yield return new WaitForSeconds(0.5f);
            yield return ScreenFader.Instance.Fade(0f, 2.5f);
            yield return Seq("Intro");

            // --- Kaarsjes aansteken ---
            SetState(ExperienceState.Candles);
            if (lighter != null) { lighter.SetInteractable(true); lighter.ShowHint(true); }
            HintDisplay.Show(VR
                ? "Grijp de aansteker (grip-knop) en houd het vlammetje bij elk kaarsje."
                : "Klik op de aansteker om hem te pakken en houd het vlammetje bij de kaarsjes.  (scrollwiel = dichter/verder)");

            float t = 0; bool reminded = false;
            while (candles != null && !candles.AllAreLit)
            {
                t += Time.deltaTime;
                if (!reminded && t > candleReminderAfter) { reminded = true; Say("Kaarsjes_Herinnering"); }
                if (t > candleFallbackAfter)
                {
                    HintDisplay.Hide();
                    yield return Seq("Kaarsjes_ZelfDoen");
                    candles.LightAll();
                    break;
                }
                yield return null;
            }
            HintDisplay.Hide();
            if (lighter != null) { lighter.ShowHint(false); lighter.ForceRelease(); lighter.SetInteractable(false); }
            yield return Seq("Kaarsjes_Klaar");

            // --- Rondkijken (rouwkaartje van Marc) ---
            SetState(ExperienceState.Explore);
            if (marcCard != null) marcCard.armed = true;
            t = 0;
            while (t < exploreTime && !_marcSeen) { t += Time.deltaTime; yield return null; }
            if (_marcSeen) yield return Seq("Marc");

            // --- De envelop ---
            yield return Seq("Envelop_Intro");
            SetState(ExperienceState.Envelope);
            if (envelope != null) { envelope.SetInteractable(true); envelope.ShowHint(true); }
            HintDisplay.Show(VR
                ? "Ga naar de koelkast en grijp de envelop."
                : "Loop naar de koelkast (WASD) en klik op de envelop.");

            t = 0; reminded = false;
            while (envelope != null && !envelope.HasBeenGrabbed)
            {
                t += Time.deltaTime;
                if (_marcSeen && marcCard != null && marcCard.armed) { marcCard.armed = false; yield return Seq("Marc"); }
                if (!reminded && t > envelopeReminderAfter) { reminded = true; Say("Envelop_Herinnering"); }
                if (t > envelopeFallbackAfter)
                {
                    HintDisplay.Hide();
                    envelope.ShowHint(false);
                    envelope.SetInteractable(false);
                    yield return ChoiceFlow(ScreeningChoice.IgnoredTrash, "Keuze_A_Niets");
                    yield break;
                }
                yield return null;
            }

            // --- Envelop vast: keuze maken ---
            if (lucZone != null) lucZone.Armed = true;
            if (trashZone != null) trashZone.Armed = true;
            HintDisplay.Show(VR
                ? "Trigger = brief lezen.  Geef de envelop aan Luc (houd hem bij zijn hand) of gooi hem in de vuilbak."
                : "E / rechtermuis = brief lezen.  Houd de envelop bij Luc's hand om hem te geven, of boven de vuilbak om hem weg te gooien.");
            yield return Seq("Envelop_Gepakt");

            t = 0;
            while (envelope != null && !envelope.Delivered)
            {
                t += Time.deltaTime;
                if (t > choiceFallbackAfter)
                {
                    yield return Seq("Keuze_Timeout");
                    if (!envelope.Delivered && trashZone != null) envelope.DeliverTo(trashZone);
                    break;
                }
                yield return null;
            }
            // Verder in OnEnvelopeDelivered → ChoiceFlow
        }

        private void OnLetterOpened()
        {
            if (_letterCommented || currentState != ExperienceState.Envelope) return;
            _letterCommented = true;
            StartCoroutine(SayWhenFree("Brief_Gelezen"));
        }

        private void OnEnvelopeDelivered(ScreeningChoice choice)
        {
            if (currentState == ExperienceState.Choice || currentState == ExperienceState.TransitionToColon) return;
            if (_mainRoutine != null) StopCoroutine(_mainRoutine);
            _mainRoutine = StartCoroutine(ChoiceFlow(choice, choice == ScreeningChoice.InsistedKeys ? "Keuze_B_Aandringen" : "Keuze_A_Negeren"));
        }

        private IEnumerator ChoiceFlow(ScreeningChoice choice, string sequence)
        {
            SetState(ExperienceState.Choice);
            playerChoice = choice;
            HintDisplay.Hide();
            if (lucZone != null) lucZone.Armed = false;
            if (trashZone != null) trashZone.Armed = false;
            if (DialogueManager.Instance != null) DialogueManager.Instance.StopCurrent();
            onChoiceMade?.Invoke(choice);

            yield return Seq(sequence);
            yield return new WaitForSeconds(0.6f);
            GoToColon();
        }

        // =====================================================================
        // DEEL 2: NAAR DE DARM
        // =====================================================================

        /// <summary>Start de (uitgebreide) transitie en laadt daarna de darmscène.</summary>
        public void GoToColon()
        {
            if (currentState == ExperienceState.TransitionToColon) return;
            if (playerChoice == ScreeningChoice.None) playerChoice = ScreeningChoice.IgnoredTrash;
            SetState(ExperienceState.TransitionToColon);
            HintDisplay.Hide();
            var choice = playerChoice;
            if (transition != null) transition.Play(() => SceneFlow.LoadColon(choice));
            else SceneFlow.LoadColon(choice);
        }

        // =====================================================================
        // DEEL 3: HET EINDE
        // =====================================================================

        private IEnumerator OutroFlow(ScreeningChoice choice)
        {
            SetState(ExperienceState.Outro);
            playerChoice = choice;
            bool good = choice == ScreeningChoice.InsistedKeys;

            ScreenFader.Instance.SetImmediate(1f);
            ApplySet(birthday: false, grey: !good, future: good);
            if (envelope != null) envelope.gameObject.SetActive(false);
            if (lighter != null) lighter.gameObject.SetActive(false);
            if (candles != null) candles.ExtinguishAll();
            if (luc != null) luc.gameObject.SetActive(good);
            if (infoCard != null) infoCard.SetActive(false);
            SetDaylight(good);
            if (!good) EnableGreyGrade();

            yield return new WaitForSeconds(0.6f);
            yield return ScreenFader.Instance.ShowTitle(good ? "Twee jaar later" : "Drie jaar later",
                                                        good ? "Luc wordt 60." : "Dezelfde keuken.", 2.6f);
            yield return ScreenFader.Instance.Fade(0f, 2.5f);
            yield return new WaitForSeconds(good ? 0.5f : 2.5f); // in het slechte einde: eerst de stilte laten spreken
            yield return Seq(good ? "Einde_Goed" : "Einde_Slecht");

            if (infoCard != null) infoCard.SetActive(true);
            if (rewindClock != null) rewindClock.SetClickable(true);
            if (rewindClockHighlight != null) rewindClockHighlight.Set(true);
            HintDisplay.Show(good
                ? (VR ? "Grijp de klok aan de muur om het verhaal opnieuw te beleven." : "Klik op de klok aan de muur om het verhaal opnieuw te beleven.")
                : (VR ? "Grijp de klok aan de muur om de tijd terug te draaien en opnieuw te kiezen." : "Klik op de klok aan de muur om de tijd terug te draaien en opnieuw te kiezen."));
            onOutroReached?.Invoke();
        }

        /// <summary>Draait de tijd terug: begint opnieuw bij de verjaardag.</summary>
        public void RewindTime()
        {
            if (currentState == ExperienceState.TransitionToColon) return;
            StopAllCoroutines();
            StartCoroutine(RewindRoutine());
        }

        private IEnumerator RewindRoutine()
        {
            HintDisplay.Hide();
            if (rewindClock != null) rewindClock.SetClickable(false);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayWhoosh();
            yield return ScreenFader.Instance.Fade(1f, 1.2f, Color.white);
            SceneFlow.ResetSession();
            SceneFlow.ReloadIrl();
        }

        // =====================================================================
        // HULPFUNCTIES
        // =====================================================================

        private void SetState(ExperienceState s) => currentState = s;

        private void ApplySet(bool birthday, bool grey, bool future)
        {
            if (setBirthday != null) setBirthday.SetActive(birthday);
            if (setGrey != null) setGrey.SetActive(grey);
            if (setFuture != null) setFuture.SetActive(future);
        }

        private void SetDaylight(bool warm)
        {
            if (sun != null) { sun.color = warm ? warmSun : greySun; sun.intensity = warm ? 1.3f : 0.55f; }
            if (ceilingLamp != null) ceilingLamp.intensity = warm ? ceilingLamp.intensity : ceilingLamp.intensity * 0.5f;
        }

        private void EnableGreyGrade()
        {
            var vol = gameObject.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 50;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var ca = profile.Add<ColorAdjustments>(true);
            ca.saturation.Override(-80f);
            ca.postExposure.Override(-0.35f);
            ca.colorFilter.Override(new Color(0.88f, 0.92f, 1f));
            vol.profile = profile;
        }

        private IEnumerator Seq(string key)
        {
            var dm = DialogueManager.Instance;
            if (dm == null) yield break;
            bool done = false;
            float max = dm.GetDuration(key) + 2f;
            dm.PlaySequence(key, () => done = true);
            float t = 0;
            while (!done && t < max) { t += Time.deltaTime; yield return null; }
        }

        private void Say(string key)
        {
            if (DialogueManager.Instance != null) DialogueManager.Instance.PlaySequence(key);
        }

        private IEnumerator SayWhenFree(string key)
        {
            var dm = DialogueManager.Instance;
            if (dm == null) yield break;
            float t = 0;
            while (dm.IsPlaying && t < 8f) { t += Time.deltaTime; yield return null; }
            if (currentState == ExperienceState.Envelope && !dm.IsPlaying) dm.PlaySequence(key);
        }

        // =====================================================================
        // TESTEN OP DESKTOP (Tab = menu)
        // =====================================================================

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab)) _showDebug = !_showDebug;
            if (!_showDebug) return;
            if (Input.GetKeyDown(KeyCode.Alpha1)) DebugChoose(ScreeningChoice.IgnoredTrash);
            if (Input.GetKeyDown(KeyCode.Alpha2)) DebugChoose(ScreeningChoice.InsistedKeys);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SceneFlow.LoadOutro(ScreeningChoice.IgnoredTrash);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SceneFlow.LoadOutro(ScreeningChoice.InsistedKeys);
            if (Input.GetKeyDown(KeyCode.Alpha5)) { SceneFlow.ResetSession(); SceneFlow.ReloadIrl(); }
            if (Input.GetKeyDown(KeyCode.Alpha6)) SkipToEnvelope();
        }

        private void DebugChoose(ScreeningChoice c)
        {
            if (currentState >= ExperienceState.Choice) return;
            if (envelope != null && !envelope.Delivered)
            {
                envelope.SetInteractable(true);
                var zone = c == ScreeningChoice.InsistedKeys ? lucZone : trashZone;
                if (zone != null) { envelope.DeliverTo(zone); return; }
            }
            OnEnvelopeDelivered(c);
        }

        private void SkipToEnvelope()
        {
            if (currentState >= ExperienceState.Envelope) return;
            if (candles != null) candles.LightAll();
            if (DialogueManager.Instance != null) DialogueManager.Instance.StopCurrent();
            exploreTime = 0f;
        }

        private void OnGUI()
        {
            if (VR) return;
            if (!_showDebug)
            {
                GUI.Label(new Rect(10, 8, 300, 22), "<color=#ffffffaa>[Tab] testmenu</color>");
                return;
            }
            GUILayout.BeginArea(new Rect(10, 10, 330, 230), GUI.skin.box);
            GUILayout.Label($"<b>ONCOLOGIE – IRL test</b>   fase: <color=yellow>{currentState}</color>   keuze: {playerChoice}");
            GUILayout.Label("1 = keuze A (vuilbak)     2 = keuze B (aan Luc)");
            GUILayout.Label("3 = einde slecht           4 = einde goed");
            GUILayout.Label("5 = herstart                 6 = kaarsjes overslaan");
            GUILayout.Label("Klik = pakken/loslaten · E = gebruiken · scroll = afstand");
            GUILayout.Label("Esc = muiscursor vrijgeven");
            GUILayout.EndArea();
        }
    }
}
