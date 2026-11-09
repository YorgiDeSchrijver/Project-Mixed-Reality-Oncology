using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Oncologie.Dialogue;

namespace Oncologie.Core
{
    /// <summary>
    /// Regisseert alle cinematische cutscenes:
    /// 1. Krimp-cutscene (vanuit de keuken naar de buik van vader Luc naar de darm).
    /// 2. Uitzoom-cutscene (vanuit de darmtunnel terug naar de keuken).
    /// Zorgt voor vloeiende camera-swoops, FOV-warping, vignet/fade effecten en geluidsdemping.
    /// </summary>
    public class CinematicTransitionDirector : MonoBehaviour
    {
        public static CinematicTransitionDirector Instance { get; private set; }

        [Header("Camera & Speler Referenties")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Camera playerCamera;

        [Header("Target Punten")]
        [SerializeField] private Transform fatherAbdomenTarget;
        [SerializeField] private Transform colonEntryAnchor;
        [SerializeField] private Transform kitchenReturnAnchor;

        [Header("Visuele Fade Overlay")]
        [SerializeField] private CanvasGroup transitionCanvasGroup;
        [SerializeField] private Image transitionColorImage;

        [Header("Duur & Timing")]
        [SerializeField] private float approachDuration = 2.2f;
        [SerializeField] private float zoomFovDuration = 1.2f;
        [SerializeField] private float fadeDuration = 0.6f;

        private bool _isTransitioning = false;

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
            if (playerCamera == null) playerCamera = Camera.main;
            if (playerTransform == null && playerCamera != null)
            {
                var tp = playerCamera.GetComponentInParent<SampleSceneTestPlayer>();
                if (tp != null) playerTransform = tp.transform;
                else playerTransform = playerCamera.transform;
            }

            if (fatherAbdomenTarget == null)
            {
                var dad = GameObject.Find("Scavenger");
                if (dad != null)
                {
                    var abdomen = dad.transform.Find("Abdomen_FocusPoint");
                    if (abdomen == null)
                    {
                        var go = new GameObject("Abdomen_FocusPoint");
                        go.transform.SetParent(dad.transform, false);
                        go.transform.localPosition = new Vector3(0f, 1.05f, 0.1f);
                        abdomen = go.transform;
                    }
                    fatherAbdomenTarget = abdomen;
                }
            }

            EnsureTransitionUI();
        }

        private void EnsureTransitionUI()
        {
            if (transitionCanvasGroup == null)
            {
                var existingCanvas = GameObject.Find("Transition_Fade_Canvas");
                if (existingCanvas != null)
                {
                    transitionCanvasGroup = existingCanvas.GetComponent<CanvasGroup>();
                    transitionColorImage = existingCanvas.GetComponentInChildren<Image>();
                }
                else
                {
                    var canvasGO = new GameObject("Transition_Fade_Canvas");
                    var canvas = canvasGO.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 999;

                    transitionCanvasGroup = canvasGO.AddComponent<CanvasGroup>();
                    transitionCanvasGroup.alpha = 0f;
                    transitionCanvasGroup.blocksRaycasts = false;

                    var imgGO = new GameObject("Fade_Image");
                    imgGO.transform.SetParent(canvasGO.transform, false);
                    var rect = imgGO.AddComponent<RectTransform>();
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;

                    transitionColorImage = imgGO.AddComponent<Image>();
                    transitionColorImage.color = new Color(0.2f, 0.02f, 0.04f, 1f); // Diep biologisch rood
                }
            }
        }

        /// <summary>
        /// Start de volledige krimp-cutscene van keuken naar darm.
        /// </summary>
        public void PlayShrinkToColonCutscene(Action onColonArrived)
        {
            if (_isTransitioning) return;
            StartCoroutine(ShrinkToColonRoutine(onColonArrived));
        }

        private IEnumerator ShrinkToColonRoutine(Action onColonArrived)
        {
            _isTransitioning = true;
            EnsureReferences();

            // 1. Blokkeer tijdelijk spelerbesturing
            var testPlayer = playerTransform != null ? playerTransform.GetComponent<SampleSceneTestPlayer>() : null;
            if (testPlayer != null) testPlayer.disableMovement = true;
            var cc = playerTransform != null ? playerTransform.GetComponent<CharacterController>() : null;
            if (cc != null) cc.enabled = false;

            // Start audio demping & aanzwellende hartslag
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StartHeartbeat(false);
            }

