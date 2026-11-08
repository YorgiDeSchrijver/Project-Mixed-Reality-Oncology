using System;
using System.Collections.Generic;
using UnityEngine;
using Oncologie.Dialogue;
using Oncologie.Core;

namespace Oncologie.Colon
{
    /// <summary>
    /// Beheert de vloeiende beweging van de speler/camera langs de 36 darm-waypoints.
    /// Ondersteunt zowel automatische voortgang (cinematische rondleiding) als handmatige besturing (W/S of thumbstick).
    /// Triggert de medische vertelling en de kankergroei/resectie op de juiste plekken.
    /// </summary>
    public class ColonPathFollower : MonoBehaviour
    {
        public static ColonPathFollower Instance { get; private set; }

        [Header("Waypoints & Paden")]
        [SerializeField] private Transform pathContainer;
        [SerializeField] private List<Transform> waypoints = new List<Transform>();

        [Header("Speler Referentie")]
        [SerializeField] private Transform playerRig;

        [Header("Voortgang Instellingen")]
        [Range(0f, 1f)]
        public float progress = 0f;
        public float moveSpeed = 0.045f;
        public bool autoAdvance = true;

        [Header("Status & Triggers")]
        private bool _introTriggered = false;
        private bool _polypTriggered = false;
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
        }

        public void StartColonWalk(Transform player)
        {
            playerRig = player;
            progress = 0f;
            _introTriggered = false;
            _polypTriggered = false;
            _finishTriggered = false;
            _isActive = true;

            var tp = playerRig != null ? playerRig.GetComponent<SampleSceneTestPlayer>() : null;
            if (tp != null) tp.disableMovement = true;
            var cc = playerRig != null ? playerRig.GetComponent<CharacterController>() : null;
            if (cc != null) cc.enabled = false;

            if (waypoints.Count > 0 && playerRig != null)
            {
                playerRig.position = waypoints[0].position;
                playerRig.rotation = waypoints[0].rotation;
            }

            // Start introductie dialoog
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.PlaySequence("Darm_Intro");
                _introTriggered = true;
            }
        }

        public void StopColonWalk()
        {
            _isActive = false;
            var tp = playerRig != null ? playerRig.GetComponent<SampleSceneTestPlayer>() : null;
            if (tp != null) tp.disableMovement = false;
            var cc = playerRig != null ? playerRig.GetComponent<CharacterController>() : null;
            if (cc != null) cc.enabled = true;
        }

        private void Update()
        {
            if (!_isActive || waypoints.Count < 2 || playerRig == null) return;

            // 1. Handmatige invoer ondersteunen (W/S of pijltjestoetsen)
            float input = Input.GetAxis("Vertical");
            if (Mathf.Abs(input) > 0.1f)
            {
                progress += input * moveSpeed * 1.5f * Time.deltaTime;
            }
            else if (autoAdvance)
            {
                progress += moveSpeed * Time.deltaTime;
            }

            progress = Mathf.Clamp01(progress);

            // 2. Positie & rotatie interpoleren langs de waypoints
            UpdatePlayerPositionOnPath();

            // 3. Mijlpaal Triggers
            CheckMilestones();
        }

        private void UpdatePlayerPositionOnPath()
        {
            float totalSegments = waypoints.Count - 1;
            float rawIndex = progress * totalSegments;
            int idxA = Mathf.FloorToInt(rawIndex);
            int idxB = Mathf.Min(idxA + 1, waypoints.Count - 1);
            float segT = rawIndex - idxA;

            Vector3 pos = Vector3.Lerp(waypoints[idxA].position, waypoints[idxB].position, segT);
            Quaternion rot = Quaternion.Slerp(waypoints[idxA].rotation, waypoints[idxB].rotation, segT);

            playerRig.position = pos;
            // Alleen rotatie bijwerken als speler niet zelf vrij rondkijkt, of behoud yaw
            playerRig.rotation = Quaternion.Slerp(playerRig.rotation, rot, Time.deltaTime * 3f);
        }

        private void CheckMilestones()
        {
            // Mijlpaal 1: Aankomst bij PolypSpot (~55% van het pad)
            if (progress >= 0.52f && !_polypTriggered)
            {
                _polypTriggered = true;
                TriggerPolypEvent();
            }

            // Mijlpaal 2: Einde van de darmwandeling (~98% van het pad)
            if (progress >= 0.98f && !_finishTriggered)
            {
                _finishTriggered = true;
                FinishColonTour();
            }
        }

        private void TriggerPolypEvent()
        {
            ScreeningChoice choice = ExperienceFlowManager.Instance != null 
                ? ExperienceFlowManager.Instance.GetPlayerChoice() 
                : ScreeningChoice.IgnoredTrash;

            if (choice == ScreeningChoice.InsistedKeys)
            {
                // Goede Route: Snelle interventie
                if (DialogueManager.Instance != null)
                {
                    DialogueManager.Instance.PlaySequence("Darm_Route_B_Genezing");
                }
                if (PolypProgressionManager.Instance != null)
                {
                    PolypProgressionManager.Instance.StartPolypRemovalAnimation();
                }
            }
            else
            {
                // Slechte Route: 3 jaar later, kankergroei
                if (DialogueManager.Instance != null)
                {
                    DialogueManager.Instance.PlaySequence("Darm_Route_A_Kanker");
                }
                if (PolypProgressionManager.Instance != null)
                {
                    PolypProgressionManager.Instance.StartCancerGrowthAnimation(2.8f);
                }
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.StartHeartbeat(true);
                }
            }
        }

        private void FinishColonTour()
        {
            _isActive = false;
            var tp = playerRig != null ? playerRig.GetComponent<SampleSceneTestPlayer>() : null;
            if (tp != null) tp.disableMovement = false;
            var cc = playerRig != null ? playerRig.GetComponent<CharacterController>() : null;
            if (cc != null) cc.enabled = true;

            ScreeningChoice choice = ExperienceFlowManager.Instance != null 
                ? ExperienceFlowManager.Instance.GetPlayerChoice() 
                : ScreeningChoice.IgnoredTrash;

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
        }
    }
}
