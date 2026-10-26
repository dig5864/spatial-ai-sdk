package com.spatialai.samples.cardboardnative;

import android.app.Activity;
import android.os.Bundle;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.hardware.display.DisplayManager;
import android.view.Display;
import android.opengl.GLSurfaceView;
import android.view.Choreographer;
import android.view.MotionEvent;
import android.view.View;
import android.view.WindowManager;
import android.content.res.AssetManager;
import android.util.Log;
import java.io.File;
import java.io.FileOutputStream;
import java.nio.charset.StandardCharsets;
import org.json.JSONObject;
import javax.microedition.khronos.egl.EGLConfig;
import javax.microedition.khronos.opengles.GL10;

/** Independent Java/NDK render loop, using the exact Cardboard binary in the Unity comparison. */
public final class MainActivity extends Activity implements Choreographer.FrameCallback, DisplayManager.DisplayListener {
    static { System.loadLibrary("GfxPluginCardboard"); System.loadLibrary("spatial_cardboard_compare"); }
    private GLSurfaceView view;
    private final SceneRenderer renderer = new SceneRenderer();
    private volatile boolean running;
    // Publish one immutable snapshot: the GL thread must never mix two timelines.
    private volatile FrameTiming frameTiming;
    private long requestSequence;
    private TimelineCallback timelineCallback;
    private long lastRequest;
    private String csvPath;
    private boolean synthetic;
    private int predictionMs;
    private String predictionMode;
    private DisplayManager displays;
    private File rateFile;
    private String refreshPolicy;
    private int preferredFps=60;
    private int requestedModeId, actualModeId=-1, rateGeneration;
    private float callbackHz=60, modeHz;
    private long lastRatePoll, lastRateLog;

    private Display currentDisplay() {
        Display attached = view == null ? null : view.getDisplay();
        return attached != null ? attached : getWindowManager().getDefaultDisplay();
    }

    private void requestDisplayMode() {
        Display display=currentDisplay(); Display.Mode current=display.getMode();
        Display.Mode selected=null; StringBuilder supported=new StringBuilder();
        for (Display.Mode mode: display.getSupportedModes()) {
            supported.append(mode.getModeId()).append(':').append(mode.getPhysicalWidth()).append('x')
                .append(mode.getPhysicalHeight()).append('@').append(mode.getRefreshRate()).append(';');
            if (!refreshPolicy.equals("follow-display") && mode.getPhysicalWidth()==current.getPhysicalWidth() &&
                mode.getPhysicalHeight()==current.getPhysicalHeight() && Math.abs(mode.getRefreshRate()-preferredFps)<1 &&
                (selected==null || Math.abs(mode.getRefreshRate()-preferredFps)<Math.abs(selected.getRefreshRate()-preferredFps))) selected=mode;
        }
        requestedModeId=selected==null?0:selected.getModeId();
        WindowManager.LayoutParams attributes=getWindow().getAttributes();
        attributes.preferredDisplayModeId=requestedModeId;
        getWindow().setAttributes(attributes);
        Log.i("SpatialNative","DISPLAY_REQUEST revision=diagnostics-v6 policy="+refreshPolicy+" displayId="+
            display.getDisplayId()+" requestedModeId="+requestedModeId+" supported="+supported);
        updateDisplayRate("request");
    }

    private void updateDisplayRate(String reason) {
        Display display=currentDisplay(); Display.Mode mode=display.getMode(); float hz=display.getRefreshRate();
        if (!Float.isFinite(hz) || hz<1) { Log.w("SpatialNative","DISPLAY_INVALID refreshHz="+hz); return; }
        boolean changed=actualModeId!=mode.getModeId() || Math.abs(callbackHz-hz)>.01f || Math.abs(modeHz-mode.getRefreshRate())>.01f;
        if (changed || reason.equals("request")) {
            actualModeId=mode.getModeId(); modeHz=mode.getRefreshRate(); callbackHz=hz;
            lastRequest=0; rateGeneration++; frameTiming=null;
            try {
                JSONObject state=new JSONObject();
                state.put("revision","diagnostics-v6"); state.put("policy",refreshPolicy);
                state.put("predictionMs",predictionMs); state.put("preferredFps",preferredFps);
                state.put("predictionMode",predictionMode);
                state.put("timelineSupported",Build.VERSION.SDK_INT>=33);
                state.put("pid",android.os.Process.myPid()); state.put("generation",rateGeneration);
                state.put("displayId",display.getDisplayId()); state.put("requestedModeId",requestedModeId);
                state.put("actualModeId",actualModeId); state.put("modeHz",modeHz); state.put("callbackHz",callbackHz);
                state.put("divisor",RefreshPolicy.divisor(callbackHz,preferredFps)); state.put("targetFps",callbackHz/RefreshPolicy.divisor(callbackHz,preferredFps));
                state.put("modeRequestApplied",requestedModeId!=0 && actualModeId==requestedModeId);
                File temporary=new File(rateFile.getPath()+".tmp");
                try(FileOutputStream stream=new FileOutputStream(temporary)) { stream.write(state.toString().getBytes(StandardCharsets.UTF_8)); }
                if(!temporary.renameTo(rateFile)) throw new java.io.IOException("rate state rename failed");
            } catch(Exception error) { Log.e("SpatialNative","DISPLAY_STATE_WRITE_FAILED",error); }
            logDisplayRate(reason);
        }
    }

