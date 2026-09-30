package com.spatialai.hands;

import android.app.Activity;
import android.graphics.Bitmap;
import android.graphics.Color;
import android.hardware.camera2.CameraCharacteristics;
import android.os.SystemClock;
import android.util.Log;
import android.util.Size;
import com.google.mediapipe.framework.image.BitmapImageBuilder;
import com.google.mediapipe.framework.image.MPImage;
import com.google.mediapipe.tasks.core.BaseOptions;
import com.google.mediapipe.tasks.vision.core.RunningMode;
import com.google.mediapipe.tasks.vision.handlandmarker.HandLandmarker;
import com.google.mediapipe.tasks.vision.handlandmarker.HandLandmarkerResult;
import com.google.mediapipe.tasks.components.containers.NormalizedLandmark;
import com.spatialai.camera.CameraService;
import java.util.List;
import org.json.JSONArray;
import org.json.JSONObject;

/** Android inference adapter. Unity reads an immutable JSON snapshot on its main thread. */
public final class SpatialHandBridge {
    private static final int INPUT_WIDTH=384;
    private static final long INPUT_INTERVAL_NS=33_000_000L;
    private final Activity activity;
    private final Object inferenceLock=new Object();
    private CameraService camera;
    private CameraService.Subscription subscription;
    private HandLandmarker landmarker;
    private volatile String state="stopped";
    private volatile String latest="";
    private volatile boolean running;
    private long lastInputNs;
    private long lastCompletedNs;
    private float smoothedFps;
    private long inferenceCount;

    public SpatialHandBridge(Activity activity) { this.activity=activity; }
    public String getState() { return state; }
    public String getLatestJson() { return latest; }

    public void start() {
        if (running) return;
        try {
            BaseOptions base=BaseOptions.builder().setModelAssetPath("hand_landmarker.task").build();
            HandLandmarker.HandLandmarkerOptions options=HandLandmarker.HandLandmarkerOptions.builder()
                .setBaseOptions(base).setRunningMode(RunningMode.VIDEO).setNumHands(2).build();
            landmarker=HandLandmarker.createFromOptions(activity,options);
            camera=new CameraService(activity,new CameraService.Listener() {
                @Override public void onState(String value) { state=value; }
            });
            CameraService.Descriptor selected=null;
            for (CameraService.Descriptor descriptor:camera.enumerate()) {
                if (descriptor.colorFilter<0 ||
                    descriptor.colorFilter==CameraCharacteristics.SENSOR_INFO_COLOR_FILTER_ARRANGEMENT_MONO ||
                    descriptor.colorFilter==CameraCharacteristics.SENSOR_INFO_COLOR_FILTER_ARRANGEMENT_NIR) continue;
                if (selected==null || "2".equals(descriptor.id)) selected=descriptor;
            }
            if (selected==null) throw new IllegalStateException("No RGB Camera2 sensor");
            Size size=null;
            // The RhinoX RGB intrinsics were calibrated at 1440x1080. Prefer that
            // exact Camera2 stream so the image-to-ray mapping has no guessed crop.
            for (Size candidate:selected.yuvSizes)
                if (candidate.getWidth()==1440 && candidate.getHeight()==1080) { size=candidate; break; }
            if (size==null) for (Size candidate:selected.yuvSizes)
                if (candidate.getWidth()==1920 && candidate.getHeight()==1080) { size=candidate; break; }
            if (size==null) throw new IllegalStateException("RGB camera has no supported YUV mode");
            subscription=camera.subscribe(new CameraService.Consumer() {
                @Override public void onFrame(CameraService.Frame frame) { process(frame); }
            },"UnityHands");
            running=true;
            state="opening camera="+selected.id;
            camera.open(selected.id,size.getWidth(),size.getHeight());
        } catch (Exception | UnsatisfiedLinkError error) {
            state="error: "+error;
            stopInternal(false);
        }
    }

    public void stop() { stopInternal(true); }
    private void stopInternal(boolean resetState) {
        running=false;
        if (subscription!=null) { subscription.close(); subscription=null; }
        if (camera!=null) { camera.close(); camera=null; }
        synchronized (inferenceLock) {
            if (landmarker!=null) { landmarker.close(); landmarker=null; }
        }
        latest="";
        if (resetState) state="stopped";
    }

