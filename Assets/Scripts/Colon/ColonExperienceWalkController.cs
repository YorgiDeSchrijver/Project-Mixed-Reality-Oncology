using System.Collections;
using UnityEngine;
using Oncologie.Core;
using Oncologie.Dialogue;

namespace Oncologie.Colon
{
    /// <summary>
    /// Vrije loopervaring in de darm (GEEN automatische cutscene).
    /// De speler kan zelf rustig rondlopen met WASD of VR-joystick.
    /// Bevat zone-detectie bij de poliep (met rustige, seniorenvriendelijke kankergroei)
    /// en een uitgang-trigger die de speler terugbrengt naar de keuken.
    /// </summary>
    public class ColonExperienceWalkController : MonoBehaviour
    {
        public static ColonExperienceWalkController Instance { get; private set; }

        [Header("Referenties")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Transform polypSpot;
        [SerializeField] private Transform exitAnchor;

        [Header("Afstanden & Drempels")]
        [SerializeField] private float polypDetectionRange = 7.5f;
        [SerializeField] private float exitDetectionRange = 3.5f;

        [Header("Status")]
        public bool isPlayerInside = false;
        private bool _introPlayed = false;
        private bool _polypShowcaseStarted = false;
        private bool _canExit = false; // Pas waar zodra de volledige kankergroei/demonstratie is afgerond!
        private bool _exitReached = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureReferences();
        }

        public void EnsureReferences()
        {
            if (playerTransform == null || !playerTransform.gameObject.activeInHierarchy)
            {
                var xr = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
                if (xr != null && xr.gameObject.activeInHierarchy) playerTransform = xr.transform;
                else
                {
                    var p = GameObject.Find("Test Player");
                    if (p != null && p.activeInHierarchy) playerTransform = p.transform;
                    else if (Camera.main != null) playerTransform = Camera.main.transform;
                }
            }

            if (polypSpot == null)
            {
                var ps = transform.Find("PolypSpot");
                if (ps != null) polypSpot = ps;
            }

            if (exitAnchor == null)
            {
                var exitPortal = transform.Find("Colon_Exit_Portal");
                if (exitPortal != null) exitAnchor = exitPortal;
                else
                {
                    var pt35 = transform.Find("PlayerPath/PathPoint_035");
                    if (pt35 != null) exitAnchor = pt35;
                }
            }
        }

        public void StartColonWalk()
        {
            EnsureReferences();
            isPlayerInside = true;
            _introPlayed = false;
            _polypShowcaseStarted = false;
            _canExit = false;
            _exitReached = false;

            // Zorg dat de speler direct VRIJ kan lopen
            if (playerTransform != null)
            {
                var tp = playerTransform.GetComponent<SampleSceneTestPlayer>();
                if (tp != null) tp.disableMovement = false;

                var cc = playerTransform.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = true;
            }

            // Start introductie dialoog
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.PlaySequence("Darm_Intro");
                _introPlayed = true;
            }
        }

        private void Update()
        {
            if (!isPlayerInside || playerTransform == null) return;

            // 1. Detecteer nabijheid van PolypSpot
            if (!_polypShowcaseStarted && polypSpot != null)
            {
                float distToPolyp = Vector3.Distance(playerTransform.position, polypSpot.position);
                if (distToPolyp <= polypDetectionRange)
                {
                    _polypShowcaseStarted = true;
                    StartCoroutine(ElderlyFriendlyPolypRoutine());
                }
            }

            // 2. Detecteer het bereiken van de tunnel-uitgang
            // CRUCIAAL: Mag PAS getriggerd worden als de kankerdemonstratie VOLLEDIG is afgerond (_canExit == true)!
            if (_canExit && !_exitReached && exitAnchor != null)
            {
                float distToExit = Vector3.Distance(playerTransform.position, exitAnchor.position);
                if (distToExit <= exitDetectionRange)
                {
                    _exitReached = true;
                    OnExitReached();
                }
            }
        }

