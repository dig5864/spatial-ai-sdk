package com.spatialai.samples;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.os.Build;
import android.util.Log;
import com.unity3d.player.UnityPlayer;

/** Development scene control, registered only while the Unity sample is running. */
public final class SpatialXRSceneControl {
    private static Context context;
    private static BroadcastReceiver receiver;

    public static synchronized void register(Context activity) {
        if (receiver != null) return;
        Context application = activity.getApplicationContext();
        String action = application.getPackageName() + ".SWITCH_SCENE";
        BroadcastReceiver candidate = new BroadcastReceiver() {
            @Override public void onReceive(Context source, Intent intent) {
                String scene = intent.getStringExtra("scene");
                if (scene == null || scene.length() == 0 || scene.length() > 64) {
                    Log.w("SpatialXRScene", "Ignored broadcast: missing or invalid scene extra");
                    return;
                }
                if(intent.hasExtra("oneEuro"))
                    UnityPlayer.UnitySendMessage("SpatialXRSceneControl","SetHandSmoothing",intent.getBooleanExtra("oneEuro",true)?"true":"false");
                String stereoMode=intent.getStringExtra("phoneStereoMode");
                if(stereoMode!=null) {
                    stereoMode=stereoMode.trim().toLowerCase(java.util.Locale.ROOT);
                    if(!"profile".equals(stereoMode) && !"normal".equals(stereoMode) && !"zero".equals(stereoMode) && !"reverse".equals(stereoMode)) {
                        Log.w("SpatialXRScene","Invalid phoneStereoMode; use profile/normal/zero/reverse");return;
                    }
                    UnityPlayer.UnitySendMessage("SpatialXRSceneControl","SetPhoneStereoMode",stereoMode);
                }
                if(intent.hasExtra("cameraPitchDegrees")) {
                    float pitch=intent.getFloatExtra("cameraPitchDegrees",Float.NaN);
                    if(Float.isNaN(pitch)||Float.isInfinite(pitch)||Math.abs(pitch)>45) {
                        Log.w("SpatialXRScene","Invalid cameraPitchDegrees; use -45..45");return;
                    }
                    UnityPlayer.UnitySendMessage("Spatial XRI sample input","PreviewCameraPitch",Float.toString(pitch));
                }
                boolean gestures = intent.getBooleanExtra("gestures", true);
                String cameraSource = intent.getStringExtra("cameraSource");
                cameraSource = cameraSource == null ? "" : cameraSource.trim().toLowerCase(java.util.Locale.ROOT);
                if (!cameraSource.isEmpty() && !"uvc".equals(cameraSource) && !"rear".equals(cameraSource) && !"front".equals(cameraSource)) {
                    Log.w("SpatialXRScene", "Ignored broadcast: cameraSource must be uvc/rear/front");
                    return;
                }
                String cameraSizeTier = "";
                if (intent.hasExtra("cameraSizeTier")) {
                    int tier = intent.getIntExtra("cameraSizeTier", -1);
                    if (tier < 1 || tier > 3) {
                        Log.w("SpatialXRScene", "Ignored broadcast: cameraSizeTier must be 1/2/3");
                        return;
                    }
                    cameraSizeTier = Integer.toString(tier);
                }
                String maxHands = "";
                if (intent.hasExtra("maxHands")) {
                    int count = intent.getIntExtra("maxHands", -1);
                    if (count != 1 && count != 2) {
                        Log.w("SpatialXRScene", "Ignored broadcast: maxHands must be 1 or 2");
                        return;
                    }
                    maxHands = Integer.toString(count);
                }
                int exposureMode = intent.getIntExtra("exposureMode", -1);
                int exposureTimeUs = intent.getIntExtra("exposureTimeUs", -1);
                String inferenceDelegate=intent.getStringExtra("inferenceDelegate");
                inferenceDelegate=inferenceDelegate==null?"":inferenceDelegate.trim().toUpperCase(java.util.Locale.ROOT);
                if (!inferenceDelegate.isEmpty() && !"CPU".equals(inferenceDelegate) && !"GPU".equals(inferenceDelegate)) {
                    Log.w("SpatialXRScene","Ignored broadcast: inferenceDelegate must be CPU or GPU");
                    return;
                }
                String modelVariant=intent.getStringExtra("modelVariant");
                modelVariant=modelVariant==null?"":modelVariant.trim().toUpperCase(java.util.Locale.ROOT);
                if (!modelVariant.isEmpty() && !"FP16".equals(modelVariant) && !"INT8".equals(modelVariant)) {
                    Log.w("SpatialXRScene","Ignored broadcast: modelVariant must be FP16 or INT8");
                    return;
                }
                UnityPlayer.UnitySendMessage("SpatialXRSceneControl", "SwitchScene", scene + "|" +
                    (gestures ? "1" : "0") + "|" + exposureMode + "|" + exposureTimeUs+"|"+inferenceDelegate+"|"+maxHands+"|"+cameraSizeTier+"|"+cameraSource+"|"+
                    (intent.hasExtra("schedulingMode")?intent.getStringExtra("schedulingMode"):"")+"|"+modelVariant);
            }
        };
        IntentFilter filter = new IntentFilter(action);
        if (Build.VERSION.SDK_INT >= 33) {
            application.registerReceiver(candidate, filter, Context.RECEIVER_EXPORTED);
        } else {
            application.registerReceiver(candidate, filter);
        }
        context = application;
        receiver = candidate;
        Log.i("SpatialXRScene", "Broadcast ready: " + action);
    }

    public static synchronized void unregister() {
        if (receiver == null) return;
        context.unregisterReceiver(receiver);
        receiver = null;
        context = null;
    }
}
