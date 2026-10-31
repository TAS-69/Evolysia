
import json
import math
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
from .terrain import geometry, stable

ROOT=Path(__file__).resolve().parents[3]
CONNECTION_BITS={'n':1,'e':2,'s':4,'w':8}

def connection(mask,width,organic=False):
    yy,xx=np.mgrid[:16,:16]
    distance=np.sqrt((xx-7.5)**2+(yy-7.5)**2)
    if mask&1: distance=np.minimum(distance,np.abs(xx-7.5)+np.maximum(yy-7.5,0))
    if mask&2: distance=np.minimum(distance,np.abs(yy-7.5)+np.maximum(7.5-xx,0))
    if mask&4: distance=np.minimum(distance,np.abs(xx-7.5)+np.maximum(7.5-yy,0))
    if mask&8: distance=np.minimum(distance,np.abs(yy-7.5)+np.maximum(xx-7.5,0))
    wobble=(1.5 if organic else .5)*np.sin(xx*.35+yy*.3-.4)*np.sin(math.pi*xx/15)*np.sin(math.pi*yy/15)
    return distance+wobble<=width/2, distance+wobble

def build(seed=0):
    data=json.loads((ROOT/'assets/palette/palette.json').read_text())
    slots={s['name']:s['index'] for s in data['slots']}
    if 'culture_white' not in slots: raise ValueError('Rebuild palette to append culture_white slot')
    colours=np.zeros((256,4),dtype=np.uint8)
    for s in data['slots']: colours[s['index']]=[*s['reference'],255]
    out=ROOT/'assets/overlays'; out.mkdir(parents=True,exist_ok=True)
    entries=[]
    def slot(ramp,shade):
        return slots[f'fixed_{ramp}_{shade}']
    def emit(name,size,rows,frames,draw,**extra):
        w,h=size; idx=np.zeros((h*len(rows),w*frames,4),dtype=np.uint8)
        for r in range(len(rows)):
            for f in range(frames):
                image=draw(r,f)
                idx[r*h:(r+1)*h,f*w:(f+1)*w]=np.array(image)
        ref=colours[idx[:,:,0]].copy(); ref[:,:,3]=idx[:,:,3]; ref[idx[:,:,3]==0]=0
        Image.fromarray(ref).save(out/f'{name}.png')
        Image.fromarray(idx).save(out/f'{name}_idx.png')
        used=set(idx[idx[:,:,3]>0,0].tolist())
        e={'id':f'overlays.{name}','family':'overlays','files':[f'overlays/{name}.png',f'overlays/{name}_idx.png'],
           'sidecar':f'overlays/{name}.json','frame':size,'frames':frames,'directions':['none'],
           'sheet_rows':rows,'fps':4 if frames>1 else 0,'pivot':[w//2,h-1],'seed':seed,
           'recolor_slots':[s['name'] for s in data['slots'] if s['index'] in used],
           'anims':{'idle':{'row':0,'frames':frames,'fps':4}} if frames>1 else {},'tags':['ground_overlay'],**extra}
        (out/f'{name}.json').write_text(json.dumps(e,indent=2)+'\n',encoding='utf-8')
        entries.append(e)
    def blank(size=(16,16)): return Image.new('RGBA',size)
    def ink(index): return (index,0,0,255)
    for kind,types in [('river',[('stream',4),('river',8),('great_river',12)]),
                       ('road',[('trampled_path',4),('packed_dirt_road',6),('stone_road',8)])]:
        for subtype,width in types:
            def draw(mask,frame,kind=kind,width=width,subtype=subtype):
                image=blank(); coverage,distance=connection(mask,width,kind=='river')
                for y in range(16):
                    for x in range(16):
                        if not coverage[y,x]: continue
                        edge=distance[y,x]>width/2-1
                        if kind=='river':
                            shade=4 if edge else 2
                            if not edge and 0<x<15 and 0<y<15 and (x+2*y+frame*2)%8<2: shade=3
                            index=slot('water',shade)
                        else:
                            ramp='stone' if subtype=='stone_road' else 'soil'
                            shade=1 if edge else (3 if subtype=='stone_road' else 2)
                            if 0<x<15 and 0<y<15:
                                if subtype=='stone_road' and (y%4==0 or (x+(y//4%2)*3)%6==0): shade=1
                                elif stable(f'{seed}:{subtype}:{x}:{y}')%9==0: shade=max(1,shade-1)
                            index=slot(ramp,shade)
                        image.putpixel((x,y),ink(index))
                return image
            emit(f'{kind}_{subtype}',[16,16],[f'mask_{m:02}' for m in range(16)],4 if kind=='river' else 1,draw,
                 asset_id=kind,set_type='connection',masks=list(range(16)),mask_bits=CONNECTION_BITS,
                 subtype=subtype,width_px=width)
    def border(mask,frame):
        t,r,b,l=(7,0),(15,7),(7,15),(0,7)
        segments={0:[],1:[(l,t)],2:[(t,r)],3:[(l,r)],4:[(r,b)],
                  5:[(l,t),(r,b)],6:[(t,b)],7:[(l,b)],8:[(b,l)],
                  9:[(t,b)],10:[(t,r),(b,l)],11:[(r,b)],12:[(l,r)],
                  13:[(t,r)],14:[(l,t)],15:[]}
        image=blank(); d=ImageDraw.Draw(image)
        for start,end in segments[mask]:
            mid=((start[0]+end[0])//2,(start[1]+end[1])//2)
            mid=(min(13,max(2,mid[0]+1)),min(13,max(2,mid[1]-1)))
            d.line([start,mid,end],fill=ink(slots['culture_white']),width=1)
        for y in range(16):
            for x in range(16):
                if (x+2*y)%5>=3: image.putpixel((x,y),(0,0,0,0))
        return image
    emit('territory_border',[16,16],[f'mask_{m:02}' for m in range(16)],1,border,
         asset_id='territory_border',set_type='corner',masks=list(range(16)),mask_bits={'tl':1,'tr':2,'br':4,'bl':8},culture_tinted=True)
    def snow(mask,frame):
        coverage,values=geometry(mask); image=blank()
        for y in range(16):
            for x in range(16):
                if coverage[y,x]:
                    shade=4 if values[y,x]<.60 else (2 if (x*3+y*7+seed)%19==0 and 0<x<15 and 0<y<15 else 3)
                    image.putpixel((x,y),ink(slot('snow',shade)))
        return image
    emit('snow_cover',[16,16],[f'mask_{m:02}' for m in range(16)],1,snow,
         asset_id='snow_cover',set_type='corner',masks=list(range(16)),mask_bits={'tl':1,'tr':2,'br':4,'bl':8})
    for name in ['snow_cover_dusting','frost','wet_ground','ash_fall','burned_ground','flood_water']:
        count=1 if name=='snow_cover_dusting' else 3
        frames=4 if name=='flood_water' else 1
        def draw(variant,frame,name=name):
            image=blank()
            rng=np.random.default_rng(stable(name)+seed+variant*991)
            d=ImageDraw.Draw(image)
            ramp={'snow_cover_dusting':'snow','frost':'ice','wet_ground':'soil',
                  'ash_fall':'ash','burned_ground':'basalt','flood_water':'water'}[name]
            for _ in range(14 if name in {'frost','ash_fall'} else 22):
                x,y=map(int,rng.integers(1,14,2))
                shade=1 if name in {'wet_ground','burned_ground'} else int(rng.choice([2,3]))
                if name=='snow_cover_dusting': shade+=1
                d.line((x,y,min(14,x+int(rng.integers(1,3))),y),fill=ink(slot(ramp,shade)))
                if name=='frost':
                    d.point((x,max(1,y-1)),fill=ink(slot(ramp,4)))
                if name=='flood_water':
                    d.point((1+(x+frame*3)%14,y),fill=ink(slot('water',4)))
            return image
        emit(name,[16,16],[f'variant_{i}' for i in range(count)],frames,draw,
             asset_id='snow_cover' if name=='snow_cover_dusting' else name,set_type='variants',tileable=True)
    for size in [16,32]:
        def crack(variant,frame,size=size):
            image=blank((size,size)); d=ImageDraw.Draw(image)
            rng=np.random.default_rng(stable('crack')+seed+variant*53+size)
            x=size//2; points=[]
            for y in range(2,size-2,2):
                x=int(np.clip(x+int(rng.integers(-2,3)),3,size-4)); points.append((x,y))
            d.line(points,fill=ink(slot('soil',0)),width=1 if size==16 else 2)
            for x,y in points[2:-2:3]:
                direction=-1 if y%4 else 1
                d.line([(x,y),(x+direction*2,y+1),(x+direction*3,y+3)],fill=ink(slot('soil',0)))
            return image
        emit(f'crack_{size}',[size,size],[f'variant_{i}' for i in range(4)],1,crack,asset_id='crack',set_type='variants')
    for size in [32,48]:
        def crater(variant,frame,size=size):
            image=blank((size,size))
            for y in range(size):
                for x in range(size):
                    dx=(x-(size-1)/2)/(size*.43); dy=(y-(size-1)/2)/(size*.29)
                    radius=math.sqrt(dx*dx+dy*dy)+.035*math.sin(x*1.2+seed)*math.cos(y*.8)
                    if radius<1:
                        shade=1 if radius<.72 else (3 if dx+dy<0 else 2)
                        if radius<.55 and stable(f'{seed}:{x}:{y}')%13==0: shade=0
                        image.putpixel((x,y),ink(slot('soil',shade)))
            return image
        emit(f'crater_{size}',[size,size],['variant_0'],1,crater,asset_id='crater',set_type='variants',shadow='engine')
    def footprints(variant,frame):
        image=blank(); d=ImageDraw.Draw(image)
        rng=np.random.default_rng(stable('footprint')+seed+variant)
        for i in range(4):
            x=6+(i%2)*3+int(rng.integers(-1,2)); y=2+i*3
            d.line((x,y,x,y+1),fill=ink(slot('soil',1)))
        return image.rotate(variant*90,Image.Resampling.NEAREST)
    emit('footprint_trail',[16,16],[f'variant_{i}' for i in range(4)],1,footprints,
         asset_id='footprint_trail',set_type='variants',optional=True)
    return entries
