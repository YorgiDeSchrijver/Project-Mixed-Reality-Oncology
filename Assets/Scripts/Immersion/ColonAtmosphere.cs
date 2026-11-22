using UnityEngine;

/// <summary>
/// Centrale "ziekte-meter" voor de wandeling door de darm.
/// Houdt één waarde bij, Severity (0 = gezond, 1 = kritiek), en stuurt daarmee alle sfeer-effecten:
///  - kleur, glans en rode gloed van de darmwand,
///  - donkerder en roder licht (ambient, directional light, fog),
///  - de darm die smaller wordt (via ColonWallMotion en ColonPathRider),
///  - een vignette die dichtgaat,
///  - poliepen die per stadium donkerder worden.
/// Andere scripts (bv. ColonHeartbeat) lezen Severity en roepen Pulse() aan op elke hartslag.
/// Zuinig voor de Quest: geen textures, geen extra lichten, geen allocaties per frame.
/// Zet dit op een lege GameObject (bv. "ColonAtmosphere").
/// </summary>
public class ColonAtmosphere : MonoBehaviour
{
    [Header("Referenties (leeg = automatisch zoeken)")]
    public ColonPathRider rider;
    public ColonWallMotion wall;
    [Tooltip("Renderers van de darmwand. Leeg = alle renderers onder de ColonWallMotion.")]
    public Renderer[] wallRenderers;
    public Light mainLight;
    [Tooltip("Renderer van de vignette (TunnelingVignette-mesh voor de camera)")]
    public Renderer vignette;

