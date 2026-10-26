"""Reuse the SDK Demo IMU implementation in a Unity 6 consumer project."""
import argparse
import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

def prepare(project: Path, request: bool = False):
    project = project.resolve()
    if not (project / 'Packages/manifest.json').is_file():
        raise ValueError('Expected an existing Unity project with Packages/manifest.json')
    version = (project / 'ProjectSettings/ProjectVersion.txt').read_text(encoding='utf-8-sig')
    if 'm_EditorVersion: 6000.' not in version:
        raise ValueError('Expected a Unity 6 project')
    backup = project / 'Library/SpatialUnity6ImuSetup'
    backup.mkdir(parents=True, exist_ok=True)
    for name in ['ProjectSettings.asset', 'EditorBuildSettings.asset']:
        source = project / 'ProjectSettings' / name
        if source.exists() and not (backup / name).exists():
            shutil.copy2(source, backup / name)
    destination = project / 'Assets/SpatialAI/ImuDemo'
    destination.mkdir(parents=True, exist_ok=True)
    support = ROOT / 'samples/unity/Unity6'
    for source in support.rglob('*'):
        if not source.is_file() or source.name == 'README.md':
            continue
        target = destination / source.relative_to(support)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    for name in ['ImuDiagnostics.cs', 'ImuFramePacingProbe.cs', 'SpatialFrameRateDebugControls.cs']:
        source = ROOT / 'samples/unity/SpatialXR/Assets/SpatialXR' / name
        shutil.copy2(source, destination / name)
        shutil.copy2(Path(str(source) + '.meta'), destination / (name + '.meta'))
    resources = destination / 'Resources'
    resources.mkdir(exist_ok=True)
    for name in ['CalibrationUnlit.shader', 'DiagnosticPanel.shader']:
        source = ROOT / 'samples/unity/SpatialXR/Assets/Resources' / name
        shutil.copy2(source, resources / name)
        shutil.copy2(Path(str(source) + '.meta'), resources / (name + '.meta'))
    (destination / 'source.json').write_text(json.dumps({
        'source': 'samples/unity/SpatialXR/Assets/SpatialXR/ImuDiagnostics.cs',
        'productName': 'Spatial-XR-Unity6', 'defaultScene': 'Assets/Scenes/ImuDiagnostics.unity'
    }, indent=2) + '\n', encoding='utf-8')
    if request:
        (project / 'Temp').mkdir(exist_ok=True)
        (project / 'Temp/SpatialUnity6ImuSetup-v2.request').write_text('configure', encoding='utf-8')
    print(destination)

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('project', type=Path)
    parser.add_argument('--request', action='store_true', help='Configure automatically after the open editor refreshes')
    args = parser.parse_args()
    prepare(args.project, args.request)
