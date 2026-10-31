"""Detailed modular art at three densities on the unchanged logical grid."""
import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
from .terrain import stable
from .civilisation_expansion import WALLS as EXTRA_WALLS, FLOORS as EXTRA_FLOORS, ROOFS as EXTRA_ROOFS, FOODS, OBJECTS as EXTRA_OBJECTS, NATURE, VEHICLES, food, fixture, nature, vehicle

ROOT=Path(__file__).resolve().parents[3]
DENSITIES=(16,32,64)
FLOORS={'earth':'soil','reed_mat':'thatch','plank':'wood','flagstone':'stone','concrete':'rock','metal_deck':'metal_iron','grass':'grass','packed_path':'soil'}
WALLS={'wattle':'thatch','wood':'wood','mudbrick':'mudbrick','stone':'stone','fired_brick':'clay','palisade':'bark','metal':'metal_iron','glass':'ice'}
FLOORS.update(EXTRA_FLOORS); WALLS.update(EXTRA_WALLS)
ROOFS={'thatch':'thatch','tile':'clay','metal':'metal_iron',**EXTRA_ROOFS}
CONTAINERS={
    'basket':[(18,24),(30,24),(18,34),(30,34)],
    'chest':[(12,24),(24,24),(36,24),(12,35),(24,35),(36,35)],
    'backpack':[(22,27),(28,34)],
    'handcart':[(12,20),(24,20),(36,20),(12,30),(24,30),(36,30),(18,35),(30,35)],
    'grain_bin':[(12+12*x,18+10*y) for y in range(3) for x in range(3)],
    'crate':[(16,22),(30,22),(16,34),(30,34)],
    'pallet':[(10+14*x,20+12*y) for y in range(2) for x in range(3)],
    'cargo_pod':[(10+14*x,20+10*y) for y in range(3) for x in range(3)]}
RESOURCES={'grain':'thatch','log':'wood','stone':'stone','clay':'clay','copper_ore':'metal_copper','iron_ingot':'metal_iron','cloth':'cloth_a','medicine':'blight','food':'fruit','components':'metal_copper'}
RESOURCES.update(FOODS)
OBJECTS=['bed','stool','table','workbench','shelf','knapping_block','chopping_block','loom','potter_wheel','millstone','kiln','forge','anvil','lathe','console','generator','solar_panel','robot_arm','biotech_vat','water_barrel','hearth','lamp','grain_sack','pot','oven','wash_basin','door_wood_open','door_wood_closed','door_metal_open','door_metal_closed','hearth_unlit','kiln_unlit','forge_unlit','oven_unlit']
ANIMATED={'hearth','kiln','forge','oven','generator','biotech_vat'}
OUTDOOR={'tree_broadleaf':([64,128],[16,32]),'tree_conifer':([64,128],[16,32]),'crop_grain_detail':([64,64],[16,16]),'boulder_detail':([64,64],[16,16])}
OBJECTS.extend(EXTRA_OBJECTS); ANIMATED.add('brazier'); OUTDOOR.update(NATURE)

class Pixels:
    def __init__(self,slots,size=64):
        self.slots=slots; self.im=Image.new('L',(size,size) if isinstance(size,int) else tuple(size),255); self.d=ImageDraw.Draw(self.im)

    def ink(self,ramp,shade):
        group=ramp if ramp in {'bark','grass','foliage','flower','skin','hair','cloth_a','cloth_b','culture','fruit'} else 'fixed_'+ramp
        shade=min(shade,2 if ramp=='culture' else 3 if ramp in {'hair','cloth_a','cloth_b','fruit'} else 4)
        return self.slots[f'{group}_{shade}']

    def rect(self,box,ramp,shade): self.d.rectangle(box,fill=self.ink(ramp,shade))
    def poly(self,points,ramp,shade): self.d.polygon(points,fill=self.ink(ramp,shade))
    def ellipse(self,box,ramp,shade): self.d.ellipse(box,fill=self.ink(ramp,shade))
    def line(self,points,ramp,shade,width=1): self.d.line(points,fill=self.ink(ramp,shade),width=width)

    def box(self,x,y,w,h,ramp='wood'):
        self.rect((x,y,x+w-1,y+h-1),ramp,1)
        if w>=5 and h>=5: self.rect((x+2,y+2,x+w-3,y+h-3),ramp,2)
        self.line([(x,y),(x+w-2,y)],ramp,4,2)
        self.line([(x,y),(x,y+h-2)],ramp,3,2)
        self.line([(x+w-1,y+2),(x+w-1,y+h-1)],ramp,0,2)

    def boards(self,box,ramp='wood',vertical=True):
        x0,y0,x1,y1=box
        self.box(x0,y0,x1-x0+1,y1-y0+1,ramp)
        if vertical:
            for x in range(x0+8,x1-1,8):
                self.line([(x,y0+3),(x,y1-2)],ramp,1)
                self.line([(x+2,y0+4),(x+2,y1-4)],ramp,3)
        else:
            for y in range(y0+8,y1-1,8): self.line([(x0+3,y),(x1-2,y)],ramp,1)

    def flame(self,x,y,frame,height=18):
        shift=[-4,0,4,0][frame]; h=height+[0,4,-3,1][frame]
        self.poly([(x-9,y),(x-8,y-7),(x-3,y-10),(x+shift,y-h),(x+5,y-7),(x+9,y-3),(x+7,y)],'fire',1)
        self.poly([(x-5,y-1),(x-2,y-8),(x+shift,y-h+7),(x+4,y-6),(x+5,y-1)],'fire',3)
        cx=x+[-4,0,4,0][frame]; cy=y-(8 if frame==3 else 4)
        self.rect((cx,cy,cx+3,cy+3),'fire',4)

