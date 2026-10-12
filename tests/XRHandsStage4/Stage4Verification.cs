using System;
using SpatialAI.Core;
using SpatialAI.Unity;
using SpatialAI.XRHands;
using SpatialAI.XRInteraction;
using SpatialAI.XRInteraction.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Stage4Verification
{
    private static int checks;
    private static void Check(bool value,string text){checks++;if(!value)throw new Exception(text);}
    private static SpatialHandFrame CompressedPalm(long sequence)
    {
        var points=new SpatialHandJoint[21];points[0]=new SpatialHandJoint{x=.5f,y=.5f};
        for(int finger=0;finger<5;finger++)for(int joint=0;joint<4;joint++)
        {
            int id=finger==0?1+joint:5+(finger-1)*4+joint;
            float x=finger==0?-.055f:-.035f+(finger-1)*.07f/3;
            float y=(finger==0?.035f:.08f)+joint*.025f;
            points[id]=new SpatialHandJoint{x=.5f+x*.4f*554.256f/(640*.5f),y=.5f-y*554.256f/(480*.5f)};
        }
        return new SpatialHandFrame{cameraId="uvc:11cb:281b",sequence=sequence,imageWidth=640,imageHeight=480,
            hands=new[]{new SpatialHand{rawHandedness="Left",hasHandednessScore=true,handednessScore=.9f,joints=points}}};
    }
    public static void Run()
    {
        try
        {
            Check(SpatialHandTracking.InferenceBackend==SpatialHandInferenceBackend.CPU,"provider defaults to CPU inference");
            var gate=new SpatialPinchGate();
            Check(!gate.Advance(.1f,0)&&!gate.Armed,"pinch on acquisition cannot select");
            Check(!gate.Advance(.6f,.1)&&!gate.Armed,"open must debounce");
            Check(!gate.Advance(.6f,.16)&&gate.Armed,"open arms");
            Check(!gate.Advance(.1f,.2),"short pinch does not press");
            Check(!gate.Advance(.4f,.23),"boundary cancels pending press");
            Check(!gate.Advance(.1f,.3),"new pinch pending");Check(gate.Advance(.1f,.36),"debounced press");
            for(int i=0;i<20;i++)Check(gate.Advance(i%2==0?.36f:.44f,.4+i*.01),"hysteresis avoids chatter");
            Check(gate.Advance(.6f,.7),"release must debounce");Check(!gate.Advance(.6f,.76),"release");
            gate.Reset();Check(!gate.Armed&&!gate.Pressed,"loss resets");Check(!gate.Advance(.1f,.8),"reacquired pinch blocked");
            Check(!gate.Advance(float.NaN,.9)&&!gate.Armed,"invalid geometry blocks");
            gate.Advance(.6f,1);gate.Advance(.6f,1.06);gate.Advance(.1f,1.1);Check(gate.Advance(.1f,1.16),"second press");
            Check(!gate.Advance(.1f,.1)&&!gate.Armed,"clock rollback resets");
            long clock=100_000_000;var strict=new UvcHandsFrameSource(clock:()=>clock);
            strict.Publish(CompressedPalm(1),Pose.identity,0);
            Check(strict.TryGetLatestFrame(out var strictFrame)&&!strictFrame.Left.IsTracked,"default provider keeps strict geometry");
            var tolerant=new UvcHandsFrameSource(clock:()=>clock,maximumPalmAxisRatio:4);
            tolerant.Publish(CompressedPalm(1),Pose.identity,0);
            Check(tolerant.TryGetLatestFrame(out var freshFrame)&&freshFrame.Left.IsTracked,"far interaction accepts fresh compressed palm estimate");
            Check(tolerant.Diagnostics.Contains("foreshortened accepted"),"live geometry relaxation is diagnosed");
            tolerant.Invalidate("USB disconnected");
            Check(!tolerant.TryGetLatestFrame(out freshFrame),"relaxed source clears immediately on camera invalidation");
            tolerant.Publish(CompressedPalm(0),Pose.identity,0);
            Check(tolerant.TryGetLatestFrame(out freshFrame)&&freshFrame.Left.IsTracked,"relaxed policy survives source reconstruction");
            var missing=CompressedPalm(1);missing.hands[0].joints[5]=null;tolerant.Publish(missing,Pose.identity,0);
            Check(tolerant.TryGetLatestFrame(out freshFrame)&&!freshFrame.Left.IsTracked,"missing live anchor never uses held geometry");
            tolerant.Publish(CompressedPalm(2),Pose.identity,0);clock+=351_000_000;
            Check(!tolerant.TryGetLatestFrame(out freshFrame),"relaxed source retains 350ms stale timeout");
            SpatialHandInteractionScene.Ensure();
            Check(System.IO.File.Exists(SpatialHandInteractionScene.ScenePath),"UVC scene generated");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(SpatialHandInteractionScene.PrefabPath);
            Check(prefab!=null&&prefab.GetComponent<SpatialHandInteraction>().rightRay.rightHand,"prefab side bindings");
            Check(prefab.GetComponent<SpatialHandInteraction>().leftRay.inputActions!=null,"prefab carries editable action asset");
            var package=UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.spatialai.xr-interaction");
            string samplePath=System.IO.Path.Combine(package.resolvedPath,"Samples~/HandsInteraction");
            System.IO.Directory.CreateDirectory("Assets/ImportedStage4");
            foreach(var file in new[]{"SpatialHandActions.inputactions","SpatialHandInteractionRig.prefab"})
            {
                System.IO.File.Copy(System.IO.Path.Combine(samplePath,file),"Assets/ImportedStage4/"+file,true);
                System.IO.File.Copy(System.IO.Path.Combine(samplePath,file+".meta"),"Assets/ImportedStage4/"+file+".meta",true);
            }
            AssetDatabase.Refresh();var imported=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ImportedStage4/SpatialHandInteractionRig.prefab");
            Check(imported!=null,"UPM sample prefab imports");
            Check(imported.GetComponent<SpatialHandInteraction>().leftRay.inputActions!=null,"UPM sample action GUID and file ID resolve");
            Debug.Log("SPATIALAI_STAGE4_CONTRACT_PASS checks="+checks);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Stage4 verification").AddComponent<Stage4PlayProbe>();
            EditorApplication.EnterPlaymode();
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
