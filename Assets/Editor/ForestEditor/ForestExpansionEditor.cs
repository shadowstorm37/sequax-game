using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Deterministic map-expansion tool for MainForest.unity.
// Expands the hand-authored forest N/S/E to a square 130x130 map, keeping the
// west edge and the vertical road fixed. Reuses ONLY assets already in the project.
// Safe to delete after use. Nothing here runs at play time.
//
// Verified facts this tool relies on (measured from the original scene):
//   Original playable bounds : X [-41,47)  Y [-31,37)   (88 x 68)
//   Correct dark-brown ground: pixelgreenground_0..8  (sprite pixelgreenground.png)
//   Road                     : 3 tiles wide at X = -24,-23,-22, sheet road_0..8
//                              left col  (x=-24): road_0/3/6
//                              center    (x=-23): road_1/4/7  <- dashed centre line
//                              right col (x=-22): road_2/5/8
//                              row phase p = ((y%3)+3)%3 : p2->top(6/7/8) p0->mid(3/4/5) p1->bottom(0/1/2)
public static class ForestExpansionEditor
{
    private const int Seed = 20260719; // exposed, reproducible

    // Original bounds (measured, compressed).
    private const int OrigXMin = -41, OrigXMax = 47, OrigYMin = -31, OrigYMax = 37;
    // Final square bounds (130 x 130), west fixed at -41.
    private const int NewXMin = -41, NewXMax = 89, NewYMin = -62, NewYMax = 68;

    // Road columns.
    private const int RoadLeftX = -24, RoadCenterX = -23, RoadRightX = -22;
    // Road opening half-width margin for the outer border (keep border trees off the road).
    private const int RoadOpeningXMin = -26, RoadOpeningXMax = -20;
    // Keep-clear corridor for scattered props (a bit wider than the road).
    private const int CorridorXMin = -27, CorridorXMax = -19;

    // How deep (world units) the old N/S/E border walls reach inward; everything in
    // these bands is removed so the old walls become open interior.
    private const float OldBorderRemovalDepth = 10f;

    // Interior scatter: attempt-rate is set above the measured target (~0.08/unit^2) so
    // that after overlap rejection + clearings the REALIZED density lands near the original.
    private const float InteriorDensity = 0.16f;
    private const float EdgeDensity = 0.30f; // ramps up toward the new outer edge
    private const float EdgeRampDistance = 34f;

    private class Placed { public Vector2 pos; public float radius; }
    private static System.Random rng;
    private static Dictionary<Vector2Int, List<Placed>> hash;
    private const float HashCell = 4f;

