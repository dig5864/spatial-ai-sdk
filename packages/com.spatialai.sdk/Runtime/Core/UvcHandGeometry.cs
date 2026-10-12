using System;

namespace SpatialAI.Core
{
    public struct HandRigidPose
    {
        public readonly HandVector3 Position;
        public readonly Rotation Orientation;
        public HandRigidPose(HandVector3 position, Rotation orientation) { Position = position; Orientation = orientation; }
        public bool IsValid { get { return Position.IsFinite && Orientation.IsUnit; } }
        public HandVector3 Transform(HandVector3 point) { return HandGeometry.Add(Position, HandGeometry.Rotate(Orientation, point)); }
        public static HandRigidPose Identity { get { return new HandRigidPose(default(HandVector3), Rotation.Identity); } }
    }
    internal static class HandGeometry
    {
        internal static HandVector3 Add(HandVector3 a, HandVector3 b) { return new HandVector3(a.X+b.X,a.Y+b.Y,a.Z+b.Z); }
        internal static HandVector3 Sub(HandVector3 a, HandVector3 b) { return new HandVector3(a.X-b.X,a.Y-b.Y,a.Z-b.Z); }
        internal static HandVector3 Scale(HandVector3 a, float scale) { return new HandVector3(a.X*scale,a.Y*scale,a.Z*scale); }
        internal static float Dot(HandVector3 a, HandVector3 b) { return a.X*b.X+a.Y*b.Y+a.Z*b.Z; }
        internal static HandVector3 Cross(HandVector3 a, HandVector3 b)
        { return new HandVector3(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X); }
        internal static float Length(HandVector3 v) { return (float)Math.Sqrt(Dot(v,v)); }
        internal static HandVector3 Rotate(Rotation q, HandVector3 v)
        {
            var u = new HandVector3(q.X,q.Y,q.Z); var uv = Cross(u,v);
            return Add(v, Add(Scale(uv,2*q.W),Scale(Cross(u,uv),2)));
        }
        internal static bool LookRotation(HandVector3 forward, HandVector3 up, out Rotation q)
        {
            q = default(Rotation);
            float length = Length(forward); if (length < 0.0001f) return false;
            var z = Scale(forward,1/length); var x = Cross(up,z);
            length = Length(x); if (length < 0.0001f) return false;
            x = Scale(x,1/length); var y = Cross(z,x);
            double trace = x.X+y.Y+z.Z, a,b,c,d,s;
            if (trace > 0) { s=Math.Sqrt(trace+1)*2; d=s/4; a=(y.Z-z.Y)/s; b=(z.X-x.Z)/s; c=(x.Y-y.X)/s; }
            else if (x.X > y.Y && x.X > z.Z) { s=Math.Sqrt(1+x.X-y.Y-z.Z)*2; a=s/4; d=(y.Z-z.Y)/s; b=(y.X+x.Y)/s; c=(z.X+x.Z)/s; }
            else if (y.Y > z.Z) { s=Math.Sqrt(1+y.Y-x.X-z.Z)*2; b=s/4; d=(z.X-x.Z)/s; a=(y.X+x.Y)/s; c=(z.Y+y.Z)/s; }
            else { s=Math.Sqrt(1+z.Z-x.X-y.Y)*2; c=s/4; d=(x.Y-y.X)/s; a=(z.X+x.Z)/s; b=(z.Y+y.Z)/s; }
            q=new Rotation((float)a,(float)b,(float)c,(float)d); return q.IsUnit;
        }
    }

