using System.Collections.Generic;
using UnityEngine;

namespace Oncologie.IRL
{
    /// <summary>
    /// UV-instelling voor één vlak: ofwel getegeld in meters (voor muren, vloer, hout),
    /// ofwel uitgerekt over een uitsnede (Rect) van een atlas-texture.
    /// </summary>
    public struct FaceUV
    {
        public bool tiled;
        public float tile;   // meter per texture-herhaling
        public Rect rect;    // atlas-uitsnede (0..1)

        public static FaceUV Tiled(float metersPerRepeat) => new FaceUV { tiled = true, tile = metersPerRepeat };
        public static FaceUV Atlas(Rect r) => new FaceUV { tiled = false, rect = r };
        public static implicit operator FaceUV(Rect r) => Atlas(r);
    }

    /// <summary>
    /// Bouwt eenvoudige, low-poly meshes met degelijke UV's. Gebruikt door de keukenbouwer (editor)
    /// en door het anatomie-hologram (runtime).
    /// </summary>
    public static class MeshFactory
    {
        /// <summary>Pixel-rechthoek (y van boven) in een atlas van <paramref name="size"/> px naar UV-Rect, met kleine inset tegen bleeding.</summary>
        public static Rect Px(float x0, float y0, float x1, float y1, float size = 1024f, float inset = 4f)
        {
            x0 += inset; y0 += inset; x1 -= inset; y1 -= inset;
            return new Rect(x0 / size, 1f - y1 / size, (x1 - x0) / size, (y1 - y0) / size);
        }

        // Volgorde vlakken: +X, -X, +Y, -Y, +Z, -Z
        static readonly Vector3[] N = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        static readonly Vector3[] U = { Vector3.forward, Vector3.back, Vector3.right, Vector3.right, Vector3.left, Vector3.right };
        static readonly Vector3[] V = { Vector3.up, Vector3.up, Vector3.forward, Vector3.back, Vector3.up, Vector3.up };

        /// <summary>Balk gecentreerd rond de oorsprong. faces: 1 waarde voor alles, of 6 (+X,-X,+Y,-Y,+Z,-Z).</summary>
        public static Mesh Box(Vector3 size, params FaceUV[] faces)
        {
            if (faces == null || faces.Length == 0) faces = new[] { FaceUV.Tiled(1f) };
            var verts = new List<Vector3>(24); var norms = new List<Vector3>(24);
            var uvs = new List<Vector2>(24); var tris = new List<int>(36);
            Vector3 h = size * 0.5f;
            for (int f = 0; f < 6; f++)
            {
                FaceUV fu = faces.Length >= 6 ? faces[f] : faces[0];
                Vector3 n = N[f], u = U[f], v = V[f];
                float hu = Mathf.Abs(Vector3.Dot(h, u)), hv = Mathf.Abs(Vector3.Dot(h, v)), hn = Mathf.Abs(Vector3.Dot(h, n));
                Vector3 c = n * hn;
                int b = verts.Count;
                Vector3[] corners = { c - u * hu - v * hv, c - u * hu + v * hv, c + u * hu + v * hv, c + u * hu - v * hv };
                Vector2[] t = { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
                for (int i = 0; i < 4; i++)
                {
                    verts.Add(corners[i]); norms.Add(n);
                    uvs.Add(fu.tiled ? new Vector2(t[i].x * hu * 2f / fu.tile, t[i].y * hv * 2f / fu.tile)
                                     : new Vector2(fu.rect.x + t[i].x * fu.rect.width, fu.rect.y + t[i].y * fu.rect.height));
                }
                tris.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
            return Finish("Box", verts, norms, uvs, tris);
        }

        /// <summary>Vlak met zichtbare kant naar -Z (zoals Unity's Quad). Pivot in het midden.</summary>
        public static Mesh Quad(float width, float height, FaceUV uv)
        {
            float hw = width * 0.5f, hh = height * 0.5f;
            var verts = new List<Vector3> { new Vector3(-hw, -hh, 0), new Vector3(-hw, hh, 0), new Vector3(hw, hh, 0), new Vector3(hw, -hh, 0) };
            var norms = new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            Vector2[] t = { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            var uvs = new List<Vector2>();
            foreach (var p in t)
                uvs.Add(uv.tiled ? new Vector2(p.x * width / uv.tile, p.y * height / uv.tile)
                                 : new Vector2(uv.rect.x + p.x * uv.rect.width, uv.rect.y + p.y * uv.rect.height));
            return Finish("Quad", verts, norms, uvs, new List<int> { 0, 1, 2, 0, 2, 3 });
        }

        /// <summary>Cilinder/kegel met pivot onderaan. sideUV wordt rondom uitgerekt, capUV op de deksels.</summary>
        public static Mesh Cylinder(float radiusBottom, float radiusTop, float height, int segments, FaceUV sideUV, FaceUV capUV, bool capTop = true, bool capBottom = true)
        {
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            float slope = (radiusBottom - radiusTop) / Mathf.Max(height, 0.0001f);
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Vector3 n = (dir + Vector3.up * slope).normalized;
                verts.Add(dir * radiusBottom); norms.Add(n);
                verts.Add(dir * radiusTop + Vector3.up * height); norms.Add(n);
                float u = i / (float)segments;
                if (sideUV.tiled)
                {
                    float circ = Mathf.PI * 2f * Mathf.Max(radiusBottom, radiusTop);
                    uvs.Add(new Vector2(u * circ / sideUV.tile, 0)); uvs.Add(new Vector2(u * circ / sideUV.tile, height / sideUV.tile));
                }
                else
                {
                    uvs.Add(new Vector2(sideUV.rect.x + u * sideUV.rect.width, sideUV.rect.y));
                    uvs.Add(new Vector2(sideUV.rect.x + u * sideUV.rect.width, sideUV.rect.yMax));
                }
                if (i < segments)
                {
                    int b = i * 2;
                    tris.AddRange(new[] { b, b + 1, b + 3, b, b + 3, b + 2 });
                }
            }
            if (capTop && radiusTop > 0f) AddCap(verts, norms, uvs, tris, height, radiusTop, segments, capUV, true);
            if (capBottom && radiusBottom > 0f) AddCap(verts, norms, uvs, tris, 0f, radiusBottom, segments, capUV, false);
            return Finish("Cylinder", verts, norms, uvs, tris);
        }

        static void AddCap(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<int> tris, float y, float r, int seg, FaceUV uv, bool top)
        {
            Vector3 n = top ? Vector3.up : Vector3.down;
            int c = verts.Count;
            verts.Add(new Vector3(0, y, 0)); norms.Add(n); uvs.Add(CapUV(uv, 0, 0, r));
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                verts.Add(new Vector3(x, y, z)); norms.Add(n); uvs.Add(CapUV(uv, x, z, r));
                if (i < seg)
                {
                    if (top) tris.AddRange(new[] { c, c + i + 2, c + i + 1 });
                    else tris.AddRange(new[] { c, c + i + 1, c + i + 2 });
                }
            }
        }

        static Vector2 CapUV(FaceUV uv, float x, float z, float r)
        {
            if (uv.tiled) return new Vector2(x / uv.tile, z / uv.tile);
            return new Vector2(uv.rect.center.x + x / (2f * r) * uv.rect.width, uv.rect.center.y + z / (2f * r) * uv.rect.height);
        }

        /// <summary>UV-bol, pivot in het midden.</summary>
        public static Mesh Sphere(float radius, int lat, int lon, FaceUV uv)
        {
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            for (int y = 0; y <= lat; y++)
            {
                float v = y / (float)lat, th = v * Mathf.PI;
                for (int x = 0; x <= lon; x++)
                {
                    float u = x / (float)lon, ph = u * Mathf.PI * 2f;
                    Vector3 n = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    verts.Add(n * radius); norms.Add(n);
                    uvs.Add(uv.tiled ? new Vector2(u, 1 - v) : new Vector2(uv.rect.x + u * uv.rect.width, uv.rect.y + (1 - v) * uv.rect.height));
                    if (y < lat && x < lon)
                    {
                        int a = y * (lon + 1) + x, b = a + lon + 1;
                        tris.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                    }
                }
            }
            return Finish("Sphere", verts, norms, uvs, tris);
        }

        /// <summary>Platte ring (of schijf als inner = 0) in het XZ-vlak, zichtbaar van boven.</summary>
        public static Mesh Ring(float inner, float outer, int segments)
        {
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                verts.Add(d * inner); verts.Add(d * outer);
                norms.Add(Vector3.up); norms.Add(Vector3.up);
                uvs.Add(new Vector2(i / (float)segments, 0)); uvs.Add(new Vector2(i / (float)segments, 1));
                if (i < segments) { int b = i * 2; tris.AddRange(new[] { b, b + 3, b + 1, b, b + 2, b + 3 }); }
            }
            return Finish("Ring", verts, norms, uvs, tris);
        }

