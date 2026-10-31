from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,shutil,zipfile
root=Path(__file__).resolve().parents[1]
build=root/'Builds/Windows'
assert (build/'Mika.exe').is_file(),'Build the Windows player first.'
assert 'MIKA_BUILD Succeeded errors=0' in (root/'Artifacts/unity-build.log').read_text(encoding='utf-8-sig'), 'Latest build did not succeed.'
shutil.copy2(root/'README.md',build/'README.md')
shutil.copytree(root/'Docs',build/'Docs',dirs_exist_ok=True)
(build/'Open Animation Studio.cmd').write_text('@echo off\nstart "Mika Animation Studio" "%~dp0Mika.exe" --mika-animation-lab\n',encoding='ascii')
licenses=build/'ThirdPartyNotices';licenses.mkdir(exist_ok=True)
for source,name in [
    ('MikaUnity/Assets/Live2D/Cubism/LICENSE.md','Live2D-Components-LICENSE.md'),
    ('MikaUnity/Assets/Live2D/Cubism/NOTICE.md','Live2D-NOTICE.md'),
    ('MikaUnity/Assets/Live2D/Cubism/Plugins/LICENSE.md','Live2D-Core-LICENSE.md'),
    ('.tools/dotnet/LICENSE.txt','DotNet-LICENSE.txt'),
    ('.tools/dotnet/ThirdPartyNotices.txt','DotNet-ThirdPartyNotices.txt')]:
    if (root/source).exists():shutil.copy2(root/source,licenses/name)
info={'product':'MIKA LIVES HERE','status':'Development build; authored collar rig required',
      'unity':'6000.6.0f1','cubism':'BiRP 5-r.4.2','builtAtUtc':datetime.now(timezone.utc).isoformat(),
      'playerSha256':hashlib.sha256((build/'Mika.exe').read_bytes()).hexdigest()}
(build/'BUILD-INFO.json').write_text(json.dumps(info,indent=2),encoding='utf-8')
target=root/'Builds/MIKA-LIVES-HERE-Windows-Development.zip'
with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=3) as archive:
    for source in sorted(build.rglob('*')):
        if source.is_file():archive.write(source,Path('MIKA LIVES HERE')/source.relative_to(build))
print(f'PACKAGE_READY {target} ({target.stat().st_size/1048576:.1f} MiB)')
