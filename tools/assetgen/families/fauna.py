
import json
import math
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
from .terrain import stable

ROOT=Path(__file__).resolve().parents[3]
GRAZER={'idle':2,'walk':4,'run':4,'eat':2,'dead':1}
SPEC={
    'rabbit':([8,8],{'idle':2,'walk':4,'run':4,'dead':1}),
    'fowl':([8,8],{'idle':2,'walk':4,'flap':2,'dead':1}),
    'bird_flock':([16,16],{'fly':4}), 'fish_school':([16,16],{'swim':4}),
    'goat':([16,16],GRAZER),'sheep':([16,16],GRAZER),'deer':([16,16],GRAZER),
    'boar':([16,16],{**GRAZER,'attack':3}), 'elk':([16,24],GRAZER),
    'bison':([24,16],{**GRAZER,'attack':3}),'horse':([24,16],GRAZER),
    'camel':([24,24],{'idle':2,'walk':4,'dead':1}),
    'wolf':([16,16],{'idle':2,'walk':4,'run':4,'attack':3,'howl':2,'dead':1}),
    'bear':([24,16],{'idle':2,'walk':4,'run':4,'attack':3,'stand':2,'dead':1}),
    'big_cat':([24,16],{'idle':2,'walk':4,'run':4,'attack':3,'dead':1}),
    'crocodile':([24,8],{'idle':2,'crawl':4,'attack':3,'dead':1}),
    'snake':([16,8],{'idle':2,'slither':4,'strike':2})}
DOMESTIC={'fowl_domestic':'fowl','goat_domestic':'goat','sheep_domestic':'sheep',
          'horse_domestic':'horse','cattle':'bison','dog':'wolf'}
FPS={'idle':3,'walk':8,'run':12,'eat':4,'attack':10,'howl':3,'stand':3,
     'fly':8,'swim':6,'crawl':6,'slither':6,'strike':10,'flap':8,'dead':0}
PREDATORS={'wolf','bear','big_cat','crocodile','snake'}
MODELS={
    'goat':((3,7,10,10),(11,5,13,8)), 'sheep':((2,6,10,11),(11,7,13,10)),
    'deer':((3,6,10,9),(11,4,13,7)), 'boar':((2,6,10,11),(11,8,13,11)),
    'elk':((3,12,10,17),(11,9,13,13)), 'bison':((4,5,15,11),(16,7,20,11)),
    'horse':((5,6,15,10),(18,3,20,6)), 'camel':((4,11,15,16),(18,4,20,7)),
    'wolf':((3,7,9,10),(10,5,12,8)), 'bear':((4,5,16,12),(17,5,20,9)),
    'big_cat':((5,7,15,10),(17,6,19,9))}

def contracts():
    for name in [*SPEC,*DOMESTIC]:
        source=DOMESTIC.get(name,name); size,anims=SPEC[source]
        for anim,frames in anims.items(): yield name,source,size,anim,frames,3 if name in DOMESTIC else 1

class Animal:
    def __init__(self,size,slots):
        self.w,self.h=size; self.slots=slots
        self.image=Image.new('L',tuple(size),255); self.d=ImageDraw.Draw(self.image)

    def ink(self,ramp,shade):
        return self.slots[f'{ramp}_{shade}' if ramp=='hair' else f'fixed_{ramp}_{shade}']

    def point(self,x,y,ramp,shade):
        if 1<=x<self.w-1 and 1<=y<self.h-1: self.d.point((x,y),fill=self.ink(ramp,shade))

    def line(self,points,ramp,shade,width=1):
        margin=1+width//2
        points=[(int(np.clip(x,margin,self.w-1-margin)),int(np.clip(y,margin,self.h-1-margin))) for x,y in points]
        self.d.line(points,fill=self.ink(ramp,shade),width=width)

    def blob(self,box,ramp,pattern=0,seed=0):
        x0,y0,x1,y1=box
        mask=Image.new('L',(self.w,self.h)); ImageDraw.Draw(mask).ellipse(box,fill=255)
        a=np.array(mask)
        for y,x in np.argwhere(a):
            if not (1<=x<self.w-1 and 1<=y<self.h-1): continue
            t=(x-x0)/max(1,x1-x0)+(y-y0)/max(1,y1-y0)
            shade=3 if t<.65 else (1 if t>1.5 else 2)
            if ramp=='hair': shade=min(3,shade)
            colour_ramp=ramp
            middle=(x0+x1)//2
            patch=(pattern==1 and x<middle) or (pattern==2 and x>middle)
            if patch and (x!=middle-1 or stable(f'{seed}:{x}:{y}')%3!=0):
                colour_ramp='hide'; shade=4 if t<1 else 3
            self.point(int(x),int(y),colour_ramp,shade)

