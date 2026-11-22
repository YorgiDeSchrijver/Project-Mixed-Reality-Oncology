using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;

/// <summary>
/// Hartslag in de darm: geluid, controller-vibratie en een puls naar ColonAtmosphere (wand klopt mee, rode gloed).
/// Het ritme volgt de severity: rustig en regelmatig bij gezond, sneller en onregelmatiger
/// (overgeslagen slagen, extra vroege slagen) als het kritiek wordt.
/// Zonder eigen AudioClip wordt een korte "lub-dub" één keer procedureel aangemaakt (geen asset nodig).
/// Zet dit op dezelfde GameObject als ColonAtmosphere, samen met een AudioSource.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ColonHeartbeat : MonoBehaviour
{
    [Header("Referenties")]
    public ColonAtmosphere atmosphere;
    [Tooltip("Eén hartslag (lub-dub). Leeg = procedureel geluid.")]
    public AudioClip beatClip;

    [Header("Ritme")]
    public float bpmHealthy = 62f;
    public float bpmSick = 108f;
    [Tooltip("Hoeveel het ritme willekeurig verschuift bij severity 1 (0.2 = 20% van een slag)")]
    [Range(0f, 0.5f)] public float jitterSick = 0.18f;
    [Tooltip("Kans per slag op een overgeslagen slag bij severity 1")]
    [Range(0f, 0.5f)] public float skipChanceSick = 0.12f;
    [Tooltip("Kans per slag op een extra vroege slag bij severity 1")]
    [Range(0f, 0.5f)] public float extraBeatChanceSick = 0.1f;
    [Tooltip("Onder deze severity blijft het ritme perfect regelmatig")]
    [Range(0f, 1f)] public float irregularFrom = 0.35f;

    [Header("Volume")]
    [Range(0f, 1f)] public float volumeHealthy = 0.35f;
    [Range(0f, 1f)] public float volumeSick = 0.9f;

    [Header("Controller-vibratie")]
    public bool haptics = true;
    [Range(0f, 1f)] public float hapticHealthy = 0.08f;
    [Range(0f, 1f)] public float hapticSick = 0.45f;
    public float hapticSeconds = 0.07f;

    [Header("Events")]
    [Tooltip("Bij elke slag, met de sterkte (0..1)")]
    public UnityEvent<float> onBeat;

    AudioSource source;
    float nextBeat;
    bool ownsClip;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;   // het hart zit overal rondom je
        if (atmosphere == null) atmosphere = GetComponent<ColonAtmosphere>();
        if (atmosphere == null) atmosphere = FindAnyObjectByType<ColonAtmosphere>();
        if (beatClip == null) { beatClip = CreateLubDub(); ownsClip = true; }
    }

    void OnEnable() { nextBeat = Time.time + 0.5f; }

    void Update()
    {
        if (Time.time < nextBeat) return;

        float s = atmosphere != null ? atmosphere.Severity : 0f;
        float irregular = Mathf.InverseLerp(irregularFrom, 1f, s);
        float interval = 60f / Mathf.Lerp(bpmHealthy, bpmSick, s);
        float strength = 1f;

        float roll = Random.value;
        if (roll < skipChanceSick * irregular)
        {
            // overgeslagen slag: lange stilte, daarna een harde inhaalslag
            interval *= 1.9f;
            strength = 1f;
        }
        else if (roll < (skipChanceSick + extraBeatChanceSick) * irregular)
        {
            // extra vroege, zwakke slag
            interval *= 0.55f;
            strength = 0.55f;
        }
        interval *= 1f + Random.Range(-1f, 1f) * jitterSick * irregular;

        Beat(strength, s);
        nextBeat = Time.time + Mathf.Max(0.25f, interval);
    }

    void Beat(float strength, float s)
    {
        if (beatClip != null)
            source.PlayOneShot(beatClip, Mathf.Lerp(volumeHealthy, volumeSick, s) * Mathf.Lerp(0.7f, 1f, strength));

        if (haptics)
        {
            float amp = Mathf.Lerp(hapticHealthy, hapticSick, s) * strength;
            SendHaptic(XRNode.LeftHand, amp);
            SendHaptic(XRNode.RightHand, amp);
        }

        if (atmosphere != null) atmosphere.Pulse(strength);
        onBeat?.Invoke(strength);
    }

    void SendHaptic(XRNode node, float amplitude)
    {
        if (amplitude <= 0.001f) return;
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        if (device.isValid) device.SendHapticImpulse(0u, Mathf.Clamp01(amplitude), hapticSeconds);
    }

    // Twee doffe, lage stoten ("lub" en zachtere "dub"), eenmalig aangemaakt
    static AudioClip CreateLubDub()
    {
        const int rate = 22050;
        const float length = 0.55f;
        int n = Mathf.CeilToInt(rate * length);
        var data = new float[n];
        AddThump(data, rate, 0f, 1f, 55f);
        AddThump(data, rate, 0.24f, 0.7f, 68f);
        var clip = AudioClip.Create("Heartbeat_LubDub", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static void AddThump(float[] data, int rate, float start, float gain, float freq)
    {
        int s0 = Mathf.FloorToInt(start * rate);
        int len = Mathf.FloorToInt(0.16f * rate);
        float phase = 0f;
        for (int i = 0; i < len && s0 + i < data.Length; i++)
        {
            float t = i / (float)rate;
            float env = (1f - Mathf.Exp(-t * 400f)) * Mathf.Exp(-t * 26f);
            float f = freq * (1f - 0.35f * Mathf.Clamp01(t / 0.12f));   // toon zakt licht, klinkt dof
            phase += 2f * Mathf.PI * f / rate;
            data[s0 + i] += gain * env * (Mathf.Sin(phase) + 0.25f * Mathf.Sin(phase * 2f));
        }
    }

    void OnDestroy()
    {
        if (ownsClip && beatClip != null) Destroy(beatClip);
    }
}