    [MenuItem("Tools/Forest Editor/Expansion/Regenerate (Deterministic)")]
    public static void Regenerate()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.EndsWith("MainForest.unity"))
            scene = EditorSceneManager.OpenScene("Assets/Scenes/MainForest.unity", OpenSceneMode.Single);

        rng = new System.Random(Seed);
        hash = new Dictionary<Vector2Int, List<Placed>>();

        // ---- Load tile assets (existing project assets only) ----
        var groundTiles = new TileBase[9];
        for (int i = 0; i < 9; i++)
            groundTiles[i] = AssetDatabase.LoadAssetAtPath<TileBase>($"Assets/Art/Environment/Tiles/pixelgreenground_{i}.asset");
        var roadTiles = new TileBase[9];
        for (int i = 0; i < 9; i++)
            roadTiles[i] = AssetDatabase.LoadAssetAtPath<TileBase>($"Assets/Art/Environment/Tiles/road_{i}.asset");
        if (groundTiles.Any(t => t == null) || roadTiles.Any(t => t == null))
        { Debug.LogError("[Regen] Missing ground/road tile assets."); return; }

        GameObject gridGO = GameObject.Find("Grid");
        Tilemap ground = gridGO.transform.Find("Ground").GetComponent<Tilemap>();
        Tilemap paths = gridGO.transform.Find("Paths").GetComponent<Tilemap>();
        GameObject forestStuff = ForestEditorHierarchy.GroupGO(ForestEditorHierarchy.HandPlaced);

        // ---- Snapshot template prototypes BEFORE any removal (deactivated copies) ----
        var holder = new GameObject("__ExpansionTemplates_TEMP");
        holder.SetActive(false);
        List<GameObject> MakePrototypes(string container, int max)
        {
            var list = new List<GameObject>();
            var t = forestStuff.transform.Find(container);
            if (t == null) return list;
            var kids = new List<Transform>();
            foreach (Transform c in t) kids.Add(c);
            // spread the picks across the container for variety
            for (int i = 0; i < kids.Count && list.Count < max; i += Mathf.Max(1, kids.Count / max))
            {
                var proto = Object.Instantiate(kids[i].gameObject, holder.transform);
                proto.name = kids[i].name; // strip clone suffix noise
                list.Add(proto);
            }
            return list;
        }
        var treeProtos = MakePrototypes("treenormal", 24);
        treeProtos.AddRange(MakePrototypes("darktrees", 10));
        var bushProtos = MakePrototypes("bushes", 14);
        var stumpProtos = MakePrototypes("stumps", 6);
        var logProtos = MakePrototypes("Logs", 6);
        Debug.Log($"[Regen] Prototypes: trees={treeProtos.Count} bushes={bushProtos.Count} stumps={stumpProtos.Count} logs={logProtos.Count}");

        // ---- 1. GROUND: fill every empty cell in the new square with pixelgreenground only ----
        int groundAdded = 0;
        for (int x = NewXMin; x < NewXMax; x++)
            for (int y = NewYMin; y < NewYMax; y++)
            {
                var pos = new Vector3Int(x, y, 0);
                if (ground.GetTile(pos) != null) continue; // preserve all original ground
                ground.SetTile(pos, groundTiles[rng.Next(9)]);
                groundAdded++;
            }
        Debug.Log($"[Regen] Ground tiles added (pixelgreenground only): {groundAdded}");

        // ---- 1b. Sweep the WHOLE ground layer: replace any non-pixelgreenground tile
        //         (bright-green roughgrass/Floor-Grass, stray road tiles) with dark-brown ground.
        //         This also cleans pre-existing specks in the original area so no bright green remains.
        ground.CompressBounds();
        int groundCleaned = 0;
        foreach (var pos in ground.cellBounds.allPositionsWithin)
        {
            var t = ground.GetTile(pos);
            if (t == null || t.name.StartsWith("pixelgreenground")) continue;
            ground.SetTile(pos, groundTiles[rng.Next(9)]);
            groundCleaned++;
        }
        Debug.Log($"[Regen] Ground bright-green/stray tiles replaced with pixelgreenground: {groundCleaned}");

        // ---- 2. ROAD: deterministic 3-column repeat across the full height ----
        int roadSet = 0;
        int[] baseByPhase = { 3, 0, 6 }; // index by phase p -> base tile of that row (p0 mid=3, p1 bottom=0, p2 top=6)
        for (int y = NewYMin; y < NewYMax; y++)
        {
            int p = ((y % 3) + 3) % 3;
            int rowBase = baseByPhase[p];
            for (int x = RoadLeftX; x <= RoadRightX; x++)
            {
                int idx = rowBase + (x - RoadLeftX); // left=+0, center=+1, right=+2
                paths.SetTile(new Vector3Int(x, y, 0), roadTiles[idx]);
                roadSet++;
            }
        }
        Debug.Log($"[Regen] Road tiles set (deterministic): {roadSet}");

        ground.CompressBounds();
        paths.CompressBounds();

        // ---- Parents ----
        Transform pN = Root("MapExpansion_North");
        Transform pE = Root("MapExpansion_East");
        Transform pS = Root("MapExpansion_South");
        Transform pBorder = Root("NewOuterBorder");
        Transform pZones = Root("ReservedLandmarkZones");

        // ---- Reserved landmark zones (empty markers, kept clear) ----
        var zones = new List<Rect>();
        void Zone(string name, float cx, float cy, float w, float h)
        {
            var go = new GameObject(name);
            go.transform.position = new Vector3(cx, cy, 0);
            go.transform.SetParent(pZones, true);
            zones.Add(new Rect(cx - w / 2f, cy - h / 2f, w, h));
        }
        Zone("LandmarkZone_North_01", 6, 52, 18, 15);
        Zone("LandmarkZone_East_01", 68, 20, 16, 16);
        Zone("LandmarkZone_East_02", 68, -14, 16, 16);
        Zone("LandmarkZone_South_01", 8, -49, 18, 15);

        // ---- 3. Remove old N/S/E border walls (and their colliders live on the objects) ----
        int removed = RemoveOldBorders(forestStuff);
        Debug.Log($"[Regen] Old N/S/E border objects removed: {removed}");

        // ---- Register all surviving (preserved) objects so nothing overlaps them ----
        foreach (var col in forestStuff.GetComponentsInChildren<CircleCollider2D>())
            Add(new Placed { pos = col.transform.position, radius = ColliderWorldRadius(col) });

        // ---- 4. Interior scatter over new territory + opened bands ----
        // Preserved original clearing (keep as authored, add nothing inside it).
        Rect preserved = Rect.MinMaxRect(OrigXMin, OrigYMin + OldBorderRemovalDepth,
                                         OrigXMax - OldBorderRemovalDepth, OrigYMax - OldBorderRemovalDepth);
        int scattered = ScatterInterior(treeProtos, bushProtos, stumpProtos, logProtos, zones, preserved, pN, pE, pS);
        Debug.Log($"[Regen] Interior scatter objects placed: {scattered}");

        // ---- 5. New dense outer border (square perimeter), with road openings N & S ----
        int borderCount = BuildOuterBorder(treeProtos, pBorder);
        Debug.Log($"[Regen] New outer border trees placed: {borderCount}");

        // ---- 5b. Boundary blockers across the road openings (edge collision, not internal) ----
        AddRoadEdgeBlocker(pBorder, "RoadEdgeBlocker_North", (RoadLeftX + RoadRightX) / 2f, NewYMax - 0.4f);
        AddRoadEdgeBlocker(pBorder, "RoadEdgeBlocker_South", (RoadLeftX + RoadRightX) / 2f, NewYMin + 0.4f);

        // ---- cleanup templates ----
        Object.DestroyImmediate(holder);

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Regen] Scene saved: {saved}. DONE.");
    }

    // Expansion-owned groups live under ForestEnvironment (kept out of the scene root).
    private static Transform Root(string name) => ForestEditorHierarchy.EnsureGroup(name);

    private static float ColliderWorldRadius(CircleCollider2D c)
        => c.radius * Mathf.Max(Mathf.Abs(c.transform.lossyScale.x), Mathf.Abs(c.transform.lossyScale.y));

    private static Vector2Int Key(Vector2 p) => new Vector2Int(Mathf.FloorToInt(p.x / HashCell), Mathf.FloorToInt(p.y / HashCell));
    private static void Add(Placed p)
    {
        var k = Key(p.pos);
        if (!hash.TryGetValue(k, out var l)) { l = new List<Placed>(); hash[k] = l; }
        l.Add(p);
    }
    private static bool Overlaps(Vector2 pos, float radius, float factor)
    {
        var key = Key(pos);
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

    private static bool InZone(Vector2 p, List<Rect> zones, float pad)
    {
        foreach (var z in zones)
            if (new Rect(z.x - pad, z.y - pad, z.width + pad * 2, z.height + pad * 2).Contains(p)) return true;
        return false;
    }

    private static int RemoveOldBorders(GameObject forestStuff)
    {
        string[] containers = { "treenormal", "darktrees", "bushes", "stumps", "Logs" };
        var toRemove = new List<GameObject>();
        foreach (var cn in containers)
        {
            var c = forestStuff.transform.Find(cn);
            if (c == null) continue;
            var kids = new List<Transform>();
            foreach (Transform k in c) kids.Add(k);
            foreach (var k in kids)
            {
                Vector2 p = k.position;
                bool north = p.y >= OrigYMax - OldBorderRemovalDepth;
                bool south = p.y <= OrigYMin + OldBorderRemovalDepth;
                bool east = p.x >= OrigXMax - OldBorderRemovalDepth;
                // West is preserved: do not remove purely-west objects. But a NW/SE corner
                // that is also north/south/east qualifies. Exclude the west wall band itself.
                bool westWall = p.x <= OrigXMin + OldBorderRemovalDepth;
                if ((north || south || east) && !(westWall && !north && !south))
                    toRemove.Add(k.gameObject);
            }
        }
        foreach (var g in toRemove) Object.DestroyImmediate(g);
        return toRemove.Count;
    }

    private static GameObject Pick(List<GameObject> pool) => pool[rng.Next(pool.Count)];

    private static GameObject PlaceClone(GameObject proto, Vector2 pos, Transform parent, float scaleJitter)
    {
        var clone = Object.Instantiate(proto, parent);
        clone.SetActive(true);
        clone.name = proto.name + "_exp";
        clone.transform.position = new Vector3(pos.x, pos.y, proto.transform.position.z);
        clone.transform.rotation = Quaternion.Euler(0, 0, (float)(rng.NextDouble() * 360.0));
        clone.transform.localScale = proto.transform.localScale * scaleJitter;
        return clone;
    }

    private static int ScatterInterior(List<GameObject> trees, List<GameObject> bushes,
        List<GameObject> stumps, List<GameObject> logs, List<Rect> zones, Rect preserved,
        Transform pN, Transform pE, Transform pS)
    {
        int placed = 0;
        // Poisson-ish: jittered grid of candidate points; per-point probability from density.
        const float step = 1.6f;
        for (float gx = NewXMin + 1; gx < NewXMax - 1; gx += step)
            for (float gy = NewYMin + 1; gy < NewYMax - 1; gy += step)
            {
                Vector2 c = new Vector2(
                    gx + (float)(rng.NextDouble() - 0.5) * step,
                    gy + (float)(rng.NextDouble() - 0.5) * step);

                if (preserved.Contains(c)) continue;                 // keep original clearing untouched
                if (c.x >= CorridorXMin && c.x <= CorridorXMax) continue; // road corridor
                if (InZone(c, zones, 2.5f)) continue;                // reserved landmark zones
                // leave the outermost band for the dense wall builder
                if (c.x > NewXMax - 6 || c.x < NewXMin + 1 || c.y > NewYMax - 6 || c.y < NewYMin + 6) continue;

                // density: base interior, ramping toward the new N/E/S outer edges
                float dEdge = Mathf.Min(NewXMax - c.x, Mathf.Min(NewYMax - c.y, c.y - NewYMin));
                float t = 1f - Mathf.Clamp01(dEdge / EdgeRampDistance);
                float density = Mathf.Lerp(InteriorDensity, EdgeDensity, t);
                // open clearings via low-frequency noise (kept as navigable pockets)
                float clearing = Mathf.PerlinNoise(c.x * 0.05f + 11.3f, c.y * 0.05f + 4.7f);
                if (clearing > 0.80f) density *= 0.14f;
                // cluster bias: another noise field concentrates objects into irregular clumps
                float clump = Mathf.PerlinNoise(c.x * 0.12f + 30f, c.y * 0.12f + 60f);
                density *= Mathf.Lerp(0.5f, 1.7f, clump);

                // probability per candidate cell = density * cellArea
                if (rng.NextDouble() > density * step * step) continue;

                // choose type by original proportions (~92% tree, 6% bush, 1% stump, 1% log)
                double r = rng.NextDouble();
                List<GameObject> pool; float radius;
                if (r < 0.92 && trees.Count > 0) { pool = trees; radius = 2.0f; }
                else if (r < 0.98 && bushes.Count > 0) { pool = bushes; radius = 1.3f; }
                else if (r < 0.99 && stumps.Count > 0) { pool = stumps; radius = 1.5f; }
                else if (logs.Count > 0) { pool = logs; radius = 1.5f; }
                else if (trees.Count > 0) { pool = trees; radius = 2.0f; }
                else continue;

                if (Overlaps(c, radius, 0.72f)) continue;

                Transform parent = c.y >= OrigYMax ? pN : (c.y < OrigYMin ? pS : pE);
                PlaceClone(Pick(pool), c, parent, 0.85f + (float)rng.NextDouble() * 0.35f);
                Add(new Placed { pos = c, radius = radius });
                placed++;
            }
        return placed;
    }

    private static void AddRoadEdgeBlocker(Transform parent, string name, float cx, float cy)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = new Vector3(cx, cy, 0);
        var box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(4.5f, 0.6f); // spans the 3-wide road at the map edge
    }

    private static int BuildOuterBorder(List<GameObject> trees, Transform parent)
    {
        if (trees.Count == 0) return 0;
        int placed = 0;
        const float bandDepth = 6f;     // wall thickness
        const float spacing = 1.0f;     // dense
        bool RoadOpening(float x) => x >= RoadOpeningXMin && x <= RoadOpeningXMax;

        // helper to fill a band; edgeSign/axis chosen per side
        void FillHorizontalBand(float yInner, float yOuter, bool isNorth)
        {
            for (float x = NewXMin; x <= NewXMax - 1; x += spacing)
                for (float y = Mathf.Min(yInner, yOuter); y <= Mathf.Max(yInner, yOuter); y += spacing)
                {
                    if (RoadOpening(x)) continue; // leave road entrance/exit open
                    // irregular inner edge via noise
                    float edgeNoise = Mathf.PerlinNoise(x * 0.15f + (isNorth ? 5f : 90f), 0.5f) * 2.2f;
                    if (isNorth && y < yInner + edgeNoise) continue;
                    if (!isNorth && y > yOuter - edgeNoise) continue;
                    Vector2 c = new Vector2(x + (float)(rng.NextDouble() - 0.5) * 0.7f,
                                            y + (float)(rng.NextDouble() - 0.5) * 0.7f);
                    if (Overlaps(c, 1.4f, 0.5f)) continue;
                    PlaceClone(Pick(trees), c, parent, 0.8f + (float)rng.NextDouble() * 0.5f);
                    Add(new Placed { pos = c, radius = 1.4f });
                    placed++;
                }
        }
        void FillVerticalBand(float xInner, float xOuter, float yMin, float yMax, bool isEast)
        {
            for (float y = yMin; y <= yMax; y += spacing)
                for (float x = Mathf.Min(xInner, xOuter); x <= Mathf.Max(xInner, xOuter); x += spacing)
                {
                    float edgeNoise = Mathf.PerlinNoise(y * 0.15f + (isEast ? 40f : 70f), 0.5f) * 2.2f;
                    if (isEast && x < xInner + edgeNoise) continue;
                    if (!isEast && x > xOuter - edgeNoise) continue;
                    Vector2 c = new Vector2(x + (float)(rng.NextDouble() - 0.5) * 0.7f,
                                            y + (float)(rng.NextDouble() - 0.5) * 0.7f);
                    if (Overlaps(c, 1.4f, 0.5f)) continue;
                    PlaceClone(Pick(trees), c, parent, 0.8f + (float)rng.NextDouble() * 0.5f);
                    Add(new Placed { pos = c, radius = 1.4f });
                    placed++;
                }
        }

        // North & South full-width walls (with road opening).
        FillHorizontalBand(NewYMax - 1 - bandDepth, NewYMax - 1, true);
        FillHorizontalBand(NewYMin + 1, NewYMin + 1 + bandDepth, false);
        // East full-height wall.
        FillVerticalBand(NewXMax - 1 - bandDepth, NewXMax - 1, NewYMin + 1, NewYMax - 1, true);
        // West: original wall covers y[-31,37); close the NEW west portions only.
        FillVerticalBand(NewXMin + 1, NewXMin + 1 + bandDepth, NewYMin + 1, OrigYMin - 1, false);
        FillVerticalBand(NewXMin + 1, NewXMin + 1 + bandDepth, OrigYMax + 1, NewYMax - 1, false);

        return placed;
    }
}
