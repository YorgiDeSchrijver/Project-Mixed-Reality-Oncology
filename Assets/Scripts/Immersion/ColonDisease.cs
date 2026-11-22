using UnityEngine;

/// <summary>
/// De ziekte verspreidt zich lokaal vanuit de poliepen, in plaats van als een filter over de hele darm.
/// Elke PolypStation wordt een "haard":
///  - rond de poliep is de wand ontstoken (rood, adertjes) en bij hogere stadia necrotisch (donker, mat),
///    via de shader Oncologie/ColonWallInfected,
///  - als de speler nadert, flakkert de haard op: de vlek breidt zich zichtbaar uit en de wand rond de
///    poliep zwelt op in het ritme van de hartslag (via ColonWallMotion),
///  - poliepen worden per stadium donkerder.
/// Severity = hoe dicht je bij hoe zieke plek bent; ColonHeartbeat gebruikt die waarde voor ritme en vibratie.
/// Zuinig voor de Quest: max. 4 haarden, geen textures, geen allocaties per frame.
/// </summary>
public class ColonDisease : MonoBehaviour
{
    const int MaxSpots = 4;

    [Header("Referenties (leeg = automatisch zoeken)")]
    public ColonPathRider rider;
    public ColonWallMotion wall;

    [Header("Haarden per stadium (index 0 = stadium 1)")]
    [Tooltip("Straal (m) van de zieke plek rond de poliep")]
    public float[] radiusByStage = { 0.35f, 0.6f, 0.95f, 1.4f };
    [Tooltip("Ernst 0..1: stadium 1 is lichte ontsteking, stadium 4 heeft een necrotische kern")]
    public float[] severityByStage = { 0.15f, 0.4f, 0.7f, 1f };

    [Header("Opflakkeren bij nadering")]
    [Tooltip("Vanaf deze afstand (m langs het pad) begint de haard te reageren")]
    public float reactDistance = 7f;
    [Tooltip("Op deze afstand (m) is de reactie maximaal")]
    public float fullDistance = 2f;
    [Tooltip("Hoeveel groter de vlek wordt als je dichtbij bent (0.4 = 40%)")]
    public float spreadOnApproach = 0.45f;
    [Tooltip("Seconden die het uitbreiden duurt. Kort = een duidelijk 'moment'.")]
    public float spreadSeconds = 2.5f;

    [Header("Zwelling van de wand rond de poliep")]
    [Tooltip("Hoeveel (m) de wand naar binnen zwelt bij een hartslag (stadium 4, dichtbij)")]
    public float throbDepth = 0.06f;
    [Tooltip("Blijvende zwelling (m) rond een stadium 4-poliep")]
    public float swellDepth = 0.05f;

    [Header("Poliepen")]
    [Tooltip("Kleur van een stadium 4-poliep (stadium 1 houdt zijn eigen kleur)")]
    public Color polypDarkest = new Color(0.32f, 0.12f, 0.16f);

    [Header("Test")]
    [Tooltip("Alle haarden volledig laten opflakkeren, ongeacht waar de speler is")]
    public bool previewFlare = false;

    static readonly int PosId = Shader.PropertyToID("_ColonInfectPos");
    static readonly int DataId = Shader.PropertyToID("_ColonInfectData");
    static readonly int BeatId = Shader.PropertyToID("_ColonHeartbeat");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

    PolypStation[] spots = new PolypStation[0];
    readonly Vector4[] posData = new Vector4[MaxSpots];
    readonly Vector4[] infoData = new Vector4[MaxSpots];
    readonly float[] flare = new float[MaxSpots];
    Material[] polypMats = new Material[0];
    float pulse;
    float severity;

    /// <summary>0 = niets ziek in de buurt, 1 = vlak bij een stadium 4-poliep.</summary>
    public float Severity => severity;
    /// <summary>Huidige sterkte van de hartslag-puls (0..1).</summary>
    public float PulseValue => pulse;

    /// <summary>Roep aan op elke hartslag. strength 1 = normale slag.</summary>
    public void Pulse(float strength) { pulse = Mathf.Max(pulse, Mathf.Clamp01(strength)); }

