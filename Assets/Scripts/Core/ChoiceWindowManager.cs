using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class ChoiceWindowManager : MonoBehaviour
{
    [Header("Instellingen Keuzevenster (Scène 4)")]
    [Tooltip("Duur van het zingen waarin de speler kan ingrijpen (standaard 20s).")]
    [SerializeField] private float choiceDuration = 20f;

    [Header("Events voor Scène-overgangen")]
    public UnityEvent onBranchB_EnvelopeGiven; // Speler geeft envelop -> Scène 5B
    public UnityEvent onBranchA_DidNothing;    // Speler doet niets -> Scène 5A

    private bool _choiceMade = false;
    private Coroutine _timerCoroutine;

    // Start dit zodra Mama en Luc beginnen te zingen in Scène 4
    public void StartChoiceWindow()
    {
        _choiceMade = false;
        if (_timerCoroutine != null) StopCoroutine(_timerCoroutine);
        _timerCoroutine = StartCoroutine(ChoiceTimerRoutine());
    }

    private IEnumerator ChoiceTimerRoutine()
    {
        float elapsed = 0f;

        while (elapsed < choiceDuration && !_choiceMade)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Als de tijd om is en de envelop niet is gegeven -> Tak A (Grijs)
        if (!_choiceMade)
        {
            _choiceMade = true;
            onBranchA_DidNothing?.Invoke();
        }
    }

    // Wordt aangeroepen door de DropZone op de tafel / Luc's hand
    public void ConfirmEnvelopeGiven()
    {
        if (_choiceMade) return;

        _choiceMade = true;
        if (_timerCoroutine != null) StopCoroutine(_timerCoroutine);
        onBranchB_EnvelopeGiven?.Invoke();
    }
}