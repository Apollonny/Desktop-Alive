from pathlib import Path
import json,sys
root=Path(__file__).resolve().parents[1]
folder=root/'Artifacts'/sys.argv[1]
baseline={r['id']:r for r in json.loads((folder/'probe-000.geometry.json').read_text())['drawables']}
for index in [0,16,17,18,22,24,29,34,66]:
    rows=json.loads((folder/f'probe-{index:03}.geometry.json').read_text())['drawables']
    summary={'probe':index,'visible':sum(r['visible'] for r in rows),'enabled':sum(r['enabled'] for r in rows),
             'visibilityMismatch':sum(r['visible']!=r['enabled'] for r in rows),'vertexMismatch':sum(r['vertexError']>.0001 for r in rows),
             'maxVertexError':max(r['vertexError'] for r in rows),'opacityChanged':[r['id'] for r in rows if abs(r['opacity']-baseline[r['id']]['opacity'])>.01],
             'maxZ':max(r['z'] for r in rows),'minZ':min(r['z'] for r in rows)}
    print(json.dumps(summary))
