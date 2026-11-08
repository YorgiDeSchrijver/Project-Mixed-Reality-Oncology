using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Oncologie.Dialogue;
using Oncologie.Core;

namespace Oncologie.Colon
{
    /// <summary>
    /// Cinematische regisseur van de darmwandeling.
    /// Stuurt de camera/speler langs de 36 waypoints, remt automatisch af en focust
    /// op de poliep (PolypSpot), regisseert de kankergroei/resectie met dialogen,
    /// en initieert de zoom-out terug naar de keuken via CinematicTransitionDirector.
    /// </summary>
    public class ColonPathFollower : MonoBehaviour
    {
        public static ColonPathFollower Instance { get; private set; }

        [Header("Waypoints & Paden")]
        [SerializeField] private Transform pathContainer;
        [SerializeField] private List<Transform> waypoints = new List<Transform>();
        [SerializeField] private Transform polypFocusTarget;

        [Header("Speler Referentie")]
        [SerializeField] private Transform playerRig;
        [SerializeField] private Camera playerCamera;

        [Header("Snelheden")]
        [SerializeField] private float cruiseSpeed = 0.035f;
        [SerializeField] private float currentSpeed = 0f;

        [Header("Voortgang")]
        [Range(0f, 1f)]
        public float progress = 0f;
        public bool isInspectingPolyp = false;

        private bool _introTriggered = false;
        private bool _polypReached = false;
        private bool _finishTriggered = false;
        private bool _isActive = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CollectWaypoints();
        }

        public void CollectWaypoints()
        {
            waypoints.Clear();
            if (pathContainer == null)
            {
                var pt = GameObject.Find("PlayerPath");
                if (pt != null) pathContainer = pt.transform;
            }

            if (pathContainer != null)
            {
                for (int i = 0; i < pathContainer.childCount; i++)
                {
                    waypoints.Add(pathContainer.GetChild(i));
                }
            }

            if (polypFocusTarget == null)
            {
                var ps = GameObject.Find("PolypSpot");
                if (ps != null) polypFocusTarget = ps.transform;
            }
        }

