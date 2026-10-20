using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom Editor for BakuTrackSplineBuilder.
/// Provides 1-click generation buttons and Scene GUI handles to interactively adjust Baku Castle Corner.
/// </summary>
[CustomEditor(typeof(BakuTrackSplineBuilder))]
public class BakuTrackBuilderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        BakuTrackSplineBuilder builder = (BakuTrackSplineBuilder)target;

        // Custom Header Banner
        EditorGUILayout.Space(8);
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleCenter
        };
        headerStyle.normal.textColor = new Color(0f, 0.85f, 0.72f);
        EditorGUILayout.LabelField("🏁 BAKU CITY CIRCUIT — FULL F1 STREET CIRCUIT", headerStyle);
        EditorGUILayout.HelpBox("Features a 690m Waterfront DRS Straight (Neftchilar Ave), Start/Finish Gantry, 10 Grid Boxes, Pit Lane & Garages, Flame Towers Skyline, Stadium Floodlights, and the iconic Turn 8 Castle Corner (7.6m bottleneck).\n\n🔒 SAFE MIGRATION: Old overlapping components are deactivated as [OLD_INACTIVE_BACKUP]_Baku_CastleCorner_Track without deleting any assets. New meshes use Mesh_BakuF1_*.", MessageType.Info);
        EditorGUILayout.Space(6);

        // Action Buttons
        GUI.backgroundColor = new Color(0.1f, 0.85f, 0.45f);
        if (GUILayout.Button("🔨 GENERATE FULL BAKU F1 CIRCUIT (690m Straight + Castle Corner)", GUILayout.Height(38)))
        {
            Undo.RegisterFullObjectHierarchyUndo(builder.gameObject, "Build Full Baku F1 Track");
            builder.BuildBakuTrack();
            EditorUtility.SetDirty(builder);
        }

        EditorGUILayout.Space(4);
        GUI.backgroundColor = new Color(0.95f, 0.45f, 0.2f);
        if (GUILayout.Button("⛔ Deactivate Old Overlapping Circuit Components", GUILayout.Height(28)))
        {
            Undo.RegisterFullObjectHierarchyUndo(builder.gameObject, "Deactivate Old Circuit");
            builder.DeactivateOldOverlappingTrackComponents();
            EditorUtility.SetDirty(builder);
        }

        EditorGUILayout.Space(2);
        GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🏁 Switch to New F1 Track", GUILayout.Height(26)))
        {
            builder.ToggleTrackVersion(true);
            EditorUtility.SetDirty(builder);
        }
        if (GUILayout.Button("🔙 Switch to Old Castle Backup", GUILayout.Height(26)))
        {
            builder.ToggleTrackVersion(false);
            EditorUtility.SetDirty(builder);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        GUI.backgroundColor = new Color(0.8f, 0.8f, 0.8f);
        if (GUILayout.Button("🔄 Reset Official Baku 34 Waypoints", GUILayout.Height(26)))
        {
            Undo.RecordObject(builder, "Reset Baku Waypoints");
            builder.InitializeBakuCastleWaypoints();
            EditorUtility.SetDirty(builder);
        }

        EditorGUILayout.Space(2);
        GUI.backgroundColor = new Color(0.9f, 0.75f, 0.1f);
        if (GUILayout.Button("🏎️ Place PlayerCar at Pole Position (Grid Box P1)", GUILayout.Height(28)))
        {
            Undo.RegisterFullObjectHierarchyUndo(builder.gameObject, "Place Car at Pole Position");
            builder.PositionPlayerCarAtPole();
            EditorUtility.SetDirty(builder);
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(10);

        // Default Inspector fields
        DrawDefaultInspector();
    }

    private void OnSceneGUI()
    {
        BakuTrackSplineBuilder builder = (BakuTrackSplineBuilder)target;
        var samples = builder.GenerateSplineSamples();

        if (samples != null && samples.Count > 1)
        {
            // Draw track ribbons in Scene view
            Handles.color = new Color(0f, 0.82f, 0.74f, 0.6f);
            for (int i = 0; i < samples.Count - 1; i++)
            {
                var s0 = samples[i];
                var s1 = samples[i + 1];

                Vector3 left0 = s0.position - s0.right * (s0.width * 0.5f);
                Vector3 right0 = s0.position + s0.right * (s0.width * 0.5f);
                Vector3 left1 = s1.position - s1.right * (s1.width * 0.5f);
                Vector3 right1 = s1.position + s1.right * (s1.width * 0.5f);

                Handles.DrawLine(left0, left1);
                Handles.DrawLine(right0, right1);

                // Bottleneck marker at Turn 8
                if (s0.width <= 7.8f && s0.innerKerb)
                {
                    Handles.color = Color.red;
                    Handles.Label(s0.position + Vector3.up * 2f, $"⚠️ CASTLE CORNER (7.6m Bottleneck)\nElevation: +{s0.position.y:F1}m");
                    Handles.color = new Color(0f, 0.82f, 0.74f, 0.6f);
                }
            }
        }
    }
}
