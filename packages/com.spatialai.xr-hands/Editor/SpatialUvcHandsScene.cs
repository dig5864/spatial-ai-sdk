using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace SpatialAI.XRHands.Editor
{
    public static class SpatialUvcHandsScene
    {
        public const string ScenePath="Assets/Scenes/SpatialUvcXRHands.unity";
        [MenuItem("Spatial AI/XR Hands/Create UVC Scene")]
        public static void Create()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            Ensure();EditorSceneManager.OpenScene(ScenePath);
        }
        public static void Ensure()
        {
            if(File.Exists(ScenePath))return;
            var previous=EditorSceneManager.GetSceneManagerSetup();
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            SpatialAI.Editor.SpatialDisplayScene.PopulateXR();
            new GameObject("Real UVC XR Hands").AddComponent<SpatialUvcHandsSample>().xrCamera=Camera.main;
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),ScenePath);
            if(previous.Length>0 && Array.TrueForAll(previous,s=>!string.IsNullOrEmpty(s.path)))EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
    }
}
