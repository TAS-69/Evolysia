"""Material-based landmark structures, condition states and contained fire."""
import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
from .terrain import stable

ROOT=Path(__file__).resolve().parents[3]
SPEC={
    'windbreak':([16,16],['wood','thatch']), 'lean_to':([16,16],['wood','thatch']),
    'hide_tent':([16,24],['hide','wood']), 'pit_house':([32,24],['soil','wood','thatch']),
    'fire_pit':([16,16],['stone','wood','ash']), 'drying_rack':([16,16],['wood','hide']),
    'storage_pit':([16,16],['soil','wood']), 'granary':([16,24],['wood','thatch']),
    'kiln':([16,24],['clay','stone']), 'smelter':([16,24],['mudbrick','stone']),
    'well':([16,16],['stone','wood']), 'burial_mound':([16,16],['soil','grass']),
    'cairn':([16,16],['stone']), 'standing_stone':([16,24],['stone']),
    'totem':([16,32],['wood','culture']), 'altar':([16,16],['stone']),
    'raft':([16,16],['wood','bark']), 'canoe':([16,16],['wood'])}
STATES=['building','complete','damaged','ruined','burned']
LIT={'fire_pit','kiln','smelter'}
SHELTERS={'windbreak','lean_to','hide_tent','pit_house','granary'}

class Structure:
    def __init__(self,size,slots):
        self.w,self.h=size; self.base=self.h-2; self.cx=self.w//2; self.slots=slots
        self.image=Image.new('L',tuple(size),255); self.d=ImageDraw.Draw(self.image)

    def ink(self,ramp,shade):
        return self.slots[f'{ramp}_{shade}' if ramp in {'grass','bark','culture'} else f'fixed_{ramp}_{shade}']

    def rect(self,box,ramp,shade): self.d.rectangle(box,fill=self.ink(ramp,shade))
    def poly(self,points,ramp,shade): self.d.polygon(points,fill=self.ink(ramp,shade))
    def line(self,points,ramp,shade): self.d.line(points,fill=self.ink(ramp,shade))
    def ellipse(self,box,ramp,shade): self.d.ellipse(box,fill=self.ink(ramp,shade))
    def dot(self,x,y,ramp,shade): self.d.point((x,y),fill=self.ink(ramp,shade))

    def rock(self,x,y,radius=2,ramp='stone'):
        self.poly([(x-radius,y),(x-radius+1,y-1),(x+radius-1,y-1),(x+radius,y),(x+radius-1,y+1),(x-radius+1,y+1)],ramp,2)
        self.line([(x-radius+1,y-1),(x,y-1)],ramp,3)
        self.dot(x+radius-1,y+1,ramp,1)

