using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace Oncologie.IRL
{
    /// <summary>
    /// Kiest automatisch de speler: met een actieve headset de XR Origin, anders de desktop-testspeler
    /// (met realistische ooghoogte). Zo kan je testen zonder bril en werkt dezelfde scène in VR.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class PlayerRigSelector : MonoBehaviour
    {
        public GameObject desktopPlayer;
        public GameObject xrRig;
        public Transform startPoint;
        [Tooltip("Forceer de desktop-testspeler, ook als er een headset aangesloten is.")]
        public bool forceDesktop;

        public static bool IsVR { get; private set; }
        public static GameObject ActiveRig { get; private set; }

        private static readonly string[] LocomotionTypes = { "MoveProvider", "TurnProvider", "Teleportation", "Climb" };

        private void Awake()
        {
            IsVR = !forceDesktop && XRSettings.isDeviceActive && xrRig != null;
            if (xrRig != null) xrRig.SetActive(IsVR);
            if (desktopPlayer != null) desktopPlayer.SetActive(!IsVR);
            ActiveRig = IsVR ? xrRig : desktopPlayer;

            if (startPoint != null && ActiveRig != null)
            {
                var tp = ActiveRig.GetComponent<SampleSceneTestPlayer>();
                if (tp != null) tp.Teleport(startPoint.position, startPoint.rotation);
                else ActiveRig.transform.SetPositionAndRotation(startPoint.position, startPoint.rotation);
            }

            foreach (var cam in ActiveRig != null ? ActiveRig.GetComponentsInChildren<Camera>(true) : new Camera[0])
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = true;
            }
        }

        /// <summary>Zet bewegen (lopen/draaien/teleporteren) aan of uit, bv. tijdens de transitie.</summary>
        public static void SetMovementLocked(bool locked)
        {
            if (ActiveRig == null) return;
            var tp = ActiveRig.GetComponent<SampleSceneTestPlayer>();
            if (tp != null) tp.disableMovement = locked;
            foreach (var mb in ActiveRig.GetComponentsInChildren<MonoBehaviour>(true))
            {
                string n = mb.GetType().Name;
                foreach (var t in LocomotionTypes)
                    if (n.Contains(t)) { mb.enabled = !locked; break; }
            }
        }
    }
}
