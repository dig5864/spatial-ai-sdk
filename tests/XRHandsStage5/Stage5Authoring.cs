using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SpatialAI.Editor;
using SpatialAI.XRHands.Editor;
using SpatialAI.XRInteraction.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class Stage5Authoring
{
    public static void Export()
    {
        string root=Environment.GetEnvironmentVariable("SPATIALAI_SAMPLE_EXPORT_ROOT");
        if(string.IsNullOrEmpty(root))throw new Exception("Missing sample export root");
        SpatialDisplayScene.Create(); SpatialHandsSimulationScene.Create(); SpatialUvcHandsScene.Ensure(); SpatialHandInteractionScene.Ensure();
        var copies=new Dictionary<string,string>{
            {SpatialDisplayScene.ScenePath,"com.spatialai.sdk/Samples~/Display3DOF/SpatialDisplay.unity"},
            {"Assets/SpatialHandsSimulation.unity","com.spatialai.xr-hands/Samples~/HandsSimulation/SpatialHandsSimulation.unity"},
            {SpatialUvcHandsScene.ScenePath,"com.spatialai.xr-hands/Samples~/HandsUvc/SpatialUvcXRHands.unity"},
            {SpatialHandInteractionScene.ScenePath,"com.spatialai.xr-interaction/Samples~/HandsInteraction/SpatialHandInteraction.unity"},
            {SpatialHandInteractionScene.PrefabPath,"com.spatialai.xr-interaction/Samples~/HandsInteraction/SpatialHandInteractionRig.prefab"},
            {SpatialHandInteractionScene.ActionsPath,"com.spatialai.xr-interaction/Samples~/HandsInteraction/SpatialHandActions.inputactions"},
            {SpatialHandInteractionScene.GesturesPrefabPath,"com.spatialai.xr-interaction/Samples~/HandsInteraction/XRHandsStandardGestures.prefab"}
        };
        foreach(string file in Directory.GetFiles("Assets/SpatialAI/Gestures","*.asset")) copies.Add(file,"com.spatialai.xr-interaction/Samples~/HandsInteraction/Gestures/"+Path.GetFileName(file));
        var guids=new Dictionary<string,string>();
        foreach(var copy in copies)
        {
            var destination=Path.Combine(root,copy.Value);Directory.CreateDirectory(Path.GetDirectoryName(destination));
            if(File.Exists(destination+".meta"))guids[Guid(copy.Key+".meta")]=Guid(destination+".meta");
            else File.Copy(copy.Key+".meta",destination+".meta");
        }
        foreach(var copy in copies)
        {
            var text=File.ReadAllText(copy.Key);
            foreach(var guid in guids)text=text.Replace(guid.Key,guid.Value);
            File.WriteAllText(Path.Combine(root,copy.Value),text,new System.Text.UTF8Encoding(false));
        }
        UnityEngine.Debug.Log("SPATIALAI_STAGE5_SAMPLES_EXPORTED count="+copies.Count);
    }
    private static string Guid(string path){return Regex.Match(File.ReadAllText(path),@"guid: ([0-9a-f]+)").Groups[1].Value;}
}
