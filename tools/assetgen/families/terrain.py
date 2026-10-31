
import hashlib
import json
import math
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[3]
MATERIALS = ['deep_water','water','shallow_water','sand','grass','dry_grass',
    'forest_floor','jungle_floor','tundra','dirt','mud','marsh','rock','scree',
    'snow','ice','basalt','lava','ash','salt_flat','tilled_soil','path']
PRIORITY = ['deep_water','water','shallow_water','marsh','mud','sand','salt_flat',
    'dirt','path','tilled_soil','dry_grass','grass','tundra','forest_floor',
    'jungle_floor','scree','rock','basalt','ash','snow','ice','lava']
RAMPS = dict(deep_water='water',water='water',shallow_water='water',sand='sand',
    grass='grass',dry_grass='thatch',forest_floor='foliage',jungle_floor='foliage',
    tundra='grass',dirt='soil',mud='soil',marsh='soil',rock='rock',scree='rock',
    snow='snow',ice='ice',basalt='basalt',lava='lava',ash='ash',salt_flat='sand',
    tilled_soil='soil',path='soil')
ANIMATED = {'deep_water','water','shallow_water','lava'}

def stable(name):
    return int.from_bytes(hashlib.sha256(name.encode()).digest()[:4],'little')

def field(mask,x,y):
    u=x/15; v=y/15
    a,b,c,d=[int(bool(mask & bit)) for bit in (1,2,4,8)]
    base=a*(1-u)*(1-v)+b*u*(1-v)+c*u*v+d*(1-u)*v
    wobble=math.sin(math.pi*u)*math.sin(math.pi*v)*(
        .10*math.sin(2*math.pi*u+.8)+.08*math.sin(4*math.pi*v+.4))
    return base+wobble if mask not in (0,15) else float(mask==15)

def geometry(mask,water=False):
    values=np.array([[field(mask,x,y) for x in range(16)] for y in range(16)])
    return values >= (.44 if water and mask not in (0,15) else .5), values

