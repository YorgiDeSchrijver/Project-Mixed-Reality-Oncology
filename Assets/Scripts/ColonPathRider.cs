using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Beweegt de XR Origin langs de PathPoint_xxx-objecten uit Colon_Tunnel.fbx.
/// Modus Manual: de speler duwt de thumbstick vooruit/achteruit en glijdt zo langs het pad.
/// Modus Auto: het pad wordt automatisch afgelegd (handig voor cutscenes).
/// Zet dit script op een lege GameObject (bv. "ColonRide").
/// </summary>
public class ColonPathRider : MonoBehaviour
{
    public enum ControlMode { Manual, Auto }

    [Header("Referenties")]
    [Tooltip("De PlayerPath-parent met PathPoint_000, _001, ... als kinderen")]
    public Transform pathRoot;
    [Tooltip("De XR Origin die verplaatst wordt (of een lege parent erboven)")]
    public Transform xrOrigin;
    [Tooltip("De Main Camera ONDER de XR Origin (Camera Offset > Main Camera)")]
    public Camera head;

    [Header("Besturing")]
    public ControlMode mode = ControlMode.Manual;
    [Tooltip("Sleep hier 'XRI Left Locomotion / Move' in (Vector2). Alleen de y-as (vooruit/achteruit) wordt gebruikt.")]
    public InputActionProperty moveAction;
    [Tooltip("W/S of pijltjestoetsen, handig om in de editor te testen zonder headset")]
    public bool allowKeyboard = true;
    public bool allowBackwards = true;
    [Range(0f, 0.5f)] public float deadzone = 0.15f;
    [Tooltip("Zet op false om de speler tijdelijk vast te zetten (bv. tijdens een dialoog of cutscene)")]
    public bool inputEnabled = true;

    [Header("Beweging")]
    [Tooltip("Maximale snelheid in meter per seconde. 0.4-0.8 is comfortabel.")]
    public float speed = 0.6f;
    [Tooltip("Hoe snel de speler op- en afremt (m/s per seconde). Lager = zachter.")]
    public float acceleration = 1.0f;
    [Tooltip("Afstand (m) voor het begin/einde waarover je afremt")]
    public float easeDistance = 1.5f;
    [Tooltip("Stuurt de speler mee in de bochten (alleen links/rechts, geen op/neer)")]
    public bool rotateWithPath = true;
    [Tooltip("Hoger = sneller meedraaien. Houd laag voor comfort.")]
    public float turnSmoothing = 1.2f;
    [Tooltip("Alleen Auto-modus: start meteen")]
    public bool autoStart = false;

    [Header("Veiligheid")]
    [Tooltip("Zet CharacterController, gravity, teleport en joystick-beweging uit op de XR Origin. Anders vechten die tegen dit script.")]
    public bool disableOtherLocomotion = true;
    public bool debugLog = false;

    [Header("Events")]
    public UnityEvent onRideStarted;
    public UnityEvent onReachedEnd;

    readonly List<Vector3> samples = new List<Vector3>();
    readonly List<float> cumulative = new List<float>();
    float totalLength;
    float distance;
    float velocity;          // huidige snelheid langs het pad (m/s, negatief = achteruit)
    bool moving;             // alleen Auto-modus
    bool endFired;
    const int SamplesPerSegment = 16;

    public float Progress01 => totalLength > 0f ? distance / totalLength : 0f;
    public float Distance => distance;
    public float TotalLength => totalLength;
    public bool IsMoving => mode == ControlMode.Auto ? moving : Mathf.Abs(velocity) > 0.01f;
    public bool Ready => samples.Count > 1;

