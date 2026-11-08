using UnityEngine;
using Oncologie.Core;

namespace Oncologie.Interaction
{
    /// <summary>
    /// Interactief 3D-object op de keukentafel (zakhorloge / tijdmachine) waarmee
    /// de speler in VR of op Desktop de tijd kan terugspoelen zonder toetsenbord.
    /// </summary>
    public class InteractiveRewindClock : MonoBehaviour
    {
        [Header("Visuele Effecten")]
        [SerializeField] private float rotationSpeed = 45f;
        [SerializeField] private float bobAmplitude = 0.03f;
        [SerializeField] private float bobFrequency = 2.0f;

        private Vector3 _basePos;
        private bool _isHovered = false;

        private void Start()
        {
            _basePos = transform.position;
        }

        private void Update()
        {
            // Zwevend en langzaam roterend effect
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            float newY = _basePos.y + Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
            transform.position = new Vector3(_basePos.x, newY, _basePos.z);
        }

        private void OnMouseDown()
        {
            TriggerRewind();
        }

        private void OnTriggerEnter(Collider other)
        {
            // Hand collider in VR
            if (other.name.ToLower().Contains("hand") || other.name.ToLower().Contains("controller") || other.name.ToLower().Contains("player"))
            {
                TriggerRewind();
            }
        }

        public void TriggerRewind()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayWhoosh();
            }

            if (ExperienceFlowManager.Instance != null)
            {
                ExperienceFlowManager.Instance.RewindTime();
            }
        }
    }
}
