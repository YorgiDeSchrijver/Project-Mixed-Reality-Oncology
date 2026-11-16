using System;
using UnityEngine;

namespace Oncologie.IRL
{
    /// <summary>Vuurt wanneer de speler een tijdje naar dit object kijkt (werkt met headset en muis).</summary>
    public class LookAtTrigger : MonoBehaviour
    {
        public float maxAngle = 14f;
        public float maxDistance = 4.5f;
        public float requiredSeconds = 1.5f;
        public bool once = true;
        public bool armed = true;

        public event Action Looked;

        private float _t;
        private bool _done;

        private void Update()
        {
            if (!armed || (_done && once)) return;
            var cam = WorldUI.MainCamera();
            if (cam == null) return;
            Vector3 to = transform.position - cam.transform.position;
            bool looking = to.magnitude < maxDistance && Vector3.Angle(cam.transform.forward, to) < maxAngle;
            _t = looking ? _t + Time.deltaTime : Mathf.Max(0, _t - Time.deltaTime * 2f);
            if (_t >= requiredSeconds)
            {
                _done = true;
                _t = 0;
                Looked?.Invoke();
            }
        }
    }
}