        /// <summary>
        /// Rustige, meeslepende demonstratie bij de poliep.
        /// De speler blijft rustig voor de tumor staan en ziet de groei van Fase 1 naar Fase 4,
        /// terwijl de verteller uitlegt dat vader Luc al die jaren van niets weet.
        /// </summary>
        private IEnumerator ElderlyFriendlyPolypRoutine()
        {
            ScreeningChoice choice = ExperienceFlowManager.Instance != null 
                ? ExperienceFlowManager.Instance.GetPlayerChoice() 
                : ScreeningChoice.IgnoredTrash;

            if (choice == ScreeningChoice.InsistedKeys)
            {
                // GOEDE ROUTE: Vroege opsporing & genezing
                if (DialogueManager.Instance != null)
                {
                    DialogueManager.Instance.PlaySingleLine(new DialogueLine(
                        "Dr. Verteller",
                        new Color(0.35f, 0.85f, 1f),
                        "Dankzij jouw aandringen werd de test opgestuurd. De artsen vonden het adenoom in een vroeg stadium!",
                        6.5f
                    ));
                }

                yield return new WaitForSeconds(3.0f);

                if (PolypProgressionManager.Instance != null)
                {
                    PolypProgressionManager.Instance.StartPolypRemovalAnimation();
                }

                // Geef rustig de tijd om de verwijdering te observeren (10 seconden)
                yield return new WaitForSeconds(10.0f);

                if (DialogueManager.Instance != null)
                {
                    DialogueManager.Instance.PlaySingleLine(new DialogueLine(
                        "Dr. Verteller",
                        new Color(1f, 0.85f, 0.35f),
                        "De medische lis heeft de poliep pijnloos weggenomen. Kanker heeft nooit een kans gekregen. Vader Luc is genezen!",
                        7.5f
                    ));
                }

                yield return new WaitForSeconds(7.5f);

                // Nu pas mag de speler naar de uitgang lopen
                _canExit = true;

                if (DialogueManager.Instance != null)
                {
                    DialogueManager.Instance.PlaySingleLine(new DialogueLine(
                        "Dr. Verteller",
                        new Color(0.9f, 0.9f, 0.9f),
                        "Loop nu rustig verder door de tunnel naar het portaal om terug te keren naar de keuken.",
                        7.0f
                    ));
                }
            }
            else
            {
                // SLECHTE ROUTE: Rustige kankergroei van Fase 1 naar Fase 4
                // De speler blijft hier en ziet stap voor stap hoe het erger wordt terwijl vader Luc nergens van weet
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.StartHeartbeat(false);
                }

                // Fase 1: Zichtbaar maken adenoom (8.5 seconden)
                if (PolypProgressionManager.Instance != null)
                {
                    PolypProgressionManager.Instance.ApplyStage(1);
                }
                if (DialogueManager.Instance != null)
                {
                    DialogueManager.Instance.PlaySingleLine(new DialogueLine(
                        "Dr. Verteller",
                        new Color(1f, 0.85f, 0.35f),
                        "Kijk goed naar de darmwand. Dit kleine knobbeltje is een adenoom (poliep). Vader Luc voelt zich kerngezond en merkt hier helemaal niets van.",
                        8.5f
                    ));
                }
                yield return new WaitForSeconds(9.0f);

                // Fase 2: Uitgroei na 2 jaar (9 seconden)
                if (PolypProgressionManager.Instance != null)
                {
                    PolypProgressionManager.Instance.ApplyStage(2);
                }
                if (DialogueManager.Instance != null)
                {
                    DialogueManager.Instance.PlaySingleLine(new DialogueLine(
                        "Dr. Verteller",
                        new Color(1f, 0.70f, 0.35f),
                        "2 jaar later... De kankercellen delen zich ongemerkt dieper in het slijmvlies. Luc heeft nergens last van en weet nog van niets.",
                        8.5f
                    ));
                }
                yield return new WaitForSeconds(9.0f);

                // Fase 3: Gevorderd carcinoom na 3 jaar (9 seconden)
                if (PolypProgressionManager.Instance != null)
                {
                    PolypProgressionManager.Instance.ApplyStage(3);
                }
                if (DialogueManager.Instance != null)
                {
                    DialogueManager.Instance.PlaySingleLine(new DialogueLine(
                        "Dr. Verteller",
                        new Color(1f, 0.55f, 0.35f),
                        "3 jaar later... De poliep is veranderd in een kwaadaardige tumor die door de darmwand dringt. Luc voelt nog steeds geen énkele pijn.",
                        8.5f
                    ));
                }
                yield return new WaitForSeconds(9.0f);

                // Fase 4: Invasieve tumor met bloedvatinfiltratie na 5 jaar (11 seconden)
                if (PolypProgressionManager.Instance != null)
                {
                    PolypProgressionManager.Instance.ApplyStage(4);
                }
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.StartHeartbeat(true);
                }
                if (DialogueManager.Instance != null)
                {
                    DialogueManager.Instance.PlaySingleLine(new DialogueLine(
                        "Dr. Verteller",
                        new Color(1f, 0.35f, 0.35f),
                        "5 jaar later... De tumor breekt door de darmwand en dringt de bloedvaten binnen: uitzaaiing. Pas nu krijgt Luc pijn... maar nu is het te laat.",
                        9.5f
                    ));
                }
                yield return new WaitForSeconds(10.5f);

                // Nu pas is de demonstratie klaar en mag de speler naar de uitgang
                _canExit = true;

                if (DialogueManager.Instance != null)
                {
                    DialogueManager.Instance.PlaySingleLine(new DialogueLine(
                        "Dr. Verteller",
                        new Color(0.9f, 0.9f, 0.9f),
                        "De ziekte is onbehandelbaar. Loop nu rustig verder door de tunnel naar het portaal om terug te keren.",
                        8.0f
                    ));
                }
            }
        }

        private void OnExitReached()
        {
            isPlayerInside = false;

            ScreeningChoice choice = ExperienceFlowManager.Instance != null 
                ? ExperienceFlowManager.Instance.GetPlayerChoice() 
                : ScreeningChoice.IgnoredTrash;

            if (CinematicTransitionDirector.Instance != null)
            {
                CinematicTransitionDirector.Instance.PlayZoomOutToKitchenCutscene(() =>
                {
                    if (ExperienceFlowManager.Instance != null)
                    {
                        if (choice == ScreeningChoice.InsistedKeys)
                        {
                            ExperienceFlowManager.Instance.TeleportToKitchenOutroGood();
                        }
                        else
                        {
                            ExperienceFlowManager.Instance.TeleportToKitchenOutroBad();
                        }
                    }
                });
            }
            else
            {
                if (ExperienceFlowManager.Instance != null)
                {
                    if (choice == ScreeningChoice.InsistedKeys) ExperienceFlowManager.Instance.TeleportToKitchenOutroGood();
                    else ExperienceFlowManager.Instance.TeleportToKitchenOutroBad();
                }
            }
        }
    }
}
