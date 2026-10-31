
import json
import numpy as np
from PIL import Image
from families.structures import SPEC, STATES, LIT

def validate_structures(root,manifest,check):
    entries={e['id']:e for e in manifest['assets'] if e['family']=='structures'}
    expected={f'structures.{kind}_{state}' for kind in SPEC for state in STATES+(['lit'] if kind in LIT else [])}
    check(len(SPEC)==18 and len(entries)==93 and set(entries)==expected,'Early structure inventory mismatch')
    palette=json.loads((root/'assets/palette/palette.json').read_text())
    slots={s['name']:s['index'] for s in palette['slots']}
    for kind,(size,materials) in SPEC.items():
        w,h=size; conditions=[]
        for state in STATES+(['lit'] if kind in LIT else []):
            key=f'structures.{kind}_{state}'
            if key not in entries: continue
            e=entries[key]; frames=4 if state=='lit' else 1
            check(e['frame']==size and e['frames']==frames and e['directions']==['none'],f'Structure size/frame contract: {key}')
            check(e['asset_id']==kind and e['state']==('complete' if state=='lit' else state),f'Structure ID/state: {key}')
            check(e['lighting']==('lit' if state=='lit' else 'unlit'),f'Structure lighting state: {key}')
            check(e['materials']==materials and e['pivot']==[w//2,h-2] and e['shadow']=='engine',f'Structure material/pivot contract: {key}')
            a=np.array(Image.open(root/'assets'/e['files'][0])); tiles=[]
            for f in range(frames):
                tile=a[:,f*w:(f+1)*w]; alpha=tile[:,:,3]>0
                check(np.count_nonzero(alpha)>=5,f'Empty structure: {key}:{f}')
                check(not(alpha[0].any() or alpha[-1].any() or alpha[:,0].any() or alpha[:,-1].any()),f'Clipped structure: {key}:{f}')
                check(alpha[-2].any(),f'Floating structure footprint: {key}:{f}')
                check(len(np.unique(tile[alpha,:3],axis=0))<=16,f'Structure colour budget: {key}:{f}')
                check(not np.any(np.all(tile[alpha,:3]==0,axis=1)),f'Black structure pixel: {key}')
                tiles.append(tile.tobytes())
            check(len(set(tiles))==frames,f'Duplicate structure animation frames: {key}')
            if state!='lit': conditions.append(tiles[0])
            else:
                check(bool(e['emissive_slots']) and e['fps']==6,f'Missing structure emissive/timing metadata: {key}')
                if kind in {'kiln','smelter'}:
                    reference=entries[f'structures.{kind}_complete']
                    base=np.array(Image.open(root/'assets'/reference['files'][1]))
                    idx=np.array(Image.open(root/'assets'/e['files'][1]))
                    x0,y0,x1,y1=e['fire_window']
                    allowed=np.zeros((h,w),bool); allowed[y0:y1+1,x0:x1+1]=True
                    allowed &= base[:,:,0]==slots['fixed_clay_0' if kind=='kiln' else 'fixed_mudbrick_0']
                    for f in range(frames):
                        tile=idx[:,f*w:(f+1)*w]
                        check(np.array_equal(tile[~allowed],base[~allowed]),f'Flame escapes furnace opening: {key}:{f}')
                        check(np.array_equal(tile[:,:,3],base[:,:,3]),f'Flame changes furnace silhouette: {key}:{f}')
            if kind=='totem' and state=='complete':
                check(any(s.startswith('culture_') for s in e['recolor_slots']),'Missing totem culture slots')
                check(e['glyph_anchor']['glyph_generated'] is False,'Early structures must not generate culture glyphs')
        check(len(set(conditions))==5,f'Duplicate structure conditions: {kind}')
        check((root/f'assets/_preview/structures_{kind}.png').exists(),f'Missing structure contact sheet: {kind}')
