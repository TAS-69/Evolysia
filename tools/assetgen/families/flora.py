
import json
import math
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
from .terrain import stable

ROOT=Path(__file__).resolve().parents[3]
TREES={
    'conifer':([16,32],4,['sapling','young','mature','old','dead','burned','snow_capped']),
    'broadleaf':([16,32],4,['sapling','young','mature','old','bare','dead','burned','old_bare']),
    'rainforest_giant':([32,48],3,['young','mature','burned']),
    'jungle_tree':([32,32],4,['young','mature','burned']),
    'palm':([16,32],3,['young','mature','burned']),
    'acacia':([32,32],3,['young','mature','bare','burned'])}
SMALL={
    'shrub':(4,['normal','dry','burned']),
    'berry_bush':(3,['empty','flowering','fruiting']),
    'cactus':(3,['normal','flowering']),
    'tall_grass':(4,['normal']), 'reeds':(3,['normal']),
    'flowers':(4,['normal']), 'mushrooms':(3,['normal']),
    'stump':(2,['normal']), 'fallen_log':(2,['normal'])}
for kind in ['grain','tuber','gourd','fiber']:
    SMALL['wild_'+kind]=(2,['growing','ripe'])
    SMALL['crop_'+kind]=(1,['seedling','growing','ripe','harvested','withered'])

def contracts():
    result=[]
    for species,(size,count,states) in TREES.items():
        for state in states:
            frame=[16,16] if state=='sapling' else ([32,32] if species=='broadleaf' and state in {'old','old_bare'} else size)
            result.append((f'{species}_{state}',species,state,frame,count,1))
    for species,(count,states) in SMALL.items():
        for state in states:
            for size in ([16,16],[16,32]) if species=='cactus' else [[16,16]]:
                name=species if state=='normal' else f'{species}_{state}'
                if species=='cactus' and size[1]==32: name+='_tall'
                result.append((name,species,state,size,count,4 if species in {'tall_grass','reeds'} else 1))
    return result

class Plant:
    def __init__(self,size,slots):
        self.size=size; self.slots=slots
        self.image=Image.new('L',tuple(size),255); self.d=ImageDraw.Draw(self.image)

    def ink(self,ramp,shade):
        prefix='' if ramp in {'foliage','bark','grass','flower','cloth_a','fruit'} else 'fixed_'
        return self.slots[f'{prefix}{ramp}_{shade}']

    def line(self,points,ramp='bark',shade=1,width=1):
        self.d.line(points,fill=self.ink(ramp,shade),width=width)

    def leaf(self,cx,cy,rx,ry,ramp='foliage',salt=0):
        w,h=self.size
        for y in range(max(1,int(cy-ry)),min(h-1,int(cy+ry)+1)):
            for x in range(max(1,int(cx-rx)),min(w-1,int(cx+rx)+1)):
                dx=(x-cx)/max(1,rx); dy=(y-cy)/max(1,ry)
                noise=stable(f'{salt}:{x}:{y}')
                radius=dx*dx+dy*dy
                if radius>1+(noise%5-2)*.045: continue
                shade=2
                if dx+dy<-.25: shade=3
                if dx+dy<-.95 and radius<.85: shade=4
                if dx+dy>.65: shade=1
                if radius>.80 and dx+dy>.1: shade=0
                if noise%17==0 and radius<.7: shade=max(1,shade-1)
                self.image.putpixel((x,y),self.ink(ramp,shade))