def complete(kind,seed,slots):
    size,materials=SPEC[kind]; p=Structure(size,slots); b=p.base; c=p.cx
    if kind=='windbreak':
        p.poly([(2,6),(12,4),(13,12),(3,14)],'thatch',2)
        for x in range(4,12,2): p.line([(x,6),(x+1,12)],'thatch',1)
        p.line([(2,4),(3,14)],'wood',1); p.line([(12,3),(13,13)],'wood',1)
        p.line([(2,5),(12,3)],'wood',3); p.line([(3,12),(13,10)],'wood',2)
    elif kind=='lean_to':
        p.line([(3,5),(3,13)],'wood',1); p.line([(12,5),(12,14)],'wood',1)
        p.poly([(2,5),(10,2),(14,8),(7,11)],'thatch',2)
        p.poly([(10,2),(14,8),(12,9),(9,3)],'thatch',1)
        p.line([(2,5),(9,2)],'thatch',3)
        for x in range(4,10,2): p.line([(x,5),(x+4,9)],'thatch',3)
        p.line([(7,11),(13,14)],'wood',2)
    elif kind=='hide_tent':
        p.poly([(8,3),(14,19),(11,22),(2,20)],'hide',2)
        p.poly([(8,3),(11,22),(14,19)],'hide',1)
        p.poly([(8,12),(11,21),(6,21)],'hide',0)
        p.line([(8,3),(2,20)],'hide',3)
        p.line([(7,2),(9,5)],'wood',3)
        p.line([(3,20),(11,22),(14,19)],'hide',0)
    elif kind=='pit_house':
        p.ellipse((2,12,30,22),'soil',1)
        p.poly([(5,12),(25,12),(28,20),(5,21)],'wood',1)
        p.poly([(3,12),(14,3),(27,6),(30,15),(16,17)],'thatch',2)
        p.poly([(14,3),(27,6),(30,15),(16,17)],'thatch',1)
        p.line([(3,12),(14,3),(26,6)],'thatch',3)
        for x in range(6,15,3): p.line([(x,12),(14,5),(x+12,14)],'thatch',3)
        p.rect((13,16,18,21),'soil',0)
        p.line([(12,17),(12,21),(19,22)],'wood',3)
    elif kind=='fire_pit':
        p.ellipse((3,7,12,13),'ash',1)
        p.line([(5,9),(10,12)],'wood',1); p.line([(5,12),(10,9)],'wood',2)
        for x,y in [(4,6),(8,5),(11,7),(12,10),(10,13),(6,13),(3,11),(2,8)]: p.rock(x,y,1)
    elif kind=='drying_rack':
        p.line([(3,3),(3,14)],'wood',1); p.line([(12,4),(12,14)],'wood',1)
        p.line([(2,4),(13,5)],'wood',3)
        for x,h in [(5,5),(8,4),(10,6)]:
            p.line([(x,5),(x,6)],'wood',1)
            p.rect((x,7,x+1,7+h),'hide',2)
            p.dot(x,7,'hide',3); p.dot(x+1,7+h,'hide',0)
    elif kind=='storage_pit':
        p.ellipse((2,6,13,14),'soil',2); p.ellipse((4,8,11,12),'soil',0)
        p.line([(3,8),(5,6),(10,6)],'soil',3)
        p.poly([(3,7),(9,6),(10,9),(4,10)],'wood',2)
        p.line([(4,8),(9,7)],'wood',3); p.line([(6,7),(7,9)],'wood',1)
    elif kind=='granary':
        for x in [4,11]:
            p.line([(x,16),(x,22)],'wood',1)
            p.rect((x-1,20,x+1,20),'stone',3)
        p.poly([(3,11),(11,10),(13,17),(5,19)],'wood',2)
        p.poly([(11,10),(13,12),(13,17),(11,18)],'wood',1)
        for y in range(12,18,2): p.line([(5,y),(11,y-1)],'wood',1)
        p.poly([(2,11),(7,3),(13,8),(14,12),(7,14)],'thatch',2)
        p.poly([(7,3),(13,8),(14,12),(7,14)],'thatch',1)
        p.line([(2,11),(7,3),(10,6)],'thatch',3)
    elif kind=='kiln':
        p.ellipse((2,6,13,22),'clay',2)
        p.poly([(10,7),(13,12),(12,21),(10,22),(11,14)],'clay',1)
        p.line([(3,12),(4,8),(7,7)],'clay',3)
        p.ellipse((6,14,11,20),'clay',0)
        p.line([(6,20),(11,20)],'stone',2)
        p.rect((6,5,9,6),'clay',1); p.line([(6,5),(8,5)],'clay',3)
    elif kind=='smelter':
        p.poly([(5,3),(10,2),(11,13),(14,18),(12,22),(3,22),(2,18),(5,13)],'mudbrick',2)
        p.poly([(10,2),(11,13),(14,18),(12,22),(11,19),(9,14)],'mudbrick',1)
        p.line([(5,3),(5,13),(3,17)],'mudbrick',3)
        p.rect((6,3,9,4),'mudbrick',0)
        for y in [8,11,15]: p.line([(6,y),(9,y)],'mudbrick',1)
        p.rect((6,17,10,20),'mudbrick',0)
        p.line([(5,21),(11,21)],'stone',3)
    elif kind=='well':
        p.ellipse((2,7,13,14),'stone',1)
        p.ellipse((2,5,13,11),'stone',2); p.ellipse((5,7,10,9),'stone',0)
        p.line([(3,7),(5,5),(9,5)],'stone',3)
        for x in [4,11]: p.line([(x,9),(x,13)],'stone',0)
        p.line([(3,8),(3,3),(12,4),(12,9)],'wood',1)
        p.line([(4,3),(11,4)],'wood',3); p.line([(8,4),(8,8)],'wood',2)
    elif kind=='burial_mound':
        p.ellipse((2,5,13,14),'soil',2)
        p.poly([(2,10),(4,6),(8,5),(11,7),(8,9),(6,8),(4,11)],'grass',2)
        p.line([(4,7),(7,5),(9,6)],'grass',3)
        p.line([(11,11),(12,12),(11,14)],'soil',1)
        p.rock(4,12,1)
    elif kind=='cairn':
        for x,y,r in [(4,13,2),(9,13,3),(12,11,1),(6,10,2),(10,9,2),(7,7,2)]: p.rock(x,y,r)
        p.rect((7,4,9,5),'stone',2); p.line([(7,4),(8,4)],'stone',3)
    elif kind=='standing_stone':
        p.poly([(5,3),(9,2),(12,7),(11,22),(4,22),(3,17)],'stone',2)
        p.poly([(9,2),(12,7),(11,22),(9,21)],'stone',1)
        p.line([(5,4),(4,16),(5,19)],'stone',3)
        p.line([(6,3),(8,2)],'stone',4)
    elif kind=='totem':
        p.poly([(4,3),(10,2),(13,5),(13,26),(11,28),(2,27),(2,6)],'wood',2)
        p.poly([(10,2),(13,5),(13,26),(11,28),(10,8)],'wood',0)
        p.line([(3,7),(4,4),(8,3)],'wood',3)
        p.rect((2,28,13,30),'wood',1); p.line([(2,28),(11,28)],'wood',3)
        for x,y in [(5,6),(8,7),(4,23),(7,25)]:
            p.line([(x,y),(x,y+1),(x+1,y+2)],'culture',2)
    elif kind=='altar':
        p.rect((4,10,11,14),'stone',1)
        p.poly([(2,9),(7,6),(13,8),(12,11),(4,12)],'stone',2)
        p.poly([(2,9),(7,6),(13,8),(8,10)],'stone',3)
        p.line([(12,9),(12,11),(5,12)],'stone',0)
    elif kind=='raft':
        for i in range(4):
            x=2+i*3
            p.poly([(x,5),(x+1,4),(x+2,5),(x+2,13),(x+1,14),(x,13)],'wood',2)
            p.line([(x,6),(x,12)],'wood',3); p.line([(x+2,6),(x+2,12)],'wood',1)
        for y in [7,11]: p.line([(2,y),(13,y)],'bark',1)
    else:
        p.poly([(1,9),(4,6),(12,4),(14,5),(12,11),(4,14),(2,12)],'wood',1)
        p.poly([(2,9),(5,7),(12,5),(13,5),(10,10),(4,12)],'wood',3)
        p.poly([(4,9),(6,7),(11,6),(10,9),(5,11)],'wood',0)
        p.line([(4,13),(11,10),(13,7)],'wood',2)
    a=np.array(p.image)
    rng=np.random.default_rng(stable(kind)+seed)
    for _ in range(max(2,p.w//4)):
        x=int(rng.integers(2,p.w-2)); y=int(rng.integers(3,p.h-2))
        current=int(a[y,x])
        if current!=255:
            name=next(n for n,index in slots.items() if index==current)
            if name.startswith('fixed_'):
                ramp=name[6:].rsplit('_',1)[0]
                a[y,x]=p.ink(ramp,3 if x<p.cx else 1)
    return Image.fromarray(a)

def condition(kind,state,seed,slots):
    size,materials=SPEC[kind]; p=Structure(size,slots); b=p.base; c=p.cx
    source=np.array(complete(kind,seed,slots)); rng=np.random.default_rng(stable(kind)+seed+711)
    main=materials[0]
    if state=='building':
        if kind=='windbreak':
            p.line([(2,4),(3,b)],'wood',1); p.line([(12,3),(13,b)],'wood',1)
            p.line([(2,5),(12,4)],'wood',3)
            for x in range(4,8): p.line([(x,7),(x,12)],'thatch',2 if x%2 else 1)
        elif kind in SHELTERS:
            p.line([(3,b),(3,max(3,b-10)),(c,3),(p.w-3,max(5,b-7)),(p.w-3,b)],'wood',1)
            p.line([(3,b-2),(p.w-3,b-2)],'wood',3)
            p.line([(c,3),(c,b-3)],'wood',2)
            p.rock(c,b-1,2,'soil')
        elif kind in {'raft','canoe'}:
            if kind=='canoe':
                p.line([(1,9),(4,6),(12,4),(14,5),(12,11),(4,14),(2,12),(1,9)],'wood',2)
                for x,y in [(4,9),(7,8),(10,7)]: p.line([(x,y-1),(x+1,y+2)],'wood',3)
            else:
                for x in [2,5,8]:
                    p.line([(x,5),(x,13),(x+1,14)],'wood',2)
                p.line([(2,7),(12,7)],'bark',1)
        elif kind in {'drying_rack','totem'}:
            p.line([(3,b),(p.w-3,b-3),(p.w-4,b-5),(3,b-2)],'wood',2)
            p.line([(5,b-1),(5,max(5,b-10))],'wood',1)
            if kind=='totem': p.rect((5,10,10,b),'wood',2)
        elif kind=='kiln':
            p.ellipse((3,b-7,12,b),'clay',2)
            p.rect((3,b-7,12,b-5),'clay',1)
            p.rect((6,b-4,10,b-1),'clay',0)
            p.line([(4,b-6),(8,b-6)],'clay',3)
        elif kind=='smelter':
            p.poly([(4,b),(3,b-4),(5,b-9),(10,b-9),(12,b-4),(11,b)],'mudbrick',2)
            for y in [b-7,b-4]: p.line([(5,y),(10,y)],'mudbrick',1)
            p.rect((6,b-3,9,b-1),'mudbrick',0)
            p.line([(5,b-9),(9,b-9)],'mudbrick',3)
        elif kind=='well':
            p.ellipse((2,7,13,b),'stone',2); p.ellipse((4,8,11,11),'stone',0)
            p.line([(3,9),(5,7),(9,7)],'stone',3)
            p.rect((10,7,13,9),'stone',1)
        elif kind=='standing_stone':
            p.line([(3,b-3),(12,b-3)],'wood',1)
            p.line([(3,b),(12,b)],'wood',2)
            p.poly([(3,b-7),(11,b-8),(12,b-5),(11,b-1),(4,b-1),(2,b-4)],'stone',2)
            p.line([(3,b-6),(10,b-7)],'stone',3)
        elif kind=='cairn':
            for x,y,r in [(4,b-1,2),(9,b-1,3),(8,b-4,2)]: p.rock(x,y,r)
        elif kind=='altar':
            p.rect((4,b-4,6,b),'stone',1); p.rect((10,b-4,12,b),'stone',2)
            p.line([(4,b-4),(6,b-4)],'stone',3)
            p.rock(8,b-1,1)
        else:
            for x in range(3,p.w-2,4): p.rock(x,b-1,2,main)
        return p.image
    a=source.copy()
    if state=='ruined':
        a[:b-max(3,p.h//4)]=255
        for y,x in np.argwhere(a!=255):
            if rng.random()<.18: a[y,x]=255
        p.image=Image.fromarray(a); p.d=ImageDraw.Draw(p.image)
        for i in range(3):
            x=int(rng.integers(3,p.w-3)); y=b-int(rng.integers(1,4))
            p.rock(x,y,1 if p.w==16 else 2,main if main not in {'wood','hide'} else 'stone')
        if main in {'wood','hide'}: p.line([(c,b),(c-3,b-5)],'wood',1)
        return p.image
    count=3
    for _ in range(count):
        edge=(np.roll(a,-1,axis=1)==255)|(np.roll(a,1,axis=1)==255)|(np.roll(a,1,axis=0)==255)
        candidates=np.argwhere((a!=255)&edge&(np.indices(a.shape)[0]<b-2))
        if not len(candidates): break
        y,x=map(int,candidates[int(rng.integers(len(candidates)))])
        radius=1 if state=='damaged' else 2
        offsets=[(0,0),(1,0),(1,1),(0,1),(-1,1)] if radius==1 else [(0,-1),(1,-1),(-1,0),(0,0),(1,0),(2,0),(0,1),(1,1),(2,1),(1,2)]
        for dx,dy in offsets:
            xx,yy=x+dx,y+dy
            if 1<=xx<p.w-1 and 1<=yy<b-1: a[yy,xx]=255
    if state=='burned':
        for y,x in np.argwhere(a!=255):
            name=next(n for n,index in slots.items() if index==int(a[y,x]))
            organic=any(token in name for token in ['wood','bark','thatch','hide','grass','culture'])
            ramp='basalt' if organic else 'ash'
            original_shade=int(name.rsplit('_',1)[1])
            shade=0 if original_shade==0 else (2 if original_shade>=3 else 1)
            if shade>0 and stable(f'{kind}:{seed}:{x}:{y}')%11==0: shade=2
            a[y,x]=p.ink(ramp,shade)
    else:
        for y in range(max(3,b-8),b-2):
            x=c+(y%3)-1
            if a[y,x]!=255: a[y,x]=p.ink(main,0)
    return Image.fromarray(a)

def lit(kind,frame,seed,slots):
    p=Structure(SPEC[kind][0],slots); p.image=complete(kind,seed,slots); p.d=ImageDraw.Draw(p.image)
    original=np.array(p.image)
    x,y={'fire_pit':(8,11),'kiln':(8,19),'smelter':(8,20)}[kind]
    height=[4,5,3,4][frame]; shift=int(frame==3)
    p.poly([(x-2,y),(x-1+shift,y-2),(x+shift,y-height),(x+1,y-2),(x+2,y)],'fire',1)
    p.poly([(x-1,y),(x+shift,y-height+2),(x+1,y)],'fire',3)
    p.dot(x+shift,y-1,'fire',4)
    if kind in {'kiln','smelter'}:
        a=np.array(p.image); opening=original==p.ink('clay' if kind=='kiln' else 'mudbrick',0)
        yy,xx=np.indices(a.shape)
        opening &= (yy>= (14 if kind=='kiln' else 17)) & (yy<=20) & (xx>=6) & (xx<=11 if kind=='kiln' else xx<=10)
        fire=np.isin(a,[p.ink('fire',i) for i in range(5)])
        a[fire&~opening]=original[fire&~opening]; p.image=Image.fromarray(a)
    return p.image

def build(seed=0):
    palette=json.loads((ROOT/'assets/palette/palette.json').read_text()); slots={s['name']:s['index'] for s in palette['slots']}
    lut=np.zeros((256,4),dtype=np.uint8)
    for s in palette['slots']: lut[s['index']]=[*s['reference'],255]
    out=ROOT/'assets/structures'; out.mkdir(parents=True,exist_ok=True); entries=[]
    for kind,(size,materials) in SPEC.items():
        for state in STATES+(['lit'] if kind in LIT else []):
            w,h=size; frames=4 if state=='lit' else 1
            idx=np.zeros((h,w*frames,4),dtype=np.uint8)
            for f in range(frames):
                image=complete(kind,seed,slots) if state=='complete' else (lit(kind,f,seed,slots) if state=='lit' else condition(kind,state,seed,slots))
                indices=np.array(image); tile=np.zeros((h,w,4),dtype=np.uint8)
                tile[:,:,0]=np.where(indices==255,0,indices); tile[:,:,3]=np.where(indices==255,0,255)
                idx[:,f*w:(f+1)*w]=tile
            ref=lut[idx[:,:,0]].copy(); ref[:,:,3]=idx[:,:,3]; ref[idx[:,:,3]==0]=0
            name=f'{kind}_{state}'; used=set(idx[idx[:,:,3]>0,0].tolist())
            Image.fromarray(idx).save(out/f'{name}_idx.png'); Image.fromarray(ref).save(out/f'{name}.png')
            e={'id':f'structures.{name}','family':'structures','asset_id':kind,
               'state':'complete' if state=='lit' else state,'lighting':'lit' if state=='lit' else 'unlit',
               'files':[f'structures/{name}.png',f'structures/{name}_idx.png'],'sidecar':f'structures/{name}.json',
               'frame':size,'frames':frames,'directions':['none'],'fps':6 if frames>1 else 0,'pivot':[w//2,h-2],
               'anims':{'lit':{'row':0,'frames':frames,'fps':6}} if frames>1 else {},
               'tags':['structure','landmark',*materials],'materials':materials,'seed':seed,
               'shadow':'engine','shadow_size':'L' if w==32 else ('M' if h>=24 else 'S'),
               'recolor_slots':[s['name'] for s in palette['slots'] if s['index'] in used],
               'emissive_slots':[s['name'] for s in palette['slots'] if s['index'] in used and s['name'].startswith('fixed_fire_')]}
            if kind=='totem': e['glyph_anchor']={'position':[2,10],'size':[12,12],'visible_states':['complete','damaged'],'glyph_generated':False}
            if state=='lit' and kind in {'kiln','smelter'}:
                e['fire_window']=[6,14,11,20] if kind=='kiln' else [6,17,10,20]
            (out/f'{name}.json').write_text(json.dumps(e,indent=2)+'\n',encoding='utf-8'); entries.append(e)
    return entries
