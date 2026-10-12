"""Prepare a clean Unity consumer importing only the SDK tarball."""
import argparse, hashlib, json, shutil, tarfile, uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

def main():
    parser=argparse.ArgumentParser();parser.add_argument('release');parser.add_argument('--legacy-installer');args=parser.parse_args()
    release=Path(args.release).resolve()
    manifest=json.loads((release/'release-manifest.json').read_text(encoding='utf-8'))
    installer=next(p for p in manifest['packages'] if p['name']==manifest['installation']['entryPackage'])
    for line in (release/'SHA256SUMS.txt').read_text().splitlines():
        digest,name=line.split('  ',1)
        if Path(name).name!=name or hashlib.sha256((release/name).read_bytes()).hexdigest()!=digest:
            raise ValueError('Release checksum mismatch: '+name)
    with tarfile.open(release/installer['file'],'r:gz') as tar:
        package=json.loads(tar.extractfile('package/package.json').read().decode('utf-8-sig'))
        companions={'com.google.xr.cardboard','com.spatialai.xr-hands','com.spatialai.xr-interaction'}
        if companions.intersection(package['dependencies']):raise ValueError('Entry package may not depend on unregistered local packages')
        plan=json.loads(tar.extractfile('package/Payload~/install-manifest.json').read().decode('utf-8'))
        if len(plan['packages'])!=3 or {p['name'] for p in plan['packages']}!=companions:raise ValueError('Expected three companion payload packages')
        for payload in plan['packages']:
            data=tar.extractfile('package/Payload~/'+payload['file']).read()
            if hashlib.sha256(data).hexdigest()!=payload['sha256'] or data!=(release/payload['file']).read_bytes():
                raise ValueError('Payload differs from the matching release: '+payload['name'])
    root=ROOT/'build/validation'/('upm-installer-'+uuid.uuid4().hex)
    project=root/'consumer'
    for directory in ('Assets/Editor','Packages','ProjectSettings','IncomingSDK'):
        (project/directory).mkdir(parents=True,exist_ok=True)
    shutil.copy2(release/installer['file'],project/'IncomingSDK/sdk.tgz')
    if args.legacy_installer:
        shutil.copy2(Path(args.legacy_installer).resolve(),project/'IncomingSDK/legacy-installer.tgz')
    shutil.copy2(ROOT/'tests/UnityInstaller/InstallerVerification.cs',project/'Assets/Editor/InstallerVerification.cs')
    (project/'Assets/KeepMe.txt').write_text('preserved application asset')
    (project/'Packages/manifest.json').write_text(json.dumps({'dependencies':{'com.unity.modules.physics':'1.0.0'}},indent=2))
    (project/'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 2022.3.14f1\n')
    (ROOT/'build/installer-validation-path.txt').write_text(str(root))
    print(root)

if __name__=='__main__': main()
