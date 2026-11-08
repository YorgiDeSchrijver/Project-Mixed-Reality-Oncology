using System;
using System.Collections;
using UnityEngine;

namespace Oncologie.Core
{
    /// <summary>
    /// Beheert de medische darmwandeling:
    /// - Gezonde wand + goedaardige poliep (adenoom)
    /// - Fysieke checkpoints / triggers tijdens het lopen door het lumen
    /// - Checkpoint 1 (Z~10.5): Uitleg over de goedaardige poliep (symptoomloos, microscopisch bloed)
    /// - Checkpoint 2 (Z~14.5):
    ///     * Slechte route: "3 jaar later", versnelde hartslag, donkerrode belichting, woekerende tumor
    ///     * Goede route: Medische coloscopie-lis daalt neer, knipt poliep pijnloos weg
    /// - Checkpoint 3 (Z~19.5): Einde van de darmwandeling, transitie terug naar de keuken
    /// </summary>
    public class ColonExperienceController : MonoBehaviour
    {
        [Header("Poliep & Kanker Visuals")]
        [Tooltip("Goedaardige poliep (kleine zwelling).")]
        [SerializeField] private GameObject benignPolypObject;

        [Tooltip("Kwaadaardige uitgegroeide tumor (cancer 1 mesh).")]
        [SerializeField] private GameObject malignantTumorObject;

        [Tooltip("Visuele coloscoop lis / medisch instrument.")]
        [SerializeField] private GameObject colonoscopeSnareObject;

        [Header("Darm Verlichting")]
        [SerializeField] private Light colonLight;
        [SerializeField] private Color healthyColonLightColor = new Color(1f, 0.65f, 0.55f);
        [SerializeField] private Color diseasedColonLightColor = new Color(0.40f, 0.10f, 0.15f);

        [Header("Speler Referentie")]
        [SerializeField] private Transform playerTransform;

        private ScreeningChoice _activeChoice = ScreeningChoice.IgnoredTrash;
        private Action _onFinishedCallback;
        private bool _cp1Triggered = false;
        private bool _cp2Triggered = false;
        private bool _cp3Triggered = false;
        private bool _isWalkingActive = false;

        private void Awake()
        {
            EnsureReferences();
            ResetVisuals();
        }

        public void EnsureReferences()
        {
            if (benignPolypObject == null)
            {
                var p = GameObject.Find("Benign_Polyp");
                if (p != null) benignPolypObject = p;
            }

            if (malignantTumorObject == null)
            {
                var c = GameObject.Find("cancer 1");
                if (c != null) malignantTumorObject = c;
            }

            if (colonoscopeSnareObject == null)
            {
                var s = GameObject.Find("Colonoscope_Snare");
                if (s != null) colonoscopeSnareObject = s;
            }

            if (colonLight == null)
            {
                var l = GameObject.Find("Colon_InternalLight_1");
                if (l != null) colonLight = l.GetComponent<Light>();
            }

            if (playerTransform == null)
            {
                var player = FindAnyObjectByType<SampleSceneTestPlayer>();
                if (player != null) playerTransform = player.transform;
                else if (Camera.main != null) playerTransform = Camera.main.transform;
            }
        }

        public void ResetVisuals()
        {
            EnsureReferences();
            _cp1Triggered = false;
            _cp2Triggered = false;
            _cp3Triggered = false;
            _isWalkingActive = false;

            if (benignPolypObject != null)
            {
                benignPolypObject.SetActive(true);
                benignPolypObject.transform.localScale = Vector3.one * 0.2f;
            }

            if (malignantTumorObject != null)
            {
                malignantTumorObject.SetActive(false);
            }

            if (colonoscopeSnareObject != null)
            {
                colonoscopeSnareObject.SetActive(false);
            }

            if (colonLight != null)
            {
                colonLight.color = healthyColonLightColor;
                colonLight.intensity = 3.0f;
            }
        }

        public void PlayColonSequence(ScreeningChoice choice, Action onSequenceFinished)
        {
            ResetVisuals();
            _activeChoice = choice;
            _onFinishedCallback = onSequenceFinished;
            _isWalkingActive = true;

            // Start rustige hartslag
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StartHeartbeat(false);
            }

            if (ExperienceFlowManager.Instance != null)
            {
                ExperienceFlowManager.Instance.ShowSubtitle(
                    "Je bevindt je in de dikke darm van je vader. Loop naar voren om de wand te inspecteren...",
                    4.0f
                );
            }
        }

