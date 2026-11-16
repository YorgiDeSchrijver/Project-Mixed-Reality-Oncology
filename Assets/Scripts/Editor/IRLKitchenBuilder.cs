using System.Collections.Generic;
using System.IO;
using System.Linq;
using Oncologie.Core;
using Oncologie.IRL;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using static Oncologie.IRL.MeshFactory;
using Highlighter = Oncologie.IRL.Highlighter;

/// <summary>
/// Bouwt de volledige IRL-keuken in IRL_Kitchen.unity met eigen low-poly meshes en de keuken-texturen/atlassen
/// uit Assets/Models/Kitchen. Opnieuw uitvoeren mag altijd: de vorige versie wordt eerst verwijderd.
/// Menu: Oncologie → Bouw IRL-keuken
/// </summary>
public static class IRLKitchenBuilder
{
    private const string GenDir = "Assets/Models/Kitchen/Generated";
    private const string MeshPath = GenDir + "/IRLKitchenMeshes.asset";
    private const string MatDir = "Assets/Models/Materials";
    private const string TexDir = "Assets/Models/Kitchen/Textures";
    private const string ResDir = "Assets/Resources/IRL";
    private const string RootName = "IRL_Kitchen_Set";

    // Binnenmaten van de kamer (m)
    private const float X0 = -2.5f, X1 = 2.5f, Z0 = -2.0f, Z1 = 2.2f, H = 2.6f, T = 0.12f;

    private static Mesh _container;
    private static int _meshCount;
    private static readonly Dictionary<string, Material> M = new Dictionary<string, Material>();

    // ---- Atlas-uitsneden ----
    // T_KitchenAppliance
    private static readonly Rect A_HOB = Px(0, 0, 512, 512), A_OVEN = Px(512, 0, 1024, 512), A_WHITE = Px(0, 512, 256, 1024),
        A_MICRO = Px(512, 512, 1024, 768), A_BLACK = Px(520, 776, 760, 888);
    // T_KitchenSteel
    private static readonly Rect S_STEEL = Px(0, 0, 1024, 760), S_DRAIN = Px(384, 768, 512, 896), S_FILTER = Px(512, 768, 1024, 1024), S_DARK = Px(256, 768, 384, 896);
    // T_KitchenProps_Base_B
    private static readonly Rect P_TOWEL = Px(0, 0, 512, 512), P_CLOCK = Px(512, 0, 768, 256), P_PAINTING = Px(768, 0, 1024, 256),
        P_LEAF = Px(768, 256, 896, 512), P_MAT = Px(16, 512, 496, 760), P_POT = Px(896, 768, 1024, 896);
    // T_RoomTrim
    private static readonly Rect R_WHITE = Px(256, 512, 1024, 768), R_BLACK = Px(0, 256, 512, 512), R_BRUSHED = Px(512, 768, 1024, 1024);
    // T_Set_Birthday58_Atlas
    private static readonly Rect B_ENV = Px(0, 0, 768, 384), B_MARC = Px(768, 0, 1024, 384), B_REMINDER = Px(0, 384, 512, 576),
        B_CARD1 = Px(512, 384, 640, 576), B_CARD2 = Px(768, 384, 896, 576), B_GROCERY = Px(0, 576, 128, 768),
        B_FRITUUR = Px(128, 576, 256, 768), B_DRAWING = Px(256, 576, 384, 768), B_CAKE = Px(384, 576, 512, 768);
    private static Rect Sw(int x, int y) => Px(x + 10, y + 10, x + 54, y + 54, 1024, 0);
    private static Rect B_CREAM => Sw(512, 576);
    private static Rect B_RED => Sw(704, 576);
    private static Rect B_WHITE => Sw(768, 576);
    private static Rect B_YELLOW => Sw(640, 576);
    private static Rect B_BLUE => Sw(576, 640);
    private static Rect B_CORAL => Sw(832, 640);
    private static Rect B_TEAL => Sw(960, 640);
    private static Rect B_PINK => Sw(768, 768);
    private static Rect B_NAVYDOTS => Sw(896, 576);
    private static Rect B_GREEN => Sw(0, 960);
    private static Rect B_KRAFT => Sw(896, 832);
    private static Rect Letter(char c)
    {
        int i = "GELUKIVRJAD".IndexOf(c);
        return Px(i * 64, 768, i * 64 + 64, 896);
    }

    [MenuItem("Oncologie/Bouw IRL-keuken")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "IRL_Kitchen")
        {
            EditorUtility.DisplayDialog("IRL-keuken", "Open eerst Assets/Scenes/IRL_Kitchen.unity.", "OK");
            return;
        }

        PrepareAssets();
        RemoveOld(scene);

        var root = new GameObject(RootName).transform;
        var room = Group("Room", root);
        var kitchen = Group("KitchenRun", root);
        var dining = Group("Dining", root);
        var decor = Group("Decor", root);
        var interact = Group("Interactables", root);
        var setBirthday = Group("Set_Birthday", root);
        var setGrey = Group("Set_Grey", root);
        var setFuture = Group("Set_Future", root);

        BuildRoom(room);
        BuildKitchenRun(kitchen);
        BuildDining(dining);
        var decorRefs = BuildDecor(decor);
        BuildBirthday(setBirthday, out var candles);
        BuildGrey(setGrey);
        BuildFuture(setFuture);
        var lights = BuildLights(root);
        var refs = BuildInteractables(interact, candles);
        var info = BuildInfoCard(root);

        setGrey.gameObject.SetActive(false);
        setFuture.gameObject.SetActive(false);

