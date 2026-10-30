"""Terrain sheets with frame sequences, seasonal rows and random dual-grid mosaics."""
import json
import numpy as np
from PIL import Image, ImageDraw

def build_terrain_preview(root):
    entries=[e for e in json.loads((root/'assets/manifest.json').read_text())['assets'] if e['family']=='terrain']
    palette=json.loads((root/'assets/palette/palette.json').read_text())
    lut=Image.open(root/'assets/palette/lut_nature.png')
    for material in dict.fromkeys(e['material'] for e in entries):
        group=[e for e in entries if e['material']==material]
        height=60+sum(len(e['sheet_rows'])*76+30 for e in group)+210
        sheet=Image.new('RGB',(1100,height),'#1e1813'); d=ImageDraw.Draw(sheet)
        d.text((10,10),f'{material} | 4x frames left to right | native grass/dark strips | masks TL=1 TR=2 BR=4 BL=8',fill='#f1e6d9')
        yy=40
        for e in group:
            source=Image.open(root/'assets'/e['files'][0])
            d.text((10,yy),e['id'],fill='#f1e6d9'); yy+=24
            for r,label in enumerate(e['sheet_rows']):
                d.text((10,yy+20),label,fill='#f1e6d9')
                for f in range(e['frames']):
                    tile=source.crop((f*16,r*16,(f+1)*16,(r+1)*16))
                    x=160+f*72
                    d.rectangle((x,yy,x+63,yy+63),fill='#72864d')
                    sheet.paste(tile.resize((64,64),Image.Resampling.NEAREST),(x,yy),tile.resize((64,64),Image.Resampling.NEAREST))
                    for k,bg in enumerate(['#72864d','#1e1813']):
                        bx=490+f*40+k*18
                        d.rectangle((bx,yy+24,bx+15,yy+39),fill=bg)
                        sheet.paste(tile,(bx,yy+24),tile)
                yy+=76
            yy+=6
        e=next(e for e in group if 'corners' in e['tags'])
        source=Image.open(root/'assets'/e['files'][0])
        rng=np.random.default_rng(41); cells=rng.integers(0,2,(7,7))
        mosaic=Image.new('RGBA',(96,96),'#ac945a')
        for y in range(6):
            for x in range(6):
                mask=int(cells[y,x]+2*cells[y,x+1]+4*cells[y+1,x+1]+8*cells[y+1,x])
                tile=source.crop((0,mask*16,16,(mask+1)*16))
                mosaic.paste(tile,(x*16,y*16),tile)
        d.text((10,yy),'Random mask tiling (2x)',fill='#f1e6d9')
        sheet.paste(mosaic.resize((192,192),Image.Resampling.NEAREST),(10,yy+16))
        d.text((230,yy),'Temperate forest: spring / summer / autumn / winter (4x)',fill='#f1e6d9')
        e=group[0]; idx=np.asarray(Image.open(root/'assets'/e['files'][1]))[:16,:16]
        for i,season in enumerate(['spring','summer','autumn','winter']):
            row=palette['luts']['lut_nature']['rows'].index('temperate_forest.'+season)
            colours=np.array([lut.getpixel((x,row)) for x in range(256)],dtype=np.uint8)
            a=colours[idx[:,:,0]]; a[:,:,3]=idx[:,:,3]
            tile=Image.fromarray(a).resize((64,64),Image.Resampling.NEAREST)
            sheet.paste(tile,(230+i*90,yy+36),tile)
        sheet.crop((0,0,1100,yy+210)).save(root/f'assets/_preview/terrain_{material}.png')
    for page in range(2):
        materials=list(dict.fromkeys(e['material'] for e in entries))[page*11:(page+1)*11]
        sheet=Image.new('RGB',(1100,len(materials)*168+40),'#1e1813'); d=ImageDraw.Draw(sheet)
        d.text((10,10),'Terrain overview | 4x | plain variants and animation frames / masks 0-15',fill='#f1e6d9')
        for r,material in enumerate(materials):
            yy=40+r*168
            d.text((10,yy),material,fill='#f1e6d9')
            group=[e for e in entries if e['material']==material]
            plain=next(e for e in group if 'plain' in e['tags'])
            corners=next(e for e in group if 'corners' in e['tags'])
            for offset,e in [(18,plain),(88,corners)]:
                source=Image.open(root/'assets'/e['files'][0])
                n=0
                for row in range(4 if offset==18 else 16):
                    for f in range(e['frames'] if offset==18 else 1):
                        tile=source.crop((f*16,row*16,(f+1)*16,(row+1)*16)).resize((64,64),Image.Resampling.NEAREST)
                        x=10+n*67
                        d.rectangle((x,yy+offset,x+63,yy+offset+63),fill='#d4c3b1')
                        sheet.paste(tile,(x,yy+offset),tile); n+=1
        sheet.save(root/f'assets/_preview/terrain_overview_{page+1}.png')
