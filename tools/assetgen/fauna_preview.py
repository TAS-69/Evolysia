"""Labelled animal animations, native silhouettes and coat lookup galleries."""
import json
import math
import numpy as np
from PIL import Image, ImageDraw

def build_fauna_preview(root):
    entries=[e for e in json.loads((root/'assets/manifest.json').read_text())['assets'] if e['family']=='fauna']
    names=list(dict.fromkeys(e['id'].split('.',1)[1].removesuffix('_'+e['animation']) for e in entries))
    lut=np.array(Image.open(root/'assets/palette/lut_hair.png'))
    for name in names:
        group=[e for e in entries if e['id']==f'fauna.{name}_{e["animation"]}']
        width=max(1100,max(len(e['sheet_rows'])*(e['frames']*(e['frame'][0]*4+8)+16)+20 for e in group))
        height=40+sum(e['frame'][1]*5+48 for e in group)+220
        sheet=Image.new('RGB',(width,height),'#1e1813'); d=ImageDraw.Draw(sheet)
        d.text((8,8),f'{name} | 4x frames in time order | 1x grass / dark / silhouette | faces right',fill='#f1e6d9')
        yy=32
        for e in group:
            w,h=e['frame']; source=Image.open(root/'assets'/e['files'][0]); xx=12
            d.text((8,yy),f'{e["animation"]} | {e["frames"]} frames | {e["fps"]} fps',fill='#f1e6d9')
            for v in range(len(e['sheet_rows'])):
                d.text((xx,yy+14),f'coat {v}' if len(e['sheet_rows'])>1 else 'reference',fill='#d4c3b1')
                for f in range(e['frames']):
                    tile=source.crop((f*w,v*h,(f+1)*w,(v+1)*h))
                    large=tile.resize((w*4,h*4),Image.Resampling.NEAREST)
                    sheet.paste(large,(xx,yy+28),large)
                    by=yy+30+h*4
                    for k,bg in enumerate(['#72864d','#1e1813']):
                        bx=xx+k*(w+2); d.rectangle((bx,by,bx+w-1,by+h-1),fill=bg); sheet.paste(tile,(bx,by),tile)
                    silhouette=Image.new('RGBA',(w,h),'#f1e6d9'); silhouette.putalpha(tile.getchannel('A'))
                    sheet.paste(silhouette,(xx+2*(w+2),by),silhouette)
                    xx+=w*4+8
                xx+=16
            yy+=h*5+48
        e=group[0]; w,h=e['frame']
        if e['form']=='domestic':
            d.text((8,yy),'All eight hair LUT rows on coat 1 (4x); coat markings preserve the light hide ramp',fill='#f1e6d9')
            idx=np.array(Image.open(root/'assets'/e['files'][1]))[h:2*h,:w]
            for i in range(8):
                a=lut[i][idx[:,:,0]].copy(); a[:,:,3]=idx[:,:,3]
                tile=Image.fromarray(a).resize((w*4,h*4),Image.Resampling.NEAREST)
                sheet.paste(tile,(12+i*(w*4+12),yy+20),tile)
        sheet.save(root/f'assets/_preview/fauna_{name}.png')
    for page in range(math.ceil(len(names)/12)):
        selected=names[page*12:(page+1)*12]
        sheet=Image.new('RGB',(1000,math.ceil(len(selected)/4)*170+40),'#1e1813'); d=ImageDraw.Draw(sheet)
        d.text((8,8),'Fauna overview | 4x reference | native grass / dark / silhouette',fill='#f1e6d9')
        for i,name in enumerate(selected):
            e=next(e for e in entries if e['id']==f'fauna.{name}_{e["animation"]}')
            w,h=e['frame']; tile=Image.open(root/'assets'/e['files'][0]).crop((0,0,w,h))
            x=12+i%4*245; y=40+i//4*170
            d.text((x,y),name,fill='#f1e6d9')
            large=tile.resize((w*4,h*4),Image.Resampling.NEAREST); sheet.paste(large,(x,y+16),large)
            by=y+20+h*4
            for k,bg in enumerate(['#72864d','#1e1813']):
                bx=x+k*(w+2); d.rectangle((bx,by,bx+w-1,by+h-1),fill=bg); sheet.paste(tile,(bx,by),tile)
            silhouette=Image.new('RGBA',(w,h),'#f1e6d9'); silhouette.putalpha(tile.getchannel('A'))
            sheet.paste(silhouette,(x+2*(w+2),by),silhouette)
        sheet.save(root/f'assets/_preview/fauna_overview_{page+1}.png')
