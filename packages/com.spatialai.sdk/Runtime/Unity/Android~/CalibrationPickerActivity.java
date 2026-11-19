package com.spatialai.calibration;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.util.Base64;
import java.io.ByteArrayOutputStream;
import java.io.FileNotFoundException;
import java.io.IOException;
import java.io.InputStream;
import java.lang.reflect.Method;

public final class CalibrationPickerActivity extends Activity {
    private static final int REQUEST = 7115, MAX = 1024 * 1024;
    private static final Uri SHARED_DEFAULT_URI =
            Uri.parse("content://com.spatialai.diagnostics.webview.calibration/default.json");
    private static String receiver;

    public static String readSharedDefault(Context context) throws IOException {
        try {
            return readCalibration(context, SHARED_DEFAULT_URI);
        } catch (FileNotFoundException unavailable) {
            return null;
        }
    }

    private static String readCalibration(Context context, Uri uri) throws IOException {
        ByteArrayOutputStream out = new ByteArrayOutputStream();
        try (InputStream input = context.getContentResolver().openInputStream(uri)) {
            if (input == null) throw new IOException("Cannot open calibration JSON");
            byte[] buffer = new byte[8192];
            int count;
            while ((count = input.read(buffer)) != -1) {
                if (out.size() + count > MAX) throw new IOException("Calibration JSON exceeds 1 MiB");
                out.write(buffer, 0, count);
            }
        }
        if (out.size() == 0) throw new IOException("Calibration JSON is empty");
        return Base64.encodeToString(out.toByteArray(), Base64.NO_WRAP);
    }
    public static void open(final Activity parent, final String objectName) {
        parent.runOnUiThread(() -> {
            receiver = objectName;
            try { parent.startActivity(new Intent(parent, CalibrationPickerActivity.class)); }
            catch (Exception e) { send("error:" + e.getMessage()); }
        });
    }
    @Override protected void onCreate(Bundle state) {
        super.onCreate(state);
        if (state == null) {
            try {
                Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
                intent.addCategory(Intent.CATEGORY_OPENABLE);
                intent.setType("*/*");
                intent.putExtra(Intent.EXTRA_MIME_TYPES, new String[] {"application/json", "text/plain", "application/octet-stream"});
                intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
                startActivityForResult(intent, REQUEST);
            } catch (Exception e) { send("error:" + e.getMessage()); finish(); }
        }
    }
    @Override protected void onActivityResult(int request, int result, Intent data) {
        super.onActivityResult(request, result, data);
        if (request != REQUEST) return;
        if (result != RESULT_OK || data == null || data.getData() == null) { send("cancel"); finish(); return; }
        try {
            send("ok:" + readCalibration(this, data.getData()));
        } catch (Exception e) { send("error:" + e.getMessage()); }
        finish();
    }
    private static void send(String message) {
        try {
            Class<?> player = Class.forName("com.unity3d.player.UnityPlayer");
            Method method = player.getMethod("UnitySendMessage", String.class, String.class, String.class);
            method.invoke(null, receiver, "OnCalibrationPickerResult", message);
        } catch (Exception e) { android.util.Log.e("SpatialCalibration", "Picker callback failed", e); }
    }
}
