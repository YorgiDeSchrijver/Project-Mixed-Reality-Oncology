using System;
using UnityEngine;

namespace Oncologie.IRL
{
    /// <summary>Eén kaarsje op de taart.</summary>
    public class Candle : MonoBehaviour
    {
        public GameObject flame;
        public bool IsLit { get; private set; }
        public Vector3 Wick => flame != null ? flame.transform.position : transform.position + Vector3.up * 0.07f;

        private Vector3 _baseScale;

        private void Awake()
        {
            if (flame != null) { _baseScale = flame.transform.localScale; flame.SetActive(false); }
        }

        public void SetLit(bool lit)
        {
            IsLit = lit;
            if (flame != null) flame.SetActive(lit);
        }

        private void Update()
        {
            if (!IsLit || flame == null) return;
            float n = Mathf.PerlinNoise(Time.time * 9f, transform.position.x * 31f);
            flame.transform.localScale = new Vector3(_baseScale.x * (0.9f + n * 0.2f), _baseScale.y * (0.8f + n * 0.4f), _baseScale.z * (0.9f + n * 0.2f));
        }
    }
}
