using System.Collections;
using UnityEngine;

/// <summary>
/// Laat de darmwand "leven": een trage peristaltische golf langs de darm plus kleine, onregelmatige
/// bewegingen van de wand. De wand beweegt alleen radiaal (naar of van de middellijn), een paar centimeter.
/// Zuinig gebouwd voor de Quest: alleen de segmenten dichtbij de speler worden bijgewerkt, met een
/// instelbaar aantal updates per seconde. Er worden geen textures of extra geometrie toegevoegd.
/// Zet dit op Colon_Tunnel. De FBX moet Read/Write aan hebben.
/// </summary>
public class ColonWallMotion : MonoBehaviour
{
    [Header("Referenties")]
    public ColonPathRider rider;
    [Tooltip("Meestal de Main Camera. Leeg = Camera.main")]
    public Transform head;

    [Header("Peristaltische golf")]
    [Tooltip("Hoeveel (m) de wand heen en weer gaat door de grote golf. 0.02-0.05 is subtiel.")]
    public float waveAmplitude = 0.035f;
    [Tooltip("Afstand (m) tussen twee golftoppen")]
    public float waveLength = 7f;
    [Tooltip("Snelheid (m/s) waarmee de golf door de darm reist")]
    public float waveSpeed = 0.25f;

    [Header("Kleine bewegingen")]
    [Tooltip("Amplitude (m) van de onregelmatige beweging")]
    public float twitchAmplitude = 0.012f;
    [Tooltip("Hoe snel de kleine bewegingen gaan (rad/s)")]
    public float twitchSpeed = 0.9f;
    [Tooltip("Hoe fijn de kleine bewegingen zijn (hoger = kleinere plekjes)")]
    public float twitchDetail = 2.5f;

    [Header("Reactie op de speler")]
    [Tooltip("De wand reageert op hoe de speler beweegt: rustiger bij stilstaan, levendiger onderweg, met een golf die de speler meeneemt")]
    public bool reactToPlayer = true;
    [Tooltip("Snelheid (m/s) waarbij het effect volledig is")]
    public float referenceSpeed = 1f;
    [Tooltip("Vermenigvuldiger van alle bewegingen als de speler stilstaat (1 = geen verschil)")]
    [Range(0f, 1f)] public float idleCalm = 0.4f;
    [Tooltip("Vermenigvuldiger van alle bewegingen bij volle snelheid")]
    public float movingBoost = 1.6f;
    [Tooltip("Amplitude (m) van de golf rond de speler bij volle snelheid")]
    public float pushAmplitude = 0.03f;
    [Tooltip("Afstand (m) tussen de toppen van die golf")]
    public float pushLength = 2.5f;
    [Tooltip("Tot hoever (m) vanaf de speler de golf merkbaar is")]
    public float pushReach = 5f;
    [Tooltip("Hoeveel sneller de toppen meebewegen dan de speler. Hoger = de wand lijkt je harder vooruit te duwen.")]
    public float pushTravel = 1.5f;
    [Tooltip("Hoe snel het effect aan- en uitgaat (seconden)")]
    public float reactionTime = 0.6f;

    [Header("Performance")]
    [Tooltip("Alleen segmenten binnen deze afstand (m) van de speler bewegen")]
    public float activeRange = 10f;
    [Range(5, 60)] public int updatesPerSecond = 30;

    class Segment
    {
        public MeshFilter filter;
        public Mesh mesh;
        public Vector3[] baseV, work, dir;   // dir: radiaal naar buiten, in lokale ruimte
        public float[] s, pa, pb;            // afstand langs pad en twee fases per punt
        public bool ready;
    }

    Segment[] segments;
    WallAttached[] attached = new WallAttached[0];
    float nextUpdate;
    float lastUpdateTime;
    Vector3 lastHeadPos;
    float lastPathDistance;
    float activity;          // 0 = stilstaan, 1 = volle snelheid (vloeiend)
    float pushDistance;      // opgetelde afgelegde afstand langs het pad, bepaalt de fase van de golf
    bool haveLast;

