
import json
import math
import numpy as np
from PIL import Image, ImageDraw
from families.people import ANIMS, ORDER, PARTS, profiles

class Assembly:
    def __init__(self,root):
        self.root=root
        self.entries={e['id']:e for e in json.loads((root/'assets/manifest.json').read_text())['assets'] if e['family']=='people'}
        self.images={key:np.array(Image.open(root/'assets'/e['files'][1])) for key,e in self.entries.items()}
        palette=json.loads((root/'assets/palette/palette.json').read_text())
        self.slots=palette['slots']; self.lookup=np.zeros((256,4),dtype=np.uint8)
        for s in self.slots: self.lookup[s['index']]=[*s['reference'],255]
        self.luts={k:np.array(Image.open(root/f'assets/palette/lut_{k}.png')) for k in ['skin','hair','cloth','culture']}

    def tile(self,layer,variant,anim,profile,f):
        if variant is None: return np.zeros((16,16,4),dtype=np.uint8)
        e=self.entries[f'people.{layer}_{anim}']; n=e['pose_row_count']
        row=e['row_lookup'][e['variants'].index(variant)][e['profiles'].index(list(profile))]
        return self.images[e['id']][row*16:(row+1)*16,f*16:(f+1)*16].copy()

    def reference(self,idx,tints=None):
        lookup=self.lookup.copy()
        for kind,row in (tints or {}).items():
            for slot in self.slots:
                groups=['cloth_a','cloth_b'] if kind=='cloth' else [kind]
                if any(slot['name'].startswith(g+'_') for g in groups): lookup[slot['index']]=self.luts[kind][row,slot['index']]
        a=lookup[idx[:,:,0]].copy(); a[:,:,3]=idx[:,:,3]
        return Image.fromarray(a)

    def compose(self,anim,profile,f,choices=None,tints=None):
        selected={'hair_back':'long','body':'base','clothing':'woven_tunic','body_paint':'cheek_dots',
                  'hair_front':'long','accessory':None,'held_item':None}
        selected.update(choices or {})
        tiles={layer:self.tile(layer,selected[layer],anim,profile,f) for layer in ORDER}
        body=tiles['body'][:,:,3]>0; cloth=tiles['clothing'][:,:,3]>0
        tiles['body_paint'][~(body & ~cloth)]=0
        result=Image.new('RGBA',(16,16))
        if profile[0]=='elder' and not (tints and 'hair' in tints): tints={**(tints or {}),'hair':6}
        for layer in ORDER: result=Image.alpha_composite(result,self.reference(tiles[layer],tints))
        return result

def native(sheet,tile,x,y):
    d=ImageDraw.Draw(sheet)
    for i,bg in enumerate(['#72864d','#1e1813']):
        xx=x+i*18; d.rectangle((xx,y,xx+15,y+15),fill=bg); sheet.paste(tile,(xx,y),tile)
    silhouette=Image.new('RGBA',(16,16),'#f1e6d9'); silhouette.putalpha(tile.getchannel('A'))
    sheet.paste(silhouette,(x+36,y),silhouette)

