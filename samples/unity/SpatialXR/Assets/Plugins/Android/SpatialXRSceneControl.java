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
                boolean gestures = intent.getBooleanExtra("gestures", true);
                int exposureMode = intent.getIntExtra("exposureMode", -1);
                int exposureTimeUs = intent.getIntExtra("exposureTimeUs", -1);
                String inferenceDelegate=intent.getStringExtra("inferenceDelegate");
                inferenceDelegate=inferenceDelegate==null?"":inferenceDelegate.trim().toUpperCase(java.util.Locale.ROOT);
                if (!inferenceDelegate.isEmpty() && !"CPU".equals(inferenceDelegate) && !"GPU".equals(inferenceDelegate)) {
                    Log.w("SpatialXRScene","Ignored broadcast: inferenceDelegate must be CPU or GPU");
                    return;
                }
                UnityPlayer.UnitySendMessage("SpatialXRSceneControl", "SwitchScene", scene + "|" +
                    (gestures ? "1" : "0") + "|" + exposureMode + "|" + exposureTimeUs+"|"+inferenceDelegate);
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
