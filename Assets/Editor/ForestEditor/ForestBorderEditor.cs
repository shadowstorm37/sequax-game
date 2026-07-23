using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Outer-border-only tool for MainForest. Read-only inspection + border rebuild.
// Uses ONLY the verified leafy tree sprite (pixeltree.png). Nothing runs at play time.
public static class ForestBorderEditor
{
    private const int NewXMin = -41, NewXMax = 89, NewYMin = -62, NewYMax = 68;
    private const int OrigYMin = -31, OrigYMax = 37; // original west-wall vertical span
    private const float RoadOpenXMin = -26, RoadOpenXMax = -20;

    // Verified by viewing the sprite: the correct leafy top-down tree.
    private const string PixelTreeSprite = "pixeltree";

    private static string SpritePathClass(GameObject go)
    {
        var sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return "<none>";
        string path = AssetDatabase.GetAssetPath(sr.sprite);
        return System.IO.Path.GetFileNameWithoutExtension(path);
    }

    private static bool InBorderBand(Vector2 p, float t)
        => p.x <= NewXMin + t || p.x >= NewXMax - t || p.y <= NewYMin + t || p.y >= NewYMax - t;

    // The "placement" objects we consider: direct children of these containers.
    private static IEnumerable<Transform> PlacementObjects()
    {
        string[] roots = { "NewOuterBorder", "MapExpansion_North", "MapExpansion_East", "MapExpansion_South" };
        foreach (var r in roots)
        {
            var go = ForestEditorHierarchy.GroupGO(r);
            if (go == null) continue;
            foreach (Transform c in go.transform) yield return c;
        }
        var forest = ForestEditorHierarchy.GroupGO(ForestEditorHierarchy.HandPlaced);
        if (forest != null)
            foreach (var cn in new[] { "treenormal", "darktrees", "bushes", "stumps", "Logs" })
            {
                var cont = forest.transform.Find(cn);
                if (cont == null) continue;
                foreach (Transform c in cont) yield return c;
            }
    }

