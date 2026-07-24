using UnityEngine;

/// <summary>
/// Shared lookup helpers for the organized forest hierarchy:
///   ForestEnvironment / { HandPlacedForest, GeneratedInterior, InteriorInfill, NewOuterBorder, ReservedLandmarkZones }
/// Tools resolve their group as an EXACT child of ForestEnvironment rather than doing a broad,
/// scene-wide name search that could select an unrelated object. Generators create their own
/// group under ForestEnvironment so re-running keeps the clean hierarchy (and never touches
/// HandPlacedForest).
/// </summary>
public static class ForestEditorHierarchy
{
    public const string EnvironmentName = "ForestEnvironment";
    public const string HandPlaced = "HandPlacedForest";
    public const string GeneratedInterior = "GeneratedInterior";
    public const string InteriorInfill = "InteriorInfill";
    public const string OuterBorder = "NewOuterBorder";
    public const string LandmarkZones = "ReservedLandmarkZones";

    /// ForestEnvironment root transform (null if it does not exist).
    public static Transform Environment()
    {
        var go = GameObject.Find(EnvironmentName);
        return go ? go.transform : null;
    }

    /// ForestEnvironment root, created at the scene root with an identity transform if missing.
    public static Transform EnsureEnvironment()
    {
        var t = Environment();
        if (t == null)
        {
            var go = new GameObject(EnvironmentName);
            go.transform.SetParent(null, true);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            t = go.transform;
        }
        return t;
    }

    /// Exact child group under ForestEnvironment (null if absent). No broad scene search.
    public static Transform Group(string childName)
    {
        var env = Environment();
        return env ? env.Find(childName) : null;
    }

    public static GameObject GroupGO(string childName)
    {
        var t = Group(childName);
        return t ? t.gameObject : null;
    }

    /// Exact child group, created under ForestEnvironment if missing.
    public static Transform EnsureGroup(string childName)
    {
        var env = EnsureEnvironment();
        var t = env.Find(childName);
        if (t == null)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(env, false);
            t = go.transform;
        }
        return t;
    }
}
