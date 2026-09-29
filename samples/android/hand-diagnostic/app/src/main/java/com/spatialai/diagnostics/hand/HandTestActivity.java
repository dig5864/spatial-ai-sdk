package com.spatialai.diagnostics.hand;

import android.Manifest;
import android.app.Activity;
import android.content.pm.PackageManager;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Matrix;
import android.graphics.Paint;
import android.graphics.SurfaceTexture;
import android.hardware.camera2.CameraAccessException;
import android.hardware.camera2.CameraCharacteristics;
import android.os.Bundle;
import android.os.Handler;
import android.os.SystemClock;
import android.util.Log;
import android.util.Size;
import android.view.Surface;
import android.view.TextureView;
import android.view.View;
import android.view.WindowManager;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.TextView;
import com.google.mediapipe.framework.image.BitmapImageBuilder;
import com.google.mediapipe.framework.image.MPImage;
import com.google.mediapipe.tasks.core.BaseOptions;
import com.google.mediapipe.tasks.vision.core.RunningMode;
import com.google.mediapipe.tasks.vision.handlandmarker.HandLandmarker;
import com.google.mediapipe.tasks.vision.handlandmarker.HandLandmarkerResult;
import com.google.mediapipe.tasks.components.containers.NormalizedLandmark;
import com.spatialai.camera.CameraService;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

/** CameraService and MediaPipe smoke test. Unity XR integration follows after camera calibration. */
public final class HandTestActivity extends Activity {
    private static final String TAG="SpatialHand";
    private final Handler ui=new Handler();
    private final Object handLock=new Object();
    private CameraService cameraService;
    private CameraService.Subscription handSubscription;
    private TextureView preview;
    private Surface previewSurface;
    private HandOverlay overlay;
    private TextView metrics;
    private HandLandmarker landmarker;
    private volatile String state="STARTING", handState="Waiting for model";
    private volatile long captureCount, inferenceCount, lastInferenceNs, lastInputNs, lastResultNs;
    private volatile int handCount, inferenceMs;
    private long lastStatsNs, lastCaptureCount, lastInferenceCount;
    private int captureFps, inferenceFps;
    private boolean modelFailed;

