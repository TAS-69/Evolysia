"""Indexed portrait assembly and labelled review galleries."""
import json
import math
import numpy as np
from PIL import Image, ImageDraw
from families.portraits import PARTS, PROFILES, EXPRESSIONS, ORDER, EYE_SHAPES, IRISES, BROW_SHAPES

class PortraitAssembly:
    def __init__(self,root):
        self.root=root
        self.entries={e['layer']:e for e in json.loads((root/'assets/manifest.json').read_text())['assets'] if e['family']=='portraits'}
        self.images={layer:np.array(Image.open(root/'assets'/e['files'][1])) for layer,e in self.entries.items()}
        self.recipe=json.loads((root/'assets/portraits/expressions.json').read_text())
        data=json.loads((root/'assets/palette/palette.json').read_text()); self.slots=data['slots']
        self.lookup=np.zeros((256,4),dtype=np.uint8)
        for s in self.slots: self.lookup[s['index']]=[*s['reference'],255]
        self.luts={kind:np.array(Image.open(root/f'assets/palette/lut_{kind}.png')) for kind in ['skin','hair','cloth','culture']}

    def tile(self,layer,variant,profile):
        if variant is None: return np.zeros((48,48,4),dtype=np.uint8)
        e=self.entries[layer]
        cell=e['row_lookup'][e['variants'].index(variant)][e['profiles'].index(list(profile))]
        x=cell%e['atlas_columns']*48; y=cell//e['atlas_columns']*48
        return self.images[layer][y:y+48,x:x+48].copy()

    def indices(self,profile,emotion='calm',choices=None):
        choices=choices or {}; expr=self.recipe['expressions'][emotion]
        hair=choices.get('hair',self.recipe['defaults']['hair'][profile[0]])
        style=self.recipe['hair_styles'][hair]
        selected={'bg':choices.get('bg','vignette'),'shoulders':choices.get('shoulders','woven_tunic'),'head':'base','ears':'base',
            'eyes':f'{choices.get("eye_shape","almond")}.{choices.get("iris","brown")}.{expr["eyes"]}',
            'brows':f'{choices.get("brow_shape","fine")}.{expr["brows"]}',
            'nose':choices.get('nose','straight'),'mouth':expr['mouth'],
            'facial_hair':None if profile[0]=='child' else choices.get('facial_hair'),
            'hair_back':style['back'],'hair_front':style['front'],'scars':choices.get('scars'),
            'paint':choices.get('paint'),'accessory':choices.get('accessory')}
        result=np.zeros((48,48,4),dtype=np.uint8)
        skin_slots={s['index'] for s in self.slots if s['name'].startswith('skin_')}
        for declared in self.recipe['layer_order']:
            for layer in (self.recipe['hair_sublayers'] if declared=='hair' else [declared]):
                tile=self.tile(layer,selected[layer],profile)
                if layer=='ears' and selected['scars']=='missing_ear':
                    mask=Image.new('L',(48,48)); polygon=self.recipe['scar_operations']['missing_ear']['polygon_by_profile']['.'.join(profile)]
                    ImageDraw.Draw(mask).polygon(polygon,fill=255); tile[np.array(mask)>0]=0
                if layer=='paint':
                    visible=np.isin(result[:,:,0],list(skin_slots)) & (result[:,:,3]>0)
                    tile[~visible]=0
                active=tile[:,:,3]>0; result[active]=tile[active]
        return result

    def reference(self,idx,tints=None):
        lookup=self.lookup.copy()
        for kind,row in (tints or {}).items():
            groups=['cloth_a','cloth_b'] if kind=='cloth' else [kind]
            for s in self.slots:
                if any(s['name'].startswith(group+'_') for group in groups): lookup[s['index']]=self.luts[kind][row,s['index']]
        a=lookup[idx[:,:,0]].copy(); a[:,:,3]=idx[:,:,3]
        return Image.fromarray(a)

    def compose(self,profile,emotion='calm',choices=None,tints=None):
        if profile[0]=='elder' and not (tints and 'hair' in tints): tints={**(tints or {}),'hair':6}
        return self.reference(self.indices(profile,emotion,choices),tints)

def gallery(root,name,samples,assembly,cols=5):
    cellw=245; cellh=274
    sheet=Image.new('RGB',(cols*cellw,34+math.ceil(len(samples)/cols)*cellh),'#1e1813'); d=ImageDraw.Draw(sheet)
    d.text((8,8),name+' | 4x | native grass / dark / silhouette',fill='#f1e6d9')
    for i,(label,profile,emotion,choices,tints) in enumerate(samples):
        x=8+i%cols*cellw; y=34+i//cols*cellh
        d.text((x,y),label,fill='#f1e6d9')
        tile=assembly.compose(profile,emotion,choices,tints)
        large=tile.resize((192,192),Image.Resampling.NEAREST); sheet.paste(large,(x,y+16),large)
        native=assembly.compose(profile,emotion,{**choices,'bg':None},tints)
        for k,bg in enumerate(['#72864d','#1e1813']):
            bx=x+k*52; d.rectangle((bx,y+212,bx+47,y+259),fill=bg); sheet.paste(native,(bx,y+212),native)
        silhouette=Image.new('RGBA',(48,48),'#f1e6d9'); silhouette.putalpha(native.getchannel('A'))
        sheet.paste(silhouette,(x+104,y+212),silhouette)
    sheet.save(root/f'assets/_preview/portraits_{name}.png')

def build_portrait_preview(root):
    a=PortraitAssembly(root); default=('adult','oval')
    for age in ['child','adult','elder']:
        gallery(root,f'expressions_{age}',[(emotion,(age,'oval'),emotion,{},None) for emotion in EXPRESSIONS],a,4)
    gallery(root,'head_shapes',[(f'{age} / {shape}',(age,shape),'calm',{'hair':'shaved'},None) for age,shape in PROFILES],a,3)
    for kind,count in [('skin',8),('hair',8),('cloth',13),('culture',12)]:
        gallery(root,f'lut_{kind}',[(f'{kind} {i}',default,'calm',{'paint':'face_streak','shoulders':'ceremonial'} if kind=='culture' else {},{kind:i}) for i in range(count)],a,4)
    for layer,variants in PARTS.items():
        if layer in {'bg','head','ears','eyes','brows','mouth','hair_back','hair_front'}: continue
        if layer=='scars':
            samples=[(v,default,'calm',{'scars':v,'hair':'cropped'},None) for v in variants]
        elif layer=='paint':
            samples=[(v+(' (below crop)' if v=='leg_marks' else ''),default,'calm',{'paint':v,'shoulders':'none','hair':'cropped'},None) for v in variants]
        else: samples=[(v,default,'calm',{layer:v},None) for v in variants]
        gallery(root,layer,samples,a,4)
    gallery(root,'hair_styles',[(v,default,'calm',{'hair':v},None) for v in a.recipe['hair_styles']],a,5)
    gallery(root,'eye_shapes',[(f'{s} / {iris}',default,'calm',{'eye_shape':s,'iris':iris,'hair':'cropped'},None) for s in EYE_SHAPES for iris in IRISES],a,5)
    gallery(root,'brow_shapes',[(s,default,'anger',{'brow_shape':s,'hair':'shaved'},None) for s in BROW_SHAPES],a,4)
    gallery(root,'overview',[(f'{age} / {emotion}',(age,'oval'),emotion,{},None) for age in ['child','adult','elder'] for emotion in ['calm','joy','fear','anger']],a,4)