def build_people_preview(root):
    assembly=Assembly(root)
    items={'gather':'basket','chop':'stone_axe','dig':'digging_stick','attack':'spear','carry':'bundle',
           'build':'stone_axe','craft':'basket','eat':'basket','drink':'basket','pray':'staff'}
    for anim,(frames,_,_) in ANIMS.items():
        rows=profiles(anim)
        sheet=Image.new('RGB',(1120,40+len(rows)*100),'#1e1813'); d=ImageDraw.Draw(sheet)
        d.text((8,8),f'People {anim} | all profiles and directions | 4x frames | 1x grass / dark / silhouette',fill='#f1e6d9')
        for r,profile in enumerate(rows):
            yy=36+r*100
            d.text((8,yy+20),' / '.join(profile),fill='#f1e6d9')
            for f in range(frames):
                choices={'held_item':items.get(anim)}
                if profile[0]=='child': choices.update(hair_front='child_tuft',hair_back=None)
                tile=assembly.compose(anim,profile,f,choices)
                x=210+f*86; large=tile.resize((64,64),Image.Resampling.NEAREST)
                sheet.paste(large,(x,yy),large); native(sheet,tile,x,yy+67)
        sheet.save(root/f'assets/_preview/people_anim_{anim}.png')
    for layer,variants in PARTS.items():
        n=len(variants)*len(profiles('idle')); cols=9; rh=104
        sheet=Image.new('RGB',(1180,40+math.ceil(n/cols)*rh),'#1e1813'); d=ImageDraw.Draw(sheet)
        d.text((8,8),f'{layer} | idle frame 0, all variants / ages / builds / directions | 4x + native',fill='#f1e6d9')
        number=0
        for variant in variants:
            for profile in profiles('idle'):
                x=8+(number%cols)*130; y=36+(number//cols)*rh; number+=1
                d.text((x,y),variant,fill='#f1e6d9')
                d.text((x,y+12),f'{profile[0][0]}/{profile[1][:2]}/{profile[2][0]}',fill='#d4c3b1')
                if layer=='body': tile=assembly.compose('idle',profile,0,{'hair_front':None,'hair_back':None,'clothing':'none'})
                elif layer in {'hair_front','hair_back'}:
                    tile=assembly.compose('idle',profile,0,{'hair_front':variant if layer=='hair_front' else None,'hair_back':variant if layer=='hair_back' else None})
                elif layer=='body_paint':
                    tile=assembly.compose('idle',profile,0,{'body_paint':variant,'clothing':'none','hair_front':'cropped','hair_back':None})
                else: tile=assembly.compose('idle',profile,0,{layer:variant})
                large=tile.resize((64,64),Image.Resampling.NEAREST); sheet.paste(large,(x,y+25),large)
                native(sheet,tile,x+68,y+47)
        sheet.save(root/f'assets/_preview/people_parts_{layer}.png')
    sheet=Image.new('RGB',(1120,650),'#1e1813'); d=ImageDraw.Draw(sheet)
    for r,(kind,count) in enumerate([('skin',8),('hair',8),('cloth',13),('culture',12)]):
        yy=12+r*145; d.text((8,yy),f'All {kind} LUT rows (4x)',fill='#f1e6d9')
        for i in range(count):
            choices={'clothing':'ceremonial','body_paint':'face_streak'} if kind=='culture' else {}
            tile=assembly.compose('idle',('adult','average','down'),0,choices,{kind:i})
            x=12+i*80; large=tile.resize((64,64),Image.Resampling.NEAREST)
            sheet.paste(large,(x,yy+20),large); native(sheet,tile,x,yy+87)
    sheet.save(root/'assets/_preview/people_recolours.png')
    sheet=Image.new('RGB',(1120,40+math.ceil(len(ANIMS)/3)*255),'#1e1813'); d=ImageDraw.Draw(sheet)
    d.text((8,8),'People animation overview | 4x, frames left to right | down / up / side',fill='#f1e6d9')
    for number,(anim,(frames,_,_)) in enumerate(ANIMS.items()):
        xx=8+(number%3)*370; yy=36+(number//3)*255
        d.text((xx,yy),anim,fill='#f1e6d9')
        selected=[p for p in profiles(anim) if p[0]==('child' if anim=='play' else 'adult') and p[1]=='average']
        for r,profile in enumerate(selected):
            d.text((xx,yy+26+r*72),profile[2][0],fill='#d4c3b1')
            for f in range(frames):
                tile=assembly.compose(anim,profile,f,{'held_item':items.get(anim)})
                large=tile.resize((64,64),Image.Resampling.NEAREST)
                sheet.paste(large,(xx+18+f*76,yy+18+r*72),large)
    sheet.save(root/'assets/_preview/people_overview.png')
    sheet=Image.new('RGB',(1120,680),'#1e1813'); d=ImageDraw.Draw(sheet)
    rig=json.loads((root/'assets/people/rig.json').read_text())
    categories=[('hair',list(rig['hair_styles'])),('clothing',PARTS['clothing']),
                ('body_paint',PARTS['body_paint']),('accessory',PARTS['accessory']),('held_item',PARTS['held_item'])]
    for r,(layer,variants) in enumerate(categories):
        yy=12+r*132; d.text((8,yy),layer+' | 4x / 1x',fill='#f1e6d9')
        for i,variant in enumerate(variants):
            if layer=='hair':
                style=rig['hair_styles'][variant]; choices={'hair_front':style['front'],'hair_back':style['back']}
            else: choices={layer:variant}
            tile=assembly.compose('idle',('adult','average','down'),0,choices)
            x=12+i*106; d.text((x,yy+14),variant,fill='#d4c3b1')
            sheet.paste(tile.resize((64,64),Image.Resampling.NEAREST),(x,yy+28),tile.resize((64,64),Image.Resampling.NEAREST))
            native(sheet,tile,x,yy+96)
    sheet.save(root/'assets/_preview/people_styles.png')
