using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Sluit de open uiteinden van de darm (Colon_Tunnel) naadloos af met een verlenging die verder loopt,
/// smaller wordt en in het donker verdwijnt.
/// De eerste ring van de verlenging ligt exact op de open rand van de tunnel (zelfde punten en normalen),
/// dus er is geen zichtbare overgang. De verlenging wordt een kind van Colon_Tunnel, zodat ColonWallMotion
/// ze mee laat bewegen met de wand.
/// Gebruik: open de scène en kies Oncologie > Darm > Bouw uiteinden. Opnieuw uitvoeren vervangt de oude.
/// </summary>
public static class ColonEndFunnelBuilder
{
    const int Seg = 40;          // punten rond
    const int Rings = 20;        // ringen in de lengte
    const float Length = 5.5f;   // m
    const float RimOverlap = 0.05f;   // m achter de tunnelrand
    const string MeshFolder = "Assets/Models/Colon";
    const string MaterialPath = "Assets/Models/Colon/Materials/M_ColonWall_EndFade.mat";

    [MenuItem("Oncologie/Darm/Bouw uiteinden")]
    public static void BuildForOpenScene()
    {
        var tunnel = GameObject.Find("Colon_Tunnel");
        var pathRoot = GameObject.Find("PlayerPath");
        if (tunnel == null || pathRoot == null) { Debug.LogError("ColonEndFunnelBuilder: Colon_Tunnel of PlayerPath niet gevonden."); return; }

        var pts = new List<Transform>();
        foreach (Transform t in pathRoot.transform) if (t.name.StartsWith("PathPoint")) pts.Add(t);
        pts.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        if (pts.Count < 2) { Debug.LogError("ColonEndFunnelBuilder: te weinig PathPoints."); return; }

        // oude versies opruimen (ook de vroegere losse doppen)
        foreach (var n in new[] { "Colon_EndCap_Start", "Colon_EndCap_End", "Colon_EndFunnel_Start", "Colon_EndFunnel_End" })
        {
            var old = GameObject.Find(n);
            if (old != null) Object.DestroyImmediate(old);
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        Build(tunnel.transform, pts[0].position, pts[1].position, "Start", mat);
        Build(tunnel.transform, pts[pts.Count - 1].position, pts[pts.Count - 2].position, "End", mat);

        EditorSceneManager.MarkSceneDirty(tunnel.scene);
        AssetDatabase.SaveAssets();
    }

    static void Build(Transform tunnel, Vector3 end, Vector3 inner, string label, Material mat)
    {
        Vector3 axis = (end - inner).normalized;                       // naar buiten
        Vector3 side = Vector3.Cross(axis, Vector3.up);
        if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(axis, Vector3.right);
        side.Normalize();
        Vector3 up = Vector3.Cross(side, axis).normalized;

        // ---- 1. open rand van de tunnel bij dit uiteinde zoeken ----
        var rimPos = new List<Vector3>();
        var rimNrm = new List<Vector3>();
        foreach (var mf in tunnel.GetComponentsInChildren<MeshFilter>())
        {
            var mesh = mf.sharedMesh;
            if (mesh == null || !mesh.isReadable) continue;
            var tr = mf.transform;
            var v = mesh.vertices; var nr = mesh.normals; var tri = mesh.triangles;

            // punten samenvoegen op positie (UV-naden hebben dubbele punten)
            var key = new Dictionary<Vector3Int, int>();
            var weld = new int[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                var k = new Vector3Int(Mathf.RoundToInt(v[i].x * 1000), Mathf.RoundToInt(v[i].y * 1000), Mathf.RoundToInt(v[i].z * 1000));
                if (!key.TryGetValue(k, out int id)) { id = i; key[k] = i; }
                weld[i] = id;
            }
            var edges = new Dictionary<long, int>();
            for (int i = 0; i < tri.Length; i += 3)
                for (int e = 0; e < 3; e++)
                {
                    int a = weld[tri[i + e]], b = weld[tri[i + (e + 1) % 3]];
                    long id = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    edges.TryGetValue(id, out int c); edges[id] = c + 1;
                }
            var seen = new HashSet<int>();
            foreach (var kv in edges)
            {
                if (kv.Value != 1) continue;   // randen met maar één driehoek = open rand
                int a = (int)(kv.Key >> 32), b = (int)(kv.Key & 0xffffffff);
                foreach (int idx in new[] { a, b })
                {
                    if (!seen.Add(idx)) continue;
                    Vector3 w = tr.TransformPoint(v[idx]);
                    if ((w - end).magnitude > 3.2f) continue;          // alleen de rand bij dit uiteinde
                    if (Vector3.Dot(w - end, axis) < -1.6f) continue;  // geen naden tussen segmenten verder binnen
                    rimPos.Add(w);
                    rimNrm.Add(nr.Length > idx ? tr.TransformDirection(nr[idx]).normalized : -((w - end) - axis * Vector3.Dot(w - end, axis)).normalized);
                }
            }
        }
        if (rimPos.Count < 8) { Debug.LogError("ColonEndFunnelBuilder: geen open rand gevonden bij " + label); return; }

        // ---- 2. rand herbemonsteren op vaste hoeken (geen lus-volgorde nodig) ----
        Vector3 centre = Vector3.zero; foreach (var p in rimPos) centre += p; centre /= rimPos.Count;
        float Angle(Vector3 p) { Vector3 d = p - centre; return Mathf.Atan2(Vector3.Dot(d, up), Vector3.Dot(d, side)); }
        var order = new List<int>(); for (int i = 0; i < rimPos.Count; i++) order.Add(i);
        order.Sort((a, b) => Angle(rimPos[a]).CompareTo(Angle(rimPos[b])));

        var ring0 = new Vector3[Seg]; var ring0N = new Vector3[Seg];
        float meanR = 0f;
        for (int j = 0; j < Seg; j++)
        {
            float phi = -Mathf.PI + j * 2f * Mathf.PI / Seg;
            int hi = order.FindIndex(i => Angle(rimPos[i]) >= phi);
            if (hi < 0) hi = 0;
            int lo = (hi - 1 + order.Count) % order.Count;
            float a0 = Angle(rimPos[order[lo]]), a1 = Angle(rimPos[order[hi]]);
            if (a1 < a0) a1 += 2f * Mathf.PI;
            float pj = phi < a0 ? phi + 2f * Mathf.PI : phi;
            float t = a1 - a0 > 1e-4f ? Mathf.Clamp01((pj - a0) / (a1 - a0)) : 0f;
            ring0[j] = Vector3.Lerp(rimPos[order[lo]], rimPos[order[hi]], t);
            ring0N[j] = Vector3.Slerp(rimNrm[order[lo]], rimNrm[order[hi]], t).normalized;
            Vector3 d = ring0[j] - centre;
            Vector3 radialOut = (d - axis * Vector3.Dot(d, axis));
            meanR += radialOut.magnitude;
            // een paar cm naar buiten en iets terug in de tunnel: de rand zit zo net achter de wand,
            // dus geen kiertjes tussen de bemonsterde ring en de echte (onregelmatige) tunnelrand
            ring0[j] += radialOut.normalized * RimOverlap - axis * RimOverlap;
        }
        meanR /= Seg;
        float lumen = Mathf.Min(meanR, 1.35f);

        // ---- 3. verlenging: van de exacte rand naar een ronde, smaller wordende, donkere buis ----
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var cols = new List<Color>(); var uvs = new List<Vector2>();
        var tris = new List<int>();
        for (int i = 0; i <= Rings; i++)
        {
            float k = i / (float)Rings;
            float z = Length * Mathf.Pow(k, 1.35f);
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(z / 1.6f));          // rand -> ronde buis
            float narrow = Mathf.Lerp(1f, 0.12f, Mathf.Pow(Mathf.Clamp01((z - 1f) / (Length - 1f)), 1.3f));
            float hau = 1f + 0.08f * Mathf.Sin(z * 2f * Mathf.PI / 1.4f) * Mathf.Clamp01(z - 0.6f);
            float shade = Mathf.Exp(-Mathf.Max(0f, z - 0.5f) * 1.25f);
            Vector3 bend = side * 0.8f * k * k + up * 0.3f * k * k;
            Vector3 c = centre + axis * z + bend;
            for (int j = 0; j <= Seg; j++)
            {
                int jj = j % Seg;
                float phi = -Mathf.PI + jj * 2f * Mathf.PI / Seg;
                Vector3 radial = side * Mathf.Cos(phi) + up * Mathf.Sin(phi);
                float wob = 1f + 0.04f * Mathf.Sin(phi * 3f + z * 1.7f);
                Vector3 round = c + radial * lumen * narrow * hau * wob;
                Vector3 fromRim = ring0[jj] + axis * z + bend;
                Vector3 p = i == 0 ? ring0[jj] : Vector3.Lerp(fromRim, round, blend);
                verts.Add(tunnel.InverseTransformPoint(p));
                norms.Add(Vector3.zero);
                cols.Add(new Color(shade, shade, shade, 1f));
                uvs.Add(new Vector2(j / (float)Seg * 4f, z * 0.45f));
            }
        }
        int row = Seg + 1;
        for (int i = 0; i < Rings; i++)
            for (int j = 0; j < Seg; j++)
            {
                int a = i * row + j, b = a + 1, c = a + row, d = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        int tip = verts.Count;
        verts.Add(tunnel.InverseTransformPoint(centre + axis * (Length + 0.15f) + side * 0.8f + up * 0.3f));
        norms.Add(Vector3.zero); cols.Add(Color.black); uvs.Add(new Vector2(0.5f, Length * 0.45f));
        int last = Rings * row;
        for (int j = 0; j < Seg; j++) { tris.Add(last + j); tris.Add(tip); tris.Add(last + j + 1); }

        var meshOut = new Mesh { name = "Colon_EndFunnel_" + label };
        meshOut.SetVertices(verts); meshOut.SetColors(cols); meshOut.SetUVs(0, uvs); meshOut.SetTriangles(tris, 0);
        meshOut.RecalculateNormals();

        // binnenkant moet naar de as kijken; anders omdraaien
        var nn = meshOut.normals;
        int probe = (Rings / 2) * row;
        Vector3 pw = tunnel.TransformPoint(verts[probe]);
        Vector3 cw = centre + axis * Vector3.Dot(pw - centre, axis);
        if (Vector3.Dot(tunnel.TransformDirection(nn[probe]), cw - pw) < 0f)
        {
            var t = meshOut.triangles;
            for (int q = 0; q < t.Length; q += 3) { int tmp = t[q + 1]; t[q + 1] = t[q + 2]; t[q + 2] = tmp; }
            meshOut.triangles = t; meshOut.RecalculateNormals(); nn = meshOut.normals;
        }
        // naad rond gladmaken en de eerste ring dezelfde normalen geven als de tunnelrand (geen lichtovergang)
        for (int i = 0; i <= Rings; i++) { var avg = (nn[i * row] + nn[i * row + Seg]).normalized; nn[i * row] = avg; nn[i * row + Seg] = avg; }
        for (int j = 0; j <= Seg; j++)
        {
            Vector3 rimN = tunnel.InverseTransformDirection(ring0N[j % Seg]);
            if (Vector3.Dot(rimN, nn[j]) < 0f) rimN = -rimN;
            nn[j] = rimN.normalized;
            nn[row + j] = Vector3.Slerp(rimN, nn[row + j], 0.5f).normalized;
        }
        meshOut.normals = nn;
        meshOut.RecalculateBounds();

        string path = MeshFolder + "/Colon_EndFunnel_" + label + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) { EditorUtility.CopySerialized(meshOut, existing); meshOut = existing; }
        else AssetDatabase.CreateAsset(meshOut, path);

        var go = new GameObject("Colon_EndFunnel_" + label, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(tunnel, false);   // kind van de tunnel: beweegt mee via ColonWallMotion
        go.GetComponent<MeshFilter>().sharedMesh = meshOut;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        Debug.Log($"ColonEndFunnelBuilder: {label} gebouwd ({rimPos.Count} randpunten, straal {meanR:F2} m).");
    }
}
