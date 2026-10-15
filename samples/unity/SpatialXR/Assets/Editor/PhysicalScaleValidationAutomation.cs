using System;
using System.IO;
using SpatialAI.Editor;
using SpatialAI.Samples;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PhysicalScaleValidationAutomation
{
    public const string ScenePath = "Assets/Scenes/PhysicalScaleValidation.unity";

    [MenuItem("Spatial AI/Create Physical Scale Validation Scene")]
    public static void CreateScene()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = SpatialDisplayScene.PopulateXR();
        camera.backgroundColor = Color.black;
        var root = new GameObject("Scale target anchored at initial eye pose");
        var targetFrame = new GameObject("Ruler plane 15 degrees below initial gaze").transform;
        targetFrame.SetParent(root.transform, false);
        targetFrame.localRotation = Quaternion.Euler(PhysicalScaleValidation.TargetDepressionDegrees, 0, 0);
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "20 cm cube - front 50 cm - center 60 cm - below gaze";
        cube.transform.SetParent(targetFrame, false);
        cube.transform.localPosition = new Vector3(0, 0,
            PhysicalScaleValidation.FrontDistanceMetres + PhysicalScaleValidation.CubeSizeMetres * .5f);
        cube.transform.localScale = Vector3.one * PhysicalScaleValidation.CubeSizeMetres;
        UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
        var shader = Shader.Find("SpatialAI/CalibrationUnlit");
        if (shader == null) throw new Exception("Missing CalibrationUnlit shader.");
        Directory.CreateDirectory("Assets/PhysicalScaleMaterials");
        var green = SaveMaterial(shader, new Color(.2f, 1f, .35f), "Green");
        var white = SaveMaterial(shader, Color.white, "White");
        cube.GetComponent<MeshRenderer>().sharedMaterial = green;
        // Cross lies wholly inside the front face; its width does not extend the measured silhouette.
        AddCross(targetFrame, new Vector3(.03f, .001f, .0002f), white, "Horizontal center mark");
        AddCross(targetFrame, new Vector3(.001f, .03f, .0002f), white, "Vertical center mark");
        var text = new GameObject("Ruler instructions").AddComponent<TextMesh>();
        text.transform.SetParent(root.transform, false);
        text.transform.localPosition = new Vector3(0, .30f, 1f);
        text.anchor = TextAnchor.UpperCenter;
        text.alignment = TextAlignment.Center;
        text.fontSize = 64;
        text.characterSize = .008f;
        text.lineSpacing = 1.15f;
        text.color = Color.white;
        text.text = "20 cm CUBE\nRuler: 50 cm\nTest LEFT / RIGHT eye\nTap to reposition";
        var controller = new GameObject("Physical scale test").AddComponent<PhysicalScaleValidation>();
        controller.xrCamera = camera;
        controller.targetRoot = root.transform;
        // Check the rendered primitive, rather than only checking constants.
        var size = Vector3.Scale(cube.GetComponent<MeshFilter>().sharedMesh.bounds.size, cube.transform.localScale);
        var frontCenter = targetFrame.TransformPoint(cube.transform.localPosition - Vector3.forward * size.z * .5f);
        if (Mathf.Abs(size.x - .2f) > .00001f || Mathf.Abs(size.y - .2f) > .00001f ||
            Mathf.Abs(size.z - .2f) > .00001f || Mathf.Abs(Vector3.Distance(root.transform.position, frontCenter) - .5f) > .00001f ||
            Mathf.Abs(Vector3.Distance(root.transform.position, cube.transform.position) - .6f) > .00001f ||
            Mathf.Abs(Vector3.Angle(root.transform.forward, targetFrame.forward) - 15f) > .0001f || frontCenter.y >= 0 ||
            root.transform.parent != null) throw new Exception("Scale geometry or world anchor incorrect.");
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("SPATIALAI_SCALE_GEOMETRY_PASS width=0.2m front=0.5m center=0.6m depression=15deg worldRoot=true");
    }

    private static Material SaveMaterial(Shader shader, Color color, string name)
    {
        var path = "Assets/PhysicalScaleMaterials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader;
        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void AddCross(Transform parent, Vector3 size, Material material, string name)
    {
        var mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mark.name = name;
        mark.transform.SetParent(parent, false);
        mark.transform.localPosition = new Vector3(0, 0, PhysicalScaleValidation.FrontDistanceMetres - .0002f);
        mark.transform.localScale = size;
        mark.GetComponent<MeshRenderer>().sharedMaterial = material;
        UnityEngine.Object.DestroyImmediate(mark.GetComponent<Collider>());
    }

    [MenuItem("Spatial AI/Build Physical Scale Validation APK")]
    public static void Build()
    {
        SpatialAndroidSetup.Configure();
        CreateScene();
        PlayerSettings.companyName = "SpatialAI";
        PlayerSettings.productName = "Physical Scale Test";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.spatialai.validation.scale");
        PlayerSettings.Android.useCustomKeystore = false;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        var sdk = Environment.GetEnvironmentVariable("SPATIALAI_SCALE_SDK_ROOT");
        if (string.IsNullOrEmpty(sdk)) sdk = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.."));
        var apk = Path.Combine(sdk, "build/samples/android/spatialxr-physical-scale.apk");
        Directory.CreateDirectory(Path.GetDirectoryName(apk));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath },
            locationPathName = apk, target = BuildTarget.Android, options = BuildOptions.Development });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Physical scale APK build failed.");
        Debug.Log("SPATIALAI_SCALE_BUILD_PASS " + apk);
    }
}
