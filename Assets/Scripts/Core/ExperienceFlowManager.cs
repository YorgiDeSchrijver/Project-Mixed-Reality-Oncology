using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Oncologie.Core
{
    public enum ExperienceState
    {
        KitchenIntro,
        TransitionToColon,
        ColonDarmwandeling,
        KitchenOutro
    }

    public enum ScreeningChoice
    {
        None,
        IgnoredTrash,   // Optie A: In de prullenmand gegooid / negeren
        InsistedKeys    // Optie B: Aan vader Luc gegeven / aandringen
    }

    /// <summary>
    /// Centrale regisseur van de Oncologie Darmkanker VR Experience.
    /// Beheert de flow tussen Keuken, Darmwandeling en Keuken-Einde.
    /// Biedt volledige teleportatie- en testknoppen voor elke scène in het project.
    /// </summary>
    public class ExperienceFlowManager : MonoBehaviour
    {
        public static ExperienceFlowManager Instance { get; private set; }

        [Header("Huidige Status")]
        [SerializeField] private ExperienceState currentState = ExperienceState.KitchenIntro;
        [SerializeField] private ScreeningChoice playerChoice = ScreeningChoice.None;
        public ExperienceState CurrentState => currentState;
        public ScreeningChoice PlayerChoice => playerChoice;
        public ScreeningChoice GetPlayerChoice() => playerChoice;

        [Header("Locaties (Camera / Speler Transforms)")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Transform kitchenPlayerAnchor;
        [SerializeField] private Transform colonStartAnchor;

        [Header("Omgevingen (GameObjects)")]
        [SerializeField] private GameObject kitchenEnvironment;
        [SerializeField] private GameObject colonEnvironment;

        [Header("Visuele Feedback & Belichting")]
        [SerializeField] private Light kitchenDirectionalLight;
        [SerializeField] private Color warmSunlightColor = new Color(1f, 0.94f, 0.82f);
        [SerializeField] private Color coldGrievingColor = new Color(0.55f, 0.65f, 0.85f);
        [SerializeField] private GreyscaleTransition greyscaleTransition;

        [Header("Personages & Props in Keuken")]
        [SerializeField] private GameObject fatherCharacter;
        [SerializeField] private GameObject motherCharacter;
        [SerializeField] private GameObject hospitalBraceletPhoto;

        [Header("Darm Controllers")]
        [SerializeField] private ColonExperienceController colonController;

        [Header("UI & Ondertiteling")]
        [SerializeField] private Text subtitleText;
        [SerializeField] private GameObject outroPanel;
        [SerializeField] private RawImage outroCardImage;
        [SerializeField] private Texture2D goodEndingCardTexture;
        [SerializeField] private Texture2D badEndingCardTexture;
        [SerializeField] private GameObject rewindSystem;

        [Header("Events")]
        public UnityEvent onExperienceStarted;
        public UnityEvent<ScreeningChoice> onChoiceMade;
        public UnityEvent onColonEntered;
        public UnityEvent onOutroReached;

        private Coroutine _subtitleCoroutine;
        private string _currentSubtitle = "";
        private bool _showDebugMenu = true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureAllReferences();
        }

        private void Start()
        {
            EnsureAllReferences();
            StartKitchenIntro();
        }

        public void EnsureAllReferences()
        {
            // 1. Speler transform (ondersteunt zowel XR Origin VR Rig als Test Player)
            if (playerTransform == null || !playerTransform.gameObject.activeInHierarchy)
            {
                var xr = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
                if (xr != null && xr.gameObject.activeInHierarchy)
                {
                    playerTransform = xr.transform;
                }
                else
                {
                    var player = FindAnyObjectByType<SampleSceneTestPlayer>();
                    if (player != null && player.gameObject.activeInHierarchy) playerTransform = player.transform;
                    else
                    {
                        var pGO = GameObject.Find("Test Player");
                        if (pGO != null && pGO.activeInHierarchy) playerTransform = pGO.transform;
                        else if (Camera.main != null) playerTransform = Camera.main.transform;
                    }
                }
            }

            // 2. Anchors
            if (kitchenPlayerAnchor == null)
            {
                var ka = GameObject.Find("Kitchen_PlayerAnchor");
                if (ka != null) kitchenPlayerAnchor = ka.transform;
            }

            if (colonStartAnchor == null)
            {
                var ca = GameObject.Find("Colon_PlayerAnchor");
                if (ca != null) colonStartAnchor = ca.transform;
            }

            // 3. Omgevingen
            if (kitchenEnvironment == null)
            {
                kitchenEnvironment = GameObject.Find("kitchen");
            }

            if (colonEnvironment == null)
            {
                colonEnvironment = GameObject.Find("organ final");
            }

            // 4. Luc
            if (fatherCharacter == null)
            {
                fatherCharacter = GameObject.Find("Scavenger");
            }

            // 5. Belichting
            if (kitchenDirectionalLight == null)
            {
                var sun = GameObject.Find("Directional Light (Sunlight)");
                if (sun != null) kitchenDirectionalLight = sun.GetComponent<Light>();
                else
                {
                    var lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (var l in lights)
                    {
                        if (l.type == LightType.Directional) { kitchenDirectionalLight = l; break; }
                    }
                }
            }

            // 6. Colon controller
            if (colonController == null && colonEnvironment != null)
            {
                colonController = colonEnvironment.GetComponent<ColonExperienceController>();
                if (colonController == null)
                {
                    colonController = colonEnvironment.AddComponent<ColonExperienceController>();
                }
            }

            // 7. Rewind System
            if (rewindSystem == null)
            {
                var rw = GameObject.Find("Rewind_System");
                if (rw != null) rewindSystem = rw;
                else
                {
                    var sceneRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                    foreach (var r in sceneRoots)
                    {
                        if (r.name == "Rewind_System" || r.name == "Rewind_Clock_Object")
                        {
                            rewindSystem = r;
                            break;
                        }
                    }
                }
            }
        }

        private void Update()
        {
            // Sneltoetsen voor Desktop testers en docenten
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                MakeChoice(ScreeningChoice.IgnoredTrash);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                MakeChoice(ScreeningChoice.InsistedKeys);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                TeleportToDarmStart();
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                TeleportToKitchenOutroBad();
            }
            else if (Input.GetKeyDown(KeyCode.Alpha5))
            {
                TeleportToKitchenOutroGood();
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                RewindTime();
            }
            else if (Input.GetKeyDown(KeyCode.Tab))
            {
                _showDebugMenu = !_showDebugMenu;
            }
        }

        // ==========================================
        // SCÈNE 1: KEUKEN INTRO
        // ==========================================

        public void StartKitchenIntro()
        {
            EnsureAllReferences();
            currentState = ExperienceState.KitchenIntro;
            playerChoice = ScreeningChoice.None;

            if (kitchenEnvironment != null) kitchenEnvironment.SetActive(true);
            if (colonEnvironment != null) colonEnvironment.SetActive(false);
            if (outroPanel != null) outroPanel.SetActive(false);
            if (rewindSystem != null) rewindSystem.SetActive(false);
            ApplyEnvironmentLighting(false);

            if (fatherCharacter != null)
            {
                fatherCharacter.SetActive(true);
                var anim = fatherCharacter.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.speed = 0f;
                    anim.Play("New State", 0, 0.15f);
                }
            }

            if (hospitalBraceletPhoto != null) hospitalBraceletPhoto.SetActive(false);

            if (kitchenDirectionalLight != null)
            {
                kitchenDirectionalLight.color = warmSunlightColor;
                kitchenDirectionalLight.intensity = 1.25f;
            }

            if (greyscaleTransition != null)
            {
                greyscaleTransition.FadeToColor();
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopHeartbeat();
                AudioManager.Instance.StopAmbience();
            }

            // Teleporteer speler naar de keukentafel
            Vector3 kPos = kitchenPlayerAnchor != null ? kitchenPlayerAnchor.position : new Vector3(-0.41f, 0.0f, -0.6f);
            Quaternion kRot = kitchenPlayerAnchor != null ? kitchenPlayerAnchor.rotation : Quaternion.identity;
            TeleportPlayer(kPos, kRot);

            // Reset brief positie op tafel
            var env = GameObject.Find("envelope");
            if (env == null)
            {
                var sceneRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                foreach (var r in sceneRoots)
                {
                    if (r.name == "envelope") { env = r; break; }
                }
            }
            if (env != null)
            {
                env.SetActive(true);
                var kit = env.GetComponent<ScreeningKitInteraction>();
                if (kit != null) kit.ResetPosition();
                else
                {
                    env.transform.position = new Vector3(-0.41f, 1.54f, 0.90f);
                    env.transform.rotation = Quaternion.Euler(0f, 15f, 0f);
                }
            }

            // Dialoog Luc
            if (Oncologie.Dialogue.DialogueManager.Instance != null)
            {
                Oncologie.Dialogue.DialogueManager.Instance.PlaySequence("Intro_Keuken");
            }
            else
            {
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayVoice(AudioManager.Instance.voiceLucIntro, "Vader Luc");
                }
                ShowSubtitle("Vader Luc: \"Kijk nu, alweer zo'n envelop voor die stoelgangtest... Ik voel me kerngezond, ik doe dat volgend jaar wel.\"", 6.5f);
            }

            onExperienceStarted?.Invoke();
        }

        // ==========================================
        // KEUZEMOMENT
        // ==========================================

        public void MakeChoice(ScreeningChoice choice)
        {
            if (currentState != ExperienceState.KitchenIntro)
                return;

            playerChoice = choice;
            onChoiceMade?.Invoke(playerChoice);

            if (playerChoice == ScreeningChoice.IgnoredTrash)
            {
                if (Oncologie.Dialogue.DialogueManager.Instance != null)
                {
                    Oncologie.Dialogue.DialogueManager.Instance.PlaySequence("Keuze_A_Negeren");
                }
                else
                {
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlayVoice(AudioManager.Instance.voiceLucTrash, "Vader Luc");
                    }
                    ShowSubtitle("Vader Luc: \"Zo, in de prullenmand. Geen zorgen maken om niks!\"", 3.5f);
                }
            }
            else
            {
                if (Oncologie.Dialogue.DialogueManager.Instance != null)
                {
                    Oncologie.Dialogue.DialogueManager.Instance.PlaySequence("Keuze_B_Aandringen");
                }
                else
                {
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlayVoice(AudioManager.Instance.voiceLucKeys, "Vader Luc");
                    }
                    ShowSubtitle("Vader Luc: \"Allee vooruit, omdat jij zo aandringt. Morgenochtend doe ik hem meteen.\"", 3.5f);
                }
            }

            StartCoroutine(StartCinematicTransitionToColon());
        }

        private IEnumerator StartCinematicTransitionToColon()
        {
            // Laat de keuze dialoog 2.5 seconden bezinken
            yield return new WaitForSeconds(2.5f);

            currentState = ExperienceState.TransitionToColon;

            if (CinematicTransitionDirector.Instance != null)
            {
                CinematicTransitionDirector.Instance.PlayShrinkToColonCutscene(() =>
                {
                    TeleportToDarmStart();
                });
            }
            else
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayWhoosh();
                yield return new WaitForSeconds(1.5f);
                TeleportToDarmStart();
            }
        }

        // ==========================================
        // DARM TELEPORTATIE FUNCTIES
        // ==========================================

        public void TeleportToDarmStart()
        {
            EnsureAllReferences();
            StopAllCoroutines();

            if (kitchenEnvironment != null) kitchenEnvironment.SetActive(false);
            if (colonEnvironment != null) colonEnvironment.SetActive(true);
            if (rewindSystem != null) rewindSystem.SetActive(false);
            ApplyEnvironmentLighting(true);
            ApplyEnvironmentLighting(true);

            if (playerChoice == ScreeningChoice.None) playerChoice = ScreeningChoice.IgnoredTrash;

            Vector3 cPos = colonStartAnchor != null ? colonStartAnchor.position : new Vector3(0.0f, -52.18f, 0.5f);
            TeleportPlayer(cPos, Quaternion.identity);

            // Zorg dat de speler direct ZELF vrij kan lopen in de darm
            var testPlayer = playerTransform != null ? playerTransform.GetComponent<SampleSceneTestPlayer>() : null;
            if (testPlayer != null) testPlayer.disableMovement = false;
            var charCtrl = playerTransform != null ? playerTransform.GetComponent<CharacterController>() : null;
            if (charCtrl != null) charCtrl.enabled = true;

            currentState = ExperienceState.ColonDarmwandeling;
            onColonEntered?.Invoke();

            if (Oncologie.Colon.ColonExperienceWalkController.Instance != null)
            {
                Oncologie.Colon.ColonExperienceWalkController.Instance.StartColonWalk();
            }
            else if (Oncologie.Colon.ColonPathFollower.Instance != null)
            {
                Oncologie.Colon.ColonPathFollower.Instance.StartColonWalk(playerTransform);
            }
            else if (colonController != null)
            {
                colonController.PlayColonSequence(playerChoice, OnColonSequenceFinished);
            }
        }

        public void TeleportToDarmPolyp()
        {
            EnsureAllReferences();
            StopAllCoroutines();

            if (kitchenEnvironment != null) kitchenEnvironment.SetActive(false);
            if (colonEnvironment != null) colonEnvironment.SetActive(true);
            if (rewindSystem != null) rewindSystem.SetActive(false);
            ApplyEnvironmentLighting(true);

            // Checkpoint 1: Speler staat op de darmvloer recht voor de poliep
            Vector3 polypFloorPos = new Vector3(-21.36f, -54.20f, 16.04f);
            Quaternion lookRot = Quaternion.Euler(0f, 323.0f, 0f);
            TeleportPlayer(polypFloorPos, lookRot);

            var testPlayer = playerTransform != null ? playerTransform.GetComponent<SampleSceneTestPlayer>() : null;
            if (testPlayer != null) testPlayer.disableMovement = false;
            var charCtrl = playerTransform != null ? playerTransform.GetComponent<CharacterController>() : null;
            if (charCtrl != null) charCtrl.enabled = true;

            currentState = ExperienceState.ColonDarmwandeling;

            if (Oncologie.Colon.PolypProgressionManager.Instance != null)
            {
                Oncologie.Colon.PolypProgressionManager.Instance.ApplyStage(1);
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopHeartbeat();
            }

            ShowSubtitle("Dr. Verteller: \"Kijk hier rechts op de darmwand: dit is een goedaardige poliep (adenoom). De test vindt dit direct!\"", 5.5f);
        }

        public void TeleportToDarmTumor()
        {
            EnsureAllReferences();
            StopAllCoroutines();

            playerChoice = ScreeningChoice.IgnoredTrash;

            if (kitchenEnvironment != null) kitchenEnvironment.SetActive(false);
            if (colonEnvironment != null) colonEnvironment.SetActive(true);
            if (rewindSystem != null) rewindSystem.SetActive(false);
            ApplyEnvironmentLighting(true);

            // Checkpoint 2: Speler staat op de darmvloer recht voor het gevorderde carcinoom
            Vector3 polypFloorPos = new Vector3(-21.36f, -54.20f, 16.04f);
            Quaternion lookRot = Quaternion.Euler(0f, 323.0f, 0f);
            TeleportPlayer(polypFloorPos, lookRot);

            var testPlayer = playerTransform != null ? playerTransform.GetComponent<SampleSceneTestPlayer>() : null;
            if (testPlayer != null) testPlayer.disableMovement = false;
            var charCtrl = playerTransform != null ? playerTransform.GetComponent<CharacterController>() : null;
            if (charCtrl != null) charCtrl.enabled = true;

            currentState = ExperienceState.ColonDarmwandeling;

            if (Oncologie.Colon.PolypProgressionManager.Instance != null)
            {
                Oncologie.Colon.PolypProgressionManager.Instance.ApplyStage(4);
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StartHeartbeat(true);
            }

            ShowSubtitle("Dr. Verteller: \"Fase 4 (Invasief Carcinoom): 3 jaar later is de poliep doorgegroeid in de darmwand en bloedvaten!\"", 6.0f);
        }

        public void TeleportToDarmSnare()
        {
            EnsureAllReferences();
            StopAllCoroutines();

            playerChoice = ScreeningChoice.InsistedKeys;

            if (kitchenEnvironment != null) kitchenEnvironment.SetActive(false);
            if (colonEnvironment != null) colonEnvironment.SetActive(true);
            if (rewindSystem != null) rewindSystem.SetActive(false);
            ApplyEnvironmentLighting(true);

            // Checkpoint 3: Resectie met medische lus
            Vector3 polypFloorPos = new Vector3(-21.36f, -54.20f, 16.04f);
            Quaternion lookRot = Quaternion.Euler(0f, 323.0f, 0f);
            TeleportPlayer(polypFloorPos, lookRot);

            var testPlayer = playerTransform != null ? playerTransform.GetComponent<SampleSceneTestPlayer>() : null;
            if (testPlayer != null) testPlayer.disableMovement = false;
            var charCtrl = playerTransform != null ? playerTransform.GetComponent<CharacterController>() : null;
            if (charCtrl != null) charCtrl.enabled = true;

            currentState = ExperienceState.ColonDarmwandeling;

            if (Oncologie.Colon.PolypProgressionManager.Instance != null)
            {
                Oncologie.Colon.PolypProgressionManager.Instance.ApplyStage(1);
                Oncologie.Colon.PolypProgressionManager.Instance.StartPolypRemovalAnimation();
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopHeartbeat();
                AudioManager.Instance.PlaySnareSnip();
            }

            ShowSubtitle("Dr. Verteller: \"Coloscopie Interventie: Met een medische lus wordt de poliep pijnloos weggenomen. Vader Luc is genezen!\"", 5.5f);
        }

        private void OnColonSequenceFinished()
        {
            StartCoroutine(ReturnToKitchenRoutine());
        }

        private IEnumerator ReturnToKitchenRoutine()
        {
            ShowSubtitle("Transitie terug naar de realiteit...", 2.0f);
            yield return new WaitForSeconds(2.0f);

            if (playerChoice == ScreeningChoice.IgnoredTrash)
                TeleportToKitchenOutroBad();
            else
                TeleportToKitchenOutroGood();
        }

        // ==========================================
        // SCÈNE 4: KEUKEN GEVOLGEN (EINDE)
        // ==========================================

        public void TeleportToKitchenOutroBad()
        {
            EnsureAllReferences();
            StopAllCoroutines();

            playerChoice = ScreeningChoice.IgnoredTrash;
            currentState = ExperienceState.KitchenOutro;

            if (colonEnvironment != null) colonEnvironment.SetActive(false);
            if (kitchenEnvironment != null) kitchenEnvironment.SetActive(true);
            ApplyEnvironmentLighting(false);

            Vector3 kPos = kitchenPlayerAnchor != null ? kitchenPlayerAnchor.position : new Vector3(-0.41f, 0f, -0.6f);
            TeleportPlayer(kPos, Quaternion.identity);

            SetupOutroScene(ScreeningChoice.IgnoredTrash);
        }

        public void TeleportToKitchenOutroGood()
        {
            EnsureAllReferences();
            StopAllCoroutines();

            playerChoice = ScreeningChoice.InsistedKeys;
            currentState = ExperienceState.KitchenOutro;

            if (colonEnvironment != null) colonEnvironment.SetActive(false);
            if (kitchenEnvironment != null) kitchenEnvironment.SetActive(true);
            ApplyEnvironmentLighting(false);

            Vector3 kPos = kitchenPlayerAnchor != null ? kitchenPlayerAnchor.position : new Vector3(-0.41f, 0f, -0.6f);
            TeleportPlayer(kPos, Quaternion.identity);

            SetupOutroScene(ScreeningChoice.InsistedKeys);
        }

        private void SetupOutroScene(ScreeningChoice choice)
        {
            EnsureAllReferences();
            if (outroPanel != null) outroPanel.SetActive(true);
            if (rewindSystem != null) rewindSystem.SetActive(true);

            // Verberg de envelop tijdens het einde (die is jaren geleden al verzonden/weggegooid)
            var env = GameObject.Find("envelope");
            if (env != null) env.SetActive(false);

            if (choice == ScreeningChoice.IgnoredTrash)
            {
                // SLECHT EINDE: Vader Luc is afwezig
                if (fatherCharacter != null) fatherCharacter.SetActive(false);
                if (hospitalBraceletPhoto != null) hospitalBraceletPhoto.SetActive(true);

                if (kitchenDirectionalLight != null)
                {
                    kitchenDirectionalLight.color = coldGrievingColor;
                    kitchenDirectionalLight.intensity = 0.45f;
                }

                if (greyscaleTransition != null)
                {
                    greyscaleTransition.FadeToGreyscale();
                }

                if (outroCardImage != null && badEndingCardTexture != null)
                {
                    outroCardImage.texture = badEndingCardTexture;
                }

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayAmbience(false);
                }

                // Pas tekst op 3D kaart aan voor slecht einde
                if (rewindSystem != null)
                {
                    var sub = rewindSystem.transform.Find("Rewind_Card_Canvas/Subtitle_Text");
                    if (sub != null)
                    {
                        var t = sub.GetComponent<UnityEngine.UI.Text>();
                        if (t != null) t.text = "Darmkanker geeft pas laat merkbare klachten.\nWachten kost levens. Vroege opsporing redt levens!";
                    }
                }

                if (Oncologie.Dialogue.DialogueManager.Instance != null)
                {
                    Oncologie.Dialogue.DialogueManager.Instance.PlaySequence("Einde_Slecht");
                }
                else
                {
                    ShowSubtitle("Darmkanker geeft pas laat klachten. Wacht niet tot het te laat is. Spoel de tijd terug.", 6.0f);
                }
            }
            else
            {
                // GOED EINDE: Vader Luc is gezond en aanwezig
                if (fatherCharacter != null) fatherCharacter.SetActive(true);
                if (hospitalBraceletPhoto != null) hospitalBraceletPhoto.SetActive(false);

                if (kitchenDirectionalLight != null)
                {
                    kitchenDirectionalLight.color = warmSunlightColor;
                    kitchenDirectionalLight.intensity = 1.35f;
                }

                if (greyscaleTransition != null)
                {
                    greyscaleTransition.FadeToColor();
                }

                if (outroCardImage != null && goodEndingCardTexture != null)
                {
                    outroCardImage.texture = goodEndingCardTexture;
                }

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayAmbience(true);
                }

                // Pas tekst op 3D kaart aan voor goed einde
                if (rewindSystem != null)
                {
                    var sub = rewindSystem.transform.Find("Rewind_Card_Canvas/Subtitle_Text");
                    if (sub != null)
                    {
                        var t = sub.GetComponent<UnityEngine.UI.Text>();
                        if (t != null) t.text = "Geweldig gedaan! Dankzij jouw aandringen werd de poliep op tijd gevonden.\nVader Luc is kankervrij en kerngezond!";
                    }
                }

                if (Oncologie.Dialogue.DialogueManager.Instance != null)
                {
                    Oncologie.Dialogue.DialogueManager.Instance.PlaySequence("Einde_Goed");
                }
                else
                {
                    ShowSubtitle("Vroege opsporing redt levens. De poliep werd tijdig verwijderd. Luc is kerngezond!", 6.0f);
                }
            }

            onOutroReached?.Invoke();
        }

        public void RewindTime()
        {
            StopAllCoroutines();
            EnsureAllReferences();

            if (Oncologie.Colon.ColonPathFollower.Instance != null)
            {
                Oncologie.Colon.ColonPathFollower.Instance.StopColonWalk();
            }

            if (Oncologie.Colon.PolypProgressionManager.Instance != null)
            {
                Oncologie.Colon.PolypProgressionManager.Instance.ApplyStage(1);
            }

            if (colonController != null) colonController.ResetVisuals();

            StartKitchenIntro();
        }

        // ==========================================
        // ONDERTITELING
        // ==========================================

        public void ShowSubtitle(string message, float duration = 4.0f)
        {
            _currentSubtitle = message;

            if (subtitleText != null)
            {
                subtitleText.text = message;
                subtitleText.gameObject.SetActive(true);
            }

            if (Oncologie.Dialogue.SubtitleUI.Instance != null)
            {
                string speaker = "Verteller";
                Color speakerColor = new Color(1f, 0.82f, 0.35f);
                string body = message;
                int colonIdx = message.IndexOf(':');
                if (colonIdx > 0 && colonIdx < 25)
                {
                    speaker = message.Substring(0, colonIdx).Trim(' ', '"');
                    body = message.Substring(colonIdx + 1).Trim(' ', '"');
                    if (speaker.ToLower().Contains("luc")) speakerColor = new Color(0.35f, 0.78f, 1f);
                    else if (speaker.ToLower().Contains("jij")) speakerColor = Color.white;
                }
                Oncologie.Dialogue.SubtitleUI.Instance.DisplayLine(new Oncologie.Dialogue.DialogueLine(speaker, speakerColor, body, duration));
            }

            if (_subtitleCoroutine != null) StopCoroutine(_subtitleCoroutine);
            _subtitleCoroutine = StartCoroutine(SubtitleRoutine(duration));
        }

        private IEnumerator SubtitleRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            _currentSubtitle = "";
            if (subtitleText != null) subtitleText.text = "";
            if (Oncologie.Dialogue.SubtitleUI.Instance != null)
            {
                Oncologie.Dialogue.SubtitleUI.Instance.ClearSubtitle();
            }
        }

        private void ApplyEnvironmentLighting(bool inColon)
        {
            if (inColon)
            {
                if (kitchenDirectionalLight != null) kitchenDirectionalLight.enabled = false;
                RenderSettings.fog = true;
                RenderSettings.fogColor = Color.black;
                RenderSettings.fogDensity = 0.035f;
                var cam = Camera.main;
                if (cam != null)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Color.black;
                }
            }
            else
            {
                if (kitchenDirectionalLight != null)
                {
                    kitchenDirectionalLight.enabled = true;
                    kitchenDirectionalLight.color = warmSunlightColor;
                }
                RenderSettings.fog = false;
                var cam = Camera.main;
                if (cam != null)
                {
                    cam.clearFlags = CameraClearFlags.Skybox;
                }
            }
        }

        private void TeleportPlayer(Vector3 targetPosition, Quaternion targetRotation)
        {
            EnsureAllReferences();
            if (playerTransform == null) return;

            var testPlayer = playerTransform.GetComponent<SampleSceneTestPlayer>();
            if (testPlayer != null)
            {
                testPlayer.Teleport(targetPosition, targetRotation);
                return;
            }

            var cc = playerTransform.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            playerTransform.SetPositionAndRotation(targetPosition, targetRotation);

            if (cc != null) cc.enabled = true;
        }

        // ==========================================
        // ONGUI TEST HUD & ONDERTITELING
        // ==========================================

        private void OnGUI()
        {
            // 1. Ondertitelingsbalk onderaan het scherm
            if (!string.IsNullOrEmpty(_currentSubtitle))
            {
                GUI.skin.box.fontSize = 17;
                GUI.skin.box.wordWrap = true;
                GUI.skin.box.alignment = TextAnchor.MiddleCenter;

                float boxWidth = Mathf.Min(Screen.width * 0.75f, 900f);
                float boxHeight = 65f;
                Rect subRect = new Rect((Screen.width - boxWidth) / 2f, Screen.height - 90f, boxWidth, boxHeight);

                GUI.backgroundColor = new Color(0.1f, 0.1f, 0.15f, 0.88f);
                GUI.Box(subRect, _currentSubtitle);
            }

            // 2. Instructie-hint in de keuken
            if (currentState == ExperienceState.KitchenIntro)
            {
                GUI.skin.box.fontSize = 13;
                GUI.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 0.75f);
                Rect hintRect = new Rect((Screen.width - 500f) / 2f, Screen.height - 130f, 500f, 32f);
                GUI.Box(hintRect, "<b>[Linkermuisknop ingedrukt]</b> Pak brief op ➜ Oud papier (A) of Vader Luc (B)");
            }

            // 3. Grote prominente rewind knop tijdens het einde
            if (currentState == ExperienceState.KitchenOutro)
            {
                GUI.skin.button.fontSize = 16;
                GUI.backgroundColor = new Color(0.96f, 0.76f, 0.20f, 0.95f);
                float btnWidth = 520f;
                float btnHeight = 52f;
                Rect rewindBtnRect = new Rect((Screen.width - btnWidth) / 2f, Screen.height - 150f, btnWidth, btnHeight);
                if (GUI.Button(rewindBtnRect, "<b>↺ TIJD TERUGSPOELEN (Druk op 'R' of Klik Hier)</b>"))
                {
                    RewindTime();
                }
            }

            // 4. Complete Quick-Test HUD voor elke scène
            if (_showDebugMenu)
            {
                GUI.skin.button.fontSize = 12;
                GUI.skin.box.fontSize = 12;
                GUILayout.BeginArea(new Rect(15, 15, 280, 440), GUI.skin.box);
                GUILayout.Label($"<b>ONCOLOGIE TEST CONTROLLER</b>\nFase: <color=yellow>{currentState}</color>");

                GUILayout.Space(3);
                GUILayout.Label("<b>--- 1. Keuken & Keuze ---</b>");
                if (GUILayout.Button("Keuken: Start / Intro")) StartKitchenIntro();
                if (GUILayout.Button("Optie A: In Prullenmand (Negeren)")) MakeChoice(ScreeningChoice.IgnoredTrash);
                if (GUILayout.Button("Optie B: Aan Vader Luc Geven (Aandringen)")) MakeChoice(ScreeningChoice.InsistedKeys);

                GUILayout.Space(3);
                GUILayout.Label("<b>--- 2. Darmwandeling Scènes ---</b>");
                if (GUILayout.Button("Darm: Start (Vrije Wandeling)")) TeleportToDarmStart();
                if (GUILayout.Button("Darm: Fase 1 (Adenoom Poliep)")) TeleportToDarmPolyp();
                if (GUILayout.Button("Darm: Fase 4 (Kanker & Uitzaaiing)")) TeleportToDarmTumor();
                if (GUILayout.Button("Darm: Coloscopie Lis-resectie")) TeleportToDarmSnare();

                GUILayout.Space(3);
                GUILayout.Label("<b>--- 3. Keuken Eindes ---</b>");
                if (GUILayout.Button("Keuken: Slecht Einde (Overleden)")) TeleportToKitchenOutroBad();
                if (GUILayout.Button("Keuken: Goed Einde (Gezond)")) TeleportToKitchenOutroGood();

                GUILayout.Space(3);
                if (GUILayout.Button("<b>↺ ALLES RESETTEN (R)</b>")) RewindTime();

                GUILayout.Label("<size=10>[Tab] Verberg HUD | [Esc] Muiscursor</size>");
                GUILayout.EndArea();
            }
        }
    }
}
