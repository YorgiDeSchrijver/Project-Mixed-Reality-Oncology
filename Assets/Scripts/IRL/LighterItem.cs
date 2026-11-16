using UnityEngine;

namespace Oncologie.IRL
{
    /// <summary>Aansteker: zolang je hem vasthoudt brandt het vlammetje; houd het bij een kaarsje om het aan te steken.</summary>
    public class LighterItem : InteractableItem
    {
        public Transform flameTip;
        public GameObject flame;
        public CandleSet candles;
        public Highlighter highlighter;
        [Tooltip("Hoe dicht de vlam bij de lont moet (m). Ruim gekozen voor beginners.")]
        public float igniteRadius = 0.09f;

        protected override void Awake()
        {
            base.Awake();
            if (flame != null) flame.SetActive(false);
        }

        protected override void OnGrab()
        {
            if (flame != null) flame.SetActive(true);
            if (highlighter != null) highlighter.Set(false);
        }

        protected override void OnRelease()
        {
            if (flame != null) flame.SetActive(false);
        }

        public void ShowHint(bool on)
        {
            if (highlighter != null) highlighter.Set(on && !IsHeld);
        }

        private void Update()
        {
            if (!IsHeld || candles == null || flameTip == null) return;
            candles.TryIgnite(flameTip.position, igniteRadius);
        }
    }
}