def texture(p,seed,name):
    a=np.array(p.im); rng=np.random.default_rng(stable(name)+seed)
    reverse={v:k for k,v in p.slots.items()}
    for _ in range(35):
        x=int(rng.integers(5,a.shape[1]-5)); y=int(rng.integers(5,a.shape[0]-5))
        if a[y,x]==255: continue
        key=reverse[int(a[y,x])]; ramp,shade=key.rsplit('_',1)
        if ramp.endswith(('wood','thatch','soil','stone','clay','bark')) and int(shade) in {2,3}:
            a[y,x]=p.slots[ramp+'_'+str(3 if x<32 else 1)]
    p.im=Image.fromarray(a); p.d=ImageDraw.Draw(p.im)
    return p.im

def floor(material,slots,seed):
    p=Pixels(slots); r=FLOORS[material]; p.rect((0,0,63,63),r,2)
    if material in {'plank','bamboo'}:
        for y in range(0,64,16):
            p.line([(0,y),(63,y)],r,1,2); p.line([(0,y+2),(63,y+2)],r,3)
            for x in range(8+(y//16%2)*24,64,48):
                p.line([(x,y+3),(x,y+15)],r,1); p.rect((x+3,y+5,x+4,y+6),r,0)
    elif material in {'flagstone','concrete','metal_deck','sandstone','granite','basalt','ceramic_tile','ice','composite'}:
        for y in range(0,64,32):
            p.line([(0,y),(63,y)],r,0,2)
            for x in range(0,64,32):
                p.line([(x,y),(x,y+31)],r,0,2)
                p.line([(x+2,y+2),(x+29,y+2)],r,3)
                if material in {'metal_deck','composite'}:
                    for dx in range(6,30,8): p.line([(x+dx,y+8),(x+dx,y+24)],r,1,2)
    elif material=='reed_mat':
        for y in range(0,64,4): p.line([(0,y),(63,y)],r,1 if y%8 else 3)
        for x in range(2,64,8):
            for y in range(2,64,8): p.rect((x,y,x+2,y+3),r,3)
    elif material=='grass':
        rng=np.random.default_rng(stable('grass_detail')+seed)
        for _ in range(42):
            x,y=map(int,rng.integers(4,59,size=2)); shade=int(rng.choice([1,2,3]))
            p.line([(x,y+3),(x-1,y)],'grass',shade)
            p.line([(x+1,y+3),(x+3,y+1)],'grass',shade)
    a=np.array(texture(p,seed,'floor_'+material)); a[-1]=a[0]; a[:,-1]=a[:,0]
    return Image.fromarray(a)

def wall(material,mask,slots):
    p=Pixels(slots); r=WALLS[material]
    cap=Image.new('L',(64,64)); d=ImageDraw.Draw(cap); d.rectangle((24,26,39,37),fill=1)
    for bit,box in [(1,(24,0,39,31)),(2,(32,26,63,37)),(4,(24,32,39,63)),(8,(0,26,31,37))]:
        if mask&bit: d.rectangle(box,fill=1)
    top=np.array(cap,dtype=bool); a=np.full((64,64),255,dtype=np.uint8)
    for shift in range(14,0,-1):
        down=np.zeros_like(top); down[shift:]=top[:-shift]; a[down]=p.ink(r,1 if shift>10 else 2)
    a[top]=p.ink(r,3)
    left=np.zeros_like(top); left[:,1:]=top[:,:-1]; left[:,0]=top[:,0] if mask&8 else False
    right=np.zeros_like(top); right[:,:-1]=top[:,1:]; right[:,-1]=top[:,-1] if mask&2 else False
    a[top&~left]=p.ink(r,4); a[top&~right]=p.ink(r,0)
    p.im=Image.fromarray(a); p.d=ImageDraw.Draw(p.im)
    if material in {'stone','mudbrick','fired_brick'}:
        for x in [12,32,52]:
            if a[41,x]!=255: p.line([(x,39),(x,47)],r,0)
    elif material in {'wood','wattle','palisade'}:
        for x in range(6,60,6):
            if a[42,x]!=255: p.line([(x,39),(x,47)],r,0)
    elif material=='metal':
        for x in [12,50]:
            if a[42,x]!=255: p.rect((x,41,x+1,42),r,4)
    a=np.array(p.im)
    for y,x in np.argwhere(top):
        if not (4<x<59 and 4<y<59) or a[y,x]!=p.ink(r,3): continue
        if material in EXTRA_WALLS:
            if material=='bamboo' and (x%5==0 or y%13 in {1,2}): a[y,x]=p.ink(r,2)
            elif material=='reed' and (x//2+y//4)%2==0: a[y,x]=p.ink(r,2)
            elif material=='hide' and (x%11==0 or y%13==0): a[y,x]=p.ink(r,1)
            elif material=='rammed_earth' and y%5==0: a[y,x]=p.ink(r,2)
            elif material in {'adobe','sandstone','limestone','concrete','ceramic'}:
                period={'adobe':9,'sandstone':19,'limestone':21,'concrete':29,'ceramic':7}[material]
                if y%period==0 or (x+(y//period%2)*9)%period==0: a[y,x]=p.ink(r,2)
            elif material in {'granite','basalt','snow'} and stable(f'{material}:{x}:{y}')%11==0: a[y,x]=p.ink(r,2 if material!='snow' else 4)
            elif material=='ice' and (x+y)%17 in {0,1}: a[y,x]=p.ink(r,4)
            elif material=='composite' and (x//4+y//4)%2==0: a[y,x]=p.ink(r,2)
            elif material=='salvage' and (x%9==0 or y%15==0): a[y,x]=p.ink(r,1)
        elif material=='wattle':
            if (x//3+y//3)%2==0: a[y,x]=p.ink(r,2)
            elif x%3==1: a[y,x]=p.ink(r,4)
        elif material in {'wood','palisade'}:
            if x%7 in {0,1} and y%17 in range(3,13): a[y,x]=p.ink(r,2)
            elif x%11==2 and y%19 in range(6,12): a[y,x]=p.ink(r,4)
        elif material in {'mudbrick','stone','fired_brick'}:
            period=16 if material=='stone' else 12
            if y%period==0 or (x+(y//period%2)*8)%16==0: a[y,x]=p.ink(r,2)
        elif material=='metal':
            if (x%16==3 and y%16==3): a[y:y+2,x:x+2]=p.ink(r,0)
            elif x%16==6: a[y,x]=p.ink(r,2)
        elif material=='glass' and (x+y)%23 in {0,1}: a[y,x]=p.ink(r,4)
    p.im=Image.fromarray(a)
    return p.im

def roof(material,slots,seed):
    p=Pixels(slots); r=ROOFS[material]
    p.rect((0,0,63,63),r,2)
    for y in range(0,64,16):
        p.line([(0,y+14),(63,y+14)],r,0,2)
        for x in range(-8+(y//16%2)*8,64,16):
            if material in {'thatch','reed','bark'}:
                for dx in [2,6,10]: p.line([(x+dx,y+2),(x+dx-2,y+12)],r,3,2)
            else:
                p.line([(x,y),(x,y+13)],r,1,2); p.line([(x+3,y+2),(x+3,y+11)],r,3,2)
    a=np.array(texture(p,seed,'roof_'+material)); a[-1]=a[0]; a[:,-1]=a[:,0]
    return Image.fromarray(a)

def container(kind,layer,slots,seed):
    p=Pixels(slots); metal=kind=='cargo_pod'; r='metal_iron' if metal else ('hide' if kind=='backpack' else 'wood')
    if kind=='basket':
        if layer=='back':
            p.ellipse((13,19,50,53),'thatch',1); p.ellipse((14,17,49,45),'thatch',3)
            p.ellipse((18,21,45,43),'thatch',0); p.ellipse((19,23,44,42),'thatch',1)
        elif layer=='front':
            p.poly([(14,40),(49,40),(47,49),(40,53),(24,53),(16,49)],'thatch',2)
            for y in [42,46,50]: p.line([(18,y),(45,y)],'thatch',3)
            for x in range(19,45,5): p.line([(x,41),(x+2,50)],'thatch',1)
            p.line([(15,40),(48,40)],'thatch',4,2)
        else:
            p.ellipse((14,19,49,44),'thatch',2)
            for y in range(24,44,4): p.line([(19,y),(44,y)],'thatch',3)
            p.line([(27,23),(37,23)],'thatch',0,2)
        return texture(p,seed,kind+'_'+layer)
    if kind=='backpack':
        if layer=='back':
            p.line([(20,26),(13,31),(15,49),(21,47)],'hide',0,3)
            p.line([(43,26),(49,32),(48,48),(42,48)],'hide',0,3)
            p.d.rounded_rectangle((18,18,45,55),radius=6,fill=p.ink('hide',2))
            p.line([(20,27),(20,45)],'hide',3,2)
            p.rect((22,25,41,44),'hide',0)
        elif layer=='front':
            p.d.rounded_rectangle((19,43,44,55),radius=4,fill=p.ink('hide',1))
            p.box(24,46,15,8,'hide'); p.rect((29,47,33,51),'metal_copper',2)
        else:
            p.d.rounded_rectangle((19,20,44,44),radius=4,fill=p.ink('hide',2))
            p.poly([(20,37),(43,37),(33,45),(28,45)],'hide',3)
            p.line([(25,23),(38,23)],'hide',0,2)
        return texture(p,seed,kind+'_'+layer)
    bounds={'basket':(14,21,49,51),'chest':(8,18,55,53),'backpack':(18,19,45,55),'handcart':(7,15,56,51),'grain_bin':(7,13,56,53),'crate':(12,16,51,53),'pallet':(6,17,57,53),'cargo_pod':(5,13,58,55)}[kind]
    x0,y0,x1,y1=bounds
    if layer=='back':
        if kind=='handcart':
            for x in [2,52]:
                p.ellipse((x,32,x+9,59),'wood',0); p.ellipse((x+2,35,x+7,56),'wood',2)
                p.rect((x+3,43,x+6,48),'metal_iron',2)
            p.line([(17,47),(12,61)],'wood',1,3); p.line([(46,47),(51,61)],'wood',1,3)
        if kind=='backpack':
            p.line([(20,26),(13,31),(15,49),(21,47)],'hide',0,3)
            p.line([(43,26),(49,32),(48,48),(42,48)],'hide',0,3)
        p.boards((x0,y0,x1,y1),r,vertical=kind not in {'basket','backpack','cargo_pod'})
        p.rect((x0+4,y0+6,x1-4,y1-4),r,0)
        p.rect((x0+6,y0+8,x1-6,y1-6),r,1)
        if kind=='basket':
            for y in range(y0+2,y1-2,4): p.line([(x0+2,y),(x1-2,y)],'thatch',3)
            p.rect((x0+5,y0+5,x1-5,y1-5),'thatch',1)
        if kind in {'grain_bin','chest','cargo_pod'}:
            p.rect((x0+2,y0+1,x0+5,y1-1),r,3); p.rect((x1-5,y0+1,x1-2,y1-1),r,1)
        if kind=='pallet':
            p=Pixels(slots)
            for y in [24,34,44]: p.boards((6,y,57,y+7),vertical=False)
            for x in [10,44]: p.rect((x,50,x+9,57),'wood',0)
        if kind=='cargo_pod':
            p.poly([(5,13),(10,13),(5,18)],'metal_iron',0)
            p.poly([(54,13),(58,13),(58,18)],'metal_iron',0)
            p.rect((9,15,12,20),'ice',3); p.rect((51,15,54,20),'ice',2)
    elif layer=='front':
        p.boards((x0,y1-(4 if kind=='pallet' else 10),x1,y1),r,vertical=kind in {'crate','grain_bin'})
        if kind=='basket':
            for y in range(y1-8,y1,3): p.line([(x0+2,y),(x1-2,y)],'thatch',3)
        if kind in {'chest','backpack','cargo_pod'}:
            p.box(29,y1-9,6,7,'metal_copper' if not metal else 'ice')
        if kind=='cargo_pod':
            p.rect((x0+4,y1-6,x0+10,y1-4),'ice',3)
        if kind=='crate': p.line([(x0+3,y1-2),(x1-3,y1-8)],'wood',3,3)
    else:
        p.boards((x0+1,y0+1,x1-1,y1-(5 if kind=='pallet' else 10)),r,vertical=False)
        if kind=='basket':
            for y in range(y0+4,y1-12,4): p.line([(x0+4,y),(x1-4,y)],'thatch',3)
        p.line([(24,y0+4),(39,y0+4)],r,0,3)
        p.line([(25,y0+3),(38,y0+3)],r,4)
    return texture(p,seed,kind+'_'+layer)

def token(kind,slots):
    p=Pixels(slots,16); r=RESOURCES[kind]
    if kind in FOODS: return food(kind,p)
    if kind=='grain':
        p.poly([(2,9),(5,4),(11,4),(14,9),(12,13),(3,13)],r,1)
        for x,y in [(4,9),(7,7),(10,6),(11,10),(6,11)]:
            p.rect((x,y-1,x+1,y+1),r,3)
    elif kind=='log':
        p.poly([(2,5),(11,3),(14,9),(5,13),(2,10)],r,1)
        p.ellipse((2,6,7,13),r,3); p.ellipse((3,8,5,11),r,1)
        p.line([(7,6),(11,5)],r,3)
    elif kind in {'cloth','medicine','components'}:
        p.box(2,4,12,10,r); p.line([(4,6),(10,6)],r,3)
        if kind=='medicine': p.ellipse((5,6,10,10),r,4)
    elif kind=='iron_ingot':
        p.poly([(2,6),(10,3),(14,6),(13,11),(5,14),(2,11)],r,1)
        p.poly([(2,6),(10,3),(14,6),(5,10)],r,3)
        p.line([(5,11),(13,7)],r,0)
    else:
        p.poly([(2,8),(5,3),(10,2),(14,8),(11,13),(4,13)],r,2)
        p.poly([(3,7),(5,3),(10,3),(8,7)],r,3)
        p.line([(11,5),(13,8),(11,12)],r,0)
    return p.im

def object_art(kind,slots,seed,frame=0):
    p=Pixels(slots)
    if kind in EXTRA_OBJECTS:
        fixture(kind,p,frame); return texture(p,seed,kind)
    if kind.endswith('_unlit'):
        source=kind.removesuffix('_unlit'); a=np.array(object_art(source,slots,seed))
        fire=np.isin(a,[p.ink('fire',i) for i in range(5)])
        a[fire]=p.ink('ash',1) if source=='hearth' else p.ink('stone' if source=='forge' else 'clay',0)
        return Image.fromarray(a)
    if kind.startswith('door_'):
        r='metal_iron' if 'metal' in kind else 'wood'
        p.box(12,12,6,46,r); p.box(47,12,6,46,r); p.box(12,10,41,7,r)
        if kind.endswith('_closed'):
            p.boards((18,17,46,56),r); p.rect((40,34,43,38),'metal_copper',3)
        else:
            p.poly([(18,17),(28,23),(28,57),(18,52)],r,1)
            p.line([(19,18),(27,24),(27,54)],r,3,2)
            p.rect((24,38,26,41),'metal_copper',3)
        p.line([(17,58),(48,58)],'stone',2,2)
    elif kind=='bed':
        p.boards((11,13,52,58)); p.rect((15,17,48,54),'hide',2)
        p.box(16,18,31,10,'hide'); p.rect((15,31,48,53),'cloth_a',2)
        for x in [20,39]: p.line([(x,32),(x+2,50)],'cloth_a',3,2)
        p.rect((15,50,48,53),'cloth_a',0)
    elif kind in {'table','workbench','lathe','potter_wheel'}:
        for x,y in [(12,31),(47,31),(12,49),(47,49)]: p.rect((x,y,x+4,y+10),'wood',0)
        p.boards((8,21,55,47),vertical=False)
        if kind=='workbench':
            p.box(13,24,13,13,'stone'); p.line([(33,27),(46,40)],'wood',0,4)
            p.box(34,23,12,7,'metal_iron'); p.rect((11,42,20,49),'wood',1)
        elif kind=='potter_wheel':
            p.ellipse((17,24,46,43),'stone',1); p.ellipse((18,22,45,38),'stone',3)
            p.ellipse((25,17,38,32),'clay',1); p.ellipse((26,16,37,27),'clay',3)
            p.ellipse((29,17,34,21),'clay',0)
        elif kind=='lathe':
            p.box(11,26,42,16,'metal_iron'); p.box(13,22,9,14,'metal_iron')
            p.rect((22,25,43,29),'metal_copper',2); p.box(44,22,7,13,'metal_iron')
            p.ellipse((30,35,39,44),'metal_iron',0); p.ellipse((32,37,37,42),'metal_iron',3)
    elif kind=='stool':
        for x in [19,41]: p.line([(x,37),(x-2,53)],'wood',0,4)
        p.ellipse((16,23,47,42),'wood',1); p.ellipse((16,21,47,36),'wood',3)
        p.line([(24,25),(40,25)],'wood',2)
    elif kind=='shelf':
        p.boards((9,8,54,55))
        for y in [17,31,45]:
            p.rect((14,y-3,49,y+4),'wood',0); p.box(11,y+4,42,5,'wood')
        for x,y in [(18,13),(34,27),(22,41)]:
            p.ellipse((x,y-3,x+10,y+4),'clay',2); p.line([(x+1,y-3),(x+7,y-3)],'clay',4)
    elif kind in {'knapping_block','chopping_block','millstone','anvil'}:
        p.ellipse((12,30,51,57),'wood' if kind=='chopping_block' else 'stone',1)
        p.ellipse((12,23,51,48),'wood' if kind=='chopping_block' else 'stone',3)
        if kind=='chopping_block':
            p.ellipse((18,27,45,43),'wood',2); p.line([(27,30),(32,35),(28,40)],'wood',0)
            p.line([(30,32),(46,14)],'wood',1,4); p.poly([(40,13),(51,12),(53,20),(44,23)],'stone',2)
        elif kind=='millstone':
            p.ellipse((17,20,46,39),'stone',2); p.ellipse((27,25,35,31),'stone',0)
            p.line([(32,28),(45,16)],'wood',2,4)
        elif kind=='anvil':
            p.box(23,25,21,19,'metal_iron'); p.poly([(9,23),(19,16),(48,16),(54,22),(43,28),(21,28)],'metal_iron',1)
            p.line([(19,17),(47,17)],'metal_iron',4,3)
        else:
            p.poly([(22,26),(29,17),(39,21),(41,31),(32,36)],'rock',0)
            p.poly([(23,25),(29,18),(34,21),(29,28)],'rock',3)
            p.ellipse((43,42,49,47),'rock',2)
    elif kind=='loom':
        for x in [10,49]: p.box(x,8,5,49,'wood')
        p.box(9,9,47,6,'wood'); p.box(9,48,47,6,'wood')
        for x in range(18,48,4): p.line([(x,16),(x,46)],'thatch',3)
        p.rect((17,29,47,45),'cloth_a',2)
        for y in [33,39]: p.line([(18,y),(46,y)],'cloth_a',3,2)
        p.line([(19,25),(45,30)],'wood',0,3); p.box(24,36,17,4,'wood')
    elif kind in {'kiln','oven','forge','hearth'}:
        if kind=='hearth':
            p.ellipse((7,25,56,56),'ash',1)
            for x,y in [(13,32),(24,27),(42,27),(51,36),(47,48),(29,53),(15,47)]:
                p.ellipse((x-5,y-3,x+5,y+4),'stone',2); p.line([(x-3,y-3),(x+2,y-3)],'stone',4)
            p.line([(21,39),(42,47)],'wood',1,5); p.line([(21,46),(41,37)],'wood',2,4)
            p.flame(32,46,frame)
        else:
            r='clay' if kind!='forge' else 'stone'
            p.ellipse((8,12,55,57),r,1); p.ellipse((9,8,52,48),r,2)
            p.line([(15,23),(18,15),(28,11)],r,4,2)
            for y in [24,34]: p.line([(12,y),(50,y)],r,1)
            p.ellipse((20,31,44,51),r,0)
            p.flame(32,48,frame,14)
            p.box(16,50,33,6,'stone')
            if kind=='forge':
                p.box(39,3,12,30,'stone'); p.rect((43,6,48,9),'stone',0)
                p.line([(50,15),(50,29)],'stone',1,2)
            if kind=='oven': p.ellipse((25,45,39,50),'thatch',3)
    elif kind in {'console','generator','biotech_vat'}:
        p.box(10,40,44,16,'metal_iron')
        if kind=='console':
            p.box(14,10,37,31,'metal_iron'); p.box(18,14,28,21,'ice')
            p.rect((21,17,42,31),'ice',0)
            for y,length in [(20,12),(24,17),(28,8)]: p.line([(23,y),(23+length,y)],'ice',3,2)
            for x in range(16,49,6): p.rect((x,45,x+3,47),'metal_iron',0)
            p.rect((42,51,48,53),'culture',2)
        elif kind=='generator':
            p.box(14,18,35,29,'metal_iron')
            for x in range(19,46,6): p.line([(x,23),(x,39)],'metal_iron',0,3)
            p.ellipse((23,26,40,43),'metal_iron',1)
            p.line([(31,28),(31+[5,0,-5,0][frame],34+[0,5,0,-5][frame])],'metal_copper',3,3)
            p.box(17,9,12,12,'metal_copper')
        else:
            p.ellipse((17,9,47,49),'ice',1); p.rect((19,18,45,39),'ice',2)
            p.ellipse((19,9,45,20),'metal_iron',3); p.ellipse((19,38,45,49),'metal_iron',1)
            p.rect((23,22,40,35),'blight',2)
            p.ellipse((27+[-6,-2,2,6][frame],24,35+[-6,-2,2,6][frame],32),'blight',4)
            p.line([(21,19),(21,37)],'ice',4,2)
    elif kind=='solar_panel':
        p.line([(20,41),(16,56)],'metal_iron',0,4); p.line([(47,38),(49,54)],'metal_iron',0,4)
        p.poly([(8,16),(49,11),(57,42),(15,48)],'metal_iron',3)
        p.poly([(12,19),(46,15),(52,39),(18,43)],'ice',0)
        for y in [23,30,37]: p.line([(16,y),(48,y-4)],'ice',2,2)
        for x in [24,35,45]: p.line([(x,18),(x+5,40)],'ice',1,2)
    elif kind=='robot_arm':
        p.ellipse((12,40,51,57),'metal_iron',1); p.ellipse((14,37,49,49),'metal_iron',3)
        p.line([(31,42),(25,25),(42,14)],'metal_iron',0,10)
        p.line([(30,39),(25,25),(41,15)],'metal_copper',2,6)
        for x,y in [(30,40),(25,25),(42,14)]:
            p.ellipse((x-4,y-4,x+4,y+4),'metal_iron',2); p.rect((x-1,y-1,x+1,y+1),'metal_iron',4)
        p.line([(43,15),(51,21),(47,29)],'metal_iron',1,3)
        p.line([(43,15),(44,23),(39,27)],'metal_iron',1,3)
    elif kind in {'water_barrel','pot','wash_basin','grain_sack'}:
        r={'water_barrel':'wood','pot':'clay','wash_basin':'stone','grain_sack':'hide'}[kind]
        p.ellipse((15,17,49,57),r,1); p.ellipse((16,12,47,51),r,2)
        p.line([(20,25),(20,40)],r,3,2)
        if kind=='water_barrel':
            for y in [26,44]: p.line([(16,y),(48,y)],'metal_iron',1,3)
        if kind=='grain_sack':
            p.ellipse((22,15,42,24),'hide',0); p.rect((23,13,41,16),'hide',3)
            p.line([(27,24),(25,39)],'hide',1,2)
        else:
            p.ellipse((17,13,46,26),r,3); p.ellipse((21,16,42,23),'water' if kind!='pot' else 'clay',1)
    elif kind=='lamp':
        p.box(22,49,22,7,'wood'); p.line([(32,49),(32,18)],'wood',1,4)
        p.box(23,10,20,20,'metal_copper'); p.rect((27,14,38,25),'fire',3)
        p.rect((29,16,33,22),'fire',4)
    return texture(p,seed,kind)

def person(slots):
    p=Pixels(slots)
    p.line([(24,45),(21,58)],'cloth_a',0,7); p.line([(38,45),(42,58)],'cloth_a',1,7)
    p.rect((16,57,26,60),'hide',0); p.rect((38,57,48,60),'hide',0)
    p.poly([(20,27),(37,26),(43,43),(35,49),(21,46),(17,37)],'cloth_a',2)
    p.line([(22,28),(20,39)],'cloth_a',3,3); p.line([(36,30),(39,42)],'cloth_a',0,3)
    p.line([(18,32),(12,42),(18,48)],'skin',2,5); p.line([(40,32),(46,40),(40,47)],'skin',1,5)
    p.ellipse((20,9,39,30),'skin',2); p.ellipse((18,8,38,22),'hair',1)
    p.poly([(19,17),(20,9),(30,5),(39,11),(39,17),(31,14),(25,18)],'hair',2)
    p.line([(22,11),(28,8),(34,10)],'hair',3,2)
    p.rect((23,20,25,22),'skin',0); p.rect((33,20,35,22),'skin',0)
    p.line([(27,27),(32,27)],'skin',0); p.rect((28,22,30,24),'skin',3)
    p.line([(22,44),(38,43)],'hide',0,3); p.rect((29,43,33,45),'metal_copper',3)
    return p.im

def outdoor(kind,slots,seed):
    size,_=OUTDOOR[kind]; p=Pixels(slots,size); rng=np.random.default_rng(stable(kind)+seed)
    if kind in NATURE:
        nature(kind,p,seed); return texture(p,seed,kind)
    if kind.startswith('tree_'):
        p.poly([(29,66),(36,65),(36,111),(44,124),(36,122),(30,124),(23,122),(28,113)],'bark',1)
        p.line([(30,78),(30,113),(27,120)],'bark',3,3)
        p.line([(35,80),(34,115)],'bark',0,2)
        if kind=='tree_broadleaf':
            p.line([(31,96),(19,73),(13,59)],'bark',1,6)
            p.line([(34,86),(46,69)],'bark',1,5)
            clusters=[(31,30,21),(16,45,14),(47,43,14),(30,60,21),(13,64,11),(47,65,12),(31,76,15)]
            for x,y,r in sorted(clusters,key=lambda c:c[1]):
                p.ellipse((x-r,y-r//2,x+r,y+r),'foliage',0)
                p.ellipse((x-r,y-r//2,x+r-3,y+r-4),'foliage',2)
                p.ellipse((x-r+3,y-r//2+2,x+3,y+4),'foliage',3)
        else:
            for y,r in [(83,27),(65,23),(47,18),(30,12)]:
                p.poly([(32,y-26),(32+r,y+13),(38,y+10),(31,y+16),(24,y+10),(32-r,y+13)],'foliage',0)
                p.poly([(32,y-26),(31,y+9),(25,y+6),(32-r+2,y+8)],'foliage',2)
                p.line([(31,y-17),(24,y+3),(31,y+1)],'foliage',3,2)
                p.poly([(33,y-18),(32+r-4,y+7),(37,y+4)],'foliage',1)
        a=np.array(p.im)
        for _ in range(80):
            x=int(rng.integers(5,59)); y=int(rng.integers(12,97))
            if a[y,x] in [p.ink('foliage',i) for i in [2,3]]:
                a[y:y+2,x:x+2]=p.ink('foliage',4 if x<32 else 1)
        p.im=Image.fromarray(a)
    elif kind=='crop_grain_detail':
        for row in range(3):
            for col in range(4):
                x=10+col*13+int(rng.integers(-2,3)); y=30+row*11
                p.line([(x,y+12),(x+1,y-16)],'thatch',1,2)
                p.line([(x,y+5),(x-6,y-1)],'grass',2,2)
                p.line([(x,y+3),(x+7,y-4)],'grass',3,2)
                p.poly([(x-2,y-15),(x+1,y-23),(x+4,y-16),(x+3,y-5),(x-1,y-8)],'thatch',2)
                for dy in [-16,-12,-8]: p.line([(x, y+dy),(x+2,y+dy-1)],'thatch',4,2)
    else:
        p.poly([(7,34),(16,18),(36,13),(51,24),(57,47),(45,58),(16,57),(6,46)],'rock',1)
        p.poly([(7,34),(16,18),(36,13),(31,34),(13,43)],'rock',3)
        p.poly([(31,34),(36,13),(51,24),(49,45),(43,54),(16,53)],'rock',2)
        p.line([(35,17),(31,33),(35,38),(33,45)],'rock',0,2)
        p.line([(12,34),(19,21),(30,18)],'rock',4,2)
    return texture(p,seed,kind)

def build(seed=0):
    palette=json.loads((ROOT/'assets/palette/palette.json').read_text()); slots={s['name']:s['index'] for s in palette['slots']}
    lut=np.zeros((256,4),np.uint8)
    for s in palette['slots']: lut[s['index']]=[*s['reference'],255]
    out=ROOT/'assets/civilisation'; out.mkdir(parents=True,exist_ok=True); entries=[]
    def emit(name,images,kind,logical=16,**extra):
        cw,ch=images[0].size; logical=[logical,logical] if isinstance(logical,int) else logical
        for density in DENSITIES:
            w,h=cw*density//64,ch*density//64; frames=len(images); idx=np.zeros((h,w*frames,4),np.uint8)
            for f,im in enumerate(images):
                a=np.array(im.resize((w,h),Image.Resampling.NEAREST)); visible=a!=255
                if kind in {'floor','roof'}:
                    a[-1]=a[0]; a[:,-1]=a[:,0]; visible=a!=255
                idx[:,f*w:(f+1)*w,0]=np.where(visible,a,0); idx[:,f*w:(f+1)*w,3]=visible*255
            ref=lut[idx[:,:,0]].copy(); ref[:,:,3]=idx[:,:,3]; ref[idx[:,:,3]==0]=0
            stem=f'{name}_lod{density}'; used=set(idx[idx[:,:,3]>0,0].tolist())
            Image.fromarray(ref).save(out/f'{stem}.png'); Image.fromarray(idx).save(out/f'{stem}_idx.png')
            meta={'id':'civilisation.'+stem,'family':'civilisation','asset_id':name,'files':[f'civilisation/{stem}.png',f'civilisation/{stem}_idx.png'],'sidecar':f'civilisation/{stem}.json',
                  'frame':[w,h],'frames':frames,'directions':['none'],'fps':6 if frames>1 else 0,'pivot':[w//2,h-1],
                  'logical_frame':logical,'logical_pivot':[logical[0]/2,logical[1]-.25],'placement_footprint':[16,16] if kind!='stored_item' else [4,4],'density':density,'detail_source':64,
                  'kind':kind,'seed':seed,'shadow':'engine','tags':['detail',kind],
                  'recolor_slots':[s['name'] for s in palette['slots'] if s['index'] in used],
                  'emissive_slots':[s['name'] for s in palette['slots'] if s['index'] in used and s['name'].startswith('fixed_fire_')],
                  'anims':{'active':{'row':0,'frames':frames,'fps':6}} if frames>1 else {},**extra}
            (out/f'{stem}.json').write_text(json.dumps(meta,indent=2)+'\n',encoding='utf-8'); entries.append(meta)
    for material in FLOORS: emit('floor_'+material,[floor(material,slots,seed)],'floor',material=material)
    for material in WALLS:
        for mask in range(16): emit(f'wall_{material}_{mask:02d}',[wall(material,mask,slots)],'wall',material=material,connection_mask=mask,connection_bits={'N':1,'E':2,'S':4,'W':8})
    for material in ROOFS: emit('roof_'+material,[roof(material,slots,seed)],'roof',material=material)
    for kind in CONTAINERS:
        for layer in ['back','front','lid']: emit(kind+'_'+layer,[container(kind,layer,slots,seed)],'container_layer',container_id=kind,layer=layer)
    for kind in RESOURCES: emit('stored_'+kind,[token(kind,slots)],'stored_item',logical=4,resource_id=kind)
    for kind in OBJECTS: emit(kind,[object_art(kind,slots,seed,f) for f in range(4 if kind in ANIMATED else 1)],'object')
    emit('person_detail',[person(slots)],'person',pose='standing_reference',production_animation=False)
    for kind,(_,logical) in OUTDOOR.items(): emit(kind,[outdoor(kind,slots,seed)],'outdoor',logical=logical)
    for kind,(size,logical) in VEHICLES.items():
        p=Pixels(slots,size); vehicle(kind,p,seed)
        emit(kind,[texture(p,seed,kind)],'vehicle',logical=logical,placement_footprint=[logical[0],min(16,logical[1])],transport_mode='water' if kind in {'raft','canoe','rowing_boat','sailboat','cargo_ship'} else 'land')
    from .civilisation_data import build_data
    build_data(out,entries,seed)
    return entries
