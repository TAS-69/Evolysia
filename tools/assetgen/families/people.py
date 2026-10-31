
import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
from .terrain import stable

ROOT=Path(__file__).resolve().parents[3]
AGES=['child','adult','elder']
BUILDS=['slim','average','stout']
DIRECTIONS=['down','up','side']
ORDER=['hair_back','body','clothing','body_paint','hair_front','accessory','held_item']
PARTS={
    'hair_back':['long','braided','topknot','mane','tied_back','locks'],
    'body':['base'],
    'clothing':['none','hide_wrap','fur_cloak','woven_tunic','dyed_robe','ceremonial'],
    'body_paint':['cheek_dots','brow_band','arm_bands','leg_marks','face_streak','shoulder_dabs'],
    'hair_front':['short','cropped','long','braided','topknot','mane','tied_back','locks','child_tuft'],
    'accessory':['feather_band','bone_necklace','beads','mask','helmet','helmet_bone'],
    'held_item':['spear','stone_axe','digging_stick','basket','torch','bow','net','staff','bundle','infant']}
ANIMS={
    'idle':(2,3,['idle','wait']), 'walk':(4,8,['walk','migrate']),
    'run':(4,12,['flee','chase']), 'gather':(3,6,['forage','harvest']),
    'chop':(3,8,['chop']), 'dig':(3,6,['dig','mine','farm']),
    'attack':(3,10,['hunt','fight']), 'carry':(4,8,['haul']),
    'build':(3,6,['build']), 'craft':(3,6,['craft','cook']),
    'eat':(2,4,['eat']), 'drink':(2,4,['drink']), 'talk':(2,4,['talk']),
    'pray':(2,3,['pray','ritual']), 'dance':(4,8,['celebrate']),
    'mourn':(2,3,['mourn']), 'sleep':(2,2,['sleep']),
    'swim':(4,6,['swim']), 'play':(4,8,['play']), 'dead':(1,0,['dead'])}
SIDE_ONLY={'craft','sleep','dead'}

def profiles(anim):
    return [(age,build,direction) for age in (['child'] if anim=='play' else AGES)
            for build in BUILDS for direction in (['side'] if anim in SIDE_ONLY else DIRECTIONS)]

def pose(age,build,direction,anim,f):
    height={'child':8,'adult':11,'elder':10}[age]; width=2+BUILDS.index(build)
    top=15-height; hx=8+(age=='elder' and direction=='side'); hy=top
    left=8-width//2; right=left+width-1; shoulder=top+4; hip=12
    hands=[(left-1,11),(right+1,11)]; feet=[(7,14),(9,14)]
    bob=0
    if anim=='idle': bob=-f
    if anim in {'walk','run','carry','play'}:
        bob=-int(f==1)
        if direction=='side':
            step=2 if anim in {'run','play'} else 1
            feet=[(7+[-1,0,1,0][f]*step,14-int(f==1)),(9+[1,0,-1,1][f]*step,14-int(f==3))]
        else:
            feet=[(7,14-[0,1,2,1][f]),(9,14-[2,1,0,0][f])]
        hands=[(left-1,10+[0,1,2,1][f]),(right+1,10+[2,1,0,0][f])]
    if anim=='carry': hands=[(7,10),(10,10+f%2)]
    if anim in {'gather','chop','dig','attack','build','craft'}:
        positions={'gather':[(11,12),(12,13),(10,11)],'chop':[(10,4),(12,8),(11,12)],
                   'dig':[(11,7),(12,11),(10,12)],'attack':[(10,8),(13,9),(11,10)],
                   'build':[(11,9),(11,12),(10,10)],'craft':[(10,11),(11,12),(12,11)]}
        hands=[(7,10+f%2),positions[anim][f]]
        if anim=='gather': hx+=1; hy+=1
        if anim=='attack': hx+=int(f==1)
    if anim in {'eat','drink'}: hands=[(left-1,11),(hx+2,hy+2+f*2)]
    if anim=='talk': hands=[(left-1,11),(right+2,8+f*2)]
    if anim=='pray': hands=[(7,9-f),(9,9-f)]; bob=-f
    if anim=='dance':
        hands=[([4,3,5,4][f],[7,6,8,9][f]),([12,11,12,10][f],[7,8,6,9][f])]
        feet=[(7-int(f==1),14-int(f==2)),(9+int(f==3),14-int(f==0))]; bob=-int(f==1)
    if anim in {'craft','mourn'}:
        hy+=2; shoulder+=2; hip=13
        feet=[(6,14),(10,14)]
        if anim=='mourn':
            hy+=f; hands=[(hx-1,hy+2),(hx+1,hy+1+f)]
    hy+=bob; shoulder+=bob
    if anim=='swim':
        hy=9-int(f==1); shoulder=12
        hands=[(4+[0,1,2,1][f],12),(12-[2,1,0,0][f],12+int(f==3))]
    lying=anim in {'sleep','dead'}
    if lying:
        hx=5 if age=='child' else 3; hy=11; shoulder=12; hip=12
        end=11 if age=='child' else (12 if age=='elder' else 13)
        feet=[(end-1,14),(end,14)]; hands=[(7,13),(9,12+(f if anim=='sleep' else 0))]
    pivot=[max(x for x,y in feet)-1,14] if lying else [8,14]
    return {'head':[hx,hy],'torso':[left,shoulder,right,hip],
            'hands':[list(v) for v in hands],'feet':[list(v) for v in feet],
            'pivot':pivot,'lying':lying,'direction':direction,'age':age,'build':build}

