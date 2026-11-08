using UnityEngine;
using UnityEngine.Events;

namespace Oncologie.Core
{
    [RequireComponent(typeof(Collider))]
    public class ScreeningKitInteraction : MonoBehaviour
    {
        [Header("Glow Feedback")]
        [SerializeField] private ObjectGlowPulse glowEffect;

        [Header("Standaard Tafel Positie")]
        [SerializeField] private Vector3 initialTablePosition = new Vector3(-0.41f, 1.54f, 0.90f);
        [SerializeField] private Quaternion initialTableRotation = Quaternion.Euler(0f, 15f, 0f);

        [Header("Zone Identificatie")]
        [Tooltip("Objectnaam of tag voor de prullenmand / negeer optie.")]
        [SerializeField] private string trashZoneIdentifier = "Trash";

        [Tooltip("Objectnaam of tag voor de vader / aandring optie.")]
        [SerializeField] private string dadZoneIdentifier = "Scavenger";

        [Header("Events")]
        public UnityEvent onPickedUp;
        public UnityEvent onDeliveredTrash;
        public UnityEvent onDeliveredKeys;

        private Rigidbody _rb;
        private bool _isDelivered = false;
        private bool _hasBeenPickedUp = false;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            ResetPosition();
        }

        public void OnPickedUp()
        {
            _hasBeenPickedUp = true;
            onPickedUp?.Invoke();
        }

        public void ResetPosition()
        {
            _isDelivered = false;
            _hasBeenPickedUp = false;
            transform.position = initialTablePosition;
            transform.rotation = initialTableRotation;

            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            if (glowEffect != null)
            {
                glowEffect.SetGlowing(true);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Veiligheid: de envelop kan NOOIT een keuze triggeren als de speler hem nog niet heeft opgepakt!
            if (_isDelivered || !_hasBeenPickedUp) return;

            string targetName = other.gameObject.name;
            string targetTag = other.gameObject.tag;

            // Check Optie A: Weggooien in de prullenmand
            if (targetName.Contains(trashZoneIdentifier) || targetTag.Contains(trashZoneIdentifier) || targetName.Contains("Bin") || targetName.Contains("Paper"))
            {
                DeliverChoice(ScreeningChoice.IgnoredTrash);
                onDeliveredTrash?.Invoke();
            }
            // Check Optie B: Aan vader Luc geven
            else if (targetName.Contains(dadZoneIdentifier) || targetTag.Contains(dadZoneIdentifier) || targetName.Contains("Dad") || targetName.Contains("Hand") || targetName.Contains("Father"))
            {
                DeliverChoice(ScreeningChoice.InsistedKeys);
                onDeliveredKeys?.Invoke();
            }
        }

        private void DeliverChoice(ScreeningChoice choice)
        {
            _isDelivered = true;

            if (glowEffect != null)
            {
                glowEffect.SetGlowing(false);
            }

            Debug.Log($"[ScreeningKitInteraction] Envelop geplaatst! Keuze: {choice}");

            if (ExperienceFlowManager.Instance != null)
            {
                ExperienceFlowManager.Instance.MakeChoice(choice);
            }
        }

        public void SetHover(bool isHovering)
        {
            if (_isDelivered) return;

            if (glowEffect != null)
            {
                glowEffect.SetGlowing(isHovering);
            }
        }
    }
}
