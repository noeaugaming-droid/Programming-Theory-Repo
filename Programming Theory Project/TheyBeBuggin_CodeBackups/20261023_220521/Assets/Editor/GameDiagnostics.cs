
#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Reflection;

public class GameDiagnostics : EditorWindow
{
    private Vector2 scrollPosition;
    private string report = "";

    [MenuItem("Tools/They Be Buggin/Game Diagnostics")]
    public static void OpenWindow()
    {
        GetWindow<GameDiagnostics>(
            "Game Diagnostics"
        );
    }

    private void OnGUI()
    {
        GUILayout.Label(
            "They Be Buggin - Game Diagnostics",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            "Scans the active Unity scene and rendering " +
            "configuration for camera, TAA, motion-vector, " +
            "and player physics issues. No project settings " +
            "will be modified.",
            MessageType.Info
        );

        if (GUILayout.Button(
            "Generate Full Diagnostic Report",
            GUILayout.Height(40)))
        {
            GenerateReport();
        }

        if (GUILayout.Button(
            "Save Report to Desktop",
            GUILayout.Height(30)))
        {
            if (string.IsNullOrEmpty(report))
                GenerateReport();

            SaveReport();
        }

        if (GUILayout.Button("Copy Report"))
        {
            if (string.IsNullOrEmpty(report))
                GenerateReport();

            EditorGUIUtility.systemCopyBuffer = report;
        }

        GUILayout.Space(10);

        scrollPosition = EditorGUILayout.BeginScrollView(
            scrollPosition
        );

        EditorGUILayout.TextArea(
            report,
            GUILayout.ExpandHeight(true)
        );

        EditorGUILayout.EndScrollView();
    }

    private void GenerateReport()
    {
        StringBuilder sb = new StringBuilder();

        AddHeader(sb, "THEY BE BUGGIN - DIAGNOSTICS");

        sb.AppendLine(
            "Generated: " + DateTime.Now
        );

        sb.AppendLine(
            "Play Mode: " + EditorApplication.isPlaying
        );

        ScanUnity(sb);
        ScanRenderPipeline(sb);
        ScanQuality(sb);
        ScanScene(sb);
        ScanCameras(sb);
        ScanPlayers(sb);
        ScanRenderers(sb);
        ScanVolumes(sb);
        ScanScripts(sb);
        ScanPotentialIssues(sb);

        report = sb.ToString();

        Debug.Log(
            "Game Diagnostics report generated."
        );
    }

    private void ScanUnity(StringBuilder sb)
    {
        AddHeader(sb, "UNITY INFORMATION");

        sb.AppendLine(
            "Unity Version: " + Application.unityVersion
        );

        sb.AppendLine(
            "Platform: " + Application.platform
        );

        sb.AppendLine(
            "Graphics Device: " +
            SystemInfo.graphicsDeviceName
        );

        sb.AppendLine(
            "Graphics API: " +
            SystemInfo.graphicsDeviceType
        );

        sb.AppendLine(
            "Graphics Memory: " +
            SystemInfo.graphicsMemorySize + " MB"
        );

        sb.AppendLine(
            "Color Space: " +
            PlayerSettings.colorSpace
        );

        sb.AppendLine(
            "Target FPS: " +
            Application.targetFrameRate
        );

        sb.AppendLine(
            "VSync Count: " +
            QualitySettings.vSyncCount
        );
    }