        WireScene(refs, decorRefs, candles, lights, setBirthday.gameObject, setGrey.gameObject, setFuture.gameObject, info);
        SetupEnvironment();

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[IRLKitchenBuilder] Keuken gebouwd: {_meshCount} meshes, scene opgeslagen.");
    }

    // =====================================================================
    // ASSETS
    // =====================================================================

    private static void PrepareAssets()
    {
        Directory.CreateDirectory(GenDir);
        Directory.CreateDirectory(ResDir);
        AssetDatabase.Refresh();
        AssetDatabase.DeleteAsset(MeshPath);
        _container = null; _meshCount = 0;
        M.Clear();

        Lit("M_FloorTile", "FloorTile"); Lit("M_WallPlaster", "WallPlaster"); Lit("M_KitchenOak", "KitchenOak");
        Lit("M_Worktop", "Worktop"); Lit("M_KitchenAppliance", "KitchenAppliance"); Lit("M_KitchenSteel", "KitchenSteel");
        Lit("M_DiningOak", "DiningOak"); Lit("M_Rug", "Rug"); Lit("M_KitchenProps_Base_B", "KitchenProps_Base_B");
        Lit("M_Set_Birthday58_Atlas", "Set_Birthday58_Atlas");
        Lit("M_Ceiling", "Ceiling"); Lit("M_BacksplashTile", "BacksplashTile"); Lit("M_ChairFabric", "ChairFabric");
        Lit("M_RoomTrim", "RoomTrim");
        var shade = Lit("M_LampShade", "LightDiffuser"); shade.SetFloat("_Cull", 0f);

        var glass = LoadOrCreate("M_Glass", () =>
        {
            var g = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            g.SetColor("_BaseColor", new Color(0.85f, 0.93f, 1f, 0.12f));
            g.SetFloat("_Smoothness", 0.95f);
            MakeTransparent(g);
            return g;
        });
        M["M_Glass"] = glass;

        M["M_Flame"] = LoadOrCreate("M_Flame", () => UnlitMat(new Color(1f, 0.72f, 0.28f), false));
        M["M_Bulb"] = LoadOrCreate("M_Bulb", () => UnlitMat(new Color(1f, 0.93f, 0.8f), false));
        M["M_Highlight"] = LoadOrCreate("M_Highlight", () => { var h = UnlitMat(new Color(1f, 0.85f, 0.35f, 0.5f), true); h.SetFloat("_Cull", 0f); return h; });

        // Basis voor runtime-materialen (hologram, fade) → Resources zodat ze in een build zitten.
        if (AssetDatabase.LoadAssetAtPath<Material>(ResDir + "/M_UnlitTransparent.mat") == null)
            AssetDatabase.CreateAsset(UnlitMat(Color.white, true), ResDir + "/M_UnlitTransparent.mat");
        if (AssetDatabase.LoadAssetAtPath<Material>(ResDir + "/M_UnlitOpaque.mat") == null)
            AssetDatabase.CreateAsset(UnlitMat(Color.white, false), ResDir + "/M_UnlitOpaque.mat");

        M["Sky"] = LoadOrCreate("M_Sky_Garden", () =>
        {
            var s = new Material(Shader.Find("Skybox/Panoramic"));
            s.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/T_Backdrop_Birthday58.png"));
            s.SetFloat("_Mapping", 1f);
            s.SetFloat("_ImageType", 0f);
            s.SetFloat("_Exposure", 1.1f);
            s.SetFloat("_Rotation", 270f);
            return s;
        });
    }

    private static Material Lit(string name, string tex)
    {
        var m = LoadOrCreate(name, () =>
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var bc = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/T_{tex}_BaseColor.png");
            var n = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/T_{tex}_Normal.png");
            var ms = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/T_{tex}_MetallicSmoothness.png");
            if (bc != null) mat.SetTexture("_BaseMap", bc);
            if (n != null) { mat.SetTexture("_BumpMap", n); mat.EnableKeyword("_NORMALMAP"); }
            if (ms != null) { mat.SetTexture("_MetallicGlossMap", ms); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); mat.SetFloat("_Smoothness", 0.8f); }
            else mat.SetFloat("_Smoothness", 0.3f);
            return mat;
        });
        M[name] = m;
        return m;
    }

    private static Material LoadOrCreate(string name, System.Func<Material> create)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = create();
        m.name = name;
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    private static Material UnlitMat(Color c, bool transparent)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetColor("_BaseColor", c);
        if (transparent) MakeTransparent(m);
        return m;
    }

    private static void MakeTransparent(Material m)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetShaderPassEnabled("DepthOnly", false);
        m.SetShaderPassEnabled("ShadowCaster", false);
    }

    // =====================================================================
    // OPRUIMEN
    // =====================================================================

    private static readonly string[] OldRoots =
    {
        RootName, "kitchen", "envelope", "lette_0", "Zone_Trash_Marker", "Rewind_System", "Transition_Fade_Canvas",
        "Kitchen_Ceiling_Light", "Kitchen_PlayerAnchor", "Player_Start", "Outro_Billboard", "TrashCan", "GiveZone", "TrashZone", "Zone_Keys_Marker"
    };

    private static readonly string[] OldComponents =
    {
        "ChoiceWindowManager", "ExperienceBootstrap", "CinematicTransitionDirector", "GreyscaleTransition",
        "ScreeningKitInteraction", "EnvelopeGiveInteraction", "ScreeningZoneVisualizer", "GazeTrigger",
        "ObjectGlowPulse", "InteractiveRewindClock", "PerspectiveSwitcher", "DevDesktopTester"
    };

    private static void RemoveOld(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (var r in scene.GetRootGameObjects())
            if (OldRoots.Contains(r.name)) Object.DestroyImmediate(r);

        foreach (var r in scene.GetRootGameObjects())
        {
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                foreach (var mb in t.GetComponents<MonoBehaviour>())
                    if (mb != null && OldComponents.Contains(mb.GetType().Name)) Object.DestroyImmediate(mb);
            }
        }
    }

    // =====================================================================
    // KAMER
    // =====================================================================

    private static void BuildRoom(Transform p)
    {
        var plaster = M["M_WallPlaster"]; var trim = M["M_RoomTrim"];
        float cz = (Z0 + Z1) * 0.5f, dz = Z1 - Z0, dx = X1 - X0;

        Box("Floor", p, new Vector3(0, -0.05f, cz), new Vector3(dx + 2 * T, 0.1f, dz + 2 * T), M["M_FloorTile"], FaceUV.Tiled(1.8f));
        Box("Ceiling", p, new Vector3(0, H + 0.05f, cz), new Vector3(dx + 2 * T, 0.1f, dz + 2 * T), M["M_Ceiling"], FaceUV.Tiled(2f));
        Box("Wall_Back", p, new Vector3(0, H / 2, Z1 + T / 2), new Vector3(dx + 2 * T, H, T), plaster, FaceUV.Tiled(2.4f));
        Box("Wall_Front", p, new Vector3(0, H / 2, Z0 - T / 2), new Vector3(dx + 2 * T, H, T), plaster, FaceUV.Tiled(2.4f));

        // Linkermuur met raam (z -0.45..0.95, y 0.95..2.15)
        float wz0 = -0.45f, wz1 = 0.95f, wy0 = 0.95f, wy1 = 2.15f, lx = X0 - T / 2;
        Box("Wall_Left_Low", p, new Vector3(lx, wy0 / 2, cz), new Vector3(T, wy0, dz), plaster, FaceUV.Tiled(2.4f));
        Box("Wall_Left_High", p, new Vector3(lx, (wy1 + H) / 2, cz), new Vector3(T, H - wy1, dz), plaster, FaceUV.Tiled(2.4f));
        Box("Wall_Left_A", p, new Vector3(lx, (wy0 + wy1) / 2, (Z0 + wz0) / 2), new Vector3(T, wy1 - wy0, wz0 - Z0), plaster, FaceUV.Tiled(2.4f));
        Box("Wall_Left_B", p, new Vector3(lx, (wy0 + wy1) / 2, (wz1 + Z1) / 2), new Vector3(T, wy1 - wy0, Z1 - wz1), plaster, FaceUV.Tiled(2.4f));
        // Raamkader + vensterbank + glas
        var wf = FaceUV.Atlas(R_WHITE);
        Box("Window_Sill", p, new Vector3(X0 + 0.07f, wy0 - 0.02f, (wz0 + wz1) / 2), new Vector3(0.26f, 0.04f, wz1 - wz0 + 0.12f), trim, wf);
        Box("Window_Top", p, new Vector3(X0 - 0.02f, wy1 - 0.03f, (wz0 + wz1) / 2), new Vector3(0.08f, 0.06f, wz1 - wz0), trim, wf);
        Box("Window_SideA", p, new Vector3(X0 - 0.02f, (wy0 + wy1) / 2, wz0 + 0.03f), new Vector3(0.08f, wy1 - wy0, 0.06f), trim, wf);
        Box("Window_SideB", p, new Vector3(X0 - 0.02f, (wy0 + wy1) / 2, wz1 - 0.03f), new Vector3(0.08f, wy1 - wy0, 0.06f), trim, wf);
        Box("Window_Mullion", p, new Vector3(X0 - 0.02f, (wy0 + wy1) / 2, (wz0 + wz1) / 2), new Vector3(0.06f, wy1 - wy0, 0.05f), trim, wf);
        Box("Window_Transom", p, new Vector3(X0 - 0.02f, 1.75f, (wz0 + wz1) / 2), new Vector3(0.06f, 0.05f, wz1 - wz0), trim, wf);
        var glass = QuadObj("Window_Glass", p, new Vector3(X0 - 0.03f, (wy0 + wy1) / 2, (wz0 + wz1) / 2), new Vector3(0, -90, 0), wz1 - wz0, wy1 - wy0, M["M_Glass"], FaceUV.Tiled(1f), false);
        glass.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

        // Rechtermuur met deur (z -1.75..-0.85, h 2.1)
        float dz0 = -1.75f, dz1 = -0.85f, dh = 2.1f, rx = X1 + T / 2;
        Box("Wall_Right_A", p, new Vector3(rx, H / 2, (Z0 + dz0) / 2), new Vector3(T, H, dz0 - Z0), plaster, FaceUV.Tiled(2.4f));
        Box("Wall_Right_B", p, new Vector3(rx, H / 2, (dz1 + Z1) / 2), new Vector3(T, H, Z1 - dz1), plaster, FaceUV.Tiled(2.4f));
        Box("Wall_Right_Top", p, new Vector3(rx, (dh + H) / 2, (dz0 + dz1) / 2), new Vector3(T, H - dh, dz1 - dz0), plaster, FaceUV.Tiled(2.4f));
        Box("Door", p, new Vector3(X1 + 0.04f, dh / 2, (dz0 + dz1) / 2), new Vector3(0.04f, dh - 0.01f, dz1 - dz0 - 0.01f), trim, wf);
        Box("Door_FrameA", p, new Vector3(X1 - 0.01f, dh / 2, dz0 - 0.03f), new Vector3(0.03f, dh, 0.06f), trim, wf);
        Box("Door_FrameB", p, new Vector3(X1 - 0.01f, dh / 2, dz1 + 0.03f), new Vector3(0.03f, dh, 0.06f), trim, wf);
        Box("Door_FrameTop", p, new Vector3(X1 - 0.01f, dh + 0.03f, (dz0 + dz1) / 2), new Vector3(0.03f, 0.06f, dz1 - dz0 + 0.12f), trim, wf);
        Cyl("Door_Handle", p, new Vector3(X1 - 0.02f, 1.02f, dz1 - 0.1f), 0.012f, 0.012f, 0.1f, M["M_KitchenSteel"], S_STEEL, S_STEEL, 10, new Vector3(0, 0, 90));

        // Plinten
        var bb = FaceUV.Atlas(R_WHITE);
        Box("Baseboard_Front", p, new Vector3(0, 0.04f, Z0 + 0.008f), new Vector3(dx, 0.08f, 0.016f), trim, bb);
        Box("Baseboard_Left", p, new Vector3(X0 + 0.008f, 0.04f, cz), new Vector3(0.016f, 0.08f, dz), trim, bb);
        Box("Baseboard_RightA", p, new Vector3(X1 - 0.008f, 0.04f, (Z0 + dz0) / 2), new Vector3(0.016f, 0.08f, dz0 - Z0), trim, bb);
        Box("Baseboard_RightB", p, new Vector3(X1 - 0.008f, 0.04f, (dz1 + Z1) / 2), new Vector3(0.016f, 0.08f, Z1 - dz1), trim, bb);
        // Plafondlijst
        Box("Cornice_Front", p, new Vector3(0, H - 0.03f, Z0 + 0.02f), new Vector3(dx, 0.06f, 0.04f), trim, bb);
        Box("Cornice_Left", p, new Vector3(X0 + 0.02f, H - 0.03f, cz), new Vector3(0.04f, 0.06f, dz), trim, bb);
        Box("Cornice_Right", p, new Vector3(X1 - 0.02f, H - 0.03f, cz), new Vector3(0.04f, 0.06f, dz), trim, bb);
    }

    // =====================================================================
    // KEUKENBLOK (achterwand)
    // =====================================================================

    private static void BuildKitchenRun(Transform p)
    {
        var oak = M["M_KitchenOak"]; var trim = M["M_RoomTrim"]; var steel = M["M_KitchenSteel"]; var app = M["M_KitchenAppliance"];
        float runX0 = X0, runX1 = 1.3f, front = 1.6f;
        float runCx = (runX0 + runX1) / 2, runW = runX1 - runX0;

        Box("Base_Carcass", p, new Vector3(runCx, 0.49f, 1.91f), new Vector3(runW, 0.78f, 0.58f), trim, FaceUV.Atlas(R_WHITE));
        Box("Base_Plinth", p, new Vector3(runCx, 0.05f, 1.93f), new Vector3(runW, 0.1f, 0.54f), trim, FaceUV.Atlas(R_BLACK));
        Box("Worktop", p, new Vector3(runCx - 0.02f, 0.9f, 1.885f), new Vector3(runW + 0.04f, 0.04f, 0.63f), M["M_Worktop"], FaceUV.Tiled(1.2f));

        // Fronten
        void Door(string n, float x0, float x1, float y0, float y1, bool handleTop = true)
        {
            Box(n, p, new Vector3((x0 + x1) / 2, (y0 + y1) / 2, front + 0.01f), new Vector3(x1 - x0 - 0.004f, y1 - y0 - 0.004f, 0.02f), oak, FaceUV.Tiled(0.9f));
            float hy = handleTop ? y1 - 0.06f : (y0 + y1) / 2;
            Box(n + "_Handle", p, new Vector3((x0 + x1) / 2, hy, front - 0.012f), new Vector3(Mathf.Min(0.16f, (x1 - x0) * 0.5f), 0.012f, 0.02f), steel, FaceUV.Atlas(S_STEEL));
        }
        Door("Door_Sink_L", -2.5f, -1.9f, 0.11f, 0.87f);
        Door("Door_Sink_R", -1.9f, -1.3f, 0.11f, 0.87f);
        Door("Drawer_1", -1.3f, -0.9f, 0.62f, 0.87f, false);
        Door("Drawer_2", -1.3f, -0.9f, 0.37f, 0.62f, false);
        Door("Drawer_3", -1.3f, -0.9f, 0.11f, 0.37f, false);
        Door("Drawer_Oven", -0.9f, -0.3f, 0.11f, 0.2f, false);
        Door("Door_Mid_L", -0.3f, 0.15f, 0.11f, 0.87f);
        Door("Door_Mid_R", 0.15f, 0.6f, 0.11f, 0.87f);
        Door("Door_Dishwasher", 0.6f, 1.3f, 0.11f, 0.87f);

        // Oven + kookplaat + dampkap
        Box("Oven", p, new Vector3(-0.6f, 0.535f, front + 0.01f), new Vector3(0.594f, 0.66f, 0.02f), app, Front(A_OVEN, A_BLACK));
        Box("Oven_Handle", p, new Vector3(-0.6f, 0.78f, front - 0.02f), new Vector3(0.46f, 0.015f, 0.02f), steel, FaceUV.Atlas(S_STEEL));
        Box("Hob", p, new Vector3(-0.6f, 0.924f, 1.9f), new Vector3(0.58f, 0.008f, 0.5f), app, Faces(A_BLACK, A_BLACK, A_HOB, A_BLACK, A_BLACK, A_BLACK));
        Box("Hood_Canopy", p, new Vector3(-0.6f, 1.62f, 1.95f), new Vector3(0.6f, 0.1f, 0.5f), steel, Faces(S_STEEL, S_STEEL, S_STEEL, S_FILTER, S_STEEL, S_STEEL));
        Box("Hood_Chimney", p, new Vector3(-0.6f, 2.135f, 2.08f), new Vector3(0.3f, 0.93f, 0.24f), steel, FaceUV.Atlas(S_STEEL));

        // Spoelbak + kraan
        Box("Sink_Rim", p, new Vector3(-1.9f, 0.926f, 1.88f), new Vector3(0.62f, 0.012f, 0.46f), steel, Faces(S_STEEL, S_STEEL, S_STEEL, S_STEEL, S_STEEL, S_STEEL));
        Box("Sink_Basin", p, new Vector3(-1.9f, 0.9335f, 1.88f), new Vector3(0.5f, 0.002f, 0.36f), steel, Faces(S_DARK, S_DARK, S_DRAIN, S_DARK, S_DARK, S_DARK));
        Cyl("Tap_Post", p, new Vector3(-1.9f, 0.932f, 2.1f), 0.016f, 0.014f, 0.32f, steel, S_STEEL, S_STEEL, 12);
        Box("Tap_Spout", p, new Vector3(-1.9f, 1.24f, 2.0f), new Vector3(0.022f, 0.022f, 0.22f), steel, FaceUV.Atlas(S_STEEL));

        // Achterwand-tegels
        Box("Backsplash", p, new Vector3(runCx, 1.185f, Z1 - 0.006f), new Vector3(runW, 0.53f, 0.012f), M["M_BacksplashTile"], FaceUV.Tiled(0.6f));

        // Bovenkasten
        void Upper(string n, float x0, float x1, int doors)
        {
            Box(n + "_Carcass", p, new Vector3((x0 + x1) / 2, 1.825f, 2.03f), new Vector3(x1 - x0, 0.75f, 0.34f), trim, FaceUV.Atlas(R_WHITE));
            float w = (x1 - x0) / doors;
            for (int i = 0; i < doors; i++)
            {
                float a = x0 + i * w, b = a + w;
                Box($"{n}_Door{i}", p, new Vector3((a + b) / 2, 1.825f, 1.85f), new Vector3(w - 0.004f, 0.746f, 0.02f), oak, FaceUV.Tiled(0.9f));
                Box($"{n}_Handle{i}", p, new Vector3((a + b) / 2, 1.5f, 1.828f), new Vector3(0.14f, 0.012f, 0.02f), steel, FaceUV.Atlas(S_STEEL));
            }
        }
        Upper("Upper_Left", -2.5f, -1.3f, 2);
        Upper("Upper_Right", -0.3f, 1.3f, 2);

        // Microgolf, koffiezet, waterkoker
        Box("Microwave", p, new Vector3(0.25f, 1.07f, 1.97f), new Vector3(0.5f, 0.3f, 0.36f), app, Front(A_MICRO, A_BLACK));
        Box("CoffeeMachine", p, new Vector3(0.9f, 1.08f, 2.02f), new Vector3(0.2f, 0.32f, 0.26f), app, FaceUV.Atlas(A_BLACK));
        Cyl("CoffeePot", p, new Vector3(0.9f, 0.925f, 1.92f), 0.06f, 0.05f, 0.12f, M["M_Glass"], FaceUV.Tiled(1), FaceUV.Tiled(1), 16);
        Cyl("Kettle", p, new Vector3(-1.15f, 0.92f, 2.0f), 0.08f, 0.065f, 0.2f, steel, S_STEEL, S_STEEL, 18);
        Box("TeaTowel", p, new Vector3(-0.6f, 0.66f, front - 0.03f), new Vector3(0.22f, 0.3f, 0.006f), M["M_KitchenProps_Base_B"], Front(P_TOWEL, P_TOWEL));

        // Koelkast + hoge kast
        Box("Fridge", p, new Vector3(1.7f, 0.925f, 1.875f), new Vector3(0.7f, 1.85f, 0.65f), app, FaceUV.Atlas(A_WHITE));
        Box("Fridge_Split", p, new Vector3(1.7f, 1.22f, 1.5485f), new Vector3(0.69f, 0.006f, 0.004f), app, FaceUV.Atlas(A_BLACK));
        Box("Fridge_HandleTop", p, new Vector3(1.39f, 1.5f, 1.53f), new Vector3(0.02f, 0.4f, 0.03f), steel, FaceUV.Atlas(S_STEEL));
        Box("Fridge_HandleBottom", p, new Vector3(1.39f, 0.95f, 1.53f), new Vector3(0.02f, 0.3f, 0.03f), steel, FaceUV.Atlas(S_STEEL));
        Box("TallCabinet", p, new Vector3(2.29f, 1.1f, 1.89f), new Vector3(0.42f, 2.2f, 0.62f), oak, FaceUV.Tiled(0.9f));

        // Papieren op de koelkast (altijd)
        var atlas = M["M_Set_Birthday58_Atlas"];
        QuadObj("Fridge_GroceryList", p, new Vector3(1.97f, 1.6f, 1.5475f), Vector3.zero, 0.1f, 0.15f, atlas, B_GROCERY);
        QuadObj("Fridge_Frituur", p, new Vector3(1.47f, 1.67f, 1.5475f), new Vector3(0, 0, 4), 0.1f, 0.15f, atlas, B_FRITUUR);
        Magnet(p, new Vector3(1.97f, 1.66f, 1.54f), B_YELLOW);
        Magnet(p, new Vector3(1.47f, 1.73f, 1.54f), B_BLUE);
    }

    // =====================================================================
    // EETHOEK
    // =====================================================================

    private static void BuildDining(Transform p)
    {
        var oak = M["M_DiningOak"];
        Vector3 c = new Vector3(0.1f, 0f, -0.15f);
        Box("Table_Top", p, c + new Vector3(0, 0.74f, 0), new Vector3(1.6f, 0.04f, 0.9f), oak, FaceUV.Tiled(1.2f));
        foreach (var sx in new[] { -1, 1 })
            foreach (var sz in new[] { -1, 1 })
                Box("Table_Leg", p, c + new Vector3(sx * 0.72f, 0.36f, sz * 0.37f), new Vector3(0.07f, 0.72f, 0.07f), oak, FaceUV.Tiled(1.2f));
        Box("Table_ApronA", p, c + new Vector3(0, 0.68f, 0.37f), new Vector3(1.38f, 0.08f, 0.025f), oak, FaceUV.Tiled(1.2f));
        Box("Table_ApronB", p, c + new Vector3(0, 0.68f, -0.37f), new Vector3(1.38f, 0.08f, 0.025f), oak, FaceUV.Tiled(1.2f));

        Chair(p, new Vector3(-0.3f, 0, -0.95f), 180);
        Chair(p, new Vector3(0.5f, 0, -0.95f), 180);
        Chair(p, new Vector3(-0.3f, 0, 0.62f), 0);
        Chair(p, new Vector3(0.5f, 0, 0.62f), 0);
        Chair(p, new Vector3(-0.95f, 0, -0.15f), -90);

        var rug = QuadObj("Rug", p, new Vector3(0.1f, 0.004f, -0.15f), new Vector3(90, 0, 0), 2.7f, 1.9f, M["M_Rug"], new Rect(0, 0, 1, 1));
        rug.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    private static GameObject Chair(Transform parent, Vector3 pos, float yaw)
    {
        var oak = M["M_DiningOak"]; var fabric = M["M_ChairFabric"];
        var root = Obj("Chair", parent, pos, new Vector3(0, yaw, 0)).transform;
        foreach (var sx in new[] { -1, 1 })
        {
            foreach (var sz in new[] { -1, 1 })
                Box("Leg", root, new Vector3(sx * 0.19f, 0.215f, sz * 0.18f), new Vector3(0.035f, 0.43f, 0.035f), oak, FaceUV.Tiled(1f));
            Box("BackPost", root, new Vector3(sx * 0.19f, 0.7f, 0.19f), new Vector3(0.035f, 0.5f, 0.035f), oak, FaceUV.Tiled(1f));
        }
        Box("Seat", root, new Vector3(0, 0.45f, 0), new Vector3(0.44f, 0.045f, 0.42f), fabric, FaceUV.Tiled(0.5f));
        Box("Back", root, new Vector3(0, 0.8f, 0.2f), new Vector3(0.38f, 0.26f, 0.03f), fabric, FaceUV.Tiled(0.5f));
        return root.gameObject;
    }

    // =====================================================================
    // DECOR
    // =====================================================================

    private class DecorRefs { public GameObject marcCard, clock, clockRing; }

    private static DecorRefs BuildDecor(Transform p)
    {
        var refs = new DecorRefs();
        var oak = M["M_DiningOak"]; var atlas = M["M_Set_Birthday58_Atlas"]; var props = M["M_KitchenProps_Base_B"];

        // Dressoir aan de linkermuur (naast het raam) met het rouwkaartje van Marc
        Box("Sideboard", p, new Vector3(-2.275f, 0.425f, -1.2f), new Vector3(0.45f, 0.85f, 1.1f), oak, FaceUV.Tiled(1f));
        Box("Sideboard_DoorA", p, new Vector3(-2.04f, 0.43f, -1.47f), new Vector3(0.02f, 0.7f, 0.53f), M["M_KitchenOak"], FaceUV.Tiled(0.9f));
        Box("Sideboard_DoorB", p, new Vector3(-2.04f, 0.43f, -0.93f), new Vector3(0.02f, 0.7f, 0.53f), M["M_KitchenOak"], FaceUV.Tiled(0.9f));
        refs.marcCard = QuadObj("Marc_Rouwkaart", p, new Vector3(-2.3f, 0.94f, -1.05f), new Vector3(-8, -90, 0), 0.12f, 0.18f, atlas, B_MARC, true);
        Box("Marc_Stand", p, new Vector3(-2.33f, 0.87f, -1.05f), new Vector3(0.03f, 0.04f, 0.1f), oak, FaceUV.Tiled(1f));
        Box("PhotoFrame", p, new Vector3(-2.38f, 0.98f, -1.5f), new Vector3(0.02f, 0.26f, 0.2f), atlas, Faces(B_DRAWING, B_KRAFT, B_KRAFT, B_KRAFT, B_KRAFT, B_KRAFT));
        Plant(p, new Vector3(-2.38f, 0.85f, -0.75f), 0.07f);

        // Plant op de vensterbank
        Plant(p, new Vector3(-2.4f, 0.95f, 0.55f), 0.06f);

        // Klok boven het dressoir (= ook de "tijd terugdraaien"-knop op het einde)
        refs.clock = Cyl("Wall_Clock", p, new Vector3(X0 + 0.005f, 1.85f, -1.2f), 0.17f, 0.17f, 0.04f, props, R_BLACK, P_CLOCK, 32, new Vector3(0, 0, -90));
        refs.clock.GetComponent<MeshRenderer>().sharedMaterial = props;
        Box("Clock_HandHour", refs.clock.transform.parent, new Vector3(X0 + 0.05f, 1.88f, -1.18f), new Vector3(0.005f, 0.08f, 0.012f), M["M_RoomTrim"], FaceUV.Atlas(R_BLACK)).transform.localRotation = Quaternion.Euler(30, 0, 0);
        Box("Clock_HandMinute", refs.clock.transform.parent, new Vector3(X0 + 0.052f, 1.9f, -1.24f), new Vector3(0.005f, 0.12f, 0.008f), M["M_RoomTrim"], FaceUV.Atlas(R_BLACK)).transform.localRotation = Quaternion.Euler(-40, 0, 0);
        refs.clockRing = Mesh("Clock_Highlight", p, Ring(0.19f, 0.22f, 40), M["M_Highlight"], new Vector3(X0 + 0.06f, 1.85f, -1.2f), new Vector3(0, 0, -90), false, false);

        // Schilderij aan de rechtermuur, deurmat
        Box("Painting", p, new Vector3(X1 - 0.02f, 1.6f, 0.3f), new Vector3(0.03f, 0.42f, 0.7f), props, Faces(P_PAINTING, P_PAINTING, P_PAINTING, P_PAINTING, P_PAINTING, P_PAINTING)
            .Select((f, i) => i == 1 ? f : FaceUV.Atlas(B_KRAFT)).ToArray()).GetComponent<MeshRenderer>().sharedMaterial = props;
        QuadObj("Doormat", p, new Vector3(X1 - 0.35f, 0.004f, -1.3f), new Vector3(90, 90, 0), 0.8f, 0.5f, props, P_MAT);

        // Hanglamp boven de tafel
        Cyl("Lamp_Cord", p, new Vector3(0.1f, 1.95f, -0.15f), 0.004f, 0.004f, H - 1.95f, M["M_RoomTrim"], R_BLACK, R_BLACK, 6);
        Mesh("Lamp_Shade", p, Cylinder(0.22f, 0.05f, 0.2f, 28, FaceUV.Tiled(1), FaceUV.Tiled(1), false, false), M["M_LampShade"], new Vector3(0.1f, 1.75f, -0.15f), default, false)
            .GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        var bulb = Mesh("Lamp_Bulb", p, Sphere(0.045f, 10, 14, FaceUV.Tiled(1)), M["M_Bulb"], new Vector3(0.1f, 1.78f, -0.15f), default, false);
        bulb.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        return refs;
    }

    private static void Plant(Transform p, Vector3 basePos, float r)
    {
        var props = M["M_KitchenProps_Base_B"];
        Cyl("Plant_Pot", p, basePos, r * 0.8f, r, r * 1.6f, props, P_POT, P_POT, 16);
        for (int i = 0; i < 6; i++)
        {
            float a = i * 60f;
            var leaf = Mesh("Plant_Leaf", p, Sphere(r * 0.7f, 6, 8, P_LEAF), props,
                basePos + new Vector3(0, r * 1.6f + r * 0.6f, 0) + Quaternion.Euler(0, a, 0) * new Vector3(r * 0.5f, 0, 0), new Vector3(0, a, 35), false);
            leaf.transform.localScale = new Vector3(1.6f, 0.35f, 0.8f);
        }
    }

    private static void Magnet(Transform p, Vector3 pos, Rect color)
    {
        Cyl("Magnet", p, pos, 0.016f, 0.016f, 0.012f, M["M_Set_Birthday58_Atlas"], color, color, 14, new Vector3(-90, 0, 0), false);
    }

    // =====================================================================
    // SETS
    // =====================================================================

    private static void BuildBirthday(Transform p, out CandleSet candleSet)
    {
        var atlas = M["M_Set_Birthday58_Atlas"];
        float top = 0.76f;

        // Taart met kaarsjes
        var cake = Obj("Cake", p, new Vector3(0.1f, top, -0.1f));
        Cyl("Cake_Plate", cake.transform, Vector3.zero, 0.17f, 0.17f, 0.012f, atlas, B_WHITE, B_WHITE, 28);
        Cyl("Cake_Body", cake.transform, new Vector3(0, 0.012f, 0), 0.12f, 0.12f, 0.09f, atlas, B_CAKE, B_CREAM, 28);
        var list = new List<Candle>();
        Rect[] cols = { B_PINK, B_BLUE, B_YELLOW, B_WHITE };
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2f;
            var pos = new Vector3(Mathf.Cos(a) * 0.075f, 0.102f, Mathf.Sin(a) * 0.075f);
            var cGo = Cyl("Candle", cake.transform, pos, 0.0045f, 0.0045f, 0.055f, atlas, cols[i % 4], cols[i % 4], 8, default, false);
            var flame = Mesh("Flame", cGo.transform, Sphere(0.5f, 6, 8, FaceUV.Tiled(1)), M["M_Flame"], new Vector3(0, 0.068f, 0), default, false, false);
            flame.transform.localScale = new Vector3(0.011f, 0.022f, 0.011f);
            flame.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var candle = cGo.AddComponent<Candle>();
            candle.flame = flame;
            list.Add(candle);
        }
        candleSet = cake.AddComponent<CandleSet>();
        candleSet.candles = list.ToArray();
        var glowGo = Obj("Candle_Glow", cake.transform, new Vector3(0, 0.25f, 0));
        var glow = glowGo.AddComponent<Light>();
        glow.type = LightType.Point; glow.range = 1.6f; glow.color = new Color(1f, 0.68f, 0.38f); glow.intensity = 0.9f; glow.shadows = LightShadows.None;
        candleSet.glow = glow;

        // Borden en tassen
        foreach (var pos in new[] { new Vector3(-0.3f, top, -0.48f), new Vector3(0.5f, top, -0.48f), new Vector3(-0.3f, top, 0.18f), new Vector3(0.5f, top, 0.18f) })
        {
            Cyl("Plate", p, pos, 0.11f, 0.11f, 0.012f, atlas, B_WHITE, B_WHITE, 24);
            Cyl("Cup", p, pos + new Vector3(0.16f, 0, pos.z < 0 ? 0.06f : -0.06f), 0.036f, 0.04f, 0.085f, atlas, B_WHITE, B_KRAFT, 14);
        }

        // Cadeautje
        Box("Gift", p, new Vector3(0.78f, top + 0.06f, 0.18f), new Vector3(0.18f, 0.12f, 0.14f), atlas, FaceUV.Atlas(B_NAVYDOTS));
        Box("Gift_Ribbon", p, new Vector3(0.78f, top + 0.061f, 0.18f), new Vector3(0.03f, 0.122f, 0.142f), atlas, FaceUV.Atlas(B_RED));

        // Verjaardagskaarten
        QuadObj("Card_Sideboard", p, new Vector3(-2.3f, 0.93f, -1.3f), new Vector3(-8, -90, 0), 0.1f, 0.15f, atlas, B_CARD1);
        QuadObj("Card_Worktop", p, new Vector3(0.62f, 0.995f, 2.12f), new Vector3(-8, 0, 0), 0.1f, 0.15f, atlas, B_CARD2);

        // Ballonnen
        Balloon(p, new Vector3(1.95f, 2.2f, 0.95f), B_RED);
        Balloon(p, new Vector3(2.18f, 2.3f, 0.62f), B_TEAL);
        Balloon(p, new Vector3(1.85f, 2.32f, 0.42f), B_YELLOW);
        Balloon(p, new Vector3(-1.95f, 2.28f, -1.62f), B_CORAL);
        Balloon(p, new Vector3(-1.62f, 2.22f, -1.78f), B_WHITE);
        Balloon(p, new Vector3(-1.78f, 2.36f, -1.45f), B_PINK);

        // Slinger "GELUKKIGE VERJAARDAG" boven het keukenblok
        const string text = "GELUKKIGE VERJAARDAG";
        float x0 = -2.42f, x1 = 1.25f, y = 2.54f, z = 1.82f;
        Box("Bunting_String", p, new Vector3((x0 + x1) / 2, y - 0.03f, z), new Vector3(x1 - x0, 0.004f, 0.004f), atlas, FaceUV.Atlas(B_KRAFT));
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == ' ') continue;
            float t = (i + 0.5f) / text.Length;
            float x = Mathf.Lerp(x0, x1, t);
            float sag = Mathf.Sin(t * Mathf.PI * 3f) * 0.025f;
            QuadObj("Flag_" + text[i], p, new Vector3(x, y - 0.11f - Mathf.Abs(sag), z), new Vector3(0, 0, (i % 2 == 0 ? 3 : -3)), 0.13f, 0.15f, atlas, Letter(text[i]));
        }
    }

    private static void Balloon(Transform p, Vector3 pos, Rect color)
    {
        var atlas = M["M_Set_Birthday58_Atlas"];
        var b = Mesh("Balloon", p, Sphere(0.14f, 12, 16, color), atlas, pos, default, false);
        b.transform.localScale = new Vector3(1f, 1.18f, 1f);
        Cyl("Balloon_Knot", p, pos + new Vector3(0, -0.18f, 0), 0.012f, 0.004f, 0.02f, atlas, color, color, 8, default, false);
        Box("Balloon_String", p, pos + new Vector3(0, -0.18f - 0.5f, 0), new Vector3(0.003f, 1.0f, 0.003f), atlas, FaceUV.Atlas(B_WHITE)).GetComponent<Collider>().enabled = false;
    }

    private static void BuildGrey(Transform p)
    {
        var atlas = M["M_Set_Birthday58_Atlas"];
        float top = 0.76f;
        // Luc's lege stoel aan het hoofd van de tafel, met een dekentje
        Chair(p, new Vector3(1.3f, 0, -0.15f), 90);
        Box("Blanket_Seat", p, new Vector3(1.3f, 0.49f, -0.15f), new Vector3(0.42f, 0.04f, 0.44f), atlas, FaceUV.Atlas(B_TEAL));
        Box("Blanket_Fold", p, new Vector3(1.09f, 0.36f, -0.15f), new Vector3(0.03f, 0.24f, 0.44f), atlas, FaceUV.Atlas(B_TEAL));
        // Medicijnen, glas water, een kop koffie voor jou
        Box("Pills_A", p, new Vector3(0.0f, top + 0.0125f, -0.3f), new Vector3(0.07f, 0.025f, 0.045f), atlas, FaceUV.Atlas(B_WHITE));
        Box("Pills_B", p, new Vector3(0.09f, top + 0.0125f, -0.25f), new Vector3(0.06f, 0.025f, 0.04f), atlas, FaceUV.Atlas(B_BLUE));
        Cyl("Pill_Bottle", p, new Vector3(0.18f, top, -0.32f), 0.025f, 0.025f, 0.08f, atlas, B_CORAL, B_WHITE, 12);
        Cyl("Water_Glass", p, new Vector3(-0.08f, top, -0.2f), 0.033f, 0.036f, 0.1f, M["M_Glass"], FaceUV.Tiled(1), FaceUV.Tiled(1), 16);
        Cyl("Coffee_Cup", p, new Vector3(0.12f, top, -0.5f), 0.036f, 0.04f, 0.085f, atlas, B_WHITE, B_KRAFT, 14);
        // Afspraakkaartjes op de koelkast
        QuadObj("Fridge_Appointments", p, new Vector3(1.72f, 1.42f, 1.5475f), Vector3.zero, 0.24f, 0.09f, atlas, B_REMINDER);
        Magnet(p, new Vector3(1.72f, 1.47f, 1.54f), B_RED);
    }

    private static void BuildFuture(Transform p)
    {
        var atlas = M["M_Set_Birthday58_Atlas"];
        // Kindertekening en de nieuwe uitnodiging op de koelkast
        QuadObj("Fridge_Drawing", p, new Vector3(1.72f, 1.45f, 1.5475f), new Vector3(0, 0, -3), 0.18f, 0.24f, atlas, B_DRAWING);
        Magnet(p, new Vector3(1.72f, 1.56f, 1.54f), B_RED);
        QuadObj("Fridge_NewEnvelope", p, new Vector3(1.62f, 1.05f, 1.5475f), new Vector3(0, 0, 2), 0.22f, 0.11f, atlas, B_ENV);
        Magnet(p, new Vector3(1.62f, 1.1f, 1.54f), B_YELLOW);
        // Bloemen op tafel en twee ballonnen
        Cyl("Vase", p, new Vector3(0.1f, 0.76f, -0.1f), 0.045f, 0.055f, 0.18f, M["M_Glass"], FaceUV.Tiled(1), FaceUV.Tiled(1), 16);
        Rect[] flowerCols = { B_YELLOW, B_RED, B_PINK, B_CORAL, B_YELLOW };
        for (int i = 0; i < 5; i++)
        {
            float a = i * 72f * Mathf.Deg2Rad;
            Vector3 head = new Vector3(0.1f + Mathf.Cos(a) * 0.05f, 1.02f + (i % 2) * 0.03f, -0.1f + Mathf.Sin(a) * 0.05f);
            Box("Stem", p, (head + new Vector3(0.1f, 0.85f, -0.1f)) / 2f, new Vector3(0.004f, head.y - 0.85f, 0.004f), atlas, FaceUV.Atlas(B_GREEN)).GetComponent<Collider>().enabled = false;
            Mesh("Flower", p, Sphere(0.03f, 6, 8, flowerCols[i]), atlas, head, default, false);
        }
        Balloon(p, new Vector3(1.95f, 2.25f, 0.9f), B_TEAL);
        Balloon(p, new Vector3(-1.85f, 2.3f, -1.6f), B_YELLOW);
    }

    // =====================================================================
    // LICHT
    // =====================================================================

    private class LightRefs { public Light sun, lamp; }

    private static LightRefs BuildLights(Transform root)
    {
        var refs = new LightRefs();
        var sunGo = GameObject.Find("Directional Light (Sunlight)"); if (sunGo == null) sunGo = new GameObject("Directional Light (Sunlight)");
        var sun = GetOrAdd<Light>(sunGo);
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(32f, 78f, 0f); // door het raam (linkermuur) naar binnen
        sun.color = new Color(1f, 0.93f, 0.8f);
        sun.intensity = 1.3f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.75f;
        refs.sun = sun;

        var lampGo = Obj("Lamp_Light", root, new Vector3(0.1f, 1.7f, -0.15f));
        var lamp = lampGo.AddComponent<Light>();
        lamp.type = LightType.Point; lamp.range = 5f; lamp.intensity = 2.2f; lamp.color = new Color(1f, 0.86f, 0.68f);
        lamp.shadows = LightShadows.None;
        refs.lamp = lamp;

        var kitchenGo = Obj("Kitchen_Light", root, new Vector3(-0.6f, 2.45f, 1.4f));
        var kl = kitchenGo.AddComponent<Light>();
        kl.type = LightType.Point; kl.range = 3.5f; kl.intensity = 0.9f; kl.color = new Color(1f, 0.9f, 0.78f); kl.shadows = LightShadows.None;
        return refs;
    }

    private static void SetupEnvironment()
    {
        RenderSettings.skybox = M["Sky"];
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.64f, 0.68f);
        RenderSettings.ambientEquatorColor = new Color(0.5f, 0.46f, 0.42f);
        RenderSettings.ambientGroundColor = new Color(0.26f, 0.23f, 0.2f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.fog = false;
        RenderSettings.reflectionIntensity = 0.6f;
        DynamicGI.UpdateEnvironment();
    }

    // =====================================================================
    // INTERACTIE
    // =====================================================================

    private class InteractRefs
    {
        public EnvelopeItem envelope; public LighterItem lighter; public DropZone lucZone, trashZone; public LucCharacter luc; public Transform playerStart;
    }

    private static InteractRefs BuildInteractables(Transform p, CandleSet candles)
    {
        var refs = new InteractRefs();
        var atlas = M["M_Set_Birthday58_Atlas"]; var steel = M["M_KitchenSteel"]; var hl = M["M_Highlight"];

        // ---- Envelop op de koelkast ----
        var env = Box("Envelope", p, new Vector3(1.72f, 1.42f, 1.543f), new Vector3(0.22f, 0.11f, 0.004f), atlas,
            Faces(B_WHITE, B_WHITE, B_WHITE, B_WHITE, B_WHITE, B_ENV));
        MakeDynamic(env);
        var envItem = env.AddComponent<EnvelopeItem>();
        envItem.displayName = "envelop"; envItem.useVerb = "brief lezen"; envItem.desktopHoldEuler = new Vector3(0, 0, 0);
        AddGrab(env);
        var aura = Mesh("Envelope_Aura", env.transform, Quad(0.27f, 0.16f, FaceUV.Tiled(1)), hl, new Vector3(0, 0, 0.0015f), default, false, false);
        envItem.highlighter = AddHighlighter(env, aura);
        envItem.letterView = BuildLetter(env.transform);
        QuadObj("Fridge_ReminderLetter", p, new Vector3(1.66f, 1.47f, 1.5476f), new Vector3(0, 0, -4), 0.24f, 0.09f, atlas, B_REMINDER);
        Magnet(p, new Vector3(1.72f, 1.47f, 1.538f), B_RED);
        refs.envelope = envItem;

        // ---- Aansteker op tafel ----
        var lighter = Obj("Lighter", p, new Vector3(-0.08f, 0.766f, -0.5f), new Vector3(90, 0, 25));
        var lb = Box("Lighter_Body", lighter.transform, Vector3.zero, new Vector3(0.024f, 0.075f, 0.012f), atlas, FaceUV.Atlas(B_RED));
        Object.DestroyImmediate(lb.GetComponent<Collider>());
        Box("Lighter_Top", lighter.transform, new Vector3(0, 0.043f, 0), new Vector3(0.024f, 0.012f, 0.012f), steel, FaceUV.Atlas(S_STEEL)).GetComponent<Collider>().enabled = false;
        var lcol = lighter.AddComponent<BoxCollider>(); lcol.size = new Vector3(0.03f, 0.1f, 0.02f); lcol.center = new Vector3(0, 0.01f, 0);
        MakeDynamic(lighter);
        var tip = Obj("FlameTip", lighter.transform, new Vector3(0, 0.07f, 0));
        var lflame = Mesh("Lighter_Flame", lighter.transform, Sphere(0.5f, 6, 8, FaceUV.Tiled(1)), M["M_Flame"], new Vector3(0, 0.064f, 0), default, false, false);
        lflame.transform.localScale = new Vector3(0.012f, 0.028f, 0.012f);
        var li = lighter.AddComponent<LighterItem>();
        li.displayName = "aansteker"; li.flame = lflame; li.flameTip = tip.transform; li.candles = candles;
        AddGrab(lighter);
        var lring = Mesh("Lighter_Highlight", p, Ring(0.07f, 0.085f, 32), hl, new Vector3(-0.08f, 0.763f, -0.5f), default, false, false);
        li.highlighter = AddHighlighter(lighter, lring);
        refs.lighter = li;

        // ---- Vuilbak (keuze A) ----
        var bin = Obj("TrashBin", p, new Vector3(-2.18f, 0, 1.3f));
        Cyl("Bin_Body", bin.transform, Vector3.zero, 0.14f, 0.15f, 0.5f, steel, S_STEEL, S_DARK, 24);
        Box("Bin_Pedal", bin.transform, new Vector3(0, 0.03f, -0.16f), new Vector3(0.1f, 0.015f, 0.06f), steel, FaceUV.Atlas(S_DARK));
        var hinge = Obj("Bin_LidHinge", bin.transform, new Vector3(0, 0.5f, 0.15f));
        var lidGo = Cyl("Bin_Lid", hinge.transform, new Vector3(0, 0, -0.15f), 0.155f, 0.15f, 0.025f, steel, S_STEEL, S_STEEL, 24);
        lidGo.GetComponent<Collider>().enabled = false;
        GameObjectUtility.SetStaticEditorFlags(lidGo, 0);
        var trash = Obj("Zone_Trash", bin.transform, new Vector3(0, 0.75f, 0));
        var tcol = trash.AddComponent<BoxCollider>(); tcol.size = new Vector3(0.5f, 0.6f, 0.5f); tcol.isTrigger = true;
        var tsnap = Obj("Snap", bin.transform, new Vector3(0, 0.3f, 0), new Vector3(70, 20, 0));
        var tz = trash.AddComponent<DropZone>();
        tz.choice = ScreeningChoice.IgnoredTrash; tz.snapPoint = tsnap.transform; tz.hideOnArrive = true;
        tz.lid = hinge.transform; tz.lidOpenEuler = new Vector3(75, 0, 0);
        var tvis = Obj("Trash_Highlight", trash.transform, Vector3.zero);
        Mesh("Ring", tvis.transform, Ring(0.2f, 0.235f, 40), hl, new Vector3(0, -0.12f, 0), default, false, false);
        Label(tvis.transform, "Weggooien", new Vector3(0, 0.3f, 0));
        tz.highlighter = AddHighlighter(trash, tvis);
        refs.trashZone = tz;

        // ---- Luc ----
        var lucGo = GameObject.Find("Luc"); if (lucGo == null) lucGo = GameObject.Find("Scavenger");
        if (lucGo != null)
        {
            lucGo.name = "Luc";
            lucGo.transform.SetPositionAndRotation(new Vector3(1.32f, 0, -0.15f), Quaternion.Euler(0, -90, 0));
            // Schaal naar een realistische 1,75 m en voeten op de vloer.
            lucGo.transform.localScale = Vector3.one;
            var lb2 = new Bounds(lucGo.transform.position, Vector3.zero);
            foreach (var rr in lucGo.GetComponentsInChildren<SkinnedMeshRenderer>()) if (rr.gameObject.activeInHierarchy && !rr.name.Contains("backpack")) lb2.Encapsulate(rr.bounds);
            if (lb2.size.y > 0.5f)
            {
                lucGo.transform.localScale = Vector3.one * (1.75f / lb2.size.y);
                lb2 = new Bounds(lucGo.transform.position, Vector3.zero);
                foreach (var rr in lucGo.GetComponentsInChildren<SkinnedMeshRenderer>()) if (!rr.name.Contains("backpack")) lb2.Encapsulate(rr.bounds);
                lucGo.transform.position += Vector3.up * (0f - lb2.min.y);
            }
            var backpack = lucGo.transform.Find("Scavenger_mesh_backpack");
            if (backpack != null) backpack.gameObject.SetActive(false);
            foreach (var old in new[] { "Luc_Envelope_GiveTrigger", "Envelope_Hand_Trigger", "Abdomen_FocusPoint" })
            {
                var o = lucGo.transform.Find(old);
                if (o != null) Object.DestroyImmediate(o.gameObject);
            }
            var anim = lucGo.GetComponent<Animator>();
            if (anim != null) anim.runtimeAnimatorController = LucController();
            refs.luc = GetOrAdd<LucCharacter>(lucGo);
        }

        // ---- Luc's hand (keuze B) ----
        var give = Obj("Zone_GiveToLuc", p, new Vector3(1.0f, 1.02f, -0.15f));
        var gcol = give.AddComponent<SphereCollider>(); gcol.radius = 0.32f; gcol.isTrigger = true;
        var gsnap = Obj("Snap", give.transform, new Vector3(0.12f, -0.04f, 0.08f), new Vector3(70, -90, 0));
        var gz = give.AddComponent<DropZone>();
        gz.choice = ScreeningChoice.InsistedKeys; gz.snapPoint = gsnap.transform;
        var gvis = Obj("Give_Highlight", give.transform, Vector3.zero);
        Mesh("Ring", gvis.transform, Ring(0.2f, 0.23f, 40), hl, Vector3.zero, new Vector3(0, 0, 90), false, false);
        Label(gvis.transform, "Geef aan Luc", new Vector3(0, 0.32f, 0));
        gz.highlighter = AddHighlighter(give, gvis);
        refs.lucZone = gz;

        // ---- Startpunt speler: staand achter de stoelen, kijkend naar de tafel en Luc ----
        refs.playerStart = Obj("Player_Start", p, new Vector3(0.1f, 0f, -1.55f)).transform;
        return refs;
    }

    private static void MakeDynamic(GameObject go)
    {
        GameObjectUtility.SetStaticEditorFlags(go, 0);
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        var rb = GetOrAdd<Rigidbody>(go);
        rb.isKinematic = true; rb.useGravity = false; rb.mass = 0.05f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private static void AddGrab(GameObject go)
    {
        var g = go.AddComponent<XRGrabInteractable>();
        g.movementType = XRBaseInteractable.MovementType.Instantaneous;
        g.throwOnDetach = false;
        g.useDynamicAttach = true;
    }

    private static Highlighter AddHighlighter(GameObject owner, GameObject visual)
    {
        var h = owner.AddComponent<Highlighter>();
        h.visual = visual;
        visual.SetActive(false);
        return h;
    }

    private static void Label(Transform parent, string text, Vector3 localPos)
    {
        var canvas = WorldUI.CreateCanvas("Label", parent, new Vector2(380, 90), 0.0009f);
        canvas.transform.localPosition = localPos;
        WorldUI.AddPanel(canvas.transform, new Color(0.05f, 0.05f, 0.05f, 0.65f), Vector2.zero, Vector2.one);
        WorldUI.AddText(canvas.transform, text, 46, new Color(1f, 0.9f, 0.55f), TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, default, default, FontStyle.Bold);
        canvas.gameObject.AddComponent<Billboard>();
    }

    private static GameObject BuildLetter(Transform envelope)
    {
        var canvas = WorldUI.CreateCanvas("LetterView", envelope, new Vector2(900, 660), 0.00053f);
        var teal = new Color(0.1f, 0.5f, 0.53f);
        WorldUI.AddPanel(canvas.transform, new Color(0.98f, 0.97f, 0.94f, 1f), Vector2.zero, Vector2.one);
        WorldUI.AddPanel(canvas.transform, teal, new Vector2(0, 0.88f), Vector2.one);
        WorldUI.AddText(canvas.transform, "Gratis bevolkingsonderzoek dikkedarmkanker", 30, Color.white, TextAnchor.MiddleLeft,
            new Vector2(0, 0.88f), Vector2.one, new Vector2(30, 0), new Vector2(-20, 0), FontStyle.Bold);
        WorldUI.AddText(canvas.transform, "Herinnering", 46, teal, TextAnchor.UpperLeft, new Vector2(0, 0.74f), new Vector2(1, 0.86f), new Vector2(30, 0), new Vector2(-30, 0), FontStyle.Bold);
        WorldUI.AddText(canvas.transform,
            "Beste meneer Janssens,\n\n" +
            "Een tijd geleden kreeg u een uitnodiging voor het gratis bevolkingsonderzoek naar dikkedarmkanker. We ontvingen uw staal nog niet.\n\n" +
            "•  De test doet u thuis, in een paar minuten.\n" +
            "•  Hij zoekt naar bloed in de stoelgang dat u zelf niet kan zien.\n" +
            "•  Dikkedarmkanker geeft in het begin meestal geen klachten.\n\n" +
            "Stuur het staal op met de bijgevoegde antwoordenvelop.",
            27, new Color(0.15f, 0.15f, 0.15f), TextAnchor.UpperLeft, new Vector2(0, 0.1f), new Vector2(1, 0.74f), new Vector2(30, 0), new Vector2(-30, 0));
        WorldUI.AddText(canvas.transform, "Voor iedereen van 50 tot 74 jaar  ·  om de 2 jaar  ·  bevolkingsonderzoek.be", 22, teal, TextAnchor.MiddleCenter,
            new Vector2(0, 0), new Vector2(1, 0.1f));
        canvas.gameObject.SetActive(false);
        return canvas.gameObject;
    }

    private static GameObject BuildInfoCard(Transform root)
    {
        var canvas = WorldUI.CreateCanvas("InfoCard_Einde", root, new Vector2(1000, 560), 0.00095f);
        canvas.transform.position = new Vector3(0.1f, 1.38f, 0.05f);
        var teal = new Color(0.1f, 0.5f, 0.53f);
        WorldUI.AddPanel(canvas.transform, new Color(0.98f, 0.97f, 0.94f, 0.96f), Vector2.zero, Vector2.one);
        WorldUI.AddPanel(canvas.transform, teal, new Vector2(0, 0.8f), Vector2.one);
        WorldUI.AddText(canvas.transform, "Bevolkingsonderzoek Dikkedarmkanker", 44, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 0.8f), Vector2.one, default, default, FontStyle.Bold);
        WorldUI.AddText(canvas.transform,
            "Voor iedereen van <b>50 tot 74 jaar</b>\n<b>Gratis</b>  ·  om de <b>2 jaar</b>  ·  een eenvoudige test, <b>gewoon thuis</b>\n\n" +
            "De test vindt bloed dat je zelf niet ziet, lang vóór je iets voelt.\n\n" +
            "<i>Ligt er bij jou thuis, of bij iemand die je graag ziet, ook een envelop te wachten?</i>",
            32, new Color(0.15f, 0.15f, 0.15f), TextAnchor.MiddleCenter, new Vector2(0, 0.12f), new Vector2(1, 0.8f), new Vector2(30, 0), new Vector2(-30, 0));
        WorldUI.AddText(canvas.transform, "bevolkingsonderzoek.be", 30, teal, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(1, 0.12f), default, default, FontStyle.Bold);
        canvas.gameObject.SetActive(false);
        return canvas.gameObject;
    }

    private static RuntimeAnimatorController LucController()
    {
        const string path = "Assets/Models/Characters/Luc.controller";
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (existing != null) return existing;
        Directory.CreateDirectory("Assets/Models/Characters");
        AssetDatabase.Refresh();
        var src = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Homeless_people/Free_Scavenger/Animation/Free_scavenger.controller");
        Motion motion = src != null ? src.layers[0].stateMachine.defaultState?.motion : null;
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var state = ctrl.layers[0].stateMachine.AddState("Pose");
        state.motion = motion;
        var layers = ctrl.layers;
        layers[0].iKPass = true;
        ctrl.layers = layers;
        return ctrl;
    }

    // =====================================================================
    // KOPPELEN
    // =====================================================================

    private static void WireScene(InteractRefs r, DecorRefs d, CandleSet candles, LightRefs lights, GameObject birthday, GameObject grey, GameObject future, GameObject info)
    {
        var gm = GameObject.Find("GameManagers"); if (gm == null) gm = new GameObject("GameManagers");

        // Speler-rig
        var rig = GetOrAdd<PlayerRigSelector>(gm);
        rig.desktopPlayer = FindInactive("Test Player");
        rig.xrRig = FindInactive("XR Origin (XR Rig)");
        rig.startPoint = r.playerStart;
        if (rig.desktopPlayer != null)
        {
            rig.desktopPlayer.transform.SetPositionAndRotation(r.playerStart.position, r.playerStart.rotation);
            rig.desktopPlayer.transform.localScale = Vector3.one;
        }
        if (rig.xrRig != null) { rig.xrRig.transform.SetPositionAndRotation(r.playerStart.position, r.playerStart.rotation); rig.xrRig.SetActive(false); }

        // Transitie
        var tr = GetOrAdd<ColonTransition>(gm);
        tr.luc = r.luc; tr.lucFeet = r.luc != null ? r.luc.transform : null;

        // Klok = tijd terugdraaien
        var clickable = GetOrAdd<ClickableObject>(d.clock);
        clickable.hint = "tijd terugdraaien";
        if (d.clock.GetComponent<XRSimpleInteractable>() == null) d.clock.AddComponent<XRSimpleInteractable>();
        GameObjectUtility.SetStaticEditorFlags(d.clock, 0);
        var clockHl = AddHighlighter(d.clock, d.clockRing);

        // Rouwkaartje = kijk-trigger
        var look = GetOrAdd<LookAtTrigger>(d.marcCard);
        look.requiredSeconds = 1.5f;

        var flow = GetOrAdd<ExperienceFlowManager>(gm);
        flow.luc = r.luc; flow.envelope = r.envelope; flow.lighter = r.lighter; flow.candles = candles;
        flow.lucZone = r.lucZone; flow.trashZone = r.trashZone; flow.marcCard = look; flow.transition = tr;
        flow.rewindClock = clickable; flow.rewindClockHighlight = clockHl;
        flow.setBirthday = birthday; flow.setGrey = grey; flow.setFuture = future; flow.infoCard = info;
        flow.sun = lights.sun; flow.ceilingLamp = lights.lamp;
        EditorUtility.SetDirty(flow);
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static GameObject FindInactive(string name)
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t.name == name && t.hideFlags == HideFlags.None && t.gameObject.scene.IsValid()) return t.gameObject;
        return null;
    }

    // =====================================================================
    // MESH-HULPFUNCTIES
    // =====================================================================

    private static FaceUV[] Front(Rect front, Rect rest) => Faces(rest, rest, rest, rest, rest, front);

    private static FaceUV[] Faces(params Rect[] r) => r.Select(x => FaceUV.Atlas(x)).ToArray();

    private static Transform Group(string name, Transform parent) => Obj(name, parent, Vector3.zero).transform;

    private static GameObject Obj(string name, Transform parent, Vector3 pos, Vector3 euler = default)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        return go;
    }

    private static Mesh Save(Mesh m, string name)
    {
        m.name = $"{name}_{_meshCount++}";
        if (_container == null) { AssetDatabase.CreateAsset(m, MeshPath); _container = m; }
        else AssetDatabase.AddObjectToAsset(m, _container);
        return m;
    }

    private static GameObject Mesh(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 euler = default, bool collider = true, bool isStatic = true)
    {
        var go = Obj(name, parent, pos, euler);
        go.AddComponent<MeshFilter>().sharedMesh = Save(mesh, name);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (collider)
        {
            var bc = go.AddComponent<BoxCollider>();
            bc.center = mesh.bounds.center;
            bc.size = Vector3.Max(mesh.bounds.size, Vector3.one * 0.01f);
        }
        if (isStatic) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    private static GameObject Box(string name, Transform p, Vector3 center, Vector3 size, Material mat, params FaceUV[] uv)
        => Mesh(name, p, MeshFactory.Box(size, uv), mat, center);

    private static GameObject Cyl(string name, Transform p, Vector3 basePos, float rb, float rt, float h, Material mat, FaceUV side, FaceUV cap, int seg = 20, Vector3 euler = default, bool collider = true)
        => Mesh(name, p, Cylinder(rb, rt, h, seg, side, cap), mat, basePos, euler, collider);

    private static GameObject QuadObj(string name, Transform p, Vector3 pos, Vector3 euler, float w, float h, Material mat, FaceUV uv, bool collider = false)
    {
        var go = Mesh(name, p, Quad(w, h, uv), mat, pos, euler, false);
        if (collider) { var bc = go.AddComponent<BoxCollider>(); bc.size = new Vector3(w, h, 0.02f); }
        go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }
}