        /// <summary>Buis langs een pad (Catmull-Rom gladgemaakt). fraction 0..1 = hoeveel van het pad getekend wordt.</summary>
        public static Mesh Tube(IList<Vector3> controlPoints, float radius, int sides, int samples, float fraction = 1f, Mesh reuse = null)
        {
            var path = new List<Vector3>();
            int segs = controlPoints.Count - 1;
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples * segs;
                int k = Mathf.Min(Mathf.FloorToInt(t), segs - 1);
                float lt = t - k;
                Vector3 p0 = controlPoints[Mathf.Max(k - 1, 0)], p1 = controlPoints[k], p2 = controlPoints[k + 1], p3 = controlPoints[Mathf.Min(k + 2, segs)];
                path.Add(0.5f * (2f * p1 + (-p0 + p2) * lt + (2f * p0 - 5f * p1 + 4f * p2 - p3) * lt * lt + (-p0 + 3f * p1 - 3f * p2 + p3) * lt * lt * lt));
            }
            int count = Mathf.Clamp(Mathf.CeilToInt(path.Count * Mathf.Clamp01(fraction)), 2, path.Count);
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            Vector3 prevNormal = Vector3.forward;
            for (int i = 0; i < count; i++)
            {
                Vector3 fwd = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                Vector3 side = Vector3.Cross(fwd, prevNormal);
                if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(fwd, Vector3.up);
                side.Normalize();
                Vector3 up = Vector3.Cross(side, fwd).normalized;
                prevNormal = up;
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    Vector3 n = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                    verts.Add(path[i] + n * radius); norms.Add(n);
                    uvs.Add(new Vector2(s / (float)sides, i / (float)samples));
                    if (i < count - 1 && s < sides)
                    {
                        int b = i * (sides + 1) + s, c = b + sides + 1;
                        tris.AddRange(new[] { b, c, b + 1, b + 1, c, c + 1 });
                    }
                }
            }
            var m = reuse != null ? reuse : new Mesh { name = "Tube" };
            m.Clear();
            m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        static Mesh Finish(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateTangents(); m.RecalculateBounds();
            return m;
        }
    }
}
