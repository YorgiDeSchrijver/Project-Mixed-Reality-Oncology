using System.Collections;
using UnityEngine;

namespace Oncologie.Colon
{
    /// <summary>
    /// Beheert de 4 stadia van poliep- en kankergroei in de crypt/darmwand.
    /// Schakelt tussen Stage 1 (adenoom/poliep), Stage 2 (uitgroei),
    /// Stage 3 (gevorderd carcinoom) en Stage 4 (invasief carcinoom met uitzaaiing).
    /// Bevat tevens de coloscopie-resectie animatie voor de goede afloop.
    /// </summary>
    public class PolypProgressionManager : MonoBehaviour
    {
        public static PolypProgressionManager Instance { get; private set; }

        [Header("Stadia Modellen (FBX)")]
        [SerializeField] private GameObject stage1Cluster;
        [SerializeField] private GameObject stage2Cluster;
        [SerializeField] private GameObject stage3Cluster;
        [SerializeField] private GameObject stage4Cluster;

        [Header("Biologische Details")]
        [SerializeField] private GameObject capillaryObject;
        [SerializeField] private GameObject cryptBaseObject;
        [SerializeField] private GameObject cryptSurfaceObject;

        [Header("Interventie (Goede Route)")]
        [SerializeField] private GameObject colonoscopeSnare;
        [SerializeField] private ParticleSystem healingParticles;

        [Header("Huidig Stadium")]
        [Range(1, 4)]
        [SerializeField] private int currentStage = 1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            ApplyStage(1);
            if (colonoscopeSnare != null) colonoscopeSnare.SetActive(false);
        }

        public void ApplyStage(int stage)
        {
            currentStage = Mathf.Clamp(stage, 1, 4);

            if (stage1Cluster != null) stage1Cluster.SetActive(currentStage == 1);
            if (stage2Cluster != null) stage2Cluster.SetActive(currentStage == 2);
            if (stage3Cluster != null) stage3Cluster.SetActive(currentStage == 3);
            if (stage4Cluster != null) stage4Cluster.SetActive(currentStage == 4);

            // Capillair wordt cruciaal bij stage 3 en 4 voor visualisatie van ingroei
            if (capillaryObject != null)
            {
                capillaryObject.SetActive(currentStage >= 2);
            }
        }

        /// <summary>
        /// Animeert de geleidelijke mutatie en doorgroei van Stage 1 naar Stage 4 (Slechte route).
        /// </summary>
        public void StartCancerGrowthAnimation(float durationPerStage = 2.5f)
        {
            StartCoroutine(CancerGrowthRoutine(durationPerStage));
        }

        private IEnumerator CancerGrowthRoutine(float duration)
        {
            ApplyStage(1);
            yield return new WaitForSeconds(duration);

            ApplyStage(2);
            yield return new WaitForSeconds(duration);

            ApplyStage(3);
            yield return new WaitForSeconds(duration);

            ApplyStage(4);
        }

        /// <summary>
        /// Animeert het verwijderen van de poliep via de coloscoop (Goede route).
        /// </summary>
        public void StartPolypRemovalAnimation()
        {
            StartCoroutine(PolypRemovalRoutine());
        }

        private IEnumerator PolypRemovalRoutine()
        {
            // Zorg dat we op stadium 1 staan
            ApplyStage(1);

            if (colonoscopeSnare != null)
            {
                colonoscopeSnare.SetActive(true);
                Vector3 startPos = colonoscopeSnare.transform.localPosition + Vector3.up * 2f;
                Vector3 targetPos = colonoscopeSnare.transform.localPosition;

                // Daal af naar de poliep
                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime * 1.5f;
                    colonoscopeSnare.transform.localPosition = Vector3.Lerp(startPos, targetPos, t);
                    yield return null;
                }

                yield return new WaitForSeconds(0.8f);

                // Poliep krimpt / wordt verwijderd
                if (stage1Cluster != null)
                {
                    Vector3 origScale = stage1Cluster.transform.localScale;
                    t = 0f;
                    while (t < 1f)
                    {
                        t += Time.deltaTime * 2f;
                        stage1Cluster.transform.localScale = Vector3.Lerp(origScale, Vector3.zero, t);
                        yield return null;
                    }
                    stage1Cluster.SetActive(false);
                    stage1Cluster.transform.localScale = origScale;
                }

                if (healingParticles != null) healingParticles.Play();

                // Snare trekt terug
                t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime * 1.5f;
                    colonoscopeSnare.transform.localPosition = Vector3.Lerp(targetPos, startPos, t);
                    yield return null;
                }
                colonoscopeSnare.SetActive(false);
            }
            else
            {
                if (stage1Cluster != null) stage1Cluster.SetActive(false);
                yield return new WaitForSeconds(1.5f);
            }
        }
    }
}
