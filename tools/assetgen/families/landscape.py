"""Detailed terrain materials, seam-compatible transitions and exposed rock faces."""
import json
from functools import lru_cache
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
from .terrain import MATERIALS as ORIGINAL, PRIORITY, RAMPS, ANIMATED, stable, field
from .civilisation import DENSITIES, Pixels

ROOT=Path(__file__).resolve().parents[3]
EXTRA={
    'chalk':('stone','rock'),'limestone':('stone','rock'),'granite':('rock','rock'),'slate':('rock','rock'),'shale':('rock','rock'),'sandstone':('sand','rock'),'obsidian':('basalt','basalt'),
    'peat':('soil','mud'),'loam':('soil','dirt'),'silt':('sand','mud'),'red_clay':('clay','dirt'),'yellow_clay':('sand','dirt'),'black_soil':('soil','dirt'),'gravel':('rock','scree'),
    'coarse_sand':('sand','sand'),'fine_sand':('sand','sand'),'red_sand':('clay','sand'),'dune':('sand','sand'),'bog':('soil','marsh'),'salt_marsh':('grass','marsh'),'mudflat':('soil','mud'),
    'coral_shore':('clay','sand'),'shell_beach':('sand','sand'),'mangrove_floor':('foliage','jungle_floor'),'leaf_litter':('foliage','forest_floor'),'pine_needles':('bark','forest_floor'),
    'moss_floor':('foliage','tundra'),'lichen_rock':('rock','rock'),'frozen_mud':('soil','mud'),'permafrost':('rock','tundra'),'meadow':('grass','grass'),'heathland':('foliage','tundra'),'steppe':('thatch','dry_grass'),'volcanic_glass':('basalt','basalt')}
MATERIALS={**{m:(RAMPS[m],m) for m in ORIGINAL},**EXTRA}
CLIFFS=['rock','scree','granite','sandstone','basalt','ice','chalk','limestone']

