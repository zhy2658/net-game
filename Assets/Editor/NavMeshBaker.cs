using UnityEditor;
using UnityEngine;
using Unity.AI.Navigation;

public static class NavMeshBaker
{
    [MenuItem("Tools/Bake NavMesh")]
    public static void BakeAll()
    {
        var surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        foreach (var surface in surfaces)
        {
            surface.BuildNavMesh();
            Debug.Log($"NavMesh baked on '{surface.gameObject.name}'");
        }
        if (surfaces.Length == 0)
            Debug.LogWarning("No NavMeshSurface found in scene.");
    }
}
