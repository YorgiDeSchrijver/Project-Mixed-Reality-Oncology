using UnityEngine;

public class DevDesktopTester : MonoBehaviour
{
    [Header("Kijk-instellingen (Houd Rechtermuisknop ingedrukt)")]
    [SerializeField] private float lookSpeed = 3f;
    private float _pitch = 0f;
    private float _yaw = 0f;

    [Header("Vastpak-instellingen (Linkermuisknop)")]
    [SerializeField] private float grabDistance = 2f;
    private Transform _grabbedObject;

    private void Start()
    {
        Vector3 rot = transform.eulerAngles;
        _pitch = rot.x;
        _yaw = rot.y;
    }

    private void Update()
    {
        // 1. Rondkijken met Rechtermuisknop
        if (Input.GetMouseButton(1))
        {
            _yaw += lookSpeed * Input.GetAxis("Mouse X");
            _pitch -= lookSpeed * Input.GetAxis("Mouse Y");
            _pitch = Mathf.Clamp(_pitch, -80f, 80f);
            transform.eulerAngles = new Vector3(_pitch, _yaw, 0f);
        }

        // 2. Object oppakken met Linkermuisknop
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 10f))
            {
                if (hit.collider.GetComponent<Rigidbody>() != null)
                {
                    _grabbedObject = hit.transform;
                }
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            _grabbedObject = null;
        }

        // Beweeg het opgepakte object mee met de muis
        if (_grabbedObject != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            _grabbedObject.position = ray.GetPoint(grabDistance);
        }
    }
}
