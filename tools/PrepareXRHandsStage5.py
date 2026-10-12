"""Create consumer projects containing only copied release tarballs, not source packages."""
import argparse, hashlib, importlib.util, io, json, shutil, subprocess, tarfile, uuid
from pathlib import Path
ROOT=Path(__file__).resolve().parent.parent
parser=argparse.ArgumentParser();parser.add_argument('release')
parser.add_argument('--baseline-revision',default='4cbde6bac79762a55522aafd702fa1977797bcaf',help='Tracked preview.9 revision used for serialized upgrade checks')
args=parser.parse_args()
release=Path(args.release).resolve()
manifest=json.loads((release/'release-manifest.json').read_text(encoding='utf-8-sig'))
for line in (release/'SHA256SUMS.txt').read_text(encoding='utf-8').splitlines():
    digest,name=line.split('  ',1)
    if Path(name).name!=name or hashlib.sha256((release/name).read_bytes()).hexdigest()!=digest:raise ValueError('Release checksum mismatch: '+name)
root=ROOT/'build/validation'/('xr-hands-stage5-'+uuid.uuid4().hex);root.mkdir(parents=True)
shutil.copytree(release,root/'SDKRelease')
files={item['name']:item['file'] for item in manifest['packages']}
settings='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!129 &1\nPlayerSettings:\n  m_ObjectHideFlags: 0\n  serializedVersion: 26\n  activeInputHandler: 2\n'
for mode in ['full','display','upgrade']:
    project=root/mode
    for directory in ['Assets/Editor','Packages','ProjectSettings']:(project/directory).mkdir(parents=True,exist_ok=True)
    (project/'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 2022.3.14f1\n')
    (project/'ProjectSettings/ProjectSettings.asset').write_text(settings)
    shutil.copy2(ROOT/'tests/UnityInstaller/InstallerVerification.cs',project/'Assets/Editor/InstallerVerification.cs')
    names=list(files) if mode=='full' else ['com.spatialai.sdk','com.google.xr.cardboard']
    deps={name:'file:../../SDKRelease/'+files[name] for name in names}
    if mode=='upgrade':
        # Pin the original preview.9 baseline so committing the new SDK cannot silently change this test.
        archive=subprocess.check_output(['git','-C',str(ROOT),'archive',args.baseline_revision,'packages/com.spatialai.sdk'])
        baseline=root/'baseline-source/package';baseline.mkdir(parents=True)
        with tarfile.open(fileobj=io.BytesIO(archive)) as tar:
            for entry in tar:
                prefix='packages/com.spatialai.sdk/'
                if not entry.isfile() or not entry.name.startswith(prefix):continue
                path=baseline/entry.name[len(prefix):]
                if not path.resolve().is_relative_to(baseline.resolve()):raise ValueError('Unsafe baseline archive path')
                path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(tar.extractfile(entry).read())
        baseline_manifest=json.loads((baseline/'package.json').read_text(encoding='utf-8-sig'))
        if baseline_manifest['version']!='0.2.0-preview.9':raise ValueError('Update the upgrade test baseline for this source revision')
        # Keep the available Android bridge binary; this baseline only tests serialized Core/Host assets, not old binary behavior.
        source=ROOT/'packages/com.spatialai.sdk/Plugins/Android/spatial-hand-unity.aar';shutil.copy2(source,baseline/'Plugins/Android/spatial-hand-unity.aar')
        spec=importlib.util.spec_from_file_location('pack',ROOT/'tools/PackUnityRelease.py');pack=importlib.util.module_from_spec(spec);spec.loader.exec_module(pack)
        pack.archive(baseline,root/'SDKRelease/com.spatialai.sdk-baseline.tgz')
        deps['com.spatialai.sdk']='file:../../SDKRelease/com.spatialai.sdk-baseline.tgz'
        shutil.copy2(ROOT/'tests/XRHandsStage5/Stage5UpgradeBaseline.cs',project/'Assets/Editor/Stage5UpgradeBaseline.cs')
    else:
        shutil.copy2(ROOT/'tests/XRHandsStage5/Stage5DisplayVerification.cs',project/'Assets/Editor/Stage5DisplayVerification.cs')
        if mode=='full':
            shutil.copy2(ROOT/'tests/XRHandsStage5/Stage5Verification.cs',project/'Assets/Editor/Stage5Verification.cs')
            shutil.copy2(ROOT/'tests/XRHandsStage5/Stage5PlayProbe.cs',project/'Assets/Stage5PlayProbe.cs')
    (project/'Packages/manifest.json').write_text(json.dumps({'dependencies':deps},indent=2))
(ROOT/'build/stage5-validation-path.txt').write_text(str(root));print(root)
