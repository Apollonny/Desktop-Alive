"""Exercise verifier rejection with temporary copies, preserving the captured evidence."""
from pathlib import Path
from tempfile import TemporaryDirectory
import json,shutil,subprocess,sys
root=Path(__file__).resolve().parents[1]
source=root/'Artifacts/rig-audit'
with TemporaryDirectory(prefix='rig-reject-',dir=root/'Artifacts') as temp:
    folder=Path(temp)
    probes=[{'file':name,'parameter':'','value':0,'facing':1} for name in ('probe-000.png','probe-066.png')]
    (folder/'manifest.json').write_text(json.dumps({'probes':probes}))
    for probe in probes:
        shutil.copy2(source/'probe-000.png',folder/probe['file'])
        shutil.copy2(source/'probe-000.geometry.json',folder/Path(probe['file']).with_suffix('.geometry.json'))
    geometry=json.loads((source/'probe-000.geometry.json').read_text())
    for field,value,reason in [('vertexError',1,'native/Unity vertex mismatch'),('enabled',not geometry['drawables'][0]['visible'],'visibility mismatch')]:
        data=json.loads(json.dumps(geometry));data['drawables'][0][field]=value
        (folder/'probe-000.geometry.json').write_text(json.dumps(data))
        result=subprocess.run([sys.executable,str(root/'Tools/verify_rig_render.py'),folder.name],capture_output=True,text=True)
        assert result.returncode!=0 and reason in result.stderr,(field,result.stdout,result.stderr)
    (folder/'probe-000.geometry.json').write_text(json.dumps(geometry))
    shutil.copy2(source/'probe-029.png',folder/'probe-066.png')
    result=subprocess.run([sys.executable,str(root/'Tools/verify_rig_render.py'),folder.name],capture_output=True,text=True)
    assert result.returncode!=0 and 'Neutral restoration differs' in result.stderr,result.stderr
print('RIG_VERIFIER_REJECTION_CHECKS passed=3')
