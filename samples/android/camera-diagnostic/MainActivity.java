package com.spatialai.diagnostics.camera;

import android.Manifest;
import android.app.Activity;
import android.content.pm.PackageManager;
import android.graphics.Bitmap;
import android.graphics.Color;
import android.hardware.camera2.CameraAccessException;
import android.hardware.camera2.CameraCharacteristics;
import android.os.Bundle;
import android.os.Handler;
import android.util.Log;
import android.util.Size;
import android.view.View;
import android.view.WindowManager;
import android.widget.Button;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextView;
import com.spatialai.camera.CameraService;
import java.util.List;
import java.util.Locale;

public final class MainActivity extends Activity {
    private static final String TAG="SpatialCamera";
    private final Handler ui=new Handler();
    private CameraService service;
    private CameraService.Subscription preview, second;
    private List<CameraService.Descriptor> cameras;
    private int index;
    private TextView text;
    private ImageView image;
    private volatile String state="STARTING";
    private volatile long previewCount, secondCount, previewId, secondId;
    private volatile long previewTime, secondTime;
    private volatile int previewChecksum, secondChecksum;
    private volatile long previewShownAt;

    @Override public void onCreate(Bundle saved) {
        super.onCreate(saved);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        LinearLayout root=new LinearLayout(this); root.setOrientation(LinearLayout.HORIZONTAL);
        root.setBackgroundColor(Color.BLACK); setContentView(root);
        LinearLayout controls=new LinearLayout(this); controls.setOrientation(LinearLayout.VERTICAL);
        controls.setPadding(20,20,20,20);
        root.addView(controls,new LinearLayout.LayoutParams(0,-1,1));
        text=new TextView(this); text.setTextColor(Color.WHITE); text.setTextSize(16);
        controls.addView(text,new LinearLayout.LayoutParams(-1,0,1));
        Button next=new Button(this); next.setText("Next camera"); controls.addView(next);
        next.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) {
                if (cameras!=null && !cameras.isEmpty()) { index=(index+1)%cameras.size(); startSelected(); }
            }
        });
        Button restart=new Button(this); restart.setText("Restart camera"); controls.addView(restart);
        restart.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { startSelected(); }
        });
        Button stop=new Button(this); stop.setText("Stop camera"); controls.addView(stop);
        stop.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { if(service!=null) service.closeCamera(); }
        });
        image=new ImageView(this); image.setBackgroundColor(Color.DKGRAY);
        root.addView(image,new LinearLayout.LayoutParams(0,-1,1));
        if (checkSelfPermission(Manifest.permission.CAMERA)!=PackageManager.PERMISSION_GRANTED)
            requestPermissions(new String[]{Manifest.permission.CAMERA},1);
    }
    @Override public void onRequestPermissionsResult(int request,String[] permissions,int[] results) {
        super.onRequestPermissionsResult(request,permissions,results);
        if (request==1 && results.length>0 && results[0]==PackageManager.PERMISSION_GRANTED) begin();
        else state="CAMERA permission denied";
    }
    @Override protected void onResume() {
        super.onResume();
        if (checkSelfPermission(Manifest.permission.CAMERA)==PackageManager.PERMISSION_GRANTED) begin();
        ui.post(tick);
    }
    private void begin() {
        if (service!=null) return;
        service=new CameraService(this,new CameraService.Listener() {
            @Override public void onState(String message) { state=message; Log.i(TAG,message); }
        });
        try {
            cameras=service.enumerate();
            if (!cameras.isEmpty()) {
                for (int i=0;i<cameras.size();i++) {
                    int filter=cameras.get(i).colorFilter;
                    if (filter>=0 && filter!=CameraCharacteristics.SENSOR_INFO_COLOR_FILTER_ARRANGEMENT_MONO
                        && filter!=CameraCharacteristics.SENSOR_INFO_COLOR_FILTER_ARRANGEMENT_NIR
                        && cameras.get(i).yuvSizes.length>0) { index=i; break; }
                }
                preview=service.subscribe(new CameraService.Consumer() {
                    @Override public void onFrame(CameraService.Frame frame) {
                        previewCount++; previewId=frame.sequence; previewTime=frame.timestampNs;
                        previewChecksum=checksum(frame.i420);
                        long now=android.os.SystemClock.uptimeMillis();
                        if (now-previewShownAt>500) {
                            previewShownAt=now;
                            final Bitmap bitmap=thumbnail(frame,isColorCamera(frame.cameraId));
                            runOnUiThread(new Runnable() {
                                @Override public void run() { image.setImageBitmap(bitmap); }
                            });
                        }
                    }
                },"preview");
                second=service.subscribe(new CameraService.Consumer() {
                    @Override public void onFrame(CameraService.Frame frame) {
                        secondCount++; secondId=frame.sequence; secondTime=frame.timestampNs;
                        secondChecksum=checksum(frame.i420);
                        if (secondCount%5==0) try { Thread.sleep(80); } catch (InterruptedException ignored) {}
                    }
                },"algorithm-simulator");
                startSelected();
            } else state="No Camera2 cameras";
        } catch (CameraAccessException e) { state="Enumerate failed: "+e; }
    }
    private void startSelected() {
        if(service==null || cameras==null || cameras.isEmpty()) return;
        CameraService.Descriptor d=cameras.get(index);
        Size size=selectSize(d.yuvSizes);
        if(size==null) { state="No YUV output on camera "+d.id; return; }
        previewCount=secondCount=previewId=secondId=0;
        previewTime=secondTime=0;
        state="OPENING camera="+d.id;
        try { service.open(d.id,size.getWidth(),size.getHeight()); }
        catch (Exception e) { state="Open failed: "+e; Log.e(TAG,state,e); }
    }
    private static Size selectSize(Size[] choices) {
        Size best=null;
        for (Size size:choices) {
            if ((size.getWidth()&1)!=0 || (size.getHeight()&1)!=0) continue;
            if (best==null || Math.abs(size.getWidth()*size.getHeight()-640*480)
                <Math.abs(best.getWidth()*best.getHeight()-640*480)) best=size;
        }
        return best;
    }
    private static int checksum(byte[] bytes) {
        int sum=1;
        for (int i=0;i<bytes.length;i+=97) sum=31*sum+(bytes[i]&255);
        return sum;
    }
    private boolean isColorCamera(String id) {
        if (cameras==null) return false;
        for (CameraService.Descriptor camera:cameras) if (camera.id.equals(id))
            return camera.colorFilter!=CameraCharacteristics.SENSOR_INFO_COLOR_FILTER_ARRANGEMENT_MONO
                && camera.colorFilter!=CameraCharacteristics.SENSOR_INFO_COLOR_FILTER_ARRANGEMENT_NIR;
        return false;
    }
    private static int clamp(int value) { return Math.max(0,Math.min(255,value)); }
    private static Bitmap thumbnail(CameraService.Frame frame,boolean color) {
        int w=160,h=120;
        int[] pixels=new int[w*h];
        for(int y=0;y<h;y++) for(int x=0;x<w;x++) {
            int sx=x*frame.width/w, sy=y*frame.height/h;
            int lum=frame.i420[sy*frame.width+sx]&255;
            if (!color) { pixels[y*w+x]=Color.rgb(lum,lum,lum); continue; }
            int chroma=(sy/2)*(frame.width/2)+sx/2;
            int u=(frame.i420[frame.width*frame.height+chroma]&255)-128;
            int v=(frame.i420[frame.width*frame.height*5/4+chroma]&255)-128;
            int c=Math.max(0,lum-16);
            pixels[y*w+x]=Color.rgb(clamp((298*c+409*v+128)>>8),
                clamp((298*c-100*u-208*v+128)>>8),clamp((298*c+516*u+128)>>8));
        }
        return Bitmap.createBitmap(pixels,w,h,Bitmap.Config.ARGB_8888);
    }
    private final Runnable tick=new Runnable() {
        @Override public void run() {
            StringBuilder b=new StringBuilder(state).append('\n');
            if (cameras!=null) {
                b.append("Camera2 count: ").append(cameras.size()).append('\n');
                for (int i=0;i<cameras.size();i++) {
                    CameraService.Descriptor d=cameras.get(i);
                    b.append(i==index?"> ":"  ").append("id=").append(d.id)
                     .append(" facing=").append(d.facing==CameraCharacteristics.LENS_FACING_FRONT?"FRONT":
                        d.facing==CameraCharacteristics.LENS_FACING_BACK?"BACK":"OTHER")
                     .append(" physical=").append(java.util.Arrays.toString(d.physicalIds))
                     .append(" sensor=").append(colorName(d.colorFilter))
                     .append(" YUV modes=").append(d.yuvSizes.length).append('\n');
                }
                CameraService.Descriptor d=cameras.isEmpty()?null:cameras.get(index);
                if (d!=null) b.append("Timestamp source: ").append(d.timestampSource).append('\n');
            }
            b.append(String.format(Locale.US,"Consumer A: %d frames  seq=%d  ts=%d  sum=%08x\n",
                previewCount,previewId,previewTime,previewChecksum));
            b.append(String.format(Locale.US,"Consumer B: %d frames  seq=%d  ts=%d  sum=%08x\n",
                secondCount,secondId,secondTime,secondChecksum));
            if(service!=null) b.append("Pool drops: ").append(service.getPoolDrops()).append('\n');
            b.append("Select each ID and cover its physical lens to map cameras.\n");
            text.setText(b.toString());
            Log.i(TAG,String.format(Locale.US,"A=%d/%d B=%d/%d drops=%d",previewCount,previewId,
                secondCount,secondId,service==null?0:service.getPoolDrops()));
            ui.postDelayed(this,1000);
        }
    };
    private static String colorName(int filter) {
        if (filter==CameraCharacteristics.SENSOR_INFO_COLOR_FILTER_ARRANGEMENT_MONO) return "MONO";
        if (filter==CameraCharacteristics.SENSOR_INFO_COLOR_FILTER_ARRANGEMENT_NIR) return "NIR";
        if (filter==CameraCharacteristics.SENSOR_INFO_COLOR_FILTER_ARRANGEMENT_BGGR) return "BGGR/color";
        if (filter==CameraCharacteristics.SENSOR_INFO_COLOR_FILTER_ARRANGEMENT_RGGB) return "RGGB/color";
        if (filter==CameraCharacteristics.SENSOR_INFO_COLOR_FILTER_ARRANGEMENT_GRBG) return "GRBG/color";
        if (filter==CameraCharacteristics.SENSOR_INFO_COLOR_FILTER_ARRANGEMENT_GBRG) return "GBRG/color";
        if (filter==CameraCharacteristics.SENSOR_INFO_COLOR_FILTER_ARRANGEMENT_RGB) return "RGB";
        return "UNKNOWN";
    }
    @Override protected void onPause() {
        ui.removeCallbacks(tick);
        if(service!=null) { service.close(); service=null; preview=second=null; }
        super.onPause();
    }
}
