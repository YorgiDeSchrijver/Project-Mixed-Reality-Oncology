using UnityEngine;

namespace Oncologie.IRL
{
    /// <summary>
    /// Zachte pulserende markering (een doorschijnende "aura" of ring) om aan te geven waar de speler iets kan doen.
    /// Werkt zonder post-processing, dus ook goedkoop op Quest.
    /// </summary>
    public class Highlighter : MonoBehaviour
    {
        [Tooltip("Object dat aan/uit gezet wordt en pulseert (bv. ring of aura).")]
        public GameObject visual;
        public Color color = new Color(1f, 0.85f, 0.35f, 0.55f);
        public float pulseSpeed = 2.4f;
        public float pulseScale = 0.12f;
        public bool startOn;

        private Vector3 _baseScale;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private bool _on;

        private void Awake()
        {
            if (visual == null) return;
            _baseScale = visual.transform.localScale;
            _renderers = visual.GetComponentsInChildren<Renderer>(true);
            _mpb = new MaterialPropertyBlock();
            Set(startOn);
        }

        public void Set(bool on)
        {
            _on = on;
            if (visual != null) visual.SetActive(on);
        }

        private void Update()
        {
            if (!_on || visual == null) return;
            float s = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            visual.transform.localScale = _baseScale * (1f + s * pulseScale);
            var c = color; c.a *= Mathf.Lerp(0.45f, 1f, s);
            foreach (var r in _renderers)
            {
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_BaseColor", c);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