        private void Update()
        {
            if (!_isWalkingActive) return;

            EnsureReferences();
            if (playerTransform == null) return;

            float playerZ = playerTransform.position.z;

            // Checkpoint 1: Speler bereikt de goedaardige poliep (Z >= 10.5m)
            if (!_cp1Triggered && playerZ >= 10.5f)
            {
                _cp1Triggered = true;
                TriggerCheckpoint1();
            }

            // Checkpoint 2: Speler bereikt het keuzemoment / tijdssprong (Z >= 14.5f)
            if (!_cp2Triggered && playerZ >= 14.5f)
            {
                _cp2Triggered = true;
                TriggerCheckpoint2();
            }

            // Checkpoint 3: Speler bereikt het einde van de darmwandeling (Z >= 19.5m)
            if (!_cp3Triggered && playerZ >= 19.5f)
            {
                _cp3Triggered = true;
                TriggerCheckpoint3();
            }
        }

        private void TriggerCheckpoint1()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayVoice(AudioManager.Instance.voiceDoctorPolypExplanation, "Arts");
            }
            if (ExperienceFlowManager.Instance != null)
            {
                ExperienceFlowManager.Instance.ShowSubtitle(
                    "Arts: \"Kijk hier op de wand: dit kleine bobbeltje is een adenoom (goedaardige poliep). Luc voelt hier niets van, maar de screeningstest detecteert nu al microscopisch bloed.\"",
                    6.0f
                );
            }
        }

        private void TriggerCheckpoint2()
        {
            StartCoroutine(Checkpoint2Routine());
        }

        private IEnumerator Checkpoint2Routine()
        {
            if (_activeChoice == ScreeningChoice.InsistedKeys)
            {
                // GOEDE ROUTE: Poliep wordt verwijderd met lis
                if (colonoscopeSnareObject != null)
                {
                    colonoscopeSnareObject.SetActive(true);
                }

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayVoice(AudioManager.Instance.voiceDoctorPolypRemoved, "Arts");
                }
                if (ExperienceFlowManager.Instance != null)
                {
                    ExperienceFlowManager.Instance.ShowSubtitle(
                        "Arts: \"Omdat je vader meedeed, ontdekten we de poliep op tijd. Met deze medische lus nemen we hem direct en pijnloos weg.\"",
                        5.0f
                    );
                }

                yield return new WaitForSeconds(2.0f);

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySnareSnip();
                }

                // Poliep verdwijnt
                if (benignPolypObject != null) benignPolypObject.SetActive(false);
                if (colonoscopeSnareObject != null) colonoscopeSnareObject.SetActive(false);

                if (ExperienceFlowManager.Instance != null)
                {
                    ExperienceFlowManager.Instance.ShowSubtitle(
                        "Arts: \"De darmwand is schoon en gezond. Vader Luc is genezen vóór er kanker kon ontstaan!\"",
                        5.0f
                    );
                }
            }
            else
            {
                // SLECHTE ROUTE: Brief genegeerd -> 3 jaar later groeit tumor
                if (ExperienceFlowManager.Instance != null)
                {
                    ExperienceFlowManager.Instance.ShowSubtitle("--- 3 JAAR LATER ---", 3.0f);
                }

                // Versnelde hartslag (~95 BPM)
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.StartHeartbeat(true);
                }

                yield return new WaitForSeconds(2.5f);

                // Belichting wordt ziekelijk donkerrood
                if (colonLight != null)
                {
                    colonLight.color = diseasedColonLightColor;
                    colonLight.intensity = 1.8f;
                }

                // Kleine poliep wordt vervangen door uitgegroeide grillige tumor
                if (benignPolypObject != null) benignPolypObject.SetActive(false);
                if (malignantTumorObject != null)
                {
                    malignantTumorObject.SetActive(true);
                    malignantTumorObject.transform.localScale = Vector3.one * 0.9f;
                }

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayVoice(AudioManager.Instance.voiceDoctorCancerFormed, "Arts");
                }
                if (ExperienceFlowManager.Instance != null)
                {
                    ExperienceFlowManager.Instance.ShowSubtitle(
                        "Arts: \"Zonder screening kon de poliep 3 jaar lang ongemerkt woekeren tot een agressieve tumor die door de wand breekt. Pas nú ontstaan er klachten... maar veel te laat.\"",
                        6.0f
                    );
                }
            }
        }

        private void TriggerCheckpoint3()
        {
            _isWalkingActive = false;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopHeartbeat();
            }

            if (ExperienceFlowManager.Instance != null)
            {
                ExperienceFlowManager.Instance.ShowSubtitle("Transitie terug naar de keuken...", 2.5f);
            }
            StartCoroutine(FinishAfterDelay(2.5f));
        }

        private IEnumerator FinishAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            _onFinishedCallback?.Invoke();
        }
    }
}