    private void logDisplayRate(String reason) {
        Log.i("SpatialNative","DISPLAY_RATE reason="+reason+" generation="+rateGeneration+" policy="+refreshPolicy+
            " requestedModeId="+requestedModeId+" actualModeId="+actualModeId+" modeHz="+modeHz+" callbackHz="+callbackHz+
            " divisor="+RefreshPolicy.divisor(callbackHz,preferredFps)+" targetFps="+(callbackHz/RefreshPolicy.divisor(callbackHz,preferredFps))+
            " modeRequestApplied="+(requestedModeId!=0 && actualModeId==requestedModeId)+" predictionMs="+predictionMs+
            " predictionMode="+predictionMode+" timelineSupported="+(Build.VERSION.SDK_INT>=33));
    }
    @Override public void onDisplayAdded(int id) {}
    @Override public void onDisplayRemoved(int id) {}
    @Override public void onDisplayChanged(int id) {
        if(running && id==currentDisplay().getDisplayId()) updateDisplayRate("listener");
    }
    private native void nativeInitialize();
    private native long nativeCreate(AssetManager assets, String csv, boolean synthetic, int predictionMs, boolean displayTime);
    private static native void nativeResize(long handle, int width, int height);
    private static native void nativeDraw(long handle, long callbackNanos, long expectedPresentNanos,
        long deadlineNanos, long vsyncId, long requestSequence, int generation);
    private static native void nativeReset(long handle);
    private static native void nativeDestroy(long handle);

