using UnityEngine;

namespace Oncologie.Dialogue
{
    /// <summary>
    /// Zorgt dat het ondertiteling-canvas in VR en desktop altijd comfortabel
    /// op ooghoogte voor de speler zweeft, zachtjes meebeweegt en naar de camera kijkt.
    /// </summary>
    public class VRSubtitleFollower : MonoBehaviour
    {
        [Header("Afstand & Positie")]
        [SerializeField] private float distance = 1.75f;
        [SerializeField] private float heightOffset = -0.32f;
        [SerializeField] private float followSpeed = 6.0f;

        private Transform _camTransform;

        private void Start()
        {
            FindCamera();
        }

        private void FindCamera()
        {
            if (Camera.main != null)
            {
                _camTransform = Camera.main.transform;
            }
        }

        private void LateUpdate()
        {
            if (_camTransform == null)
            {
                FindCamera();
                if (_camTransform == null) return;
            }

            // Doelpositie recht voor de camera, lichtjes onder ooghoogte zodat de scène vrij blijft
            Vector3 forward = _camTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.001f) forward.Normalize();
            else forward = _camTransform.forward;

            Vector3 targetPos = _camTransform.position + forward * distance + Vector3.up * heightOffset;
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSpeed);

            // Kijk naar de camera
            Vector3 lookDir = transform.position - _camTransform.position;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * followSpeed * 1.5f);
            }
        }
    }
}
