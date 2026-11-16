using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class EnvelopeGiveInteraction : MonoBehaviour
{
    [Header("Referenties")]
    [SerializeField] private ObjectGlowPulse glowScript;

    [Header("Instellingen")]
    [Tooltip("Naam van het GeefZone-object óf de Tag van het object.")]
    [SerializeField] private string giveZoneTagOrName = "GiveZone";

    [Header("Events")]
    public UnityEvent onHoverEnter;
    public UnityEvent onHoverExit;
    public UnityEvent onEnvelopeDelivered;

    private bool _isDelivered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (_isDelivered) return;

        // Check of we de GeefZone raken (werkt nu op Tag ÉN op GameObject-naam, zonder te crashen!)
        if (IsGiveZone(other.gameObject))
        {
            _isDelivered = true;
            if (glowScript != null) glowScript.SetGlowing(false);
            Debug.Log("[EnvelopeGiveInteraction] Succes! Envelop is afgeleverd in de GeefZone!");
            onEnvelopeDelivered?.Invoke();
        }
    }

    private bool IsGiveZone(GameObject obj)
    {
        // Check eerst of de naam van het object overeenkomt (bijv. "GiveZone" of "DropZone_Dad")
        if (obj.name.Contains(giveZoneTagOrName) || obj.name.Contains("DropZone"))
            return true;

        // Check veilig de Tag zonder dat Unity crasht als de Tag niet bestaat
        try
        {
            return obj.CompareTag(giveZoneTagOrName);
        }
        catch
        {
            return false;
        }
    }

    // Wordt aangeroepen door je muis/camera (of later VR-controller) voor het gloei-effect
    public void SetHover(bool isHovering)
    {
        if (_isDelivered) return;

        if (glowScript != null) glowScript.SetGlowing(isHovering);

        if (isHovering) onHoverEnter?.Invoke();
        else onHoverExit?.Invoke();
    }

    public void ResetDeliveryState()
    {
        _isDelivered = false;
    }
}