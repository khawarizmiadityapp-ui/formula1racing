using UnityEngine;
using UnityEditor;
using System.IO;

[InitializeOnLoad]
public static class ApexDiagnostics
{
    static ApexDiagnostics()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlaying)
            {
                Debug.Log("[APEX V12] Exiting Play Mode to apply modifications...");
                EditorApplication.isPlaying = false;
            }
            InspectSceneObjects();
        };
    }
    [MenuItem("APEX V12/Diagnostics/Inspect Red Geometry")]
    public static void InspectSceneObjects()
    {
        string logPath = "Logs/Apex_Diagnostics_RedGeometry.txt";
        using (var sw = new StreamWriter(logPath, false))
        {
            sw.WriteLine("=== APEX V12 RED GEOMETRY DIAGNOSTIC ===");
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            sw.WriteLine($"Total Renderers found: {renderers.Length}");

            foreach (var r in renderers)
            {
                bool hasRed = false;
                foreach (var m in r.sharedMaterials)
                {
                    if (m != null && (m.name.ToLower().Contains("red") || 
                        (m.HasProperty("_BaseColor") && m.GetColor("_BaseColor").r > 0.6f && m.GetColor("_BaseColor").g < 0.3f)))
                    {
                        hasRed = true;
                        break;
                    }
                }

                if (hasRed)
                {
                    sw.WriteLine($"Object: {GetHierarchyPath(r.transform)}");
                    sw.WriteLine($"   Active: {r.gameObject.activeInHierarchy}, Enabled: {r.enabled}");
                    sw.WriteLine($"   World Pos: {r.transform.position}, Scale: {r.transform.lossyScale}");
                    sw.WriteLine($"   Bounds Center: {r.bounds.center}, Extents: {r.bounds.extents}");
                    var mf = r.GetComponent<MeshFilter>();
                    sw.WriteLine($"   Mesh: {(mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "null")}");
                    sw.WriteLine($"   Materials: {string.Join(", ", System.Array.ConvertAll(r.sharedMaterials, m => m != null ? m.name : "null"))}");
                    sw.WriteLine("--------------------------------------------------");
                }
            }
        }
        Debug.Log("Diagnostics written to Logs/Apex_Diagnostics_RedGeometry.txt");
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null) return "";
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
