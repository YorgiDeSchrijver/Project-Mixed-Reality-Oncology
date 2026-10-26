using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class EnvelopeGiveInteraction : MonoBehaviour
{
    [Header("Referenties")]
    [Tooltip("Sleep hier het ObjectGlowPulse script van de envelop in.")]
    [SerializeField] private ObjectGlowPulse glowScript;

    [Tooltip("Tag van de speler-hand of controller (bijv. 'PlayerHand') voor hover-effect.")]
    [SerializeField] private string handTag = "PlayerHand";

    [Tooltip("Tag van de trigger-zone op tafel of bij Luc's hand (bijv. 'GiveZone').")]
    [SerializeField] private string giveZoneTag = "GiveZone";

    [Header("Events")]
    public UnityEvent onHoverEnter;
    public UnityEvent onHoverExit;
    public UnityEvent onEnvelopeDelivered; // Koppel dit aan Scène 2 of ChoiceWindowManager.ConfirmEnvelopeGiven()

    private bool _isDelivered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (_isDelivered) return;

        // 1. Hover-effect wanneer de hand van de speler de envelop raakt
        if (other.CompareTag(handTag))
        {
            if (glowScript != null) glowScript.SetGlowing(true);
            onHoverEnter?.Invoke();
        }

        // 2. Aflever-check wanneer de envelop in de zone van Luc/tafel komt
        if (other.CompareTag(giveZoneTag))
        {
            _isDelivered = true;
            if (glowScript != null) glowScript.SetGlowing(false);
            onEnvelopeDelivered?.Invoke();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (_isDelivered) return;

        if (other.CompareTag(handTag))
        {
            if (glowScript != null) glowScript.SetGlowing(false);
            onHoverExit?.Invoke();
        }
    }

    // Handig om de envelop te resetten als je van Scène 1 naar Scène 4 gaat
    public void ResetDeliveryState()
    {
        _isDelivered = false;
    }
}