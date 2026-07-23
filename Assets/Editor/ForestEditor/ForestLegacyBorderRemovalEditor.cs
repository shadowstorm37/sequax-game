using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Removes the two OBSOLETE internal horizontal tree borders (old north ~y=37, old south ~y=-31)
// left inside the expanded map, plus their invisible boundary colliders. Read-only inspect first.
public static class ForestLegacyBorderRemovalEditor
{
    private const int NewXMin = -41, NewXMax = 89, NewYMin = -62, NewYMax = 68;
    private const float OldNorthY = 37f, OldSouthY = -31f;

    private static string SpriteClass(GameObject go)
    {
        var sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return "<none>";
        return System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(sr.sprite));
    }

    private static IEnumerable<(Transform tr, string group)> Placements()
    {
        var forest = ForestEditorHierarchy.GroupGO(ForestEditorHierarchy.HandPlaced);
        if (forest != null)
            foreach (var cn in new[] { "treenormal", "darktrees", "bushes", "stumps", "Logs" })
            {
                var cont = forest.transform.Find(cn);
                if (cont == null) continue;
                foreach (Transform c in cont) yield return (c, ForestEditorHierarchy.HandPlaced + "/" + cn);
            }
        foreach (var r in new[] { "MapExpansion_North", "MapExpansion_East", "MapExpansion_South" })
        {
            var go = ForestEditorHierarchy.GroupGO(r);
            if (go == null) continue;
            foreach (Transform c in go.transform) yield return (c, r);
        }
    }

    [MenuItem("Tools/Forest Editor/Border Repair/Legacy Inspect (Read Only)")]
    public static void Inspect()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.EndsWith("MainForest.unity"))
            scene = EditorSceneManager.OpenScene("Assets/Scenes/MainForest.unity", OpenSceneMode.Single);

        // Y histogram of interior placements (exclude NewOuterBorder), bin size 1.
        var hist = new SortedDictionary<int, int>();
        foreach (var (tr, grp) in Placements())
        {
            int by = Mathf.RoundToInt(tr.position.y);
            hist[by] = hist.GetValueOrDefault(by) + 1;
        }
        // print a compact histogram, flag spikes
        var sb = new System.Text.StringBuilder("[OB] Interior Y-histogram (y: count) — spikes reveal horizontal bands:\n");
        foreach (var kv in hist) sb.AppendLine($"    y={kv.Key,4}: {new string('#', Mathf.Min(kv.Value, 60))} {kv.Value}");
        Debug.Log(sb.ToString());

        // Windows around old borders.
        void Window(string label, float lo, float hi)
        {
            var bySprite = new Dictionary<string, int>();
            var byGroup = new Dictionary<string, int>();
            int n = 0;
            foreach (var (tr, grp) in Placements())
            {
                float y = tr.position.y;
                if (y < lo || y > hi) continue;
                n++;
                bySprite[SpriteClass(tr.gameObject)] = bySprite.GetValueOrDefault(SpriteClass(tr.gameObject)) + 1;
                byGroup[grp] = byGroup.GetValueOrDefault(grp) + 1;
            }
            Debug.Log($"[OB] {label} window y[{lo},{hi}]: {n} objects | sprites: " +
                string.Join(", ", bySprite.OrderByDescending(k => k.Value).Select(k => k.Key + "=" + k.Value)) +
                " | groups: " + string.Join(", ", byGroup.OrderByDescending(k => k.Value).Select(k => k.Key + "=" + k.Value)));
        }
        Window("OLD-NORTH", 33, 41);
        Window("OLD-SOUTH", -35, -27);

        // All BoxCollider2D in scene (candidates for invisible boundary walls).
        Debug.Log("[OB] BoxCollider2D inventory (name @ world pos, size, hasSprite):");
        foreach (var box in Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None))
        {
            var go = box.gameObject;
            bool hasSprite = go.GetComponentInChildren<SpriteRenderer>() != null;
            Vector3 wp = box.transform.position;
            Debug.Log($"    '{go.name}' parent='{(go.transform.parent ? go.transform.parent.name : "-")}' pos=({wp.x:F1},{wp.y:F1}) size={box.size} sprite={hasSprite}");
        }

        // Road container children.
        var road = GameObject.Find("Road");
        if (road != null)
        {
            Debug.Log("[OB] Road container children:");
            foreach (Transform c in road.transform)
                Debug.Log($"    '{c.name}' pos=({c.position.x:F1},{c.position.y:F1}) hasBox={(c.GetComponent<BoxCollider2D>() != null)} hasSprite={(c.GetComponentInChildren<SpriteRenderer>() != null)}");
        }

        Debug.Log("[OB] DONE (read-only)");
    }

    [MenuItem("Tools/Forest Editor/Border Repair/Legacy Inspect East + Ground (Read Only)")]
    public static void InspectEastGround()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.EndsWith("MainForest.unity"))
            scene = EditorSceneManager.OpenScene("Assets/Scenes/MainForest.unity", OpenSceneMode.Single);

        // ---- X histogram of interior placements (exclude NewOuterBorder) ----
        var hist = new SortedDictionary<int, int>();
        foreach (var (tr, grp) in Placements())
        {
            int bx = Mathf.RoundToInt(tr.position.x);
            hist[bx] = hist.GetValueOrDefault(bx) + 1;
        }
        var sb = new System.Text.StringBuilder("[E] Interior X-histogram (x: count) — spike = vertical band:\n");
        foreach (var kv in hist) sb.AppendLine($"    x={kv.Key,4}: {new string('#', Mathf.Min(kv.Value, 60))} {kv.Value}");
        Debug.Log(sb.ToString());

        // ---- East band window x[41,51], and its Y extent ----
        var yext = new SortedDictionary<int, int>();
        var bySprite = new Dictionary<string, int>();
        int n = 0;
        foreach (var (tr, grp) in Placements())
        {
            float x = tr.position.x;
            if (x < 41 || x > 51) continue;
            n++;
            bySprite[SpriteClass(tr.gameObject)] = bySprite.GetValueOrDefault(SpriteClass(tr.gameObject)) + 1;
            int by5 = Mathf.RoundToInt(tr.position.y / 5f) * 5;
            yext[by5] = yext.GetValueOrDefault(by5) + 1;
        }
        Debug.Log($"[E] East window x[41,51]: {n} objs | sprites: " + string.Join(", ", bySprite.OrderByDescending(k => k.Value).Select(k => k.Key + "=" + k.Value)));
        Debug.Log($"[E] East window Y-extent (bin5): " + string.Join(", ", yext.Select(k => $"y{k.Key}={k.Value}")));

        // ---- Ground pattern analysis ----
        var ground = GameObject.Find("Grid").transform.Find("Ground").GetComponent<UnityEngine.Tilemaps.Tilemap>();
        string Idx(int x, int y)
        {
            var t = ground.GetTile(new Vector3Int(x, y, 0));
            if (t == null) return "-";
            var nm = t.name;
            return nm.StartsWith("pixelgreenground_") ? nm.Substring("pixelgreenground_".Length) : "?" + nm;
        }
        void DumpGrid(string label, int x0, int y0, int w, int h)
        {
            var g = new System.Text.StringBuilder($"[G] {label} tile grid (rows top->bottom y={y0 + h - 1}..{y0}, cols x={x0}..{x0 + w - 1}):\n");
            for (int y = y0 + h - 1; y >= y0; y--)
            {
                g.Append("    ");
                for (int x = x0; x < x0 + w; x++) g.Append(Idx(x, y).PadLeft(3));
                g.Append('\n');
            }
            // period-3 repeat fraction
            int match3 = 0, tot = 0;
            for (int y = y0; y < y0 + h - 3; y++)
                for (int x = x0; x < x0 + w - 3; x++)
                {
                    tot++;
                    if (Idx(x, y) == Idx(x + 3, y) && Idx(x, y) == Idx(x, y + 3)) match3++;
                }
            g.Append($"    period-3 repeat fraction: {(tot == 0 ? 0 : (float)match3 / tot):F2} (1.0 = perfect 3x3 tiling)\n");
            // histogram
            var hh = new Dictionary<string, int>();
            for (int y = y0; y < y0 + h; y++) for (int x = x0; x < x0 + w; x++) hh[Idx(x, y)] = hh.GetValueOrDefault(Idx(x, y)) + 1;
            g.Append("    tile-index histogram: " + string.Join(", ", hh.OrderBy(k => k.Key).Select(k => k.Key + "=" + k.Value)));
            Debug.Log(g.ToString());
        }
        DumpGrid("GOOD-EXPANSION (x60..75,y45..60)", 60, 45, 15, 15);
        DumpGrid("NORTH-STRIP (x0..15,y27..40)", 0, 27, 15, 14);
        DumpGrid("SOUTH-STRIP (x0..15,y-33..-20)", 0, -33, 15, 14);
        DumpGrid("EAST-STRIP (x40..52,y-10..5)", 40, -10, 13, 15);

        Debug.Log("[E] DONE (read-only)");
    }

    // Removal region (old map footprint only; x starts at -33 to keep the west outer border).
    private const float RmXMin = -33f, RmXMax = 47.5f;
    private const float NorthLo = 25f, NorthHi = 40f;
    private const float SouthLo = -34f, SouthHi = -19f;

    private static bool InRemovalBand(Vector2 p)
    {
        if (p.x < RmXMin || p.x > RmXMax) return false;
        return (p.y >= NorthLo && p.y <= NorthHi) || (p.y >= SouthLo && p.y <= SouthHi);
    }

    // True if the object's own transform, or any of its child colliders/renderers, lands in a band.
    private static bool ObjectTouchesBand(GameObject go)
    {
        if (InRemovalBand(go.transform.position)) return true;
        foreach (var c in go.GetComponentsInChildren<Collider2D>(true))
            if (InRemovalBand(c.transform.position)) return true;
        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
            if (InRemovalBand(sr.transform.position)) return true;
        return false;
    }

    [MenuItem("Tools/Forest Editor/Border Repair/Legacy Remove Internal Bands")]
    public static void RemoveInternalBorders()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.EndsWith("MainForest.unity"))
            scene = EditorSceneManager.OpenScene("Assets/Scenes/MainForest.unity", OpenSceneMode.Single);

        // 1. Remove scattered objects (trees + props) inside the two bands.
        var byBand = new Dictionary<string, int>();
        var bySprite = new Dictionary<string, int>();
        var toRemove = new List<GameObject>();
        foreach (var (tr, grp) in Placements())
        {
            Vector2 p = tr.position;
            if (!ObjectTouchesBand(tr.gameObject)) continue;
            string band = p.y > 0 ? "NORTH" : "SOUTH";
            byBand[band] = byBand.GetValueOrDefault(band) + 1;
            bySprite[SpriteClass(tr.gameObject)] = bySprite.GetValueOrDefault(SpriteClass(tr.gameObject)) + 1;
            toRemove.Add(tr.gameObject);
        }
        int colliders = toRemove.Sum(g => g.GetComponentsInChildren<Collider2D>(true).Length);
        foreach (var g in toRemove) Object.DestroyImmediate(g);
        Debug.Log($"[OBRemove] Removed scatter objects in bands: {toRemove.Count} " +
                  $"(NORTH={byBand.GetValueOrDefault("NORTH")}, SOUTH={byBand.GetValueOrDefault("SOUTH")}); " +
                  $"colliders on them: {colliders}");
        Debug.Log($"[OBRemove]   by sprite: " + string.Join(", ", bySprite.OrderByDescending(k => k.Value).Select(k => k.Key + "=" + k.Value)));

        // 2. Remove the invisible old road boundary colliders.
        int roadColliders = 0;
        var road = GameObject.Find("Road");
        if (road != null)
            foreach (var name in new[] { "RoadTopBorder", "roadBottomBorder" })
            {
                var t = road.transform.Find(name);
                if (t != null) { Object.DestroyImmediate(t.gameObject); roadColliders++; Debug.Log($"[OBRemove] Removed invisible road barrier '{name}'"); }
            }
        Debug.Log($"[OBRemove] Road barrier objects removed: {roadColliders}");

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        Debug.Log($"[OBRemove] Scene saved: {saved}. DONE.");
    }

    [MenuItem("Tools/Forest Editor/Border Repair/Legacy Verify Cleared (Read Only)")]
    public static void VerifyCleared()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.EndsWith("MainForest.unity"))
            scene = EditorSceneManager.OpenScene("Assets/Scenes/MainForest.unity", OpenSceneMode.Single);

        int inBands = 0;
        foreach (var (tr, grp) in Placements())
            if (InRemovalBand(tr.position)) inBands++;
        Debug.Log($"[OBVerify] Scatter objects remaining in removal bands: {inBands} (expect 0)");

        // Any collider left in the band regions?
        int collidersInBand = 0;
        foreach (var c in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
        {
            var go = c.gameObject;
            if (go.transform.IsChildOf(GameObject.Find("Cabin").transform)) continue; // cabin walls ok
            var parent = go.transform.parent ? go.transform.parent.name : "";
            if (parent == "NewOuterBorder") continue; // outer border ok
            if (InRemovalBand(c.transform.position)) { collidersInBand++; Debug.Log($"[OBVerify]   collider still in band: '{go.name}' parent='{parent}' pos=({c.transform.position.x:F1},{c.transform.position.y:F1})"); }
        }
        Debug.Log($"[OBVerify] Non-outer-border/non-cabin colliders in bands: {collidersInBand}");

        // Confirm road barriers gone.
        var road = GameObject.Find("Road");
        bool top = road && road.transform.Find("RoadTopBorder");
        bool bot = road && road.transform.Find("roadBottomBorder");
        Debug.Log($"[OBVerify] RoadTopBorder present: {top}; roadBottomBorder present: {bot} (both expect False)");

        // Confirm road tiles still continuous through the band Y ranges.
        var paths = GameObject.Find("Grid").transform.Find("Paths").GetComponent<UnityEngine.Tilemaps.Tilemap>();
        int gapN = 0, gapS = 0;
        for (int y = (int)NorthLo; y <= (int)NorthHi; y++) for (int x = -24; x <= -22; x++) if (paths.GetTile(new Vector3Int(x, y, 0)) == null) gapN++;
        for (int y = (int)SouthLo; y <= (int)SouthHi; y++) for (int x = -24; x <= -22; x++) if (paths.GetTile(new Vector3Int(x, y, 0)) == null) gapS++;
        Debug.Log($"[OBVerify] Missing road tiles within north band: {gapN}, south band: {gapS} (expect 0 => road continuous)");

        int missing = 0;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            foreach (var comp in go.GetComponents<Component>())
                if (comp == null) missing++;
        Debug.Log($"[OBVerify] Missing/null components: {missing}");
        Debug.Log("[OBVerify] DONE");
    }
}