        public void StartColonWalk(Transform player)
        {
            playerRig = player;
            if (playerRig != null) playerCamera = playerRig.GetComponentInChildren<Camera>();

            var tp = playerRig != null ? playerRig.GetComponent<SampleSceneTestPlayer>() : null;
            if (tp != null) tp.disableMovement = true;
            var cc = playerRig != null ? playerRig.GetComponent<CharacterController>() : null;
            if (cc != null) cc.enabled = false;

            progress = 0f;
            currentSpeed = cruiseSpeed;
            _introTriggered = false;
            _polypReached = false;
            _finishTriggered = false;
            isInspectingPolyp = false;
            _isActive = true;

            if (waypoints.Count > 0 && playerRig != null)
            {
                playerRig.position = waypoints[0].position;
                playerRig.rotation = waypoints[0].rotation;
            }

            // Start medische darm-intro dialoog
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.PlaySequence("Darm_Intro");
                _introTriggered = true;
            }
        }

        public void StopColonWalk()
        {
            _isActive = false;
            isInspectingPolyp = false;

            var tp = playerRig != null ? playerRig.GetComponent<SampleSceneTestPlayer>() : null;
            if (tp != null) tp.disableMovement = false;
            var cc = playerRig != null ? playerRig.GetComponent<CharacterController>() : null;
            if (cc != null) cc.enabled = true;
        }

        private void Update()
        {
            if (!_isActive || waypoints.Count < 2 || playerRig == null) return;

            // Als we bij de poliep stilstaan voor de demonstratie, beweeg niet vooruit
            if (isInspectingPolyp)
            {
                // Richt camera zachtjes op de poliep
                if (polypFocusTarget != null)
                {
                    Vector3 lookDir = (polypFocusTarget.position - playerRig.position).normalized;
                    Quaternion targetRot = Quaternion.LookRotation(lookDir);
                    playerRig.rotation = Quaternion.Slerp(playerRig.rotation, targetRot, Time.deltaTime * 2.5f);
                }
                return;
            }

            // Automatische voortgang
            progress += currentSpeed * Time.deltaTime;
            progress = Mathf.Clamp01(progress);

            UpdatePositionAndRotation();

            // Mijlpaal 1: Aankomst bij PolypSpot (~54% van de tunnel)
            if (progress >= 0.52f && !_polypReached)
            {
                _polypReached = true;
                StartCoroutine(PolypShowcaseRoutine());
            }

            // Mijlpaal 2: Einde van de darmwandeling (~98% van de tunnel)
            if (progress >= 0.98f && !_finishTriggered)
            {
                _finishTriggered = true;
                FinishColonTour();
            }
        }

        private void UpdatePositionAndRotation()
        {
            float totalSegments = waypoints.Count - 1;
            float rawIndex = progress * totalSegments;
            int idxA = Mathf.FloorToInt(rawIndex);
            int idxB = Mathf.Min(idxA + 1, waypoints.Count - 1);
            float segT = rawIndex - idxA;

            Vector3 pos = Vector3.Lerp(waypoints[idxA].position, waypoints[idxB].position, segT);
            Quaternion rot = Quaternion.Slerp(waypoints[idxA].rotation, waypoints[idxB].rotation, segT);

            playerRig.position = pos;
            playerRig.rotation = Quaternion.Slerp(playerRig.rotation, rot, Time.deltaTime * 4f);
        }

        private IEnumerator PolypShowcaseRoutine()
        {
            isInspectingPolyp = true;

            ScreeningChoice choice = ExperienceFlowManager.Instance != null 
                ? ExperienceFlowManager.Instance.GetPlayerChoice() 
                : ScreeningChoice.IgnoredTrash;

            if (choice == ScreeningChoice.InsistedKeys)
            {
                // GOEDE ROUTE: Coloscopie en poliepresectie
                if (DialogueManager.Instance != null)
                {
                    DialogueManager.Instance.PlaySequence("Darm_Route_B_Genezing");
                }

                yield return new WaitForSeconds(1.5f);

                if (PolypProgressionManager.Instance != null)
                {
                    PolypProgressionManager.Instance.StartPolypRemovalAnimation();
                }

                // Wacht tot resectie en dialoog rustig klaar zijn
                yield return new WaitForSeconds(7.0f);
            }
            else
            {
                // SLECHTE ROUTE: Drie jaar later, mutatie naar kankerstadium 4
                if (DialogueManager.Instance != null)
                {
                    DialogueManager.Instance.PlaySequence("Darm_Route_A_Kanker");
                }

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.StartHeartbeat(true);
                }

                yield return new WaitForSeconds(2.0f);

                if (PolypProgressionManager.Instance != null)
                {
                    PolypProgressionManager.Instance.StartCancerGrowthAnimation(2.5f);
                }

                // Wacht tot alle 4 stadia en uitzaaiing zichtbaar zijn geworden
                yield return new WaitForSeconds(10.5f);
            }

            // Hervat rustig de tocht naar de uitgang van de tunnel
            isInspectingPolyp = false;
            currentSpeed = cruiseSpeed * 1.2f;
        }

        private void FinishColonTour()
        {
            _isActive = false;

            ScreeningChoice choice = ExperienceFlowManager.Instance != null 
                ? ExperienceFlowManager.Instance.GetPlayerChoice() 
                : ScreeningChoice.IgnoredTrash;

            // Start cinematische uitzoom-cutscene terug naar de keuken
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
                // Direct fallback
                if (ExperienceFlowManager.Instance != null)
                {
                    if (choice == ScreeningChoice.InsistedKeys) ExperienceFlowManager.Instance.TeleportToKitchenOutroGood();
                    else ExperienceFlowManager.Instance.TeleportToKitchenOutroBad();
                }
            }
        }
    }
}
