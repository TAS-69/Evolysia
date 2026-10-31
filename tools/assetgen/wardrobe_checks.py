"""Outfit inventory, rig addressing, independent dye and silhouette checks."""
import json
import numpy as np
from PIL import Image
from families.wardrobe import PARTS, ORDER, DENSITIES
from families.people import ANIMS, profiles
from wardrobe_preview import Wardrobe

def validate_wardrobe(root,manifest,check):
    entries=[e for e in manifest['assets'] if e['family']=='wardrobe']
    expected={f'wardrobe.{layer}_{anim}_lod{density}' for layer in PARTS for anim in ANIMS for density in DENSITIES}
    check({e['id'] for e in entries}==expected and len(entries)==len(expected),'Wardrobe sheet inventory')
    for listed in entries:
        e=json.loads((root/'assets'/listed['sidecar']).read_text())
        check(all(e[key]==value for key,value in listed.items()),f'Wardrobe manifest summary differs: {listed["id"]}')
        layer=e['asset_id']; anim=e['animation']; density=e['density']; frames,fps,actions=ANIMS[anim]
        check(e['profiles']==[list(p) for p in profiles(anim)] and e['variants']==PARTS[layer],f'Wardrobe profile/part inventory: {e["id"]}')
        check(e['frame']==[density,density] and e['frames']==frames and e['fps']==fps and e['sim_actions']==actions,f'Wardrobe animation contract: {e["id"]}')
        count=len(e['sheet_rows']); mapping=e['row_lookup']; image=np.array(Image.open(root/'assets'/e['files'][1])); hashes=set()
        check(e['strip_count']==count,f'Wardrobe physical strip count: {e["id"]}')
        check(len(mapping)==len(PARTS[layer]) and all(len(row)==len(e['profiles']) for row in mapping),f'Wardrobe row map dimensions: {e["id"]}')
        check(all(0<=r<count for row in mapping for r in row),f'Wardrobe unresolved physical strip: {e["id"]}')
        for row in range(count):
            x=(row%e['strip_columns'])*density*frames; y=(row//e['strip_columns'])*density
            strip=image[y:y+density,x:x+density*frames]; key=strip.tobytes()
            check(key not in hashes,f'Duplicate physical wardrobe strip: {e["id"]}:{row}'); hashes.add(key)
            for f in range(frames):
                tile=strip[:,f*density:(f+1)*density]; visible=tile[:,:,3]>0
                check(len(np.unique(tile[visible,0]))<=16,f'Wardrobe part colour budget: {e["id"]}:{row}:{f}')
                check(not(visible[0].any() or visible[-1].any() or visible[:,0].any() or visible[:,-1].any()),f'Wardrobe part clipped: {e["id"]}:{row}:{f}')
        for anchors in e['hand_anchors']:
            check(len(anchors)==frames and all(0<=x<density and 0<=y<density for x,y in anchors),f'Wardrobe hand anchor bounds: {e["id"]}')
        check(all(0<=x<density and 0<=y<density for x,y in e['row_pivots']),f'Wardrobe pivot bounds: {e["id"]}')
    a=Wardrobe(root)
    check(a.config['layer_order']==ORDER and len(a.outfits)==32,'Wardrobe customisation contract')
    for outfit in a.outfits.values():
        check(set(outfit['pieces'])==set(ORDER),f'Outfit missing layer: {outfit["id"]}')
        for layer,style in outfit['pieces'].items(): check(style is None or style in PARTS[layer],f'Unknown outfit piece: {outfit["id"]}:{layer}')
        for values in outfit['dyes'].values(): check(all(0<=row<13 for row in values.values()),f'Invalid outfit dye row: {outfit["id"]}')
    for density in DENSITIES:
        base=a.tile('top','court_coat','idle',('adult','average','down'),0,density)
        first=np.array(a.reference(base,dyes={'main':0,'trim':1})); main=np.array(a.reference(base,dyes={'main':6,'trim':1})); trim=np.array(a.reference(base,dyes={'main':0,'trim':8}))
        main_mask=np.isin(base,a.groups['cloth_a']); trim_mask=np.isin(base,a.groups['cloth_b'])
        check(np.array_equal(first[~main_mask],main[~main_mask]) and np.any(first[main_mask]!=main[main_mask]),f'Main dye leaks or does not change: {density}')
        check(np.array_equal(first[~trim_mask],trim[~trim_mask]) and np.any(first[trim_mask]!=trim[trim_mask]),f'Trim dye leaks or does not change: {density}')
        for anim,(frames,_,_) in ANIMS.items():
            age='child' if anim=='play' else 'adult'; directions=['side'] if anim in {'craft','sleep','dead'} else ['down','up','side']
            for dr in directions:
                tiles=[a.compose('farmer',anim,(age,'average',dr),f,density).tobytes() for f in range(frames)]
                check(len(set(tiles))==frames,f'Assembled wardrobe animation duplicate: {anim}:{dr}:{density}')
        for name in a.outfits:
            im=np.array(a.compose(name,density=density)); check(np.count_nonzero(im[:,:,3])>=5,f'Invisible outfit: {name}:{density}')
    for name in ['outfits_1','outfits_2','dyes','profiles','recolours']:
        check((root/f'assets/_preview/wardrobe_{name}.png').exists(),f'Missing wardrobe review: {name}')
