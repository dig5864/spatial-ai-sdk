using System;
using SpatialAI.Core;
using UnityEngine;

namespace SpatialAI.Unity
{
    [Serializable]
    public sealed class SpatialHandSmoothing
    {
        public bool enabled=true;
        [Min(.01f)] public float minCutoffHz=3;
        [Min(0)] public float beta=8;
        [Min(.01f)] public float derivativeCutoffHz=1;
    }

    // One Euro positions: metres, seconds, beta in Hz/(m/s).
    // Orientations follow the same adaptive alpha. Validity and tracking are preserved.
    public sealed class SpatialHandOneEuroFilter
    {
        private sealed class Joint
        {
            public Vector3 raw,position,velocity;
            public Quaternion rotation;
            public bool hasPosition,hasRotation;
        }
        private sealed class Hand
        {
            public int identity;
            public long time=-1;
            public Joint[] joints;
            public void Reset(){time=-1;joints=null;}
        }
        private readonly Hand[] hands={new Hand(),new Hand()};
        private string source;
        private long revision=-1,sequence=-1;
        private SpatialHandsFrame cached;
        public void Reset(){foreach(var h in hands)h.Reset();source=null;revision=sequence=-1;cached=null;}
        private static float Alpha(float cutoff,float dt){return 1f/(1f+1f/(2f*Mathf.PI*cutoff*dt));}
        private static float Safe(float value,float fallback,float minimum)
        {return float.IsNaN(value)||float.IsInfinity(value)||value<minimum?fallback:value;}
        public SpatialHandsFrame Filter(SpatialHandsFrame frame,int leftIdentity,int rightIdentity,SpatialHandSmoothing settings)
        {
            if(frame==null){Reset();return null;}
            if(settings==null||!settings.enabled){Reset();return frame;}
            if(source!=frame.SourceId||revision!=frame.OriginRevision||frame.Sequence<sequence)Reset();
            if(frame.Sequence==sequence)return cached;
            source=frame.SourceId;revision=frame.OriginRevision;sequence=frame.Sequence;
            cached=new SpatialHandsFrame(frame.SourceId,frame.Source,frame.Sequence,frame.OriginRevision,
                frame.SourceTimestampNs,frame.SourceClock,frame.ReceivedTimestampNs,frame.ReceivedClock,
                FilterHand(frame.Left,leftIdentity,frame.ReceivedTimestampNs,settings),
                FilterHand(frame.Right,rightIdentity,frame.ReceivedTimestampNs,settings));
            return cached;
        }
        private SpatialHandSample FilterHand(SpatialHandSample sample,int identity,long time,SpatialHandSmoothing settings)
        {
            var h=hands[(int)sample.Side];
            if(!sample.IsTracked){h.Reset();return sample;}
            if(h.identity!=identity||time<=h.time||time-h.time>350_000_000)h.Reset();
            h.identity=identity;
            float dt=h.time<0?0:(float)((time-h.time)/1e9);h.time=time;
            if(h.joints==null){h.joints=new Joint[(int)HandJointId.Count+1];for(int i=0;i<h.joints.Length;i++)h.joints[i]=new Joint();}
            var joints=new HandJointSample[(int)HandJointId.Count];
            for(int i=0;i<joints.Length;i++)joints[i]=FilterJoint(sample.GetJoint((HandJointId)i),h.joints[i],dt,settings);
            var root=FilterJoint(sample.Root,h.joints[joints.Length],dt,settings);
            return new SpatialHandSample(sample.Side,true,root,joints);
        }
        private static HandJointSample FilterJoint(HandJointSample input,Joint state,float dt,SpatialHandSmoothing settings)
        {
            float cutoff=Safe(settings.minCutoffHz,3,.01f);
            float alpha=dt>0?Alpha(cutoff,dt):1;
            if((input.Valid&HandDataFields.Position)!=0)
            {
                var raw=new Vector3(input.Position.X,input.Position.Y,input.Position.Z);
                if(state.hasPosition&&dt>0)
                {
                    var derivative=(raw-state.raw)/dt;
                    state.velocity=Vector3.Lerp(state.velocity,derivative,Alpha(Safe(settings.derivativeCutoffHz,1,.01f),dt));
                    alpha=Alpha(cutoff+Safe(settings.beta,8,0)*state.velocity.magnitude,dt);
                    state.position=Vector3.Lerp(state.position,raw,alpha);
                }
                else {state.position=raw;state.velocity=Vector3.zero;alpha=1;}
                state.raw=raw;state.hasPosition=true;
            }
            else state.hasPosition=false;
            if((input.Valid&HandDataFields.Rotation)!=0)
            {
                var q=input.Orientation;var raw=new Quaternion(q.X,q.Y,q.Z,q.W);
                state.rotation=state.hasRotation&&dt>0?Quaternion.Slerp(state.rotation,raw,alpha):raw;
                state.hasRotation=true;
            }
            else state.hasRotation=false;
            var r=state.rotation;
            return new HandJointSample(input.Valid,input.Estimated,
                (input.Valid&HandDataFields.Position)!=0?new HandVector3(state.position.x,state.position.y,state.position.z):input.Position,
                (input.Valid&HandDataFields.Rotation)!=0?new Rotation(r.x,r.y,r.z,r.w):input.Orientation,
                input.Radius,input.LinearVelocity,input.AngularVelocity);
        }
    }
}
