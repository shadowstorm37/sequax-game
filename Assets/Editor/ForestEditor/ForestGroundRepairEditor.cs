using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Two targeted corrections:
//  1) remove the obsolete eastern internal tree band (~x=30..40) inside the expanded map;
//  2) repaint the exposed periodic ground in the former N/S/E border strips with the
//     same seeded-random pixelgreenground_0..8 distribution used by the good expansion.
// Does NOT regenerate the map or touch interior placement elsewhere. Read helper reused
// from ForestOldBorderTool where possible. Nothing runs at play time.
public static class ForestGroundRepairEditor
{
    private const int Seed = 20260719; // same seed family as the good expansion ground

    // East tree removal band (vertical), covering the visible x~30..38 line with margin,
    // spanning between the already-cleared north (y<=40) and south (y>=-34) strips.
    private const float EXMin = 28f, EXMax = 42f, EYMin = -35f, EYMax = 42f;

    // Road columns to never repaint (road lives on Paths; keep Ground under it untouched).
    private const int RoadXMin = -25, RoadXMax = -21;

    private static string SpriteClass(GameObject go)
    {
        var sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return "<none>";
        return System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(sr.sprite));
    }

    private static IEnumerable<Transform> Placements()
    {
        var forest = ForestEditorHierarchy.GroupGO(ForestEditorHierarchy.HandPlaced);
        if (forest != null)
            foreach (var cn in new[] { "treenormal", "darktrees", "bushes", "stumps", "Logs" })
            {
                var cont = forest.transform.Find(cn);
                if (cont == null) continue;
                foreach (Transform c in cont) yield return c;
            }
        foreach (var r in new[] { "MapExpansion_North", "MapExpansion_East", "MapExpansion_South" })
        {
            var go = ForestEditorHierarchy.GroupGO(r);
            if (go == null) continue;
            foreach (Transform c in go.transform) yield return c;
        }
    }

    private static bool InEastBand(Vector2 p) => p.x >= EXMin && p.x <= EXMax && p.y >= EYMin && p.y <= EYMax;
    private static bool ObjTouchesEast(GameObject go)
    {
        if (InEastBand(go.transform.position)) return true;
        foreach (var c in go.GetComponentsInChildren<Collider2D>(true)) if (InEastBand(c.transform.position)) return true;
        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true)) if (InEastBand(sr.transform.position)) return true;
        return false;
    }

    // Repaint predicate: three former-border strips, minus road columns.
    private static bool InRepaint(int x, int y)
    {
        if (x >= RoadXMin && x <= RoadXMax) return false;
        bool north = y >= 24 && y <= 41 && x >= -34 && x <= 48;
        bool south = y >= -35 && y <= -18 && x >= -34 && x <= 48;
        bool east = x >= 28 && x <= 50 && y >= -34 && y <= 40;
        return north || south || east;
    }

    [MenuItem("Tools/Forest Editor/Ground Repair/Apply Corrections")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.EndsWith("MainForest.unity"))
            scene = EditorSceneManager.OpenScene("Assets/Scenes/MainForest.unity", OpenSceneMode.Single);

        // ---- TASK 1: remove obsolete eastern tree band ----
        var bySprite = new Dictionary<string, int>();
        var toRemove = new List<GameObject>();
        foreach (var tr in Placements())
        {
            if (!ObjTouchesEast(tr.gameObject)) continue;
            bySprite[SpriteClass(tr.gameObject)] = bySprite.GetValueOrDefault(SpriteClass(tr.gameObject)) + 1;
            toRemove.Add(tr.gameObject);
        }
        int colliders = toRemove.Sum(g => g.GetComponentsInChildren<Collider2D>(true).Length);
        foreach (var g in toRemove) Object.DestroyImmediate(g);
        Debug.Log($"[EG] Task1 removed east-band objects: {toRemove.Count} (colliders: {colliders}) | " +
                  string.Join(", ", bySprite.OrderByDescending(k => k.Value).Select(k => k.Key + "=" + k.Value)));

        // ---- TASK 2: repaint exposed ground strips with seeded-random pixelgreenground ----
        var groundTiles = new TileBase[9];
        for (int i = 0; i < 9; i++)
            groundTiles[i] = AssetDatabase.LoadAssetAtPath<TileBase>($"Assets/Art/Environment/Tiles/pixelgreenground_{i}.asset");
        if (groundTiles.Any(t => t == null)) { Debug.LogError("[EG] Missing pixelgreenground tiles."); return; }

        var ground = GameObject.Find("Grid").transform.Find("Ground").GetComponent<Tilemap>();
        var rng = new System.Random(Seed);
        int repainted = 0, skippedRoad = 0;
        int xMinR = -34, xMaxR = 50, yMinR = -35, yMaxR = 41;
        for (int x = xMinR; x <= xMaxR; x++)
            for (int y = yMinR; y <= yMaxR; y++)
            {
                if (!InRepaint(x, y)) continue;
                var pos = new Vector3Int(x, y, 0);
                var cur = ground.GetTile(pos);
                if (cur == null) continue;                       // no ground here -> leave
                if (!cur.name.StartsWith("pixelgreenground"))    // never touch a non-ground tile
                { skippedRoad++; continue; }
                ground.SetTile(pos, groundTiles[rng.Next(9)]);
                repainted++;
            }
        Debug.Log($"[EG] Task2 repainted ground cells: {repainted} (skipped non-ground/road: {skippedRoad})");
        Debug.Log("[EG] Repaired ranges: NORTH x[-34,48] y[24,41]; SOUTH x[-34,48] y[-35,-18]; EAST x[28,50] y[-34,40] (road cols x[-25,-21] skipped)");

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        Debug.Log($"[EG] Scene saved: {saved}. DONE.");
    }

    [MenuItem("Tools/Forest Editor/Ground Repair/Verify (Read Only)")]
    public static void Verify()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.EndsWith("MainForest.unity"))
            scene = EditorSceneManager.OpenScene("Assets/Scenes/MainForest.unity", OpenSceneMode.Single);

        int inBand = 0;
        foreach (var tr in Placements()) if (ObjTouchesEast(tr.gameObject)) inBand++;
        Debug.Log($"[EGV] Objects remaining in east band x[{EXMin},{EXMax}] y[{EYMin},{EYMax}]: {inBand} (expect 0)");

        int collidersInBand = 0;
        foreach (var c in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
        {
            var cab = GameObject.Find("Cabin");
            if (cab && c.transform.IsChildOf(cab.transform)) continue;
            var parent = c.transform.parent ? c.transform.parent.name : "";
            if (parent == "NewOuterBorder") continue;
            if (InEastBand(c.transform.position)) collidersInBand++;
        }
        Debug.Log($"[EGV] Non-outer/non-cabin colliders in east band: {collidersInBand} (expect 0)");

        // Ground checks: no non-pixelgreenground introduced; period-3 fraction in strips.
        var ground = GameObject.Find("Grid").transform.Find("Ground").GetComponent<Tilemap>();
        string Idx(int x, int y) { var t = ground.GetTile(new Vector3Int(x, y, 0)); return t == null ? "-" : (t.name.StartsWith("pixelgreenground_") ? t.name.Substring(17) : "?" + t.name); }
        float Period3(int x0, int y0, int w, int h)
        {
            int m = 0, tot = 0;
            for (int y = y0; y < y0 + h - 3; y++) for (int x = x0; x < x0 + w - 3; x++) { tot++; if (Idx(x, y) == Idx(x + 3, y) && Idx(x, y) == Idx(x, y + 3)) m++; }
            return tot == 0 ? 0 : (float)m / tot;
        }
        Debug.Log($"[EGV] period-3 repeat after repair: NORTH(x0..15,y27..40)={Period3(0, 27, 15, 14):F2}, SOUTH(x0..15,y-33..-20)={Period3(0, -33, 15, 14):F2}, EAST(x30..45,y-15..0)={Period3(30, -15, 15, 15):F2} (want ~0)");

        // Confirm no bright-green/road tiles anywhere in the repaint bounding box.
        var bad = new Dictionary<string, int>();
        for (int x = -34; x <= 50; x++) for (int y = -35; y <= 41; y++)
        { if (!InRepaint(x, y)) continue; var t = ground.GetTile(new Vector3Int(x, y, 0)); if (t != null && !t.name.StartsWith("pixelgreenground")) bad[t.name] = bad.GetValueOrDefault(t.name) + 1; }
        Debug.Log($"[EGV] Non-pixelgreenground tiles in repaired strips: {(bad.Count == 0 ? "NONE" : string.Join(", ", bad.Select(k => k.Key + "=" + k.Value)))}");

        // Road tiles intact through east band Y (paths untouched) and outer border intact.
        var paths = GameObject.Find("Grid").transform.Find("Paths").GetComponent<Tilemap>();
        int roadCells = 0; for (int y = -62; y < 68; y++) for (int x = -24; x <= -22; x++) if (paths.GetTile(new Vector3Int(x, y, 0)) != null) roadCells++;
        Debug.Log($"[EGV] Road (Paths) tiles present: {roadCells} (expect 390, unchanged)");

        var ob = ForestEditorHierarchy.GroupGO(ForestEditorHierarchy.OuterBorder);
        int eastOuter = 0;
        if (ob) foreach (var sr in ob.GetComponentsInChildren<SpriteRenderer>()) if (sr.transform.position.x > 82) eastOuter++;
        Debug.Log($"[EGV] NewOuterBorder trees with x>82 (east outer border): {eastOuter} (should be unchanged, ~230)");

        int missing = 0;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            foreach (var comp in go.GetComponents<Component>()) if (comp == null) missing++;
        Debug.Log($"[EGV] Missing/null components: {missing}");
        Debug.Log("[EGV] DONE");
    }
}
