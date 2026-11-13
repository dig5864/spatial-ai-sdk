"""Build the Android document-picker plugin from the checked-in source."""
import subprocess
import zipfile
from pathlib import Path

root = Path(__file__).resolve().parents[1]
player = Path('D:/unity/hub/editor/6000.3.27f1/Editor/Data/PlaybackEngines/AndroidPlayer')
build = root / 'build/native/spatial-calibration-picker'
classes = build / 'classes'
classes.mkdir(parents=True, exist_ok=True)
source = root / 'packages/com.spatialai.sdk/Runtime/Unity/Android~/CalibrationPickerActivity.java'
subprocess.run([str(player/'OpenJDK/bin/javac.exe'), '-encoding', 'UTF-8', '-source', '8', '-target', '8', '-classpath', str(player/'SDK/platforms/android-34/android.jar'), '-d', str(classes), str(source)], check=True)
jar = build / 'classes.jar'
subprocess.run([str(player/'OpenJDK/bin/jar.exe'), 'cf', str(jar), '-C', str(classes), '.'], check=True)
aaru = root / 'packages/com.spatialai.sdk/Plugins/Android/spatial-calibration-picker.aar'
manifest = '''<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.spatialai.calibration"><application><activity android:name="com.spatialai.calibration.CalibrationPickerActivity" android:exported="false" android:theme="@android:style/Theme.Translucent.NoTitleBar" /></application></manifest>'''
with zipfile.ZipFile(aaru, 'w', zipfile.ZIP_DEFLATED) as out:
    out.write(jar, 'classes.jar')
    out.writestr('AndroidManifest.xml', manifest)
    out.writestr('proguard.txt', '-keep class com.spatialai.calibration.CalibrationPickerActivity { *; }\n')
print(aaru)
