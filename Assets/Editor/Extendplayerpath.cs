#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Breidt PlayerPath uit van PathPoint_035 tot het einde van Colon_Seg_08.
/// Gebruik: menu Tools > Colon > Extend PlayerPath (hele darm). Eenmalig uitvoeren.
/// Zet dit bestand in Assets/Editor/ExtendPlayerPath.cs
/// </summary>
public static class ExtendPlayerPath
{
    // Punten in FBX-ruimte (zoals in Colon_Tunnel.fbx), ongeveer 1 m uit elkaar,
    // berekend uit de middelpunten van de darmringen van Colon_Seg_05 t/m Colon_Seg_08.
    static readonly Vector3[] NewPoints =
    {
        new Vector3(23.2402f, -2.7324f, 17.9488f),
        new Vector3(24.0347f, -2.6894f, 18.5376f),
        new Vector3(24.8264f, -2.6561f, 19.1297f),
        new Vector3(25.6059f, -2.5209f, 19.7319f),
        new Vector3(26.3763f, -2.4881f, 20.3520f),
        new Vector3(27.1792f, -2.5068f, 20.9476f),
        new Vector3(28.0031f, -2.4817f, 21.5132f),
        new Vector3(28.9073f, -2.4262f, 21.9341f),
        new Vector3(29.8419f, -2.2932f, 22.2626f),
        new Vector3(30.7884f, -2.0885f, 22.5106f),
        new Vector3(31.7562f, -1.8988f, 22.6731f),
        new Vector3(32.7285f, -1.6730f, 22.7225f),
        new Vector3(33.7036f, -1.4642f, 22.6550f),
        new Vector3(34.6772f, -1.2523f, 22.5730f),
        new Vector3(35.6588f, -1.0717f, 22.5219f),
        new Vector3(36.6146f, -0.8435f, 22.3397f),
        new Vector3(37.5790f, -0.6093f, 22.2241f),
        new Vector3(38.5320f, -0.3478f, 22.0832f),
        new Vector3(39.4944f, -0.1062f, 21.9940f),
        new Vector3(40.4615f, 0.1296f, 21.9277f),
        new Vector3(41.4300f, 0.3240f, 21.7741f),
        new Vector3(42.4062f, 0.5193f, 21.6808f),
        new Vector3(43.3763f, 0.7554f, 21.6363f),
        new Vector3(44.3387f, 1.0077f, 21.7281f),
        new Vector3(45.2684f, 1.3342f, 21.8725f),
        new Vector3(46.2016f, 1.5556f, 22.1551f),
        new Vector3(47.1246f, 1.7689f, 22.4710f),
        new Vector3(47.9699f, 2.0951f, 22.8908f)
    };

    // PathPoint_035 in FBX-ruimte; hiermee controleren we hoe Unity de assen heeft omgezet.
    static readonly Vector3 RefFbx = new Vector3(22.46433f, -2.838893f, 17.341143f);

    [MenuItem("Tools/Colon/Extend PlayerPath (hele darm)")]
    static void Run()
    {
        Transform root = null;
        if (Selection.activeTransform != null && Selection.activeTransform.name == "PlayerPath")
            root = Selection.activeTransform;
        if (root == null)
        {
            GameObject go = GameObject.Find("PlayerPath");
            if (go != null) root = go.transform;
        }
        if (root == null)
        {
            EditorUtility.DisplayDialog("PlayerPath", "Geen PlayerPath gevonden in de scene. Selecteer hem in de Hierarchy en probeer opnieuw.", "OK");
            return;
        }

        Transform last = root.Find("PathPoint_035");
        if (last == null)
        {
            EditorUtility.DisplayDialog("PlayerPath", "PathPoint_035 niet gevonden onder PlayerPath.", "OK");
            return;
        }
        if (root.Find("PathPoint_036") != null)
        {
            EditorUtility.DisplayDialog("PlayerPath", "Het pad is al uitgebreid (PathPoint_036 bestaat al).", "OK");
            return;
        }

        // Unity spiegelt de x-as bij het importeren van een FBX. Bepaal dat aan PathPoint_035
        // en stop als de positie daarna niet klopt (bv. andere schaal), zodat we niets verkeerd plaatsen.
        float sx = Mathf.Sign(last.localPosition.x * RefFbx.x);
        Vector3 expected = new Vector3(sx * RefFbx.x, RefFbx.y, RefFbx.z);
        float err = Vector3.Distance(expected, last.localPosition);
        if (err > 0.05f)
        {
            EditorUtility.DisplayDialog("PlayerPath",
                "PathPoint_035 staat niet waar verwacht (afwijking " + err.ToString("F2") + " m). " +
                "Waarschijnlijk is de schaal of orientatie van het FBX gewijzigd. Er is niets aangepast.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        for (int i = 0; i < NewPoints.Length; i++)
        {
            var go = new GameObject("PathPoint_" + (36 + i).ToString("000"));
            Undo.RegisterCreatedObjectUndo(go, "Extend PlayerPath");
            go.transform.SetParent(root, false);
            Vector3 p = NewPoints[i];
            go.transform.localPosition = new Vector3(sx * p.x, p.y, p.z);
        }
        Undo.SetCurrentGroupName("Extend PlayerPath");
        EditorUtility.SetDirty(root);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

        Debug.Log("PlayerPath uitgebreid met " + NewPoints.Length + " punten (PathPoint_036 t/m PathPoint_" + (35 + NewPoints.Length).ToString("000") + ").");
    }
}
#endif