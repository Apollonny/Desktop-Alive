"""Import the pinned official Unity package and copy original art without modifying it."""
from pathlib import Path
import tarfile, shutil, json
ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / 'MikaUnity'
with tarfile.open(ROOT / 'Artifacts/CubismSdkForUnity-5-r.4.2.unitypackage', 'r:gz') as archive:
    entries = {m.name: m for m in archive.getmembers()}
    for name, member in entries.items():
        if not name.endswith('/pathname'): continue
        relative = archive.extractfile(member).read().decode().strip()
        if not relative.startswith('Assets/Live2D/'): continue
        if '/Samples' in relative: continue
        destination = (PROJECT / relative).resolve()
        if not destination.is_relative_to(PROJECT.resolve()): raise ValueError(relative)
        stem = name.rsplit('/', 1)[0]
        for source, target in [(stem + '/asset', destination), (stem + '/asset.meta', Path(str(destination)+'.meta'))]:
            if source in entries and entries[source].isfile():
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(archive.extractfile(entries[source]).read())
art = PROJECT / 'Assets/StreamingAssets/Mika'
art.mkdir(parents=True, exist_ok=True)
for source in ROOT.iterdir():
    if source.suffix in ('.moc3', '.json') and '.vtube.' not in source.name and source.name.startswith('165 '):
        shutil.copy2(source, art/source.name)
for folder in ['165 218.4096', 'motion', 'exp']:
    shutil.copytree(ROOT/folder, art/folder, dirs_exist_ok=True)
model_path = art/'165 218.model3.json'
model = json.loads(model_path.read_text())
model['FileReferences']['Motions'] = {p.stem.split('.')[0]: [{'File': 'motion/'+p.name}] for p in (art/'motion').glob('*.json')}
model['FileReferences']['Expressions'] = [{'Name':p.stem.split('.')[0], 'File':'exp/'+p.name} for p in (art/'exp').glob('*.json')]
model_path.write_text(json.dumps(model, indent=2), encoding='utf-8')
print('Imported Cubism BiRP 5-r.4.2 and copied Mika assets.')
for path in (PROJECT/'Assets/Live2D/Cubism').rglob('*.cs'):
    text=path.read_text(encoding='utf-8-sig')
    if 'GetInstanceID()' not in text: continue
    text=text.replace('AssetDatabase.GetAssetPath(activeObject.GetInstanceID())','AssetDatabase.GetAssetPath(activeObject)')
    text=text.replace('animationClip.GetInstanceID()','animationClip.GetEntityId().GetHashCode()')
    path.write_text(text,encoding='utf-8')
print('Applied Unity 6000.6 editor identifier compatibility patch.')
