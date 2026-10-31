
import json
import numpy as np
from PIL import Image
from families.flora import contracts, TREES

def validate_flora(root,manifest,check):
    entries={e['id']:e for e in manifest['assets'] if e['family']=='flora'}
    expected=contracts()
    check(len(expected)==72 and len({c[1] for c in expected})==23,'Flora specification inventory count')
    check(set(entries)=={f'flora.{c[0]}' for c in expected},'Flora state coverage mismatch')
    palette=json.loads((root/'assets/palette/palette.json').read_text())
    cloth=np.array(Image.open(root/'assets/palette/lut_cloth.png'))
    slots={s['name']:s for s in palette['slots']}
    for shade in range(4):
        name=f'fruit_{shade}'
        check(name in slots,f'Missing fruit palette slot: {name}')
        if name not in slots: continue
        check(slots[name]['reference']==palette['ramps']['cloth_0'][shade],f'Fruit reference tint: {name}')
        for dye in range(13):
            check(np.array_equal(cloth[dye,slots[name]['index'],:3],palette['ramps'][f'cloth_{dye}'][shade]),f'Fruit dye lookup: {name}:{dye}')
    for name,species,state,size,count,frames in expected:
        ident=f'flora.{name}'
        if ident not in entries: continue
        e=entries[ident]; w,h=size
        check(e['frame']==size and e['frames']==frames and len(e['sheet_rows'])==count,f'Flora dimensions/counts: {name}')
        check(e['asset_id']==species and e['state']==state,f'Flora simulation ID/state: {name}')
        check(e['pivot']==[w//2,h-2],f'Flora base pivot: {name}')
        check(e['directions']==['none'] and e['shadow']=='engine',f'Flora rendering metadata: {name}')
        for target in e.get('seasonal_states',{}).values():
            check(target in entries and entries[target]['frame']==size,f'Invalid seasonal state target: {name}')
        check(len(e['files'])==2 and e['files'][1].endswith('_idx.png'),f'Flora missing index map: {name}')
        a=np.array(Image.open(root/'assets'/e['files'][0]))
        rows=[]
        for v in range(count):
            row=[]
            for f in range(frames):
                tile=a[v*h:(v+1)*h,f*w:(f+1)*w]; row.append(tile)
                alpha=tile[:,:,3]>0
                check(np.count_nonzero(alpha)>=3,f'Empty or unreadable flora: {name}:{v}:{f}')
                check(not (alpha[0].any() or alpha[-1].any() or alpha[:,0].any() or alpha[:,-1].any()),f'Clipped flora silhouette: {name}:{v}:{f}')
                check(len(np.unique(tile[alpha,:3],axis=0))<=16,f'Flora colour limit: {name}:{v}:{f}')
                check(not bool(np.any(np.all(tile[alpha,:3]==0,axis=1))),f'Black flora pixel: {name}')
                if state!='snow_capped': check(alpha[h-2].any(),f'Floating flora base: {name}:{v}:{f}')
            if frames>1:
                check(len({t.tobytes() for t in row})==frames,f'Duplicate sway frames: {name}:{v}')
                check(all(np.array_equal(t[-2:],row[0][-2:]) for t in row),f'Sway moves plant base: {name}:{v}')
            rows.append(b''.join(t.tobytes() for t in row))
        check(len(set(rows))==count,f'Duplicate flora variants: {name}')
        if species in TREES and state not in {'bare','old_bare','burned','dead'}:
            check(e['wind_sway']['mode']=='shader' and e['wind_sway']['max_displacement_px']==1,f'Missing tree wind metadata: {name}')
        if state in {'bare','old_bare','burned','dead'}:
            check(not any(s.startswith('foliage_') for s in e['recolor_slots']),f'Leafy bare/burned tree: {name}')
        if state=='snow_capped':
            mature=entries['flora.conifer_mature']
            base=np.array(Image.open(root/'assets'/mature['files'][0]))
            check(bool(np.all((a[:,:,3]==0)|(base[:,:,3]>0))),'Snow cap extends beyond matching canopy')
        if species=='berry_bush' and state=='fruiting':
            check(any(s.startswith('fruit_') for s in e['recolor_slots']),'Missing fruit tint slots')
        check((root/f'assets/_preview/flora_{species}.png').exists(),f'Missing flora preview: {species}')
