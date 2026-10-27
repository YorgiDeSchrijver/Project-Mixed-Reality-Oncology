using UnityEngine;
using UnityEngine.Rendering.Universal; // Nodig om Post-Processing voor Zwart-Wit aan te zetten

[RequireComponent(typeof(CharacterController))]
public sealed class SampleSceneTestPlayer : MonoBehaviour
{
    [Header("Beweging & Kijken")]
    [SerializeField] private float movementSpeed = 2.5f;
    [SerializeField] private float mouseSensitivity = 2.0f; // Iets verhoogd voor soepel testen
    [SerializeField] private float gravity = -9.81f;

    [Header("Interactie (Linkermuisknop om op te pakken)")]
    [SerializeField] private float grabRange = 4f;
    [SerializeField] private float holdDistance = 1.5f;

    private CharacterController characterController;
    private Transform playerCamera;
    private Camera camComponent;
    private float verticalVelocity;
    private float pitch;
    private Rigidbody grabbedRigidbody;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        characterController.height = 1.8f;
        characterController.radius = 0.2f;
        characterController.center = new Vector3(0f, 0.9f, 0f);

        var cameraObject = new GameObject("Player Camera");
        cameraObject.tag = "MainCamera"; // CRUCIAAL: Zo vindt GazeTrigger jouw Camera.main!
        cameraObject.transform.SetParent(transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);

        camComponent = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();

        // Zet Post-Processing automatisch aan op deze nieuwe camera (voor GreyscaleTransition)
        var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
        cameraData.renderPostProcessing = true;

        playerCamera = cameraObject.transform;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleMovement();
        HandleLook();
        HandleGrabbing();
    }

    private void HandleMovement()
    {
        var moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        var movement = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;

        if (characterController.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        if (characterController.isGrounded && Input.GetButtonDown("Jump"))
            verticalVelocity = 4f;

        verticalVelocity += gravity * Time.deltaTime;
        movement.y = verticalVelocity;
        characterController.Move(movement * movementSpeed * Time.deltaTime);
    }

    private void HandleLook()
    {
        var lookDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - lookDelta.y, -80f, 80f);
        playerCamera.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        transform.Rotate(Vector3.up, lookDelta.x);
    }

    private void HandleGrabbing()
    {
        // Klik met linkermuisknop om de envelop of zaklamp op te pakken
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = new Ray(playerCamera.position, playerCamera.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, grabRange))
            {
                if (hit.rigidbody != null)
                {
                    grabbedRigidbody = hit.rigidbody;
                    grabbedRigidbody.useGravity = false;
                }
            }
        }

        // Laat linkermuisknop los om het object los te laten
        if (Input.GetMouseButtonUp(0) && grabbedRigidbody != null)
        {
            grabbedRigidbody = null;
        }

        // Houd het opgepakte object recht voor je camera
        if (grabbedRigidbody != null)
        {
            Vector3 targetPos = playerCamera.position + playerCamera.forward * holdDistance;
            grabbedRigidbody.MovePosition(Vector3.Lerp(grabbedRigidbody.position, targetPos, Time.deltaTime * 15f));
        }
    }
}