package com.spatialai.cardboard;

/** Pure SDK policy shared by the Android observer and host-side policy tests. */
public final class FrameRatePolicy {
    public static final int FOLLOW_DISPLAY=0, LOCKED_60=1;
    public final float displayHz,targetFps,headroom;
    public final int softwareCap,divisor,sourceMilliHz,targetMilliHz;
    public final boolean fractional;
    private FrameRatePolicy(float hz,int mode,float margin){
        displayHz=hz;headroom=margin;
        int nearestDivisor=Math.max(1,Math.round(hz/60f));
        boolean nearInteger=mode==LOCKED_60 && Math.abs(hz-nearestDivisor*60)<.5f;
        targetFps=mode==FOLLOW_DISPLAY?hz:(nearInteger?hz/nearestDivisor:Math.min(60f,hz));
        double normalized=Math.round(hz*100.0)/100.0;
        softwareCap=(int)Math.ceil(normalized*margin-.00001);
        sourceMilliHz=mode==LOCKED_60?(nearInteger?nearestDivisor*60000:Math.round(hz*1000)):0;
        targetMilliHz=mode==LOCKED_60?(nearInteger?60000:Math.round(targetFps*1000)):0;
        divisor=mode==FOLLOW_DISPLAY?1:(sourceMilliHz%targetMilliHz==0?sourceMilliHz/targetMilliHz:0);
        fractional=mode==LOCKED_60 && divisor==0;
    }
    public static void validate(int mode,float margin){
        if(mode!=FOLLOW_DISPLAY && mode!=LOCKED_60)throw new IllegalArgumentException("Unknown frame-rate mode");
        if(!Float.isFinite(margin)||margin<2f||margin>4f)throw new IllegalArgumentException("Headroom must be 2..4");
    }
    public static FrameRatePolicy calculate(float hz,int mode,float margin){
        validate(mode,margin);
        if(!Float.isFinite(hz)||hz<1f||hz>1000f)throw new IllegalArgumentException("Invalid display refresh rate");
        return new FrameRatePolicy(hz,mode,margin);
    }
}
