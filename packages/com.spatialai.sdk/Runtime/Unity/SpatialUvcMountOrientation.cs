using System;

namespace SpatialAI.Unity
{
    /// <summary>Applies the same clockwise UVC mount mapping as Android WebView NativeHands.</summary>
    public static class SpatialUvcMountOrientation
    {
        // Operates on landmarks and dimensions together, before any association
        // or monocular unprojection. Does not rotate the captured preview texture.
        public static bool Apply(SpatialHandFrame frame, int degrees, bool mirrorOutputX=false)
        {
            if(degrees!=0 && degrees!=90 && degrees!=180 && degrees!=270)
                throw new ArgumentOutOfRangeException(nameof(degrees));
            if(frame==null || frame.cameraSource!="external-uvc" || (degrees==0 && !mirrorOutputX))return false;
            if(frame.imageWidth<=0 || frame.imageHeight<=0 || frame.inputWidth<=0 || frame.inputHeight<=0)return false;
            float depthScale=(degrees==90 || degrees==270)?(float)frame.inputWidth/frame.inputHeight:1f;
            foreach(var hand in frame.hands??Array.Empty<SpatialHand>())
            {
                if(hand?.joints==null)continue;
                foreach(var p in hand.joints)
                {
                    if(p==null)continue;
                    float x=p.x,y=p.y;
                    switch(degrees)
                    {
                        case 90:p.x=1-y;p.y=x;break;
                        case 180:p.x=1-x;p.y=1-y;break;
                        case 270:p.x=y;p.y=1-x;break;
                    }
                    if(mirrorOutputX)p.x=1-p.x;
                    p.z*=depthScale;
                }
            }
            if(degrees==90 || degrees==270)
            {
                int w=frame.imageWidth;frame.imageWidth=frame.imageHeight;frame.imageHeight=w;
                w=frame.inputWidth;frame.inputWidth=frame.inputHeight;frame.inputHeight=w;
            }
            return true;
        }
    }
}