    private void ScanRenderPipeline(StringBuilder sb)
    {
        AddHeader(sb, "RENDER PIPELINE");

        RenderPipelineAsset pipeline =
            GraphicsSettings.currentRenderPipeline;

        if (pipeline == null)
        {
            sb.AppendLine(
                "Active Pipeline: Built-in Render Pipeline"
            );
            return;
        }

        sb.AppendLine(
            "Pipeline: " + pipeline.name
        );

        sb.AppendLine(
            "Pipeline Type: " +
            pipeline.GetType().FullName
        );

        sb.AppendLine(
            "Asset Path: " +
            AssetDatabase.GetAssetPath(pipeline)
        );

        DumpSerializedProperties(
            sb,
            pipeline,
            "Render Pipeline Asset",
            2,
            180
        );

        // Inspect nested renderer assets associated with URP.
        string assetPath =
            AssetDatabase.GetAssetPath(pipeline);

        if (!string.IsNullOrEmpty(assetPath))
        {
            UnityEngine.Object[] subAssets =
                AssetDatabase.LoadAllAssetsAtPath(
                    assetPath
                );

            foreach (UnityEngine.Object asset in subAssets)
            {
                if (asset == null || asset == pipeline)
                    continue;

                sb.AppendLine(
                    "Pipeline Sub Asset: " +
                    asset.name + " (" +
                    asset.GetType().Name + ")"
                );
            }
        }

        ScanPipelineReferences(sb, pipeline);
    }

    private void ScanPipelineReferences(
        StringBuilder sb,
        UnityEngine.Object pipeline)
    {
        AddHeader(sb, "PIPELINE ASSET REFERENCES");

        SerializedObject serialized =
            new SerializedObject(pipeline);

        SerializedProperty iterator =
            serialized.GetIterator();

        bool enterChildren = true;

        HashSet<int> visited = new HashSet<int>();

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.propertyType !=
                SerializedPropertyType.ObjectReference)
                continue;

            UnityEngine.Object reference =
                iterator.objectReferenceValue;

            if (reference == null)
                continue;

            if (!visited.Add(reference.GetInstanceID()))
                continue;

            sb.AppendLine(
                iterator.propertyPath + ": " +
                reference.name + " (" +
                reference.GetType().FullName + ")"
            );

            string typeName =
                reference.GetType().FullName;

