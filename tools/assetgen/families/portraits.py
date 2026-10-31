
import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
from .terrain import stable

ROOT=Path(__file__).resolve().parents[3]
AGES=['child','adult','elder']
SHAPES=['oval','round','angular']
PROFILES=[(age,shape) for age in AGES for shape in SHAPES]
ORDER=['bg','shoulders','head','ears','eyes','brows','nose','mouth','facial_hair','hair','scars','paint','accessory']
EYE_SHAPES=['almond','rounded','hooded','deep_set','upturned','downturned']
IRISES={'brown':('wood',1),'amber':('metal_copper',2),'green':('ui',7),'blue':('water',2),'grey':('rock',2)}
EYE_POSES=['normal','crinkled','narrowed','soft','wide','downcast','closed','averted','side_glance']
BROW_SHAPES=['fine','full','arched','straight']
BROW_POSES=['neutral','raised','raised_high','pinched','furrowed','furrowed_one_side','sad_tilt']
EXPRESSIONS={
    'calm':('neutral','normal','neutral'), 'joy':('raised','crinkled','smile'),
    'pride':('neutral','narrowed','smirk'), 'love':('raised','soft','small_smile'),
    'surprise':('raised_high','wide','open_round'), 'fear':('pinched','wide','grimace'),
    'anger':('furrowed','narrowed','open_shout'), 'disgust':('furrowed_one_side','narrowed','sneer'),
    'sadness':('sad_tilt','downcast','frown'), 'shame':('sad_tilt','averted','pressed'),
    'envy':('furrowed','side_glance','pout')}
HAIR=['short','cropped','long','braided','topknot','mane','tied_back','locks','child_tuft']
PARTS={
    'bg':['vignette'],
    'shoulders':['none','hide_wrap','fur_cloak','woven_tunic','dyed_robe','ceremonial'],
    'head':['base'],'ears':['base'],
    'eyes':[f'{shape}.{iris}.{state}' for shape in EYE_SHAPES for iris in IRISES for state in EYE_POSES],
    'brows':[f'{shape}.{state}' for shape in BROW_SHAPES for state in BROW_POSES],
    'nose':['straight','broad','hooked','button','flat'],
    'mouth':list(dict.fromkeys(v[2] for v in EXPRESSIONS.values())),
    'facial_hair':['stubble','moustache','chin_beard','full_beard'],
    'hair_back':['long','braided','mane','tied_back','locks'], 'hair_front':HAIR,
    'scars':['cheek','brow','missing_ear'],
    'paint':['cheek_dots','brow_band','arm_bands','leg_marks','face_streak','shoulder_dabs'],
    'accessory':['feather_band','bone_necklace','beads','mask','helmet','helmet_bone']}

def geometry(age,shape):
    dy=3 if age=='child' else 0
    eye_y=23 if age=='child' else (22 if age=='elder' else 21)
    mouth_y=31 if age!='adult' else 29
    points={
        'oval':[(19,9),(27,9),(32,13),(34,20),(32,29),(27,34),(21,35),(15,31),(12,25),(12,18),(15,12)],
        'round':[(19,9),(28,10),(34,15),(35,24),(32,31),(27,35),(20,35),(14,31),(11,24),(12,17),(15,12)],
        'angular':[(18,9),(27,9),(33,14),(34,23),(31,31),(23,35),(16,32),(12,27),(12,17),(15,12)]}[shape]
    if age=='child': points=[(x,round(12+(y-9)*.90)) for x,y in points]
    return {'outline':points,'eye_y':eye_y,'mouth_y':mouth_y,'dy':dy,
            'ear_x':35 if shape=='round' else 34,'ear_y':eye_y+1}

class Portrait:
    def __init__(self,slots,profile):
        self.slots=slots; self.age,self.shape=profile; self.g=geometry(*profile)
        self.image=Image.new('L',(48,48),255); self.d=ImageDraw.Draw(self.image)

    def ink(self,ramp,shade):
        prefix='' if ramp in {'skin','hair','cloth_a','cloth_b','culture','grass'} else 'fixed_'
        return self.slots[f'{prefix}{ramp}_{shade}']

    def line(self,points,ramp,shade,width=1): self.d.line(points,fill=self.ink(ramp,shade),width=width)
    def poly(self,points,ramp,shade): self.d.polygon(points,fill=self.ink(ramp,shade))
    def rect(self,box,ramp,shade): self.d.rectangle(box,fill=self.ink(ramp,shade))
    def ellipse(self,box,ramp,shade): self.d.ellipse(box,fill=self.ink(ramp,shade))
    def dot(self,x,y,ramp,shade): self.d.point((x,y),fill=self.ink(ramp,shade))

