"""Composable outfits with independent per-piece dye lookups."""
import json
import math
from collections import OrderedDict
import numpy as np
from PIL import Image, ImageDraw
from families.wardrobe import PARTS, ORDER
from families.people import ANIMS

BG='#1e1813'; FG='#f1e6d9'

class Wardrobe:
    def __init__(self,root):
        self.root=root; self.base=root/'assets/wardrobe'; self.cache=OrderedDict(); self.meta={}
        self.config=json.loads((self.base/'customisation.json').read_text()); self.outfits={p['id']:p for p in self.config['outfits']}
        palette=json.loads((root/'assets/palette/palette.json').read_text()); self.slots=palette['slots']
        self.lookup=np.zeros((256,4),np.uint8)
        for s in self.slots: self.lookup[s['index']]=[*s['reference'],255]
        self.luts={kind:np.array(Image.open(root/f'assets/palette/lut_{kind}.png')) for kind in ['skin','hair','cloth','culture']}
        self.groups={name:[s['index'] for s in self.slots if s['name'].startswith(name+'_')] for name in ['skin','hair','cloth_a','cloth_b','culture']}

    def source(self,layer,anim,density):
        stem=f'{layer}_{anim}_lod{density}'
        if stem not in self.meta: self.meta[stem]=json.loads((self.base/f'{stem}.json').read_text())
        if stem not in self.cache:
            a=np.array(Image.open(self.base/f'{stem}_idx.png')); self.cache[stem]=np.where(a[:,:,3]>0,a[:,:,0],255).astype(np.uint8)
            if len(self.cache)>24: self.cache.popitem(last=False)
        self.cache.move_to_end(stem)
        return self.meta[stem],self.cache[stem]

    def tile(self,layer,style,anim,profile,f,density):
        if style is None: return np.full((density,density),255,np.uint8)
        e,a=self.source(layer,anim,density); row=e['row_lookup'][e['variants'].index(style)][e['profiles'].index(list(profile))]
        x=(row%e['strip_columns'])*density*e['frames']+f*density; y=(row//e['strip_columns'])*density
        return a[y:y+density,x:x+density].copy()

    def reference(self,a,skin=3,hair=2,culture=0,dyes=None):
        lookup=self.lookup.copy()
        selections={'skin':('skin',skin),'hair':('hair',hair),'culture':('culture',culture),
                    'cloth_a':('cloth',(dyes or {}).get('main',12)),'cloth_b':('cloth',(dyes or {}).get('trim',12))}
        for group,(kind,row) in selections.items(): lookup[self.groups[group]]=self.luts[kind][row,self.groups[group]]
        return Image.fromarray(lookup[a])

    def compose(self,outfit='farmer',anim='idle',profile=('adult','average','down'),f=0,density=64,pieces=None,dyes=None,skin=None,hair=None,culture=None):
        preset=self.outfits[outfit]; selected={**preset['pieces'],**(pieces or {})}; colours={**preset['dyes'],**(dyes or {})}
        skin=preset['skin'] if skin is None else skin; hair=(6 if profile[0]=='elder' else preset['hair']) if hair is None else hair
        culture=preset['culture'] if culture is None else culture
        result=Image.new('RGBA',(density,density)); accumulated=np.full((density,density),255,np.uint8)
        for layer in ORDER:
            a=self.tile(layer,selected.get(layer),anim,profile,f,density)
            if layer=='body_paint': a[~np.isin(accumulated,self.groups['skin'])]=255
            result.alpha_composite(self.reference(a,skin,hair,culture,colours.get(layer)))
            visible=a!=255; accumulated[visible]=a[visible]
        return result

def native(sheet,tile,x,y):
    for col,bg in enumerate(['#72864d',BG,BG]):
        sample=Image.new('RGBA',tile.size,bg)
        if col<2: sample.alpha_composite(tile)
        else:
            silhouette=Image.new('RGBA',tile.size,FG); silhouette.putalpha(tile.getchannel('A')); sample.alpha_composite(silhouette)
        sheet.paste(sample.convert('RGB'),(x+col*(tile.width+4),y))

def build_wardrobe_preview(root):
    a=Wardrobe(root); out=root/'assets/_preview'; names=list(a.outfits)
    for page in range(math.ceil(len(names)/16)):
        sheet=Image.new('RGB',(1120,1660),BG); d=ImageDraw.Draw(sheet)
        d.text((10,10),'Editable outfit presets | 4x authored close art | native 64 / 32 / 16 levels below',fill=FG)
        for i,name in enumerate(names[page*16:(page+1)*16]):
            x=10+(i%4)*278; y=38+(i//4)*400; tile=a.compose(name); large=tile.resize((256,256),Image.Resampling.NEAREST)
            d.text((x,y),name,fill=FG); sheet.paste(large,(x,y+18),large); native(sheet,tile,x,y+278)
            for j,density in enumerate([16,32]): native(sheet,a.compose(name,density=density),x+j*86,y+350)
        sheet.save(out/f'wardrobe_outfits_{page+1}.png')
    for layer,styles in PARTS.items():
        if layer=='body': continue
        for page in range(math.ceil(len(styles)/12)):
            shown=styles[page*12:(page+1)*12]; sheet=Image.new('RGB',(1120,40+math.ceil(len(shown)/4)*350),BG); d=ImageDraw.Draw(sheet)
            d.text((10,10),layer+' | selectable parts, 4x + native grass / dark / silhouette',fill=FG)
            for i,style in enumerate(shown):
                x=10+(i%4)*278; y=36+(i//4)*350
                choices={'headwear':None,'accessory':None,'outerwear':None,'armour':None,'held_item':None,'hair_front':'cropped','hair_back':None,layer:style}
                tile=a.compose('farmer',pieces=choices); large=tile.resize((256,256),Image.Resampling.NEAREST)
                d.text((x,y),style,fill=FG); sheet.paste(large,(x,y+18),large); native(sheet,tile,x,y+278)
            sheet.save(out/f'wardrobe_parts_{layer}_{page+1}.png')
    for anim,(frames,fps,_) in ANIMS.items():
        direction=['side'] if anim in {'craft','sleep','dead'} else ['down','up','side']; age='child' if anim=='play' else 'adult'
        name='guard' if anim=='attack' else 'smith' if anim in {'chop','craft','build'} else 'farmer'
        sheet=Image.new('RGB',(1350,40+len(direction)*350),BG); d=ImageDraw.Draw(sheet)
        d.text((10,10),f'{anim} | {fps} fps | all directions, frames left to right | 4x + native',fill=FG)
        for row,dr in enumerate(direction):
            y=36+row*350; d.text((8,y+20),dr,fill=FG)
            for f in range(frames):
                x=140+f*290; tile=a.compose(name,anim,(age,'average',dr),f); large=tile.resize((256,256),Image.Resampling.NEAREST)
                sheet.paste(large,(x,y),large); native(sheet,tile,x,y+260)
        sheet.save(out/f'wardrobe_anim_{anim}.png')
    sheet=Image.new('RGB',(1700,1990),BG); d=ImageDraw.Draw(sheet)
    d.text((10,10),'Independent top main x trim colours | 13 x 13 combinations | trousers / skin / hair stay fixed | 2x',fill=FG)
    for main in range(13):
        for trim in range(13):
            tile=a.compose('court',pieces={'headwear':None,'accessory':None,'held_item':None},dyes={'top':{'main':main,'trim':trim}})
            tile=tile.resize((128,128),Image.Resampling.NEAREST); x=8+trim*130; y=35+main*150
            d.text((x,y),f'{main}/{trim}',fill=FG); sheet.paste(tile,(x,y+16),tile)
    sheet.save(out/'wardrobe_dyes.png')
    sheet=Image.new('RGB',(1680,1800),BG); d=ImageDraw.Draw(sheet)
    d.text((10,10),'Ages / builds / directions | identical selectable clothes fit the shared animation rig | 4x',fill=FG)
    for i,profile in enumerate([(age,build,dr) for age in ['child','adult','elder'] for build in ['slim','average','stout'] for dr in ['down','up','side']]):
        x=10+(i%6)*278; y=36+(i//6)*350; tile=a.compose('traveller',profile=profile,pieces={'held_item':None}); large=tile.resize((256,256),Image.Resampling.NEAREST)
        d.text((x,y),'/'.join(profile),fill=FG); sheet.paste(large,(x,y+20),large); native(sheet,tile,x,y+280)
    sheet.save(out/'wardrobe_profiles.png')
    sheet=Image.new('RGB',(2240,1450),BG); d=ImageDraw.Draw(sheet)
    d.text((10,10),'Character colour choices | all skin / hair / culture rows | 4x + native strips | clothing colours held fixed',fill=FG)
    choices=[('skin',row) for row in range(8)]+[('hair',row) for row in range(8)]+[('culture',row) for row in range(12)]
    for i,(group,row) in enumerate(choices):
        x=10+i%8*278; y=36+i//8*350
        pieces={'headwear':None,'accessory':None,'held_item':None,'outerwear':None,'armour':None,'body_paint':'face_streak'}
        tile=a.compose('farmer',pieces=pieces,**{group:row})
        large=tile.resize((256,256),Image.Resampling.NEAREST); d.text((x,y),f'{group} row {row}',fill=FG)
        sheet.paste(large,(x,y+18),large); native(sheet,tile,x,y+278)
    sheet.save(out/'wardrobe_recolours.png')
