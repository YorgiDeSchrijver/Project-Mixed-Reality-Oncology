using UnityEngine;
using UnityEngine.Events;

public class GazeTrigger : MonoBehaviour
{
    [SerializeField] private Transform playerCamera;
    [SerializeField] private float requiredGazeTime = 2.0f;
    [SerializeField] private float maxGazeDistance = 5.0f;
    [SerializeField] private bool triggerOnce = true;

    public UnityEvent onGazeCompleted;

    private float _currentGazeTime = 0f;
    private bool _hasTriggered = false;

    private void Start()
    {
        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }
    }

    private void Update()
    {
        if (_hasTriggered && triggerOnce) return;
        if (playerCamera == null) return;

        Ray ray = new Ray(playerCamera.position, playerCamera.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxGazeDistance))
        {
            if (hit.transform == transform)
            {
                _currentGazeTime += Time.deltaTime;
                if (_currentGazeTime >= requiredGazeTime)
                {
                    _hasTriggered = true;
                    onGazeCompleted?.Invoke();
                }
                return;
            }
        }

        _currentGazeTime = 0f;
    }
}