class Figure:
    def __init__(self,slots,p):
        self.image=Image.new('L',(16,16),255); self.d=ImageDraw.Draw(self.image)
        self.slots=slots; self.p=p

    def ink(self,ramp,shade):
        prefix='' if ramp in {'skin','hair','cloth_a','cloth_b','culture'} else 'fixed_'
        return self.slots[f'{prefix}{ramp}_{shade}']

    def dot(self,x,y,ramp,shade):
        if 1<=x<=14 and 1<=y<=14: self.image.putpixel((int(x),int(y)),self.ink(ramp,shade))

    def line(self,points,ramp,shade):
        points=[(int(np.clip(x,1,14)),int(np.clip(y,1,14))) for x,y in points]
        self.d.line(points,fill=self.ink(ramp,shade))

    def rect(self,box,ramp,shade):
        x0,y0,x1,y1=box
        if x0<=x1 and y0<=y1:
            self.d.rectangle((max(1,x0),max(1,y0),min(14,x1),min(14,y1)),fill=self.ink(ramp,shade))

    def local(self,x,y,ramp,shade):
        hx,hy=self.p['head']
        if self.p['lying']: self.dot(hx+y-1,hy+1-(x-3),ramp,shade)
        else: self.dot(hx+x-3,hy+y-1,ramp,shade)

