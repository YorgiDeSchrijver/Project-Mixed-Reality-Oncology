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

            // Automatische integratie met XR Interaction Toolkit (VR controllers)
            var grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grab != null)
            {
                grab.selectEntered.AddListener((args) => OnPickedUp());
            }
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
            ProcessInteraction(other.gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision != null && collision.collider != null)
            {
                ProcessInteraction(collision.collider.gameObject);
            }
        }

        private void Update()
        {
            if (_isDelivered || !_hasBeenPickedUp) return;

            // Nabijheid-detectie als robuuste back-up voor desktop muis-dragging en VR interacties:
            // 1. Optie B: Aan tafel voor Luc of direct bij zijn handen / bord
            Vector3 lucHandsPos = new Vector3(0.45f, 1.45f, 0.50f);
            Vector3 zoneBPos = new Vector3(0.25f, 1.45f, 0.70f);
            if (Vector3.Distance(transform.position, lucHandsPos) < 1.15f || Vector3.Distance(transform.position, zoneBPos) < 1.0f)
            {
                DeliverChoice(ScreeningChoice.InsistedKeys);
                onDeliveredKeys?.Invoke();
                return;
            }

            // 2. Optie A: Bij de prullenmand / oud papier bak
            Vector3 trashPos = new Vector3(-1.35f, 0.50f, 0.35f);
            if (Vector3.Distance(transform.position, trashPos) < 1.0f)
            {
                DeliverChoice(ScreeningChoice.IgnoredTrash);
                onDeliveredTrash?.Invoke();
                return;
            }
        }

        private void ProcessInteraction(GameObject target)
        {
            // Veiligheid: de envelop kan NOOIT een keuze triggeren als de speler hem nog niet heeft opgepakt!
            if (_isDelivered || !_hasBeenPickedUp || target == null) return;

            string targetName = target.name;
            string targetTag = target.tag;

            // Check ook de parent naam (bijv. als je een child van Luc raakt)
            string parentName = target.transform.parent != null ? target.transform.parent.name : "";

            // Check Optie A: Weggooien in de prullenmand
            if (targetName.Contains(trashZoneIdentifier) || targetTag.Contains(trashZoneIdentifier) || 
                targetName.Contains("Bin") || targetName.Contains("Paper") || targetName.Contains("Trash") || targetName.Contains("Zone_Trash") ||
                parentName.Contains("Trash") || parentName.Contains("Bin"))
            {
                DeliverChoice(ScreeningChoice.IgnoredTrash);
                onDeliveredTrash?.Invoke();
            }
            // Check Optie B: Aan vader Luc geven
            else if (targetName.Contains(dadZoneIdentifier) || targetTag.Contains(dadZoneIdentifier) || 
                     targetName.Contains("Dad") || targetName.Contains("Hand") || targetName.Contains("Father") || 
                     targetName.Contains("Luc") || targetName.Contains("GiveZone") || targetName.Contains("Keys") || targetName.Contains("Zone_Keys") ||
                     parentName.Contains("Scavenger") || parentName.Contains("Luc") || parentName.Contains("Dad") || parentName.Contains("Zone_Keys"))
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

            var fm = ExperienceFlowManager.Instance != null 
                ? ExperienceFlowManager.Instance 
                : FindAnyObjectByType<ExperienceFlowManager>();

            if (fm != null)
            {
                fm.MakeChoice(choice);
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
