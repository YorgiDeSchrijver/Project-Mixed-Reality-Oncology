using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Oncologie.Dialogue
{
    /// <summary>
    /// Verzorgt de weergave van ondertitels en dialoog in zowel Desktop als VR mode.
    /// Ondersteunt sprekernamen met aangepaste kleuren, fade-in/out en achtergrondcontrast.
    /// </summary>
    public class SubtitleUI : MonoBehaviour
    {
        public static SubtitleUI Instance { get; private set; }

        [Header("UI Elementen")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text speakerLabel;
        [SerializeField] private Text dialogueBodyText;
        [SerializeField] private Image backgroundPanel;

        [Header("Animatie")]
        [SerializeField] private float fadeDuration = 0.25f;

        private Coroutine _fadeCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        public void DisplayLine(DialogueLine line)
        {
            if (line == null) return;

            if (speakerLabel != null)
            {
                speakerLabel.text = line.speakerName;
                speakerLabel.color = line.speakerColor;
            }

            if (dialogueBodyText != null)
            {
                dialogueBodyText.text = line.subtitleText;
            }

            FadeToAlpha(1f);
        }

        public void ClearSubtitle()
        {
            FadeToAlpha(0f);
        }

        private void FadeToAlpha(float targetAlpha)
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha));
        }

        private IEnumerator FadeRoutine(float targetAlpha)
        {
            if (canvasGroup == null) yield break;

            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
        }
    }
}
