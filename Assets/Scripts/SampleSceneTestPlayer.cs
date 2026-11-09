using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(CharacterController))]
public sealed class SampleSceneTestPlayer : MonoBehaviour
{
    [Header("Beweging & Kijken")]
    public bool disableMovement = false;
    [SerializeField] private float movementSpeed = 2.5f;
    [SerializeField] private float mouseSensitivity = 2.0f;
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
        if (characterController.height <= 0f)
        {
            characterController.height = 1.8f;
            characterController.radius = 0.25f;
            characterController.center = new Vector3(0f, 0.9f, 0f);
        }

        // Zoek of er al een camera is
        Camera existingCam = GetComponentInChildren<Camera>();
        if (existingCam != null)
        {
            camComponent = existingCam;
            playerCamera = existingCam.transform;
            // Behoud de door de gebruiker ingestelde ooghoogte
        }
        else
        {
            var cameraObject = new GameObject("Player Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.70f, 0f);

            camComponent = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();

            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;

            playerCamera = cameraObject.transform;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        // Toggle muiscursor met Escape (handig voor UI klikken in desktop testmodus)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        if (!disableMovement)
        {
            HandleMovement();
        }

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            HandleLook();
        }

        HandleGrabbing();
    }

    public void Teleport(Vector3 targetPos, Quaternion targetRot)
    {
        if (grabbedRigidbody != null)
        {
            grabbedRigidbody.useGravity = true;
            grabbedRigidbody = null;
        }

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        verticalVelocity = 0f;
        transform.position = targetPos;
        transform.rotation = targetRot;

        if (characterController != null)
        {
            characterController.enabled = true;
        }
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
        // Klik met linkermuisknop om object op te pakken
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = camComponent.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Cursor.lockState == CursorLockMode.None)
            {
                ray = camComponent.ScreenPointToRay(Input.mousePosition);
            }

            if (Physics.Raycast(ray, out RaycastHit hit, grabRange))
            {
                if (hit.rigidbody != null)
                {
                    grabbedRigidbody = hit.rigidbody;
                    grabbedRigidbody.useGravity = false;

                    var kit = grabbedRigidbody.GetComponent<Oncologie.Core.ScreeningKitInteraction>();
                    if (kit != null) kit.OnPickedUp();
                }
            }
        }

        // Laat los
        if (Input.GetMouseButtonUp(0) && grabbedRigidbody != null)
        {
            grabbedRigidbody.useGravity = true;
            grabbedRigidbody = null;
        }

        // Beweeg opgepakt object mee
        if (grabbedRigidbody != null)
        {
            Vector3 targetPos = playerCamera.position + playerCamera.forward * holdDistance;
            grabbedRigidbody.MovePosition(Vector3.Lerp(grabbedRigidbody.position, targetPos, Time.deltaTime * 15f));
        }
    }
}
