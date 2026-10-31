from pathlib import Path
import zipfile
root=Path(__file__).resolve().parents[1]
with zipfile.ZipFile(root/'Builds/MIKA-LIVES-HERE-Windows-Development.zip') as z:
    assert z.testzip() is None,'Archive CRC failure'
    names=z.namelist()
    for suffix in ['/Mika.exe','/UnityPlayer.dll','/Mika.MediaHost.exe','/Live2DCubismCore.dll','/165 218.moc3','/BUILD-INFO.json','/Open Animation Studio.cmd','/animation-profile.json','/authored/recover.motion3.json','/authored/grabbed-leg-swing-draft.motion3.json','/Docs/GRAB_SWAY_DRAFT.md']:
        assert any(n.endswith(suffix) for n in names),f'Missing {suffix}'
    print(f'PACKAGE_VERIFIED: {len(names)} files; CRC and required Windows/Live2D/media payloads passed.')