            // 2. Camera beweegt soepel naar de buik van Luc (Swoop naar abdomen)
            if (fatherAbdomenTarget != null && playerCamera != null)
            {
                Vector3 startPos = playerTransform.position;
                Quaternion startRot = playerTransform.rotation;

                // Stop op 0.45m voor de buik van vader
                Vector3 targetPos = fatherAbdomenTarget.position - fatherAbdomenTarget.forward * 0.45f;
                targetPos.y = fatherAbdomenTarget.position.y;
                Quaternion targetRot = Quaternion.LookRotation((fatherAbdomenTarget.position - targetPos).normalized);

                float elapsed = 0f;
                float origFov = playerCamera.fieldOfView;

                while (elapsed < approachDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / approachDuration);

                    playerTransform.position = Vector3.Lerp(startPos, targetPos, t);
                    playerTransform.rotation = Quaternion.Slerp(startRot, targetRot, t);

                    // FOV verkleint lichtjes voor dramatische focus
                    playerCamera.fieldOfView = Mathf.Lerp(origFov, 45f, t);
                    yield return null;
                }

                // 3. Zoom-rush door het weefsel (FOV warp en rode vignet fade)
                elapsed = 0f;
                while (elapsed < zoomFovDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / zoomFovDuration;

                    playerCamera.fieldOfView = Mathf.Lerp(45f, 20f, t);
                    if (transitionCanvasGroup != null)
                    {
                        transitionCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t * t); // Kwadratische versnelling
                    }
                    yield return null;
                }

                // Scherm is nu volledig rood/donker
                playerCamera.fieldOfView = origFov;
            }
            else
            {
                // Fallback fade indien camera niet gevonden
                yield return FadeRoutine(0f, 1f, 1.0f);
            }

            // 4. Voer de overgang naar de darm uit (Callback)
            onColonArrived?.Invoke();

            yield return new WaitForSeconds(0.4f);

            // 5. Fade-in in de darmtunnel
            if (transitionCanvasGroup != null)
            {
                yield return FadeRoutine(1f, 0f, fadeDuration);
            }

            // Herstel spelerbesturing zodat de speler direct ZELF vrij door de darm kan lopen!
            if (testPlayer != null) testPlayer.disableMovement = false;
            if (cc != null) cc.enabled = true;

            _isTransitioning = false;
        }

        /// <summary>
        /// Start de uitzoom-cutscene van darm terug naar de keuken.
        /// </summary>
        public void PlayZoomOutToKitchenCutscene(Action onKitchenArrived)
        {
            if (_isTransitioning) return;
            StartCoroutine(ZoomOutToKitchenRoutine(onKitchenArrived));
        }

        private IEnumerator ZoomOutToKitchenRoutine(Action onKitchenArrived)
        {
            _isTransitioning = true;
            EnsureReferences();

            // 1. Fade out naar wit/licht (tunnel-opening)
            if (transitionColorImage != null)
            {
                transitionColorImage.color = new Color(0.95f, 0.95f, 1f, 1f); // Helder wit licht aan het einde van de tunnel
            }

            yield return FadeRoutine(0f, 1f, 1.2f);

            // 2. Teleporteer terug naar keuken
            onKitchenArrived?.Invoke();

            yield return new WaitForSeconds(0.5f);

            // 3. Herstel fade kleur naar donker voor toekomstige fades
            if (transitionColorImage != null)
            {
                transitionColorImage.color = new Color(0.15f, 0.02f, 0.03f, 1f);
            }

            // 4. Fade in terug in de keuken
            yield return FadeRoutine(1f, 0f, 1.2f);

            // Herstel spelerbesturing
            var testPlayer = playerTransform != null ? playerTransform.GetComponent<SampleSceneTestPlayer>() : null;
            if (testPlayer != null) testPlayer.disableMovement = false;
            var cc = playerTransform != null ? playerTransform.GetComponent<CharacterController>() : null;
            if (cc != null) cc.enabled = true;

            _isTransitioning = false;
        }

        private IEnumerator FadeRoutine(float from, float to, float duration)
        {
            if (transitionCanvasGroup == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transitionCanvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            transitionCanvasGroup.alpha = to;
        }
    }
}
