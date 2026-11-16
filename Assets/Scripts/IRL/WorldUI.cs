using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Oncologie.IRL
{
    /// <summary>
    /// Hulpfuncties voor VR-vriendelijke UI (world-space canvassen) en runtime-materialen.
    /// De basismaterialen staan in Resources/IRL zodat ze ook in een build bestaan.
    /// </summary>
    public static class WorldUI
    {
        private static Font _font;
        private static Material _overlayUI;

        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        /// <summary>UI-materiaal dat altijd bovenop alles tekent (ook over de zwarte fade heen).</summary>
        public static Material OverlayUIMaterial
        {
            get
            {
                if (_overlayUI == null)
                {
                    _overlayUI = new Material(Shader.Find("UI/Default")) { name = "UI_Overlay" };
                    _overlayUI.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
                    _overlayUI.renderQueue = 4010;
                }
                return _overlayUI;
            }
        }

        public static Material Unlit(Color color, bool transparent, bool doubleSided = false, int queueOffset = 0)
        {
            var src = Resources.Load<Material>(transparent ? "IRL/M_UnlitTransparent" : "IRL/M_UnlitOpaque");
            Material m = src != null ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", color);
            if (doubleSided) m.SetFloat("_Cull", 0f);
            if (queueOffset != 0) m.renderQueue += queueOffset;
            return m;
        }

        public static Canvas CreateCanvas(string name, Transform parent, Vector2 sizePx, float metersPerPixel)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = sizePx;
            rt.localScale = Vector3.one * metersPerPixel;
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
            return canvas;
        }

        public static Image AddPanel(Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            var go = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text AddText(Transform parent, string text, int size, Color color, TextAnchor anchor,
                                   Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin = default, Vector2 offsetMax = default, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            var t = go.GetComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.color = color; t.alignment = anchor;
            t.fontStyle = style; t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>Zet alle Graphics van een canvas op het "altijd bovenop"-materiaal.</summary>
        public static void MakeOverlay(Canvas canvas)
        {
            if (canvas == null) return;
            canvas.sortingOrder = 100;
            foreach (var g in canvas.GetComponentsInChildren<Graphic>(true)) g.material = OverlayUIMaterial;
        }

        public static Camera MainCamera()
        {
            var cam = Camera.main;
            if (cam != null) return cam;
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude))
                if (c.isActiveAndEnabled && c.targetTexture == null) return c;
            return null;
        }
    }
}