    void Start()
    {
        if (rider == null) rider = FindAnyObjectByType<ColonPathRider>();
        if (wall == null) wall = FindAnyObjectByType<ColonWallMotion>();

        var found = FindObjectsByType<PolypStation>(FindObjectsInactive.Include);
        System.Array.Sort(found, (a, b) => a.stage.CompareTo(b.stage));
        spots = new PolypStation[Mathf.Min(MaxSpots, found.Length)];
        System.Array.Copy(found, spots, spots.Length);
        if (found.Length > MaxSpots)
            Debug.LogWarning("ColonDisease: meer dan " + MaxSpots + " poliepen gevonden, alleen de eerste " + MaxSpots + " krijgen een haard.");

        TintPolyps();
        Apply();
    }

    void TintPolyps()
    {
        var list = new System.Collections.Generic.List<Material>();
        foreach (var st in spots)
        {
            Transform root = st.modelRoot != null ? st.modelRoot : st.transform;
            float k = (st.stage - 1) / 3f;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].HasProperty(BaseColorId)) continue;
                    var m = new Material(mats[i]) { name = mats[i].name + "_Stage" + st.stage };
                    m.SetColor(BaseColorId, Color.Lerp(mats[i].GetColor(BaseColorId), polypDarkest, k));
                    if (m.HasProperty(SmoothnessId)) m.SetFloat(SmoothnessId, Mathf.Lerp(0.7f, 0.25f, k));
                    mats[i] = m;
                    list.Add(m);
                }
                r.sharedMaterials = mats;
            }
        }
        polypMats = list.ToArray();
    }

    void Update()
    {
        pulse = Mathf.MoveTowards(pulse, 0f, Time.deltaTime * 3.5f);
        Apply();
    }

    void Apply()
    {
        float dt = Time.deltaTime;
        float playerS = rider != null && rider.Ready ? rider.Distance : float.NegativeInfinity;
        float p = pulse * pulse;   // korte, scherpe piek
        float maxSev = 0f;

        for (int i = 0; i < MaxSpots; i++)
        {
            if (i >= spots.Length || spots[i] == null)
            {
                posData[i] = Vector4.zero; infoData[i] = Vector4.zero;
                if (wall != null) wall.SetHotspot(i, Vector3.zero, 0f, 0f);
                continue;
            }

            var st = spots[i];
            int si = Mathf.Clamp(st.stage - 1, 0, 3);
            float sev = severityByStage[Mathf.Min(si, severityByStage.Length - 1)];
            float radius = radiusByStage[Mathf.Min(si, radiusByStage.Length - 1)];

            float near = 0f;
            if (previewFlare) near = 1f;
            else if (rider != null && rider.Ready && st.StationDistance > 0f)
            {
                float delta = Mathf.Abs(playerS - st.StationDistance);
                near = 1f - Mathf.InverseLerp(fullDistance, reactDistance, delta);
            }
            // uitbreiden gaat in een paar seconden, terugtrekken veel trager: wat je zag blijft hangen
            float rate = near > flare[i] ? 1f / Mathf.Max(0.1f, spreadSeconds) : 0.08f;
            flare[i] = Mathf.MoveTowards(flare[i], near, dt * rate);
            float f = Mathf.SmoothStep(0f, 1f, flare[i]);

            float r = radius * (1f + spreadOnApproach * f);
            Vector3 pos = st.transform.position;
            posData[i] = new Vector4(pos.x, pos.y, pos.z, r);
            infoData[i] = new Vector4(sev, f, 0f, 0f);

            if (wall != null)
            {
                float depth = sev * (swellDepth * (0.5f + 0.5f * f) + throbDepth * p * f);
                wall.SetHotspot(i, pos, r * 1.1f, depth);
            }

            maxSev = Mathf.Max(maxSev, sev * near);
        }

        severity = Mathf.MoveTowards(severity, maxSev, dt * 0.5f);

        Shader.SetGlobalVectorArray(PosId, posData);
        Shader.SetGlobalVectorArray(DataId, infoData);
        Shader.SetGlobalFloat(BeatId, p);
    }

    void OnDisable()
    {
        // haarden uitzetten zodat de wand in de editor weer gezond is
        for (int i = 0; i < MaxSpots; i++) { posData[i] = Vector4.zero; infoData[i] = Vector4.zero; }
        Shader.SetGlobalVectorArray(PosId, posData);
        Shader.SetGlobalVectorArray(DataId, infoData);
        Shader.SetGlobalFloat(BeatId, 0f);
    }

    void OnDestroy()
    {
        foreach (var m in polypMats) if (m != null) Destroy(m);
    }
}
