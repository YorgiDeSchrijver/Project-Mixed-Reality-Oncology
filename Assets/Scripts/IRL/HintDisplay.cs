using UnityEngine;
using UnityEngine.UI;

namespace Oncologie.IRL
{
    /// <summary>
    /// Kleine, groene "wat moet ik doen?"-hint onderaan het zicht. Los van de ondertitels,
    /// zodat dialogen en bedieningsuitleg elkaar niet wegduwen. Werkt in VR (world-space aan de camera).
    /// </summary>
    public class HintDisplay : MonoBehaviour
    {
        private static HintDisplay _instance;
        private Canvas _canvas;
        private CanvasGroup _group;
        private Text _text;
        private float _target;
        private static bool _suppressed;

        /// <summary>Tijdelijk verbergen (bv. terwijl de brief open is) zonder de tekst te verliezen.</summary>
        public static void Suppress(bool on) => _suppressed = on;

        public static void Show(string text)
        {
            var h = Get();
            if (h == null) return;
            h._text.text = text;
            h._target = 1f;
        }

        public static void Hide()
        {
            if (_instance != null) _instance._target = 0f;
        }

        private static HintDisplay Get()
        {
            if (_instance != null) return _instance;
            var cam = WorldUI.MainCamera();
            if (cam == null) return null;
            var canvas = WorldUI.CreateCanvas("HintDisplay", cam.transform, new Vector2(1100, 120), 0.001f);
            canvas.transform.localPosition = new Vector3(0, -0.40f, 1.3f);
            canvas.transform.localRotation = Quaternion.Euler(12f, 0, 0);
            _instance = canvas.gameObject.AddComponent<HintDisplay>();
            _instance._canvas = canvas;
            _instance._group = canvas.GetComponent<CanvasGroup>();
            _instance._group.alpha = 0;
            WorldUI.AddPanel(canvas.transform, new Color(0.02f, 0.08f, 0.04f, 0.72f), Vector2.zero, Vector2.one);
            _instance._text = WorldUI.AddText(canvas.transform, "", 38, new Color(0.6f, 1f, 0.65f), TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, new Vector2(24, 8), new Vector2(-24, -8));
            WorldUI.MakeOverlay(canvas);
            return _instance;
        }

        private void Update()
        {
            _group.alpha = Mathf.MoveTowards(_group.alpha, _suppressed ? 0f : _target, Time.deltaTime * 3f);
        }

        private void OnDestroy()
        {
            if (_instance == this) { _instance = null; _suppressed = false; }
        }
    }
}