    [Header("Ziekte-verloop")]
    [Tooltip("Severity volgens de voortgang langs het pad (x = 0..1 voortgang, y = 0..1 severity)")]
    public AnimationCurve severityByProgress = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.15f, 0.05f), new Keyframe(0.85f, 0.9f), new Keyframe(1f, 1f));
    [Tooltip("Seconden om van 0 naar 1 te gaan. Zorgt dat alles vloeiend verandert, ook als je terugloopt.")]
    public float responseSeconds = 4f;
    [Tooltip("Handig om te testen in de editor: negeert de voortgang")]
    public bool usePreview = false;
    [Range(0f, 1f)] public float previewSeverity = 0.5f;

    [Header("Darmwand")]
    public Gradient wallTint = MakeGradient(
        new Color(1f, 0.92f, 0.92f), new Color(0.78f, 0.55f, 0.6f), new Color(0.38f, 0.22f, 0.22f));
    [Tooltip("Glans (nat/slijmerig) bij gezond en bij kritiek")]
    [Range(0f, 1f)] public float smoothnessHealthy = 0.82f;
    [Range(0f, 1f)] public float smoothnessSick = 0.4f;
    [Tooltip("Rode gloed van de wand, in het ritme van de hartslag")]
    [ColorUsage(false, true)] public Color glowColor = new Color(0.55f, 0.02f, 0.03f);
    public float glowHealthy = 0.04f;
    public float glowSick = 0.35f;
    [Tooltip("Hoeveel extra gloed een hartslag geeft")]
    public float glowPulse = 0.6f;

    [Header("Licht")]
    public Color ambientHealthy = new Color(0.55f, 0.38f, 0.38f);
    public Color ambientSick = new Color(0.12f, 0.02f, 0.03f);
    public float lightHealthy = 1f;
    public float lightSick = 0.2f;
    public Color lightColorHealthy = new Color(1f, 0.9f, 0.85f);
    public Color lightColorSick = new Color(0.9f, 0.25f, 0.2f);

    [Header("Fog (duisternis in de verte)")]
    public bool useFog = true;
    public Color fogHealthy = new Color(0.3f, 0.12f, 0.12f);
    public Color fogSick = new Color(0.04f, 0f, 0.005f);
    public float fogDensityHealthy = 0.03f;
    public float fogDensitySick = 0.22f;

    [Header("Inkrimpen")]
    [Tooltip("Hoeveel (m) de wand naar binnen komt bij severity 1")]
    public float maxNarrowing = 0.22f;
    [Tooltip("Hoeveel (m) de wand naar binnen klopt bij een hartslag (bij severity 1)")]
    public float pulseDepth = 0.018f;

    [Header("Vignette")]
    [Tooltip("1 = helemaal open, lager = meer zwart rond het beeld")]
    [Range(0f, 1f)] public float apertureHealthy = 1f;
    [Range(0f, 1f)] public float apertureSick = 0.6f;
    [Range(0f, 1f)] public float featherHealthy = 0.2f;
    [Range(0f, 1f)] public float featherSick = 0.45f;
    public Color vignetteColor = new Color(0.12f, 0f, 0.01f, 1f);

    [Header("Poliepen")]
    [Tooltip("Kleur van een stadium 4-poliep (stadium 1 houdt zijn eigen kleur)")]
    public Color polypDarkest = new Color(0.32f, 0.12f, 0.16f);

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    static readonly int ApertureId = Shader.PropertyToID("_ApertureSize");
    static readonly int FeatherId = Shader.PropertyToID("_FeatheringEffect");
    static readonly int VignetteColorId = Shader.PropertyToID("_VignetteColor");
    static readonly int VignetteBlendId = Shader.PropertyToID("_VignetteColorBlend");

    float severity;
    float pulse;             // 0..1, springt omhoog bij een hartslag en sterft snel uit
    float baseTubeRadius;
    Material wallMat, vignetteMat;
    Material[] polypMats = new Material[0];

    /// <summary>0 = gezond, 1 = kritiek. Vloeiend.</summary>
    public float Severity => severity;
    /// <summary>Huidige sterkte van de hartslag-puls (0..1).</summary>
    public float PulseValue => pulse;

    /// <summary>Roep aan op elke hartslag. strength 1 = normale slag.</summary>
    public void Pulse(float strength) { pulse = Mathf.Max(pulse, Mathf.Clamp01(strength)); }

    void Start()
    {
        if (rider == null) rider = FindAnyObjectByType<ColonPathRider>();
        if (wall == null) wall = FindAnyObjectByType<ColonWallMotion>();
        if (mainLight == null) mainLight = RenderSettings.sun != null ? RenderSettings.sun : FindAnyObjectByType<Light>();
        if ((wallRenderers == null || wallRenderers.Length == 0) && wall != null)
            wallRenderers = wall.GetComponentsInChildren<MeshRenderer>(true);
        if (rider != null) baseTubeRadius = rider.tubeRadius;

        // Eén gedeelde kopie van het wandmateriaal: SRP Batcher blijft werken, het asset zelf blijft ongewijzigd.
        if (wallRenderers != null && wallRenderers.Length > 0 && wallRenderers[0].sharedMaterial != null)
        {
            wallMat = new Material(wallRenderers[0].sharedMaterial) { name = "ColonWall_Atmosphere" };
            wallMat.EnableKeyword("_EMISSION");
            wallMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            foreach (var r in wallRenderers) if (r != null) r.sharedMaterial = wallMat;
        }

        if (vignette != null && vignette.sharedMaterial != null)
        {
            vignetteMat = new Material(vignette.sharedMaterial) { name = "Vignette_Atmosphere" };
            vignette.sharedMaterial = vignetteMat;
        }

        TintPolyps();

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.fog = useFog;
        RenderSettings.fogMode = FogMode.ExponentialSquared;

        severity = TargetSeverity();
        Apply(0f);
    }

    // Poliepen worden per stadium donkerder (eenmalig bij de start)
    void TintPolyps()
    {
        var stations = FindObjectsByType<PolypStation>(FindObjectsInactive.Include);
        var list = new System.Collections.Generic.List<Material>();
        foreach (var st in stations)
        {
            Transform root = st.modelRoot != null ? st.modelRoot : st.transform;
            float k = (st.stage - 1) / 3f;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].HasProperty(BaseColorId)) continue;
                    var m = new Material(mats[i]) { name = mats[i].name + "_Stage" + st.stage };
                    m.SetColor(BaseColorId, Color.Lerp(mats[i].GetColor(BaseColorId), polypDarkest, k));
                    if (m.HasProperty(SmoothnessId)) m.SetFloat(SmoothnessId, Mathf.Lerp(0.7f, 0.25f, k));
                    mats[i] = m;
                    list.Add(m);
                }
                r.sharedMaterials = mats;
            }
        }
        polypMats = list.ToArray();
    }

    float TargetSeverity()
    {
        if (usePreview) return previewSeverity;
        if (rider == null || !rider.Ready) return 0f;
        return Mathf.Clamp01(severityByProgress.Evaluate(rider.Progress01));
    }

    void Update()
    {
        float dt = Time.deltaTime;
        severity = Mathf.MoveTowards(severity, TargetSeverity(), dt / Mathf.Max(0.05f, responseSeconds));
        pulse = Mathf.MoveTowards(pulse, 0f, dt * 3.5f);
        Apply(dt);
    }

    void Apply(float dt)
    {
        float s = severity;
        float p = pulse * pulse;   // korte, scherpe piek

        if (wallMat != null)
        {
            wallMat.SetColor(BaseColorId, wallTint.Evaluate(s));
            wallMat.SetFloat(SmoothnessId, Mathf.Lerp(smoothnessHealthy, smoothnessSick, s));
            float glow = Mathf.Lerp(glowHealthy, glowSick, s) + glowPulse * p * (0.3f + 0.7f * s);
            wallMat.SetColor(EmissionId, glowColor * glow);
        }

        RenderSettings.ambientLight = Color.Lerp(ambientHealthy, ambientSick, s) * (1f + 0.25f * p);
        if (mainLight != null)
        {
            mainLight.intensity = Mathf.Lerp(lightHealthy, lightSick, s);
            mainLight.color = Color.Lerp(lightColorHealthy, lightColorSick, s);
        }
        if (useFog)
        {
            RenderSettings.fogColor = Color.Lerp(fogHealthy, fogSick, s);
            RenderSettings.fogDensity = Mathf.Lerp(fogDensityHealthy, fogDensitySick, s);
        }

        float narrow = maxNarrowing * s * s;   // eerst nauwelijks, op het einde echt benauwd
        if (wall != null)
        {
            wall.narrowing = narrow;
            wall.pulse = pulseDepth * p * (0.25f + 0.75f * s);
        }
        if (rider != null && baseTubeRadius > 0f)
            rider.tubeRadius = Mathf.Max(0.15f, baseTubeRadius - narrow);

        if (vignetteMat != null)
        {
            float ap = Mathf.Lerp(apertureHealthy, apertureSick, s) - 0.03f * p * s;
            vignetteMat.SetFloat(ApertureId, Mathf.Clamp01(ap));
            vignetteMat.SetFloat(FeatherId, Mathf.Lerp(featherHealthy, featherSick, s));
            vignetteMat.SetColor(VignetteColorId, vignetteColor);
            vignetteMat.SetColor(VignetteBlendId, vignetteColor);
            bool show = ap < 0.999f;
            if (vignette.enabled != show) vignette.enabled = show;
        }
    }

    static Gradient MakeGradient(Color a, Color b, Color c)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 0.55f), new GradientColorKey(c, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    void OnDestroy()
    {
        if (wallMat != null) Destroy(wallMat);
        if (vignetteMat != null) Destroy(vignetteMat);
        foreach (var m in polypMats) if (m != null) Destroy(m);
    }
}
