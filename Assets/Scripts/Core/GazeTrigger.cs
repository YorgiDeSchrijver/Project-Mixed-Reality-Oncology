using UnityEngine;
using UnityEngine.Events;

public class GazeTrigger : MonoBehaviour
{
    [Tooltip("Laat dit gerust leeg! Het script vindt de ingespawnde camera zelf.")]
    [SerializeField] private Transform playerCamera;
    [SerializeField] private float requiredGazeTime = 2.0f;
    [SerializeField] private float maxGazeDistance = 5.0f;
    [SerializeField] private bool triggerOnce = true;

    public UnityEvent onGazeCompleted;

    private float _currentGazeTime = 0f;
    private bool _hasTriggered = false;

    private void Update()
    {
        if (_hasTriggered && triggerOnce) return;

        // Zoek automatisch de ingespawnde camera op als het vakje leeg is
        if (playerCamera == null)
        {
            if (Camera.main != null) playerCamera = Camera.main.transform;
            else return;
        }

        Ray ray = new Ray(playerCamera.position, playerCamera.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxGazeDistance))
        {
            if (hit.transform == transform)
            {
                _currentGazeTime += Time.deltaTime;
                if (_currentGazeTime >= requiredGazeTime)
                {
                    _hasTriggered = true;
                    Debug.Log($"[GazeTrigger] 2 seconden gekeken naar {gameObject.name}!");
                    onGazeCompleted?.Invoke();
                }
                return;
            }
        }

        _currentGazeTime = 0f;
    }
}