def draw(layer,variant,profile,seed,slots):
    p=Portrait(slots,profile); g=p.g; age=profile[0]; ey=g['eye_y']; my=g['mouth_y']; dy=g['dy']
    if layer=='bg':
        p.ellipse((2,2,45,46),'culture',0)
        p.ellipse((6,5,42,43),'culture',1)
    elif layer=='head':
        p.poly(g['outline'],'skin',2)
        mask=np.array(p.image)!=255
        for y,x in np.argwhere(mask):
            if x<18 and y<ey+4: p.dot(int(x),int(y),'skin',3)
            if x>30 or y>my+3: p.dot(int(x),int(y),'skin',1)
            if (x>26 or y>my+2) and (x==47 or y==47 or not mask[min(47,y+1),x] or not mask[y,min(47,x+1)]):
                p.dot(int(x),int(y),'skin',0)
        p.poly([(17,13+dy),(21,11+dy),(24,13+dy),(19,16+dy)],'skin',3)
        p.line([(22,ey+5),(25,ey+4),(28,ey+5)],'skin',3)
        if age=='elder':
            p.line([(18,15),(22,14),(27,15)],'skin',0)
            p.line([(18,17),(22,16),(26,17)],'skin',3)
            p.line([(29,ey+1),(31,ey+2)],'skin',0)
            p.line([(29,ey+3),(31,ey+4)],'skin',3)
            p.line([(17,my-3),(16,my)],'skin',0)
    elif layer=='ears':
        x,y=g['ear_x'],g['ear_y']
        p.poly([(x-2,y-2),(x+2,y-1),(x+3,y+2),(x+1,y+6),(x-2,y+5)],'skin',2)
        p.line([(x,y),(x+1,y+1),(x,y+3)],'skin',0)
        p.dot(x-1,y-1,'skin',3)
    elif layer=='shoulders':
        inset=4 if age=='child' else 0
        p.poly([(21,31+dy),(28,32+dy),(29,38),(37-inset,39),(43-inset,46),
                (5+inset,46),(10+inset,39),(20,37)],'skin',2)
        p.line([(21,34+dy),(22,38),(27,39)],'skin',3)
        if variant!='none':
            p.poly([(10+inset,39),(19,37),(24,41),(29,37),(37-inset,39),(43-inset,46),(5+inset,46)],'cloth_a',2)
            p.line([(8+inset,43),(16,39),(21,40)],'cloth_a',3)
            p.poly([(33,39),(37-inset,40),(43-inset,46),(36,46)],'cloth_a',0)
            if variant=='hide_wrap':
                p.poly([(10+inset,39),(19,37),(22,39),(14,46),(5+inset,46)],'skin',2)
                p.line([(26,38),(19,46)],'cloth_a',0)
            if variant=='fur_cloak':
                for x in range(11+inset,36-inset,3): p.poly([(x,38),(x+2,39),(x+1,42)],'cloth_a',3)
            if variant=='woven_tunic':
                for x in range(12,34,3):
                    if stable(f'{seed}:{age}:{x}')%2: p.dot(x,43,'cloth_a',3)
            if variant=='dyed_robe': p.line([(19,39),(25,44),(30,39)],'cloth_a',0)
            if variant=='ceremonial':
                p.line([(16,39),(20,46)],'culture',2,2)
                p.line([(32,39),(29,46)],'culture',2,2)
    elif layer=='eyes':
        shape,iris,state=variant.split('.')
        for near,(x0,x1) in enumerate([(14,18),(22,29)]):
            y=ey-(1 if near==0 else 0)
            height=4 if age=='child' else 3
            if shape=='rounded': height+=1
            if state=='wide': height+=2
            if state in {'narrowed','soft','downcast','side_glance'}: height=2
            if state in {'closed','averted'}:
                p.line([(x0,y+1),(x0+2,y+2),(x1,y+1 if state=='closed' else y+2)],'skin',0)
                if shape in {'upturned','deep_set'}: p.dot(x1,y,'skin',0)
                continue
            if shape=='rounded': p.ellipse((x0,y,x1,y+height),'ui',4)
            else: p.poly([(x0,y+height//2),(x0+1,y),(x1-1,y),(x1,y+height//2),(x1-1,y+height),(x0+1,y+height)],'ui',4)
            shift=-1 if state=='side_glance' else 0
            cx=(x0+x1)//2-1+shift
            cy=y+height//2+(state=='downcast')
            p.rect((max(x0+1,cx),y+1,max(x0+1,cx)+1,y+height-1),*IRISES[iris])
            p.dot(max(x0+1,cx),cy,'basalt',0)
            p.line([(x0,y+1),(x0+1,y),(x1-1,y)],'skin',0)
            if shape=='hooded': p.line([(x0+1,y-1),(x1-1,y-1)],'skin',0)
            if shape=='deep_set': p.line([(x0,y-2),(x0+2,y-2)],'skin',0)
            if shape=='upturned': p.dot(x1,y-1,'skin',0)
            if shape=='downturned': p.dot(x0,y+height,'skin',0)
            if state=='crinkled': p.line([(x1+1,y+1),(x1+1,y+2)],'skin',0)
            if state=='soft': p.line([(x0+1,y+height),(x1,y+height)],'skin',0)
    elif layer=='brows':
        shape,state=variant.split('.')
        for near,(x0,x1) in enumerate([(14,18),(22,29)]):
            y=ey-4-(near==0)
            if state=='raised': y-=2
            if state=='raised_high': y-=4
            points=[(x0,y),(x1,y)]
            if state=='pinched': points=[(x0,y-2),(x1,y)] if near==0 else [(x0,y),(x1,y-2)]
            if state=='furrowed' or (state=='furrowed_one_side' and near==1):
                points=[(x0,y),(x1,y+2)] if near==0 else [(x0,y+2),(x1,y)]
            if state=='sad_tilt': points=[(x0,y+1),(x1,y-1)] if near==0 else [(x0,y-1),(x1,y+1)]
            if shape=='arched': points.insert(1,((x0+x1)//2,min(v[1] for v in points)-1))
            if shape=='fine': points[0]=(x0+1,points[0][1])
            if shape=='straight': points=[points[0],(x1-1,points[-1][1])]
            p.line(points,'hair',0,2 if shape=='full' else 1)
    elif layer=='nose':
        x=19; y=ey+2
        shape={'straight':[(x,y),(x-2,y+4),(x+1,y+5)],
               'broad':[(x+1,y),(x-3,y+4),(x+2,y+5)],
               'hooked':[(x,y),(x-3,y+4),(x-1,y+6),(x+1,y+5)],
               'button':[(x-1,y+2),(x-2,y+4),(x+1,y+4)],
               'flat':[(x-1,y+2),(x-3,y+4),(x+2,y+4)]}[variant]
        p.poly(shape,'skin',3); p.line(shape[-2:],'skin',0); p.dot(x-1,y+3,'skin',3)
    elif layer=='mouth':
        x=20; y=my
        if variant in {'open_round','open_shout'}:
            w,h=(4,4) if variant=='open_round' else (7,5)
            p.ellipse((x-w//2,y-1,x+w//2,y+h-1),'skin',0)
            if variant=='open_shout': p.line([(x-2,y),(x+2,y)],'ui',4)
        elif variant=='grimace':
            p.rect((x-3,y,x+3,y+2),'skin',0); p.line([(x-2,y+1),(x+2,y+1)],'ui',4)
        else:
            shapes={'neutral':[(x-3,y),(x+3,y)],'smile':[(x-4,y-1),(x-2,y+1),(x+1,y+2),(x+4,y)],
                'smirk':[(x-3,y+1),(x+1,y+1),(x+4,y-1)],
                'small_smile':[(x-2,y),(x,y+1),(x+2,y)],
                'sneer':[(x-3,y+1),(x,y-1),(x+3,y)],
                'frown':[(x-3,y+2),(x,y),(x+3,y+2)],
                'pressed':[(x-2,y+1),(x+2,y+1)],
                'pout':[(x-2,y),(x+1,y),(x+2,y+1),(x-1,y+2)]}
            p.line(shapes[variant],'skin',0)
    elif layer=='facial_hair':
        if variant in {'chin_beard','full_beard'}:
            p.poly([(18,my+2),(26,my+2),(28,33),(23,37),(18,34)],'hair',1)
            p.line([(19,34),(22,36)],'hair',3)
        if variant in {'moustache','full_beard'}:
            p.line([(16,my-1),(19,my-2),(22,my-1),(25,my-1)],'hair',0)
        if variant=='stubble':
            for x,y in [(17,31),(20,33),(24,34),(28,31),(26,33)]: p.dot(x,y,'hair',1)
    elif layer in {'hair_back','hair_front'}:
        top=8+dy
        if layer=='hair_back':
            if variant in {'long','mane'}:
                p.ellipse((8 if variant=='long' else 6,top,39,43),'hair',0)
                p.ellipse((10,top+1,36,41),'hair',1)
                p.line([(10,top+6),(10,36)],'hair',3)
            elif variant=='braided':
                for side in [10,36]:
                    for y in range(top+7,42,3): p.rect((side+y%2,y,side+2+y%2,y+2),'hair',1)
                    p.line([(side,top+8),(side,39)],'hair',3)
            elif variant=='tied_back':
                p.poly([(32,16+dy),(39,20+dy),(40,33),(35,37),(36,24),(31,20+dy)],'hair',1)
                p.line([(36,23),(37,31)],'hair',3)
            else:
                for x in [8,11,35,38]:
                    p.line([(x,top+4),(x+(x%3-1),37+x%4)],'hair',1,2)
                    p.dot(x,top+8,'hair',3)
            head=np.array(draw('head','base',profile,seed,slots)); ears=np.array(draw('ears','base',profile,seed,slots))
            shoulders=np.array(draw('shoulders','none',profile,seed,slots))
            a=np.array(p.image); a[(head!=255)|(ears!=255)|(shoulders!=255)]=255; p.image=Image.fromarray(a)
        else:
            if variant=='child_tuft':
                p.poly([(20,top+6),(22,top),(25,top+3),(28,top+1),(27,top+7)],'hair',1)
            else:
                p.poly([(11,top+8),(12,top+2),(19,top-1),(28,top),(34,top+4),(35,top+10),
                        (31,top+6),(25,top+5),(19,top+4),(15,top+8)],'hair',1)
                p.line([(13,top+4),(19,top+1),(25,top+2)],'hair',3)
                if variant=='cropped': p.line([(16,top+6),(30,top+6)],'hair',0)
                if variant=='short': p.line([(13,top+7),(17,top+5)],'hair',0)
                if variant in {'long','mane'}:
                    for x in ([10,35] if variant=='long' else [8,10,35,37]): p.line([(x,top+6),(x+1,30)],'hair',1,2)
                if variant=='braided': p.line([(14,top+8),(18,top+5),(21,top+8)],'hair',0)
                if variant=='topknot':
                    p.ellipse((20,2+dy,29,9+dy),'hair',1); p.line([(22,3+dy),(26,3+dy),(27,4+dy)],'hair',3)
                if variant=='tied_back': p.line([(27,top+4),(34,top+8),(36,top+12)],'hair',0)
                if variant=='locks':
                    for x in [14,18,23,29,33]: p.line([(x,top+3),(x-1,top+10+x%2)],'hair',0)
    elif layer=='scars':
        if variant=='cheek':
            p.line([(26,ey+5),(30,ey+8)],'skin',0); p.line([(26,ey+4),(30,ey+7)],'skin',3)
        elif variant=='brow': p.line([(24,ey-6),(26,ey-1)],'skin',3)
        else:
            x,y=g['ear_x'],g['ear_y']; p.line([(x-1,y+2),(x,y+4)],'skin',3)
    elif layer=='paint':
        marks={'cheek_dots':[(24,ey+6),(27,ey+6),(29,ey+5)],'brow_band':[(16,ey-6),(18,ey-6),(22,ey-6),(25,ey-6)],
               'arm_bands':[(10,43),(11,43),(38,43),(39,43)],'leg_marks':[],
               'face_streak':[(28,ey+4),(28,ey+5),(28,ey+6),(28,ey+7)],
               'shoulder_dabs':[(14,40),(15,41),(34,40),(35,41)]}[variant]
        for x,y in marks: p.dot(x,y,'culture',2)
    else:
        if variant=='feather_band':
            p.line([(12,15+dy),(32,15+dy)],'culture',2,2)
            p.poly([(32,14+dy),(33,5+dy),(36,3+dy),(36,8+dy)],'ui',4)
        elif variant in {'bone_necklace','beads'}:
            for x,y in [(18,39),(21,41),(25,42),(29,40),(31,38)]:
                p.rect((x,y,x+1,y+1),'ui' if variant=='bone_necklace' else 'culture',4 if variant=='bone_necklace' else 2)
        elif variant=='mask':
            p.poly([(13,18+dy),(27,16+dy),(32,22+dy),(27,30+dy),(18,31+dy),(12,25+dy)],'cloth_a',2)
            p.line([(14,21+dy),(17,21+dy)],'hair',0)
            p.line([(23,21+dy),(28,21+dy)],'hair',0)
        else:
            p.poly([(10,16+dy),(12,8+dy),(20,5+dy),(30,7+dy),(35,12+dy),(36,19+dy),(31,17+dy),(16,16+dy)],'ui' if variant=='helmet_bone' else 'cloth_a',4 if variant=='helmet_bone' else 2)
            p.line([(12,14+dy),(31,15+dy),(35,18+dy)],'hair',0)
    if layer=='mouth':
        head=np.array(draw('head','base',profile,seed,slots))
        a=np.array(p.image); a[head==255]=255; p.image=Image.fromarray(a)
    return p.image

def build(seed=0):
    data=json.loads((ROOT/'assets/palette/palette.json').read_text()); slots={s['name']:s['index'] for s in data['slots']}
    lut=np.zeros((256,4),dtype=np.uint8)
    for s in data['slots']: lut[s['index']]=[*s['reference'],255]
    out=ROOT/'assets/portraits'; out.mkdir(parents=True,exist_ok=True); entries=[]
    for layer,variants in PARTS.items():
        profiles=[p for p in PROFILES if layer!='facial_hair' or p[0]!='child']
        unique=[]; labels=[]; row_lookup=[]; seen={}
        for variant in variants:
            mapping=[]
            for profile in profiles:
                indices=np.array(draw(layer,variant,profile,seed,slots)); idx=np.zeros((48,48,4),dtype=np.uint8)
                idx[:,:,0]=np.where(indices==255,0,indices); idx[:,:,3]=np.where(indices==255,0,255)
                key=idx.tobytes()
                if key not in seen:
                    seen[key]=len(unique); unique.append(idx); labels.append('.'.join([variant,*profile]))
                mapping.append(seen[key])
            row_lookup.append(mapping)
        cols=min(16,len(unique)); rows=(len(unique)+cols-1)//cols
        idx=np.zeros((48*rows,48*cols,4),dtype=np.uint8)
        for i,tile in enumerate(unique): idx[i//cols*48:(i//cols+1)*48,i%cols*48:(i%cols+1)*48]=tile
        ref=lut[idx[:,:,0]].copy(); ref[:,:,3]=idx[:,:,3]; ref[idx[:,:,3]==0]=0
        Image.fromarray(ref).save(out/f'{layer}.png'); Image.fromarray(idx).save(out/f'{layer}_idx.png')
        used=set(idx[idx[:,:,3]>0,0].tolist())
        e={'id':f'portraits.{layer}','family':'portraits','layer':layer,'frame':[48,48],'frames':1,
           'fps':0,'directions':['three_quarter_left'],'pivot':[24,46],
           'files':[f'portraits/{layer}.png',f'portraits/{layer}_idx.png'],'sidecar':f'portraits/{layer}.json',
           'atlas_columns':cols,'sheet_rows':labels,'variants':variants,'profiles':[list(p) for p in profiles],
           'row_lookup':row_lookup,'cell_formula':'row_lookup[variant_index][profile_index]',
           'cell_xy_formula':'[(cell_index % atlas_columns) * 48, (cell_index // atlas_columns) * 48]',
           'anims':{},'seed':seed,'tags':['portrait',layer],
           'recolor_slots':[s['name'] for s in data['slots'] if s['index'] in used]}
        if layer=='paint': e['clip_rule']='visible_skin_slots'
        if layer=='bg': e['sources']=['portraits/expressions.json']
        (out/f'{layer}.json').write_text(json.dumps(e,indent=2)+'\n',encoding='utf-8'); entries.append(e)
    expressions={'version':1,'layer_order':ORDER,'hair_sublayers':['hair_back','hair_front'],
        'expressions':{emotion:dict(zip(['brows','eyes','mouth'],parts)) for emotion,parts in EXPRESSIONS.items()},
        'defaults':{'eye_shape':'almond','iris':'brown','brow_shape':'fine','nose':'straight','shoulders':'woven_tunic',
            'hair':{'child':'child_tuft','adult':'short','elder':'cropped'},'elder_hair_lut':6},
        'hair_styles':{'shaved':{'front':None,'back':None},**{s:{'front':s,'back':s if s in PARTS['hair_back'] else None} for s in HAIR}},
        'scar_operations':{'missing_ear':{'target_layer':'ears','operation':'subtract_alpha','polygon_by_profile':{
            '.'.join(profile):[[geometry(*profile)['ear_x'],geometry(*profile)['ear_y']-2],
                [geometry(*profile)['ear_x']+4,geometry(*profile)['ear_y']-2],
                [geometry(*profile)['ear_x']+4,geometry(*profile)['ear_y']+3],
                [geometry(*profile)['ear_x'],geometry(*profile)['ear_y']+1]] for profile in PROFILES}}}}
    (out/'expressions.json').write_text(json.dumps(expressions,indent=2)+'\n',encoding='utf-8')
    return entries
