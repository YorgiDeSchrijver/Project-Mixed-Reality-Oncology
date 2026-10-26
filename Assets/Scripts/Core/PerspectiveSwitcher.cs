using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PerspectiveSwitcher : MonoBehaviour
{
    [Header("Speler & Stoel Posities")]
    [SerializeField] private Transform xrOriginTransform; // Je VR Speler / Camera Rig
    [SerializeField] private Transform chairPlayerAnchor; // Positie Zoon/Dochter
    [SerializeField] private Transform chairDadAnchor;    // Positie Papa (Luc)

    [Header("Witte Fade Overlay (Canvas Image voor de camera)")]
    [SerializeField] private Image whiteFadeImage;
    [SerializeField] private float fadeDuration = 1.5f;

    public void SwitchToDad()
    {
        StartCoroutine(SwitchRoutine(chairDadAnchor));
    }

    public void SwitchToPlayer()
    {
        StartCoroutine(SwitchRoutine(chairPlayerAnchor));
    }

    private IEnumerator SwitchRoutine(Transform targetAnchor)
    {
        // 1. Fade naar wit
        yield return StartCoroutine(FadeAlpha(0f, 1f));

        // 2. Verplaats en roteer de speler naar de nieuwe stoel
        if (xrOriginTransform != null && targetAnchor != null)
        {
            xrOriginTransform.SetPositionAndRotation(targetAnchor.position, targetAnchor.rotation);
        }

        yield return new WaitForSeconds(0.3f);

        // 3. Fade terug vanuit wit naar de scène
        yield return StartCoroutine(FadeAlpha(1f, 0f));
    }

    private IEnumerator FadeAlpha(float startAlpha, float endAlpha)
    {
        if (whiteFadeImage == null) yield break;

        float elapsed = 0f;
        Color c = whiteFadeImage.color;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(startAlpha, endAlpha, elapsed / fadeDuration);
            whiteFadeImage.color = c;
            yield return null;
        }

        c.a = endAlpha;
        whiteFadeImage.color = c;
    }
}