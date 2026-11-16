using System;
using UnityEngine;

namespace Oncologie.IRL
{
    /// <summary>Alle kaarsjes van de taart + één gedeeld lichtje (goedkoop voor Quest).</summary>
    public class CandleSet : MonoBehaviour
    {
        public Candle[] candles;
        public Light glow;
        public float glowIntensity = 0.9f;

        /// <summary>Globale dim-factor (gezet door de transitie wanneer de keuken donker wordt).</summary>
        public static float Dim = 1f;

        public event Action AllLit;
        public bool AllAreLit { get; private set; }

        private void Awake() => Dim = 1f;
        private void Start() => UpdateGlow();

        public void TryIgnite(Vector3 point, float radius)
        {
            if (AllAreLit) return;
            bool changed = false;
            foreach (var c in candles)
                if (!c.IsLit && Vector3.Distance(c.Wick, point) < radius) { c.SetLit(true); changed = true; }
            if (changed) Refresh();
        }

        public void LightAll()
        {
            foreach (var c in candles) c.SetLit(true);
            Refresh();
        }

        public void ExtinguishAll()
        {
            foreach (var c in candles) c.SetLit(false);
            AllAreLit = false;
            UpdateGlow();
        }

        public int LitCount
        {
            get { int n = 0; foreach (var c in candles) if (c.IsLit) n++; return n; }
        }

        private void Refresh()
        {
            UpdateGlow();
            if (!AllAreLit && LitCount == candles.Length)
            {
                AllAreLit = true;
                AllLit?.Invoke();
            }
        }

        private void UpdateGlow()
        {
            if (glow == null) return;
            glow.enabled = LitCount > 0;
        }

        private void Update()
        {
            if (glow == null || !glow.enabled) return;
            float k = LitCount / (float)Mathf.Max(1, candles.Length);
            glow.intensity = Dim * glowIntensity * k * (0.85f + Mathf.PerlinNoise(Time.time * 7f, 0.3f) * 0.3f);
        }
    }
}