def part(layer,variant,p,anim,f,seed,slots):
    q=Figure(slots,p); hx,hy=p['head']; left,top,right,hip=p['torso']
    direction=p['direction']; age=p['age']; lying=p['lying']
    if layer=='body':
        if lying:
            q.rect((hx-1,11,hx+1,13),'skin',2); q.line([(hx-1,11),(hx+1,11)],'skin',3)
            q.rect((hx+2,14-(right-left+1)-int(anim=='sleep' and f==1),10,13),'skin',2)
            for x,y in p['feet']: q.line([(9,13),(x,y)],'skin',1)
        else:
            q.rect((hx-1,hy,hx+1,hy+2),'skin',2)
            q.line([(hx-1,hy),(hx-1,hy+1)],'skin',3)
            q.dot(hx+1,hy+2,'skin',1)
            if direction=='side': q.dot(hx+2,hy+2,'skin',2)
            q.line([(hx,hy+3),(8,top)],'skin',2)
            if anim!='swim':
                q.rect((left,top,right,hip),'skin',2)
                q.line([(left,top),(left,hip)],'skin',3)
                for i,(x,y) in enumerate(p['feet']): q.line([(left+i*(right-left),hip),(x,y)],'skin',1 if i else 2)
            else: q.rect((left,12,right,13),'skin',2)
        for i,hand in enumerate(p['hands']):
            origin=(6,12) if lying else (left if i==0 else right,top)
            q.line([origin,hand],'skin',1 if i==1 else 3)
        if direction!='up':
            if lying: q.dot(hx,12,'skin',0)
            else:
                q.dot(hx+1,hy+1,'skin',0)
                if direction=='down': q.dot(hx-1,hy+1,'skin',0)
    elif layer=='clothing':
        if lying:
            y0=14-(right-left+1)-int(anim=='sleep' and f==1)
            if variant=='none': q.rect((7,y0,8,13),'cloth_a',2)
            else:
                start=hx+2
                q.rect((start,y0,9,13),'cloth_a',2); q.dot(9,13,'cloth_a',0)
                if variant=='hide_wrap': q.image.putpixel((start,y0),255)
                if variant=='fur_cloak': q.line([(start,y0-1),(9,y0-1)],'cloth_a',3)
                if variant=='woven_tunic': q.dot(start+1,y0,'cloth_b',3)
                if variant=='dyed_robe': q.rect((10,12,11,13),'cloth_a',2)
                if variant=='ceremonial': q.line([(6,13),(9,13)],'culture',2)
        else:
            y0=hip-1 if variant=='none' else top
            y1=13 if variant in {'dyed_robe','ceremonial','fur_cloak'} else hip
            if anim=='swim': y0=12; y1=13
            extra=1 if variant=='fur_cloak' else 0
            q.rect((left-extra,y0,right+extra,y1),'cloth_a',2)
            q.line([(right+extra,y0),(right+extra,y1)],'cloth_a',0)
            q.line([(left-extra,y0),(right,y0)],'cloth_a',3)
            if variant=='hide_wrap':
                q.image.putpixel((left,top),255)
                if top+1<=14: q.image.putpixel((left,top+1),255)
                q.line([(left,top+2),(right,hip)],'cloth_b',2)
            if variant=='fur_cloak':
                for x in range(left-1,right+2,2): q.dot(x,y1,'cloth_a',0)
            if variant=='woven_tunic':
                for y in range(top+1,hip,2):
                    if stable(f'{seed}:{age}:{p["build"]}:{y}')%2: q.dot(left,y,'cloth_b',3)
            if variant in {'dyed_robe','ceremonial'}:
                q.line([(left,hip),(right,hip)],'culture' if variant=='ceremonial' else 'cloth_b',2)
                q.dot(left,top+1,'cloth_b',3)
    elif layer=='body_paint':
        points={
            'cheek_dots':[(hx-1,hy+2),(hx+1,hy+2)],'brow_band':[(hx-1,hy),(hx,hy)],
            'arm_bands':[p['hands'][0],p['hands'][1]],
            'leg_marks':[(7,13),(9,13)],'face_streak':[(hx,hy+1),(hx,hy+2)],
            'shoulder_dabs':[(left-1,top+1),(right+1,top+1)]}[variant]
        for x,y in points: q.dot(x,y,'culture',2)
        body=np.array(part('body','base',p,anim,f,seed,slots)); a=np.array(q.image)
        a[body==255]=255; q.image=Image.fromarray(a)
    elif layer in {'hair_front','hair_back'}:
        if layer=='hair_back':
            coords={
                'long':[(1,y) for y in range(1,7)]+[(5,y) for y in range(1,7)],
                'braided':[(5+(y%2),y) for y in range(2,7)],
                'topknot':[(4,0),(5,0),(5,1)],
                'mane':[(x,y) for x in [0,6] for y in range(1,6)],
                'tied_back':[(5,2),(6,3),(6,4)],
                'locks':[(x,y) for x in [0,2,5] for y in range(2,6)]}[variant]
        else:
            coords=[(x,1) for x in range(2,5)]
            coords+={
                'short':[(1,2),(4,2)],'cropped':[(2,2)],
                'long':[(1,2),(1,3),(5,2),(5,3)],
                'braided':[(1,2),(5,2),(4,0)],
                'topknot':[(3,0),(3,-1),(4,-1)],
                'mane':[(1,0),(5,0),(0,2),(6,2)],
                'tied_back':[(4,2),(5,1),(5,2)],
                'locks':[(1,2),(2,3),(4,3),(5,2)],
                'child_tuft':[(2,0),(3,0),(4,2)]}[variant]
        for x,y in coords:
            if direction=='side' and layer=='hair_front' and x>=5 and y>=2: continue
            q.local(x,y,'hair',3 if x<=2 and y<=1 else (0 if x>=5 else 1))
        if layer=='hair_front' and direction=='up':
            for x in range(2,5):
                for y in range(1,4): q.local(x,y,'hair',1 if x<4 else 0)
    elif layer=='accessory':
        if variant=='feather_band':
            for x in range(1,6): q.local(x,1,'cloth_b',2)
            q.local(1,0,'cloth_b',3); q.local(2,-1,'cloth_b',3)
        elif variant=='bone_necklace':
            for x,y in [(2,4),(3,5),(4,4)]: q.local(x,y,'ui',4)
        elif variant=='beads':
            for x,y in [(1,3),(5,3),(2,4),(4,4)]: q.local(x,y,'cloth_b',3)
        elif variant=='mask':
            if direction!='up':
                for x in range(2,5):
                    for y in range(2,4): q.local(x,y,'wood',3 if x==2 else 1)
                q.local(2,2,'wood',0); q.local(4,2,'wood',0)
            else:
                q.local(1,2,'cloth_b',2); q.local(5,2,'cloth_b',2)
        else:
            ramp='ui' if variant=='helmet_bone' else 'cloth_b'
            shade=4 if variant=='helmet_bone' else 2
            for x in range(1,6): q.local(x,0,ramp,shade); q.local(x,1,ramp,shade)
            q.local(1,2,ramp,shade); q.local(5,2,'wood' if variant=='helmet_bone' else 'cloth_b',1)
    else:
        hand=p['hands'][1]; x,y=hand
        angle='horizontal' if lying or y>=12 else ('down' if y<6 else 'vertical')
        def put(dx,dy,ramp,shade):
            if angle=='horizontal': px,py=x+dy,y-dx
            elif angle=='down': px,py=x-dx,y-dy
            else: px,py=x+(-dx if x>11 else dx),y+dy
            q.dot(px,py,ramp,shade)
        if variant in {'spear','stone_axe','digging_stick','torch','staff'}:
            for dy in range(-4,3): put(0,dy,'wood',2 if dy<0 else 1)
            if variant=='spear':
                put(0,-5,'stone',3); put(1,-4,'stone',1)
            if variant=='stone_axe':
                for dx,dy in [(1,-3),(2,-3),(1,-2)]: put(dx,dy,'stone',3 if dx==1 else 1)
            if variant=='digging_stick': put(1,2,'wood',1)
            if variant=='staff': put(-1,-3,'wood',3); put(-1,-4,'wood',3)
            if variant=='torch':
                for dx,dy in [(0,-5),(1,-4),(0,-3),(-1,-4)]: put(dx,dy,'fire',3 if dx<1 else 1)
        elif variant in {'basket','bundle','infant'}:
            for dy in range(-1,3):
                for dx in range(-1,3): put(dx,dy,'cloth_b' if variant!='basket' else 'thatch',2 if dx<1 else 1)
            if variant=='basket':
                for dx in range(0,2): put(dx,-2,'thatch',3)
            if variant=='bundle': put(0,0,'wood',1); put(1,1,'wood',1)
            if variant=='infant':
                put(0,-1,'skin',3); put(1,-1,'skin',2); put(0,0,'skin',0)
        elif variant=='bow':
            for dx,dy in [(0,-3),(1,-2),(2,-1),(2,0),(1,1),(0,2)]: put(dx,dy,'wood',2)
            for dy in range(-2,2): put(0,dy,'cloth_b',3)
        else:
            for dy in range(-3,3):
                for dx in range(-2,3):
                    if (dx+dy)%2==0: put(dx,dy,'cloth_b',3)
            for dy in range(0,4): put(0,dy,'wood',1)
        q.dot(x,y,'skin',2)
    return q.image

