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
        [SerializeField] private bool rotateObject = false; // Standaard UIT: borden en tekst mogen nooit wegdraaien!
        [SerializeField] private bool faceCamera = false;
        [SerializeField] private float rotationSpeed = 30f;
        [SerializeField] private float bobAmplitude = 0.02f;
        [SerializeField] private float bobFrequency = 1.5f;

        private Vector3 _basePos;
        private bool _isHovered = false;

        private void Start()
        {
            _basePos = transform.position;

            // Ondersteuning voor VR Controller Ray / Direct Poke select
            var interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
            if (interactable != null)
            {
                interactable.selectEntered.AddListener((args) => TriggerRewind());
            }
        }

        private void Update()
        {
            if (rotateObject)
            {
                transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            }

            if (faceCamera && Camera.main != null)
            {
                Vector3 toCam = Camera.main.transform.position - transform.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude > 0.01f)
                {
                    // Canvas kijkt naar de speler
                    transform.rotation = Quaternion.LookRotation(-toCam);
                }
            }

            if (bobAmplitude > 0f)
            {
                float newY = _basePos.y + Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
                transform.position = new Vector3(_basePos.x, newY, _basePos.z);
            }
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