def tree(species,state,size,variant,seed,slots):
    p=Plant(size,slots); w,h=size; base=h-2
    rng=np.random.default_rng(stable(species)+variant*137+seed)
    cx=w//2; lean=[-1,0,1,-1][variant]; top=3+variant%3
    if state=='young': top=h//3+variant%2
    if state=='sapling': top=5+variant%2
    if species=='conifer' and state=='old': top+=2
    if species=='palm': top=max(7,top)
    bare=state in {'bare','old_bare','dead','burned'}
    bark='basalt' if state=='burned' else 'bark'
    trunkwidth=1 if state=='sapling' else (3 if w>=32 or state=='old' else 2)
    crown_y=top+(base-top)//3
    if species=='palm': crown_y=top+3
    p.line([(cx,base),(cx+lean,base-5),(cx+lean,crown_y)],bark,1,trunkwidth)
    p.line([(cx-1,base-1),(cx+lean-1,crown_y)],bark,3 if not bare else 2)
    if w>=32:
        for dx in [-4,-2,3,5]: p.line([(cx+dx,base),(cx,base-6)],bark,1,1)
    branches=[]
    for sign in [-1,1]:
        for i in range(3):
            if species=='acacia' or (species in {'conifer','palm'} and not bare): continue
            if state=='dead' and i==0: continue
            if species=='palm' and i>0: continue
            by=min(base-3,crown_y+2+i*2)
            reach=min(w//2-2,3+i+variant%2+(4 if state=='old_bare' else 0))
            end=(int(np.clip(cx+sign*reach,1,w-2)),max(top+2,by-5-i%2))
            if species=='conifer' and bare:
                by=min(base-4,top+5+i*6); end=(cx+sign*reach,by-1)
            if species=='palm':
                by=crown_y; end=(cx+sign*3,crown_y+1+variant%2)
            p.line([(cx,by),(cx+sign*2,by-2),end],bark,1)
            if bare: p.line([end,(end[0]+(1 if sign<0 else -1),end[1]-2)],bark,2)
            branches.append(end)
    if species=='acacia':
        p.line([(cx,crown_y+9),(cx-6,crown_y+3),(3,crown_y+2)],bark,1,2)
        p.line([(cx,crown_y+9),(cx+6,crown_y+2),(w-4,crown_y)],bark,2,2)
    if not bare:
        if species=='conifer':
            tiers=3 if state=='sapling' else 4
            bottom=base-(2 if state=='sapling' else 5)
            span=bottom-top
            for i in range(tiers-1,-1,-1):
                yy=top+i*span//tiers
                radius=min(w//2-2,3+i+variant%2)
                p.d.polygon([(cx+lean,yy),(cx-radius,yy+span//tiers+2),
                             (cx+radius,yy+span//tiers+1)],fill=p.ink('foliage',1))
                p.d.polygon([(cx+lean,yy),(cx-radius+1,yy+span//tiers+1),
                             (cx,yy+span//tiers)],fill=p.ink('foliage',3))
                p.line([(cx-1,yy+2),(cx-radius+2,yy+span//tiers)],'foliage',4)
                p.line([(cx+1,yy+3),(cx+radius-1,yy+span//tiers)],'foliage',2)
        elif species=='palm':
            center=(cx+lean,crown_y)
            for dx,dy in [(-6,1),(-5,-3),(-2,-5),(3,-4),(6,0),(5,4),(-5,4)]:
                end=(int(np.clip(center[0]+dx,2,w-3)),max(2,center[1]+dy))
                mid=(int((center[0]+end[0])/2),min(center[1],end[1])-1)
                p.line([center,mid,end],'foliage',1,2)
                p.line([center,mid,(end[0],end[1]-1)],'foliage',3)
        elif species=='acacia':
            for i,(xx,yy) in enumerate([(7,crown_y+1),(15,crown_y-1),(24,crown_y)]):
                p.leaf(xx+variant%2,yy,6,3+variant%2,salt=seed+variant*7+i)
        else:
            radius=3 if state=='sapling' else (max(4,int(w*.32)) if state=='young' else w//2-2)
            cy=min(top+radius+2,base-radius-4)
            if w==32:
                cy=min(max(top+radius,math.ceil(radius*(.35+.72*.9))+2),base-4-radius*.65*.9)
            if species=='rainforest_giant':
                p.leaf(cx+1,cy+7,11,7,salt=seed+variant)
            lobes=[(-radius*.45,2,radius*.65), (radius*.48,1,radius*.62), (0,-radius*.35,radius*.72)]
            for i,(dx,dy,rr) in enumerate(lobes):
                p.leaf(cx+lean+dx+int(rng.integers(-1,2)),cy+dy,rr,max(3,rr*(.9 if w==32 else 1.15)),salt=seed+variant*17+i)
            if species=='jungle_tree':
                for x,y in [(cx-8,cy+4),(cx+7,cy+3)]: p.line([(x,y),(x+1,min(base-2,y+7))],'foliage',1)
    if state=='snow_capped':
        original=np.array(p.image); snow=np.full_like(original,255)
        foliage={p.ink('foliage',i) for i in range(5)}
        for y in range(1,h-1):
            for x in range(1,w-1):
                if original[y,x] in foliage and original[y-1,x] not in foliage:
                    snow[y,x]=p.ink('snow',4 if x<cx else 3)
                    if y+1<h-1 and original[y+1,x] in foliage and (x+variant)%3!=0: snow[y+1,x]=p.ink('snow',2)
        p.image=Image.fromarray(snow)
    return p.image

def small(species,state,size,variant,frame,seed,slots):
    p=Plant(size,slots); w,h=size; base=h-2
    rng=np.random.default_rng(stable(species)+seed+variant*137)
    withered=state in {'withered','dry'}; burned=state=='burned'
    leaf='basalt' if burned else ('thatch' if withered else 'foliage')
    swing=[-1,0,1,0][frame]
    if species in {'shrub','berry_bush'}:
        p.line([(8,base),(7,base-7)],'bark',1,2)
        for i,(x,y) in enumerate([(5,10),(10,9),(7,7)]):
            if burned:
                p.line([(8,base-2),(x+variant%2,y+variant)],'basalt',2)
            else: p.leaf(x+variant%2,y+variant%2,3+variant%2,3,leaf,seed+variant*9+i)
        if state in {'flowering','fruiting'}:
            for x,y in [(4,9),(9,7),(11,11)]:
                if state=='fruiting':
                    p.d.rectangle((x,y,x+1,y+1),fill=p.ink('fruit',2))
                    p.d.point((x,y),fill=p.ink('fruit',3))
                else: p.d.line((x,y,x+1,y),fill=p.ink('flower',4))
    elif species=='cactus':
        top=3+variant; cx=7+variant%2
        p.d.rounded_rectangle((cx-2,top,cx+2,base),radius=2,fill=p.ink('foliage',1))
        p.line([(cx-1,top+1),(cx-1,base-1)],'foliage',3)
        for sign,offset in [(-1,3),(1,6)]:
            yy=top+offset+(h//3 if h==32 else 0)
            yy=min(base-3,yy)
            p.line([(cx,yy),(cx+sign*4,yy),(cx+sign*4,yy-3-variant%2)],'foliage',1,2)
            p.line([(cx+sign*4,yy-3),(cx+sign*4,yy-1)],'foliage',3)
        if state=='flowering':
            p.leaf(cx,top,2,1,'flower',seed+variant)
    elif species in {'tall_grass','reeds','flowers'}:
        count=3 if species=='flowers' else 5
        for i in range(count):
            x=3+i*2; top=int(rng.integers(3,9)); bend=swing+(i%2*2-1)
            if species in {'tall_grass','reeds'} and frame==3: top+=1
            if species=='flowers': bend=variant%2
            p.line([(8,base),(x,base-3),(x+bend,top)],'grass',1)
            p.line([(x,base-3),(x+bend,top+2)],'grass',3)
            if species=='reeds': p.line([(x+bend,top),(x+bend,top+3)],'bark',2,2)
            if species=='flowers':
                p.leaf(x+bend,top,1+variant%2,1,'flower',seed+i)
                p.d.point((x+bend,top),fill=p.ink('flower',4))
        if species in {'tall_grass','reeds'}:
            p.line([(7,base),(7+swing,10+frame%2)],'grass',4)
    elif species=='mushrooms':
        for i in range(2+variant%2):
            x=4+i*4; y=8+((i+variant)%3)
            p.line([(x,y),(x,base-i%2)],'bark',3)
            p.leaf(x-1,y,2,1,'flower',seed+variant+i)
            p.d.point((x-2,y-1),fill=p.ink('flower',4))
    elif species=='stump':
        p.d.rectangle((4,8+variant,11,base),fill=p.ink('bark',1))
        p.leaf(7,8+variant,4,2,'wood',seed)
        p.line([(5,11),(5,base-1)],'bark',3)
        p.line([(9,12),(10,base)],'bark',0)
        p.d.point((7,8+variant),fill=p.ink('wood',1))
    elif species=='fallen_log':
        p.d.polygon([(2,10-variant),(11,7+variant),(13,10+variant),(4,13)],fill=p.ink('bark',1))
        p.line([(3,10-variant),(10,8+variant)],'bark',3,2)
        p.leaf(12,9+variant,2,2,'wood',seed+variant)
        p.d.point((12,9+variant),fill=p.ink('wood',1))
        shifted=Image.new('L',tuple(size),255); shifted.paste(p.image,(0,1)); p.image=shifted
    else:
        kind=species.split('_',1)[1]; crop=species.startswith('crop_')
        if state=='harvested':
            for x in [4,7,10,12]: p.line([(x,base),(x,base-2-(x%2))],'bark',2)
        else:
            count=2 if state=='seedling' else (4 if crop else 3)
            for i in range(count):
                x=3+i*3+variant%2; top=10 if state=='seedling' else 4+(i+variant)%3
                p.line([(x,base),(x+(i%2),top)],'bark' if withered else 'grass',2)
                if kind in {'grain','fiber'}:
                    if state=='ripe':
                        if kind=='grain': p.leaf(x,top,1,2,'thatch',seed+i)
                        else: p.leaf(x,top,2,1,'flower',seed+i)
                    else:
                        p.line([(x,top+3),(x-2,top+1)],'thatch' if withered else 'foliage',2)
                        if kind=='fiber' and state!='seedling':
                            p.leaf(x+1,top+4,2,1,'thatch' if withered else 'foliage',seed+i)
                else:
                    p.leaf(x,top+3,2,1,'thatch' if withered else 'foliage',seed+variant+i)
                    if state=='ripe':
                        p.leaf(x+1,base-2,2,2,'clay' if kind=='gourd' else 'soil',seed+i)
                    if kind=='gourd' and state!='seedling':
                        p.line([(x,base-1),(min(13,x+2),base-3),(min(14,x+3),base-2)],'thatch' if withered else 'grass',2)
            if withered:
                p.line([(4,base-5),(6,base-2)],'thatch',1)
    return p.image

def build(seed=0):
    data=json.loads((ROOT/'assets/palette/palette.json').read_text())
    slots={s['name']:s['index'] for s in data['slots']}
    lut=np.zeros((256,4),dtype=np.uint8)
    for s in data['slots']: lut[s['index']]=[*s['reference'],255]
    out=ROOT/'assets/flora'; out.mkdir(parents=True,exist_ok=True)
    entries=[]
    for name,species,state,size,count,frames in contracts():
        w,h=size; idx=np.zeros((h*count,w*frames,4),dtype=np.uint8)
        for v in range(count):
            for f in range(frames):
                image=tree(species,state,size,v,seed,slots) if species in TREES else small(species,state,size,v,f,seed,slots)
                indices=np.array(image); tile=np.zeros((h,w,4),dtype=np.uint8)
                tile[:,:,0]=np.where(indices==255,0,indices); tile[:,:,3]=np.where(indices==255,0,255)
                idx[v*h:(v+1)*h,f*w:(f+1)*w]=tile
        ref=lut[idx[:,:,0]].copy(); ref[:,:,3]=idx[:,:,3]; ref[idx[:,:,3]==0]=0
        Image.fromarray(idx).save(out/f'{name}_idx.png'); Image.fromarray(ref).save(out/f'{name}.png')
        used=set(idx[idx[:,:,3]>0,0].tolist())
        entry={'id':f'flora.{name}','family':'flora','asset_id':species,'state':state,
            'files':[f'flora/{name}.png',f'flora/{name}_idx.png'],'sidecar':f'flora/{name}.json',
            'frame':size,'frames':frames,'directions':['none'],'sheet_rows':[f'variant_{v}' for v in range(count)],
            'fps':4 if frames>1 else 0,'pivot':[w//2,h-2],'seed':seed,'tags':['flora',state],
            'recolor_slots':[s['name'] for s in data['slots'] if s['index'] in used],
            'anims':{'idle':{'row':0,'frames':frames,'fps':4}} if frames>1 else {},
            'wind_sway':{'mode':'shader','anchor_y':h-2,'max_displacement_px':1} if species in TREES and state not in {'dead','burned','bare','old_bare'} else {'mode':'frames' if frames>1 else 'none'},
            'shadow':'engine','shadow_size':'L' if w==32 else ('M' if h==32 else 'S'),
            'layer':'snow_overlay' if state=='snow_capped' else 'base'}
        if state=='snow_capped': entry['composite_over']='flora.conifer_mature'
        if species=='broadleaf' and state in {'young','mature','old'}:
            entry['seasonal_states']={'winter':'flora.broadleaf_old_bare' if state=='old' else 'flora.broadleaf_bare'}
        if species=='acacia' and state in {'young','mature'}:
            entry['seasonal_states']={'dry':'flora.acacia_bare'}
        (out/f'{name}.json').write_text(json.dumps(entry,indent=2)+'\n',encoding='utf-8'); entries.append(entry)
    return entries
