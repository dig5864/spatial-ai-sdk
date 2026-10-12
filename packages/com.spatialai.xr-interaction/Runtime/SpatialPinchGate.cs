using System;

namespace SpatialAI.XRInteraction
{
    // Advance only on a new source frame, never on a repeated render update.
    public sealed class SpatialPinchGate
    {
        public float pressRatio=.32f,releaseRatio=.48f;
        public double debounceSeconds=.05;
        public bool Armed { get; private set; }
        public bool Pressed { get; private set; }
        private bool candidate;
        private double since=double.NaN,lastTime=double.NaN;
        public void Reset(){Armed=false;Pressed=false;since=lastTime=double.NaN;candidate=false;}
        public bool Advance(float ratio,double time)
        {
            if(float.IsNaN(ratio)||float.IsInfinity(ratio)||ratio<0||double.IsNaN(time)||double.IsInfinity(time)||
                pressRatio<=0||releaseRatio<=pressRatio||debounceSeconds<0)
            {Reset();return false;}
            if(!double.IsNaN(lastTime)&&time<lastTime)Reset();lastTime=time;
            if(!Armed)
            {
                if(ratio<releaseRatio){since=double.NaN;return false;}
                if(double.IsNaN(since))since=time;
                if(time-since>=debounceSeconds){Armed=true;since=double.NaN;}
                return false;
            }
            bool requested=Pressed?ratio<releaseRatio:ratio<=pressRatio;
            if(requested==Pressed){since=double.NaN;return Pressed;}
            if(double.IsNaN(since)||candidate!=requested){candidate=requested;since=time;}
            if(time-since>=debounceSeconds){Pressed=requested;since=double.NaN;}
            return Pressed;
        }
    }
}
