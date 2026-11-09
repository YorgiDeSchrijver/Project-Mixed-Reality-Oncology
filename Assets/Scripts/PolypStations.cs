using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Zet de 4 poliep-stages (Stage1 t/m Stage4) als stations langs de darm.
/// Gebruik: zet dit script op een lege GameObject "PolypStations", sleep de 4 FBX's en PlayerPath
/// in de velden, en kies in het kebab-menu (drie puntjes) van het component "Build stations".
/// Opnieuw uitvoeren mag altijd: de oude stations worden eerst verwijderd.
/// </summary>
public class PolypStations : MonoBehaviour
{
    struct Station
    {
        public Vector3 centre, wall, inward;   // FBX-ruimte: midden van de darm, punt op de wand, normaal naar binnen
        public Station(Vector3 c, Vector3 w, Vector3 i) { centre = c; wall = w; inward = i; }
    }

    // Plekken op 20%, 40%, 60% en 80% van de darm, afwisselend links en rechts.
    // Berekend uit de ringen van Colon_Tunnel.fbx.
    static readonly Station[] Data =
    {
        new Station(new Vector3(2.0891f,-0.4071f,12.1951f), new Vector3(2.9624f,-0.7589f,10.8219f), new Vector3(-0.5128f,0.2098f,0.8324f)),
        new Station(new Vector3(14.5174f,-1.9980f,13.0480f), new Vector3(14.2626f,-2.2926f,14.7171f), new Vector3(0.1625f,0.1700f,-0.9720f)),
        new Station(new Vector3(25.3157f,-2.4983f,19.4887f), new Vector3(26.4364f,-2.6442f,18.0500f), new Vector3(-0.6136f,0.0796f,0.7856f)),
        new Station(new Vector3(36.9655f,-0.7545f,22.2659f), new Vector3(36.8642f,-0.6575f,24.2718f), new Vector3(-0.1052f,-0.0864f,-0.9907f)),
    };

    // Basispunt van de poliepmodellen (modelruimte): hier zit de poliep vast aan de wand.
    static readonly Vector3 ModelAnchor = new Vector3(0.593f, -13.079f, -0.207f);

    // PathPoint_035 in FBX-ruimte; hiermee controleren we hoe Unity de assen heeft omgezet.
    static readonly Vector3 RefFbx = new Vector3(22.46433f, -2.838893f, 17.341143f);

    [Header("Modellen (sleep de FBX's uit het Project-venster)")]
    public GameObject stage1;
    public GameObject stage2;
    public GameObject stage3;
    public GameObject stage4;

    [Header("Referenties")]
    [Tooltip("PlayerPath uit de Hierarchy")]
    public Transform pathRoot;
    public ColonPathRider rider;

    [Header("Instellingen")]
    [Tooltip("Schaal van alle poliepen. Eén waarde voor alle stages, zodat het groeien klopt. Stage 4 is ongeveer 17 modeleenheden hoog.")]
    public float modelScale = 0.09f;
    [Tooltip("Hoeveel de basis in de wand zakt (m)")]
    public float sink = 0.15f;
    [Tooltip("Draai de poliep om zijn as, verschillend per station")]
    public float spinPerStation = 70f;

    [ContextMenu("Build stations")]
    public void Build()
    {
        GameObject[] prefabs = { stage1, stage2, stage3, stage4 };
        if (pathRoot == null) { Debug.LogError("PolypStations: sleep PlayerPath in 'Path Root'."); return; }
        for (int i = 0; i < 4; i++)
            if (prefabs[i] == null) { Debug.LogError("PolypStations: Stage " + (i + 1) + " ontbreekt."); return; }

        // Unity spiegelt de x-as bij het importeren van een FBX. Bepaal dat aan PathPoint_035.
        Transform last = pathRoot.Find("PathPoint_035");
        if (last == null) { Debug.LogError("PolypStations: PathPoint_035 niet gevonden onder " + pathRoot.name); return; }
        float sx = Mathf.Sign(last.localPosition.x * RefFbx.x);
        Vector3 expected = new Vector3(sx * RefFbx.x, RefFbx.y, RefFbx.z);
        float err = Vector3.Distance(expected, last.localPosition);
        if (err > 0.05f)
        {
            Debug.LogError("PolypStations: PathPoint_035 staat niet waar verwacht (afwijking " + err.ToString("F2") + " m). Er is niets geplaatst.");
            return;
        }

        if (rider == null) rider = FindFirstObjectByType<ColonPathRider>();
        Clear();

        for (int i = 0; i < 4; i++)
        {
            Station d = Data[i];
            Vector3 wall = pathRoot.TransformPoint(new Vector3(sx * d.wall.x, d.wall.y, d.wall.z));
            Vector3 centre = pathRoot.TransformPoint(new Vector3(sx * d.centre.x, d.centre.y, d.centre.z));
            Vector3 inward = pathRoot.TransformDirection(new Vector3(sx * d.inward.x, d.inward.y, d.inward.z)).normalized;

            var rootGO = new GameObject("Station_Stage" + (i + 1));
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(rootGO, "Build polyp stations");
#endif
            rootGO.transform.SetParent(transform, false);
            // Y-as van het station wijst van de wand naar het midden van de darm: de poliep groeit de darm in.
            rootGO.transform.position = wall - inward * sink;
            rootGO.transform.rotation = Quaternion.FromToRotation(Vector3.up, inward) * Quaternion.AngleAxis(i * spinPerStation, Vector3.up);

            var anchorGO = new GameObject("PathAnchor");
            anchorGO.transform.SetParent(rootGO.transform, true);
            anchorGO.transform.position = centre;

            GameObject inst;
#if UNITY_EDITOR
            inst = PrefabUtility.InstantiatePrefab(prefabs[i]) as GameObject;
#else
            inst = Instantiate(prefabs[i]);
#endif
            inst.name = "Polyp_Stage" + (i + 1);
            inst.transform.SetParent(rootGO.transform, false);
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one * modelScale;
            Vector3 anchorUnity = new Vector3(sx * ModelAnchor.x, ModelAnchor.y, ModelAnchor.z);
            inst.transform.localPosition = -anchorUnity * modelScale;   // basispunt van de poliep op de oorsprong van het station

            var st = rootGO.AddComponent<PolypStation>();
            st.stage = i + 1;
            st.rider = rider;
            st.pathAnchor = anchorGO.transform;
            st.modelRoot = inst.transform;
        }

#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        Debug.Log("PolypStations: 4 stations geplaatst.");
    }

    [ContextMenu("Remove stations")]
    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject c = transform.GetChild(i).gameObject;
#if UNITY_EDITOR
            if (!Application.isPlaying) { Undo.DestroyObjectImmediate(c); continue; }
#endif
            Destroy(c);
        }
    }
}