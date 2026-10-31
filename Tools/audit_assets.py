from pathlib import Path
import hashlib,json
root=Path(__file__).resolve().parents[1]
art=root/'MikaUnity/Assets/StreamingAssets/Mika'
model=json.loads((art/'165 218.model3.json').read_text(encoding='utf-8'))
refs=model['FileReferences']
paths=[refs['Moc'],refs['Physics'],refs['DisplayInfo'],*refs['Textures']]
paths += [v['File'] for group in refs['Motions'].values() for v in group]
paths += [v['File'] for v in refs['Expressions']]
audit={}
for name in paths:
    copy=(art/name).resolve()
    assert copy.is_relative_to(art.resolve()) and copy.is_file(),name
    original=root/name
    assert original.read_bytes()==copy.read_bytes(),f'Original/copy mismatch: {name}'
    audit[name]=hashlib.sha256(original.read_bytes()).hexdigest()
audit['SDK-5-r.4.2']=hashlib.sha256((root/'Artifacts/CubismSdkForUnity-5-r.4.2.unitypackage').read_bytes()).hexdigest()
(root/'Artifacts/asset-audit.json').write_text(json.dumps(audit,indent=2))
print(f'ASSET_AUDIT passed: {len(paths)} references; source art matches copied art byte for byte.')