def texture(name,variant,frame,slots,seed,orientation='ns',cliff=False):
    ramp,parent=MATERIALS[name]; p=Pixels(slots); rng=np.random.default_rng(stable(name)+seed+variant*977)
    base=1 if name in {'black_soil','peat','deep_water','obsidian'} else 3 if name in {'chalk','fine_sand','snow','salt_flat'} else 2
    p.rect((0,0,63,63),ramp,base)
    rocky=parent in {'rock','scree','basalt'} or name in CLIFFS
    if name in ANIMATED:
        for y in range(6,60,12):
            shift=(frame*6+variant*9+y//3)%40
            p.line([(shift,y),(min(60,shift+19),y-1),(min(61,shift+25),y+1)],ramp,3,2)
        if name=='lava':
            for _ in range(22):
                x,y=map(int,rng.integers(6,58,size=2)); p.poly([(x-4,y),(x,y-5),(x+5,y+2),(x,y+5)],ramp,1+(frame+x//4)%3)
        for y in [8,48]:
            for x in [8,48]:
                p.rect((x,y,x+7,y+7),ramp,base)
                dx=4*(frame%2); dy=4*(frame//2); xx=x+dx; yy=y+dy
                p.line([(xx,yy+3),(xx+2,yy+1),(xx+5,yy+3)],ramp,3)
                p.line([(xx+1,yy+2),(xx+3,yy+2)],ramp,3)
    elif name=='tilled_soil':
        for i in range(0,64,16):
            box=(i,0,i+4,63) if orientation=='ns' else (0,i,63,i+4)
            p.rect(box,ramp,1)
            line=[(i+5,0),(i+5,63)] if orientation=='ns' else [(0,i+5),(63,i+5)]
            p.line(line,ramp,3,2)
        for _ in range(15):
            x,y=map(int,rng.integers(5,59,size=2)); p.rect((x,y,x+2,y+1),ramp,2)
    elif rocky:
        for _ in range(16 if name not in {'gravel','scree'} else 55):
            x,y=map(int,rng.integers(2,62,size=2)); radius=int(rng.integers(3,9 if name not in {'gravel','scree'} else 5))
            if name in {'gravel','scree'}:
                p.poly([(x-radius,y),(x-2,y-radius),(x+radius,y-2),(x+radius-1,y+radius),(x-3,y+radius)],ramp,2)
                p.line([(x-radius,y),(x-2,y-radius),(x+radius-1,y-2)],ramp,3)
            else:
                p.line([(x-radius,y+2),(x-2,y-radius),(x+radius,y-2)],ramp,3)
                p.line([(x+radius,y-1),(x+radius-2,y+3)],ramp,1)
    else:
        for _ in range(80):
            x,y=map(int,rng.integers(2,62,size=2)); shade=int(rng.choice([1,2,3]))
            p.rect((x,y,x+int(rng.integers(1,4)),y+1),ramp,shade)
            if parent in {'grass','dry_grass','forest_floor','jungle_floor','tundra'}:
                p.line([(x,y+3),(x-1,y-2)],ramp,3); p.line([(x+1,y+3),(x+4,y)],ramp,1)
    if name in {'forest_floor','jungle_floor','leaf_litter','mangrove_floor'}:
        for _ in range(24):
            x,y=map(int,rng.integers(5,59,size=2)); p.ellipse((x,y,x+3,y+2),'soil',2)
            if name in {'leaf_litter','forest_floor'}: p.line([(x,y),(x+2,y+2)],'wood',3)
    if name in {'marsh','bog','salt_marsh','mudflat'}:
        for x,y in [(14,18),(44,39),(28,50)]:
            p.ellipse((x-6,y-3,x+7,y+4),'water',1); p.line([(x-4,y-3),(x+2,y-3)],'water',3)
    if name in {'ice','frozen_mud','permafrost','volcanic_glass'}:
        for y in range(8,54,13): p.line([(7,y),(29,y+6),(44,y+3),(57,y+10)],'ice' if name!='volcanic_glass' else 'basalt',3)
    if name in {'dune','fine_sand','shell_beach','salt_flat'}:
        for y in range(8,58,12): p.line([(5,y),(23,y-3),(42,y+3),(58,y)],ramp,3,2)
    if name=='lichen_rock':
        for x,y in [(13,15),(41,39),(23,52)]: p.ellipse((x,y,x+7,y+3),'foliage',3)
    if cliff:
        p.rect((0,0,63,7),ramp,3)
        for x in range(6,60,13):
            p.line([(x,12),(x-2,33),(x+1,58)],ramp,1,3)
            p.line([(x+3,15),(x+2,48)],ramp,3)
    a=np.array(p.im); a[0]=p.ink(ramp,base); a[-1]=p.ink(ramp,base); a[:,0]=p.ink(ramp,base); a[:,-1]=p.ink(ramp,base)
    return Image.fromarray(a),base

@lru_cache(maxsize=None)
def geometry(mask,density,water=False):
    yy,xx=np.indices((density,density)); u=xx*15/(density-1); v=yy*15/(density-1)
    values=np.array([[field(mask,float(x),float(y)) for x,y in zip(xs,ys)] for xs,ys in zip(u,v)])
    return values>= (.44 if water and mask not in {0,15} else .5),values

def build(seed=0):
    palette=json.loads((ROOT/'assets/palette/palette.json').read_text()); slots={s['name']:s['index'] for s in palette['slots']}
    lookup=np.zeros((256,4),np.uint8)
    for s in palette['slots']: lookup[s['index']]=[*s['reference'],255]
    out=ROOT/'assets/landscape'; out.mkdir(parents=True,exist_ok=True); entries=[]
    for name,(ramp,parent) in MATERIALS.items():
        kinds=['plain','corners']+(['cliff_corners','cliff_wall'] if name in CLIFFS else [])
        for kind in kinds:
            count=4 if kind=='plain' else 3 if kind=='cliff_wall' else 16
            orientations=['ns','ew'] if name=='tilled_soil' else ['none']; frames=4 if name in ANIMATED and not kind.startswith('cliff') else 1
            canonical={(orientation,row,f):texture(name,row if kind in {'plain','cliff_wall'} else 0,f,slots,seed,orientation,kind.startswith('cliff')) for orientation in orientations for row in range(count) for f in range(frames)}
            for density in DENSITIES:
                rows=[f'{orientation}_{row:02d}' for orientation in orientations for row in range(count)]; idx=np.zeros((len(rows)*density,frames*density,4),np.uint8)
                for oi,orientation in enumerate(orientations):
                    for row in range(count):
                        for f in range(frames):
                            im,base=canonical[(orientation,row,f)]; a=np.array(im.resize((density,density),Image.Resampling.NEAREST)); p=Pixels(slots)
                            a[0]=a[-1]=p.ink(ramp,base); a[:,0]=a[:,-1]=p.ink(ramp,base)
                            alpha=np.ones((density,density),bool)
                            if kind.endswith('corners'):
                                alpha,values=geometry(row,density,name in {'water','shallow_water','deep_water'})
                                if row not in {0,15}: a[(values>=.44)&(values<.51) if name in {'water','shallow_water','deep_water'} else (values>=.5)&(values<.57)]=p.ink(ramp,3)
                            y=(oi*count+row)*density; x=f*density
                            idx[y:y+density,x:x+density,0]=np.where(alpha,a,0); idx[y:y+density,x:x+density,3]=alpha*255
                ref=lookup[idx[:,:,0]].copy(); ref[:,:,3]=idx[:,:,3]; ref[idx[:,:,3]==0]=0
                stem=f'{name}_{kind}_lod{density}'; Image.fromarray(idx).save(out/f'{stem}_idx.png'); Image.fromarray(ref).save(out/f'{stem}.png')
                used=set(idx[idx[:,:,3]>0,0].tolist())
                e={'id':'landscape.'+stem,'family':'landscape','asset_id':name,'kind':kind,'files':[f'landscape/{stem}.png',f'landscape/{stem}_idx.png'],'sidecar':f'landscape/{stem}.json',
                   'frame':[density,density],'logical_frame':[16,16],'density':density,'frames':frames,'fps':3 if frames>1 else 0,'directions':['none'],'sheet_rows':rows,'pivot':[density//2,density-1],
                   'anims':{'idle':{'frames':frames,'fps':3}} if frames>1 else {},'mask_bits':{'tl':1,'tr':2,'br':4,'bl':8},'masks':list(range(16)) if kind.endswith('corners') else [],
                   'priority_anchor':parent,'layer_priority':PRIORITY.index(parent),'priority_offset':list(MATERIALS).index(name),'orientations':orientations,'variants_per_orientation':count,'seed':seed,
                   'recolor_slots':[s['name'] for s in palette['slots'] if s['index'] in used],'emissive_slots':[s['name'] for s in palette['slots'] if s['index'] in used and s['name'].startswith('fixed_lava_')],
                   'tags':['detailed_terrain',parent],'shadow':'engine','homogeneous_mask_15':'choose_any_matching_plain_variant_to_break_repetition'}
                (out/f'{stem}.json').write_text(json.dumps(e,indent=2)+'\n',encoding='utf-8'); entries.append(e)
    return entries
