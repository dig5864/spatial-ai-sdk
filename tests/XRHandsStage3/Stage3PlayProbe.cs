#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using SpatialAI.Unity;
using SpatialAI.XRHands;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Hands;

public sealed class Stage3PlayProbe : MonoBehaviour
{
    private static void Check(bool valid,string text){if(!valid)throw new Exception(text);}
    private void Send(SpatialUvcHands driver,long sequence,bool sidePalm=false,bool missing=false)
    {
        // Synthetic fixture only, not a hardware accuracy result.
        var c=new SpatialAI.Core.UvcHandCalibration{cameraId="uvc:11cb:281b",imageWidth=640,imageHeight=480,calibrationVersion="probe"};
        var joints=new SpatialHandJoint[21];
        for(int i=0;i<21;i++)joints[i]=new SpatialHandJoint{x=.5f,y=.5f};
        for(int f=0;f<5;f++)for(int j=0;j<4;j++)
        {
            int index=f==0?1+j:5+(f-1)*4+j;
            float x=new[]{-.055f,-.035f,0,.018f,.035f}[f],y=(f==0?.035f:.08f)+j*.025f;
            joints[index]=new SpatialHandJoint{x=.5f+x*554.256f/(640*.5f),y=.5f-y*554.256f/(480*.5f)};
        }
        if(sidePalm){joints[17].x=joints[5].x+.01f;joints[17].y=joints[5].y;}
        var image=new SpatialHandFrame{cameraId=c.cameraId,imageWidth=640,imageHeight=480,sequence=sequence,
            hands=missing?new SpatialHand[0]:new[]{new SpatialHand{rawHandedness="Left",joints=joints}}};
        typeof(SpatialUvcHands).GetMethod("OnFrame",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(driver,new object[]{image});
    }
    private IEnumerator Start()
    {
        var root=new GameObject("UVC driver under test");root.SetActive(false);
        var driver=root.AddComponent<SpatialUvcHands>();root.SetActive(true);
        var visualObject=new GameObject("Compensated SDK visualizer");visualObject.transform.SetParent(root.transform,false);
        visualObject.AddComponent<SpatialXRHandsVisualizer>();
        yield return null;yield return null;
        int dynamic=0,render=0;
        try
        {
            Check(driver.Subsystem!=null && driver.Subsystem.running,"driver owns running subsystem");
            driver.Subsystem.updatedHands+=(s,f,t)=>{if(t==XRHandSubsystem.UpdateType.Dynamic)dynamic++;else render++;};
            var duplicate=new GameObject("duplicate UVC owner").AddComponent<SpatialUvcHands>();
            Check(duplicate.Subsystem==null,"duplicate ownership prevented");Destroy(duplicate.gameObject);
            Send(driver,1);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);yield break;}
        for(int i=0;i<8;i++)yield return null;
        try
        {
            Check(dynamic>=5 && render>=5,"both player loop phases run");
            Check(driver.Subsystem.leftHand.isTracked,"synthetic real-source publication visible in standard API");
            var publicationCount=driver.Source.PublishedFrames;
            Check(publicationCount==1,"rendering does not manufacture inference frames");
            Send(driver,2,sidePalm:true);driver.Subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
            Check(!driver.Subsystem.leftHand.isTracked&&visualObject.transform.Find("Left IndexTip").gameObject.activeSelf,"side-palm SDK visual stays visible without tracked input");
            Send(driver,3,missing:true);driver.Subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
            Check(visualObject.transform.Find("Left IndexTip").gameObject.activeSelf,"short detector loss keeps SDK visual");
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);yield break;}
        yield return new WaitForSecondsRealtime(.13f);
        try
        {
            Check(!visualObject.transform.Find("Left IndexTip").gameObject.activeSelf,"SDK visual expires without a new detection");
            SpatialHandTracking.GestureRecognitionEnabled=false;
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);yield break;}
        yield return null;yield return null;
        try
        {
            Check(!driver.Subsystem.leftHand.isTracked,"recognition off immediately untracks");
            SpatialHandTracking.GestureRecognitionEnabled=true;Send(driver,4);
            driver.Subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
            Check(driver.Subsystem.leftHand.isTracked,"recognition recovery");
            driver.Invalidate("inference backend changed");
            Check(!driver.Subsystem.leftHand.isTracked&&!visualObject.transform.Find("Left IndexTip").gameObject.activeSelf,"backend invalidation clears input and retained SDK visual");
            driver.SendMessage("OnApplicationPause",true);Check(driver.Subsystem==null,"pause destroys subsystem");
            driver.SendMessage("OnApplicationPause",false);Check(driver.Subsystem!=null,"resume restarts subsystem");
            for(int i=0;i<20;i++)
            {
                driver.enabled=false;var all=new List<XRHandSubsystem>();SubsystemManager.GetSubsystems(all);
                Check(all.FindAll(s=>s is SpatialHandSubsystem).Count==0,"disable clears subsystems");
                driver.enabled=true;all.Clear();SubsystemManager.GetSubsystems(all);
                Check(all.FindAll(s=>s is SpatialHandSubsystem).Count==1,"reenable no duplicate subsystem");
                Check(root.GetComponents<SpatialHandTracking>().Length==1 && root.GetComponent<SpatialHandTracking>().enabled,"one camera component across restart");
            }
            driver.StopTracking();Destroy(root);
            Debug.Log("SPATIALAI_STAGE3_PASS lifecycleCycles=20 dynamic="+dynamic+" render="+render+" syntheticOnly=true");
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
#endif
