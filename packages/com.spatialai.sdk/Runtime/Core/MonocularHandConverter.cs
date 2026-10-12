using System;

namespace SpatialAI.Core
{
    public struct NormalizedHandPoint
    {
        public readonly float X,Y,Z;
        public readonly bool Available;
        public NormalizedHandPoint(float x,float y,float z) { X=x;Y=y;Z=z;Available=true; }
    }
    public sealed class MonocularHandConverter
    {
        private readonly UvcHandCalibration suppliedCalibration;
        private readonly float maximumPalmAxisRatio;
        public string GeometryDiagnostics { get; private set; }="";
        public SpatialHandCapabilities Capabilities { get; private set; }
        public string CalibrationWarning { get; private set; } = "";
        public MonocularHandConverter(UvcHandCalibration calibration=null,float maximumPalmAxisRatio=1.6f)
        {
            if(!HandVector3.Finite(maximumPalmAxisRatio)||maximumPalmAxisRatio<1.6f||maximumPalmAxisRatio>4)
                throw new ArgumentOutOfRangeException("maximumPalmAxisRatio","Expected 1.6 to 4 for live estimated geometry.");
            this.maximumPalmAxisRatio=maximumPalmAxisRatio;
            suppliedCalibration=calibration;
            var fields=new HandDataFields[26];
            for(int i=0;i<26;i++) fields[i]=HandDataFields.Position|HandDataFields.Rotation;
            Capabilities=new SpatialHandCapabilities(HandDataFields.Position|HandDataFields.Rotation,fields,fields,
                HandDataFields.Position|HandDataFields.Rotation);
        }
        [Obsolete("Calibration status is advisory. Use the single-argument constructor; missing parameters use preview defaults.")]
        public MonocularHandConverter(UvcHandCalibration calibration,bool allowDevelopmentPreview):this(calibration) { }
        public Result Convert(HandSide side,NormalizedHandPoint[] input,int width,int height,
            HandRigidPose originFromPhysicalHead,out SpatialHandSample hand,out float wristDepthMetres)
        {
            hand=null;wristDepthMetres=0;
            GeometryDiagnostics="";
            if(side!=HandSide.Left && side!=HandSide.Right)return HandJointSample.Invalid("Unknown hand side.");
            hand=SpatialHandSample.Untracked(side);
            UvcHandCalibration calibration;string warning;
            var valid=UvcHandCalibration.ResolveForPreview(suppliedCalibration,
                suppliedCalibration==null || string.IsNullOrWhiteSpace(suppliedCalibration.cameraId)?"unconfigured-uvc":suppliedCalibration.cameraId,
                width,height,out calibration,out warning);
            CalibrationWarning=warning;
            if(!valid.Success)return valid;
            if(input==null || input.Length!=21 || !calibration.MatchesDeliveredSize(width,height) || !originFromPhysicalHead.IsValid)
                return HandJointSample.Invalid("Expected 21 points, exact calibrated image size and physical head pose.");
            var rays=new HandVector3[21];var present=new bool[21];
            for(int i=0;i<21;i++)present[i]=input[i].Available && HandVector3.Finite(input[i].Z) &&
                calibration.TryCameraRay(input[i].X,input[i].Y,out rays[i]);
            foreach(int i in new[]{0,5,9,17})if(!present[i])return HandJointSample.Invalid("Palm anchors absent/outside image.");
            var span=HandGeometry.Sub(rays[5],rays[17]);var length=HandGeometry.Sub(rays[9],rays[0]);
            float spanSize=HandGeometry.Length(span),lengthSize=HandGeometry.Length(length);
            if(spanSize<0.005f || lengthSize<0.005f)return HandJointSample.Invalid(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Degenerate palm span. span={0:F4} length={1:F4}",spanSize,lengthSize));
            float widthDepth=calibration.palmWidthMetres/spanSize,lengthDepth=calibration.wristToMiddleMetres/lengthSize;
            float axisRatio=Math.Max(widthDepth,lengthDepth)/Math.Min(widthDepth,lengthDepth);
            if(axisRatio>maximumPalmAxisRatio)
                return HandJointSample.Invalid(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Palm axes disagree; severe foreshortening or incompatible palm scale. widthDepth={0:F3} lengthDepth={1:F3} ratio={2:F3} limit={3:F1}",
                    widthDepth,lengthDepth,axisRatio,maximumPalmAxisRatio));
            if(axisRatio>1.6f)GeometryDiagnostics=string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "foreshortened accepted: live estimated geometry axisRatio={0:F3} limit={1:F1}",axisRatio,maximumPalmAxisRatio);
            float depth=Math.Min(widthDepth,lengthDepth);
            if(depth<calibration.minDepthMetres || depth>calibration.maxDepthMetres)
                return HandJointSample.Invalid(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Estimated depth outside calibration test range; it is not clamped. depth={0:F3} min={1:F3} max={2:F3}",
                    depth,calibration.minDepthMetres,calibration.maxDepthMetres));
            var positions=new HandVector3[26];var available=new bool[26];
            for(int i=0;i<26;i++)
            {
                int index=LandmarkIndex((HandJointId)i); if(index<0 || !present[index])continue;
                // MediaPipe image z is wrist-relative with an approximate x-like
                // scale, not metric depth. This 2.5D baseline remains estimated.
                float z=depth+(input[index].Z-input[0].Z)*width/(float)Math.Sqrt(calibration.fx*calibration.fy)*depth;
                if(z<=0.05f || z>calibration.maxDepthMetres*1.5f)continue;
                positions[i]=HandGeometry.Scale(rays[index],z);available[i]=true;
            }
            foreach(int m in new[]{6,11,16,21})
                if(available[0] && available[m+1]) { positions[m]=HandGeometry.Add(positions[0],HandGeometry.Scale(HandGeometry.Sub(positions[m+1],positions[0]),0.5f));available[m]=true; }
            if(available[7] && available[12] && available[17] && available[22])
            { positions[1]=HandGeometry.Scale(HandGeometry.Add(HandGeometry.Add(positions[7],positions[12]),HandGeometry.Add(positions[17],positions[22])),0.25f);available[1]=true; }
            if(!available[0] || !available[7] || !available[12] || !available[22])return HandJointSample.Invalid("Unusable palm depth.");
            var forward=HandGeometry.Sub(positions[12],positions[0]);
            var across=HandGeometry.Scale(HandGeometry.Sub(positions[7],positions[22]),side==HandSide.Left?1:-1);
            var up=HandGeometry.Cross(forward,across);
            if(HandGeometry.Length(up)/(HandGeometry.Length(forward)*HandGeometry.Length(across))<0.05f)
                return HandJointSample.Invalid("Palm normal is degenerate.");
            Rotation palmRotation;
            if(!HandGeometry.LookRotation(forward,up,out palmRotation))return HandJointSample.Invalid("No reliable palm orientation.");
            var headFromCamera=calibration.HeadFromCamera;
            var originRotation=originFromPhysicalHead.Orientation*headFromCamera.Orientation;
            var samples=new HandJointSample[26];
            for(int i=0;i<26;i++)
            {
                if(!available[i])continue;
                var p=originFromPhysicalHead.Transform(headFromCamera.Transform(positions[i]));
                Rotation q=palmRotation;bool oriented=i<2;
                if(i>=2)
                {
                    bool tip=i==5 || i>=6 && (i-6)%5==4;
                    int a=tip?i-1:i,b=tip?i:i+1;
                    oriented=available[a] && available[b] && HandGeometry.LookRotation(HandGeometry.Sub(positions[b],positions[a]),up,out q);
                }
                var fields=HandDataFields.Position|(oriented?HandDataFields.Rotation:HandDataFields.None);
                samples[i]=new HandJointSample(fields,fields,p,oriented?originRotation*q:default(Rotation));
            }
            var rootPoint=originFromPhysicalHead.Transform(headFromCamera.Transform(positions[0]));
            var root=new HandJointSample(HandDataFields.Position|HandDataFields.Rotation,
                HandDataFields.Position|HandDataFields.Rotation,rootPoint,originRotation*palmRotation);
            hand=new SpatialHandSample(side,true,root,samples);wristDepthMetres=depth;return Result.Ok;
        }
        public static int LandmarkIndex(HandJointId id)
        {
            int i=(int)id;if(i<0 || id>=HandJointId.Count)throw new ArgumentOutOfRangeException("id");
            if(i==0)return 0;if(i==1)return -1;if(i<=5)return i-1;
            int offset=i-6;return offset%5==0?-1:5+offset/5*4+offset%5-1;
        }
    }
}
