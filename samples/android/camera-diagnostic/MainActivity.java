package com.spatialai.diagnostics.camera;

import android.Manifest;
import android.app.Activity;
import android.content.pm.PackageManager;
import android.graphics.Matrix;
import android.graphics.SurfaceTexture;
import android.graphics.Color;
import android.hardware.camera2.CameraAccessException;
import android.hardware.camera2.CameraCharacteristics;
import android.os.Bundle;
import android.os.Handler;
import android.util.Log;
import android.util.Size;
import android.view.View;
import android.view.Surface;
import android.view.TextureView;
import android.view.WindowManager;
import android.widget.Button;
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
    private TextureView texture;
    private Surface previewSurface;
    private volatile String state="STARTING";
    private volatile long previewCount, secondCount, previewId, secondId;
    private volatile long previewTime, secondTime;
    private volatile long previewRenderedCount;
    private volatile int previewChecksum, secondChecksum;
    private long lastStatsAt, lastPreviewCount, lastRenderedCount;
    private int captureFps, previewFps;

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
        texture=new TextureView(this);
        root.addView(texture,new LinearLayout.LayoutParams(0,-1,1));
        texture.setSurfaceTextureListener(new TextureView.SurfaceTextureListener() {
            @Override public void onSurfaceTextureAvailable(SurfaceTexture surface,int width,int height) {
                fitPreview(width,height); if(service!=null) startSelected();
            }
            @Override public void onSurfaceTextureSizeChanged(SurfaceTexture surface,int width,int height) {
                fitPreview(width,height);
            }
            @Override public boolean onSurfaceTextureDestroyed(SurfaceTexture surface) {
                if(service!=null) service.closeCamera();
                releasePreview(); return true;
            }
            @Override public void onSurfaceTextureUpdated(SurfaceTexture surface) {
                previewRenderedCount++;
            }
        });
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
        lastPreviewCount=0;
        previewRenderedCount=lastRenderedCount=0;
        lastStatsAt=0;
        state="OPENING camera="+d.id;
        if (!texture.isAvailable()) { state="Waiting for preview surface"; return; }
        try {
            service.closeCamera(); releasePreview();
            texture.getSurfaceTexture().setDefaultBufferSize(size.getWidth(),size.getHeight());
            previewSurface=new Surface(texture.getSurfaceTexture());
            fitPreview(texture.getWidth(),texture.getHeight());
            service.open(d.id,size.getWidth(),size.getHeight(),previewSurface);
        }
        catch (Exception e) { state="Open failed: "+e; Log.e(TAG,state,e); }
    }
    private static Size selectSize(Size[] choices) {
        Size best=null;
        for (Size size:choices) {
            if ((size.getWidth()&1)!=0 || (size.getHeight()&1)!=0) continue;
            if (best==null || Math.abs(size.getWidth()*size.getHeight()-1920*1080)
                <Math.abs(best.getWidth()*best.getHeight()-1920*1080)) best=size;
        }
        return best;
    }
    private static int checksum(byte[] bytes) {
        int sum=1;
        for (int i=0;i<bytes.length;i+=97) sum=31*sum+(bytes[i]&255);
        return sum;
    }
    private void releasePreview() {
        if(previewSurface!=null) { previewSurface.release(); previewSurface=null; }
    }
    private void fitPreview(int width,int height) {
        if(width<=0 || height<=0) return;
        float targetAspect=16f/9f, viewAspect=(float)width/height;
        boolean front=cameras!=null && !cameras.isEmpty()
            && cameras.get(index).facing==CameraCharacteristics.LENS_FACING_FRONT;
        Matrix matrix=new Matrix();
        float sx=viewAspect<targetAspect?1f:targetAspect/viewAspect;
        float sy=viewAspect<targetAspect?viewAspect/targetAspect:1f;
        matrix.setScale(front?-sx:sx,sy,width/2f,height/2f);
        texture.setTransform(matrix);
    }
    private final Runnable tick=new Runnable() {
        @Override public void run() {
            long now=android.os.SystemClock.uptimeMillis();
            if (lastStatsAt!=0) {
                long elapsed=Math.max(1,now-lastStatsAt);
                captureFps=(int)Math.round((previewCount-lastPreviewCount)*1000.0/elapsed);
                previewFps=(int)Math.round((previewRenderedCount-lastRenderedCount)*1000.0/elapsed);
            }
            lastStatsAt=now; lastPreviewCount=previewCount; lastRenderedCount=previewRenderedCount;
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
            b.append("Capture/preview FPS: ").append(captureFps).append(" / ").append(previewFps)
                .append(" (Camera2 Surface)\n");
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
        releasePreview();
        super.onPause();
    }
}
