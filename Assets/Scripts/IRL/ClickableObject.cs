using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Oncologie.IRL
{
    /// <summary>
    /// Iets waar je op kan "drukken": VR via XRSimpleInteractable (grip/trigger op het object),
    /// desktop via klikken met de testspeler.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ClickableObject : MonoBehaviour
    {
        public string hint = "gebruiken";
        public bool clickable = true;
        public UnityEvent onClicked = new UnityEvent();

        private void Awake()
        {
            var simple = GetComponent<XRSimpleInteractable>();
            if (simple != null) simple.selectEntered.AddListener(_ => Click());
        }

        public void SetClickable(bool value)
        {
            clickable = value;
            var simple = GetComponent<XRSimpleInteractable>();
            if (simple != null) simple.enabled = value;
        }

        public void Click()
        {
            if (!clickable) return;
            onClicked.Invoke();
        }
    }
}