    [MenuItem("Tools/Forest Editor/Border Repair/Inspect (Read Only)")]
    public static void InspectBorder()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.EndsWith("MainForest.unity"))
            scene = EditorSceneManager.OpenScene("Assets/Scenes/MainForest.unity", OpenSceneMode.Single);

        Debug.Log($"[BInspect] Final map bounds X[{NewXMin},{NewXMax}) Y[{NewYMin},{NewYMax}) = {NewXMax - NewXMin}x{NewYMax - NewYMin}");

        // Parent containers holding border-band objects + composition by sprite.
        float t = 6.5f;
        var byParent = new Dictionary<string, int>();
        var bySprite = new Dictionary<string, int>();
        var bySpriteBySide = new Dictionary<string, Dictionary<string, int>>();
        int total = 0;
        foreach (var tr in PlacementObjects())
        {
            Vector2 p = tr.position;
            if (!InBorderBand(p, t)) continue;
            total++;
            string parent = tr.parent != null ? tr.parent.name : "<root>";
            byParent[parent] = byParent.GetValueOrDefault(parent) + 1;
            string sc = SpritePathClass(tr.gameObject);
            bySprite[sc] = bySprite.GetValueOrDefault(sc) + 1;
            string side = p.x <= NewXMin + t ? "WEST" : p.x >= NewXMax - t ? "EAST" : p.y >= NewYMax - t ? "NORTH" : "SOUTH";
            if (!bySpriteBySide.TryGetValue(side, out var d)) { d = new Dictionary<string, int>(); bySpriteBySide[side] = d; }
            d[sc] = d.GetValueOrDefault(sc) + 1;
        }
        Debug.Log($"[BInspect] Border-band (thickness {t}) total placement objects: {total}");
        Debug.Log("[BInspect] By parent container: " + string.Join(", ", byParent.OrderByDescending(k => k.Value).Select(k => $"{k.Key}={k.Value}")));
        Debug.Log("[BInspect] By sprite (VERIFIED via asset path): " + string.Join(", ", bySprite.OrderByDescending(k => k.Value).Select(k => $"{k.Key}={k.Value}")));
        foreach (var side in new[] { "NORTH", "SOUTH", "EAST", "WEST" })
            if (bySpriteBySide.TryGetValue(side, out var d))
                Debug.Log($"[BInspect]   {side}: " + string.Join(", ", d.OrderByDescending(k => k.Value).Select(k => $"{k.Key}={k.Value}")));

        // Classify correct vs incorrect.
        int correct = bySprite.Where(k => k.Key == PixelTreeSprite).Sum(k => k.Value);
        int incorrect = total - correct;
        Debug.Log($"[BInspect] CORRECT leafy pixeltree in border: {correct}; INCORRECT (stumps/dead/logs/bush/other): {incorrect}");

        // Determine a representative original leafy-tree from the interior (treenormal = pixeltree).
        var forest = ForestEditorHierarchy.GroupGO(ForestEditorHierarchy.HandPlaced);
        var treenormal = forest.transform.Find("treenormal");
        int sample = 0; string exName = "";
        foreach (Transform c in treenormal) { if (SpritePathClass(c.gameObject) == PixelTreeSprite) { exName = c.name; if (++sample >= 1) break; } }
        Debug.Log($"[BInspect] Interior leafy-tree source: container 'ForestStuff/treenormal', example '{exName}', sprite Assets/Art/Environment/Sprites/Forest/pixeltree.png");

        // Measure original WEST border thickness (preserved, y in [-31,37)): min/max x of leafy trees near west.
        var westXs = new List<float>();
        foreach (Transform c in treenormal)
        {
            Vector2 p = c.position;
            if (p.y > OrigYMin && p.y < OrigYMax && p.x < NewXMin + 12) westXs.Add(p.x);
        }
        if (westXs.Count > 0)
            Debug.Log($"[BInspect] Original west-edge leafy trees: n={westXs.Count} x range [{westXs.Min():F1}, {westXs.Max():F1}] -> thickness ~{westXs.Max() - westXs.Min():F1}");

        // Report scale/rotation/collider of a sample interior pixeltree for matching.
        foreach (Transform c in treenormal)
            if (SpritePathClass(c.gameObject) == PixelTreeSprite)
            {
                var sr = c.GetComponentInChildren<SpriteRenderer>();
                var col = c.GetComponentInChildren<CircleCollider2D>();
                Debug.Log($"[BInspect] Sample pixeltree '{c.name}': scale={c.localScale} sortingLayer={sr.sortingLayerID} order={sr.sortingOrder} " +
                          $"collider={(col == null ? "NONE" : $"CircleCollider2D r={col.radius} offset={col.offset}")}");
                break;
            }

        // Escape-gap scan along each edge inner line.
        GapScanEdges();

        // Missing components.
        int missing = 0;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            foreach (var comp in go.GetComponents<Component>())
                if (comp == null) missing++;
        Debug.Log($"[BInspect] Missing/null components in scene: {missing}");

        Debug.Log("[BInspect] DONE (read-only)");
    }

    private static void GapScanEdges()
    {
        var border = ForestEditorHierarchy.GroupGO(ForestEditorHierarchy.OuterBorder);
        var pts = new List<Vector2>();
        if (border != null)
            foreach (var sr in border.GetComponentsInChildren<SpriteRenderer>())
                pts.Add(sr.transform.position);
        // include any surviving pixeltrees near edges from other parents
        foreach (var root in new[] { "MapExpansion_North", "MapExpansion_East", "MapExpansion_South", ForestEditorHierarchy.HandPlaced })
        {
            var go = ForestEditorHierarchy.GroupGO(root); if (go == null) continue;
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>())
            { Vector2 p = sr.transform.position; if (InBorderBand(p, BorderThickness + 1f)) pts.Add(p); }
        }
        void Scan(string name, IEnumerable<float> along, float min, float max, float[] roadGap)
        {
            var xs = along.OrderBy(v => v).ToList();
            if (xs.Count == 0) { Debug.Log($"[BInspect] gap {name}: NO trees"); return; }
            float maxGap = xs[0] - min, at = min;
            for (int i = 1; i < xs.Count; i++)
            {
                float g = xs[i] - xs[i - 1];
                bool road = roadGap != null && xs[i - 1] <= roadGap[1] + 1 && xs[i] >= roadGap[0] - 1;
                if (g > maxGap && !road) { maxGap = g; at = xs[i - 1]; }
            }
            float end = max - xs[xs.Count - 1];
            if (end > maxGap) { maxGap = end; at = xs[xs.Count - 1]; }
            Debug.Log($"[BInspect] gap {name}: {xs.Count} trees, largest non-road gap {maxGap:F1} near {at:F0}");
        }
        float t = BorderThickness + 1f;
        Scan("NORTH", pts.Where(p => p.y >= NewYMax - t).Select(p => p.x), NewXMin, NewXMax, new[] { RoadOpenXMin, RoadOpenXMax });
        Scan("SOUTH", pts.Where(p => p.y <= NewYMin + t).Select(p => p.x), NewXMin, NewXMax, new[] { RoadOpenXMin, RoadOpenXMax });
        Scan("EAST", pts.Where(p => p.x >= NewXMax - t).Select(p => p.y), NewYMin, NewYMax, null);
        Scan("WEST", pts.Where(p => p.x <= NewXMin + t).Select(p => p.y), NewYMin, NewYMax, null);
    }

    // ------------------------------------------------------------------
    private const int Seed = 20260720; // exposed, reproducible (border-only)
    private const float BorderThickness = 8f;

    private class Placed { public Vector2 pos; public float radius; }
    private static System.Random rng;
    private static Dictionary<Vector2Int, List<Placed>> hash;
    private const float HashCell = 4f;
    private static Vector2Int HKey(Vector2 p) => new Vector2Int(Mathf.FloorToInt(p.x / HashCell), Mathf.FloorToInt(p.y / HashCell));
    private static void HAdd(Placed p)
    {
        var k = HKey(p.pos);
        if (!hash.TryGetValue(k, out var l)) { l = new List<Placed>(); hash[k] = l; }
        l.Add(p);
    }
    private static bool HOverlaps(Vector2 pos, float radius, float factor)
    {
        var key = HKey(pos);
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (hash.TryGetValue(new Vector2Int(key.x + dx, key.y + dy), out var l))
                    foreach (var p in l)
                    {
                        float md = (p.radius + radius) * factor;
                        if ((p.pos - pos).sqrMagnitude < md * md) return true;
                    }
        return false;
    }

    [MenuItem("Tools/Forest Editor/Border Repair/Rebuild (Pixel Trees Only)")]
    public static void RebuildBorder()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.EndsWith("MainForest.unity"))
            scene = EditorSceneManager.OpenScene("Assets/Scenes/MainForest.unity", OpenSceneMode.Single);

        rng = new System.Random(Seed);
        hash = new Dictionary<Vector2Int, List<Placed>>();

        var forest = ForestEditorHierarchy.GroupGO(ForestEditorHierarchy.HandPlaced);
        var treenormal = forest.transform.Find("treenormal");

        // ---- pixeltree prototypes (deactivated copies) from the verified interior source ----
        var holder = new GameObject("__BorderTreeProtos_TEMP");
        holder.SetActive(false);
        var protos = new List<GameObject>();
        var kids = new List<Transform>();
        foreach (Transform c in treenormal) if (SpritePathClass(c.gameObject) == PixelTreeSprite) kids.Add(c);
        int stride = Mathf.Max(1, kids.Count / 30);
        for (int i = 0; i < kids.Count && protos.Count < 30; i += stride)
        {
            var pr = Object.Instantiate(kids[i].gameObject, holder.transform);
            pr.name = "pixeltree";
            protos.Add(pr);
        }
        Debug.Log($"[BRebuild] pixeltree prototypes: {protos.Count}");

        var border = ForestEditorHierarchy.GroupGO(ForestEditorHierarchy.OuterBorder);
        if (border == null) border = ForestEditorHierarchy.EnsureGroup(ForestEditorHierarchy.OuterBorder).gameObject;

        // ---- 1. Remove NewOuterBorder visual children (keep RoadEdgeBlockers) ----
        int removedBorderKids = 0;
        var blockers = new List<GameObject>();
        var borderKids = new List<Transform>();
        foreach (Transform c in border.transform) borderKids.Add(c);
        foreach (var c in borderKids)
        {
            if (c.name.StartsWith("RoadEdgeBlocker")) { blockers.Add(c.gameObject); continue; }
            Object.DestroyImmediate(c.gameObject);
            removedBorderKids++;
        }
        Debug.Log($"[BRebuild] Removed old NewOuterBorder visual children: {removedBorderKids} (kept {blockers.Count} road blockers)");

        // ---- 2. Remove NON-pixeltree objects inside the band from other parents ----
        int removedStray = 0;
        var strayLog = new Dictionary<string, int>();
        foreach (var root in new[] { "MapExpansion_North", "MapExpansion_East", "MapExpansion_South" })
        {
            var go = ForestEditorHierarchy.GroupGO(root); if (go == null) continue;
            var cs = new List<Transform>(); foreach (Transform c in go.transform) cs.Add(c);
            foreach (var c in cs)
            {
                Vector2 p = c.position;
                if (!InBorderBand(p, BorderThickness)) continue;
                string sc = SpritePathClass(c.gameObject);
                if (sc == PixelTreeSprite) continue; // keep leafy trees
                strayLog[sc] = strayLog.GetValueOrDefault(sc) + 1;
                Object.DestroyImmediate(c.gameObject);
                removedStray++;
            }
        }
        // Also sweep original ForestStuff containers for NON-tree props sitting in the band
        // (do NOT touch pixeltrees/anything outside the band => interior stays intact).
        foreach (var cn in new[] { "darktrees", "bushes", "stumps", "Logs" })
        {
            var cont = forest.transform.Find(cn); if (cont == null) continue;
            var cs = new List<Transform>(); foreach (Transform c in cont) cs.Add(c);
            foreach (var c in cs)
            {
                Vector2 p = c.position;
                if (!InBorderBand(p, BorderThickness)) continue;
                string sc = SpritePathClass(c.gameObject);
                if (sc == PixelTreeSprite) continue;
                strayLog[sc] = strayLog.GetValueOrDefault(sc) + 1;
                Object.DestroyImmediate(c.gameObject);
                removedStray++;
            }
        }
        Debug.Log($"[BRebuild] Removed non-pixeltree objects from band (other parents): {removedStray} -> {string.Join(", ", strayLog.Select(k => k.Key + "=" + k.Value))}");

        // ---- 3. Register surviving objects so the new wall doesn't stack on them ----
        foreach (var col in Object.FindObjectsByType<CircleCollider2D>(FindObjectsSortMode.None))
        {
            Vector2 p = col.transform.position;
            if (InBorderBand(p, BorderThickness + 3f))
                HAdd(new Placed { pos = p, radius = col.radius * Mathf.Max(Mathf.Abs(col.transform.lossyScale.x), Mathf.Abs(col.transform.lossyScale.y)) });
        }

        // ---- 4. Build thick pixeltree-only wall in all 4 bands (+corners), road openings on N/S ----
        int placed = 0;
        bool RoadOpen(float x) => x >= RoadOpenXMin && x <= RoadOpenXMax;
        float spacing = 1.0f;
        // iterate the full band region with a jittered grid
        for (float x = NewXMin; x <= NewXMax - 1; x += spacing)
            for (float y = NewYMin; y <= NewYMax - 1; y += spacing)
            {
                // inside-band test with irregular inner edge
                float dW = x - NewXMin, dE = (NewXMax - 1) - x, dS = y - NewYMin, dN = (NewYMax - 1) - y;
                float dMin = Mathf.Min(Mathf.Min(dW, dE), Mathf.Min(dS, dN));
                // irregular inner boundary: local thickness varies +-2 by noise
                float localThick = BorderThickness - 2f + Mathf.PerlinNoise(x * 0.16f + 3.1f, y * 0.16f + 7.7f) * 4f;
                if (dMin > localThick) continue;

                // road openings: leave the road corridor clear where it meets N & S edges
                bool nearNorth = dN <= BorderThickness, nearSouth = dS <= BorderThickness;
                if ((nearNorth || nearSouth) && RoadOpen(x)) continue;

                Vector2 c = new Vector2(x + (float)(rng.NextDouble() - 0.5) * 0.8f,
                                        y + (float)(rng.NextDouble() - 0.5) * 0.8f);
                if (HOverlaps(c, 1.4f, 0.5f)) continue;

                var proto = protos[rng.Next(protos.Count)];
                var clone = Object.Instantiate(proto, border.transform);
                clone.SetActive(true);
                clone.name = "pixeltree_border";
                clone.transform.position = new Vector3(c.x, c.y, proto.transform.position.z);
                clone.transform.rotation = Quaternion.Euler(0, 0, (float)(rng.NextDouble() * 360.0));
                clone.transform.localScale = proto.transform.localScale * (0.85f + (float)rng.NextDouble() * 0.4f);
                HAdd(new Placed { pos = c, radius = 1.4f });
                placed++;
            }
        Debug.Log($"[BRebuild] New pixeltree wall objects placed: {placed}");

        Object.DestroyImmediate(holder);
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        Debug.Log($"[BRebuild] Scene saved: {saved}. DONE.");
    }
}