def quadruped(name,source,size,anim,f,pattern,seed,slots):
    p=Animal(size,slots); w,h=size; ground=h-2
    domestic=name in DOMESTIC
    ramp='hair' if domestic else ('basalt' if source in PREDATORS else 'hide')
    body,head=MODELS[source]; x0,y0,x1,y1=body; hx0,hy0,hx1,hy1=head
    if name=='cattle': y0+=1
    bob=-(f%2) if anim in {'idle','run'} else 0
    if anim=='dead':
        p.blob((x0,ground-3,x1+1,ground-1),ramp,pattern,seed)
        p.line([(x1-1,ground-2),(min(w-6,hx0)+1,ground-1)],ramp,2)
        p.blob((min(w-6,hx0),ground-2,min(w-2,hx1+1),ground),ramp,pattern,seed)
        p.line([(x0+2,ground-1),(x0+1,ground),(x0+4,ground)],ramp,1)
        p.line([(x1-2,ground-1),(x1-1,ground),(x1+1,ground)],ramp,1)
        p.point(min(w-3,hx1),ground-1,ramp,0)
        return p.image
    if anim=='stand':
        p.blob((9,5,17,ground-1),ramp)
        p.blob((12,2+f,19,7+f),ramp)
        p.line([(12,ground-3),(11,ground),(13,ground)],ramp,1,2)
        p.line([(16,ground-3),(17,ground),(19,ground)],ramp,1,2)
        p.line([(13,7),(10-f,9)],ramp,1,2)
        p.point(18,4+f,'hide',0); p.point(19,6+f,'ui',4)
        return p.image
    moving=anim in {'walk','run'}
    stride=[(-1,1),(0,0),(1,-1),(0,1)][f%4] if moving else (0,0)
    stretch=2 if anim=='run' else 1
    rear=x0+2; front=x1-1
    for x,phase in [(rear,-stride[0]),(front,-stride[1])]:
        p.line([(x,y1-1),(x+phase*stretch,ground-1),(x+phase*stretch+1,ground)],ramp,0)
    if source=='horse':
        p.line([(x0,y0),(x0-3,y0+3),(x0-4,ground-3+(f%2 if moving else 0))],ramp,1,2)
    elif source in {'wolf','big_cat'}:
        p.line([(x0+1,y0+1),(x0-2,y0-1),(1,y0-2+(f%2))],ramp,1,2 if source=='wolf' else 1)
    else: p.line([(x0,y0+1),(x0-1,y0+3+f%2)],ramp,1)
    p.blob((x0,y0+bob,x1,y1),ramp,pattern,seed)
    if source=='sheep':
        for x in [3,5,8]: p.blob((x,y0-1+bob,x+3,y0+3),ramp,pattern,seed)
    if source=='bison' and name!='cattle': p.blob((5,3+bob,11,9),ramp,pattern,seed)
    if source=='camel':
        p.blob((5,7+bob,11,13),ramp,pattern,seed)
        p.blob((10,8+bob,15,13),ramp,pattern,seed)
    for x,phase in [(rear,stride[0]),(front,stride[1])]:
        if moving:
            knee=(x+phase*stretch,y1+2)
            p.line([(x,y1-1),knee,(x+phase*stretch,ground-1),(x+phase*stretch+1,ground)],ramp,1)
        else: p.line([(x,y1-1),(x,ground),(x+1,ground)],ramp,1,2 if source=='bear' else 1)
    if anim=='eat':
        hy0+=4+f; hy1+=4+f; hx0+=1; hx1+=1
    if anim in {'attack','howl'}:
        shift=-1 if anim=='howl' else [0,-1,1][f]
        hy0+=shift; hy1+=shift
        hx0+=f%2; hx1+=f%2
    if anim=='idle': hy0+=bob; hy1+=bob
    hy1=min(ground-1,hy1); hy0=min(hy0,hy1-2)
    hx1=min(w-3,hx1); hx0=min(hx0,hx1-1)
    p.line([(x1-1,y0+2),((x1+hx0)//2,(y0+hy0)//2+1),(hx0,hy1-1)],ramp,2,3 if w==24 else 2)
    p.blob((hx0,hy0,hx1,hy1),ramp,pattern,seed)
    if source in {'horse','camel'}:
        p.line([(hx0-1,hy0),(hx0-1,hy1+2)],ramp,0)
    if source=='boar':
        p.line([(hx1,hy1-1),(w-2,hy1-1)],ramp,1,2)
        p.point(w-3,hy1-2,'ui',4)
        for x in range(x0+2,x1): p.point(x,y0-1+bob,ramp,1)
    if source in {'wolf','big_cat','bear'}:
        p.line([(hx1,hy1-1),(min(w-2,hx1+1),hy1-1)],ramp,1,2)
        p.point(hx0,hy0-1,ramp,1)
        p.point(hx1-1,hy0-1,ramp,1)
        if not domestic: p.point(min(w-2,hx1+1),hy1,'ui',4)
        if anim=='attack':
            p.line([(hx1,hy1),(min(w-2,hx1+1),hy1)],'ui',4)
            p.point(hx1,hy1+1 if f==1 else hy1-1,ramp,0)
        elif anim=='howl':
            p.line([(hx1,hy0),(hx1,hy0-1-f)],ramp,1)
            p.point(hx1,hy0+1,'ui',4)
    if source in {'goat','deer','elk','bison'}:
        p.point(hx0-1,hy0,ramp,1)
        p.line([(hx0,hy0),(hx0-1,hy0-2)],'wood',3)
        if source=='goat':
            p.line([(hx0+1,hy0),(hx0+1,hy0-2),(hx0,hy0-3)],'wood',2)
            p.point(hx0+1,hy1+1,ramp,1)
        if source=='elk':
            for sign in [-1,1]:
                xx=hx0+sign
                p.line([(hx0,hy0),(xx,hy0-4),(xx+sign,hy0-6)],'wood',3)
                p.line([(xx,hy0-3),(xx+sign*2,hy0-4)],'wood',2)
    if source in {'horse','camel'}: p.point(hx0,hy0-1,ramp,1)
    p.point(hx1-1,hy0+1,ramp,0)
    if domestic and source=='wolf':
        p.line([(hx0,hy0),(hx0-1,hy0+2)],'hair',0)
    return p.image

def tiny(name,source,size,anim,f,pattern,seed,slots):
    p=Animal(size,slots); domestic=name in DOMESTIC; ramp='hair' if domestic else 'hide'
    if anim=='dead':
        p.blob((1,4,5,6),ramp,pattern,seed); p.point(6,6,ramp,1)
        if source=='rabbit': p.line([(4,5),(6,5)],ramp,2)
        else: p.point(5,4,'clay',3)
        return p.image
    moving=anim in {'walk','run'}
    bob=-1 if moving and (f in (1,2) if anim=='run' else f==1) else 0
    p.blob((1,3+bob,4,5),ramp,pattern,seed)
    if source=='rabbit':
        p.blob((4,3+(f%2 if anim=='idle' else 0),6,4+(f%2 if anim=='idle' else 0)),ramp,pattern,seed)
        p.line([(4,2),(4,1)],ramp,3)
        p.line([(6,2),(6,1 if f%2==0 else 2)],ramp,2)
        p.point(1,4,'hide',4)
        p.point(6,3+(f%2 if anim=='idle' else 0),ramp,0)
    else:
        p.line([(4,4),(4,2),(5,2)],ramp,2)
        p.point(6,2,'clay',3); p.point(5,1,ramp,1)
        p.line([(1,3),(1,2)],ramp,1)
        p.point(5,2,ramp,0)
        if domestic: p.point(4,1,'clay',2)
        if anim=='flap': p.line([(3,4),(2,1 if f==0 else 5)],ramp,3)
        elif anim=='idle': p.point(2,3-f,ramp,3)
    if moving:
        for x,offset in [(2,[-1,0,1,0][f]),(4,[1,0,-1,1][f])]:
            p.line([(x,5),(x+offset,5 if anim=='run' and f in (1,2) else 6)],ramp,1)
    else:
        p.point(2,6,ramp,1); p.point(4,6,ramp,1)
    return p.image

def aquatic(source,size,anim,f,slots):
    p=Animal(size,slots); w,h=size
    if source=='snake':
        for x in range(2,13):
            y=4+round(math.sin((x+f*2)*.65))
            p.point(x,y,'basalt',1); p.point(x,y-1,'basalt',3)
        if anim=='strike':
            p.line([(10,4),(12,2+f),(13,1+f)],'basalt',2,2)
            p.point(14,2+f,'ui',4)
        else:
            p.blob((12,2+f%2,14,4+f%2),'basalt')
            p.point(14,4+f%2,'ui',4)
        p.point(14,3 if anim!='strike' else 1+f,'basalt',0)
    elif source=='crocodile':
        if anim=='dead':
            p.blob((3,4,18,6),'basalt'); p.line([(17,6),(22,6)],'basalt',1); return p.image
        p.line([(1,4),(6,3),(6,5)],'basalt',1,2)
        p.blob((5,2,17,5),'basalt')
        jawy=3-(f%2 if anim=='attack' else 0)
        p.line([(15,3),(21,jawy),(22,jawy+1)],'basalt',2,2)
        p.line([(16,5),(22,5)],'basalt',1)
        for x in [7,12,15]: p.point(x,1,'basalt',1)
        for i,x in enumerate([6,14]):
            offset=([[-1,0,1,0],[1,0,-1,1]][i][f]) if anim=='crawl' else f%2
            p.line([(x,4),(x+offset,6),(x+offset+1,6)],'basalt',1)
        p.point(17,2,'basalt',0)
        for x in [19,21]: p.point(x,4,'ui',4)
        if anim=='attack' and f==2: p.point(20,2,'basalt',3)
    return p.image

def ambient(source,size,f,seed,slots):
    p=Animal(size,slots)
    if source=='bird_flock':
        for x,y in [(4,4),(10,3),(7,9),(12,11)]:
            bend=[-2,0,2,1][f]
            p.line([(x-2,y+bend),(x,y),(x+2,y+bend)],'basalt',2)
            p.point(x+1,y-1,'basalt',1)
    else:
        for i,(x,y) in enumerate([(4,3),(10,5),(5,9),(11,12)]):
            shift=[-1,0,1,0][f]
            x+=shift
            p.line([(x-1,y),(x+1,y)],'shadow',1,2)
            p.line([(x-2,y),(x-3,y+(-1 if f<2 else 1))],'shadow',2)
            p.point(x+1,y,'shadow',2 if f==3 else 1)
    return p.image

def build(seed=0):
    palette=json.loads((ROOT/'assets/palette/palette.json').read_text())
    slots={s['name']:s['index'] for s in palette['slots']}
    lut=np.zeros((256,4),dtype=np.uint8)
    for s in palette['slots']: lut[s['index']]=[*s['reference'],255]
    out=ROOT/'assets/fauna'; out.mkdir(parents=True,exist_ok=True); entries=[]
    for name,source,size,anim,frames,count in contracts():
        w,h=size; idx=np.zeros((h*count,w*frames,4),dtype=np.uint8)
        for v in range(count):
            for f in range(frames):
                if source in {'bird_flock','fish_school'}: image=ambient(source,size,f,seed,slots)
                elif source in {'rabbit','fowl'}: image=tiny(name,source,size,anim,f,v,seed,slots)
                elif source in {'snake','crocodile'}: image=aquatic(source,size,anim,f,slots)
                else: image=quadruped(name,source,size,anim,f,v,seed,slots)
                indices=np.array(image); tile=np.zeros((h,w,4),dtype=np.uint8)
                tile[:,:,0]=np.where(indices==255,0,indices); tile[:,:,3]=np.where(indices==255,0,255)
                idx[v*h:(v+1)*h,f*w:(f+1)*w]=tile
        ref=lut[idx[:,:,0]].copy(); ref[:,:,3]=idx[:,:,3]; ref[idx[:,:,3]==0]=0
        ident=f'{name}_{anim}'
        Image.fromarray(ref).save(out/f'{ident}.png'); Image.fromarray(idx).save(out/f'{ident}_idx.png')
        used=set(idx[idx[:,:,3]>0,0].tolist())
        e={'id':f'fauna.{ident}','family':'fauna','asset_id':name.removesuffix('_domestic'),
           'form':'domestic' if name in DOMESTIC else 'wild','source_species':source,
           'files':[f'fauna/{ident}.png',f'fauna/{ident}_idx.png'],'sidecar':f'fauna/{ident}.json',
           'frame':size,'frames':frames,'directions':['side'],'mirror_left':True,
           'sheet_rows':[f'coat_{v}' for v in range(count)],'fps':FPS[anim],
           'pivot':[w//2,h//2 if source in {'bird_flock','fish_school'} else h-2],
           'anims':{anim:{'row':0,'frames':frames,'fps':FPS[anim]}},'animation':anim,'seed':seed,
           'recolor_slots':[s['name'] for s in palette['slots'] if s['index'] in used],
           'tags':['fauna','predator' if source in PREDATORS and name!='dog' else 'ambient' if source in {'bird_flock','fish_school'} else 'animal'],
           'shadow':'engine','shadow_size':'S' if w==8 else ('L' if w==24 else 'M')}
        if name in DOMESTIC: e['coat_lut']='lut_hair'
        (out/f'{ident}.json').write_text(json.dumps(e,indent=2)+'\n',encoding='utf-8'); entries.append(e)
    return entries