    /// <summary>Afstand (m) langs het pad van het punt dat het dichtst bij een wereldpositie ligt.</summary>
    public float ProjectDistance(Vector3 worldPos)
    {
        if (samples.Count < 2) return 0f;
        int best = 0;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < samples.Count; i++)
        {
            float d = (samples[i] - worldPos).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = i; }
        }
        return cumulative[best];
    }

    void Awake()
    {
        // De camera MOET onder de XR Origin hangen, anders loopt de rig weg.
        if (xrOrigin != null && (head == null || !head.transform.IsChildOf(xrOrigin)))
        {
            Camera found = xrOrigin.GetComponentInChildren<Camera>(true);
            if (found != null)
            {
                Debug.LogWarning("ColonPathRider: 'Head' hing niet onder de XR Origin. Automatisch vervangen door '" + found.name + "'. Zet dit ook in de Inspector goed.");
                head = found;
            }
            else
            {
                Debug.LogError("ColonPathRider: geen camera gevonden onder de XR Origin.");
            }
        }

        if (!disableOtherLocomotion || xrOrigin == null) return;

        foreach (var cc in xrOrigin.GetComponentsInChildren<CharacterController>(true))
            cc.enabled = false;

        string[] blocked = { "Locomotion", "Gravity", "MoveProvider", "TurnProvider",
                             "Teleportation", "CharacterControllerDriver", "ClimbProvider" };
        foreach (var mb in xrOrigin.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null || mb == this) continue;
            string n = mb.GetType().Name;
            foreach (var b in blocked)
                if (n.Contains(b)) { mb.enabled = false; Debug.Log("ColonPathRider: uitgezet -> " + n); break; }
        }
    }

    void OnEnable()  { if (moveAction.action != null) moveAction.action.Enable(); }
    void OnDisable() { if (moveAction.action != null) moveAction.action.Disable(); }

    void Start()
    {
        BuildPath();
        if (samples.Count > 1)
        {
            Place(0f, true);
            if (mode == ControlMode.Auto && autoStart) StartRide();
        }
    }

    // ---------- publieke functies ----------
    public void StartRide() { if (samples.Count > 1) { moving = true; onRideStarted?.Invoke(); } }
    public void Pause()  { moving = false; }
    public void Resume() { if (distance < totalLength) moving = true; }
    public void SetSpeed(float metersPerSecond) { speed = metersPerSecond; }
    public void SetInputEnabled(bool value) { inputEnabled = value; }
    public void SetMode(ControlMode m) { mode = m; velocity = 0f; }

    /// <summary>Zet de speler direct op een afstand (m) langs het pad.</summary>
    public void JumpTo(float meters)
    {
        distance = Mathf.Clamp(meters, 0f, totalLength);
        velocity = 0f;
        endFired = distance >= totalLength - 0.01f;
        Place(distance, true);
    }

    // ---------- pad opbouwen ----------
    void BuildPath()
    {
        samples.Clear(); cumulative.Clear();
        Transform root = pathRoot != null ? pathRoot : transform;

        var pts = new List<Transform>();
        foreach (var t in root.GetComponentsInChildren<Transform>())
            if (t.name.StartsWith("PathPoint")) pts.Add(t);
        pts.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        if (pts.Count < 2)
        {
            Debug.LogError("ColonPathRider: minder dan 2 PathPoint_ objecten gevonden onder " + root.name);
            return;
        }

        // Catmull-Rom door de punten, daarna een afstandstabel zodat de snelheid constant is
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 p0 = pts[Mathf.Max(i - 1, 0)].position;
            Vector3 p1 = pts[i].position;
            Vector3 p2 = pts[i + 1].position;
            Vector3 p3 = pts[Mathf.Min(i + 2, pts.Count - 1)].position;
            for (int s = 0; s < SamplesPerSegment; s++)
                samples.Add(CatmullRom(p0, p1, p2, p3, s / (float)SamplesPerSegment));
        }
        samples.Add(pts[pts.Count - 1].position);

        cumulative.Add(0f);
        for (int i = 1; i < samples.Count; i++)
            cumulative.Add(cumulative[i - 1] + Vector3.Distance(samples[i - 1], samples[i]));
        totalLength = cumulative[cumulative.Count - 1];
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t +
                       (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                       (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    // ---------- input ----------
    float ReadInput()
    {
        if (!inputEnabled) return 0f;

        float v = 0f;
        if (moveAction.action != null) v = moveAction.action.ReadValue<Vector2>().y;

        if (Mathf.Abs(v) < deadzone) v = 0f;
        else v = Mathf.Sign(v) * Mathf.InverseLerp(deadzone, 1f, Mathf.Abs(v));

        if (allowKeyboard && Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) v = 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) v = -1f;
        }

        if (!allowBackwards) v = Mathf.Max(0f, v);
        return v;
    }

    // Zacht afremmen vlak voor het begin of einde van het pad
    float EdgeEase(float signedDirection)
    {
        if (easeDistance <= 0f) return 1f;
        float room = signedDirection >= 0f ? totalLength - distance : distance;
        return Mathf.Max(0.05f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(room / easeDistance)));
    }

    void Update()
    {
        if (samples.Count < 2) return;

        float target;
        if (mode == ControlMode.Auto)
        {
            if (!moving) { velocity = 0f; return; }
            target = speed * EdgeEase(1f);
            // Auto-modus ook met een zachte start
            if (easeDistance > 0f) target *= Mathf.Max(0.15f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distance / easeDistance)));
        }
        else
        {
            float input = ReadInput();
            target = input * speed * (Mathf.Abs(input) > 0f ? EdgeEase(input) : 1f);
        }

        velocity = Mathf.MoveTowards(velocity, target, acceleration * Time.deltaTime);
        distance = Mathf.Clamp(distance + velocity * Time.deltaTime, 0f, totalLength);

        // Aan een uiteinde: snelheid wegnemen zodat je niet "tegen de muur blijft duwen"
        if ((distance <= 0f && velocity < 0f) || (distance >= totalLength && velocity > 0f)) velocity = 0f;

        Place(distance, false);

        bool atEnd = distance >= totalLength - 0.01f;
        if (atEnd && !endFired)
        {
            endFired = true;
            if (mode == ControlMode.Auto) moving = false;
            onReachedEnd?.Invoke();
        }
        else if (!atEnd && endFired && distance < totalLength - 0.5f)
        {
            endFired = false; // speler is een stukje teruggegaan: event mag opnieuw
        }
    }

    // ---------- plaatsen ----------
    void Place(float d, bool snapRotation)
    {
        if (xrOrigin == null) return;

        int hi = cumulative.BinarySearch(d);
        if (hi < 0) hi = ~hi;
        hi = Mathf.Clamp(hi, 1, samples.Count - 1);
        int lo = hi - 1;
        float seg = cumulative[hi] - cumulative[lo];
        float k = seg > 1e-5f ? (d - cumulative[lo]) / seg : 0f;
        Vector3 pos = Vector3.Lerp(samples[lo], samples[hi], k);
        Vector3 dir = (samples[hi] - samples[lo]).normalized;

        // draaien: alleen yaw, altijd in de looprichting van het pad (ook als je achteruit gaat)
        if (rotateWithPath)
        {
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            if (flat.sqrMagnitude > 1e-4f)
            {
                Quaternion target = Quaternion.LookRotation(flat.normalized, Vector3.up);
                xrOrigin.rotation = snapRotation
                    ? target
                    : Quaternion.Slerp(xrOrigin.rotation, target, 1f - Mathf.Exp(-turnSmoothing * Time.deltaTime));
            }
        }

        // zorg dat het HOOFD (niet de vloer) op de middellijn van de darm zit
        Vector3 headInRig = head != null ? xrOrigin.InverseTransformPoint(head.transform.position) : Vector3.zero;
        xrOrigin.position = pos - xrOrigin.TransformVector(headInRig);

        if (debugLog && Time.frameCount % 60 == 0 && head != null)
            Debug.Log($"ColonPathRider: {d:F1}/{totalLength:F1} m  v={velocity:F2}  afstand hoofd-pad={Vector3.Distance(pos, head.transform.position):F2} m");
    }

    void OnDrawGizmosSelected()
    {
        Transform root = pathRoot != null ? pathRoot : transform;
        Gizmos.color = Color.cyan;
        Vector3? prev = null;
        var pts = new List<Transform>();
        foreach (var t in root.GetComponentsInChildren<Transform>())
            if (t.name.StartsWith("PathPoint")) pts.Add(t);
        pts.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        foreach (var t in pts)
        {
            Gizmos.DrawSphere(t.position, 0.08f);
            if (prev.HasValue) Gizmos.DrawLine(prev.Value, t.position);
            prev = t.position;
        }
    }
}