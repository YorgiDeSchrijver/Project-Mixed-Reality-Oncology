using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Eén station langs de darm met een poliep in één van de 4 stages.
/// Doet drie dingen:
///  1. toont het model alleen als de speler dichtbij is (zuinig op de Quest),
///  2. vuurt events af als je nadert, aankomt en eraan blijft kijken,
///  3. speelt (optioneel) de uitleg af: audio en/of ondertitel.
/// Wordt meestal automatisch aangemaakt door PolypStations.
/// </summary>
public class PolypStation : MonoBehaviour
{
    public enum PlayInfoOn { Never, Arrive, Inspected }

    [Header("Koppeling")]
    [Range(1, 4)] public int stage = 1;
    public ColonPathRider rider;
    [Tooltip("Punt op de middellijn van de darm bij dit station")]
    public Transform pathAnchor;
    [Tooltip("Het poliepmodel (wordt verborgen als de speler ver weg is)")]
    public Transform modelRoot;

    [Header("Afstanden (meter langs het pad)")]
    [Tooltip("Binnen deze afstand is het model zichtbaar")]
    public float showDistance = 20f;
    public float approachDistance = 8f;
    public float arriveDistance = 3f;
    [Tooltip("Events mogen opnieuw afgaan als de speler weggaat en terugkomt")]
    public bool repeatable = true;

    [Header("Kijken")]
    [Tooltip("De speler moet echt naar de poliep kijken voor de uitleg start")]
    public bool requireGaze = true;
    public float gazeAngle = 25f;
    public float gazeSeconds = 1.5f;

    [Header("Uitleg")]
    public PlayInfoOn playInfoOn = PlayInfoOn.Inspected;
    public AudioSource voice;
    public AudioClip infoClip;
    [TextArea(2, 5)] public string infoText;
    [Tooltip("Krijgt de infoText mee, bv. koppelen aan TMP_Text.SetText")]
    public UnityEvent<string> onInfoText;

    [Header("Events")]
    public UnityEvent onApproach;
    public UnityEvent onArrive;
    public UnityEvent onInspected;
    public UnityEvent onLeave;

    float stationDistance;
    bool distanceKnown;
    bool approached, arrived, inspected;
    float gazeTimer;
    Vector3 gazeLocal;

    public float StationDistance => stationDistance;

    void Start()
    {
        if (rider == null) rider = FindFirstObjectByType<ColonPathRider>();

        // Kijkpunt = midden van het model (nu berekenen, het model kan later verborgen zijn)
        gazeLocal = Vector3.zero;
        if (modelRoot != null)
        {
            var rs = modelRoot.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                gazeLocal = transform.InverseTransformPoint(b.center);
            }
        }
    }

    void Update()
    {
        if (rider == null || !rider.Ready) return;

        if (!distanceKnown)
        {
            Vector3 p = pathAnchor != null ? pathAnchor.position : transform.position;
            stationDistance = rider.ProjectDistance(p);
            distanceKnown = true;
        }

        float delta = Mathf.Abs(rider.Distance - stationDistance);

        if (modelRoot != null)
        {
            bool show = delta <= showDistance;
            if (modelRoot.gameObject.activeSelf != show) modelRoot.gameObject.SetActive(show);
        }

        if (!approached && delta <= approachDistance) { approached = true; onApproach?.Invoke(); }

        if (!arrived && delta <= arriveDistance)
        {
            arrived = true;
            onArrive?.Invoke();
            if (playInfoOn == PlayInfoOn.Arrive) PlayInfo();
        }

        if (arrived && !inspected)
        {
            if (!requireGaze) Inspect();
            else if (IsLookingAtPolyp())
            {
                gazeTimer += Time.deltaTime;
                if (gazeTimer >= gazeSeconds) Inspect();
            }
            else gazeTimer = Mathf.Max(0f, gazeTimer - Time.deltaTime);
        }

        if (approached && delta > approachDistance + 3f)
        {
            onLeave?.Invoke();
            if (repeatable) { approached = arrived = inspected = false; gazeTimer = 0f; }
            else approached = false; // geen herhaling van approach, wel opnieuw kunnen vertrekken
        }
    }

    bool IsLookingAtPolyp()
    {
        Transform h = rider.head != null ? rider.head.transform : (Camera.main != null ? Camera.main.transform : null);
        if (h == null) return false;
        Vector3 to = transform.TransformPoint(gazeLocal) - h.position;
        return Vector3.Angle(h.forward, to) <= gazeAngle;
    }

    void Inspect()
    {
        inspected = true;
        onInspected?.Invoke();
        if (playInfoOn == PlayInfoOn.Inspected) PlayInfo();
    }

    public void PlayInfo()
    {
        if (voice != null && infoClip != null) { voice.clip = infoClip; voice.Play(); }
        if (!string.IsNullOrEmpty(infoText)) onInfoText?.Invoke(infoText);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        if (pathAnchor != null)
        {
            Gizmos.DrawSphere(pathAnchor.position, 0.12f);
            Gizmos.DrawLine(pathAnchor.position, transform.position);
        }
        Gizmos.color = Color.magenta;
        Gizmos.DrawRay(transform.position, transform.up * 1f);
    }
}