using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

// Read-only validation + offscreen screenshot capture for the expanded MainForest.
public static class ForestValidationEditor
{
    private const int NewXMin = -41, NewXMax = 89, NewYMin = -62, NewYMax = 68;
    private const int OrigXMin = -41, OrigXMax = 47, OrigYMin = -31, OrigYMax = 37;

    [MenuItem("Tools/Forest Editor/Validation/Validate Expansion (Read Only)")]
    public static void Validate()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.EndsWith("MainForest.unity"))
            scene = EditorSceneManager.OpenScene("Assets/Scenes/MainForest.unity", OpenSceneMode.Single);

        var grid = GameObject.Find("Grid");
        var ground = grid.transform.Find("Ground").GetComponent<Tilemap>();
        var paths = grid.transform.Find("Paths").GetComponent<Tilemap>();
        ground.CompressBounds(); paths.CompressBounds();
        Debug.Log($"[Val] Ground bounds {ground.cellBounds}  Paths bounds {paths.cellBounds}");

        // 1. No bright-green tiles anywhere in ground.
        var bad = new Dictionary<string, int>();
        var gb = ground.cellBounds;
        int total = 0;
        foreach (var pos in gb.allPositionsWithin)
        {
            var t = ground.GetTile(pos);
            if (t == null) continue;
            total++;
            string n = t.name;
            if (!n.StartsWith("pixelgreenground")) bad[n] = bad.TryGetValue(n, out var c) ? c + 1 : 1;
        }
        Debug.Log($"[Val] Ground non-pixelgreenground tiles: {(bad.Count == 0 ? "NONE" : string.Join(", ", bad.Select(k => k.Key + "=" + k.Value)))} (total ground {total})");

        // 2. Road correctness: every road row must be exactly cols -24,-23,-22 with the right tiles.
        int roadRows = 0, roadErrors = 0; string firstErr = "";
        int[] baseByPhase = { 3, 0, 6 };
        for (int y = NewYMin; y < NewYMax; y++)
        {
            int p = ((y % 3) + 3) % 3; int rb = baseByPhase[p];
            bool rowOk = true;
            for (int x = -24; x <= -22; x++)
            {
                var t = paths.GetTile(new Vector3Int(x, y, 0));
                string want = "road_" + (rb + (x + 24));
                if (t == null || t.name != want) { rowOk = false; if (firstErr == "") firstErr = $"y={y} x={x} got {(t == null ? "null" : t.name)} want {want}"; }
            }
            // ensure no stray road tiles outside the 3 columns on this row
            if (paths.GetTile(new Vector3Int(-25, y, 0)) != null || paths.GetTile(new Vector3Int(-21, y, 0)) != null)
            { rowOk = false; if (firstErr == "") firstErr = $"y={y} stray tile outside road columns"; }
            roadRows++; if (!rowOk) roadErrors++;
        }
        Debug.Log($"[Val] Road rows checked {roadRows}, rows with errors {roadErrors}. {(roadErrors > 0 ? "First: " + firstErr : "Dashes locked to x=-23, width 3, continuous.")}");

        // 3. Old N/S/E border bands should now be sparse (opened). Count scatter objects still inside them.
        var forest = ForestEditorHierarchy.GroupGO(ForestEditorHierarchy.HandPlaced);
        string[] cont = { "treenormal", "darktrees", "bushes", "stumps", "Logs" };
        int inNorthBand = 0, inSouthBand = 0, inEastBand = 0;
        foreach (var cn in cont)
        {
            var c = forest.transform.Find(cn); if (c == null) continue;
            foreach (Transform k in c)
            {
                Vector2 pp = k.position;
                if (pp.y >= OrigYMax - 10 && pp.y < OrigYMax && pp.x < OrigXMax - 10 && pp.x > OrigXMin + 10) inNorthBand++;
                if (pp.y <= OrigYMin + 10 && pp.y > OrigYMin && pp.x < OrigXMax - 10 && pp.x > OrigXMin + 10) inSouthBand++;
                if (pp.x >= OrigXMax - 10 && pp.x < OrigXMax && pp.y < OrigYMax - 10 && pp.y > OrigYMin + 10) inEastBand++;
            }
        }
        Debug.Log($"[Val] Original-container scatter still inside old N/S/E bands: north={inNorthBand} south={inSouthBand} east={inEastBand} (should be ~0)");

        // 4. Realized interior density in a few new-area sample rects.
        void Density(string label, Rect r)
        {
            int n = 0;
            foreach (var root in new[] { "MapExpansion_North", "MapExpansion_East", "MapExpansion_South", ForestEditorHierarchy.HandPlaced })
            {
                var go = ForestEditorHierarchy.GroupGO(root); if (go == null) continue;
                foreach (var sr in go.GetComponentsInChildren<Transform>())
                    if (sr.childCount == 0 && r.Contains((Vector2)sr.position)) n++;
            }
            Debug.Log($"[Val] Density {label} {r}: {n} objs => {n / (r.width * r.height):F4}/unit^2");
        }
        Density("orig-center", new Rect(-20, -25, 40, 20));
        Density("new-north", new Rect(-10, 40, 40, 20));
        Density("new-east", new Rect(52, -10, 30, 30));
        Density("new-south", new Rect(-10, -58, 40, 20));

        // 5. Outer border gap check: scan each edge band for the longest run with no border object.
        var borderPts = new List<Vector2>();
        var b = ForestEditorHierarchy.GroupGO(ForestEditorHierarchy.OuterBorder);
        foreach (var tr in b.GetComponentsInChildren<Transform>())
            if (tr.GetComponent<SpriteRenderer>() != null) borderPts.Add(tr.position);
        GapScan("NORTH", borderPts.Where(p => p.y > NewYMax - 9).Select(p => p.x), NewXMin, NewXMax, new[] { -26f, -20f });
        GapScan("SOUTH", borderPts.Where(p => p.y < NewYMin + 9).Select(p => p.x), NewXMin, NewXMax, new[] { -26f, -20f });
        GapScan("EAST", borderPts.Where(p => p.x > NewXMax - 9).Select(p => p.y), NewYMin, NewYMax, null);

        // 6. Missing scripts / null components.
        int missing = 0;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            foreach (var comp in go.GetComponents<Component>())
                if (comp == null) missing++;
        Debug.Log($"[Val] Missing/null components in scene: {missing}");

        Debug.Log("[Val] DONE");
    }

    private static void GapScan(string label, IEnumerable<float> coordsEnum, float min, float max, float[] allowedGap)
    {
        var coords = coordsEnum.OrderBy(v => v).ToList();
        if (coords.Count == 0) { Debug.Log($"[Val] Border {label}: NO POINTS"); return; }
        float maxGap = coords[0] - min; float gapAt = min;
        for (int i = 1; i < coords.Count; i++)
        {
            float g = coords[i] - coords[i - 1];
            // ignore the intentional road opening
            bool road = allowedGap != null && coords[i - 1] < allowedGap[1] && coords[i] > allowedGap[0]
                        && (coords[i - 1] >= allowedGap[0] - 3 || coords[i] <= allowedGap[1] + 3);
            if (g > maxGap && !road) { maxGap = g; gapAt = coords[i - 1]; }
        }
        float endGap = max - coords[coords.Count - 1];
        if (endGap > maxGap) { maxGap = endGap; gapAt = coords[coords.Count - 1]; }
        Debug.Log($"[Val] Border {label}: {coords.Count} trees, largest non-road gap {maxGap:F1} units near {gapAt:F0} (player radius ~0.4; gap <~2 = impassable)");
    }

    // ---------- Offscreen capture ----------
    [MenuItem("Tools/Forest Editor/Validation/Capture Screenshots")]
    public static void CaptureAll()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.EndsWith("MainForest.unity"))
            scene = EditorSceneManager.OpenScene("Assets/Scenes/MainForest.unity", OpenSceneMode.Single);

        string dir = "C:/Users/dev15/AppData/Local/Temp/claude/C--Users-dev15-Games-sequax-game/e9c6ab72-cf35-42fe-abaf-e8f027905052/scratchpad";
        System.IO.Directory.CreateDirectory(dir);

        Shot(dir + "/01_whole_map.png", 24, 3, 134, 134, 2048, 2048);   // full 130x130 square
        Shot(dir + "/02_road_north.png", -23, 52, 44, 44, 1200, 1200);  // north road opening
        Shot(dir + "/03_road_south.png", -23, -52, 44, 44, 1200, 1200); // south road opening
        Shot(dir + "/04_old_north_border.png", 3, 30, 80, 46, 1600, 920); // where old north wall was
        Shot(dir + "/05_old_east_border.png", 45, 3, 46, 80, 920, 1600);  // where old east wall was
        Shot(dir + "/06_old_south_border.png", 3, -28, 80, 46, 1600, 920);// where old south wall was
        Shot(dir + "/07_new_border_forest_NE.png", 68, 46, 58, 58, 1400, 1400); // new NE outer border + forest
        Debug.Log("[Cap] DONE");
    }

    [MenuItem("Tools/Forest Editor/Validation/Capture Border Screenshots")]
    public static void CaptureBorder()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.EndsWith("MainForest.unity"))
            scene = EditorSceneManager.OpenScene("Assets/Scenes/MainForest.unity", OpenSceneMode.Single);

        string dir = "C:/Users/dev15/AppData/Local/Temp/claude/C--Users-dev15-Games-sequax-game/e9c6ab72-cf35-42fe-abaf-e8f027905052/scratchpad";
        System.IO.Directory.CreateDirectory(dir);

        Shot(dir + "/b01_whole_map.png", 24, 3, 134, 134, 2048, 2048);
        Shot(dir + "/b02_north_border.png", 24, 61, 138, 30, 2048, 445);
        Shot(dir + "/b03_south_border.png", 24, -55, 138, 30, 2048, 445);
        Shot(dir + "/b04_east_border.png", 82, 3, 30, 138, 445, 2048);
        Shot(dir + "/b05_west_border.png", -34, 3, 30, 138, 445, 2048);
        Shot(dir + "/b06_corner_NW.png", -32, 59, 32, 32, 1100, 1100);
        Shot(dir + "/b07_corner_NE.png", 80, 59, 32, 32, 1100, 1100);
        Shot(dir + "/b08_corner_SW.png", -32, -53, 32, 32, 1100, 1100);
        Shot(dir + "/b09_corner_SE.png", 80, -53, 32, 32, 1100, 1100);
        Shot(dir + "/b10_road_opening_north.png", -23, 57, 42, 42, 1200, 1200);
        Shot(dir + "/b11_road_opening_south.png", -23, -55, 42, 42, 1200, 1200);
        Debug.Log("[Cap] BORDER DONE");
    }

    [MenuItem("Tools/Forest Editor/Validation/Capture OldBorder Strips (before)")]
    public static void CaptureOldBefore() => CaptureOldBorderStrips("ob_before");
    [MenuItem("Tools/Forest Editor/Validation/Capture OldBorder Strips (after)")]
    public static void CaptureOldAfter() => CaptureOldBorderStrips("ob_after");

    public static void CaptureOldBorderStrips(string prefix)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.EndsWith("MainForest.unity"))
            scene = EditorSceneManager.OpenScene("Assets/Scenes/MainForest.unity", OpenSceneMode.Single);
        string dir = "C:/Users/dev15/AppData/Local/Temp/claude/C--Users-dev15-Games-sequax-game/e9c6ab72-cf35-42fe-abaf-e8f027905052/scratchpad";
        System.IO.Directory.CreateDirectory(dir);
        Shot($"{dir}/{prefix}_01_whole_map.png", 24, 3, 134, 134, 2048, 2048);
        Shot($"{dir}/{prefix}_02_north_strip.png", 24, 32, 138, 40, 2048, 594);   // old north ~y=37
        Shot($"{dir}/{prefix}_03_south_strip.png", 24, -26, 138, 40, 2048, 594);  // old south ~y=-31
        Shot($"{dir}/{prefix}_04_north_road.png", -23, 26, 46, 46, 1200, 1200);   // road @ old north / RoadTopBorder
        Shot($"{dir}/{prefix}_05_south_road.png", -23, -20, 46, 46, 1200, 1200);  // road @ old south / roadBottomBorder
        Debug.Log($"[Cap] {prefix} strips DONE");
    }

    [MenuItem("Tools/Forest Editor/Validation/Capture East (before)")]
    public static void CaptureEastBefore() => CaptureEast("east_before");
    [MenuItem("Tools/Forest Editor/Validation/Capture East (after)")]
    public static void CaptureEastAfter() => CaptureEast("east_after");

    public static void CaptureEast(string prefix)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.EndsWith("MainForest.unity"))
            scene = EditorSceneManager.OpenScene("Assets/Scenes/MainForest.unity", OpenSceneMode.Single);
        string dir = "C:/Users/dev15/AppData/Local/Temp/claude/C--Users-dev15-Games-sequax-game/e9c6ab72-cf35-42fe-abaf-e8f027905052/scratchpad";
        System.IO.Directory.CreateDirectory(dir);
        Shot($"{dir}/{prefix}_01_whole_map.png", 24, 3, 134, 134, 2048, 2048);
        Shot($"{dir}/{prefix}_02_east_vstrip.png", 40, 3, 44, 138, 594, 2048);   // vertical strip x~18..62
        Shot($"{dir}/{prefix}_03_east_region.png", 42, 3, 64, 64, 1400, 1400);   // old east border zone
        Debug.Log($"[Cap] {prefix} DONE");
    }

    [MenuItem("Tools/Forest Editor/Validation/Capture Interior")]
    public static void CaptureInterior()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.EndsWith("MainForest.unity"))
            scene = EditorSceneManager.OpenScene("Assets/Scenes/MainForest.unity", OpenSceneMode.Single);
        string dir = "C:/Users/dev15/AppData/Local/Temp/claude/C--Users-dev15-Games-sequax-game/e9c6ab72-cf35-42fe-abaf-e8f027905052/scratchpad";
        System.IO.Directory.CreateDirectory(dir);
        Shot($"{dir}/int_01_whole_map.png", 24, 3, 134, 134, 2048, 2048);
        Shot($"{dir}/int_02_north.png", 12, 46, 60, 44, 1500, 1100);
        Shot($"{dir}/int_03_south.png", 12, -44, 60, 44, 1500, 1100);
        Shot($"{dir}/int_04_east.png", 64, 3, 50, 70, 1100, 1540);
        Shot($"{dir}/int_05_center.png", 12, 2, 54, 54, 1300, 1300);
        Shot($"{dir}/int_06_cabin.png", -2, -8, 34, 34, 1200, 1200);
        Shot($"{dir}/int_07_road.png", -22, 0, 40, 120, 560, 1680);
        Shot($"{dir}/int_08_LandmarkZone_North.png", 6, 52, 34, 34, 1200, 1200);
        Shot($"{dir}/int_09_LandmarkZone_South.png", 8, -49, 34, 34, 1200, 1200);
        Shot($"{dir}/int_10_LandmarkZone_East_01.png", 68, 20, 34, 34, 1200, 1200);
        Shot($"{dir}/int_11_LandmarkZone_East_02.png", 68, -14, 34, 34, 1200, 1200);
        Debug.Log("[Cap] INTERIOR DONE");
    }

    private static void Shot(string path, float cx, float cy, float worldW, float worldH, int pw, int ph)
    {
        var camGO = new GameObject("__CapCam");
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.transform.position = new Vector3(cx, cy, -20);
        cam.aspect = (float)pw / ph;
        cam.orthographicSize = Mathf.Max(worldH / 2f, (worldW / 2f) / cam.aspect);
        cam.backgroundColor = Color.black;
        cam.clearFlags = CameraClearFlags.SolidColor;
        var extra = camGO.GetComponent<UniversalAdditionalCameraData>();
        if (extra == null) extra = camGO.AddComponent<UniversalAdditionalCameraData>();

        var rt = new RenderTexture(pw, ph, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        cam.targetTexture = rt;
        var request = new UniversalRenderPipeline.SingleCameraRequest();
        if (RenderPipeline.SupportsRenderRequest(cam, request))
            RenderPipeline.SubmitRenderRequest(cam, request);
        else
            cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(pw, ph, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, pw, ph), 0, 0);
        tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGO);
        Debug.Log($"[Cap] wrote {path}");
    }
}
