"""Build a portable, deterministic Unity release bundle from prepared artifacts."""
import argparse, gzip, hashlib, io, json, shutil, subprocess, tarfile, uuid, zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PACKAGES = ['com.spatialai.sdk', 'com.spatialai.xr-hands', 'com.spatialai.xr-interaction']
EXCLUDED = {'.git', '.github', '.allstar', 'Library', 'Temp', 'Logs', 'UserSettings', '__pycache__'}

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def git(root, *args):
    return subprocess.check_output(['git', '-C', str(root), *args], text=True, encoding='utf-8').strip()

def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')

def prepare_meta(root, name):
    for path in sorted(root.rglob('*')):
        if path.suffix == '.meta' or any(x in EXCLUDED for x in path.relative_to(root).parts):
            continue
        meta = Path(str(path)+'.meta')
        if meta.exists():
            continue
        guid = hashlib.sha256((name+'/'+path.relative_to(root).as_posix()).encode()).hexdigest()[:32]
        kind = {'.cs':'MonoImporter','.asmdef':'AssemblyDefinitionImporter','.shader':'ShaderImporter','.prefab':'PrefabImporter'}.get(path.suffix,'DefaultImporter')
        if path.name == 'package.json': kind = 'PackageManifestImporter'
        text = 'fileFormatVersion: 2\nguid: '+guid+'\n'
        if path.is_dir(): text += 'folderAsset: yes\n'
        meta.write_text(text+kind+':\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8')

def archive(source, target):
    files = []
    with target.open('wb') as raw, gzip.GzipFile(fileobj=raw, mode='wb', mtime=0, filename='') as gz, tarfile.open(fileobj=gz, mode='w', format=tarfile.PAX_FORMAT) as tar:
        for path in [source]+sorted(source.rglob('*')):
            if any(x in EXCLUDED for x in path.relative_to(source).parts): continue
            if path.is_symlink(): raise ValueError('Release may not contain symlinks: '+str(path))
            name = 'package' if path == source else 'package/'+path.relative_to(source).as_posix()
            info = tarfile.TarInfo(name); info.mtime=0; info.mode=0o755 if path.is_dir() else 0o644
            if path.is_dir(): info.type=tarfile.DIRTYPE; tar.addfile(info)
            else:
                data=path.read_bytes(); info.size=len(data); tar.addfile(info,io.BytesIO(data))
                files.append({'path':name,'sha256':hashlib.sha256(data).hexdigest()})
    return files

def sdk_bundle(destination, releases, version, stage):
    source=ROOT/'packages/com.spatialai.sdk'
    manifest=json.loads((source/'package.json').read_text(encoding='utf-8-sig'))
    expected={'com.spatialai.xr-hands','com.spatialai.xr-interaction','com.google.xr.cardboard'}
    if manifest['name']!='com.spatialai.sdk' or manifest['version']!=version or set(p['name'] for p in releases)!=expected:
        raise ValueError('The SDK payload must contain exactly the three companion packages')
    if expected.intersection(manifest.get('dependencies',{})):
        raise ValueError('Local companion dependencies must be installed after the SDK compiles')
    prepare_meta(source,manifest['name'])
    installer=stage/'sdk'
    shutil.copytree(source,installer)
    payload=installer/'Payload~';payload.mkdir()
    for package in releases:
        archive_path=destination/package['file']
        if sha(archive_path)!=package['sha256']:raise ValueError('Installer payload checksum mismatch: '+package['name'])
        shutil.copy2(archive_path,payload/package['file'])
    write_json(payload/'install-manifest.json',{'version':version,'packages':[{k:p[k] for k in ('name','version','file','sha256')} for p in releases]})
    documentation=installer/'Documentation~';documentation.mkdir(exist_ok=True)
    shutil.copy2(ROOT/'docs/SDK_INSTALL.md',documentation/'INSTALL.md')
    shutil.copy2(ROOT/'docs/THIRD_PARTY_NOTICES.md',documentation/'THIRD_PARTY_NOTICES.md')
    prepare_meta(installer,manifest['name'])
    filename=manifest['name']+'-'+version+'.tgz'
    checksums=archive(installer,destination/filename)
    return checksums, {'name':manifest['name'],'version':version,'file':filename,'sha256':sha(destination/filename),'dependencies':manifest['dependencies']}