def build(seed=0):
    palette=json.loads((ROOT/'assets/palette/palette.json').read_text())
    slots={s['name']:s['index'] for s in palette['slots']}
    lookup=np.zeros((256,4),dtype=np.uint8)
    for s in palette['slots']: lookup[s['index']]=[*s['reference'],255]
    out=ROOT/'assets/people'; out.mkdir(parents=True,exist_ok=True)
    entries=[]; rig={'version':1,'layer_order':ORDER,'hair_styles':{'shaved':{'front':None,'back':None}},'age_defaults':{'elder':{'hair_lut_row':6}},'animations':{}}
    for style in PARTS['hair_front']:
        rig['hair_styles'][style]={'front':style,'back':style if style in PARTS['hair_back'] else None}
    for anim,(frames,fps,actions) in ANIMS.items():
        rows=profiles(anim); poses=[[pose(*profile,anim,f) for f in range(frames)] for profile in rows]
        rig['animations'][anim]={'profiles':[list(v) for v in rows],'poses':poses,'sim_actions':actions}
        for layer,variants in PARTS.items():
            count=len(rows); idx=np.zeros((16*count*len(variants),16*frames,4),dtype=np.uint8)
            labels=[]
            for v,variant in enumerate(variants):
                for r,profile in enumerate(rows):
                    row=v*count+r; labels.append('.'.join([variant,*profile]))
                    for f in range(frames):
                        indices=np.array(part(layer,variant,poses[r][f],anim,f,seed,slots))
                        tile=np.zeros((16,16,4),dtype=np.uint8)
                        tile[:,:,0]=np.where(indices==255,0,indices); tile[:,:,3]=np.where(indices==255,0,255)
                        idx[row*16:(row+1)*16,f*16:(f+1)*16]=tile
            logical=idx.reshape(count*len(variants),16,16*frames,4)
            unique=[]; row_lookup=[]; seen={}; physical_labels=[]
            for v,variant in enumerate(variants):
                mapping=[]
                for r in range(count):
                    logical_row=v*count+r; tile=logical[logical_row]; key=tile.tobytes()
                    if key not in seen:
                        seen[key]=len(unique); unique.append(tile); physical_labels.append(labels[logical_row])
                    mapping.append(seen[key])
                row_lookup.append(mapping)
            idx=np.concatenate(unique,axis=0)
            labels=physical_labels
            ref=lookup[idx[:,:,0]].copy(); ref[:,:,3]=idx[:,:,3]; ref[idx[:,:,3]==0]=0
            name=f'{layer}_{anim}'; used=set(idx[idx[:,:,3]>0,0].tolist())
            Image.fromarray(ref).save(out/f'{name}.png'); Image.fromarray(idx).save(out/f'{name}_idx.png')
            e={'id':f'people.{name}','family':'people','layer':layer,'animation':anim,
               'files':[f'people/{name}.png',f'people/{name}_idx.png'],'sidecar':f'people/{name}.json',
               'frame':[16,16],'frames':frames,'fps':fps,'directions':['side'] if anim in SIDE_ONLY else DIRECTIONS,
               'sheet_rows':labels,'variants':variants,'profiles':[list(p) for p in rows],
               'row_formula':'row_lookup[variant_index][profile_index]','row_lookup':row_lookup,'pose_row_count':count,
               'pivot':poses[0][0]['pivot'],'row_pivots':[p[0]['pivot'] for p in poses],
               'hand_anchors':[[p['hands'][1] for p in row] for row in poses],
               'anchor_row_formula':'profile_index','rig':'people/rig.json',
               'seed':seed,'sim_actions':actions,'mirror_side_left':True,
               'anims':{anim:{'row':0,'frames':frames,'fps':fps}},
               'recolor_slots':[s['name'] for s in palette['slots'] if s['index'] in used],
               'tags':['people',layer],'shadow':'engine','shadow_size':'S'}
            if layer=='body_paint': e['clip_rule']='body_alpha & ~clothing_alpha'
            if layer=='body' and anim=='idle': e['sources']=['people/rig.json']
            (out/f'{name}.json').write_text(json.dumps(e,indent=2)+'\n',encoding='utf-8'); entries.append(e)
    (out/'rig.json').write_text(json.dumps(rig,indent=2)+'\n',encoding='utf-8')
    return entries