    private void process(CameraService.Frame frame) {
        long now=SystemClock.elapsedRealtimeNanos();
        if (!running || now-lastInputNs<INPUT_INTERVAL_NS) return;
        lastInputNs=now;
        synchronized (inferenceLock) {
            if (!running || landmarker==null) return;
            try {
                Bitmap bitmap=smallBitmap(frame);
                long convertedAtNs=SystemClock.elapsedRealtimeNanos();
                MPImage input=new BitmapImageBuilder(bitmap).build();
                HandLandmarkerResult result;
                try { result=landmarker.detectForVideo(input,frame.timestampNs/1_000_000L); }
                finally { input.close(); }
                long completedAtNs=SystemClock.elapsedRealtimeNanos();
                if (lastCompletedNs>0) {
                    float instantFps=1_000_000_000f/(completedAtNs-lastCompletedNs);
                    smoothedFps=smoothedFps==0?instantFps:smoothedFps*0.8f+instantFps*0.2f;
                }
                lastCompletedNs=completedAtNs;
                float convertMs=(convertedAtNs-now)/1_000_000f;
                float inferenceMs=(completedAtNs-convertedAtNs)/1_000_000f;
                float captureToResultMs=frame.timestampSource==
                    CameraCharacteristics.SENSOR_INFO_TIMESTAMP_SOURCE_REALTIME
                    ?(completedAtNs-frame.timestampNs)/1_000_000f:-1f;
                JSONObject payload=new JSONObject();
                payload.put("cameraId",frame.cameraId);
                payload.put("imageWidth",frame.width);
                payload.put("imageHeight",frame.height);
                payload.put("sequence",frame.sequence);
                payload.put("timestampNs",frame.timestampNs);
                payload.put("receivedAtNs",SystemClock.elapsedRealtimeNanos());
                payload.put("resultFps",smoothedFps);
                payload.put("conversionMs",convertMs);
                payload.put("inferenceMs",inferenceMs);
                payload.put("captureToResultMs",captureToResultMs);
                JSONArray hands=new JSONArray();
                for (int handIndex=0;handIndex<result.landmarks().size();handIndex++) {
                    List<NormalizedLandmark> landmarks=result.landmarks().get(handIndex);
                    JSONObject hand=new JSONObject();
                    JSONArray points=new JSONArray();
                    for (NormalizedLandmark point:landmarks) {
                        JSONObject joint=new JSONObject();
                        joint.put("x",point.x()); joint.put("y",point.y()); joint.put("z",point.z());
                        points.put(joint);
                    }
                    hand.put("joints",points);
                    if (handIndex<result.handedness().size() && !result.handedness().get(handIndex).isEmpty())
                        hand.put("rawHandedness",result.handedness().get(handIndex).get(0).categoryName());
                    hands.put(hand);
                }
                payload.put("hands",hands);
                latest=payload.toString();
                if (++inferenceCount%30==0)
                    Log.i("SpatialHands","inference="+inferenceCount+" hands="+hands.length()+
                        " camera="+frame.cameraId+" fps="+smoothedFps+
                        " convertMs="+convertMs+" inferMs="+inferenceMs+
                        " captureToResultMs="+captureToResultMs);
            } catch (Exception | UnsatisfiedLinkError error) {
                state="inference error: "+error;
                Log.e("SpatialHands",state,error);
            }
        }
    }

    private static int clamp(int value) { return Math.max(0,Math.min(255,value)); }
    private static Bitmap smallBitmap(CameraService.Frame frame) {
        final int inputHeight=frame.height*INPUT_WIDTH/frame.width;
        int[] pixels=new int[INPUT_WIDTH*inputHeight];
        int uvOffset=frame.width*frame.height, vOffset=uvOffset+uvOffset/4;
        for (int y=0;y<inputHeight;y++) {
            int sy=y*frame.height/inputHeight;
            for (int x=0;x<INPUT_WIDTH;x++) {
                int sx=x*frame.width/INPUT_WIDTH;
                int yy=frame.i420[sy*frame.width+sx]&255;
                int chroma=(sy/2)*(frame.width/2)+sx/2;
                int u=(frame.i420[uvOffset+chroma]&255)-128;
                int v=(frame.i420[vOffset+chroma]&255)-128;
                int c=Math.max(0,yy-16);
                pixels[y*INPUT_WIDTH+x]=Color.rgb(clamp((298*c+409*v+128)>>8),
                    clamp((298*c-100*u-208*v+128)>>8),clamp((298*c+516*u+128)>>8));
            }
        }
        return Bitmap.createBitmap(pixels,INPUT_WIDTH,inputHeight,Bitmap.Config.ARGB_8888);
    }
}
