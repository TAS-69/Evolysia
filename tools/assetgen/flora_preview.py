
import json
import math
import numpy as np
from PIL import Image, ImageDraw

def build_flora_preview(root):
    entries=[e for e in json.loads((root/'assets/manifest.json').read_text())['assets'] if e['family']=='flora']
    palette=json.loads((root/'assets/palette/palette.json').read_text())
    lut=np.array(Image.open(root/'assets/palette/lut_nature.png'))
    cloth=np.array(Image.open(root/'assets/palette/lut_cloth.png'))
    species=list(dict.fromkeys(e['asset_id'] for e in entries))
    for kind in species:
        group=[e for e in entries if e['asset_id']==kind]
        heights=[e['frame'][1]*4+e['frame'][1]+40 for e in group]
        width=max(1120,max(len(e['sheet_rows'])*(e['frames']*(e['frame'][0]*4+4)+12)+24 for e in group))
        sheet=Image.new('RGB',(width,sum(heights)+330),'#1e1813'); d=ImageDraw.Draw(sheet)
        d.text((8,8),f'{kind} | 4x variants / animation frames | 1x grass + dark + silhouette',fill='#f1e6d9')
        yy=32
        for e,rh in zip(group,heights):
            w,h=e['frame']; frames=e['frames']; source=Image.open(root/'assets'/e['files'][0])
            d.text((8,yy),e['id'],fill='#f1e6d9')
            if e['state']=='snow_capped':
                base=Image.open(root/'assets/flora/conifer_mature.png')
            else: base=None
            xx=12
            for v in range(len(e['sheet_rows'])):
                d.text((xx,yy+14),f'v{v}',fill='#d4c3b1')
                for f in range(frames):
                    tile=source.crop((f*w,v*h,(f+1)*w,(v+1)*h))
                    if base is not None: tile=Image.alpha_composite(base.crop((0,v*h,w,(v+1)*h)),tile)
                    large=tile.resize((w*4,h*4),Image.Resampling.NEAREST)
                    sheet.paste(large,(xx,yy+28),large)
                    by=yy+30+h*4
                    for k,bg in enumerate(['#72864d','#1e1813']):
                        bx=xx+k*(w+2)
                        d.rectangle((bx,by,bx+w-1,by+h-1),fill=bg); sheet.paste(tile,(bx,by),tile)
                    silhouette=Image.new('RGBA',(w,h),'#f1e6d9'); silhouette.putalpha(tile.getchannel('A'))
                    sheet.paste(silhouette,(xx+2*(w+2),by),silhouette)
                    xx+=w*4+4
                xx+=12
            yy+=rh
        representative=next((e for e in group if e['state']=='mature'),next((e for e in group if e['state']=='ripe'),group[0]))
        w,h=representative['frame']; idx=np.array(Image.open(root/'assets'/representative['files'][1]))[:h,:w]
        d.text((8,yy),'Seasonal state + LUT: spring / summer / autumn / winter; tropical wet / dry (4x)',fill='#f1e6d9')
        seasons=['temperate_forest.'+s for s in ['spring','summer','autumn','winter']]+['tropical_rainforest.wet','tropical_rainforest.dry']
        for i,label in enumerate(seasons):
            selected=representative.get('seasonal_states',{}).get(label.rsplit('.',1)[1])
            sample=next((e for e in group if e['id']==selected),representative)
            idx=np.array(Image.open(root/'assets'/sample['files'][1]))[:h,:w]
            lookup=lut[palette['luts']['lut_nature']['rows'].index(label)]
            a=lookup[idx[:,:,0]].copy(); a[:,:,3]=idx[:,:,3]
            tile=Image.fromarray(a).resize((w*4,h*4),Image.Resampling.NEAREST)
            x=12+i*(w*4+12); sheet.paste(tile,(x,yy+24),tile)
        if kind=='berry_bush':
            d.text((8,yy+110),'Fruit accents: three cloth dyes (4x)',fill='#f1e6d9')
            fruit=next(e for e in group if e['state']=='fruiting')
            idx=np.array(Image.open(root/'assets'/fruit['files'][1]))[:16,:16]
            for i,dye in enumerate([0,7,9]):
                a=cloth[dye][idx[:,:,0]].copy(); a[:,:,3]=idx[:,:,3]
                tile=Image.fromarray(a).resize((64,64),Image.Resampling.NEAREST)
                sheet.paste(tile,(12+i*80,yy+130),tile)
        sheet.save(root/f'assets/_preview/flora_{kind}.png')
    for page in range(math.ceil(len(species)/12)):
        kinds=species[page*12:(page+1)*12]
        sheet=Image.new('RGB',(1000,math.ceil(len(kinds)/4)*245+40),'#1e1813'); d=ImageDraw.Draw(sheet)
        d.text((8,8),'Flora overview | 4x reference silhouettes',fill='#f1e6d9')
        for i,kind in enumerate(kinds):
            group=[e for e in entries if e['asset_id']==kind]
            e=next((e for e in group if e['state']=='mature'),next((e for e in group if e['state']=='ripe'),group[0]))
            w,h=e['frame']; source=Image.open(root/'assets'/e['files'][0])
            tile=source.crop((0,0,w,h)).resize((w*4,h*4),Image.Resampling.NEAREST)
            x=12+(i%4)*245; y=40+(i//4)*245
            d.text((x,y),kind,fill='#f1e6d9'); sheet.paste(tile,(x,y+20),tile)
        sheet.save(root/f'assets/_preview/flora_overview_{page+1}.png')
