using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Laat één poliep op dezelfde plek groeien: Stage 1 -> 2 -> 3 -> 4.
/// Elke stage is een grotere versie van de vorige. Tijdens een overgang blijft wat al bestond staan en
/// komt alleen het NIEUWE deel langzaam tevoorschijn: elk punt van de nieuwe stage start op het
/// oppervlak van de vorige stage en schuift geleidelijk naar zijn eindpositie.
/// Alle stage-modellen moeten op dezelfde plek en met dezelfde schaal staan, en de FBX's moeten
/// Read/Write aan hebben. Er worden geen assets aangemaakt: de berekening gebeurt bij het starten.
/// </summary>
public class PolypGrowth : MonoBehaviour
{
    public enum StartTrigger { OnSceneStart, WhenPlayerIsNear }

    [Header("Stages (Stage 1 t/m 4, op dezelfde plek)")]
    public Transform[] stages = new Transform[4];

    [Header("Timing")]
    [Tooltip("Seconden dat een stage blijft staan voor het nieuwe deel van de volgende begint te groeien. Voor testen laag houden.")]
    public float secondsPerStage = 5f;
    [Tooltip("Seconden dat het nieuwe deel erover doet om volledig te verschijnen")]
    public float growSeconds = 10f;
    public StartTrigger startTrigger = StartTrigger.WhenPlayerIsNear;
    [Tooltip("Alleen bij WhenPlayerIsNear: afstand tot de poliep (m, hemelsbreed)")]
    public float triggerDistance = 6f;
    public Transform player;

    [Header("Events")]
    public UnityEvent<int> onStageReached;

    // Per overgang (index = nieuwe stage): beginpositie van elk punt = dichtstbijzijnd punt op de vorige stage
    Mesh[] originalMesh;
    Mesh[] growMesh;
    Vector3[][] startPos;
    Vector3[][] endPos;
    Vector3[][] work;
    bool[] ready;

    int current;            // index van de zichtbare (afgeronde) stage
    float timer;
    bool running;
    bool growing;
    int growingIndex;

    public int CurrentStage => current + 1;

    void Awake()
    {
        int n = stages.Length;
        originalMesh = new Mesh[n]; growMesh = new Mesh[n];
        startPos = new Vector3[n][]; endPos = new Vector3[n][]; work = new Vector3[n][];
        ready = new bool[n];
        for (int i = 0; i < n; i++)
        {
            var mf = Filter(i);
            if (mf != null) originalMesh[i] = mf.sharedMesh;
            if (stages[i] != null) stages[i].gameObject.SetActive(i == 0);
        }
    }

    void Start()
    {
        if (player == null && Camera.main != null) player = Camera.main.transform;
        running = startTrigger == StartTrigger.OnSceneStart;
        StartCoroutine(PrepareAll());
        onStageReached?.Invoke(1);
    }

    MeshFilter Filter(int i) => stages[i] != null ? stages[i].GetComponentInChildren<MeshFilter>(true) : null;

    void Update()
    {
        if (!running)
        {
            if (player != null && (player.position - transform.position).sqrMagnitude <= triggerDistance * triggerDistance)
                running = true;
            return;
        }

        int next = current + 1;
        if (next >= stages.Length) return;

        timer += Time.deltaTime;
        if (!growing)
        {
            if (timer < secondsPerStage || !ready[next]) return;
            BeginGrow(next);
        }

        float t = Mathf.Clamp01((timer - secondsPerStage) / Mathf.Max(0.01f, growSeconds));
        ApplyGrow(next, Mathf.SmoothStep(0f, 1f, t));

        if (t >= 1f) FinishGrow(next);
    }

    // ---------- overgang ----------
    void BeginGrow(int next)
    {
        var mf = Filter(next);
        mf.sharedMesh = growMesh[next];
        stages[next].gameObject.SetActive(true);
        stages[current].gameObject.SetActive(false);   // de nieuwe stage start precies op de vorige
        growing = true; growingIndex = next;
        ApplyGrow(next, 0f);
    }

    void ApplyGrow(int next, float k)
    {
        Vector3[] a = startPos[next], b = endPos[next], w = work[next];
        for (int i = 0; i < w.Length; i++) w[i] = Vector3.LerpUnclamped(a[i], b[i], k);
        growMesh[next].vertices = w;
    }

    void FinishGrow(int next)
    {
        Filter(next).sharedMesh = originalMesh[next];   // terug naar het originele model: geen extra kosten meer
        Destroy(growMesh[next]);
        growMesh[next] = null; startPos[next] = null; endPos[next] = null; work[next] = null;
        growing = false;
        current = next;
        timer = 0f;
        onStageReached?.Invoke(current + 1);
    }