    void Start()
    {
        if (rider == null) rider = FindFirstObjectByType<ColonPathRider>();
        if (head == null && Camera.main != null) head = Camera.main.transform;

        var filters = GetComponentsInChildren<MeshFilter>(true);
        segments = new Segment[filters.Length];
        for (int i = 0; i < filters.Length; i++)
        {
            var src = filters[i].sharedMesh;
            if (src == null) continue;
            if (!src.isReadable)
            {
                Debug.LogError("ColonWallMotion: zet Read/Write aan op de FBX van " + filters[i].name + " (Model Import Settings).");
                continue;
            }
            var seg = new Segment { filter = filters[i], mesh = Instantiate(src) };
            seg.mesh.name = src.name + "_Alive";
            seg.mesh.MarkDynamic();
            Bounds b = src.bounds; b.Expand(waveAmplitude * 3f + twitchAmplitude * 3f);
            seg.mesh.bounds = b;
            filters[i].sharedMesh = seg.mesh;
            segments[i] = seg;
        }
        StartCoroutine(PrepareAll());
    }

    IEnumerator PrepareAll()
    {
        while (rider == null || !rider.Ready) yield return null;

        int count = rider.SampleCount;
        var pts = new Vector3[count]; var dist = new float[count];
        for (int i = 0; i < count; i++) { pts[i] = rider.SamplePoint(i); dist[i] = rider.SampleDistance(i); }

        // vaste, willekeurige richtingen voor de fases (zelfde bij elke start)
        var rng = new System.Random(1234);
        Vector3 v1 = new Vector3((float)rng.NextDouble() - .5f, (float)rng.NextDouble() - .5f, (float)rng.NextDouble() - .5f).normalized;
        Vector3 v2 = new Vector3((float)rng.NextDouble() - .5f, (float)rng.NextDouble() - .5f, (float)rng.NextDouble() - .5f).normalized;

        foreach (var seg in segments)
        {
            if (seg == null) continue;
            Transform tr = seg.filter.transform;
            Vector3[] v = seg.mesh.vertices;
            int n = v.Length;
            seg.baseV = v; seg.work = new Vector3[n]; seg.dir = new Vector3[n];
            seg.s = new float[n]; seg.pa = new float[n]; seg.pb = new float[n];

            for (int i = 0; i < n; i++)
            {
                Vector3 w = tr.TransformPoint(v[i]);
                int best = 0; float bestSqr = float.MaxValue;
                for (int j = 0; j < count; j++)
                {
                    float d = (pts[j] - w).sqrMagnitude;
                    if (d < bestSqr) { bestSqr = d; best = j; }
                }
                Vector3 tan = (pts[Mathf.Min(best + 1, count - 1)] - pts[Mathf.Max(best - 1, 0)]).normalized;
                Vector3 radial = w - pts[best];
                radial -= tan * Vector3.Dot(radial, tan);
                radial = radial.sqrMagnitude > 1e-6f ? radial.normalized : Vector3.up;

                seg.dir[i] = tr.InverseTransformDirection(radial);
                seg.s[i] = dist[best];
                seg.pa[i] = Vector3.Dot(w, v1) * twitchDetail;
                seg.pb[i] = Vector3.Dot(w, v2) * twitchDetail;

                if ((i % 600) == 599) yield return null;   // verspreid over frames
            }
            seg.ready = true;
        }

        yield return StartCoroutine(AttachObjects());
    }

    // Koppel elk WallAttached-object aan het dichtstbijzijnde punt van de wand
    IEnumerator AttachObjects()
    {
        var found = FindObjectsByType<WallAttached>(FindObjectsSortMode.None);
        foreach (var a in found)
        {
            a.basePosition = a.transform.position;
            a.segment = -1; a.vertex = -1;
            float best = float.MaxValue;
            for (int si = 0; si < segments.Length; si++)
            {
                var seg = segments[si];
                if (seg == null || !seg.ready) continue;
                Transform tr = seg.filter.transform;
                Vector3 local = tr.InverseTransformPoint(a.basePosition);
                for (int i = 0; i < seg.baseV.Length; i++)
                {
                    float d = (seg.baseV[i] - local).sqrMagnitude;
                    if (d < best) { best = d; a.segment = si; a.vertex = i; }
                }
            }
            yield return null;
        }
        attached = found;
    }

