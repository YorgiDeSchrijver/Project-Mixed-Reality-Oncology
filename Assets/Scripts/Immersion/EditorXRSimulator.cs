using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Zet de XR Interaction Simulator aan in de editor als er geen headset actief is,
/// zodat je met muis en toetsenbord kan rondkijken en testen.
/// Werkt alleen in de Unity Editor; in een build (of met een headset via Link) gebeurt er niets.
/// Onafhankelijk van de projectbrede instelling "Automatically Instantiate Simulator Prefab".
/// </summary>
public class EditorXRSimulator : MonoBehaviour
{
    [Tooltip("Meestal: Samples/XR Interaction Toolkit/.../XR Interaction Simulator.prefab")]
    public GameObject simulatorPrefab;

#if UNITY_EDITOR
    void Awake()
    {
        if (simulatorPrefab == null || XRSettings.isDeviceActive) return;
        if (GameObject.Find(simulatorPrefab.name) != null) return;   // staat er al (bv. via de projectinstelling)
        var sim = Instantiate(simulatorPrefab);
        sim.name = simulatorPrefab.name;
    }
#endif
}
