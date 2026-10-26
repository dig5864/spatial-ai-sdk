package com.spatialai.cardboard;

import android.app.Activity;
import android.os.Build;
import android.os.Handler;
import android.os.HandlerThread;
import android.os.Looper;
import android.os.Trace;
import android.hardware.display.DisplayManager;
import android.view.Choreographer;
import android.view.Display;
import android.view.WindowManager;
import android.util.Log;
import java.io.File;
import java.io.FileOutputStream;
import java.nio.charset.StandardCharsets;
import org.json.JSONObject;

/** SDK display observer. Unity retains rendering/swap ownership; no display IPC in VSync callbacks. */
public final class SpatialFrameTiming implements DisplayManager.DisplayListener, Choreographer.FrameCallback {
    static {System.loadLibrary("GfxPluginCardboard");}
    private static SpatialFrameTiming instance;
    private static volatile String statusJson="";
    private final Activity activity;
    private final DisplayManager displays;
    private final Handler handler=new Handler(Looper.getMainLooper());
    private final Api33 timeline;
    private final int originalMode;
    private final float originalRefreshRate;
    private final String csvPath;
    private volatile boolean active;
    private volatile int actualMode=-1;
    private volatile float hz;
    private volatile HandlerThread rateWorker;
    private int policyMode,configurationVersion,generation,requestedMode;
    private float headroom;
    private long lastLog,sequence;
    private FrameRatePolicy policy;

    private SpatialFrameTiming(Activity activity,int mode,float margin,int version){
        this.activity=activity;policyMode=mode;headroom=margin;configurationVersion=version;
        displays=(DisplayManager)activity.getSystemService(Activity.DISPLAY_SERVICE);
        timeline=Build.VERSION.SDK_INT>=33?new Api33():null;
        WindowManager.LayoutParams params=activity.getWindow().getAttributes();
        originalMode=params.preferredDisplayModeId;originalRefreshRate=params.preferredRefreshRate;
        csvPath=new File(activity.getExternalFilesDir(null),"unity-pose-"+System.currentTimeMillis()+".csv").getAbsolutePath();
    }
    private static native void nativeStart(String path);
    private static native void nativeStop();
    private static native void nativePublish(long callback,long expected,long deadline,long id,long sequence,int generation);
    private static native void nativeInvalidate();
    private static native void nativeConfigureCadence(int sourceMilliHz,int targetMilliHz);

    public static void start(Activity activity,int mode,float margin,int version){
        FrameRatePolicy.validate(mode,margin);
        activity.runOnUiThread(()->{
            if(instance!=null)instance.stopOnUi();
            instance=new SpatialFrameTiming(activity,mode,margin,version);
            instance.resumeOnUi();
        });
    }
    public static void configure(int mode,float margin,int version){
        FrameRatePolicy.validate(mode,margin);
        new Handler(Looper.getMainLooper()).post(()->{
            if(instance==null)return;
            instance.policyMode=mode;instance.headroom=margin;instance.configurationVersion=version;
            if(instance.active){instance.applyWindowPolicy();instance.updateRate(true);}
        });
    }
    public static void setPaused(boolean value){
        new Handler(Looper.getMainLooper()).post(()->{
            if(instance!=null){if(value)instance.stopOnUi();else instance.resumeOnUi();}
        });
    }
    public static void stop(){
        new Handler(Looper.getMainLooper()).post(()->{if(instance!=null){instance.stopOnUi();instance=null;}});
    }
    public static String getStatusJson(){return statusJson;}
    private Display display(){return activity.getWindowManager().getDefaultDisplay();}

