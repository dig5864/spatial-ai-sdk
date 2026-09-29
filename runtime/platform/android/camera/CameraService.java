package com.spatialai.camera;

import android.content.Context;
import android.graphics.ImageFormat;
import android.hardware.camera2.CameraAccessException;
import android.hardware.camera2.CameraCaptureSession;
import android.hardware.camera2.CameraCharacteristics;
import android.hardware.camera2.CameraDevice;
import android.hardware.camera2.CameraManager;
import android.hardware.camera2.CaptureRequest;
import android.media.Image;
import android.media.ImageReader;
import android.os.Handler;
import android.os.HandlerThread;
import android.util.Size;
import android.hardware.camera2.params.StreamConfigurationMap;
import java.nio.ByteBuffer;
import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.concurrent.atomic.AtomicInteger;

/** One Camera2 capture owner. Subscribers receive immutable I420 frames on their own threads. */
public final class CameraService implements AutoCloseable {
    public interface Listener { void onState(String state); }
    public interface Consumer { void onFrame(Frame frame); }

    public static final class Descriptor {
        public final String id;
        public final int facing;
        public final int timestampSource;
        public final int colorFilter;
        public final Size[] yuvSizes;
        public final String[] physicalIds;
        Descriptor(String id, int facing, int timestampSource, int colorFilter,
                   Size[] sizes, String[] physicalIds) {
            this.id=id; this.facing=facing; this.timestampSource=timestampSource;
            this.colorFilter=colorFilter;
            this.yuvSizes=sizes; this.physicalIds=physicalIds;
        }
    }

    /** Data is valid through the callback; retain/release if passed to another worker. */
    public static final class Frame {
        public final String cameraId;
        public final long sequence, timestampNs;
        public final int width, height, timestampSource;
        public final byte[] i420;
        private final CameraService owner;
        private final int poolEpoch;
        private final AtomicInteger refs=new AtomicInteger(1);
        private Frame(CameraService owner, String id, long seq, long time, int width, int height,
                      int source, int epoch, byte[] data) {
            this.owner=owner; cameraId=id; sequence=seq; timestampNs=time;
            this.width=width; this.height=height; timestampSource=source; poolEpoch=epoch; i420=data;
        }
        public Frame retain() {
            for (;;) {
                int value=refs.get();
                if (value==0) throw new IllegalStateException("Frame already released");
                if (refs.compareAndSet(value,value+1)) return this;
            }
        }
        public void release() {
            int value=refs.decrementAndGet();
            if (value==0) owner.recycle(i420,poolEpoch);
            else if (value<0) throw new IllegalStateException("Frame released twice");
        }
    }

    public final class Subscription implements AutoCloseable {
        private final Consumer consumer;
        private final HandlerThread thread;
        private final Handler handler;
        private Frame pending;
        private boolean scheduled, closed;
        public long delivered, dropped;
        private Subscription(Consumer consumer, String name) {
            this.consumer=consumer; thread=new HandlerThread("SpatialCamera-"+name);
            thread.start(); handler=new Handler(thread.getLooper());
        }
        private synchronized void offer(Frame frame) {
            if (closed) return;
            frame.retain();
            if (pending!=null) { pending.release(); dropped++; }
            pending=frame;
            if (!scheduled) { scheduled=true; handler.post(drain); }
        }
        private final Runnable drain=new Runnable() {
            @Override public void run() {
                for (;;) {
                    Frame frame;
                    synchronized (Subscription.this) {
                        frame=pending; pending=null;
                        if (frame==null) { scheduled=false; return; }
                    }
                    try { consumer.onFrame(frame); delivered++; }
                    catch (RuntimeException e) { notifyState("Consumer error: "+e); }
                    finally { frame.release(); }
                }
            }
        };
        @Override public void close() {
            synchronized (this) {
                if (closed) return;
                closed=true;
                if (pending!=null) { pending.release(); pending=null; }
            }
            synchronized (subscribers) { subscribers.remove(this); }
            thread.quitSafely();
        }
    }

