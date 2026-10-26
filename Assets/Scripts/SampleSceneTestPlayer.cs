using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class SampleSceneTestPlayer : MonoBehaviour
{
    [SerializeField] private float movementSpeed = 2.5f;
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float gravity = -9.81f;

    private CharacterController characterController;
    private Transform playerCamera;
    private float verticalVelocity;
    private float pitch;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        characterController.height = 1.8f;
        characterController.radius = 0.2f;
        characterController.center = new Vector3(0f, 0.9f, 0f);

        var cameraObject = new GameObject("Player Camera");
        cameraObject.transform.SetParent(transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();
        playerCamera = cameraObject.transform;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
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

        var lookDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - lookDelta.y, -80f, 80f);
        playerCamera.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        transform.Rotate(Vector3.up, lookDelta.x);
    }
}
