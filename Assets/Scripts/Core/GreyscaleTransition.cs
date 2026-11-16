using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GreyscaleTransition : MonoBehaviour
{
    [SerializeField] private Volume postProcessVolume;
    [SerializeField] private float transitionDuration = 4.0f;

    private ColorAdjustments _colorAdjustments;

    private void Awake()
    {
        if (postProcessVolume != null && postProcessVolume.profile != null)
        {
            postProcessVolume.profile.TryGet(out _colorAdjustments);
        }
    }

    public void FadeToGreyscale()
    {
        if (_colorAdjustments != null)
        {
            StartCoroutine(AnimateSaturation(-100f));
        }
    }

    public void FadeToColor()
    {
        if (_colorAdjustments != null)
        {
            StartCoroutine(AnimateSaturation(0f));
        }
    }

    private IEnumerator AnimateSaturation(float targetSaturation)
    {
        float startSaturation = _colorAdjustments.saturation.value;
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            _colorAdjustments.saturation.value = Mathf.Lerp(startSaturation, targetSaturation, t);
            yield return null;
        }

        _colorAdjustments.saturation.value = targetSaturation;
    }
}