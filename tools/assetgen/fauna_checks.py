
import numpy as np
from PIL import Image
from families.fauna import contracts, SPEC, DOMESTIC, FPS

def validate_fauna(root,manifest,check):
    entries={e['id']:e for e in manifest['assets'] if e['family']=='fauna'}
    expected=list(contracts())
    check(len(SPEC)==17 and len(DOMESTIC)==6,'Fauna species inventory count')
    check(len(expected)==105,'Fauna animation inventory count')
    check(set(entries)=={f'fauna.{name}_{anim}' for name,_,_,anim,_,_ in expected},'Fauna animation coverage mismatch')
    for name,source,size,anim,frames,count in expected:
        ident=f'fauna.{name}_{anim}'
        if ident not in entries: continue
        e=entries[ident]; w,h=size
        check(e['frame']==size and e['frames']==frames and len(e['sheet_rows'])==count,f'Fauna size/frame count: {ident}')
        check(e['directions']==['side'] and e['mirror_left'],f'Fauna facing metadata: {ident}')
        check(e['anims']=={anim:{'row':0,'frames':frames,'fps':FPS[anim]}},f'Fauna animation metadata: {ident}')
        check(e['asset_id']==name.removesuffix('_domestic'),f'Fauna simulation ID: {ident}')
        check(e['pivot']==[w//2,h//2 if source in {'bird_flock','fish_school'} else h-2],f'Fauna pivot: {ident}')
        check(e['shadow']=='engine' and len(e['files'])==2,f'Fauna export contract: {ident}')
        a=np.array(Image.open(root/'assets'/e['files'][0])); coats=[]
        for v in range(count):
            tiles=[]
            for f in range(frames):
                tile=a[v*h:(v+1)*h,f*w:(f+1)*w]; alpha=tile[:,:,3]>0
                check(np.count_nonzero(alpha)>=5,f'Empty fauna frame: {ident}:{v}:{f}')
                check(not(alpha[0].any() or alpha[-1].any() or alpha[:,0].any() or alpha[:,-1].any()),f'Clipped fauna: {ident}:{v}:{f}')
                check(len(np.unique(tile[alpha,:3],axis=0))<=16,f'Fauna colour limit: {ident}')
                check(not np.any(np.all(tile[alpha,:3]==0,axis=1)),f'Black fauna pixel: {ident}')
                tiles.append(tile.tobytes())
            check(len(set(tiles))==frames,f'Duplicate fauna frames: {ident}:coat {v}')
            coats.append(b''.join(tiles))
        check(len(set(coats))==count,f'Duplicate fauna coats: {ident}')
        if count>1:
            check(any(s.startswith('hair_') for s in e['recolor_slots']) and e.get('coat_lut')=='lut_hair',f'Missing domestic recolour slots: {ident}')
            check(all(np.array_equal(a[:h,:,3],a[v*h:(v+1)*h,:,3]) for v in range(1,count)),f'Coat changes animal silhouette: {ident}')
        check((root/f'assets/_preview/fauna_{name}.png').exists(),f'Missing fauna preview: {name}')
