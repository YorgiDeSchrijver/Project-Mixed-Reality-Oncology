using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Oncologie.IRL
{
    /// <summary>
    /// Een voorwerp dat je kan vastpakken: in VR via XRGrabInteractable (grip = pakken, trigger = gebruiken),
    /// op desktop via de testspeler (klik = pakken/loslaten, E of rechtermuis = gebruiken).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class InteractableItem : MonoBehaviour
    {
        [Tooltip("Naam die de desktop-testspeler toont, bv. 'envelop'.")]
        public string displayName = "voorwerp";
        [Tooltip("Wat 'gebruiken' doet, bv. 'brief lezen'. Leeg = geen gebruik-actie.")]
        public string useVerb = "";
        [Tooltip("Rotatie t.o.v. de camera wanneer de desktopspeler het vasthoudt.")]
        public Vector3 desktopHoldEuler = Vector3.zero;
        [Tooltip("Valt het voorwerp na loslaten (zwaartekracht)?")]
        public bool dropWithPhysics = true;

        [SerializeField] private bool interactable = true;

        public UnityEvent onGrabbed = new UnityEvent();
        public UnityEvent onReleased = new UnityEvent();
        public UnityEvent onActivated = new UnityEvent();

        public bool IsHeld { get; private set; }
        public bool Interactable => interactable;
        public Rigidbody Body { get; private set; }

        private XRGrabInteractable _grab;

        protected virtual void Awake()
        {
            Body = GetComponent<Rigidbody>();
            _grab = GetComponent<XRGrabInteractable>();
            if (_grab != null)
            {
                _grab.selectEntered.AddListener(_ => Grab());
                _grab.selectExited.AddListener(_ => Release());
                _grab.activated.AddListener(_ => Activate());
                _grab.enabled = interactable;
            }
        }

        public void SetInteractable(bool value)
        {
            interactable = value;
            if (!value && IsHeld) ForceRelease();
            if (_grab != null) _grab.enabled = value;
        }

        public void Grab()
        {
            if (!interactable || IsHeld) return;
            IsHeld = true;
            OnGrab();
            onGrabbed.Invoke();
        }

        public void Release()
        {
            if (!IsHeld) return;
            IsHeld = false;
            OnRelease();
            onReleased.Invoke();
            if (dropWithPhysics && gameObject.activeInHierarchy) StartCoroutine(EnablePhysicsNextStep());
        }

        public void Activate()
        {
            if (!IsHeld) return;
            OnActivate();
            onActivated.Invoke();
        }

        /// <summary>Laat het voorwerp los, ongeacht wie het vasthoudt (VR-hand of testspeler).</summary>
        public void ForceRelease()
        {
            if (_grab != null && _grab.isSelected && _grab.interactionManager != null)
                _grab.interactionManager.CancelInteractableSelection((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)_grab);
            bool keepPhysics = dropWithPhysics;
            dropWithPhysics = false;
            Release();
            dropWithPhysics = keepPhysics;
        }

        /// <summary>Zet het voorwerp vast (bv. als een script het verplaatst).</summary>
        public void Freeze()
        {
            StopAllCoroutines();
            if (Body == null) return;
            if (!Body.isKinematic)
            {
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }
            Body.isKinematic = true;
            Body.useGravity = false;
        }

        private IEnumerator EnablePhysicsNextStep()
        {
            // XRGrabInteractable zet zijn eigen kinematic-status terug bij loslaten; daarna pas fysica aanzetten.
            yield return new WaitForFixedUpdate();
            if (IsHeld || Body == null) yield break;
            Body.isKinematic = false;
            Body.useGravity = true;
        }

        protected virtual void OnGrab() { }
        protected virtual void OnRelease() { }
        protected virtual void OnActivate() { }
    }
}