def main():
    parser=argparse.ArgumentParser(); parser.add_argument('--prepare-meta', action='store_true'); parser.add_argument('--output'); parser.add_argument('--replace-local-candidate', action='store_true')
    args=parser.parse_args()
    for name in PACKAGES: prepare_meta(ROOT/'packages'/name,name)
    if args.prepare_meta: return
    manifests={name:json.loads((ROOT/'packages'/name/'package.json').read_text(encoding='utf-8-sig')) for name in PACKAGES}
    for name,m in manifests.items():
        if m['name'] != name: raise ValueError('Package name mismatch')
        for dep,version in m.get('dependencies',{}).items():
            if dep in manifests and manifests[dep]['version'] != version: raise ValueError('Package dependency version mismatch: '+dep)
    hand_lock=json.loads((ROOT/'third_party/spatial-ai-android.lock.json').read_text(encoding='utf-8-sig'))
    hand=ROOT/hand_lock['artifact']
    if sha(hand) != hand_lock['sha256']: raise ValueError('Hand AAR differs from the source lock')
    with zipfile.ZipFile(hand) as z:
        for filename,key in [('hand_landmarker.task','modelSha256'),('gesture_recognizer.task','gestureModelSha256')]:
            if hashlib.sha256(z.read('assets/'+filename)).hexdigest() != hand_lock[key]: raise ValueError('Model checksum mismatch: '+filename)
    android=ROOT.parent/'spatial-ai-android'
    if git(android,'rev-parse','HEAD') != hand_lock['revision']: raise ValueError('Android base revision differs from lock')
    for entry in hand_lock.get('sourceFiles',[]):
        if sha(android/entry['path']) != entry['sha256']: raise ValueError('Android source has changed since AAR build: '+entry['path'])
    cardboard=ROOT/'third_party/checkouts/cardboard-xr-plugin-unity2022'
    native=ROOT/'build/native/cardboard-rhinox-v1301/libGfxPluginCardboard.so'
    patch=ROOT/'third_party/rhinox/cardboard-display-api.patch'
    if sha(patch) != sha(ROOT/'build/native/cardboard-rhinox-v1301/applied-cardboard.patch'): raise ValueError('Native build uses an older patch; rebuild Cardboard')
    if sha(cardboard/'Runtime/Android/GfxPluginCardboard.aar') != sha(ROOT/'build/native/cardboard-rhinox-v1301/GfxPluginCardboard-rhinox.aar'): raise ValueError('Cardboard installed AAR differs from prepared build')
    with zipfile.ZipFile(cardboard/'Runtime/Android/GfxPluginCardboard.aar') as z:
        binary=z.read('jni/arm64-v8a/libGfxPluginCardboard.so')
        if hashlib.sha256(binary).hexdigest() != sha(native): raise ValueError('Cardboard native library checksum mismatch')
        for name in ['CardboardUnity_configureHostTracking','CardboardUnity_configureSpatialEyeRects','CardboardUnity_setHostViewCompensation']:
            if name.encode() not in binary: raise ValueError('Missing patched native API: '+name)
    version=manifests[PACKAGES[0]]['version']
    destination=Path(args.output).resolve() if args.output else ROOT/'dist'/version/'unity'
    if destination.exists():
        previous=destination/'release-manifest.json'
        if not args.replace_local_candidate or not previous.exists() or json.loads(previous.read_text(encoding='utf-8'))['status'] != 'local-candidate-not-published':
            raise ValueError('Release directory exists; use a new version or explicitly replace this local candidate')
        # Only remove known files from our own manifest, never recursively delete a computed output path.
        old=json.loads(previous.read_text(encoding='utf-8'))
        names=[x['file'] for x in old['packages']]+['release-manifest.json','SHA256SUMS.txt','INSTALL.md','THIRD_PARTY_NOTICES.md','STAGE5_VALIDATION.md','source-file-checksums.json','android-source.patch','cardboard-spatial.patch','rhinox-mesh-reference.cc']
        if old.get('installer'): names.append(old['installer']['file'])
        for name in names:
            if Path(name).name != name: raise ValueError('Invalid previous manifest filename')
            (destination/name).unlink(missing_ok=True)
    destination.mkdir(parents=True,exist_ok=True)
    stage=ROOT/'build/staging/unity'/('release-'+uuid.uuid4().hex); stage.mkdir(parents=True)
    sources={}; releases=[]
    # Build companions first; the SDK embeds those archives, never itself.
    for name in PACKAGES[1:]:
        package=ROOT/'packages'/name
        filename=name+'-'+manifests[name]['version']+'.tgz'; sources[name]=archive(package,destination/filename)
        releases.append({'name':name,'version':manifests[name]['version'],'file':filename,'sha256':sha(destination/filename),'dependencies':manifests[name]['dependencies']})
    plugin_stage=stage/'cardboard'; shutil.copytree(cardboard,plugin_stage,ignore=shutil.ignore_patterns(*EXCLUDED))
    m=json.loads((plugin_stage/'package.json').read_text(encoding='utf-8-sig'))
    m['dependencies'].update({'com.unity.xr.management':'4.4.0','com.unity.ugui':'1.0.0'})
    write_json(plugin_stage/'package.json',m)
    cardboard_info={'upstreamRevision':git(cardboard,'rev-parse','HEAD'),'upstreamVersion':m['version'],'modifiedForSpatialAI':True,'nativeSourceRevision':git(ROOT/'third_party/checkouts/cardboard-sdk-v1.30.1','rev-parse','HEAD'),'patchSha256':sha(patch),'nativeSha256':sha(native),'aarSha256':sha(cardboard/'Runtime/Android/GfxPluginCardboard.aar'),'changes':['Spatial native display and IMU patch','Unity 2022 package dependency pins (XR Management 4.4.0 and uGUI 1.0.0)']}
    write_json(plugin_stage/'SpatialRelease.json',cardboard_info); prepare_meta(plugin_stage,m['name'])
    filename=m['name']+'-'+m['version']+'-spatial.tgz'; sources[m['name']]=archive(plugin_stage,destination/filename)
    releases.append({'name':m['name'],'version':m['version'],'file':filename,'sha256':sha(destination/filename),'dependencies':m['dependencies']})
    sources[PACKAGES[0]],sdk_release=sdk_bundle(destination,releases,version,stage)
    releases.insert(0,sdk_release)
    write_json(destination/'source-file-checksums.json',sources)
    shutil.copy2(ROOT/'docs/SDK_INSTALL.md',destination/'INSTALL.md')
    shutil.copy2(ROOT/'docs/THIRD_PARTY_NOTICES.md',destination/'THIRD_PARTY_NOTICES.md')
    (destination/'STAGE5_VALIDATION.md').write_text((ROOT/'docs/UNITY_XR_HANDS_STAGE5.md').read_text(encoding='utf-8').replace('](SDK_INSTALL.md)','](INSTALL.md)'),encoding='utf-8')
    (destination/'android-source.patch').write_text(git(android,'diff','HEAD','--','runtime/platform/android/hands/SpatialHandBridge.java')+'\n',encoding='utf-8')
    shutil.copy2(patch,destination/'cardboard-spatial.patch')
    shutil.copy2(ROOT/'third_party/rhinox/spatial_rhinox_mesh_data.cc',destination/'rhinox-mesh-reference.cc')
    report={'schemaVersion':2,'version':version,'status':'local-candidate-not-published','packages':releases,'sdkSource':{'baseRevision':git(ROOT,'rev-parse','HEAD'),'workingTreeModified':bool(git(ROOT,'status','--porcelain')),'fileChecksums':'source-file-checksums.json'},'android':hand_lock,'cardboard':cardboard_info,'sourceSnapshots':{'androidWorkingPatch':'android-source.patch','cardboardPatch':'cardboard-spatial.patch','meshReference':'rhinox-mesh-reference.cc'},'compatibility':{'unity':'2022.3.14f1','renderPipeline':'Built-in','androidMinApi':28,'compileSdk':34,'buildTools':'30.0.3','architecture':'ARM64','inferenceDefault':'CPU/VIDEO','hardware':'Xiaomi 2206123SC + HXQ 11cb:281b (development preview)','notCertified':['Unity 6','Tuanjie','iOS','other devices','optical and metric hand accuracy']}}
    report['installation']={'entryPackage':sdk_release['name'],'entryFile':sdk_release['file'],'automaticPackages':[p['name'] for p in releases[1:]]}
    write_json(destination/'release-manifest.json',report)
    sums=''.join(sha(p)+'  '+p.name+'\n' for p in sorted(destination.iterdir()) if p.is_file() and p.name != 'SHA256SUMS.txt')
    (destination/'SHA256SUMS.txt').write_text(sums,encoding='utf-8')
    bundle_path=(ROOT/'dist' if not args.output else destination.parent)/('spatialai-unity-'+version+'.zip')
    with zipfile.ZipFile(bundle_path,'w',compression=zipfile.ZIP_DEFLATED) as bundle:
        for path in sorted(destination.iterdir()):
            if not path.is_file():continue
            info=zipfile.ZipInfo('unity/'+path.name,date_time=(1980,1,1,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED;info.external_attr=0o100644<<16
            bundle.writestr(info,path.read_bytes())
    print(destination)

if __name__=='__main__': main()