    @Override public void onCreate(Bundle saved) {
        super.onCreate(saved);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        LinearLayout root=new LinearLayout(this); root.setOrientation(LinearLayout.HORIZONTAL);
        root.setBackgroundColor(Color.BLACK); setContentView(root);
        metrics=new TextView(this); metrics.setTextColor(Color.WHITE); metrics.setTextSize(18);
        metrics.setPadding(30,30,30,30);
        root.addView(metrics,new LinearLayout.LayoutParams(0,-1,1));
        FrameLayout visual=new FrameLayout(this);
        root.addView(visual,new LinearLayout.LayoutParams(0,-1,1));
        preview=new TextureView(this); visual.addView(preview,new FrameLayout.LayoutParams(-1,-1));
        overlay=new HandOverlay(); visual.addView(overlay,new FrameLayout.LayoutParams(-1,-1));
        preview.setSurfaceTextureListener(new TextureView.SurfaceTextureListener() {
            @Override public void onSurfaceTextureAvailable(SurfaceTexture surface,int width,int height) {
                fitPreview(width,height); if(cameraService!=null) startCamera();
            }
            @Override public void onSurfaceTextureSizeChanged(SurfaceTexture surface,int width,int height) {
                fitPreview(width,height);
            }
            @Override public boolean onSurfaceTextureDestroyed(SurfaceTexture surface) {
                if(cameraService!=null) cameraService.closeCamera();
                releaseSurface(); return true;
            }
            @Override public void onSurfaceTextureUpdated(SurfaceTexture surface) {}
        });
        if(checkSelfPermission(Manifest.permission.CAMERA)!=PackageManager.PERMISSION_GRANTED)
            requestPermissions(new String[]{Manifest.permission.CAMERA},1);
    }
    @Override public void onRequestPermissionsResult(int request,String[] permissions,int[] results) {
        super.onRequestPermissionsResult(request,permissions,results);
        if(request==1 && results.length>0 && results[0]==PackageManager.PERMISSION_GRANTED) begin();
        else state="CAMERA PERMISSION DENIED";
    }
    @Override protected void onResume() {
        super.onResume();
        if(checkSelfPermission(Manifest.permission.CAMERA)==PackageManager.PERMISSION_GRANTED) begin();
        ui.post(tick);
    }
    private void begin() {
        if(cameraService!=null) return;
        modelFailed=false; handState="Waiting for model";
        cameraService=new CameraService(this,new CameraService.Listener() {
            @Override public void onState(String message) { state=message; Log.i(TAG,message); }
        });
        handSubscription=cameraService.subscribe(new CameraService.Consumer() {
            @Override public void onFrame(CameraService.Frame frame) { processFrame(frame); }
        },"mediapipe-hand");
        startCamera();
    }
    private void startCamera() {
        if(cameraService==null || !preview.isAvailable()) return;
        try {
            CameraService.Descriptor rgb=null;
            for(CameraService.Descriptor camera:cameraService.enumerate())
                if(camera.colorFilter>=0
                    && camera.colorFilter!=CameraCharacteristics.SENSOR_INFO_COLOR_FILTER_ARRANGEMENT_MONO
                    && camera.colorFilter!=CameraCharacteristics.SENSOR_INFO_COLOR_FILTER_ARRANGEMENT_NIR) {
                    rgb=camera; break;
                }
            if(rgb==null) { state="No RGB Camera2 sensor"; return; }
            Size size=null;
            for(Size candidate:rgb.yuvSizes)
                if(candidate.getWidth()==1920 && candidate.getHeight()==1080) { size=candidate; break; }
            if(size==null) { state="RGB camera has no 1920x1080 YUV mode"; return; }
            cameraService.closeCamera(); releaseSurface();
            preview.getSurfaceTexture().setDefaultBufferSize(size.getWidth(),size.getHeight());
            previewSurface=new Surface(preview.getSurfaceTexture());
            fitPreview(preview.getWidth(),preview.getHeight());
            cameraService.open(rgb.id,size.getWidth(),size.getHeight(),previewSurface);
            state="OPENING RGB id="+rgb.id;
        } catch(Exception e) { state="Camera error: "+e; Log.e(TAG,state,e); }
    }
    private void processFrame(CameraService.Frame frame) {
        captureCount++;
        long now=SystemClock.elapsedRealtimeNanos();
        if(modelFailed || now-lastInputNs<100_000_000L) return;
        lastInputNs=now;
        synchronized(handLock) {
            if(modelFailed) return;
            try {
                if(landmarker==null) {
                    handState="Loading MediaPipe model";
                    BaseOptions base=BaseOptions.builder()
                        .setModelAssetPath("hand_landmarker.task").build();
                    HandLandmarker.HandLandmarkerOptions options=HandLandmarker.HandLandmarkerOptions.builder()
                        .setBaseOptions(base).setRunningMode(RunningMode.VIDEO).setNumHands(2).build();
                    landmarker=HandLandmarker.createFromOptions(this,options);
                    handState="Model ready";
                    Log.i(TAG,"MediaPipe model ready");
                }
                Bitmap bitmap=smallBitmap(frame);
                MPImage input=new BitmapImageBuilder(bitmap).build();
                long start=SystemClock.elapsedRealtimeNanos();
                long timestampMs=frame.timestampNs/1_000_000L;
                HandLandmarkerResult result;
                try { result=landmarker.detectForVideo(input,timestampMs); }
                finally { input.close(); }
                inferenceMs=(int)((SystemClock.elapsedRealtimeNanos()-start)/1_000_000L);
                inferenceCount++; lastInferenceNs=SystemClock.elapsedRealtimeNanos();
                handCount=result.landmarks().size();
                List<float[]> hands=new ArrayList<>();
                for(List<NormalizedLandmark> points:result.landmarks()) {
                    float[] xy=new float[points.size()*2];
                    for(int i=0;i<points.size();i++) {
                        xy[2*i]=points.get(i).x(); xy[2*i+1]=points.get(i).y();
                    }
                    hands.add(xy);
                }
                handState="TRACKING";
                runOnUiThread(new Runnable() {
                    @Override public void run() { overlay.setHands(hands); }
                });
            } catch(Exception e) {
                modelFailed=true; handState="MediaPipe error: "+e.getMessage();
                Log.e(TAG,handState,e);
            } catch(UnsatisfiedLinkError e) {
                modelFailed=true; handState="MediaPipe native error: "+e.getMessage();
                Log.e(TAG,handState,e);
            }
        }
    }
    private static int clamp(int n) { return Math.max(0,Math.min(255,n)); }
    /** Downsample I420 only for inference; capture and hardware preview remain 1080p. */
    private static Bitmap smallBitmap(CameraService.Frame frame) {
        final int width=640,height=360;
        int[] pixels=new int[width*height];
        int uvOffset=frame.width*frame.height;
        int vOffset=uvOffset+uvOffset/4;
        for(int y=0;y<height;y++) {
            int sy=y*frame.height/height;
            for(int x=0;x<width;x++) {
                int sx=x*frame.width/width;
                int yy=frame.i420[sy*frame.width+sx]&255;
                int chroma=(sy/2)*(frame.width/2)+sx/2;
                int u=(frame.i420[uvOffset+chroma]&255)-128;
                int v=(frame.i420[vOffset+chroma]&255)-128;
                int c=Math.max(0,yy-16);
                pixels[y*width+x]=Color.rgb(clamp((298*c+409*v+128)>>8),
                    clamp((298*c-100*u-208*v+128)>>8),clamp((298*c+516*u+128)>>8));
            }
        }
        return Bitmap.createBitmap(pixels,width,height,Bitmap.Config.ARGB_8888);
    }
    private void fitPreview(int width,int height) {
        if(width<=0 || height<=0) return;
        float aspect=16f/9f, viewAspect=(float)width/height;
        float sx=viewAspect<aspect?1f:aspect/viewAspect;
        float sy=viewAspect<aspect?viewAspect/aspect:1f;
        Matrix matrix=new Matrix(); matrix.setScale(-sx,sy,width/2f,height/2f);
        preview.setTransform(matrix);
    }
    private void releaseSurface() {
        if(previewSurface!=null) { previewSurface.release(); previewSurface=null; }
    }
    private final Runnable tick=new Runnable() {
        @Override public void run() {
            long now=SystemClock.elapsedRealtimeNanos();
            if(lastStatsNs>0) {
                double seconds=(now-lastStatsNs)/1e9;
                captureFps=(int)Math.round((captureCount-lastCaptureCount)/seconds);
                inferenceFps=(int)Math.round((inferenceCount-lastInferenceCount)/seconds);
            }
            lastStatsNs=now; lastCaptureCount=captureCount; lastInferenceCount=inferenceCount;
            metrics.setText(String.format(Locale.US,
                "%s\n%s\n\nCamera: 1920x1080\nInference input: 640x360\nCapture: %d FPS\nMediaPipe: %d FPS\nInference: %d ms\nHands: %d\nResult age: %d ms\n\nShow one or two hands to the RGB camera.",
                state,handState,captureFps,inferenceFps,inferenceMs,handCount,
                lastInferenceNs==0?-1:(now-lastInferenceNs)/1_000_000L));
            Log.i(TAG,String.format(Locale.US,"capture=%d inference=%d hands=%d inferenceMs=%d status=%s",
                captureFps,inferenceFps,handCount,inferenceMs,handState));
            ui.postDelayed(this,1000);
        }
    };
    @Override protected void onPause() {
        ui.removeCallbacks(tick);
        if(cameraService!=null) { cameraService.close(); cameraService=null; handSubscription=null; }
        synchronized(handLock) {
            if(landmarker!=null) { landmarker.close(); landmarker=null; }
        }
        releaseSurface();
        super.onPause();
    }
    private final class HandOverlay extends View {
        private final Paint paint=new Paint(Paint.ANTI_ALIAS_FLAG);
        private List<float[]> hands=new ArrayList<>();
        private final int[] edges={0,1,1,2,2,3,3,4,0,5,5,6,6,7,7,8,
            0,9,9,10,10,11,11,12,0,13,13,14,14,15,15,16,
            0,17,17,18,18,19,19,20,5,9,9,13,13,17};
        HandOverlay() { super(HandTestActivity.this); setWillNotDraw(false); }
        void setHands(List<float[]> points) { hands=points; invalidate(); }
        @Override protected void onDraw(Canvas canvas) {
            super.onDraw(canvas);
            float width=getWidth(), height=getHeight();
            float scale=Math.min(width/16f,height/9f);
            float imageWidth=16*scale,imageHeight=9*scale;
            float left=(width-imageWidth)/2f,top=(height-imageHeight)/2f;
            paint.setStrokeWidth(6f); paint.setStyle(Paint.Style.STROKE);
            for(int h=0;h<hands.size();h++) {
                float[] xy=hands.get(h);
                paint.setColor(h==0?Color.GREEN:Color.CYAN);
                for(int i=0;i<edges.length;i+=2) {
                    int a=edges[i]*2,b=edges[i+1]*2;
                    if(b+1>=xy.length) continue;
                    canvas.drawLine(left+xy[a]*imageWidth,top+xy[a+1]*imageHeight,
                        left+xy[b]*imageWidth,top+xy[b+1]*imageHeight,paint);
                }
                paint.setStyle(Paint.Style.FILL);
                for(int i=0;i<xy.length;i+=2)
                    canvas.drawCircle(left+xy[i]*imageWidth,top+xy[i+1]*imageHeight,9f,paint);
                paint.setStyle(Paint.Style.STROKE);
            }
        }
    }
}
