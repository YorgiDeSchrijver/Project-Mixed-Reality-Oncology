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
            if (playerTransform == null)
            {
                var p = GameObject.Find("Test Player");
                if (p != null) playerTransform = p.transform;
                else if (Camera.main != null) playerTransform = Camera.main.transform;
            }

            if (polypSpot == null)
            {
                var ps = transform.Find("PolypSpot");
                if (ps != null) polypSpot = ps;
            }

            if (exitAnchor == null)
            {
                var pt35 = transform.Find("PlayerPath/PathPoint_035");
                if (pt35 != null) exitAnchor = pt35;
            }
        }

        public void StartColonWalk()
        {
            EnsureReferences();
            isPlayerInside = true;
            _introPlayed = false;
            _polypShowcaseStarted = false;
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
            if (!_exitReached && exitAnchor != null && _polypShowcaseStarted)
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
        /// Rustige, seniorenvriendelijke demonstratie bij de poliep (ruime leestijd).
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
                    DialogueManager.Instance.PlaySequence("Darm_Route_B_Genezing");
                }

                yield return new WaitForSeconds(3.0f);

                if (PolypProgressionManager.Instance != null)
                {
                    PolypProgressionManager.Instance.StartPolypRemovalAnimation();
                }

                // Geef rustig de tijd om de verwijdering te observeren (12 seconden)
                yield return new WaitForSeconds(12.0f);

                if (DialogueManager.Instance != null)
                {
                    DialogueManager.Instance.PlaySingleLine(new DialogueLine(
                        "Dr. Verteller",
                        new Color(1f, 0.85f, 0.35f),
                        "De darmwand is volledig hersteld. Loop gerust verder door de tunnel naar de uitgang.",
                        7.0f
                    ));
                }
            }
            else
            {
                // SLECHTE ROUTE: Rustige kankergroei van Fase 1 naar Fase 4 (9 seconden per fase)
                if (DialogueManager.Instance != null)
                {
                    DialogueManager.Instance.PlaySequence("Darm_Route_A_Kanker");
                }

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.StartHeartbeat(false);
                }

                // Fase 1: Zichtbaar maken adenoom (8 seconden leestijd)
                if (PolypProgressionManager.Instance != null)
                {
                    PolypProgressionManager.Instance.ApplyStage(1);
                }
                yield return new WaitForSeconds(8.0f);

                // Fase 2: Uitgroei na 1-2 jaar (9 seconden leestijd)
                if (PolypProgressionManager.Instance != null)
                {
                    PolypProgressionManager.Instance.ApplyStage(2);
                }
                yield return new WaitForSeconds(9.0f);

                // Fase 3: Gevorderd carcinoom (9 seconden leestijd)
                if (PolypProgressionManager.Instance != null)
                {
                    PolypProgressionManager.Instance.ApplyStage(3);
                }
                yield return new WaitForSeconds(9.0f);

                // Fase 4: Invasieve tumor met bloedvatinfiltratie (12 seconden leestijd)
                if (PolypProgressionManager.Instance != null)
                {
                    PolypProgressionManager.Instance.ApplyStage(4);
                }
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.StartHeartbeat(true);
                }
                yield return new WaitForSeconds(12.0f);

                // Vriendelijke aanwijzing om naar de uitgang te lopen
                if (DialogueManager.Instance != null)
                {
                    DialogueManager.Instance.PlaySingleLine(new DialogueLine(
                        "Dr. Verteller",
                        new Color(1f, 0.45f, 0.4f),
                        "De ziekte is uitgezaaid. Loop nu rustig verder door de tunnel naar de uitgang.",
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