def texture(material,variant,frame,seed,orientation='ns'):
    rng=np.random.default_rng(stable(material)+seed+variant*977)
    ramp=RAMPS[material]
    grid=np.full((16,16),2,dtype=int)
    for _ in range(22):
        x,y=map(int,rng.integers(1,15,2)); shade=int(rng.choice([1,2,3]))
        grid[y,x]=shade
        if material in {'sand','snow','dirt','ash','salt_flat','path'}:
            grid[y,min(14,x+1)]=shade
        if material in {'rock','scree','basalt'}:
            grid[max(1,y-1),x]=min(4,shade+1)
            grid[y,min(14,x+1)]=max(0,shade-1)
        if material in {'grass','dry_grass','tundra','forest_floor','jungle_floor'}:
            grid[max(1,y-1),x]=3
            grid[y,min(14,x+1)]=1
    if material in ANIMATED:
        grid[:]=1 if material=='deep_water' else (3 if material=='shallow_water' else 2)
        for y in range(16):
            for x in range(16):
                wave=(x+2*y+frame*4)%16
                if wave in (0,1) and y%4==variant%4: grid[y,x]=min(4,grid[y,x]+1)
                if material=='lava' and (x*3+y*5+variant*3)%17<3:
                    grid[y,x]=(frame+(x+y)//3)%3+1
    if material=='tilled_soil':
        for y in range(16):
            for x in range(16):
                along,across=(y,x) if orientation=='ns' else (x,y)
                grid[y,x]=[1,1,3,2][across%4]
                if 0<x<15 and 0<y<15 and along%7==variant and across%4==3: grid[y,x]=1
    if material in {'ice','salt_flat'}:
        for y in range(2,14):
            x=(y//3+variant*3)%12+2
            grid[y,x]=1 if material=='ice' else 4
    if material not in ANIMATED and material!='tilled_soil':
        grid[0,:]=2; grid[-1,:]=2; grid[:,0]=2; grid[:,-1]=2
    if material in ANIMATED:
        base=1 if material=='deep_water' else (3 if material=='shallow_water' else 2)
        for y in (2,3,12,13):
            for x in (2,3,12,13):
                grid[y,x]=base+int((x%2)+2*(y%2)==frame)
        grid[0,:]=base; grid[-1,:]=base; grid[:,0]=base; grid[:,-1]=base
    if material=='tilled_soil':
        grid[-1,:]=grid[0,:]; grid[:,-1]=grid[:,0]
    return ramp,grid

def build(seed=0):
    palette=json.loads((ROOT/'assets/palette/palette.json').read_text())
    slots={s['name']:s['index'] for s in palette['slots']}
    reference={s['index']:s['reference'] for s in palette['slots']}
    out=ROOT/'assets/terrain'; out.mkdir(parents=True,exist_ok=True)
    entries=[]
    def slot(ramp,shade):
        return slots[f'{ramp}_{shade}' if ramp in {'grass','foliage','bark','flower'} else f'fixed_{ramp}_{shade}']
    def emit(name,kind,rows,frames,draw,**extra):
        idx=np.zeros((16*len(rows),16*frames,4),dtype=np.uint8)
        for r,label in enumerate(rows):
            for f in range(frames):
                tile=draw(r,f)
                idx[r*16:(r+1)*16,f*16:(f+1)*16]=tile
        ref=np.zeros_like(idx)
        for i,c in reference.items(): ref[idx[:,:,0]==i,:3]=c
        ref[:,:,3]=idx[:,:,3]; ref[ref[:,:,3]==0]=0
        Image.fromarray(ref).save(out/f'{name}.png')
        Image.fromarray(idx).save(out/f'{name}_idx.png')
        entry={'id':f'terrain.{name}','family':'terrain','files':[f'terrain/{name}.png',f'terrain/{name}_idx.png'],
            'sidecar':f'terrain/{name}.json','frame':[16,16],'frames':frames,'directions':['none'],
            'sheet_rows':rows,'fps':3 if frames>1 else 0,'pivot':[8,15],
            'tags':[kind],'seed':seed,'recolor_slots':[s['name'] for s in palette['slots'] if s['index'] in set(idx[idx[:,:,3]>0,0].tolist())],
            'anims':{'idle':{'row':0,'frames':frames,'fps':3}} if frames>1 else {},**extra}
        (out/f'{name}.json').write_text(json.dumps(entry,indent=2)+'\n',encoding='utf-8')
        entries.append(entry)
    for material in MATERIALS:
        frames=4 if material in ANIMATED else 1
        orientations=['ns','ew'] if material=='tilled_soil' else ['none']
        for kind in ['plain','corners']:
            count=4 if kind=='plain' else 16
            rows=[f'{orientation}_{i:02}' for orientation in orientations for i in range(count)]
            def draw(row,frame,kind=kind,material=material,count=count,orientations=orientations):
                variant=row%count; orientation=orientations[row//count]
                ramp,shades=texture(material,variant if kind=='plain' else 0,frame,seed,orientation)
                alpha=np.ones((16,16),dtype=bool)
                if kind=='corners':
                    alpha,values=geometry(variant,material in {'deep_water','water','shallow_water'})
                    if material in {'deep_water','water','shallow_water'}:
                        shades[(values>=.44)&(values<.51)]=4
                    elif variant not in (0,15):
                        shades[(values>=.5)&(values<.57)]=3
                tile=np.zeros((16,16,4),dtype=np.uint8)
                for y in range(16):
                    for x in range(16):
                        pixel_ramp=ramp; shade=int(shades[y,x])
                        interior=0<x<15 and 0<y<15
                        h=stable(material)+seed+(variant if kind=='plain' else 0)*977
                        noise=stable(f'{h}:{x}:{y}')%29
                        if interior:
                            if material in {'forest_floor','jungle_floor'} and noise>7:
                                pixel_ramp='soil'; shade=1 if material=='jungle_floor' else 2
                            if material=='marsh' and ((x-7)**2+(y-8)**2<12 or noise<2):
                                pixel_ramp='water'; shade=1
                            if material=='mud' and noise<4: shade=0
                            if material=='shallow_water' and noise==0:
                                pixel_ramp='sand'; shade=1+(frame+noise)%2
                            if material=='scree':
                                shade=1+stable(f'{h}:{x}:{y}')%3
                            if material=='dry_grass' and noise<7: pixel_ramp='soil'; shade=2
                            if material=='tundra' and noise<9: pixel_ramp='rock'; shade=2
                        if alpha[y,x]: tile[y,x]=[slot(pixel_ramp,shade),0,0,255]
                return tile
            emit(f'{material}_{kind}',kind,rows,frames,draw,material=material,
                 layer_priority=PRIORITY.index(material),masks=list(range(16)) if kind=='corners' else [],
                 mask_bits={'tl':1,'tr':2,'br':4,'bl':8},orientations=orientations,
                 variants_per_orientation=count,emissive=material=='lava')
    for material in ['rock','scree']:
        for kind,count in [('cliff_corners',16),('cliff_wall',3)]:
            def draw(row,frame,material=material,kind=kind):
                alpha,values=geometry(row) if kind=='cliff_corners' else (np.ones((16,16),bool),None)
                tile=np.zeros((16,16,4),dtype=np.uint8)
                for y in range(16):
                    for x in range(16):
                        shade=3 if y<3 else (1 if (x+row*3)%7<2 else 2)
                        if material=='scree' and (x*3+y*5+row)%13<3: shade=max(0,shade-1)
                        if x in (0,15): shade=3 if y<3 else 2
                        if y in (0,15): shade=2
                        if alpha[y,x]: tile[y,x]=[slot('rock',shade),0,0,255]
                return tile
            emit(f'{material}_{kind}',kind,[str(i) for i in range(count)],1,draw,
                 material=material,masks=list(range(16)) if count==16 else [],
                 mask_bits={'tl':1,'tr':2,'br':4,'bl':8},variants_per_orientation=count,
                 orientations=['none'],shadow='engine')
    return entries
