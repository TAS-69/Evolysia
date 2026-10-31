
import json
import math
import numpy as np
from PIL import Image, ImageDraw

def network(seed,size=6):
    rng=np.random.default_rng(seed)
    horizontal=rng.integers(0,2,(size,size+1))
    vertical=rng.integers(0,2,(size+1,size))
    return vertical[:-1]+2*horizontal[:,1:]+4*vertical[1:]+8*horizontal[:,:-1]

def build_overlay_preview(root):
    entries=[e for e in json.loads((root/'assets/manifest.json').read_text())['assets'] if e['family']=='overlays']
    palette=json.loads((root/'assets/palette/palette.json').read_text())
    lut=np.array(Image.open(root/'assets/palette/lut_culture.png'))
    summary=Image.new('RGB',(1000,math.ceil(len(entries)/4)*230+30),'#1e1813'); sd=ImageDraw.Draw(summary)
    sd.text((10,8),'Overlay overview | 4x reference samples',fill='#f1e6d9')
    for number,e in enumerate(entries):
        w,h=e['frame']; frames=e['frames']; count=len(e['sheet_rows'])
        cellw=max(180,w*4*frames+16); cols=max(1,min(4,1200//cellw))
        cellh=h*4+46; nrows=math.ceil(count/cols)
        sheet=Image.new('RGB',(max(1000,cols*cellw),nrows*cellh+330),'#1e1813'); d=ImageDraw.Draw(sheet)
        d.text((10,8),e['id']+' | 4x frames left to right | native grass / dark strips',fill='#f1e6d9')
        source=Image.open(root/'assets'/e['files'][0])
        for row,label in enumerate(e['sheet_rows']):
            xx=(row%cols)*cellw+8; yy=30+(row//cols)*cellh
            d.text((xx,yy),label,fill='#f1e6d9')
            for f in range(frames):
                tile=source.crop((f*w,row*h,(f+1)*w,(row+1)*h))
                large=tile.resize((w*4,h*4),Image.Resampling.NEAREST)
                x=xx+f*w*4
                d.rectangle((x,yy+14,x+w*4-1,yy+14+h*4-1),fill='#72864d')
                sheet.paste(large,(x,yy+14),large)
                by=yy+18+h*4
                for k,bg in enumerate(['#72864d','#1e1813']):
                    bx=xx+f*(w*2+6)+k*(w+2)
                    d.rectangle((bx,by,bx+w-1,by+h-1),fill=bg)
                    sheet.paste(tile,(bx,by),tile)
        yy=40+nrows*cellh
        if e['set_type']=='connection':
            masks=network(641)
            mosaic=Image.new('RGBA',(96,96),'#72864d')
            for y in range(6):
                for x in range(6):
                    mask=int(masks[y,x]); tile=source.crop((0,mask*16,16,(mask+1)*16))
                    mosaic.paste(tile,(x*16,y*16),tile)
            d.text((8,yy),'Seeded connected network (2x)',fill='#f1e6d9')
            sheet.paste(mosaic.resize((192,192),Image.Resampling.NEAREST),(8,yy+16))
        elif e['set_type']=='corner':
            rng=np.random.default_rng(732); cells=rng.integers(0,2,(7,7))
            masks=cells[:-1,:-1]+2*cells[:-1,1:]+4*cells[1:,1:]+8*cells[1:,:-1]
            mosaic=Image.new('RGBA',(96,96),'#72864d')
            for y in range(6):
                for x in range(6):
                    mask=int(masks[y,x]); tile=source.crop((0,mask*16,16,(mask+1)*16))
                    mosaic.paste(tile,(x*16,y*16),tile)
            d.text((8,yy),'Seeded dual-grid tiling (2x)',fill='#f1e6d9')
            sheet.paste(mosaic.resize((192,192),Image.Resampling.NEAREST),(8,yy+16))
        if e.get('culture_tinted'):
            idx=np.array(Image.open(root/'assets'/e['files'][1]))[3*h:4*h,:w]
            d.text((240,yy),'All 12 culture hues (4x)',fill='#f1e6d9')
            for c in range(12):
                a=lut[c][idx[:,:,0]].copy(); a[:,:,3]=idx[:,:,3]
                tile=Image.fromarray(a).resize((w*4,h*4),Image.Resampling.NEAREST)
                sheet.paste(tile,(240+(c%6)*(w*4+12),yy+20+(c//6)*(h*4+10)),tile)
        sheet.save(root/f'assets/_preview/{e["id"].replace(".","_")}.png')
        sx=10+(number%4)*245; sy=32+(number//4)*230
        sd.text((sx,sy),e['id'].split('.',1)[1],fill='#f1e6d9')
        r=6 if count==16 else 0
        tile=source.crop((0,r*h,w,(r+1)*h)).resize((w*4,h*4),Image.Resampling.NEAREST)
        sd.rectangle((sx,sy+16,sx+w*4-1,sy+16+h*4-1),fill='#72864d')
        summary.paste(tile,(sx,sy+16),tile)
    summary.save(root/'assets/_preview/overlays_overview.png')
