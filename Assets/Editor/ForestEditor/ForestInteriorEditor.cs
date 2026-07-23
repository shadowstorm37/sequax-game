using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// READ-ONLY inspection for interior-population planning. Measures the preserved original
// interior as the statistical reference, plus player/monster colliders and landmark zones.
// Does not modify or save the scene.
public static class ForestInteriorEditor
{
    private const int MapXMin = -41, MapXMax = 89, MapYMin = -62, MapYMax = 68;

    // Type bucket from a verified sprite asset path (not filename-trusting: sprites were viewed earlier).
    public static string TypeOf(GameObject go)
    {
        var sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return "none";
        string n = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(sr.sprite));
        switch (n)
        {
            case "pixeltree": return "tree";        // leafy
            case "tree": return "dead";             // bare branchy (darktree)
            case "stump": case "pixelstump": return "stump";
            case "realLog": return "log";
            case "pixellargetrunk": return "largetree"; // intentionally big
            case "pixelbush": return "bush";
            default: return "other:" + n;
        }
    }

    private static IEnumerable<Transform> OriginalPlacements()
    {
        var forest = ForestEditorHierarchy.GroupGO(ForestEditorHierarchy.HandPlaced);
        if (forest == null) yield break;
        foreach (var cn in new[] { "treenormal", "darktrees", "bushes", "stumps", "Logs" })
        {
            var cont = forest.transform.Find(cn);
            if (cont == null) continue;
            foreach (Transform c in cont) yield return c;
        }
    }

    [MenuItem("Tools/Forest Editor/Validation/Interior Inspect Reference (Read Only)")]
    public static void InspectReference()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.EndsWith("MainForest.unity"))
            scene = EditorSceneManager.OpenScene("Assets/Scenes/MainForest.unity", OpenSceneMode.Single);

        // ---- Colliders: player & monster ----
        void ReportCollider(string objName)
        {
            var go = GameObject.Find(objName);
            if (go == null) { Debug.Log($"[I] {objName}: NOT FOUND"); return; }
            var cols = go.GetComponentsInChildren<Collider2D>();
            if (cols.Length == 0) { Debug.Log($"[I] {objName}: no Collider2D"); return; }
            foreach (var c in cols)
            {
                float r = 0;
                if (c is CircleCollider2D cc) r = cc.radius * Mathf.Max(Mathf.Abs(c.transform.lossyScale.x), Mathf.Abs(c.transform.lossyScale.y));
                else if (c is CapsuleCollider2D cap) r = Mathf.Max(cap.size.x, cap.size.y) / 2f * Mathf.Max(Mathf.Abs(c.transform.lossyScale.x), Mathf.Abs(c.transform.lossyScale.y));
                else if (c is BoxCollider2D bx) r = Mathf.Max(bx.size.x, bx.size.y) / 2f * Mathf.Max(Mathf.Abs(c.transform.lossyScale.x), Mathf.Abs(c.transform.lossyScale.y));
                Debug.Log($"[I] {objName} collider: {c.GetType().Name} isTrigger={c.isTrigger} worldExtentRadius~{r:F2} (bounds size {c.bounds.size})");
            }
        }
        ReportCollider("Player");
        ReportCollider("Monster");

        // ---- Landmark zones ----
        Debug.Log("[I] Landmark zones:");
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.name.Contains("LandmarkZone"))
                Debug.Log($"    '{t.name}' pos=({t.position.x:F1},{t.position.y:F1}) parent='{(t.parent ? t.parent.name : "-")}'");

        // ---- Key landmarks ----
        foreach (var nm in new[] { "Player", "Cabin", "tent", "Monster" })
        { var g = GameObject.Find(nm); if (g) Debug.Log($"[I] {nm} pos=({g.transform.position.x:F1},{g.transform.position.y:F1})"); }

        // ---- Hierarchy counts ----
        void Count(string nm) { var g = GameObject.Find(nm); Debug.Log($"[I] container '{nm}': {(g ? g.transform.childCount.ToString() : "MISSING")}"); }
        var fs = ForestEditorHierarchy.GroupGO(ForestEditorHierarchy.HandPlaced);
        if (fs) foreach (Transform c in fs.transform) Debug.Log($"[I] {ForestEditorHierarchy.HandPlaced}/{c.name}: {c.childCount}");
        Count("MapExpansion_North"); Count("MapExpansion_East"); Count("MapExpansion_South");
        Count("NewOuterBorder"); Count("ReservedLandmarkZones"); Count("GeneratedInterior");

        // ---- Reference stats over preserved original clearing (exclude cabin/tent/road/west border) ----
        // Preserved clearing was roughly x[-41,37] y[-21,27]; use clean sub-region for forest stats.
        bool InRef(Vector2 p)
        {
            if (p.x < -33 || p.x > 36 || p.y < -20 || p.y > 26) return false; // inside clearing, off west border
            if (p.x >= -27 && p.x <= -19) return false;                        // road corridor
            if (p.x >= -8 && p.x <= 8 && p.y >= -12 && p.y <= 0) return false;  // cabin footprint
            if (p.x >= -2 && p.x <= 11 && p.y >= 14 && p.y <= 26) return false; // tent area
            return true;
        }
        var pts = new List<(Vector2 p, string t, float scale, float rot)>();
        foreach (var tr in OriginalPlacements())
        {
            Vector2 p = tr.position;
            if (!InRef(p)) continue;
            pts.Add((p, TypeOf(tr.gameObject), tr.lossyScale.x, tr.eulerAngles.z));
        }
        int n = pts.Count;
        float refArea = 0;
        // approximate reference area = clearing area minus excluded rects (rough)
        refArea = (36 - (-33)) * (26 - (-20)) - (8 * 12) /*cabin*/ - (13 * 12) /*tent*/ - (8 * 46) /*road*/;
        Debug.Log($"[Iref] Reference objects: {n}, approx area {refArea:F0} u^2 => density {n / refArea:F4}/u^2");

        var byType = pts.GroupBy(x => x.t).ToDictionary(g => g.Key, g => g.Count());
        Debug.Log("[Iref] Type proportions: " + string.Join(", ", byType.OrderByDescending(k => k.Value).Select(k => $"{k.Key}={k.Value} ({100f * k.Value / n:F0}%)")));

        // nearest-neighbour distances
        var nn = new List<float>();
        for (int i = 0; i < pts.Count; i++)
        {
            float best = float.MaxValue;
            for (int j = 0; j < pts.Count; j++)
            {
                if (i == j) continue;
                float d = (pts[i].p - pts[j].p).magnitude;
                if (d < best) best = d;
            }
            if (best < float.MaxValue) nn.Add(best);
        }
        nn.Sort();
        if (nn.Count > 0)
            Debug.Log($"[Iref] Nearest-neighbour dist: min={nn.First():F2}, p10={nn[nn.Count / 10]:F2}, median={nn[nn.Count / 2]:F2}, avg={nn.Average():F2}, max={nn.Last():F2}");

        // isolated vs grouped: objects whose NN > 6 are 'isolated'
        int isolated = nn.Count(d => d > 6f);
        Debug.Log($"[Iref] Isolated (NN>6u): {isolated}/{n} ({100f * isolated / Mathf.Max(1, n):F0}%); grouped: {100f * (n - isolated) / Mathf.Max(1, n):F0}%");

        // scale & rotation variation
        var scales = pts.Select(x => x.scale).OrderBy(v => v).ToList();
        Debug.Log($"[Iref] Scale range: min={scales.First():F2} median={scales[scales.Count / 2]:F2} max={scales.Last():F2}");
        Debug.Log($"[Iref] Rotation: appears {(pts.Select(x => Mathf.Round(x.rot)).Distinct().Count() > 10 ? "continuous/random" : "limited")} ({pts.Select(x => Mathf.Round(x.rot)).Distinct().Count()} distinct z-angles)");

        // ---- Occupancy / open-space analysis over the reference (1u grid, collider-based) ----
        // build blocked grid from actual colliders in reference
        int gx0 = -33, gx1 = 36, gy0 = -20, gy1 = 26;
        int W = gx1 - gx0, H = gy1 - gy0;
        var blocked = new bool[W, H];
        int blockedCells = 0;
        foreach (var c in Object.FindObjectsByType<CircleCollider2D>(FindObjectsSortMode.None))
        {
            Vector2 cp = c.transform.position;
            if (!InRef(cp)) continue;
            float rr = c.radius * Mathf.Max(Mathf.Abs(c.transform.lossyScale.x), Mathf.Abs(c.transform.lossyScale.y));
            for (int x = Mathf.FloorToInt(cp.x - rr); x <= Mathf.CeilToInt(cp.x + rr); x++)
                for (int y = Mathf.FloorToInt(cp.y - rr); y <= Mathf.CeilToInt(cp.y + rr); y++)
                {
                    int ix = x - gx0, iy = y - gy0;
                    if (ix < 0 || iy < 0 || ix >= W || iy >= H) continue;
                    if (!blocked[ix, iy] && (new Vector2(x, y) - cp).magnitude <= rr) { blocked[ix, iy] = true; blockedCells++; }
                }
        }
        Debug.Log($"[Iref] Open-ground fraction in reference: {100f * (1f - (float)blockedCells / (W * H)):F0}% open ({blockedCells}/{W * H} cells blocked)");

        // horizontal open-run lengths (corridor widths)
        var runs = new List<int>();
        for (int y = 0; y < H; y++)
        {
            int run = 0;
            for (int x = 0; x < W; x++)
            {
                if (!blocked[x, y]) run++;
                else { if (run > 0) runs.Add(run); run = 0; }
            }
            if (run > 0) runs.Add(run);
        }
        runs.Sort();
        if (runs.Count > 0)
            Debug.Log($"[Iref] Open horizontal-run (corridor width) distribution: min={runs.First()}, median={runs[runs.Count / 2]}, avg={runs.Average():F1}, max={runs.Last()} (units)");

        Debug.Log("[I] DONE (read-only)");
    }

    // ===================== DENSITY / WALL INSPECTION (read-only) =====================
    [MenuItem("Tools/Forest Editor/Validation/Interior Inspect Density & Walls (Read Only)")]
    public static void InspectDensity()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.EndsWith("MainForest.unity"))
            scene = EditorSceneManager.OpenScene("Assets/Scenes/MainForest.unity", OpenSceneMode.Single);

        // ---- per-type world scale + collider radius (so we don't shrink stumps/bushes/logs) ----
        var scaleByType = new Dictionary<string, List<float>>();
        var radByType = new Dictionary<string, List<float>>();
        void Scan(IEnumerable<Transform> src)
        {
            foreach (var tr in src)
            {
                string t = TypeOf(tr.gameObject);
                if (!scaleByType.ContainsKey(t)) { scaleByType[t] = new List<float>(); radByType[t] = new List<float>(); }
                scaleByType[t].Add(tr.lossyScale.x);
                var col = tr.GetComponentInChildren<CircleCollider2D>();
                if (col) radByType[t].Add(col.radius * Mathf.Max(Mathf.Abs(col.transform.lossyScale.x), Mathf.Abs(col.transform.lossyScale.y)));
            }
        }
        Scan(OriginalPlacements());
        // include the large-trunk singletons
        var fs = ForestEditorHierarchy.GroupGO(ForestEditorHierarchy.HandPlaced);
        foreach (Transform c in fs.transform) if (c.name.StartsWith("pixellargetrunk")) Scan(new[] { c });
        Debug.Log("[D] Per-type ORIGINAL world-scale (min/median/max) and collider radius:");
        foreach (var kv in scaleByType.OrderBy(k => k.Key))
        {
            var s = kv.Value.OrderBy(v => v).ToList(); var r = radByType[kv.Key].OrderBy(v => v).ToList();
            Debug.Log($"    {kv.Key,-9}: n={s.Count} scale[{s.First():F2}/{s[s.Count / 2]:F2}/{s.Last():F2}] " +
                      $"colliderR[{(r.Count > 0 ? $"{r.First():F2}/{r[r.Count / 2]:F2}/{r.Last():F2}" : "-")}]");
        }
        // current generated interior scales (to detect shrinkage)
        var gen = ForestEditorHierarchy.GroupGO(ForestEditorHierarchy.GeneratedInterior);
        if (gen)
        {
            var genScale = new Dictionary<string, List<float>>();
            foreach (Transform c in gen.transform)
            {
                string t = TypeOf(c.gameObject); if (!genScale.ContainsKey(t)) genScale[t] = new List<float>();
                genScale[t].Add(c.lossyScale.x);
            }
            Debug.Log("[D] Current GeneratedInterior world-scale by type (min/median/max):");
            foreach (var kv in genScale.OrderBy(k => k.Key))
            { var s = kv.Value.OrderBy(v => v).ToList(); Debug.Log($"    {kv.Key,-9}: n={s.Count} scale[{s.First():F2}/{s[s.Count / 2]:F2}/{s.Last():F2}]"); }
        }

        // ---- collision proximity: nearest-neighbour EDGE-GAP among interior obstacles ----
        // (exclude outer border, cabin walls, player, monster)
        var ob = ForestEditorHierarchy.GroupGO(ForestEditorHierarchy.OuterBorder); var cab = GameObject.Find("Cabin");
        var pl = GameObject.Find("Player"); var mo = GameObject.Find("Monster");
        var obstacles = new List<(Vector2 p, float r)>();
        foreach (var col in Object.FindObjectsByType<CircleCollider2D>(FindObjectsSortMode.None))
        {
            if (col.isTrigger) continue; var tr = col.transform;
            if (ob && tr.IsChildOf(ob.transform)) continue;
            if (cab && tr.IsChildOf(cab.transform)) continue;
            if (pl && tr.IsChildOf(pl.transform)) continue;
            if (mo && tr.IsChildOf(mo.transform)) continue;
            obstacles.Add((tr.position, col.radius * Mathf.Max(Mathf.Abs(tr.lossyScale.x), Mathf.Abs(tr.lossyScale.y))));
        }
        var gaps = new List<float>();
        for (int i = 0; i < obstacles.Count; i++)
        {
            float best = float.MaxValue;
            for (int j = 0; j < obstacles.Count; j++)
            {
                if (i == j) continue;
                float edge = (obstacles[i].p - obstacles[j].p).magnitude - obstacles[i].r - obstacles[j].r;
                if (edge < best) best = edge;
            }
            if (best < float.MaxValue) gaps.Add(best);
        }
        gaps.Sort();
        if (gaps.Count > 0)
        {
            int overlap = gaps.Count(g => g < 0), tight = gaps.Count(g => g >= 0 && g < 1.6f);
            Debug.Log($"[D] Interior obstacles: {obstacles.Count}. Nearest-neighbour EDGE-GAP: min={gaps.First():F2}, p10={gaps[gaps.Count / 10]:F2}, median={gaps[gaps.Count / 2]:F2}");
            Debug.Log($"[D] Edge-gaps < 0 (colliders OVERLAP): {overlap} ({100f * overlap / gaps.Count:F0}%); 0..1.6u (tight, sub-comfortable): {tight} ({100f * tight / gaps.Count:F0}%). Player Ø=1.0.");
        }

        // ---- wall / reachability with COMFORTABLE clearance ----
        WallScan(obstacles, 0.5f + 0.8f); // player radius 0.5 + comfortable margin 0.8
        Debug.Log("[D] DONE (read-only)");
    }

    private static void WallScan(List<(Vector2 p, float r)> obstacles, float inflate)
    {
        int gx0 = -34, gy0 = -60, W = 118, H = 126; // 1u grid over interior
        var blocked = new bool[W, H];
        foreach (var (p, r) in obstacles)
            for (int x = Mathf.FloorToInt(p.x - r - inflate); x <= Mathf.CeilToInt(p.x + r + inflate); x++)
                for (int y = Mathf.FloorToInt(p.y - r - inflate); y <= Mathf.CeilToInt(p.y + r + inflate); y++)
                { int ix = x - gx0, iy = y - gy0; if (ix < 0 || iy < 0 || ix >= W || iy >= H) continue; if ((new Vector2(x, y) - p).magnitude <= r + inflate) blocked[ix, iy] = true; }
        // flood from spawn
        var vis = new bool[W, H]; var q = new Queue<Vector2Int>();
        var st = new Vector2Int(0 - gx0, 0 - gy0);
        if (!blocked[st.x, st.y]) { vis[st.x, st.y] = true; q.Enqueue(st); }
        int reach = 0;
        while (q.Count > 0) { var c = q.Dequeue(); reach++; foreach (var d in new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) }) { int nx = c.x + d.x, ny = c.y + d.y; if (nx < 0 || ny < 0 || nx >= W || ny >= H || vis[nx, ny] || blocked[nx, ny]) continue; vis[nx, ny] = true; q.Enqueue(new Vector2Int(nx, ny)); } }
        int open = 0, pinch = 0;
        for (int x = 0; x < W; x++) for (int y = 0; y < H; y++)
            {
                if (!blocked[x, y]) open++;
                if (vis[x, y] && x > 0 && y > 0 && x < W - 1 && y < H - 1) { int f = 0; for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) if (!(dx == 0 && dy == 0) && !blocked[x + dx, y + dy]) f++; if (f <= 3) pinch++; }
            }
        Debug.Log($"[D] COMFORTABLE-clearance wall scan (inflate={inflate:F2}=playerR+0.8 margin): open={open}, reachable-from-spawn={reach} ({100f * reach / Mathf.Max(1, open):F0}% of open), pinch cells(<=3 free nb)={pinch}, unreachable-open={open - reach}");
    }

    // ===================== GENERATION =====================
    // Exposed, reproducible settings.
    public const int Seed = 20260721;
    public const float TargetDensity = 0.037f;   // objects per u^2 (from reference)
    public const float EdgeDensityMult = 1.7f;   // multiplier near outer border
    public const float LandmarkBuffer = 6f;      // clear buffer beyond landmark half-size
    public const float PathClearance = 3.0f;     // no new object center within this of a path centerline
    public const float PlayerRadius = 0.5f;      // measured
    public const float ClusterGrid = 8f;         // spacing between cluster centers (controls density)
    public const float ComfortGap = 1.6f;        // min clear gap between ANY two collider edges (player Ø1.0 + 0.6 margin)

    private const int OXMin = -32, OXMax = 82, OYMin = -56, OYMax = 62; // interior bounds (inside outer border)

    // cumulative type proportions: tree .41, dead .72, bush .88, log .94, stump 1.0
    // pre-rejection weights biased slightly toward trees so post-overlap proportions land near
    // the reference (trees have the largest collider and are rejected most often).
    private static readonly (string type, float cum, float radius)[] TypeTable =
    {
        // pre-weights heavily biased to trees: trees have the largest collider and are rejected most
        // by the ComfortGap, so this lands leafy/dead trees as the majority (NOT stump-dominated).
        ("tree", 0.60f, 1.4f), ("dead", 0.80f, 1.2f), ("bush", 0.90f, 1.0f), ("log", 0.95f, 1.5f), ("stump", 1.0f, 1.5f),
    };

    private class P { public Vector2 pos; public float r; }
    private static Dictionary<Vector2Int, List<P>> grid;         // overlap: existing + border + new
    private static Dictionary<Vector2Int, List<P>> clearingGrid; // density-skip: preserved clearing only (static)
    private const float Cell = 4f;
    private static Vector2Int K(Vector2 p) => new Vector2Int(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.y / Cell));
    private static void AddTo(Dictionary<Vector2Int, List<P>> g, Vector2 p, float r) { var k = K(p); if (!g.TryGetValue(k, out var l)) { l = new List<P>(); g[k] = l; } l.Add(new P { pos = p, r = r }); }
    private static void Add(Vector2 p, float r) => AddTo(grid, p, r);
    private static bool Hits(Vector2 p, float r, float f)
    {
        var k = K(p);
        for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                if (grid.TryGetValue(new Vector2Int(k.x + dx, k.y + dy), out var l))
                    foreach (var q in l) { float md = (q.r + r) * f; if ((q.pos - p).sqrMagnitude < md * md) return true; }
        return false;
    }
    // reject if any obstacle's collider EDGE is closer than `gap` to the new object's edge
    // (guarantees a passable gap between every pair -> no accidental walls / tiny squeezes).
    private static bool TooCloseGap(Vector2 p, float r, float gap)
    {
        var k = K(p);
        for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                if (grid.TryGetValue(new Vector2Int(k.x + dx, k.y + dy), out var l))
                    foreach (var q in l) { float md = q.r + r + gap; if ((q.pos - p).sqrMagnitude < md * md) return true; }
        return false;
    }
    // intended per-type world scale (preserve authored sizes; do NOT shrink stumps/bushes/logs)
    // and base (unscaled) collider radius, derived from the measured originals.
    private static (float scale, float baseR) TypeScaleRadius(string type, System.Random rng)
    {
        double u = rng.NextDouble();
        switch (type)
        {
            case "tree": return (0.40f + (float)u * 0.90f, 2.0f);   // 0.40..1.30 (median ~0.75), r=2.0*scale
            case "dead": return (0.60f + (float)u * 0.70f, 0.87f);  // 0.60..1.30
            case "bush": return (0.21f + (float)u * 0.06f, 2.0f);   // ~0.23 (authored), small collider
            case "stump": return (0.35f + (float)u * 0.10f, 1.10f); // ~0.40 (authored)
            case "log": return (0.45f + (float)u * 0.12f, 1.00f);   // ~0.50 (authored)
            default: return (0.7f, 1.4f);
        }
    }
    // local density of PRESERVED clearing objects only (so we don't over-fill the hand-placed clearing,
    // but still generate/ramp near the outer border).
    private static int CountWithin(Vector2 p, float rad)
    {
        int c = 0; var k = K(p); int span = Mathf.CeilToInt(rad / Cell);
        for (int dx = -span; dx <= span; dx++) for (int dy = -span; dy <= span; dy++)
                if (clearingGrid.TryGetValue(new Vector2Int(k.x + dx, k.y + dy), out var l))
                    foreach (var q in l) if ((q.pos - p).sqrMagnitude < rad * rad) c++;
        return c;
    }

    // path clearance hash
    private static HashSet<Vector2Int> pathCells;
    private static bool NearPath(Vector2 p) => NearPathR(p, PathClearance);
    private static bool NearPathR(Vector2 p, float clearance)
    {
        int cx = Mathf.RoundToInt(p.x), cy = Mathf.RoundToInt(p.y);
        int rad = Mathf.CeilToInt(clearance);
        for (int dx = -rad; dx <= rad; dx++) for (int dy = -rad; dy <= rad; dy++)
                if (dx * dx + dy * dy <= clearance * clearance && pathCells.Contains(new Vector2Int(cx + dx, cy + dy))) return true;
        return false;
    }
    private static void BuildPaths(System.Random rng)
    {
        var wp = new Dictionary<string, Vector2>
        {
            ["spawn"] = new Vector2(0, 0), ["cabin"] = new Vector2(0, 2), ["tent"] = new Vector2(4.5f, 15f),
            ["center"] = new Vector2(20, 0), ["nHub"] = new Vector2(12, 44), ["sHub"] = new Vector2(12, -42),
            ["eHub"] = new Vector2(58, 3), ["farE"] = new Vector2(78, 0),
            ["LN"] = new Vector2(6, 44), ["LS"] = new Vector2(8, -41), ["LE1"] = new Vector2(60, 20), ["LE2"] = new Vector2(60, -14),
            ["roadN"] = new Vector2(-20, 56), ["roadS"] = new Vector2(-20, -54),
            ["NE"] = new Vector2(50, 42), ["SE"] = new Vector2(50, -42), ["NW"] = new Vector2(-16, 40), ["SW"] = new Vector2(-16, -40),
            ["cSE"] = new Vector2(72, -48), ["cNE"] = new Vector2(72, 48), ["cSW"] = new Vector2(-24, -50), ["cNW"] = new Vector2(-24, 50),
        };
        var edges = new (string, string)[]
        {
            ("spawn","center"),("spawn","cabin"),("spawn","tent"),("spawn","NW"),("spawn","SW"),
            ("center","nHub"),("center","sHub"),("center","eHub"),("nHub","LN"),("sHub","LS"),("tent","nHub"),
            ("eHub","LE1"),("eHub","LE2"),("eHub","farE"),("farE","LE1"),("farE","LE2"),
            ("NW","nHub"),("NW","roadN"),("SW","sHub"),("SW","roadS"),("NE","nHub"),("NE","eHub"),("SE","sHub"),("SE","eHub"),
            ("cSE","LE2"),("cSE","SE"),("cSE","farE"),("cNE","LE1"),("cNE","NE"),("cNE","farE"),
            ("cSW","SW"),("cSW","roadS"),("cNW","NW"),("cNW","roadN"),
        };
        pathCells = new HashSet<Vector2Int>();
        foreach (var (a, b) in edges)
        {
            Vector2 pa = wp[a], pb = wp[b];
            Vector2 mid = (pa + pb) * 0.5f; Vector2 dir = (pb - pa); Vector2 perp = new Vector2(-dir.y, dir.x).normalized;
            Vector2 ctrl = mid + perp * ((float)rng.NextDouble() - 0.5f) * dir.magnitude * 0.35f;
            int steps = Mathf.CeilToInt(dir.magnitude) + 1;
            for (int i = 0; i <= steps; i++)
            { float t = i / (float)steps; Vector2 pt = (1 - t) * (1 - t) * pa + 2 * (1 - t) * t * ctrl + t * t * pb; pathCells.Add(new Vector2Int(Mathf.RoundToInt(pt.x), Mathf.RoundToInt(pt.y))); }
        }
    }

    // protected zones
    private static readonly (Vector2 c, Vector2 half)[] Rects =
    {
        (new Vector2(-3.5f,-7f), new Vector2(11f, 11f)),   // cabin + entrance
        (new Vector2(6,52), new Vector2(15,14)),            // L_N + buffer
        (new Vector2(8,-49), new Vector2(15,14)),           // L_S
        (new Vector2(68,20), new Vector2(14,14)),           // L_E1
        (new Vector2(68,-14), new Vector2(14,14)),          // L_E2
    };
    private static readonly (Vector2 c, float r)[] Circles =
    {
        (new Vector2(0,0), 4.5f),        // spawn
        (new Vector2(4.5f,20.5f), 5.5f), // tent
    };
    private static bool Protected(Vector2 p)
    {
        if (p.x >= -27 && p.x <= -19) return true; // road corridor
        foreach (var r in Rects) if (Mathf.Abs(p.x - r.c.x) <= r.half.x && Mathf.Abs(p.y - r.c.y) <= r.half.y) return true;
        foreach (var c in Circles) if ((p - c.c).sqrMagnitude <= c.r * c.r) return true;
        return false;
    }

    [MenuItem("Tools/Forest Editor/Interior Generation/Generate")]
    public static void GenerateInterior()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.EndsWith("MainForest.unity"))
            scene = EditorSceneManager.OpenScene("Assets/Scenes/MainForest.unity", OpenSceneMode.Single);

        var rng = new System.Random(Seed);
        grid = new Dictionary<Vector2Int, List<P>>();
        clearingGrid = new Dictionary<Vector2Int, List<P>>();

        // ---- prototypes by type ----
        var holder = new GameObject("__InteriorProtos_TEMP"); holder.SetActive(false);
        List<GameObject> Protos(string container, int max)
        {
            var list = new List<GameObject>(); var t = ForestEditorHierarchy.GroupGO(ForestEditorHierarchy.HandPlaced).transform.Find(container);
            if (t == null) return list; var kids = new List<Transform>(); foreach (Transform c in t) kids.Add(c);
            int stride = Mathf.Max(1, kids.Count / max);
            for (int i = 0; i < kids.Count && list.Count < max; i += stride) { var pr = Object.Instantiate(kids[i].gameObject, holder.transform); list.Add(pr); }
            return list;
        }
        var protoByType = new Dictionary<string, List<GameObject>>
        {
            ["tree"] = Protos("treenormal", 24), ["dead"] = Protos("darktrees", 12),
            ["bush"] = Protos("bushes", 12), ["log"] = Protos("Logs", 6), ["stump"] = Protos("stumps", 6),
        };
        foreach (var kv in protoByType) if (kv.Value.Count == 0) Debug.LogWarning($"[Gen] no prototypes for {kv.Key}");

        // ---- remove old generated interior (MapExpansion_* + prior GeneratedInterior) ----
        int removed = 0;
        foreach (var nm in new[] { "MapExpansion_North", "MapExpansion_East", "MapExpansion_South", "GeneratedInterior" })
        {
            var g = ForestEditorHierarchy.GroupGO(nm); if (g == null) continue;
            removed += g.GetComponentsInChildren<Transform>(true).Length - 1;
            Object.DestroyImmediate(g);
        }
        Debug.Log($"[Gen] Removed old generated interior objects: {removed}");

        var parent = ForestEditorHierarchy.EnsureGroup(ForestEditorHierarchy.GeneratedInterior);

        // ---- register EXISTING objects. overlap grid = everything (preserved + outer border);
        //      clearing density grid = preserved ForestStuff hand-placed ONLY (so density-skip
        //      protects the hand-placed clearing but still lets us ramp toward the outer border). ----
        var forestT = ForestEditorHierarchy.GroupGO(ForestEditorHierarchy.HandPlaced).transform;
        foreach (var col in Object.FindObjectsByType<CircleCollider2D>(FindObjectsSortMode.None))
        {
            var p = (Vector2)col.transform.position;
            if (p.x < OXMin - 4 || p.x > OXMax + 4 || p.y < OYMin - 4 || p.y > OYMax + 4) continue;
            float r = col.radius * Mathf.Max(Mathf.Abs(col.transform.lossyScale.x), Mathf.Abs(col.transform.lossyScale.y));
            Add(p, r);
            if (col.transform.IsChildOf(forestT)) AddTo(clearingGrid, p, r);
        }

        // ---- build path network ----
        var wp = new Dictionary<string, Vector2>
        {
            ["spawn"] = new Vector2(0, 0), ["cabin"] = new Vector2(0, 2), ["tent"] = new Vector2(4.5f, 15f),
            ["center"] = new Vector2(20, 0), ["nHub"] = new Vector2(12, 44), ["sHub"] = new Vector2(12, -42),
            ["eHub"] = new Vector2(58, 3), ["farE"] = new Vector2(78, 0),
            ["LN"] = new Vector2(6, 44), ["LS"] = new Vector2(8, -41), ["LE1"] = new Vector2(60, 20), ["LE2"] = new Vector2(60, -14),
            ["roadN"] = new Vector2(-20, 56), ["roadS"] = new Vector2(-20, -54),
            ["NE"] = new Vector2(50, 42), ["SE"] = new Vector2(50, -42), ["NW"] = new Vector2(-16, 40), ["SW"] = new Vector2(-16, -40),
            ["cSE"] = new Vector2(72, -48), ["cNE"] = new Vector2(72, 48), ["cSW"] = new Vector2(-24, -50), ["cNW"] = new Vector2(-24, 50),
        };
        var edges = new (string, string)[]
        {
            ("spawn","center"),("spawn","cabin"),("spawn","tent"),("spawn","NW"),("spawn","SW"),
            ("center","nHub"),("center","sHub"),("center","eHub"),
            ("nHub","LN"),("sHub","LS"),("tent","nHub"),
            ("eHub","LE1"),("eHub","LE2"),("eHub","farE"),("farE","LE1"),("farE","LE2"),
            ("NW","nHub"),("NW","roadN"),("SW","sHub"),("SW","roadS"),
            ("NE","nHub"),("NE","eHub"),("SE","sHub"),("SE","eHub"),
            ("cSE","LE2"),("cSE","SE"),("cSE","farE"),("cNE","LE1"),("cNE","NE"),("cNE","farE"),
            ("cSW","SW"),("cSW","roadS"),("cNW","NW"),("cNW","roadN"),
        };
        pathCells = new HashSet<Vector2Int>();
        foreach (var (a, b) in edges)
        {
            Vector2 pa = wp[a], pb = wp[b];
            // curved: perpendicular mid offset
            Vector2 mid = (pa + pb) * 0.5f; Vector2 dir = (pb - pa); Vector2 perp = new Vector2(-dir.y, dir.x).normalized;
            Vector2 ctrl = mid + perp * ((float)rng.NextDouble() - 0.5f) * dir.magnitude * 0.35f;
            int steps = Mathf.CeilToInt(dir.magnitude) + 1;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps; // quadratic bezier
                Vector2 pt = (1 - t) * (1 - t) * pa + 2 * (1 - t) * t * ctrl + t * t * pb;
                pathCells.Add(new Vector2Int(Mathf.RoundToInt(pt.x), Mathf.RoundToInt(pt.y)));
            }
        }

        // ---- navigable loose distribution ----
        // Fine jittered candidate grid; a low-frequency noise field creates irregular clumps and
        // clearings, density ramps toward the exterior, and a HARD ComfortGap between every collider
        // pair guarantees the player can always pass (no accidental walls / tiny squeezes).
        int placed = 0, attempts = 0;
        var typeCount = new Dictionary<string, int>();
        // graduated landmark surroundings: sparser just outside each protected zone (no rings)
        float LandmarkFalloff(Vector2 p)
        {
            float m = 1f;
            foreach (var r in Rects)
            {
                float dx = Mathf.Max(0, Mathf.Abs(p.x - r.c.x) - r.half.x);
                float dy = Mathf.Max(0, Mathf.Abs(p.y - r.c.y) - r.half.y);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < 10f) m = Mathf.Min(m, Mathf.Lerp(0.25f, 1f, d / 10f));
            }
            return m;
        }
        const float step = 2.0f;
        for (float gx = OXMin + 1; gx < OXMax - 1; gx += step)
            for (float gy = OYMin + 1; gy < OYMax - 1; gy += step)
            {
                attempts++;
                Vector2 c = new Vector2(gx + (float)(rng.NextDouble() - 0.5) * step * 1.1f, gy + (float)(rng.NextDouble() - 0.5) * step * 1.1f);
                if (c.x < OXMin || c.x > OXMax || c.y < OYMin || c.y > OYMax) continue;
                if (Protected(c) || NearPath(c)) continue;
                // don't over-fill the preserved hand-placed clearing
                if (CountWithin(c, 9f) / (Mathf.PI * 81f) >= TargetDensity * 0.9f) continue;

                // irregular clumps + clearings
                float clump = Mathf.PerlinNoise(c.x * 0.06f + 12.3f, c.y * 0.06f + 5.7f);
                float clearing = Mathf.PerlinNoise(c.x * 0.028f + 40f, c.y * 0.028f + 9f);
                float dEdge = Mathf.Min(Mathf.Min(c.x - OXMin, OXMax - c.x), Mathf.Min(c.y - OYMin, OYMax - c.y));
                float edgeT = 1f - Mathf.Clamp01(dEdge / 24f);
                float prob = Mathf.Pow(clump, 0.9f) * 2.6f;            // clumps: dense clumps fill to the ComfortGap limit, thin areas stay sparse
                prob *= Mathf.Lerp(1f, EdgeDensityMult, edgeT);        // denser toward exterior
                if (clearing < 0.24f) prob *= 0.22f;                   // occasional irregular clearings
                prob *= LandmarkFalloff(c);                            // sparser around landmarks
                if (rng.NextDouble() > prob) continue;

                // pick type (proportions), authored scale + collider radius
                double rv = rng.NextDouble(); string type = "tree";
                foreach (var e in TypeTable) if (rv <= e.cum) { type = e.type; break; }
                var pool = protoByType[type]; if (pool.Count == 0) continue;
                var (scale, baseR) = TypeScaleRadius(type, rng);
                float effR = baseR * scale;
                if (TooCloseGap(c, effR, ComfortGap)) continue;        // HARD navigability guarantee

                var proto = pool[rng.Next(pool.Count)];
                var clone = Object.Instantiate(proto, parent); clone.SetActive(true);
                clone.name = type + "_int";
                clone.transform.position = new Vector3(c.x, c.y, proto.transform.position.z);
                clone.transform.rotation = Quaternion.Euler(0, 0, (float)(rng.NextDouble() * 360));
                clone.transform.localScale = new Vector3(scale, scale, 1f);
                Add(c, effR); typeCount[type] = typeCount.GetValueOrDefault(type) + 1; placed++;
            }

        Object.DestroyImmediate(holder);
        Debug.Log($"[Gen] Seed={Seed}. Placed {placed} objects (ComfortGap={ComfortGap}u between all colliders). By type: " +
            string.Join(", ", typeCount.OrderByDescending(k => k.Value).Select(k => $"{k.Key}={k.Value} ({100f * k.Value / Mathf.Max(1, placed):F0}%)")));
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[Gen] Saved: {EditorSceneManager.SaveScene(scene)}. DONE.");
    }

    // ===================== SECOND-PASS ADDITIVE INFILL =====================
    // Adds foliage into empty gaps WITHOUT moving existing objects. Respects ComfortGap against
    // every existing collider (previous pass + originals + border), so no new walls. Favours
    // smaller/lighter details (bush/log/stump) so the floor reads richer without more big blockers.
    public const int InfillSeed = 20260722;
    public const float InfillComfortGap = 1.7f;   // player Ø1.0 + 0.7 margin (keeps comfortable navigation)
    public const float InfillPathClear = 2.2f;    // keep primary routes a touch wider; ComfortGap covers the rest

    [MenuItem("Tools/Forest Editor/Interior Generation/Infill Empty Gaps (Second Pass)")]
    public static void InfillInterior()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.EndsWith("MainForest.unity"))
            scene = EditorSceneManager.OpenScene("Assets/Scenes/MainForest.unity", OpenSceneMode.Single);

        var rng = new System.Random(InfillSeed);
        grid = new Dictionary<Vector2Int, List<P>>();
        clearingGrid = new Dictionary<Vector2Int, List<P>>();

        // prototypes
        var holder = new GameObject("__InfillProtos_TEMP"); holder.SetActive(false);
        List<GameObject> Protos(string container, int max)
        {
            var list = new List<GameObject>(); var t = ForestEditorHierarchy.GroupGO(ForestEditorHierarchy.HandPlaced).transform.Find(container);
            if (t == null) return list; var kids = new List<Transform>(); foreach (Transform c in t) kids.Add(c);
            int stride = Mathf.Max(1, kids.Count / max);
            for (int i = 0; i < kids.Count && list.Count < max; i += stride) { var pr = Object.Instantiate(kids[i].gameObject, holder.transform); list.Add(pr); }
            return list;
        }
        var protoByType = new Dictionary<string, List<GameObject>>
        {
            ["tree"] = Protos("treenormal", 24), ["dead"] = Protos("darktrees", 12),
            ["bush"] = Protos("bushes", 14), ["log"] = Protos("Logs", 6), ["stump"] = Protos("stumps", 6),
        };

        // remove ONLY our own prior infill
        int removed = 0; var prev = ForestEditorHierarchy.GroupGO(ForestEditorHierarchy.InteriorInfill);
        if (prev) { removed = prev.GetComponentsInChildren<Transform>(true).Length - 1; Object.DestroyImmediate(prev); }
        var parent = ForestEditorHierarchy.EnsureGroup(ForestEditorHierarchy.InteriorInfill);
        Debug.Log($"[Infill] Removed prior infill objects: {removed}");

        // register ALL existing obstacle colliders (previous pass + originals + outer border) so
        // infill keeps ComfortGap from them; ForestStuff also feeds the clearing density-skip.
        var forestT = ForestEditorHierarchy.GroupGO(ForestEditorHierarchy.HandPlaced).transform;
        var ob = ForestEditorHierarchy.GroupGO(ForestEditorHierarchy.OuterBorder); var cab = GameObject.Find("Cabin");
        var pl = GameObject.Find("Player"); var mo = GameObject.Find("Monster");
        int existing = 0;
        foreach (var col in Object.FindObjectsByType<CircleCollider2D>(FindObjectsSortMode.None))
        {
            if (col.isTrigger) continue; var tr = col.transform;
            if (cab && tr.IsChildOf(cab.transform)) continue;
            if (pl && tr.IsChildOf(pl.transform)) continue;
            if (mo && tr.IsChildOf(mo.transform)) continue;
            Vector2 p = tr.position;
            if (p.x < OXMin - 4 || p.x > OXMax + 4 || p.y < OYMin - 4 || p.y > OYMax + 4) continue;
            float r = col.radius * Mathf.Max(Mathf.Abs(tr.lossyScale.x), Mathf.Abs(tr.lossyScale.y));
            Add(p, r); existing++;
            if (tr.IsChildOf(forestT)) AddTo(clearingGrid, p, r);
        }
        Debug.Log($"[Infill] Registered existing colliders (kept as-is): {existing}");

        BuildPaths(rng);

        // graduated landmark surroundings
        float LandmarkFalloff(Vector2 p)
        {
            float m = 1f;
            foreach (var r in Rects)
            { float dx = Mathf.Max(0, Mathf.Abs(p.x - r.c.x) - r.half.x), dy = Mathf.Max(0, Mathf.Abs(p.y - r.c.y) - r.half.y); float d = Mathf.Sqrt(dx * dx + dy * dy); if (d < 10f) m = Mathf.Min(m, Mathf.Lerp(0.25f, 1f, d / 10f)); }
            return m;
        }

        int placed = 0; var typeCount = new Dictionary<string, int>();
        // Two ordered passes so big canopy trees claim the open regions first (their SPRITE fills
        // the view while the collider stays small = visual density without movement density), then
        // small floor details (bush/log/stump) enrich the remaining gaps.
        void Pass((string type, float cum)[] mix, float probBase, float clearThresh)
        {
            const float step = 1.8f;
            for (float gx = OXMin + 1; gx < OXMax - 1; gx += step)
                for (float gy = OYMin + 1; gy < OYMax - 1; gy += step)
                {
                    Vector2 c = new Vector2(gx + (float)(rng.NextDouble() - 0.5) * step * 1.2f, gy + (float)(rng.NextDouble() - 0.5) * step * 1.2f);
                    if (c.x < OXMin || c.x > OXMax || c.y < OYMin || c.y > OYMax) continue;
                    if (Protected(c) || NearPathR(c, InfillPathClear)) continue;
                    // leave the Ranger-Station original clearing as the reference (already rich)
                    if (CountWithin(c, 8f) / (Mathf.PI * 64f) >= TargetDensity * clearThresh) continue;

                    float clump = Mathf.PerlinNoise(c.x * 0.05f + 61.7f, c.y * 0.05f + 22.3f);
                    float clearing = Mathf.PerlinNoise(c.x * 0.026f + 4f, c.y * 0.026f + 88f);
                    float dEdge = Mathf.Min(Mathf.Min(c.x - OXMin, OXMax - c.x), Mathf.Min(c.y - OYMin, OYMax - c.y));
                    float edgeT = 1f - Mathf.Clamp01(dEdge / 24f);
                    float prob = Mathf.Pow(clump, 0.8f) * probBase;
                    prob *= Mathf.Lerp(1f, EdgeDensityMult, edgeT);
                    if (clearing < 0.20f) prob *= 0.18f;               // keep some real openings
                    prob *= LandmarkFalloff(c);
                    if (rng.NextDouble() > prob) continue;

                    double rv = rng.NextDouble(); string type = mix[0].type;
                    foreach (var e in mix) if (rv <= e.cum) { type = e.type; break; }
                    var pool = protoByType[type]; if (pool.Count == 0) continue;
                    var (scale, baseR) = TypeScaleRadius(type, rng);
                    float effR = baseR * scale;
                    if (TooCloseGap(c, effR, InfillComfortGap)) continue;

                    var proto = pool[rng.Next(pool.Count)];
                    var clone = Object.Instantiate(proto, parent); clone.SetActive(true);
                    clone.name = type + "_fill";
                    clone.transform.position = new Vector3(c.x, c.y, proto.transform.position.z);
                    clone.transform.rotation = Quaternion.Euler(0, 0, (float)(rng.NextDouble() * 360));
                    clone.transform.localScale = new Vector3(scale, scale, 1f);
                    Add(c, effR); typeCount[type] = typeCount.GetValueOrDefault(type) + 1; placed++;
                }
        }
        // Pass A: canopy trees + dead trees (structure/richness), fill the big empty regions.
        Pass(new[] { ("tree", 0.68f), ("dead", 1.0f) }, 2.1f, 1.0f);
        // Pass B: smaller floor details into remaining gaps (bush-led, some log, few stumps).
        Pass(new[] { ("bush", 0.60f), ("log", 0.82f), ("stump", 1.0f) }, 2.6f, 1.2f);

        Object.DestroyImmediate(holder);
        Debug.Log($"[Infill] Seed={InfillSeed}. Added {placed} objects (InfillComfortGap={InfillComfortGap}u). By type: " +
            string.Join(", ", typeCount.OrderByDescending(k => k.Value).Select(k => $"{k.Key}={k.Value} ({100f * k.Value / Mathf.Max(1, placed):F0}%)")));
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[Infill] Saved: {EditorSceneManager.SaveScene(scene)}. DONE.");
    }

    // ===================== VALIDATION =====================
    [MenuItem("Tools/Forest Editor/Validation/Interior Validate Navigation (Read Only)")]
    public static void ValidateInterior()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.EndsWith("MainForest.unity"))
            scene = EditorSceneManager.OpenScene("Assets/Scenes/MainForest.unity", OpenSceneMode.Single);

        // occupancy grid over interior, 1u cells; blocked if any collider within (r + inflate).
        float inflate = PlayerRadius + 0.35f;
        int gx0 = OXMin - 2, gy0 = OYMin - 2, W = (OXMax + 2) - gx0, H = (OYMax + 2) - gy0;
        var blocked = new bool[W, H];
        int overlapPairs = 0;
        var newCols = new List<(Vector2 p, float r)>();
        var genParent = ForestEditorHierarchy.GroupGO(ForestEditorHierarchy.GeneratedInterior);
        var playerT = GameObject.Find("Player")?.transform; var monsterT = GameObject.Find("Monster")?.transform;
        foreach (var c in Object.FindObjectsByType<CircleCollider2D>(FindObjectsSortMode.None))
        {
            if (c.isTrigger) continue;
            if (playerT && c.transform.IsChildOf(playerT)) continue;   // don't treat the player as an obstacle
            if (monsterT && c.transform.IsChildOf(monsterT)) continue; // nor the monster
            Vector2 p = c.transform.position;
            float r = c.radius * Mathf.Max(Mathf.Abs(c.transform.lossyScale.x), Mathf.Abs(c.transform.lossyScale.y));
            for (int x = Mathf.FloorToInt(p.x - r - inflate); x <= Mathf.CeilToInt(p.x + r + inflate); x++)
                for (int y = Mathf.FloorToInt(p.y - r - inflate); y <= Mathf.CeilToInt(p.y + r + inflate); y++)
                { int ix = x - gx0, iy = y - gy0; if (ix < 0 || iy < 0 || ix >= W || iy >= H) continue; if ((new Vector2(x, y) - p).magnitude <= r + inflate) blocked[ix, iy] = true; }
            if (genParent && c.transform.IsChildOf(genParent.transform)) newCols.Add((p, r));
        }
        // overlap among new colliders (severe)
        for (int i = 0; i < newCols.Count; i++) for (int j = i + 1; j < newCols.Count; j++)
            { var a = newCols[i]; var b = newCols[j]; if ((a.p - b.p).sqrMagnitude < ((a.r + b.r) * 0.5f) * ((a.r + b.r) * 0.5f)) overlapPairs++; }

        // flood fill from spawn
        bool Free(int x, int y) { int ix = x - gx0, iy = y - gy0; return ix >= 0 && iy >= 0 && ix < W && iy < H && !blocked[ix, iy]; }
        var visited = new bool[W, H];
        var q = new Queue<Vector2Int>();
        // start from the nearest free cell to spawn (0,0)
        Vector2Int start = new Vector2Int(int.MinValue, 0);
        for (int rad = 0; rad <= 8 && start.x == int.MinValue; rad++)
            for (int dx = -rad; dx <= rad && start.x == int.MinValue; dx++)
                for (int dy = -rad; dy <= rad && start.x == int.MinValue; dy++)
                    if (Free(dx, dy)) start = new Vector2Int(dx, dy);
        if (start.x != int.MinValue) { q.Enqueue(start); visited[start.x - gx0, start.y - gy0] = true; }
        Debug.Log($"[Val] flood-fill start cell: ({start.x},{start.y})");
        int reachable = 0;
        while (q.Count > 0)
        {
            var cur = q.Dequeue(); reachable++;
            foreach (var d in new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) })
            {
                int nx = cur.x + d.x, ny = cur.y + d.y, ix = nx - gx0, iy = ny - gy0;
                if (ix < 0 || iy < 0 || ix >= W || iy >= H || visited[ix, iy] || blocked[ix, iy]) continue;
                visited[ix, iy] = true; q.Enqueue(new Vector2Int(nx, ny));
            }
        }
        bool Reach(float x, float y)
        {
            for (int rad = 0; rad <= 4; rad++)
                for (int dx = -rad; dx <= rad; dx++) for (int dy = -rad; dy <= rad; dy++)
                    { int ix = Mathf.RoundToInt(x) + dx - gx0, iy = Mathf.RoundToInt(y) + dy - gy0; if (ix >= 0 && iy >= 0 && ix < W && iy < H && visited[ix, iy]) return true; }
            return false;
        }
        var targets = new (string n, float x, float y)[]
        {
            ("cabin",0,2),("tent",4.5f,18),("road-mid",-23,0),("road-N-open",-23,60),("road-S-open",-23,-58),
            ("LandmarkZone_North",6,52),("LandmarkZone_South",8,-49),("LandmarkZone_East_01",68,20),("LandmarkZone_East_02",68,-14),
            ("north-region",10,55),("south-region",10,-55),("east-region",76,0),("far-NE",70,50),("far-SE",70,-50),
        };
        Debug.Log($"[Val] Reachable open cells from spawn: {reachable}. Inflate={inflate:F2} (playerR {PlayerRadius}+margin).");
        var unreachable = new List<string>();
        foreach (var t in targets) { bool ok = Reach(t.x, t.y); Debug.Log($"[Val]   {t.n}: {(ok ? "REACHABLE" : "UNREACHABLE")}"); if (!ok) unreachable.Add(t.n); }

        // total open cells vs reachable -> isolated pockets
        int openTotal = 0; for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) if (!blocked[x, y]) openTotal++;
        Debug.Log($"[Val] Open cells total {openTotal}, reachable {reachable} => isolated/unreachable open cells: {openTotal - reachable} ({100f * (openTotal - reachable) / openTotal:F1}%)");

        // narrow corridor detection along reachable cells: reachable cell with <2 free orthogonal+diagonal neighbours in a 1-cell band = pinch
        int narrow = 0;
        for (int x = 1; x < W - 1; x++) for (int y = 1; y < H - 1; y++)
            {
                if (!visited[x, y]) continue;
                int freeN = 0; for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) if (!(dx == 0 && dy == 0) && !blocked[x + dx, y + dy]) freeN++;
                if (freeN <= 2) narrow++;
            }
        Debug.Log($"[Val] Pinch cells (reachable but <=2 free neighbours, ~narrower than clearance): {narrow}");
        Debug.Log($"[Val] Overlapping NEW collider pairs (centers < half sum of radii): {overlapPairs}");
        Debug.Log($"[Val] RESULT: {(unreachable.Count == 0 ? "PASS - all key targets reachable" : "FAIL - unreachable: " + string.Join(", ", unreachable))}");

        int missing = 0;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            foreach (var comp in go.GetComponents<Component>()) if (comp == null) missing++;
        Debug.Log($"[Val] Missing/null components: {missing}");
        Debug.Log("[Val] DONE");
    }
}