    private static final int MAX_BUFFERS=6;
    private final CameraManager manager;
    private final Listener listener;
    private final HandlerThread cameraThread=new HandlerThread("SpatialCamera-Capture");
    private final Handler cameraHandler;
    private final List<Subscription> subscribers=new ArrayList<>();
    private final ArrayDeque<byte[]> pool=new ArrayDeque<>();
    private CameraDevice camera;
    private CameraCaptureSession session;
    private ImageReader reader;
    private String activeId;
    private int timestampSource, bufferBytes, allocated, poolEpoch;
    private volatile int generation;
    private long sequence;
    private volatile long poolDrops;
    private boolean disposed;

    public CameraService(Context context, Listener listener) {
        manager=(CameraManager)context.getSystemService(Context.CAMERA_SERVICE);
        this.listener=listener;
        cameraThread.start(); cameraHandler=new Handler(cameraThread.getLooper());
    }
    public List<Descriptor> enumerate() throws CameraAccessException {
        List<Descriptor> list=new ArrayList<>();
        for (String id:manager.getCameraIdList()) {
            CameraCharacteristics c=manager.getCameraCharacteristics(id);
            StreamConfigurationMap map=c.get(CameraCharacteristics.SCALER_STREAM_CONFIGURATION_MAP);
            Size[] sizes=map==null?null:map.getOutputSizes(ImageFormat.YUV_420_888);
            Integer facing=c.get(CameraCharacteristics.LENS_FACING);
            Integer source=c.get(CameraCharacteristics.SENSOR_INFO_TIMESTAMP_SOURCE);
            Integer filter=c.get(CameraCharacteristics.SENSOR_INFO_COLOR_FILTER_ARRANGEMENT);
            list.add(new Descriptor(id, facing==null?-1:facing, source==null?-1:source,
                filter==null?-1:filter,
                sizes==null?new Size[0]:sizes, c.getPhysicalCameraIds().toArray(new String[0])));
        }
        return list;
    }
    public Subscription subscribe(Consumer consumer, String name) {
        if (disposed) throw new IllegalStateException("Service disposed");
        Subscription sub=new Subscription(consumer,name);
        synchronized (subscribers) { subscribers.add(sub); }
        return sub;
    }
    public long getPoolDrops() { return poolDrops; }
    public synchronized void open(String id, int width, int height) throws CameraAccessException {
        if (disposed) throw new IllegalStateException("Service disposed");
        closeCamera();
        final int token=++generation;
        CameraCharacteristics c=manager.getCameraCharacteristics(id);
        Integer source=c.get(CameraCharacteristics.SENSOR_INFO_TIMESTAMP_SOURCE);
        timestampSource=source==null?-1:source;
        activeId=id; sequence=0;
        bufferBytes=width*height*3/2;
        synchronized (pool) { pool.clear(); allocated=0; poolEpoch++; }
        reader=ImageReader.newInstance(width,height,ImageFormat.YUV_420_888,3);
        reader.setOnImageAvailableListener(new ImageReader.OnImageAvailableListener() {
            @Override public void onImageAvailable(ImageReader source) { onImage(source,token); }
        },cameraHandler);
        try { manager.openCamera(id,new CameraDevice.StateCallback() {
            @Override public void onOpened(CameraDevice device) {
                synchronized (CameraService.this) {
                if (token!=generation) { device.close(); return; }
                camera=device;
                try {
                    device.createCaptureSession(Collections.singletonList(reader.getSurface()),
                        new CameraCaptureSession.StateCallback() {
                            @Override public void onConfigured(CameraCaptureSession configured) {
                                synchronized (CameraService.this) {
                                if (token!=generation) { configured.close(); return; }
                                session=configured;
                                try {
                                    CaptureRequest.Builder request=device.createCaptureRequest(CameraDevice.TEMPLATE_PREVIEW);
                                    request.addTarget(reader.getSurface());
                                    configured.setRepeatingRequest(request.build(),null,cameraHandler);
                                    notifyState("RUNNING camera="+id+" "+width+"x"+height);
                                } catch (CameraAccessException e) { fail("repeating request",e); }
                                }
                            }
                            @Override public void onConfigureFailed(CameraCaptureSession failed) {
                                synchronized (CameraService.this) {
                                    if (token==generation) fail("session configuration",null);
                                    else failed.close();
                                }
                            }
                        },cameraHandler);
                } catch (CameraAccessException e) { fail("create session",e); }
                }
            }
            @Override public void onDisconnected(CameraDevice device) {
                synchronized (CameraService.this) {
                    device.close(); if (token==generation) fail("camera disconnected",null);
                }
            }
            @Override public void onError(CameraDevice device,int error) {
                synchronized (CameraService.this) {
                    device.close(); if (token==generation) fail("camera error="+error,null);
                }
            }
        },cameraHandler); }
        catch (RuntimeException | CameraAccessException e) { closeCamera(); throw e; }
    }
    private synchronized void onImage(ImageReader imageReader,int token) {
        Image image=null;
        try {
            if (token!=generation) return;
            image=imageReader.acquireLatestImage();
            if (image==null || token!=generation) return;
            byte[] bytes=takeBuffer();
            if (bytes==null) { poolDrops++; return; }
            int epoch=poolEpoch;
            try { copyI420(image,bytes); }
            catch (RuntimeException e) { recycle(bytes,epoch); throw e; }
            Frame frame=new Frame(this,activeId,++sequence,image.getTimestamp(),
                image.getWidth(),image.getHeight(),timestampSource,epoch,bytes);
            synchronized (subscribers) { for (Subscription sub:subscribers) sub.offer(frame); }
            frame.release();
        } catch (RuntimeException e) { if(token==generation) fail("frame",e); }
        finally { if (image!=null) image.close(); }
    }
    private byte[] takeBuffer() {
        synchronized (pool) {
            byte[] bytes=pool.pollFirst();
            if (bytes!=null) return bytes;
            if (allocated>=MAX_BUFFERS) return null;
            allocated++; return new byte[bufferBytes];
        }
    }
    private void recycle(byte[] bytes,int epoch) {
        synchronized (pool) {
            if (epoch!=poolEpoch) return;
            if (bytes.length==bufferBytes) pool.addLast(bytes);
            else allocated--;
        }
    }
    private static void copyI420(Image image,byte[] output) {
        int width=image.getWidth(), height=image.getHeight(), offset=0;
        Image.Plane[] planes=image.getPlanes();
        for (int p=0;p<3;p++) {
            int planeWidth=p==0?width:width/2, planeHeight=p==0?height:height/2;
            ByteBuffer source=planes[p].getBuffer();
            int rowStride=planes[p].getRowStride(), pixelStride=planes[p].getPixelStride();
            for (int y=0;y<planeHeight;y++)
                for (int x=0;x<planeWidth;x++)
                    output[offset++]=source.get(y*rowStride+x*pixelStride);
        }
    }
    public synchronized void closeCamera() {
        generation++;
        if (session!=null) { session.close(); session=null; }
        if (camera!=null) { camera.close(); camera=null; }
        if (reader!=null) { reader.close(); reader=null; }
        activeId=null;
        notifyState("STOPPED");
    }
    private void fail(String step,Exception e) {
        closeCamera();
        notifyState("ERROR "+step+(e==null?"":": "+e));
    }
    private void notifyState(String state) { if (listener!=null) listener.onState(state); }
    @Override public synchronized void close() {
        if (disposed) return;
        disposed=true; closeCamera();
        List<Subscription> copy;
        synchronized (subscribers) { copy=new ArrayList<>(subscribers); }
        for (Subscription sub:copy) sub.close();
        cameraThread.quitSafely();
    }
}
