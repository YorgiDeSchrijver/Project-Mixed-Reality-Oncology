using Oncologie.Core;
using UnityEngine;

namespace Oncologie.IRL
{
    /// <summary>
    /// Een plek waar je de envelop afgeeft. Houd de envelop een kort moment in de zone (VR: je hand bij Luc's hand
    /// of boven de vuilbak; desktop: envelop ervoor houden) en de keuze is gemaakt.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DropZone : MonoBehaviour
    {
        public ScreeningChoice choice = ScreeningChoice.InsistedKeys;
        [Tooltip("Waar de envelop naartoe glijdt na afgeven.")]
        public Transform snapPoint;
        [Tooltip("Verberg de envelop na aankomst (bv. in de vuilbak).")]
        public bool hideOnArrive;
        public Highlighter highlighter;
        [Tooltip("Optioneel deksel dat opengaat als de envelop in de buurt komt.")]
        public Transform lid;
        public Vector3 lidOpenEuler = new Vector3(-75f, 0f, 0f);
        public float dwell = 0.25f;

        public bool Armed { get; set; }

        private Collider _col;
        private float _timer;
        private bool _shown;
        private Quaternion _lidClosed;
        private float _lidOpen;

        private void Awake()
        {
            _col = GetComponent<Collider>();
            _col.isTrigger = true;
            if (lid != null) _lidClosed = lid.localRotation;
            if (highlighter != null) highlighter.Set(false);
        }

        private void Update()
        {
            var env = EnvelopeItem.Current;
            bool active = Armed && env != null && env.IsHeld && !env.Delivered;

            if (active != _shown)
            {
                _shown = active;
                if (highlighter != null) highlighter.Set(active);
            }

            float lidTarget = 0f;
            if (active)
            {
                Vector3 p = env.transform.position;
                if (lid != null && Vector3.Distance(p, _col.bounds.center) < 0.9f) lidTarget = 1f;

                if (Inside(p))
                {
                    _timer += Time.deltaTime;
                    if (_timer >= dwell) { _timer = 0; env.DeliverTo(this); }
                }
                else _timer = 0;
            }
            else _timer = 0;

            if (lid != null)
            {
                if (env != null && env.Delivered && env.transform.IsChildOf(transform)) lidTarget = 0f;
                _lidOpen = Mathf.MoveTowards(_lidOpen, lidTarget, Time.deltaTime * 3f);
                lid.localRotation = _lidClosed * Quaternion.Euler(lidOpenEuler * Mathf.SmoothStep(0, 1, _lidOpen));
            }
        }

        public bool Inside(Vector3 p) => (_col.ClosestPoint(p) - p).sqrMagnitude < 1e-6f;
    }
}
