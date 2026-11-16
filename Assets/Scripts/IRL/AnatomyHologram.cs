using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Oncologie.IRL
{
    /// <summary>
    /// Een doorschijnende "scan" van Luc's lichaam met de dikke darm en een poliep erin.
    /// Wordt runtime opgebouwd (geen extra assets) en gebruikt door ColonTransition.
    /// Lokale assen: +Z wijst naar de kijker, +X is Luc's rechterkant (voor de kijker links).
    /// </summary>
    public class AnatomyHologram : MonoBehaviour
    {
        // Verloop van de dikke darm voor iemand van ±1,75 m (meter, t.o.v. de voeten).
        public static readonly Vector3[] ColonPath =
        {
            new Vector3( 0.11f, 0.97f, 0.03f), // blindedarm (caecum)
            new Vector3( 0.13f, 1.08f, 0.04f), // opstijgend deel
            new Vector3( 0.13f, 1.20f, 0.04f),
            new Vector3( 0.11f, 1.27f, 0.05f),
            new Vector3( 0.04f, 1.23f, 0.08f), // dwars deel
            new Vector3(-0.04f, 1.22f, 0.08f),
            new Vector3(-0.12f, 1.29f, 0.04f),
            new Vector3(-0.14f, 1.18f, 0.03f), // dalend deel
            new Vector3(-0.14f, 1.05f, 0.03f),
            new Vector3(-0.11f, 0.96f, 0.05f), // sigmoïd
            new Vector3(-0.04f, 0.99f, 0.07f),
            new Vector3( 0.00f, 0.93f, 0.04f),
            new Vector3( 0.00f, 0.84f, 0.00f), // endeldarm
        };

        public const float ColonRadius = 0.022f;
        /// <summary>Punt op de middellijn van het sigmoïd waar we naartoe "inzoomen".</summary>
        public static readonly Vector3 AimLocal = new Vector3(-0.075f, 0.975f, 0.06f);
        public static readonly Vector3 AimTangentLocal = new Vector3(0.07f, 0.03f, 0.02f).normalized;

        public Transform Marker { get; private set; }

        private readonly List<Material> _bodyMats = new List<Material>();
        private Material _colonMat, _markerMat, _ringMat;
        private Mesh _colonMesh;
        private Transform _ring;
        private CanvasGroup _labelColon, _labelPolyp;
        private float _bodyAlpha, _colonDraw = -1f;
        private bool _markerOn;

        private static readonly Color BodyColor = new Color(0.35f, 0.85f, 1f, 0.16f);
        private static readonly Color ColonColor = new Color(1f, 0.55f, 0.42f, 0.9f);

        public static AnatomyHologram Create(Transform lucFeet, Vector3 lookAtPoint)
        {
            var go = new GameObject("Luc_AnatomyHologram");
            Vector3 dir = lookAtPoint - lucFeet.position; dir.y = 0;
            go.transform.SetPositionAndRotation(lucFeet.position, Quaternion.LookRotation(dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward));
            var h = go.AddComponent<AnatomyHologram>();
            h.Build();
            return h;
        }

        private void Build()
        {
            // Lichaam (silhouet uit primitieven)
            AddPart(PrimitiveType.Sphere, new Vector3(0, 1.62f, 0), new Vector3(0.21f, 0.25f, 0.23f), 0);
            AddPart(PrimitiveType.Capsule, new Vector3(0, 1.47f, 0), new Vector3(0.11f, 0.06f, 0.11f), 0);
            AddPart(PrimitiveType.Capsule, new Vector3(0, 1.22f, 0), new Vector3(0.40f, 0.30f, 0.25f), 0);
            AddPart(PrimitiveType.Capsule, new Vector3(0, 0.93f, 0), new Vector3(0.36f, 0.13f, 0.23f), 0);
            AddPart(PrimitiveType.Capsule, new Vector3(0.27f, 1.12f, 0), new Vector3(0.10f, 0.33f, 0.10f), 8);
            AddPart(PrimitiveType.Capsule, new Vector3(-0.27f, 1.12f, 0), new Vector3(0.10f, 0.33f, 0.10f), -8);
            AddPart(PrimitiveType.Capsule, new Vector3(0.10f, 0.44f, 0), new Vector3(0.15f, 0.42f, 0.15f), 0);
            AddPart(PrimitiveType.Capsule, new Vector3(-0.10f, 0.44f, 0), new Vector3(0.15f, 0.42f, 0.15f), 0);

            // Dikke darm
            var colon = new GameObject("Colon");
            colon.transform.SetParent(transform, false);
            _colonMesh = new Mesh { name = "ColonTube" };
            colon.AddComponent<MeshFilter>().sharedMesh = _colonMesh;
            _colonMat = WorldUI.Unlit(ColonColor, true, true, 3);
            var cr = colon.AddComponent<MeshRenderer>(); cr.sharedMaterial = _colonMat; NoShadows(cr);

            // Poliep-markering
            Vector3 markerPos = AimLocal + AimTangentLocal * 0.02f + Vector3.up * (ColonRadius * 0.9f);
            var m = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(m.GetComponent<Collider>());
            m.name = "Polyp_Marker";
            m.transform.SetParent(transform, false);
            m.transform.localPosition = markerPos;
            m.transform.localScale = Vector3.one * 0.016f;
            _markerMat = WorldUI.Unlit(new Color(0.85f, 0.05f, 0.08f, 1f), true, false, 4);
            var mr = m.GetComponent<MeshRenderer>(); mr.sharedMaterial = _markerMat; NoShadows(mr);
            Marker = m.transform;

            var ring = new GameObject("Polyp_Ring");
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = markerPos;
            ring.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Ring(0.024f, 0.03f, 32);
            _ringMat = WorldUI.Unlit(new Color(1f, 0.2f, 0.2f, 0.9f), true, true, 5);
            var rr = ring.AddComponent<MeshRenderer>(); rr.sharedMaterial = _ringMat; NoShadows(rr);
            _ring = ring.transform;

            _labelColon = MakeLabel("dikke darm\n(± 1,5 m lang)", new Vector3(0.42f, 1.32f, 0.08f));
            _labelPolyp = MakeLabel("poliep", markerPos + new Vector3(-0.32f, -0.06f, 0.06f));

            SetBodyAlpha(0); SetColonDraw(0); SetMarker(false);
        }

        private void AddPart(PrimitiveType type, Vector3 pos, Vector3 scale, float rollDeg)
        {
            var p = GameObject.CreatePrimitive(type);
            Destroy(p.GetComponent<Collider>());
            p.transform.SetParent(transform, false);
            p.transform.localPosition = pos;
            p.transform.localRotation = Quaternion.Euler(0, 0, rollDeg);
            // Unity-capsule is 2 m hoog en 1 m breed: schaal omrekenen naar echte maten.
            p.transform.localScale = type == PrimitiveType.Capsule ? new Vector3(scale.x, scale.y, scale.z) : scale;
            var mat = WorldUI.Unlit(BodyColor, true, false, 1);
            _bodyMats.Add(mat);
            var r = p.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; NoShadows(r);
        }

        private CanvasGroup MakeLabel(string text, Vector3 localPos)
        {
            var canvas = WorldUI.CreateCanvas("Label", transform, new Vector2(320, 100), 0.0013f);
            canvas.transform.localPosition = localPos;
            canvas.transform.localRotation = Quaternion.Euler(0, 180, 0); // leesbaar voor de kijker (+Z)
            WorldUI.AddPanel(canvas.transform, new Color(0, 0.05f, 0.08f, 0.55f), Vector2.zero, Vector2.one);
            WorldUI.AddText(canvas.transform, text, 36, new Color(0.85f, 0.97f, 1f), TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            var g = canvas.GetComponent<CanvasGroup>(); g.alpha = 0;
            return g;
        }

        private static void NoShadows(Renderer r)
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        public void SetBodyAlpha(float a)
        {
            _bodyAlpha = a;
            foreach (var m in _bodyMats) { var c = BodyColor; c.a *= a; m.SetColor("_BaseColor", c); }
        }

        public void SetColonDraw(float f)
        {
            f = Mathf.Clamp01(f);
            if (Mathf.Approximately(f, _colonDraw)) return;
            _colonDraw = f;
            if (f <= 0.001f) { _colonMesh.Clear(); return; }
            MeshFactory.Tube(ColonPath, ColonRadius, 14, 90, f, _colonMesh);
        }

        public void SetMarker(bool on)
        {
            _markerOn = on;
            Marker.gameObject.SetActive(on);
            _ring.gameObject.SetActive(on);
        }

        public void SetLabels(float colonAlpha, float polypAlpha)
        {
            _labelColon.alpha = colonAlpha;
            _labelPolyp.alpha = polypAlpha;
        }

        /// <summary>Alles samen laten vervagen (body, darm, markering).</summary>
        public void SetOverallAlpha(float a)
        {
            SetBodyAlpha(Mathf.Min(_bodyAlpha, a));
            var c = ColonColor; c.a *= a; _colonMat.SetColor("_BaseColor", c);
        }

        private void Update()
        {
            if (!_markerOn) return;
            float s = (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f;
            _ring.localScale = Vector3.one * (1f + s * 0.6f);
            var cam = WorldUI.MainCamera();
            if (cam != null) _ring.rotation = Quaternion.LookRotation(cam.transform.position - _ring.position) * Quaternion.Euler(90, 0, 0);
            _ringMat.SetColor("_BaseColor", new Color(1f, 0.2f, 0.2f, Mathf.Lerp(0.9f, 0.2f, s)));
        }
    }
}
