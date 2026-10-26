using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class PolypController : MonoBehaviour
{
    [Header("Groeifases (Schaal van Placeholder Sphere)")]
    [SerializeField] private Vector3[] stageScales = new Vector3[]
    {
        new Vector3(0.05f, 0.05f, 0.05f), // Stage 1
        new Vector3(0.12f, 0.12f, 0.12f), // Stage 2
        new Vector3(0.22f, 0.22f, 0.22f), // Stage 3
        new Vector3(0.35f, 0.35f, 0.35f)  // Stage 4
    };
    [SerializeField] private float growDuration = 4.0f;

    [Header("Envelop Zaklamp Detectie")]
    [SerializeField] private Transform envelopeLightTransform;
    [SerializeField] private float detectionAngle = 25f;
    [SerializeField] private GameObject bleedMaskObject; // Klein rood vlakje op de poliep
    [SerializeField] private bool canDetectLight = false;

    public UnityEvent onPolypIlluminated;

    private bool _illuminated = false;

    private void Start()
    {
        if (stageScales.Length > 0)
            transform.localScale = stageScales[0];

        if (bleedMaskObject != null)
            bleedMaskObject.SetActive(false);
    }

    private void Update()
    {
        if (!canDetectLight || _illuminated || envelopeLightTransform == null) return;

        Vector3 dirToPolyp = (transform.position - envelopeLightTransform.position).normalized;
        float angle = Vector3.Angle(envelopeLightTransform.forward, dirToPolyp);

        if (angle <= detectionAngle)
        {
            _illuminated = true;
            if (bleedMaskObject != null) bleedMaskObject.SetActive(true);
            onPolypIlluminated?.Invoke();
        }
    }

    public void GrowToStage(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= stageScales.Length) return;
        StopAllCoroutines();
        StartCoroutine(ScaleRoutine(stageScales[stageIndex]));
    }

    public void EnableLightDetection(bool enable)
    {
        canDetectLight = enable;
    }

    private IEnumerator ScaleRoutine(Vector3 targetScale)
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < growDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / growDuration);
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        transform.localScale = targetScale;
    }
}