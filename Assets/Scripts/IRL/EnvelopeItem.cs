using System;
using System.Collections;
using Oncologie.Core;
using UnityEngine;

namespace Oncologie.IRL
{
    /// <summary>
    /// De envelop van het bevolkingsonderzoek: hangt op de koelkast, kan vastgepakt worden,
    /// geopend (brief lezen met trigger / E) en afgegeven in een DropZone (Luc's hand of de vuilbak).
    /// </summary>
    public class EnvelopeItem : InteractableItem
    {
        public static EnvelopeItem Current { get; private set; }

        [Tooltip("World-space brief die verschijnt bij 'gebruiken'.")]
        public GameObject letterView;
        public Highlighter highlighter;
        [Tooltip("Afstand van de brief voor de ogen (m).")]
        public float letterDistance = 0.55f;

        public bool HasBeenGrabbed { get; private set; }
        public bool LetterOpen { get; private set; }
        public bool Delivered { get; private set; }

        public event Action FirstGrabbed;
        public event Action LetterOpened;
        public event Action<ScreeningChoice> DeliveredTo;

        protected override void Awake()
        {
            base.Awake();
            Current = this;
            if (letterView != null)
            {
                letterView.transform.SetParent(null, true);
                letterView.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            if (letterView != null) Destroy(letterView);
        }

        protected override void OnGrab()
        {
            if (highlighter != null) highlighter.Set(false);
            if (!HasBeenGrabbed)
            {
                HasBeenGrabbed = true;
                FirstGrabbed?.Invoke();
            }
        }

        protected override void OnRelease() => SetLetter(false);
        protected override void OnActivate() => SetLetter(!LetterOpen);

        public void SetLetter(bool open)
        {
            if (letterView == null || LetterOpen == open) return;
            LetterOpen = open;
            letterView.SetActive(open);
            HintDisplay.Suppress(open);
            if (open)
            {
                PlaceLetter(true);
                LetterOpened?.Invoke();
            }
        }

        private void LateUpdate()
        {
            if (LetterOpen) PlaceLetter(false);
        }

        private void PlaceLetter(bool snap)
        {
            var cam = WorldUI.MainCamera();
            if (cam == null) return;
            Vector3 fwd = cam.transform.forward; fwd.y = Mathf.Clamp(fwd.y, -0.5f, 0.3f); fwd.Normalize();
            Vector3 target = cam.transform.position + fwd * letterDistance + Vector3.down * 0.06f;
            Quaternion rot = Quaternion.LookRotation(target - cam.transform.position, Vector3.up);
            float k = snap ? 1f : Time.deltaTime * 8f;
            letterView.transform.SetPositionAndRotation(Vector3.Lerp(letterView.transform.position, target, k),
                                                        Quaternion.Slerp(letterView.transform.rotation, rot, k));
        }

        public void ShowHint(bool on)
        {
            if (highlighter != null) highlighter.Set(on && !IsHeld && !Delivered);
        }

        public void DeliverTo(DropZone zone)
        {
            if (Delivered || zone == null) return;
            Delivered = true;
            SetLetter(false);
            ForceRelease();
            SetInteractable(false);
            Freeze();
            if (highlighter != null) highlighter.Set(false);
            StartCoroutine(MoveInto(zone));
            DeliveredTo?.Invoke(zone.choice);
        }

        /// <summary>Script-gestuurd afleveren (bv. als de speler niets doet en Luc de brief zelf weggooit).</summary>
        public void AutoDeliver(DropZone zone, float delay = 0f) => StartCoroutine(AutoRoutine(zone, delay));

        private IEnumerator AutoRoutine(DropZone zone, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            DeliverTo(zone);
        }

        private IEnumerator MoveInto(DropZone zone)
        {
            Transform snap = zone.snapPoint != null ? zone.snapPoint : zone.transform;
            Vector3 p0 = transform.position; Quaternion r0 = transform.rotation;
            float t = 0, d = 0.6f;
            while (t < d)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0, 1, t / d);
                transform.SetPositionAndRotation(Vector3.Lerp(p0, snap.position, k), Quaternion.Slerp(r0, snap.rotation, k));
                yield return null;
            }
            transform.SetParent(snap, true);
            transform.SetPositionAndRotation(snap.position, snap.rotation);
            if (zone.hideOnArrive) gameObject.SetActive(false);
        }
    }
}