    // ---------- voorbereiding (verspreid over meerdere frames) ----------
    IEnumerator PrepareAll()
    {
        for (int next = 1; next < stages.Length; next++)
        {
            var from = originalMesh[next - 1];
            var to = originalMesh[next];
            if (from == null || to == null) continue;
            if (!from.isReadable || !to.isReadable)
            {
                Debug.LogError("PolypGrowth: de FBX van " + stages[next].name + " of de vorige stage heeft Read/Write uit (Model Import Settings).");
                continue;
            }

            Vector3[] fromV = from.vertices;
            Vector3[] toV = to.vertices;
            var grid = new PointGrid(fromV);
            var start = new Vector3[toV.Length];
            for (int i = 0; i < toV.Length; i++)
            {
                start[i] = fromV[grid.Nearest(toV[i])];
                if ((i & 2047) == 2047) yield return null;
            }

            var m = new Mesh { name = to.name + "_Grow" };
            m.indexFormat = to.indexFormat;
            m.vertices = start;
            m.normals = to.normals;
            m.uv = to.uv;
            m.tangents = to.tangents;
            m.subMeshCount = to.subMeshCount;
            for (int s = 0; s < to.subMeshCount; s++) m.SetTriangles(to.GetTriangles(s), s);
            m.bounds = to.bounds;   // eindgrootte, zodat de poliep niet wegvalt door culling
            m.MarkDynamic();

            startPos[next] = start; endPos[next] = toV; work[next] = new Vector3[toV.Length];
            growMesh[next] = m;
            ready[next] = true;
            yield return null;
        }
    }

    void OnDestroy()
    {
        if (growing && growingIndex < stages.Length && Filter(growingIndex) != null) Filter(growingIndex).sharedMesh = originalMesh[growingIndex];
        for (int i = 0; i < growMesh.Length; i++) if (growMesh[i] != null) Destroy(growMesh[i]);
    }

    /// <summary>Eenvoudig 3D-raster voor "dichtstbijzijnd punt" zoeken (eenmalig, bij het starten).</summary>
    sealed class PointGrid
    {
        readonly Vector3[] pts;
        readonly Vector3 min, max;
        readonly float cell;
        readonly int nx, ny, nz;
        readonly int[] cellStart, items;

        public PointGrid(Vector3[] p)
        {
            pts = p;
            min = max = p[0];
            for (int i = 1; i < p.Length; i++) { min = Vector3.Min(min, p[i]); max = Vector3.Max(max, p[i]); }
            Vector3 size = max - min;
            cell = Mathf.Max(size.x, size.y, size.z) / 24f + 1e-5f;
            nx = Mathf.Max(1, (int)(size.x / cell) + 1);
            ny = Mathf.Max(1, (int)(size.y / cell) + 1);
            nz = Mathf.Max(1, (int)(size.z / cell) + 1);

            cellStart = new int[nx * ny * nz + 1];
            var idx = new int[p.Length];
            for (int i = 0; i < p.Length; i++) { idx[i] = CellOf(p[i]); cellStart[idx[i] + 1]++; }
            for (int c = 0; c < nx * ny * nz; c++) cellStart[c + 1] += cellStart[c];
            items = new int[p.Length];
            var fill = new int[nx * ny * nz];
            for (int i = 0; i < p.Length; i++) items[cellStart[idx[i]] + fill[idx[i]]++] = i;
        }

        int CellOf(Vector3 q)
        {
            int x = Mathf.Clamp((int)((q.x - min.x) / cell), 0, nx - 1);
            int y = Mathf.Clamp((int)((q.y - min.y) / cell), 0, ny - 1);
            int z = Mathf.Clamp((int)((q.z - min.z) / cell), 0, nz - 1);
            return (z * ny + y) * nx + x;
        }

        public int Nearest(Vector3 q)
        {
            Vector3 inside = Vector3.Min(Vector3.Max(q, min), max);
            float pad = (q - inside).magnitude;
            int cx = Mathf.Clamp((int)((q.x - min.x) / cell), 0, nx - 1);
            int cy = Mathf.Clamp((int)((q.y - min.y) / cell), 0, ny - 1);
            int cz = Mathf.Clamp((int)((q.z - min.z) / cell), 0, nz - 1);

            int best = -1; float bestSqr = float.MaxValue;
            int maxR = Mathf.Max(nx, Mathf.Max(ny, nz));
            for (int r = 0; r <= maxR; r++)
            {
                for (int z = cz - r; z <= cz + r; z++)
                {
                    if (z < 0 || z >= nz) continue;
                    for (int y = cy - r; y <= cy + r; y++)
                    {
                        if (y < 0 || y >= ny) continue;
                        bool edgeZY = Mathf.Abs(z - cz) == r || Mathf.Abs(y - cy) == r;
                        int step = edgeZY || r == 0 ? 1 : 2 * r;   // binnenin de schil alleen de twee uiterste x-cellen
                        for (int x = cx - r; x <= cx + r; x += step)
                        {
                            if (x < 0 || x >= nx) continue;
                            int c = (z * ny + y) * nx + x;
                            for (int k = cellStart[c]; k < cellStart[c + 1]; k++)
                            {
                                float d = (pts[items[k]] - q).sqrMagnitude;
                                if (d < bestSqr) { bestSqr = d; best = items[k]; }
                            }
                        }
                    }
                }
                float safe = Mathf.Max(0f, r * cell - pad);
                if (best >= 0 && bestSqr <= safe * safe) break;
            }
            return best;
        }
    }
}
