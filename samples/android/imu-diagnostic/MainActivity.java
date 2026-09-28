package com.spatialai.diagnostics.imu;

import android.app.Activity;
import android.os.Bundle;
import android.os.Handler;
import android.os.SystemClock;
import android.hardware.Sensor;
import android.hardware.SensorEvent;
import android.hardware.SensorEventListener;
import android.hardware.SensorManager;
import android.graphics.Color;
import android.util.Log;
import android.view.Gravity;
import android.view.WindowManager;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.Button;
import java.util.Locale;

public final class MainActivity extends Activity implements SensorEventListener {
    static { System.loadLibrary("spatial_imu_diagnostic"); }
    private static native void resetFusion();
    private static native void releaseFusion();
    private static native boolean feedFusion(int type, long timestamp, float x, float y, float z);
    private static native float[] readFusion();
    private final Handler handler = new Handler();
    private SensorManager manager;
    private Sensor accelerometer, gyroscope;
    private TextView left, right;
    private boolean active, registered;
    private long accelCount, gyroCount, accelTime, gyroTime, started, lastLog;
    private int generation;
    private float peakGyro;
    private static final String TAG = "SpatialImu";

    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        manager = (SensorManager)getSystemService(SENSOR_SERVICE);
        accelerometer = manager.getDefaultSensor(Sensor.TYPE_ACCELEROMETER);
        gyroscope = manager.getDefaultSensor(Sensor.TYPE_GYROSCOPE_UNCALIBRATED);
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.HORIZONTAL);
        root.setBackgroundColor(Color.BLACK);
        left = makePanel(root); right = makePanel(root);
        setContentView(root);
        Log.i(TAG, "CREATE accel=" + (accelerometer == null ? "MISSING" : accelerometer.getName()) + " gyro=" + (gyroscope == null ? "MISSING" : gyroscope.getName()));
    }
    private TextView makePanel(LinearLayout root) {
        LinearLayout panel = new LinearLayout(this);
        panel.setOrientation(LinearLayout.VERTICAL); panel.setGravity(Gravity.CENTER);
        panel.setPadding(20,20,20,20);
        root.addView(panel, new LinearLayout.LayoutParams(0,-1,1));
        TextView text = new TextView(this); text.setTextColor(Color.WHITE); text.setTextSize(17);
        panel.addView(text);
        Button reset = new Button(this); reset.setText("Reset fusion");
        reset.setOnClickListener(new android.view.View.OnClickListener() {
            @Override public void onClick(android.view.View view) { resetSession(); }
        }); panel.addView(reset);
        return text;
    }
    private void resetSession() {
        resetFusion(); accelCount=gyroCount=accelTime=gyroTime=lastLog=0; peakGyro=0;
        started=SystemClock.elapsedRealtimeNanos(); generation++;
        Log.i(TAG, "RESET generation=" + generation);
    }
    @Override protected void onResume() {
        super.onResume(); active=true; resetSession();
        boolean a=accelerometer!=null && manager.registerListener(this,accelerometer,10000);
        boolean g=gyroscope!=null && manager.registerListener(this,gyroscope,10000);
        registered=a&&g;
        Log.i(TAG,"RESUME registered="+registered);
        handler.post(tick);
    }
    @Override protected void onPause() {
        active=false; manager.unregisterListener(this); handler.removeCallbacks(tick);
        releaseFusion(); accelTime=gyroTime=0;
        Log.i(TAG,"PAUSE released=true"); super.onPause();
    }
    @Override public void onSensorChanged(SensorEvent e) {
        if (!active || e.timestamp < started || e.timestamp > SystemClock.elapsedRealtimeNanos() || e.values.length<3) return;
        if (!feedFusion(e.sensor.getType(),e.timestamp,e.values[0],e.values[1],e.values[2])) return;
        if(e.sensor.getType()==Sensor.TYPE_ACCELEROMETER) { accelCount++; accelTime=e.timestamp; }
        else {
            gyroCount++; gyroTime=e.timestamp;
            peakGyro=Math.max(peakGyro,(float)Math.sqrt(e.values[0]*e.values[0]+e.values[1]*e.values[1]+e.values[2]*e.values[2]));
        }
    }
    @Override public void onAccuracyChanged(Sensor sensor,int accuracy) {}
    private final Runnable tick = new Runnable() {
        @Override public void run() {
            if(!active) return;
            long now=SystemClock.elapsedRealtimeNanos();
            long ageA=accelTime==0?-1:(now-accelTime)/1000000;
            long ageG=gyroTime==0?-1:(now-gyroTime)/1000000;
            float[] q=readFusion(); double norm=0;
            if(q!=null) for(float v:q) norm+=v*v;
            boolean fresh=registered && accelCount>=20 && gyroCount>=20 && ageA>=0 && ageA<250 && ageG>=0 && ageG<250;
            boolean finite=q!=null && !Double.isNaN(norm) && !Double.isInfinite(norm) && Math.abs(norm-1)<0.002;
            String status=fresh&&finite?"INPUT FRESH / FUSION FINITE":"WAITING / INVALID";
            // Only display a quaternion when inputs are fresh. This is not a tracked-pose claim.
            String rotation=fresh&&finite?String.format(Locale.US,"q %.4f %.4f\n   %.4f %.4f",q[0],q[1],q[2],q[3]):"q unavailable";
            String text="Spatial AI | IMU Diagnostic\n"+status+"\nAccel / Gyro: "+accelCount+" / "+gyroCount+"\nAge ms: "+ageA+" / "+ageG+"\n"+rotation+"\nSensor frame only\nNo stereo / optical correction";
            left.setText(text);right.setText(text);
            if(now-lastLog>1000000000L) {
                Log.i(TAG,String.format(Locale.US,"SAMPLE generation=%d fresh=%b finite=%b accel=%d gyro=%d age_ms=%d,%d norm2=%.6f peak_gyro=%.4f %s",generation,fresh,finite,accelCount,gyroCount,ageA,ageG,norm,peakGyro,rotation.replace('\n',' ')));
                lastLog=now;
            }
            handler.postDelayed(this,100);
        }
    };
}
