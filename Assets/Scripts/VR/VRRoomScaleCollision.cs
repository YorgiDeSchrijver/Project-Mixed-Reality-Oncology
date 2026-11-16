using UnityEngine;
using Unity.XR.CoreUtils;

namespace Oncologie.VR
{
    /// <summary>
    /// Zorgt voor robuuste fysieke VR botsingen en voorkomt dat de speler door muren wandelt
    /// of zweeft in de darm.
    /// 1. Synchroniseert de CharacterController capsule continu met de HMD (hoofd/camera) positie en lengte.
    /// 2. Room-scale fysiek wandelen: als de speler fysiek een stap zet in zijn kamer, beweegt de capsule
    ///    via CharacterController.Move(). Als een muur de doorgang blokkeert, wordt de XR Origin
    ///    teruggeduwd zodat de camera niet door de muur dringt.
    /// 3. Grondklemming: zorgt dat de speler stevig op de darmvloer/keukenvloer blijft staan en niet zweeft.
    /// </summary>
    [RequireComponent(typeof(XROrigin))]
    [RequireComponent(typeof(CharacterController))]
    public class VRRoomScaleCollision : MonoBehaviour
    {
        [Header("Capsule Grenzen")]
        [Tooltip("Minimale hoogte van de capsule (bijv. bukken).")]
        [SerializeField] private float minHeight = 0.8f;
        [Tooltip("Maximale hoogte van de capsule (bijv. lange speler).")]
        [SerializeField] private float maxHeight = 2.2f;

        [Header("Fysica & Grondklemming")]
        [Tooltip("Extra neerwaartse klemkracht op de vloer zodat de speler in gebogen darmbuizen niet zweeft.")]
        [SerializeField] private float groundStickForce = -4.0f;
        [Tooltip("Layers die gelden als vaste muren en vloeren.")]
        [SerializeField] private LayerMask collisionLayers = ~0;

        private XROrigin _xrOrigin;
        private CharacterController _characterController;
        private Camera _xrCamera;
        private Vector3 _lastCameraPosition;
        private bool _isInitialized = false;

        private void Awake()
        {
            _xrOrigin = GetComponent<XROrigin>();
            _characterController = GetComponent<CharacterController>();
        }

        private void Start()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_xrOrigin == null) _xrOrigin = GetComponent<XROrigin>();
            if (_characterController == null) _characterController = GetComponent<CharacterController>();

            if (_xrOrigin != null && _xrOrigin.Camera != null)
            {
                _xrCamera = _xrOrigin.Camera;
                _lastCameraPosition = _xrCamera.transform.position;
                _isInitialized = true;
                UpdateCapsuleDimensions();
            }
        }

        private void Update()
        {
            if (!_isInitialized || _xrCamera == null)
            {
                Initialize();
                if (!_isInitialized) return;
            }

            // 1. Synchroniseer capsulehoogte en middelpunt met het hoofd
            UpdateCapsuleDimensions();

            // 2. Fysieke room-scale verplaatsing afhandelen met botsingsdetectie
            ProcessRoomScaleMovement();

            // 3. Grondklemming toepassen als de speler niet al via gravity valt
            ApplyGroundStick();
        }

        private void UpdateCapsuleDimensions()
        {
            if (_xrOrigin == null || _characterController == null || _xrCamera == null) return;

            // Camera hoogte in de lokale ruimte van de origin
            float cameraHeight = Mathf.Clamp(_xrCamera.transform.localPosition.y, minHeight, maxHeight);
            _characterController.height = cameraHeight;

            // Capsule center afstemmen op lokale X en Z van de camera, met Y op halve hoogte
            Vector3 center = new Vector3(
                _xrCamera.transform.localPosition.x,
                (cameraHeight * 0.5f) + _characterController.skinWidth,
                _xrCamera.transform.localPosition.z
            );
            _characterController.center = center;
        }

        private void ProcessRoomScaleMovement()
        {
            if (_characterController == null || !_characterController.enabled) return;

            Vector3 currentCamPos = _xrCamera.transform.position;
            Vector3 cameraDelta = currentCamPos - _lastCameraPosition;

            // Als de verplaatsing in 1 frame groter is dan 1.5 meter, is het een teleportatie (geen fysieke stap)
            if (cameraDelta.sqrMagnitude > 2.25f)
            {
                _lastCameraPosition = currentCamPos;
                return;
            }

            Vector3 horizontalDelta = new Vector3(cameraDelta.x, 0f, cameraDelta.z);

            // Als de speler in zijn kamer fysiek meer dan 0.5 mm beweegt
            if (horizontalDelta.sqrMagnitude > 0.0000025f)
            {
                Vector3 originPosBefore = transform.position;

                // Voer de fysieke beweging uit via de CharacterController zodat PhysX muren respecteert
                _characterController.Move(horizontalDelta);

                // Bereken of we tegen een muur stootten
                Vector3 actualMoved = transform.position - originPosBefore;
                Vector3 blockedDistance = horizontalDelta - actualMoved;

                // Als een muur de doorgang blokkeerde, corrigeer de Origin zodat het hoofd niet door de muur dringt
                if (blockedDistance.sqrMagnitude > 0.0000025f)
                {
                    transform.position -= blockedDistance;
                }
            }

            _lastCameraPosition = _xrCamera.transform.position;
        }

        private void ApplyGroundStick()
        {
            if (_characterController == null || !_characterController.enabled) return;

            // Als we op de grond staan (of net iets erboven in de gebogen darmbuis),
            // druk een kleine zachte klemkracht omlaag zodat we niet gaan zweven bij het lopen.
            if (_characterController.isGrounded)
            {
                _characterController.Move(new Vector3(0f, groundStickForce * Time.deltaTime, 0f));
            }
        }
    }
}
