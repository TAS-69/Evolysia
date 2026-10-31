
import json
import math
import numpy as np
from PIL import Image, ImageDraw
from families.structures import SPEC

def native(sheet,tile,x,y):
    d=ImageDraw.Draw(sheet); w,h=tile.size
    for k,bg in enumerate(['#72864d','#1e1813']):
        bx=x+k*(w+3); d.rectangle((bx,y,bx+w-1,y+h-1),fill=bg); sheet.paste(tile,(bx,y),tile)
    silhouette=Image.new('RGBA',(w,h),'#f1e6d9'); silhouette.putalpha(tile.getchannel('A'))
    sheet.paste(silhouette,(x+2*(w+3),y),silhouette)

def build_structure_preview(root):
    entries=[e for e in json.loads((root/'assets/manifest.json').read_text())['assets'] if e['family']=='structures']
    lut=np.array(Image.open(root/'assets/palette/lut_culture.png'))
    for kind in SPEC:
        group=[e for e in entries if e['asset_id']==kind]; w,h=group[0]['frame']
        sheet=Image.new('RGB',(1100,45+len(group)*(h*5+35)+(250 if kind=='totem' else 0)),'#1e1813'); d=ImageDraw.Draw(sheet)
        d.text((8,8),f'{kind} | 4x | native grass / dark / silhouette | fire frames left to right',fill='#f1e6d9')
        yy=36
        for e in group:
            d.text((8,yy+16),e['state']+' / '+e['lighting'],fill='#f1e6d9')
            source=Image.open(root/'assets'/e['files'][0])
            for f in range(e['frames']):
                tile=source.crop((f*w,0,(f+1)*w,h)); large=tile.resize((w*4,h*4),Image.Resampling.NEAREST)
                x=190+f*(w*4+28); sheet.paste(large,(x,yy),large); native(sheet,tile,x,yy+h*4+4)
            yy+=h*5+35
        if kind=='totem':
            d.text((8,yy),'Carved-grain accents: all 12 culture LUT rows (4x)',fill='#f1e6d9')
            e=next(e for e in group if e['state']=='complete'); idx=np.array(Image.open(root/'assets'/e['files'][1]))
            for row in range(12):
                a=lut[row][idx[:,:,0]].copy(); a[:,:,3]=idx[:,:,3]
                tile=Image.fromarray(a).resize((w*4,h*4),Image.Resampling.NEAREST)
                sheet.paste(tile,(10+row*(w*4+18),yy+22),tile)
        sheet.save(root/f'assets/_preview/structures_{kind}.png')
    for page in range(2):
        kinds=list(SPEC)[page*9:(page+1)*9]
        sheet=Image.new('RGB',(1000,40+math.ceil(len(kinds)/3)*210),'#1e1813'); d=ImageDraw.Draw(sheet)
        d.text((8,8),'Early structures | complete, 4x | native grass / dark / silhouette',fill='#f1e6d9')
        for i,kind in enumerate(kinds):
            e=next(e for e in entries if e['asset_id']==kind and e['state']=='complete' and e['lighting']=='unlit')
            w,h=e['frame']; tile=Image.open(root/'assets'/e['files'][0])
            x=12+i%3*330; y=36+i//3*210
            d.text((x,y),kind,fill='#f1e6d9'); large=tile.resize((w*4,h*4),Image.Resampling.NEAREST)
            sheet.paste(large,(x,y+18),large); native(sheet,tile,x,y+22+h*4)
        sheet.save(root/f'assets/_preview/structures_overview_{page+1}.png')
