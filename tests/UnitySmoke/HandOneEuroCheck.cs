#if UNITY_EDITOR
using System;
using SpatialAI.Core;
using SpatialAI.Unity;
using UnityEngine;
public static class HandOneEuroCheck
{
    static void Check(bool ok,string message){if(!ok)throw new Exception("One Euro: "+message);}
    static SpatialHandsFrame Frame(long seq,float x,bool tracked=true,long revision=0)
    {
        var pose=new HandJointSample(HandDataFields.Position|HandDataFields.Rotation,HandDataFields.Position,
            new HandVector3(x,0,.5f),new Rotation(0,0,0,1));
        var joints=new HandJointSample[(int)HandJointId.Count];
        joints[0]=pose;joints[1]=pose;
        var left=tracked?new SpatialHandSample(HandSide.Left,true,pose,joints):SpatialHandSample.Untracked(HandSide.Left);
        return new SpatialHandsFrame("test",HandFrameSource.MonocularEstimate,seq,revision,-1,HandClockDomain.Unknown,
            1_000_000_000+seq*33_333_333,HandClockDomain.HostStopwatch,left,SpatialHandSample.Untracked(HandSide.Right));
    }
    static float X(SpatialHandsFrame f){return f.Left.Root.Position.X;}
    public static void Run()
    {
        var filter=new SpatialHandOneEuroFilter();var settings=new SpatialHandSmoothing();
        var first=Frame(0,0);Check(X(filter.Filter(first,1,0,settings))==0,"first sample moved");
        double raw=0,smoothed=0;
        SpatialHandsFrame output=null;
        for(int i=1;i<=100;i++)
        {
            float x=i%2==0?.002f:-.002f;var input=Frame(i,x);
            output=filter.Filter(input,1,0,settings);
            if(i>20){raw+=x*x;smoothed+=X(output)*X(output);}
            Check(X(input)==x,"input mutated");
            Check(!output.Right.IsTracked,"untracked hand invented");
            Check(output.Left.GetJoint(HandJointId.ThumbTip).Valid==HandDataFields.None,"absent joint invented");
            Check(output.Left.Root.Validate().Success,"invalid quaternion/flags");
            Check(Math.Abs(X(output)-output.Left.GetJoint(HandJointId.Wrist).Position.X)<1e-7,"root/wrist separated");
        }
        Check(smoothed<raw*.5,"stationary jitter insufficiently reduced");
        Check(ReferenceEquals(output,filter.Filter(Frame(100,.2f),1,0,settings)),"duplicate result refiltered");
        var fixedFilter=new SpatialHandOneEuroFilter();var adaptive=new SpatialHandOneEuroFilter();
        var fixedSettings=new SpatialHandSmoothing{beta=0};
        fixedFilter.Filter(Frame(0,0),1,0,fixedSettings);adaptive.Filter(Frame(0,0),1,0,settings);
        Check(X(adaptive.Filter(Frame(1,.1f),1,0,settings))>X(fixedFilter.Filter(Frame(1,.1f),1,0,fixedSettings)),"fast motion not adaptive");
        Check(X(filter.Filter(Frame(101,.3f),2,0,settings))==.3f,"new identity blended");
        filter.Filter(Frame(102,0,false),2,0,settings);
        Check(X(filter.Filter(Frame(103,.4f),2,0,settings))==.4f,"reacquisition blended");
        Check(X(filter.Filter(Frame(104,.5f,true,1),2,0,settings))==.5f,"origin change blended");
        settings.enabled=false;var bypass=Frame(105,.6f,true,1);
        Check(ReferenceEquals(filter.Filter(bypass,2,0,settings),bypass),"disabled filter changed data");
        settings.enabled=true;
        Check(X(filter.Filter(Frame(106,.7f,true,1),2,0,settings))==.7f,"toggle retained stale history");
        Check(X(filter.Filter(Frame(120,.8f,true,1),2,0,settings))==.8f,"long gap retained stale history");
        Debug.Log("HAND_ONE_EURO_PASS jitterPowerRatio="+(smoothed/raw).ToString("F3")+" adaptiveMotion=true resets=true immutable=true validity=true");
    }
}
#endif