    private void applyWindowPolicy(){
        WindowManager.LayoutParams params=activity.getWindow().getAttributes();
        if(policyMode==FrameRatePolicy.FOLLOW_DISPLAY){
            requestedMode=originalMode;
            params.preferredDisplayModeId=originalMode;params.preferredRefreshRate=originalRefreshRate;
        }else{
            Display.Mode current=display().getMode(),selected=null;
            for(Display.Mode mode:display().getSupportedModes()){
                if(mode.getPhysicalWidth()!=current.getPhysicalWidth() || mode.getPhysicalHeight()!=current.getPhysicalHeight())continue;
                float rate=mode.getRefreshRate();
                if(Math.abs(rate-60)<1){selected=mode;break;}
                if(rate>=60 && Math.abs(rate/60-Math.round(rate/60))<.01f &&
                   (selected==null || rate<selected.getRefreshRate()))selected=mode;
            }
            requestedMode=selected==null?0:selected.getModeId();
            params.preferredDisplayModeId=requestedMode;params.preferredRefreshRate=60;
        }
        activity.getWindow().setAttributes(params);
    }
    private void resumeOnUi(){
        if(active)return;active=true;
        applyWindowPolicy();nativeStart(csvPath);
        displays.registerDisplayListener(this,handler);
        updateRate(true);startRateObserver();post();
    }
    private void stopOnUi(){
        if(!active)return;active=false;
        Choreographer.getInstance().removeFrameCallback(this);if(timeline!=null)timeline.remove();
        displays.unregisterDisplayListener(this);nativeStop();
        HandlerThread worker=rateWorker;rateWorker=null;if(worker!=null)worker.quitSafely();
        WindowManager.LayoutParams params=activity.getWindow().getAttributes();
        params.preferredDisplayModeId=originalMode;params.preferredRefreshRate=originalRefreshRate;
        activity.getWindow().setAttributes(params);
        publishStatus();
    }
    private void startRateObserver(){
        final Display observed=display();
        final HandlerThread worker=new HandlerThread("SpatialDisplayRate");rateWorker=worker;worker.start();
        final Handler pollHandler=new Handler(worker.getLooper());
        pollHandler.postDelayed(new Runnable(){
            public void run(){
                if(!active || rateWorker!=worker)return;
                Trace.beginSection("SpatialUnity.displayRatePoll");
                try{
                    int mode=observed.getMode().getModeId();float rate=observed.getRefreshRate();
                    if(mode!=actualMode || Math.abs(rate-hz)>.01f)
                        handler.post(()->{if(active && rateWorker==worker)updateRate(false);});
                }catch(RuntimeException error){Log.w("SpatialUnityTiming","background display poll failed",error);}
                finally{Trace.endSection();}
                if(active && rateWorker==worker)pollHandler.postDelayed(this,1000);
            }
        },1000);
    }
    private void updateRate(boolean force){
        Display d=display();Display.Mode mode=d.getMode();float rate=d.getRefreshRate();
        if(!Float.isFinite(rate)||rate<1||rate>1000)return;
        if(force || actualMode!=mode.getModeId() || Math.abs(hz-rate)>.01f){
            actualMode=mode.getModeId();hz=rate;generation++;
            policy=FrameRatePolicy.calculate(hz,policyMode,headroom);
            nativeInvalidate();nativeConfigureCadence(policy.sourceMilliHz,policy.targetMilliHz);
            publishStatus();
        }
    }
    private void publishStatus(){
        if(policy==null)return;
        try{
            JSONObject state=new JSONObject();state.put("revision","sdk-frame-rate-v1");state.put("active",active);
            state.put("policy",policyMode==FrameRatePolicy.LOCKED_60?"locked60":"follow-display");
            state.put("policyMode",policyMode);state.put("configurationVersion",configurationVersion);
            state.put("softwareHeadroomMultiplier",headroom);state.put("softwareFrameRateCap",policy.softwareCap);
            state.put("cadenceBackend",policyMode==FrameRatePolicy.LOCKED_60?"xr-populate":"disabled");
            state.put("fractionalCadence",policy.fractional);state.put("cadenceSourceMilliHz",policy.sourceMilliHz);
            state.put("cadenceTargetMilliHz",policy.targetMilliHz);
            state.put("refreshObserver","background-poll-and-display-listener");
            state.put("predictionMode","display-time");state.put("predictionMs",50);
            state.put("preferredFps",policyMode==FrameRatePolicy.LOCKED_60?60:0);
            state.put("pid",android.os.Process.myPid());state.put("generation",generation);state.put("displayId",display().getDisplayId());
            state.put("requestedModeId",requestedMode);state.put("actualModeId",actualMode);state.put("modeHz",hz);
            state.put("callbackHz",hz);state.put("divisor",policy.divisor);state.put("targetFps",policy.targetFps);
            state.put("unityTargetFps",Math.round(policy.targetFps));state.put("timelineSupported",timeline!=null);
            state.put("modeRequestApplied",requestedMode!=0 && requestedMode==actualMode);
            statusJson=state.toString();
            File dest=new File(activity.getExternalFilesDir(null),"refresh-state.json"),temp=new File(dest.getPath()+".tmp");
            try(FileOutputStream stream=new FileOutputStream(temp)){stream.write(statusJson.getBytes(StandardCharsets.UTF_8));}
            if(!temp.renameTo(dest))throw new java.io.IOException("state rename");
            Log.i("SpatialUnityTiming","DISPLAY_STATE "+statusJson);
        }catch(Exception error){Log.e("SpatialUnityTiming","state write failed",error);}
    }
    private void post(){if(timeline!=null)timeline.post();else Choreographer.getInstance().postFrameCallback(this);}
    private void frame(long callback,long expected,long deadline,long id){
        if(!active)return;
        Trace.beginSection("SpatialUnity.publishTimeline");
        try{nativePublish(callback,expected,deadline,id,++sequence,generation);}finally{Trace.endSection();}
        if(callback-lastLog>=5000000000L){
            Log.i("SpatialUnityTiming","DISPLAY_RATE modeHz="+hz+" targetFps="+(policy==null?0:policy.targetFps)+
                " softwareCap="+(policy==null?0:policy.softwareCap)+" configurationVersion="+configurationVersion+" polling=background");
            lastLog=callback;
        }
        post();
    }
    public void doFrame(long time){frame(time,0,0,-1);}
    private final class Api33 implements Choreographer.VsyncCallback{
        public void onVsync(Choreographer.FrameData data){
            Choreographer.FrameTimeline t=data.getPreferredFrameTimeline();
            frame(data.getFrameTimeNanos(),t.getExpectedPresentationTimeNanos(),t.getDeadlineNanos(),t.getVsyncId());
        }
        void post(){Choreographer.getInstance().postVsyncCallback(this);}
        void remove(){Choreographer.getInstance().removeVsyncCallback(this);}
    }
    public void onDisplayAdded(int id){}
    public void onDisplayRemoved(int id){}
    public void onDisplayChanged(int id){if(active&&id==display().getDisplayId())updateRate(false);}
}
