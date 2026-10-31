
import json
import numpy as np
from PIL import Image
from families.people import ANIMS, PARTS, ORDER, profiles
from people_preview import Assembly

def validate_people(root,manifest,check):
    entries={e['id']:e for e in manifest['assets'] if e['family']=='people'}
    rig=json.loads((root/'assets/people/rig.json').read_text())
    expected={f'people.{layer}_{anim}' for layer in ORDER for anim in ANIMS}
    check(len(entries)==140 and set(entries)==expected,'People layer/animation inventory')
    check(rig['layer_order']==ORDER,'People layer order')
    check(len(rig['hair_styles'])==10 and rig['hair_styles']['shaved']=={'front':None,'back':None},'People hair style coverage')
    assembly=Assembly(root)
    for anim,(frames,fps,actions) in ANIMS.items():
        rows=profiles(anim)
        check(rig['animations'][anim]['profiles']==[list(r) for r in rows],f'People rig profiles: {anim}')
        for layer in ORDER:
            key=f'people.{layer}_{anim}'
            if key not in entries: continue
            e=entries[key]
            check(e['frame']==[16,16] and e['frames']==frames and e['fps']==fps,f'People frame contract: {key}')
            check(e['sim_actions']==actions,f'People simulation action IDs: {key}')
            check(e['variants']==PARTS[layer] and len(e['row_lookup'])==len(PARTS[layer]),f'People part row inventory: {key}')
            check(all(len(row)==len(rows) for row in e['row_lookup']),f'People row lookup dimensions: {key}')
            check({i for row in e['row_lookup'] for i in row}==set(range(len(e['sheet_rows']))),f'People unused or invalid physical rows: {key}')
            check(e['profiles']==[list(p) for p in rows] and e['pose_row_count']==len(rows),f'People row ordering: {key}')
            image=assembly.images[key]
            physical=[image[r*16:(r+1)*16].tobytes() for r in range(len(e['sheet_rows']))]
            check(len(set(physical))==len(physical),f'Duplicate physical people rows: {key}')
            for r,profile in enumerate(rows):
                poses=rig['animations'][anim]['poses'][r]
                check(e['row_pivots'][r]==poses[0]['pivot'],f'People pivot/rig mismatch: {key}:{r}')
                for f,p in enumerate(poses):
                    x,y=e['hand_anchors'][r][f]
                    check([x,y]==p['hands'][1] and 0<=x<16 and 0<=y<16,f'People hand anchor: {key}:{r}:{f}')
                for v,variant in enumerate(PARTS[layer]):
                    row=e['row_lookup'][v][r]
                    for f in range(frames):
                        tile=image[row*16:(row+1)*16,f*16:(f+1)*16]; alpha=tile[:,:,3]>0
                        check(not (alpha[0].any() or alpha[-1].any() or alpha[:,0].any() or alpha[:,-1].any()),f'Clipped people layer: {key}:{row}:{f}')
                        check(len(np.unique(tile[alpha,0]))<=16,f'People layer colour limit: {key}')
                        if layer in {'body','clothing','held_item'}: check(np.any(alpha),f'Empty required people layer: {key}:{row}:{f}')
                        if layer=='held_item':
                            x,y=e['hand_anchors'][r][f]
                            check(tile[y,x,3]==255,f'Held item misses grip: {key}:{row}:{f}')
        for r,profile in enumerate(rows):
            tiles=[np.array(assembly.compose(anim,profile,f)) for f in range(frames)]
            check(len({tile.tobytes() for tile in tiles})==frames,f'Duplicate assembled animation frames: {anim}:{profile}')
            for tile in tiles:
                alpha=tile[:,:,3]>0
                check(np.count_nonzero(alpha)>=10,f'Unreadable assembled person: {anim}:{profile}')
                check(not np.any(np.all(tile[alpha,:3]==0,axis=1)),f'Black person pixel: {anim}:{profile}')
                check(len(np.unique(tile[alpha,:3],axis=0))<=16,f'Assembled person colour budget: {anim}:{profile}')
        check((root/f'assets/_preview/people_anim_{anim}.png').exists(),f'Missing people animation preview: {anim}')
    for age,height in [('child',8),('adult',11),('elder',10)]:
        tile=assembly.tile('body','base','idle',(age,'average','down'),0)
        ys=np.nonzero(tile[:,:,3])[0]
        check(int(ys.max()-ys.min()+1)==height,f'People reference height: {age}')
    for layer in ORDER:
        check((root/f'assets/_preview/people_parts_{layer}.png').exists(),f'Missing people parts preview: {layer}')