    @Override public void onCreate(Bundle saved) {
        super.onCreate(saved);
        // SDK Java helpers are initialized on the Activity/UI thread, as in
        // HelloCardboard; tracking/rendering then run on the GL thread.
        nativeInitialize();
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        WindowManager.LayoutParams attributes = getWindow().getAttributes();
        attributes.layoutInDisplayCutoutMode = WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES;
        getWindow().setAttributes(attributes);
        getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_FULLSCREEN |
            View.SYSTEM_UI_FLAG_HIDE_NAVIGATION | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY |
            View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION |
            View.SYSTEM_UI_FLAG_LAYOUT_STABLE);
        synthetic = getIntent().getBooleanExtra("synthetic", false);
        predictionMs=getIntent().getIntExtra("predictionMs",50);
        predictionMode="display-time".equals(getIntent().getStringExtra("predictionMode"))?"display-time":"fixed";
        if(Build.VERSION.SDK_INT>=33) timelineCallback=new TimelineCallback();
        if(predictionMs<0 || predictionMs>100) {
            Log.w("SpatialNative","Invalid predictionMs="+predictionMs+"; using baseline 50");
            predictionMs=50;
        }
        File folder = getExternalFilesDir(null);
        if (folder == null) folder = getFilesDir();
        rateFile=new File(folder,"refresh-state.json");
        refreshPolicy="follow-display".equals(getIntent().getStringExtra("refreshPolicy"))?"follow-display":"prefer60";
        if("prefer120".equals(getIntent().getStringExtra("refreshPolicy"))) { refreshPolicy="prefer120"; preferredFps=120; }
        displays=(DisplayManager)getSystemService(DISPLAY_SERVICE);
        csvPath = new File(folder, "native-frames-" + System.currentTimeMillis() + ".csv").getAbsolutePath();
        view = new GLSurfaceView(this);
        view.setEGLContextClientVersion(3);
        view.setEGLConfigChooser(8,8,8,8,16,0);
        view.setRenderer(renderer);
        view.setRenderMode(GLSurfaceView.RENDERMODE_WHEN_DIRTY);
        view.setOnTouchListener(new View.OnTouchListener() {
          public boolean onTouch(View v, MotionEvent event) {
            if (event.getAction() == MotionEvent.ACTION_DOWN) {
                view.queueEvent(new Runnable() { public void run() { if (renderer.handle != 0) nativeReset(renderer.handle); } });
                return true;
            }
            return true;
          }
        });
        setContentView(view);
        Log.i("SpatialNative", "ACTIVITY revision=diagnostics-v6 scene=imu-comparison displayHz="+
            getWindowManager().getDefaultDisplay().getRefreshRate()+" predictionMs="+predictionMs+
            " predictionMode="+predictionMode+" synthetic="+synthetic+" csv="+csvPath);
    }
    @Override protected void onResume() {
        super.onResume(); running=true; lastRequest=0; frameTiming=null;
        displays.registerDisplayListener(this,new Handler(Looper.getMainLooper()));
        requestDisplayMode();
        view.onResume(); postCallback();
    }
    @Override protected void onPause() {
        running=false; Choreographer.getInstance().removeFrameCallback(this);
        if(timelineCallback!=null) timelineCallback.remove();
        frameTiming=null;
        displays.unregisterDisplayListener(this);
        view.queueEvent(new Runnable() { public void run() {
            if (renderer.handle != 0) { nativeDestroy(renderer.handle); renderer.handle=0; }
        } });
        view.onPause(); super.onPause();
        Log.i("SpatialNative", "PAUSE trackerReleaseQueued=true");
    }
    @Override public void doFrame(long frameTimeNanos) {
        requestFrame(frameTimeNanos,0,0,-1);
    }
    private void postCallback() {
        if(timelineCallback!=null) timelineCallback.post();
        else Choreographer.getInstance().postFrameCallback(this);
    }
    private void requestFrame(long frameTimeNanos,long expected,long deadline,long vsyncId) {
        if (!running) return;
        if(frameTimeNanos-lastRatePoll>=1_000_000_000L) { updateDisplayRate("poll"); lastRatePoll=frameTimeNanos; }
        if(frameTimeNanos-lastRateLog>=5_000_000_000L) { logDisplayRate("periodic"); lastRateLog=frameTimeNanos; }
        if (RefreshPolicy.due(frameTimeNanos,lastRequest,callbackHz,preferredFps)) {
            frameTiming=new FrameTiming(frameTimeNanos,expected,deadline,vsyncId,++requestSequence,rateGeneration);
            lastRequest=frameTimeNanos; view.requestRender();
        }
        postCallback();
    }
    private static final class FrameTiming {
        final long callback, expected, deadline, vsyncId, sequence;
        final int generation;
        FrameTiming(long callback,long expected,long deadline,long vsyncId,long sequence,int generation) {
            this.callback=callback; this.expected=expected; this.deadline=deadline;
            this.vsyncId=vsyncId; this.sequence=sequence; this.generation=generation;
        }
    }
    // Isolate API 33 types from the legacy callback path. FrameData and its
    // FrameTimeline are valid only during onVsync: copy primitives immediately.
    private final class TimelineCallback implements Choreographer.VsyncCallback {
        public void onVsync(Choreographer.FrameData data) {
            Choreographer.FrameTimeline timeline=data.getPreferredFrameTimeline();
            requestFrame(data.getFrameTimeNanos(),timeline.getExpectedPresentationTimeNanos(),
                timeline.getDeadlineNanos(),timeline.getVsyncId());
        }
        void post() { Choreographer.getInstance().postVsyncCallback(this); }
        void remove() { Choreographer.getInstance().removeVsyncCallback(this); }
    }
    private final class SceneRenderer implements GLSurfaceView.Renderer {
        long handle;
        int width, height;
        private void ensure() {
            if (handle==0 && running) {
                handle=nativeCreate(getAssets(),csvPath,synthetic,predictionMs,predictionMode.equals("display-time"));
                if (handle==0) throw new IllegalStateException("Cardboard initialization failed; inspect SpatialNative log");
                if (width>0 && height>0) nativeResize(handle,width,height);
            }
        }
        public void onSurfaceCreated(GL10 gl,EGLConfig config) { ensure(); }
        public void onSurfaceChanged(GL10 gl,int w,int h) {
            width=w; height=h; ensure(); if(handle!=0) nativeResize(handle,w,h);
        }
        public void onDrawFrame(GL10 gl) {
            FrameTiming timing=frameTiming;
            if(running && timing!=null) {
                ensure(); nativeDraw(handle,timing.callback,timing.expected,timing.deadline,
                    timing.vsyncId,timing.sequence,timing.generation);
            }
        }
    }
}
