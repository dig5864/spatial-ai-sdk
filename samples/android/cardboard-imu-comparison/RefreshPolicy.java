package com.spatialai.samples.cardboardnative;

/** Keep a whole number of display ticks per frame, targeting at least ~60 FPS. */
public final class RefreshPolicy {
    public static int divisor(float refreshHz) {
        return divisor(refreshHz,60);
    }
    public static int divisor(float refreshHz, int targetFps) {
        if (!Float.isFinite(refreshHz) || refreshHz < 1) return 1;
        // Tolerate modes advertised as 59.94 / 119.88 Hz.
        return Math.max(1, (int)Math.floor((refreshHz + .5f) / targetFps));
    }

    public static boolean due(long timestamp, long last, float refreshHz) {
        return due(timestamp,last,refreshHz,60);
    }
    public static boolean due(long timestamp, long last, float refreshHz, int targetFps) {
        if (last == 0 || timestamp <= last) return true;
        // Half-tick tolerance admits VSYNC timestamp jitter without accumulating
        // a fractional-frame debt or issuing catch-up bursts after missed ticks.
        return timestamp - last >= (divisor(refreshHz,targetFps) - .5) * 1e9 / refreshHz;
    }
}
