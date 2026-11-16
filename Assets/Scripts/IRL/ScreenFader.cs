using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Oncologie.IRL
{
    /// <summary>
    /// Fade naar/van een kleur die in VR én op desktop werkt: een vlak vlak voor de camera
    /// (screen-space overlays zijn onzichtbaar in een headset). Daarnaast een titelkaart die óver de fade tekent.
    /// Leeft per scène; gebruik ScreenFader.Instance.
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        private static ScreenFader _instance;
        public static ScreenFader Instance
        {
            get
            {
                if (_instance == null) _instance = new GameObject("[ScreenFader]").AddComponent<ScreenFader>();
                return _instance;
            }
        }

        private Transform _quad;
        private Material _mat;
        private Camera _cam;
        private float _alpha;
        private Color _color = Color.black;

        private Canvas _titleCanvas;
        private CanvasGroup _titleGroup;
        private Text _title, _subtitle;

        public float Alpha => _alpha;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void EnsureQuad()
        {
            var cam = WorldUI.MainCamera();
            if (cam == null) return;
            if (_quad != null && _cam == cam) return;
            _cam = cam;
            if (_quad == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "FadeQuad";
                Destroy(go.GetComponent<Collider>());
                _mat = WorldUI.Unlit(new Color(0, 0, 0, 0), true, true, 1000); // queue ~4000
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = _mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                _quad = go.transform;
            }
            _quad.SetParent(cam.transform, false);
            float d = Mathf.Max(cam.nearClipPlane * 1.5f, 0.05f);
            _quad.localPosition = new Vector3(0, 0, d);
            _quad.localRotation = Quaternion.identity;
            float h = 2f * d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            _quad.localScale = new Vector3(h * 6f, h * 6f, 1f); // ruim, ook voor brede VR-FOV
            Apply();
        }

        private void Apply()
        {
            if (_mat == null) return;
            var c = _color; c.a = _alpha;
            _mat.SetColor("_BaseColor", c);
            if (_quad != null) _quad.gameObject.SetActive(_alpha > 0.001f);
        }

        public void SetImmediate(float alpha, Color? color = null)
        {
            if (color.HasValue) _color = color.Value;
            _alpha = alpha;
            EnsureQuad();
            Apply();
        }

        public IEnumerator Fade(float to, float duration, Color? color = null)
        {
            if (color.HasValue) _color = color.Value;
            EnsureQuad();
            float from = _alpha, t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                _alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, t / duration));
                EnsureQuad();
                Apply();
                yield return null;
            }
            _alpha = to;
            Apply();
        }

        // ---------------- Titelkaart ----------------

        private void EnsureTitle()
        {
            var cam = WorldUI.MainCamera();
            if (cam == null) return;
            if (_titleCanvas == null)
            {
                _titleCanvas = WorldUI.CreateCanvas("TitleCard", cam.transform, new Vector2(1400, 520), 0.0011f);
                _titleGroup = _titleCanvas.GetComponent<CanvasGroup>();
                _title = WorldUI.AddText(_titleCanvas.transform, "", 96, new Color(1f, 0.93f, 0.8f), TextAnchor.LowerCenter,
                    new Vector2(0, 0.45f), new Vector2(1, 1), Vector2.zero, Vector2.zero, FontStyle.Bold);
                _subtitle = WorldUI.AddText(_titleCanvas.transform, "", 48, new Color(1f, 1f, 1f, 0.85f), TextAnchor.UpperCenter,
                    new Vector2(0.05f, 0), new Vector2(0.95f, 0.42f));
                WorldUI.MakeOverlay(_titleCanvas);
            }
            if (_titleCanvas.transform.parent != cam.transform) _titleCanvas.transform.SetParent(cam.transform, false);
            _titleCanvas.transform.localPosition = new Vector3(0, 0.02f, 1.4f);
            _titleCanvas.transform.localRotation = Quaternion.identity;
        }

        public IEnumerator ShowTitle(string title, string subtitle, float hold, float fade = 0.8f)
        {
            EnsureTitle();
            if (_titleCanvas == null) yield break;
            _title.text = title; _subtitle.text = subtitle;
            _titleCanvas.gameObject.SetActive(true);
            yield return FadeGroup(0, 1, fade);
            yield return new WaitForSeconds(hold);
            yield return FadeGroup(1, 0, fade);
            _titleCanvas.gameObject.SetActive(false);
        }

        private IEnumerator FadeGroup(float a, float b, float d)
        {
            float t = 0;
            while (t < d) { t += Time.deltaTime; _titleGroup.alpha = Mathf.Lerp(a, b, t / d); yield return null; }
            _titleGroup.alpha = b;
        }
    }
}
