using System.IO;
using SpatialAI.XRHands;
using SpatialAI.XRHands.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;
using Type=System.Type;
using AppDomain=System.AppDomain;
using Array=System.Array;
using System.Collections.Generic;
using System.Reflection;

namespace SpatialAI.XRInteraction.Editor
{
    public static class SpatialHandInteractionScene
    {
        public const string ScenePath="Assets/Scenes/SpatialHandInteraction.unity";
        public const string PrefabPath="Assets/SpatialAI/Prefabs/SpatialHandInteractionRig.prefab";
        public const string ActionsPath="Assets/SpatialAI/Input/SpatialHandActions.inputactions";
        public const string GesturesPrefabPath="Assets/SpatialAI/Prefabs/XRHandsStandardGestures.prefab";
        private static Type GestureType()
        {foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()){var type=assembly.GetType("UnityEngine.XR.Hands.Samples.GestureSample.StaticHandGesture");if(type!=null)return type;}return null;}
        [MenuItem("Spatial AI/XR Interaction/Import Unity XR Hands Gestures")]
        public static void ImportUnityGestures()
        {
            var package=UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.xr.hands");
            // Recent XR Hands Gestures samples validate these dependencies, even
            // when a consumer uses only the standard gesture components.
            CopyUnityHandsSample(package,"HandVisualizer");
            if(!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
                foreach(var name in new[]{"com.unity.ugui","com.unity.textmeshpro"})
                {
                    var ui=UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/"+name);
                    if(ui==null)continue;
                    var resources=Path.Combine(ui.resolvedPath,"Package Resources/TMP Essential Resources.unitypackage");
                    if(!File.Exists(resources))continue;
                    AssetDatabase.ImportPackage(resources,false);break;
                }
            if(GestureType()!=null){AssetDatabase.Refresh();return;}
            CopyUnityHandsSample(package,"Gestures");
            AssetDatabase.Refresh();Debug.Log("Imported unchanged Unity XR Hands Gestures sample and its visualizer/TMP dependencies. Wait for script compilation, then create the interaction scene.");
        }
        private static void CopyUnityHandsSample(UnityEditor.PackageManager.PackageInfo package,string sample)
        {
            var source=Path.Combine(package.resolvedPath,"Samples~",sample);
            if(!Directory.Exists(source))return;
            var destination="Assets/Samples/XR Hands/"+package.version+"/"+sample;
            foreach(var file in Directory.GetFiles(source,"*",SearchOption.AllDirectories))
            {
                var target=Path.Combine(destination,file.Substring(source.Length+1));Directory.CreateDirectory(Path.GetDirectoryName(target));
                if(!File.Exists(target))File.Copy(file,target);
            }
        }
        public static void EnsureStandardGesturesPrefab()
        {
            if(File.Exists(GesturesPrefabPath))return;
            var type=GestureType();if(type==null){Debug.LogWarning("Import Unity XR Hands Gestures to enable the standard gesture example.");return;}
            Directory.CreateDirectory("Assets/SpatialAI/Gestures");Directory.CreateDirectory("Assets/SpatialAI/Prefabs");
            var assets=new XRHandShape[3];var titles=new[]{"OPEN","FIST","PINCH"};
            for(int i=0;i<3;i++)
            {
                string path="Assets/SpatialAI/Gestures/"+titles[i]+".asset";
                assets[i]=AssetDatabase.LoadAssetAtPath<XRHandShape>(path);if(assets[i]!=null)continue;
                var shape=ScriptableObject.CreateInstance<XRHandShape>();shape.name=titles[i];
                if(i==2)shape.fingerShapeConditions=new List<XRFingerShapeCondition>{new XRFingerShapeCondition{fingerID=XRHandFingerID.Index,
                    targets=new[]{new XRFingerShapeCondition.Target{shapeType=XRFingerShapeType.Pinch,desired=1,lowerTolerance=.25f,upperTolerance=0}}}};
                else
                {
                    shape.fingerShapeConditions=new List<XRFingerShapeCondition>();
                    foreach(var finger in new[]{XRHandFingerID.Index,XRHandFingerID.Middle,XRHandFingerID.Ring,XRHandFingerID.Little})
                        shape.fingerShapeConditions.Add(new XRFingerShapeCondition{fingerID=finger,targets=new[]{
                            new XRFingerShapeCondition.Target{shapeType=XRFingerShapeType.BaseCurl,desired=i==0?0:1,lowerTolerance=i==0?0:.4f,upperTolerance=i==0?.25f:0},
                            new XRFingerShapeCondition.Target{shapeType=XRFingerShapeType.FullCurl,desired=i==0?0:1,lowerTolerance=i==0?0:.75f,upperTolerance=i==0?.35f:0}}});
                }
                AssetDatabase.CreateAsset(shape,path);assets[i]=shape;
            }
            var root=new GameObject("Unity XR Hands Gesture Examples",typeof(RectTransform));root.SetActive(false);
            root.transform.localPosition=new Vector3(0,.75f,4.2f);root.transform.localScale=Vector3.one*.003f;
            root.AddComponent<Canvas>().renderMode=RenderMode.WorldSpace;root.GetComponent<RectTransform>().sizeDelta=new Vector2(800,240);
            for(int side=0;side<2;side++)
            {
                var eventsObject=new GameObject(side==0?"Left XR Hand Tracking Events":"Right XR Hand Tracking Events");eventsObject.transform.SetParent(root.transform,false);
                var events=eventsObject.AddComponent<XRHandTrackingEvents>();events.handedness=side==0?Handedness.Left:Handedness.Right;
                events.updateType=XRHandTrackingEvents.UpdateTypes.Dynamic;
                for(int i=0;i<3;i++)
                {
                    var detector=new GameObject((side==0?"Left ":"Right ")+titles[i],typeof(RectTransform));detector.transform.SetParent(root.transform,false);
                    var rect=detector.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(340,52);rect.anchoredPosition=new Vector2(side==0?-190:190,70-i*62);
                    var background=detector.AddComponent<Image>();background.color=new Color(.1f,.15f,.22f);background.raycastTarget=false;background.enabled=false;
                    var textObject=new GameObject("Label",typeof(RectTransform));textObject.transform.SetParent(detector.transform,false);
                    var text=textObject.AddComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=28;text.color=Color.white;
                    text.text=(side==0?"L ":"R ")+titles[i];text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;text.rectTransform.sizeDelta=rect.sizeDelta;text.enabled=false;
                    var indicator=new GameObject("Detected",typeof(RectTransform));indicator.transform.SetParent(detector.transform,false);
                    var indicatorRect=indicator.GetComponent<RectTransform>();indicatorRect.sizeDelta=new Vector2(20,20);indicatorRect.anchoredPosition=new Vector2(145,0);
                    var image=indicator.AddComponent<Image>();image.color=Color.green;image.raycastTarget=false;image.enabled=false;indicator.SetActive(false);
                    var gesture=detector.AddComponent(type);
                    type.GetProperty("handTrackingEvents").SetValue(gesture,events);type.GetProperty("handShapeOrPose").SetValue(gesture,assets[i]);
                    type.GetProperty("background").SetValue(gesture,background);type.GetProperty("minimumHoldTime").SetValue(gesture,.2f);
                    type.GetField("m_StaticGestures",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(gesture,Array.CreateInstance(type,0));
                    var performed=new UnityEvent();var ended=new UnityEvent();
                    UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(performed,indicator.SetActive,true);
                    UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(ended,indicator.SetActive,false);
                    UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(events.trackingLost,indicator.SetActive,false);
                    type.GetProperty("gesturePerformed").SetValue(gesture,performed);type.GetProperty("gestureEnded").SetValue(gesture,ended);
                }
            }
            root.SetActive(true);PrefabUtility.SaveAsPrefabAsset(root,GesturesPrefabPath);UnityEngine.Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
        }
        [MenuItem("Spatial AI/XR Interaction/Create UVC Interaction Scene")]
        public static void Create()
        {if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;Ensure();EditorSceneManager.OpenScene(ScenePath);}
        public static void EnsurePrefab()
        {
            if(!File.Exists(ActionsPath))
            {
                Directory.CreateDirectory("Assets/SpatialAI/Input");var asset=ScriptableObject.CreateInstance<InputActionAsset>();asset.name="Spatial Hand Actions";
                foreach(var side in new[]{"LeftHand","RightHand"})
                {
                    var map=new InputActionMap(side);asset.AddActionMap(map);
                    var controls=new[]{"devicePosition","deviceRotation","isTracked","trackingState","pinch","pinchStrength"};
                    var layouts=new[]{"Vector3","Quaternion","Button","Integer","Button","Axis"};
                    for(int i=0;i<controls.Length;i++)map.AddAction(controls[i],i==2||i==4?InputActionType.Button:InputActionType.Value,
                        "<SpatialHandInputDevice>{"+side+"}/"+controls[i],expectedControlLayout:layouts[i]);
                }
                File.WriteAllText(ActionsPath,asset.ToJson());Object.DestroyImmediate(asset);AssetDatabase.Refresh();
            }
            if(File.Exists(PrefabPath))return;
            Directory.CreateDirectory("Assets/SpatialAI/Prefabs");
            var root=new GameObject("Spatial Hand Interaction Rig");
            var bridge=root.AddComponent<SpatialHandInteraction>();
            var left=new GameObject("Left Hand Ray");left.transform.SetParent(root.transform,false);bridge.leftRay=left.AddComponent<SpatialHandRayBinding>();
            var right=new GameObject("Right Hand Ray");right.transform.SetParent(root.transform,false);bridge.rightRay=right.AddComponent<SpatialHandRayBinding>();bridge.rightRay.rightHand=true;
            bridge.leftRay.inputActions=bridge.rightRay.inputActions=AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);Object.DestroyImmediate(root);
        }
        public static void Ensure()
        {
            EnsurePrefab();EnsureStandardGesturesPrefab();
            if(File.Exists(ScenePath))
            {
                if(File.Exists(GesturesPrefabPath))
                {
                    var prior=EditorSceneManager.GetSceneManagerSetup();EditorSceneManager.OpenScene(ScenePath);
                    foreach(var sample in UnityEngine.Object.FindObjectsOfType<SpatialHandInteractionSample>())AssignStandardGestures(sample);
                    EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),ScenePath);
                    if(prior.Length>0&&Array.TrueForAll(prior,s=>!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(prior);
                }
                return;
            }
            var previous=EditorSceneManager.GetSceneManagerSetup();SpatialUvcHandsScene.Ensure();
            Directory.CreateDirectory("Assets/Scenes");File.Copy(SpatialUvcHandsScene.ScenePath,ScenePath);
            AssetDatabase.Refresh();EditorSceneManager.OpenScene(ScenePath);
            foreach(var sample in Object.FindObjectsOfType<SpatialUvcHandsSample>())Object.DestroyImmediate(sample.gameObject);
            var interaction=new GameObject("Spatial Hand Interaction").AddComponent<SpatialHandInteractionSample>();interaction.xrCamera=Camera.main;
            interaction.interactionRigPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            AssignStandardGestures(interaction);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),ScenePath);
            if(previous.Length>0 && System.Array.TrueForAll(previous,s=>!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
        [MenuItem("Spatial AI/XR Interaction/Create Simulation Interaction Scene")]
        public static void CreateSimulation()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;EnsurePrefab();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.backgroundColor=new Color(.015f,.02f,.04f);camera.clearFlags=CameraClearFlags.SolidColor;
            var sample=new GameObject("Simulated Hand Interaction").AddComponent<SpatialHandInteractionSample>();sample.xrCamera=camera;sample.useSimulation=true;
            sample.interactionRigPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);EditorSceneManager.SaveScene(scene,"Assets/Scenes/SpatialHandInteractionSimulation.unity");
            EnsureStandardGesturesPrefab();AssignStandardGestures(sample);EditorSceneManager.SaveScene(scene,"Assets/Scenes/SpatialHandInteractionSimulation.unity");
        }
        private static void AssignStandardGestures(SpatialHandInteractionSample sample)
        {var serialized=new SerializedObject(sample);var field=serialized.FindProperty("standardGesturesPrefab");if(field.objectReferenceValue==null)field.objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(GesturesPrefabPath);serialized.ApplyModifiedPropertiesWithoutUndo();}
    }
}
