using Oncologie.IRL;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Desktop-testspeler (vervangt de VR-bril tijdens het ontwikkelen) met realistische lichaamslengte.
/// WASD = lopen · muis = kijken · klik = pakken/loslaten of drukken · E/rechtermuis = gebruiken ·
/// scrollwiel = voorwerp dichter/verder · Esc = cursor vrijgeven.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class SampleSceneTestPlayer : MonoBehaviour
{
    [Header("Lichaam (gemiddelde volwassene)")]
    [SerializeField] private float bodyHeight = 1.75f;
    [SerializeField] private float eyeHeight = 1.62f;

    [Header("Beweging & Kijken")]
    public bool disableMovement = false;
    [SerializeField] private float movementSpeed = 1.6f;
    [SerializeField] private float mouseSensitivity = 2.0f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Interactie")]
    [SerializeField] private float reach = 2.2f;
    [SerializeField] private float holdDistance = 0.6f;
    [SerializeField] private Vector2 holdRange = new Vector2(0.3f, 1.2f);

    private CharacterController _cc;
    private Transform _camT;
    private Camera _cam;
    private float _vy, _pitch;
    private InteractableItem _held;
    private string _lookHint = "";

    private void Awake()
    {
        transform.localScale = Vector3.one; // schaal ≠ 1 zou de ooghoogte vervormen
        _cc = GetComponent<CharacterController>();
        _cc.height = bodyHeight;
        _cc.radius = 0.22f;
        _cc.center = new Vector3(0f, bodyHeight * 0.5f, 0f);
        _cc.stepOffset = 0.3f;
        _cc.slopeLimit = 50f;
        _cc.skinWidth = 0.03f;
        _cc.minMoveDistance = 0.001f;

        _cam = GetComponentInChildren<Camera>(true);
        if (_cam == null)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.SetParent(transform, false);
            _cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
            go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        }
        _camT = _cam.transform;
        _camT.localPosition = new Vector3(0f, eyeHeight, 0f);
        _cam.nearClipPlane = 0.05f;
        _cam.fieldOfView = 70f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
        }
        else if (Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0) && GUIUtility.hotControl == 0)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            return;
        }

        if (!disableMovement) Move();
        if (Cursor.lockState == CursorLockMode.Locked) Look();
        Interact();
    }

    public void Teleport(Vector3 pos, Quaternion rot)
    {
        DropHeld();
        if (_cc == null) _cc = GetComponent<CharacterController>();
        _cc.enabled = false;
        _vy = 0f;
        transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, rot.eulerAngles.y, 0f));
        _cc.enabled = true;
    }

    private void Move()
    {
        var input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        Vector3 dir = transform.right * input.x + transform.forward * input.y;
        if (dir.sqrMagnitude > 1f) dir.Normalize();
        if (_cc.isGrounded && _vy < 0f) _vy = -2f;
        _vy += gravity * Time.deltaTime;
        Vector3 v = dir * movementSpeed; v.y = _vy;
        _cc.Move(v * Time.deltaTime);
    }

    private void Look()
    {
        var d = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * mouseSensitivity;
        _pitch = Mathf.Clamp(_pitch - d.y, -85f, 85f);
        _camT.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        transform.Rotate(Vector3.up, d.x);
    }

    private void Interact()
    {
        if (_held != null && !_held.IsHeld) { SetIgnore(_held, false); _held = null; } // door een script losgelaten (bv. afgegeven)

        Ray ray = Cursor.lockState == CursorLockMode.Locked ? new Ray(_camT.position, _camT.forward) : _cam.ScreenPointToRay(Input.mousePosition);
        InteractableItem lookItem = null; ClickableObject lookClick = null;
        if (_held == null && Physics.Raycast(ray, out var hit, reach, ~0, QueryTriggerInteraction.Ignore))
        {
            lookItem = hit.collider.GetComponentInParent<InteractableItem>();
            if (lookItem != null && !lookItem.Interactable) lookItem = null;
            lookClick = hit.collider.GetComponentInParent<ClickableObject>();
            if (lookClick != null && !lookClick.clickable) lookClick = null;
        }

        if (_held != null)
        {
            holdDistance = Mathf.Clamp(holdDistance + Input.mouseScrollDelta.y * 0.05f, holdRange.x, holdRange.y);
            Vector3 target = _camT.position + _camT.forward * holdDistance;
            Quaternion rot = _camT.rotation * Quaternion.Euler(_held.desktopHoldEuler);
            var t = _held.transform;
            t.SetPositionAndRotation(Vector3.Lerp(t.position, target, Time.deltaTime * 18f), Quaternion.Slerp(t.rotation, rot, Time.deltaTime * 14f));

            if (Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(1)) _held.Activate();
            if (Input.GetMouseButtonDown(0)) DropHeld();

            _lookHint = _held == null ? "" :
                $"[klik] {_held.displayName} loslaten" + (string.IsNullOrEmpty(_held.useVerb) ? "" : $"   ·   [E] {_held.useVerb}") + "   ·   [scroll] dichter/verder";
            return;
        }

        if (lookItem != null)
        {
            _lookHint = $"[klik] {lookItem.displayName} pakken";
            if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.Locked)
            {
                _held = lookItem;
                SetIgnore(_held, true);
                _held.Freeze();
                _held.Grab();
                holdDistance = 0.6f;
            }
        }
        else if (lookClick != null)
        {
            _lookHint = $"[klik] {lookClick.hint}";
            if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.Locked) lookClick.Click();
        }
        else _lookHint = "";
    }

    private void DropHeld()
    {
        if (_held == null) return;
        var h = _held;
        _held = null;
        SetIgnore(h, false);
        h.Release();
    }

    private void SetIgnore(InteractableItem item, bool ignore)
    {
        foreach (var c in item.GetComponentsInChildren<Collider>(true)) Physics.IgnoreCollision(_cc, c, ignore);
    }

    private void OnGUI()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;
        float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
        GUI.color = new Color(1, 1, 1, 0.8f);
        GUI.DrawTexture(new Rect(cx - 2, cy - 2, 4, 4), Texture2D.whiteTexture);
        if (!string.IsNullOrEmpty(_lookHint))
        {
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15 };
            GUI.color = Color.black; GUI.Label(new Rect(cx - 299, cy + 19, 600, 24), _lookHint, style);
            GUI.color = Color.white; GUI.Label(new Rect(cx - 300, cy + 18, 600, 24), _lookHint, style);
        }
        GUI.color = Color.white;
    }
}