            if (typeName.Contains("RendererData") ||
                typeName.Contains("RenderPipeline"))
            {
                DumpSerializedProperties(
                    sb,
                    reference,
                    "Referenced Rendering Asset",
                    1,
                    120
                );
            }
        }
    }

    private void ScanQuality(StringBuilder sb)
    {
        AddHeader(sb, "QUALITY SETTINGS");

        sb.AppendLine(
            "Active Quality Level: " +
            QualitySettings.names[
                QualitySettings.GetQualityLevel()
            ]
        );

        sb.AppendLine(
            "Anti Aliasing (MSAA): " +
            QualitySettings.antiAliasing
        );

        sb.AppendLine(
            "Shadow Distance: " +
            QualitySettings.shadowDistance
        );

        sb.AppendLine(
            "Shadow Cascades: " +
            QualitySettings.shadowCascades
        );

        sb.AppendLine(
            "LODBias: " +
            QualitySettings.lodBias
        );

        sb.AppendLine(
            "Maximum LOD Level: " +
            QualitySettings.maximumLODLevel
        );
    }

    private void ScanScene(StringBuilder sb)
    {
        AddHeader(sb, "SCENE INFORMATION");

        Scene activeScene =
            SceneManager.GetActiveScene();

        sb.AppendLine(
            "Active Scene: " + activeScene.name
        );

        sb.AppendLine(
            "Scene Path: " + activeScene.path
        );

        sb.AppendLine(
            "Loaded Scenes: " + SceneManager.sceneCount
        );

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene =
                SceneManager.GetSceneAt(i);

            sb.AppendLine(
                "Scene " + i + ": " +
                scene.name +
                " | Loaded: " + scene.isLoaded
            );
        }
    }

    private void ScanCameras(StringBuilder sb)
    {
        AddHeader(sb, "CAMERA INFORMATION");

        Camera[] cameras =
            Resources.FindObjectsOfTypeAll<Camera>();

        foreach (Camera cam in cameras)
        {
            if (cam == null)
                continue;

            if (!cam.gameObject.scene.IsValid())
                continue;

            sb.AppendLine(
                "\nCAMERA: " + GetPath(cam.transform)
            );

            sb.AppendLine(
                "Enabled: " + cam.enabled
            );

            sb.AppendLine(
                "Active: " + cam.gameObject.activeInHierarchy
            );

            sb.AppendLine(
                "Tag: " + cam.tag
            );

            sb.AppendLine(
                "Position: " + cam.transform.position
            );

            sb.AppendLine(
                "Rotation: " + cam.transform.eulerAngles
            );

            sb.AppendLine(
                "FOV: " + cam.fieldOfView
            );

            sb.AppendLine(
                "Near Clip: " + cam.nearClipPlane
            );

            sb.AppendLine(
                "Far Clip: " + cam.farClipPlane
            );

            sb.AppendLine(
                "Depth: " + cam.depth
            );

            sb.AppendLine(
                "Allow HDR: " + cam.allowHDR
            );

            sb.AppendLine(
                "Allow MSAA: " + cam.allowMSAA
            );

            sb.AppendLine(
                "Camera Type: " + cam.cameraType
            );

            sb.AppendLine(
                "Culling Mask: " + cam.cullingMask
            );

            sb.AppendLine(
                "Clear Flags: " + cam.clearFlags
            );

            ScanComponents(sb, cam.gameObject);

            foreach (Component component
                in cam.GetComponents<Component>())
            {
                if (component == null)
                    continue;

                string typeName =
                    component.GetType().FullName;

                if (typeName.Contains("UniversalAdditionalCameraData"))
                {
                    DumpSerializedProperties(
                        sb,
                        component,
                        "URP Camera Settings",
                        2,
                        200
                    );
                }
            }
        }
    }

    private void ScanPlayers(StringBuilder sb)
    {
        AddHeader(sb, "PLAYER AND PHYSICS");

        GameObject[] objects =
            Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject obj in objects)
        {
            if (obj == null || !obj.scene.IsValid())
                continue;

            bool isPlayer =
                obj.name.Equals(
                    "Player",
                    StringComparison.OrdinalIgnoreCase
                );

            if (!isPlayer)
                continue;

            sb.AppendLine(
                "\nPLAYER: " + GetPath(obj.transform)
            );

            ScanHierarchy(
                sb,
                obj.transform,
                0,
                6
            );

            Rigidbody rb = obj.GetComponent<Rigidbody>();

            if (rb != null)
            {
                sb.AppendLine("\nRIGIDBODY:");

                sb.AppendLine(
                    "Mass: " + rb.mass
                );

                sb.AppendLine(
                    "Use Gravity: " + rb.useGravity
                );

                sb.AppendLine(
                    "Is Kinematic: " + rb.isKinematic
                );

                sb.AppendLine(
                    "Interpolation: " + rb.interpolation
                );

                sb.AppendLine(
                    "Collision Detection: " +
                    rb.collisionDetectionMode
                );

                sb.AppendLine(
                    "Constraints: " + rb.constraints
                );

                sb.AppendLine(
                    "Velocity: " + rb.linearVelocity
                );

                sb.AppendLine(
                    "Angular Velocity: " + rb.angularVelocity
                );
            }

            ScanComponents(sb, obj);

            foreach (MonoBehaviour script
                in obj.GetComponents<MonoBehaviour>())
            {
                if (script == null)
                    continue;

                DumpSerializedProperties(
                    sb,
                    script,
                    "Player Script: " +
                    script.GetType().Name,
                    2,
                    100
                );
            }
        }
    }

    private void ScanHierarchy(
        StringBuilder sb,
        Transform root,
        int depth,
        int maxDepth)
    {
        if (depth > maxDepth)
            return;

        string indent = new string(' ', depth * 2);

        sb.AppendLine(
            indent +
            root.name +
            " | Active: " +
            root.gameObject.activeSelf +
            " | Layer: " +
            LayerMask.LayerToName(root.gameObject.layer)
        );

        foreach (Component component
            in root.GetComponents<Component>())
        {
            if (component == null)
                continue;

            sb.AppendLine(
                indent + "  - " +
                component.GetType().Name
            );
        }

        foreach (Transform child in root)
        {
            ScanHierarchy(
                sb,
                child,
                depth + 1,
                maxDepth
            );
        }
    }

    private void ScanRenderers(StringBuilder sb)
    {
        AddHeader(sb, "MESHES AND MOTION VECTORS");

        Renderer[] renderers =
            Resources.FindObjectsOfTypeAll<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null ||
                !renderer.gameObject.scene.IsValid())
                continue;

            // Focus on player and camera-related objects.
            string path = GetPath(renderer.transform);

            bool relevant =
                path.IndexOf(
                    "Player",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0 ||
                path.IndexOf(
                    "Enemy",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0;

            if (!relevant)
                continue;

            sb.AppendLine(
                "\nRENDERER: " + path
            );

            sb.AppendLine(
                "Type: " +
                renderer.GetType().Name
            );

            sb.AppendLine(
                "Enabled: " + renderer.enabled
            );

            sb.AppendLine(
                "Motion Vector Mode: " +
                renderer.motionVectorGenerationMode
            );

            sb.AppendLine(
                "Force Rendering Off: " +
                renderer.forceRenderingOff
            );

            sb.AppendLine(
                "Shadow Casting: " +
                renderer.shadowCastingMode
            );

            sb.AppendLine(
                "Receive Shadows: " +
                renderer.receiveShadows
            );

            if (renderer is SkinnedMeshRenderer skinned)
            {
                sb.AppendLine(
                    "Skinned Mesh: YES"
                );

                sb.AppendLine(
                    "Update When Offscreen: " +
                    skinned.updateWhenOffscreen
                );

                sb.AppendLine(
                    "Root Bone: " +
                    (skinned.rootBone != null
                        ? GetPath(skinned.rootBone)
                        : "None")
                );

                sb.AppendLine(
                    "Bones: " + skinned.bones.Length
                );
            }
            else if (renderer is MeshRenderer)
            {
                sb.AppendLine(
                    "Mesh Renderer: YES"
                );
            }

            Material[] materials =
                renderer.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];

                if (material == null)
                {
                    sb.AppendLine(
                        "Material " + i + ": NULL"
                    );
                    continue;
                }

                sb.AppendLine(
                    "Material " + i + ": " +
                    material.name
                );

                sb.AppendLine(
                    "Shader: " +
                    (material.shader != null
                        ? material.shader.name
                        : "NULL")
                );

                sb.AppendLine(
                    "Render Queue: " +
                    material.renderQueue
                );

                sb.AppendLine(
                    "MotionVectors Pass: " +
                    material.FindPass("MotionVectors")
                );

                sb.AppendLine(
                    "Material Path: " +
                    AssetDatabase.GetAssetPath(material)
                );
            }
        }
    }

    private void ScanVolumes(StringBuilder sb)
    {
        AddHeader(sb, "VOLUMES AND POST PROCESSING");

        Volume[] volumes =
            Resources.FindObjectsOfTypeAll<Volume>();

        foreach (Volume volume in volumes)
        {
            if (volume == null ||
                !volume.gameObject.scene.IsValid())
                continue;

            sb.AppendLine(
                "\nVOLUME: " + GetPath(volume.transform)
            );

            sb.AppendLine(
                "Enabled: " + volume.enabled
            );

            sb.AppendLine(
                "Global: " + volume.isGlobal
            );

            sb.AppendLine(
                "Priority: " + volume.priority
            );

            sb.AppendLine(
                "Weight: " + volume.weight
            );

            VolumeProfile profile =
                volume.sharedProfile;

            if (profile == null)
            {
                sb.AppendLine(
                    "Volume Profile: None"
                );
                continue;
            }

            sb.AppendLine(
                "Profile: " + profile.name
            );

            foreach (VolumeComponent component
                in profile.components)
            {
                if (component == null)
                    continue;

                sb.AppendLine(
                    "\nOverride: " +
                    component.GetType().Name
                );

                sb.AppendLine(
                    "Active: " + component.active
                );

                foreach (VolumeParameter parameter
                    in component.parameters)
                {
                    if (parameter == null)
                        continue;

                    sb.AppendLine(
                        "Parameter: " +
                        parameter.GetType().Name +
                        " | Override: " +
                        parameter.overrideState +
                        " | Value: " +
                        parameter.ToString()
                    );
                }

                DumpSerializedProperties(
                    sb,
                    component,
                    "Volume Override Settings",
                    2,
                    80
                );
            }
        }
    }

    private void ScanScripts(StringBuilder sb)
    {
        AddHeader(sb, "CAMERA AND PLAYER SCRIPTS");

        MonoBehaviour[] scripts =
            Resources.FindObjectsOfTypeAll<MonoBehaviour>();

        foreach (MonoBehaviour script in scripts)
        {
            if (script == null ||
                !script.gameObject.scene.IsValid())
                continue;

            string name = script.GetType().Name;

            bool relevant =
                name.Contains("Camera") ||
                name.Contains("Player") ||
                name.Contains("Look") ||
                name.Contains("Follow") ||
                name.Contains("Shooter");

            if (!relevant)
                continue;

            sb.AppendLine(
                "\nSCRIPT: " + name
            );

            sb.AppendLine(
                "GameObject: " +
                GetPath(script.transform)
            );

            sb.AppendLine(
                "Enabled: " + script.enabled
            );

            DumpSerializedProperties(
                sb,
                script,
                "Serialized Fields",
                2,
                100
            );
        }
    }

    private void ScanPotentialIssues(StringBuilder sb)
    {
        AddHeader(sb, "AUTOMATIC WARNINGS");

        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            sb.AppendLine(
                "WARNING: No MainCamera tagged camera found."
            );
        }
        else
        {
            MonoBehaviour[] scripts =
                mainCamera.GetComponents<MonoBehaviour>();

            int cameraScripts = 0;

            foreach (MonoBehaviour script in scripts)
            {
                if (script == null || !script.enabled)
                    continue;

                string name = script.GetType().Name;

                if (name.Contains("Follow") ||
                    name.Contains("Look"))
                {
                    cameraScripts++;
                }
            }

            if (cameraScripts > 1)
            {
                sb.AppendLine(
                    "CHECK: Multiple camera-related scripts " +
                    "are attached to Main Camera."
                );
            }
        }

        Renderer[] renderers =
            Resources.FindObjectsOfTypeAll<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null ||
                !renderer.gameObject.scene.IsValid())
                continue;

            string path = GetPath(renderer.transform);

            if (!path.Contains("Player"))
                continue;

            if (renderer.motionVectorGenerationMode ==
                MotionVectorGenerationMode.ForceNoMotion)
            {
                sb.AppendLine(
                    "WARNING: Player renderer has " +
                    "ForceNoMotion motion vectors: " +
                    path
                );
            }

            foreach (Material material
                in renderer.sharedMaterials)
            {
                if (material == null ||
                    material.shader == null)
                    continue;

                string shaderName =
                    material.shader.name;

                if (!shaderName.Contains("Universal") &&
                    !shaderName.Contains("Shader Graph"))
                {
                    sb.AppendLine(
                        "CHECK: Player material may use " +
                        "a non-URP shader: " +
                        shaderName
                    );
                }
            }
        }

        sb.AppendLine(
            "\nNOTE: Warnings are diagnostic hints, " +
            "not confirmed causes."
        );
    }

    private void DumpSerializedProperties(
        StringBuilder sb,
        UnityEngine.Object target,
        string title,
        int maxDepth,
        int maxProperties)
    {
        if (target == null)
            return;

        sb.AppendLine("\n" + title + ":");

        SerializedObject serialized;

        try
        {
            serialized = new SerializedObject(target);
        }
        catch (Exception ex)
        {
            sb.AppendLine(
                "Unable to inspect: " + ex.Message
            );
            return;
        }

        SerializedProperty property =
            serialized.GetIterator();

        bool enterChildren = true;
        int count = 0;

        while (property.NextVisible(enterChildren))
        {
            enterChildren = true;

            if (property.depth > maxDepth)
            {
                enterChildren = false;
                continue;
            }

            if (property.propertyPath == "m_Script")
                continue;

            string value = ReadPropertyValue(property);

            sb.AppendLine(
                property.propertyPath + " = " + value
            );

            count++;

            if (count >= maxProperties)
            {
                sb.AppendLine(
                    "... Property limit reached ..."
                );
                break;
            }
        }
    }

    private string ReadPropertyValue(
        SerializedProperty property)
    {
        try
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return property.longValue.ToString();

                case SerializedPropertyType.Boolean:
                    return property.boolValue.ToString();

                case SerializedPropertyType.Float:
                    return property.doubleValue.ToString();

                case SerializedPropertyType.String:
                    return property.stringValue;

                case SerializedPropertyType.Color:
                    return property.colorValue.ToString();

                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue != null
                        ? property.objectReferenceValue.name
                        : "None";

                case SerializedPropertyType.Enum:
                    return property.enumDisplayNames.Length >
                           property.enumValueIndex &&
                           property.enumValueIndex >= 0
                        ? property.enumDisplayNames[
                            property.enumValueIndex
                          ]
                        : property.enumValueIndex.ToString();

                case SerializedPropertyType.Vector2:
                    return property.vector2Value.ToString();

                case SerializedPropertyType.Vector3:
                    return property.vector3Value.ToString();

                case SerializedPropertyType.Vector4:
                    return property.vector4Value.ToString();

                case SerializedPropertyType.Quaternion:
                    return property.quaternionValue.ToString();

                case SerializedPropertyType.LayerMask:
                    return property.intValue.ToString();

                case SerializedPropertyType.ArraySize:
                    return property.intValue.ToString();

                case SerializedPropertyType.Generic:
                    return "(Complex Data)";

                default:
                    return property.propertyType.ToString();
            }
        }
        catch
        {
            return "(Unavailable)";
        }
    }

    private void ScanComponents(
        StringBuilder sb,
        GameObject obj)
    {
        sb.AppendLine(
            "\nCOMPONENTS: " + GetPath(obj.transform)
        );

        foreach (Component component
            in obj.GetComponents<Component>())
        {
            if (component == null)
            {
                sb.AppendLine(
                    "- Missing Script"
                );
                continue;
            }

            sb.AppendLine(
                "- " + component.GetType().FullName
            );
        }
    }

    private string GetPath(Transform transform)
    {
        if (transform == null)
            return "None";

        string path = transform.name;

        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }

        return path;
    }

    private void AddHeader(
        StringBuilder sb,
        string title)
    {
        sb.AppendLine();
        sb.AppendLine(
            "========================================"
        );
        sb.AppendLine(title);
        sb.AppendLine(
            "========================================"
        );
    }

    private void SaveReport()
    {
        string desktop = Environment.GetFolderPath(
            Environment.SpecialFolder.Desktop
        );

        string filename =
            "TheyBeBuggin_Diagnostics_" +
            DateTime.Now.ToString("yyyyMMdd_HHmmss") +
            ".txt";

        string path = Path.Combine(
            desktop,
            filename
        );

        try
        {
            File.WriteAllText(path, report);

            Debug.Log(
                "Diagnostics saved to: " + path
            );

            EditorUtility.DisplayDialog(
                "Report Saved",
                "Diagnostic report saved to:\n" + path,
                "OK"
            );
        }
        catch (Exception ex)
        {
            Debug.LogError(
                "Failed to save report: " + ex.Message
            );
        }
    }
}

#endif