    [Serializable]
    public sealed class UvcHandCalibration
    {
        public int schemaVersion = 1;
        public string cameraId, calibrationVersion, source;
        public int imageWidth, imageHeight;
        public float fx, fy, cx, cy;
        // OpenCV Brown-Conrady k1,k2,p1,p2,k3 (not RhinoX's 8-term rational model).
        public float[] distortion = new float[5];
        // Undo mirror first, then the clockwise image rotation before unprojection.
        public int imageRotationClockwise;
        public bool inputMirrored, swapHandedness;
        public bool intrinsicsCalibrated, extrinsicsMeasured, handednessVerified, palmDimensionsMeasured;
        public float[] headFromCameraTranslationMetres = new float[3];
        public float[] headFromCameraRotationXyzw = new float[] {0,0,0,1};
        public float palmWidthMetres, wristToMiddleMetres;
        public float minDepthMetres = 0.2f, maxDepthMetres = 1.2f;
        public bool validatedForHardware;
        /// <summary>Resolve optional calibration for preview; measurement status is advisory.</summary>
        public static Result ResolveForPreview(UvcHandCalibration supplied, string cameraId, int width, int height,
            out UvcHandCalibration resolved, out string warning)
        {
            resolved=null;warning="";
            if(string.IsNullOrWhiteSpace(cameraId) || width<=0 || height<=0)
                return HandJointSample.Invalid("Camera identity and delivered dimensions are required.");
            var c=supplied;
            if(c!=null && ((c.schemaVersion!=0 && c.schemaVersion!=1) ||
                (!string.IsNullOrWhiteSpace(c.cameraId) && c.cameraId!=cameraId)))
                return HandJointSample.Invalid("Calibration schema/camera identity mismatch.");
            int rotation=c==null?0:c.imageRotationClockwise;
            if(rotation!=0 && rotation!=90 && rotation!=180 && rotation!=270)
                return HandJointSample.Invalid("Invalid image rotation.");
            // Deliberate generic preview assumptions, not measured HXQ or RhinoX data.
            int canonicalWidth=rotation%180==0?width:height,canonicalHeight=rotation%180==0?height:width;
            var defaults=new System.Collections.Generic.List<string>();
            bool missingWidth=c==null || c.imageWidth==0,missingHeight=c==null || c.imageHeight==0;
            int iw=missingWidth?canonicalWidth:c.imageWidth,ih=missingHeight?canonicalHeight:c.imageHeight;
            bool missingFx=c==null || c.fx==0,missingFy=c==null || c.fy==0;
            bool missingLens=missingFx || missingFy;
            bool missingDistortion=c==null || c.distortion==null || c.distortion.Length==0;
            bool missingTranslation=c==null || c.headFromCameraTranslationMetres==null || c.headFromCameraTranslationMetres.Length==0;
            bool missingRotation=c==null || c.headFromCameraRotationXyzw==null || c.headFromCameraRotationXyzw.Length==0 ||
                c.headFromCameraRotationXyzw.Length==4 && c.headFromCameraRotationXyzw[0]==0 && c.headFromCameraRotationXyzw[1]==0 &&
                c.headFromCameraRotationXyzw[2]==0 && c.headFromCameraRotationXyzw[3]==0;
            bool missingPalm=c==null || c.palmWidthMetres==0,missingLength=c==null || c.wristToMiddleMetres==0;
            if(missingWidth || missingHeight)defaults.Add("image size");
            if(missingLens)defaults.Add("60-degree horizontal FOV / centred principal point");
            if(missingDistortion)defaults.Add("zero distortion");
            if(missingTranslation || missingRotation)defaults.Add("identity camera-to-head installation");
            if(missingPalm || missingLength)defaults.Add("70/80 mm palm dimensions");
            if(c==null || c.minDepthMetres==0 || c.maxDepthMetres==0)defaults.Add("0.2-1.2 m depth range");
            string version=c==null?null:c.calibrationVersion;
            if(string.IsNullOrWhiteSpace(version)) { version="uncalibrated-preview-v1";defaults.Add("calibration version"); }
            if(defaults.Count>0)version+="-defaults-"+iw+"x"+ih;
            resolved=new UvcHandCalibration
            {
                cameraId=cameraId,calibrationVersion=version,source=c==null?"Generic uncalibrated preview":c.source,
                imageWidth=iw,imageHeight=ih,fx=missingFx?iw*0.8660254f:c.fx,fy=missingFy?iw*0.8660254f:c.fy,
                cx=c==null || missingLens && c.cx==0?iw*.5f:c.cx,
                cy=c==null || missingLens && c.cy==0?ih*.5f:c.cy,
                distortion=missingDistortion?new float[5]:(float[])c.distortion.Clone(),
                imageRotationClockwise=rotation,inputMirrored=c!=null && c.inputMirrored,swapHandedness=c!=null && c.swapHandedness,
                headFromCameraTranslationMetres=missingTranslation?new float[3]:(float[])c.headFromCameraTranslationMetres.Clone(),
                headFromCameraRotationXyzw=missingRotation?new[]{0f,0f,0f,1f}:(float[])c.headFromCameraRotationXyzw.Clone(),
                palmWidthMetres=missingPalm ? .07f : c.palmWidthMetres,wristToMiddleMetres=missingLength ? .08f : c.wristToMiddleMetres,
                minDepthMetres=c==null || c.minDepthMetres==0 ? .2f : c.minDepthMetres,
                maxDepthMetres=c==null || c.maxDepthMetres==0?1.2f:c.maxDepthMetres,
                intrinsicsCalibrated=c!=null && c.intrinsicsCalibrated && !missingWidth && !missingHeight && !missingLens && !missingDistortion,
                extrinsicsMeasured=c!=null && c.extrinsicsMeasured && !missingTranslation && !missingRotation,
                handednessVerified=c!=null && c.handednessVerified,
                palmDimensionsMeasured=c!=null && c.palmDimensionsMeasured && !missingPalm && !missingLength,
                validatedForHardware=c!=null && c.validatedForHardware && defaults.Count==0
            };
            var valid=resolved.Validate(true);
            if(!valid.Success){resolved=null;return valid;}
            if(!resolved.MatchesDeliveredSize(width,height))
            {resolved=null;return HandJointSample.Invalid("Delivered image size does not match calibration.");}
            if(defaults.Count>0 || !resolved.Validate(false).Success)
                warning="Calibration missing/incomplete/unverified; continuing with DEVELOPMENT preview parameters"+
                    (defaults.Count>0?" (defaults: "+string.Join(", ",defaults.ToArray())+")":"")+".";
            return Result.Ok;
        }
        public Result Validate(bool allowDevelopmentPreview)
        {
            if (schemaVersion != 1 || string.IsNullOrWhiteSpace(cameraId) || string.IsNullOrWhiteSpace(calibrationVersion) ||
                imageWidth <= 0 || imageHeight <= 0 || !Positive(fx) || !Positive(fy) ||
                !HandVector3.Finite(cx) || !HandVector3.Finite(cy) || cx < 0 || cy < 0 || cx > imageWidth || cy > imageHeight ||
                distortion == null || distortion.Length != 5 || headFromCameraTranslationMetres == null ||
                headFromCameraTranslationMetres.Length != 3 || headFromCameraRotationXyzw == null ||
                headFromCameraRotationXyzw.Length != 4 || !Positive(palmWidthMetres) || !Positive(wristToMiddleMetres) ||
                !Positive(minDepthMetres) || !Positive(maxDepthMetres) || maxDepthMetres <= minDepthMetres ||
                (imageRotationClockwise != 0 && imageRotationClockwise != 90 && imageRotationClockwise != 180 && imageRotationClockwise != 270))
                return HandJointSample.Invalid("Incomplete camera geometry, palm dimensions or image transform.");
            foreach (float coefficient in distortion) if (!HandVector3.Finite(coefficient)) return HandJointSample.Invalid("Nonfinite distortion.");
            if (!HeadFromCamera.IsValid) return HandJointSample.Invalid("Camera-to-physical-head extrinsic must be a rigid pose.");
            if (!allowDevelopmentPreview && (!intrinsicsCalibrated || !extrinsicsMeasured || !handednessVerified || !palmDimensionsMeasured))
                return new Result(ErrorCode.Unsupported,"Intrinsics, installation, handedness and palm scale are not fully measured.");
            return Result.Ok;
        }
        public HandRigidPose HeadFromCamera
        {
            get
            {
                var t=headFromCameraTranslationMetres; var q=headFromCameraRotationXyzw;
                return new HandRigidPose(new HandVector3(t[0],t[1],t[2]),new Rotation(q[0],q[1],q[2],q[3]));
            }
        }
        private static bool Positive(float value) { return HandVector3.Finite(value) && value > 0; }
        public bool MatchesDeliveredSize(int width, int height)
        { return imageRotationClockwise % 180 == 0 ? width == imageWidth && height == imageHeight : width == imageHeight && height == imageWidth; }
        public bool TryCameraRay(float normalizedX, float normalizedY, out HandVector3 ray)
        {
            ray=default(HandVector3);
            if(!LensValid())return false;
            if (!HandVector3.Finite(normalizedX) || !HandVector3.Finite(normalizedY) || normalizedX < 0 || normalizedX > 1 || normalizedY < 0 || normalizedY > 1) return false;
            double u=inputMirrored?1-normalizedX:normalizedX, v=normalizedY, x,y;
            switch (imageRotationClockwise)
            {
                case 90: x=v; y=1-u; break;
                case 180: x=1-u; y=1-v; break;
                case 270: x=1-v; y=u; break;
                default: x=u; y=v; break;
            }
            double xd=(x*imageWidth-cx)/fx, yd=(y*imageHeight-cy)/fy;
            x=xd; y=yd;
            for (int iteration=0; iteration<30; iteration++)
            {
                double px,py; Distort(x,y,out px,out py);
                double ex=px-xd,ey=py-yd;
                if (Math.Abs(ex)*fx < 0.01 && Math.Abs(ey)*fy < 0.01) { ray=new HandVector3((float)x,(float)-y,1); return ray.IsFinite; }
                const double step=1e-6;
                double ax,ay,bx,by; Distort(x+step,y,out ax,out ay); Distort(x,y+step,out bx,out by);
                double jxx=(ax-px)/step,jyx=(ay-py)/step,jxy=(bx-px)/step,jyy=(by-py)/step,det=jxx*jyy-jxy*jyx;
                if (Math.Abs(det)<1e-10 || double.IsNaN(det)) return false;
                x-=(jyy*ex-jxy*ey)/det; y-=(jxx*ey-jyx*ex)/det;
                if (double.IsNaN(x) || double.IsNaN(y) || Math.Abs(x)>4 || Math.Abs(y)>4) return false;
            }
            return false;
        }
        public bool TryProject(HandVector3 cameraPoint, out float x, out float y)
        {
            x=y=0; if (!LensValid() || !cameraPoint.IsFinite || cameraPoint.Z <= 0) return false;
            double u,v; Distort(cameraPoint.X/cameraPoint.Z,-cameraPoint.Y/cameraPoint.Z,out u,out v);
            u=(u*fx+cx)/imageWidth; v=(v*fy+cy)/imageHeight;
            double a=u,b=v;
            switch (imageRotationClockwise) { case 90:a=1-v;b=u;break; case 180:a=1-u;b=1-v;break; case 270:a=v;b=1-u;break; }
            x=(float)(inputMirrored?1-a:a); y=(float)b;
            return HandVector3.Finite(x) && HandVector3.Finite(y);
        }
        private void Distort(double x,double y,out double u,out double v)
        {
            double r=x*x+y*y,radial=1+distortion[0]*r+distortion[1]*r*r+distortion[4]*r*r*r;
            u=x*radial+2*distortion[2]*x*y+distortion[3]*(r+2*x*x);
            v=y*radial+distortion[2]*(r+2*y*y)+2*distortion[3]*x*y;
        }
        private bool LensValid()
        {
            if(imageWidth<=0 || imageHeight<=0 || !Positive(fx) || !Positive(fy) || !HandVector3.Finite(cx) ||
                !HandVector3.Finite(cy) || distortion==null || distortion.Length!=5 ||
                (imageRotationClockwise!=0 && imageRotationClockwise!=90 && imageRotationClockwise!=180 && imageRotationClockwise!=270))return false;
            foreach(float value in distortion)if(!HandVector3.Finite(value))return false;
            return true;
        }
    }
}
