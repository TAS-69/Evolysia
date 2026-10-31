"""Detailed material sheets and seeded dual-grid landscape compositions."""
import json
import math
import numpy as np
from PIL import Image, ImageDraw
from civilisation_preview import native

def build_landscape_preview(root):
    entries=[e for e in json.loads((root/'assets/manifest.json').read_text())['assets'] if e['family']=='landscape' and e['density']==64]
    out=root/'assets/_preview'
    for e in entries:
        n=len(e['sheet_rows'])*e['frames']; sheet=Image.new('RGB',(1120,40+math.ceil(n/4)*350),'#1e1813'); d=ImageDraw.Draw(sheet)
        d.text((10,10),f'{e["asset_id"]} / {e["kind"]} | 4x authored source | native grass / dark / silhouette | frames labelled',fill='#f1e6d9')
        source=Image.open(root/'assets'/e['files'][0])
        for row,label in enumerate(e['sheet_rows']):
            for f in range(e['frames']):
                i=row*e['frames']+f; x=10+i%4*278; y=36+i//4*350
                tile=source.crop((f*64,row*64,(f+1)*64,(row+1)*64)); large=tile.resize((256,256),Image.Resampling.NEAREST)
                d.text((x,y),f'{label} frame {f}',fill='#f1e6d9'); sheet.paste(large,(x,y+18),large); native(sheet,tile,x,y+278)
        sheet.save(out/f'landscape_{e["asset_id"]}_{e["kind"]}.png')
    sheet=Image.new('RGB',(1320,900),'#1e1813'); d=ImageDraw.Draw(sheet)
    pairs=[('grass','loam'),('sandstone','red_clay'),('moss_floor','granite'),('shallow_water','shell_beach')]
    for i,(under,over) in enumerate(pairs):
        x=10+i%2*655; y=38+i//2*430; d.text((x,y),f'{under} + {over} | seeded dual-grid edges, native close artwork',fill='#f1e6d9')
        base=Image.open(root/f'assets/landscape/{under}_plain_lod64.png'); full=Image.open(root/f'assets/landscape/{over}_plain_lod64.png'); corners=Image.open(root/f'assets/landscape/{over}_corners_lod64.png')
        scene=Image.new('RGBA',(640,384)); rng=np.random.default_rng(412+i); cells=rng.integers(0,2,(7,11))
        for yy in range(6):
            for xx in range(10):
                mask=int(cells[yy,xx]+2*cells[yy,xx+1]+4*cells[yy+1,xx+1]+8*cells[yy+1,xx]); variant=int(rng.integers(0,4))
                scene.alpha_composite(base.crop((0,variant*64,64,(variant+1)*64)),(xx*64,yy*64))
                tile=full.crop((0,variant*64,64,(variant+1)*64)) if mask==15 else corners.crop((0,mask*64,64,(mask+1)*64))
                scene.alpha_composite(tile,(xx*64,yy*64))
        sheet.paste(scene.convert('RGB'),(x,y+20))
    sheet.save(out/'landscape_random_tilings.png')
    palette=json.loads((root/'assets/palette/palette.json').read_text()); lut=np.array(Image.open(root/'assets/palette/lut_nature.png'))
    names=['temperate_forest.spring','temperate_forest.summer','temperate_forest.autumn','temperate_forest.winter','savanna.wet','savanna.dry']
    materials=['grass','forest_floor','moss_floor','tundra','salt_marsh','meadow']
    sheet=Image.new('RGB',(1700,2140),'#1e1813'); d=ImageDraw.Draw(sheet)
    d.text((10,10),'Landscape nature LUT choices | 4x authored detail + native grass / dark / silhouette',fill='#f1e6d9')
    for row,material in enumerate(materials):
        idx=np.array(Image.open(root/f'assets/landscape/{material}_plain_lod64_idx.png'))[:64,:64]
        for col,name in enumerate(names):
            n=palette['luts']['lut_nature']['rows'].index(name); a=lut[n][idx[:,:,0]].copy(); a[:,:,3]=idx[:,:,3]
            tile=Image.fromarray(a); large=tile.resize((256,256),Image.Resampling.NEAREST); x=10+col*280; y=36+row*350
            d.text((x,y),material+' / '+name,fill='#f1e6d9'); sheet.paste(large,(x,y+18),large); native(sheet,tile,x,y+278)
    sheet.save(out/'landscape_recolours.png')

