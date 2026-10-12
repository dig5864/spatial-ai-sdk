using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Hands;
using UnityEngine.UI;
using SpatialAI.Unity;
using SpatialAI.XRHands;
using SpatialAI.XRInteraction;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class Stage5PlayProbe : MonoBehaviour
{
    [Serializable] public sealed class Paths { public string simulation,interaction,simulationInteraction; }
    private int stage,frames,checks;
    private void Start(){DontDestroyOnLoad(gameObject);}
    private void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    private void Update()
    {
        try
        {
            if(++frames<90)return;
            var systems=new List<XRHandSubsystem>();SubsystemManager.GetSubsystems(systems);systems.RemoveAll(x=>!x.running);
            Check(systems.Count==1,"Imported sample runs exactly one standard XR Hands subsystem");
            Check(systems[0].leftHand.isTracked&&systems[0].rightHand.isTracked,"Both simulated hands available through standard interface");
            Check(systems[0].leftHand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var pose),"Standard joint pose valid");
            if(stage==0)
            {
                Check(FindObjectOfType<SpatialXRHandsVisualizer>()!=null,"Imported simulation contains SDK standard visualizer");
                stage=1;frames=0;var paths=JsonUtility.FromJson<Paths>(File.ReadAllText("stage5-scenes.json"));
                SceneManager.LoadScene(paths.simulationInteraction);return;
            }
            Check(FindObjectOfType<SpatialHandInteraction>()!=null,"Interaction rig starts after scene replacement");
            Check(FindObjectsOfType<Button>().Length>=2,"Ordinary UI buttons are present");
            Check(FindObjectOfType<SpatialHandTracking>()==null,"Simulated sample never creates camera capture");
            Debug.Log("SPATIALAI_STAGE5_PLAY_PASS checks="+checks+" syntheticOnly=true");
#if UNITY_EDITOR
            EditorApplication.Exit(0);
#endif
        }
        catch(Exception error)
        {
            Debug.LogException(error);
#if UNITY_EDITOR
            EditorApplication.Exit(1);
#endif
        }
    }
}
