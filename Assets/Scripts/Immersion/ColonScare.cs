using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

/// <summary>
/// Maakt de darm echt beklemmend, zodat de speler afgeschrikt wordt door wat kanker met een lichaam doet.
/// Alles volgt ColonDisease.Severity (hoe dicht bij hoe zieke plek), dus gezond blijft rustig:
///  - licht: de omgeving wordt donkerder en roder, met trage, onregelmatige uitval (geen strobe),
///  - zicht: dichte rode mist die de verte opslokt,
///  - geluid: een laag, dissonant gedreun dat aanzwelt met de ernst,
///  - schrikmoment: bij aankomst bij een poliep (vanaf stadium 2) een plotselinge dreun,
///    een harde trilling in de controllers en een wand die even naar je toe klapt.
/// Geen camerabeweging (VR-comfort), geen textures en geen allocaties per frame; geluiden worden eenmalig
/// procedureel aangemaakt. Wordt automatisch toegevoegd door ColonDisease.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ColonScare : MonoBehaviour
{
    [Header("Referenties (leeg = automatisch zoeken)")]
    public ColonDisease disease;

    [Header("Licht en zicht")]
    [Tooltip("Omgevingslicht bij severity 1 ten opzichte van normaal (0.25 = een kwart)")]
    [Range(0.05f, 1f)] public float darkestLight = 0.25f;
    [Tooltip("Kleur van de mist bij een zieke plek")]
    public Color fogColor = new Color(0.16f, 0.025f, 0.03f);
    [Tooltip("Dichtheid van de mist bij severity 1 (exponentieel kwadratisch)")]
    public float fogDensityMax = 0.16f;
    [Tooltip("Hoeveel het licht onregelmatig wegzakt bij severity 1 (0..1). Traag en zacht, nooit sneller dan ~2x per seconde.")]
    [Range(0f, 0.6f)] public float flickerDepth = 0.35f;
    [Tooltip("Vanaf deze severity begint het licht te haperen")]
    [Range(0f, 1f)] public float flickerFrom = 0.4f;

    [Header("Geluid")]
    [Tooltip("Eigen dreun (loopt oneindig). Leeg = procedureel geluid.")]
    public AudioClip droneClip;
    [Tooltip("Eigen schrikgeluid. Leeg = procedureel geluid.")]
    public AudioClip stingerClip;
    [Range(0f, 1f)] public float droneVolumeMax = 0.55f;
    [Range(0f, 1f)] public float stingerVolume = 1f;

    [Header("Schrikmoment bij een poliep")]
    public bool stingerOnArrive = true;
    [Tooltip("Laagste stadium dat een schrikmoment krijgt (1-4)")]
    [Range(1, 4)] public int minStage = 2;
    public bool haptics = true;

    AudioSource source;
    AudioSource droneSource;
    float baseIntensity;
    Color baseAmbient;
    bool baseFog;
    FogMode baseFogMode;
    Color baseFogColor;
    float baseFogDensity;
    AmbientMode ambientMode;
    bool captured;
    bool ownsDrone, ownsStinger;
    float noiseSeed;
    PolypStation[] hooked = new PolypStation[0];
    readonly System.Collections.Generic.HashSet<PolypStation> fired = new System.Collections.Generic.HashSet<PolypStation>();

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;

        // tweede bron voor de doorlopende dreun, zodat het schrikgeluid hem niet onderbreekt
        droneSource = gameObject.AddComponent<AudioSource>();
        droneSource.playOnAwake = false;
        droneSource.loop = true;
        droneSource.spatialBlend = 0f;
        droneSource.volume = 0f;

        if (disease == null) disease = GetComponent<ColonDisease>();
        if (disease == null) disease = FindAnyObjectByType<ColonDisease>();
        if (droneClip == null) { droneClip = CreateDrone(); ownsDrone = true; }
        if (stingerClip == null) { stingerClip = CreateStinger(); ownsStinger = true; }
        noiseSeed = Random.value * 100f;
    }

    void OnEnable()
    {
        CaptureRender();
        droneSource.clip = droneClip;
        droneSource.Play();
    }

    void Start()
    {
        if (!stingerOnArrive) return;
        hooked = FindObjectsByType<PolypStation>(FindObjectsInactive.Include);
        foreach (var st in hooked)
        {
            var station = st;
            station.onArrive.AddListener(() => OnArrive(station));
        }
    }

    void CaptureRender()
    {
        if (captured) return;
        captured = true;
        baseIntensity = RenderSettings.ambientIntensity;
        baseAmbient = RenderSettings.ambientLight;
        ambientMode = RenderSettings.ambientMode;
        baseFog = RenderSettings.fog;
        baseFogMode = RenderSettings.fogMode;
        baseFogColor = RenderSettings.fogColor;
        baseFogDensity = RenderSettings.fogDensity;
    }

    void Update()
    {
        float s = disease != null ? disease.Severity : 0f;
        float k = Mathf.SmoothStep(0f, 1f, s);

        // trage ruis (~0.5 Hz) die soms diep wegzakt: het licht "hapert", zonder te flitsen
        float flick = 0f;
        if (s > flickerFrom)
        {
            float amount = Mathf.InverseLerp(flickerFrom, 1f, s) * flickerDepth;
            float n = Mathf.PerlinNoise(noiseSeed, Time.time * 0.9f);
            flick = amount * Mathf.SmoothStep(0.55f, 0.9f, n);
        }
        float light = Mathf.Lerp(1f, darkestLight, k) * (1f - flick);

        if (ambientMode == AmbientMode.Skybox) RenderSettings.ambientIntensity = baseIntensity * light;
        else RenderSettings.ambientLight = baseAmbient * light;

        if (k > 0.01f)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.Lerp(baseFog ? baseFogColor : fogColor, fogColor, k);
            RenderSettings.fogDensity = Mathf.Lerp(baseFog ? baseFogDensity : 0f, fogDensityMax, k);
        }
        else
        {
            RenderSettings.fog = baseFog;
            RenderSettings.fogMode = baseFogMode;
            RenderSettings.fogColor = baseFogColor;
            RenderSettings.fogDensity = baseFogDensity;
        }

        // gedreun: licht hoorbaar zodra er iets zieks in de buurt is, dan snel luider
        droneSource.volume = droneVolumeMax * k * k * (1f - 0.35f * flick);
        droneSource.pitch = Mathf.Lerp(0.9f, 1.0f, k);
    }

    void OnArrive(PolypStation st)
    {
        if (st == null || st.stage < minStage || !fired.Add(st)) return;   // één keer per poliep
        Scare((st.stage - 1) / 3f);
    }

    /// <summary>Speel een schrikmoment af. strength 0..1 (1 = stadium 4).</summary>
    public void Scare(float strength)
    {
        strength = Mathf.Clamp01(strength);
        if (stingerClip != null) source.PlayOneShot(stingerClip, stingerVolume * Mathf.Lerp(0.55f, 1f, strength));
        if (disease != null) disease.Shock(strength);
        if (haptics)
        {
            float amp = Mathf.Lerp(0.5f, 1f, strength);
            float dur = Mathf.Lerp(0.25f, 0.9f, strength);
            Buzz(XRNode.LeftHand, amp, dur);
            Buzz(XRNode.RightHand, amp, dur);
        }
    }

    static void Buzz(XRNode node, float amplitude, float seconds)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        if (device.isValid) device.SendHapticImpulse(0u, Mathf.Clamp01(amplitude), seconds);
    }

    void OnDisable()
    {
        if (!captured) return;
        RenderSettings.ambientIntensity = baseIntensity;
        RenderSettings.ambientLight = baseAmbient;
        RenderSettings.fog = baseFog;
        RenderSettings.fogMode = baseFogMode;
        RenderSettings.fogColor = baseFogColor;
        RenderSettings.fogDensity = baseFogDensity;
        captured = false;
        if (droneSource != null) droneSource.Stop();
    }

    void OnDestroy()
    {
        if (ownsDrone && droneClip != null) Destroy(droneClip);
        if (ownsStinger && stingerClip != null) Destroy(stingerClip);
    }

    // ---------- procedurele geluiden (eenmalig, geen assets) ----------

    // 4 s naadloze lus: lage, ietwat valse tonen die tegen elkaar zweven plus een trage tremolo.
    // Alle frequenties zijn een veelvoud van 0.25 Hz, dus hele perioden in 4 s: geen klik bij het herhalen.
    static AudioClip CreateDrone()
    {
        const int rate = 22050;
        const float length = 4f;
        int n = Mathf.CeilToInt(rate * length);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float trem = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
            float v = 0.55f * Mathf.Sin(2f * Mathf.PI * 41f * t)
                    + 0.45f * Mathf.Sin(2f * Mathf.PI * 43.5f * t)    // zweving van 2.5 Hz
                    + 0.30f * Mathf.Sin(2f * Mathf.PI * 58f * t)      // tritonus boven de grondtoon
                    + 0.12f * Mathf.Sin(2f * Mathf.PI * 123.75f * t + Mathf.Sin(2f * Mathf.PI * 0.25f * t) * 2f);
            data[i] = v * trem * 0.5f;
        }
        var clip = AudioClip.Create("Colon_Drone", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Diepe, dofe dreun (zoals een zware klap in een holle ruimte) met een korte, donkere ruisstoot en een
    // lage rommel die uitsterft. Geen hoge tonen of akkoord: dat klonk als een synthesizer. Blijft ruim onder 1.0.
    static AudioClip CreateStinger()
    {
        const int rate = 22050;
        const float length = 1.8f;
        int n = Mathf.CeilToInt(rate * length);
        var data = new float[n];
        var rng = new System.Random(77);
        float low = 0f, low2 = 0f, phaseBoom = 0f, phaseRumble = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;

            // dreun: toon zakt snel van 75 naar 35 Hz, zachte aanzet tegen een klik
            float fb = 35f + 40f * Mathf.Exp(-t * 9f);
            phaseBoom += 2f * Mathf.PI * fb / rate;
            float boom = Mathf.Sin(phaseBoom) * (1f - Mathf.Exp(-t * 250f)) * Mathf.Exp(-t * 3.2f);

            // doffe ruisstoot (sterk laagdoorlaat gefilterd), kort
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            low += (noise - low) * 0.05f;
            low2 += (low - low2) * 0.05f;
            float thud = low2 * 9f * Mathf.Exp(-t * 14f);

            // lage rommel die langer naklinkt
            phaseRumble += 2f * Mathf.PI * (46f + 3f * Mathf.Sin(t * 5f)) / rate;
            float rumble = Mathf.Sin(phaseRumble) * 0.25f * (1f - Mathf.Exp(-t * 20f)) * Mathf.Exp(-t * 2f);

            float fadeOut = Mathf.Clamp01((length - t) / 0.25f);
            data[i] = (boom * 0.7f + thud * 0.3f + rumble) * fadeOut * 0.8f;
        }
        var clip = AudioClip.Create("Colon_Stinger", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