    void Update()
    {
        if (segments == null || Time.time < nextUpdate) return;
        nextUpdate = Time.time + 1f / updatesPerSecond;

        Vector3 hp = head != null ? head.position : Vector3.zero;
        float t = Time.time;
        float dt = Mathf.Max(0.001f, t - lastUpdateTime);
        lastUpdateTime = t;

        // --- hoe beweegt de speler? ---
        float playerS = rider != null && rider.Ready ? rider.Distance : 0f;
        float mult = 1f, push = 0f;
        if (reactToPlayer && head != null)
        {
            if (haveLast)
            {
                float speed = (hp - lastHeadPos).magnitude / dt;
                float target = Mathf.Clamp01(speed / Mathf.Max(0.01f, referenceSpeed));
                activity = Mathf.Lerp(activity, target, 1f - Mathf.Exp(-dt / Mathf.Max(0.05f, reactionTime)));
                pushDistance += (playerS - lastPathDistance) * pushTravel;
            }
            lastHeadPos = hp; lastPathDistance = playerS; haveLast = true;
            mult = Mathf.Lerp(idleCalm, movingBoost, activity);
            push = pushAmplitude * activity;
        }
        float kp = 2f * Mathf.PI / Mathf.Max(0.1f, pushLength);
        float sigma = Mathf.Max(0.5f, pushReach * 0.5f);
        float inv2s2 = 1f / (2f * sigma * sigma);
        float cutoff = 3f * sigma;
        float k1 = 2f * Mathf.PI / Mathf.Max(0.1f, waveLength);
        float w1 = k1 * waveSpeed;
        float k2 = k1 / 0.37f;     // kortere golfjes (haustra)
        float tw = twitchSpeed;

        for (int si = 0; si < segments.Length; si++)
        {
            var seg = segments[si];
            if (seg == null || !seg.ready) continue;
            if (head != null)
            {
                Bounds wb = seg.filter.GetComponent<Renderer>().bounds;
                if ((wb.ClosestPoint(hp) - hp).sqrMagnitude > activeRange * activeRange) continue;
            }

            Vector3[] b = seg.baseV, w = seg.work, d = seg.dir;
            for (int i = 0; i < b.Length; i++)
            {
                float s = seg.s[i];
                float a = waveAmplitude * (Mathf.Sin(k1 * s - w1 * t) + 0.5f * Mathf.Sin(k2 * s + 1.7f * t * 0.25f))
                        + twitchAmplitude * Mathf.Sin(seg.pa[i] + tw * t) * Mathf.Sin(seg.pb[i] - 0.6f * tw * t);
                a *= mult;
                if (push > 0.0001f)
                {
                    float ds = s - playerS;
                    if (ds > -cutoff && ds < cutoff)
                        a += push * Mathf.Exp(-ds * ds * inv2s2) * Mathf.Sin(kp * (s - pushDistance));
                }
                w[i] = b[i] + d[i] * a;
            }
            seg.mesh.vertices = w;
            MoveAttached(si, seg);
        }
    }

    // Objecten die op de wand zitten bewegen precies mee met het wandpunt waar ze aan vastzitten
    void MoveAttached(int si, Segment seg)
    {
        Transform tr = seg.filter.transform;
        for (int k = 0; k < attached.Length; k++)
        {
            var a = attached[k];
            if (a == null || a.segment != si) continue;
            a.transform.position = a.basePosition + tr.TransformVector(seg.work[a.vertex] - seg.baseV[a.vertex]);
        }
    }

    void OnDestroy()
    {
        if (segments == null) return;
        foreach (var seg in segments) if (seg != null && seg.mesh != null) Destroy(seg.mesh);
    }
}
