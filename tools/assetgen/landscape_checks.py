
import numpy as np
from PIL import Image
from families.landscape import MATERIALS, CLIFFS, ANIMATED, DENSITIES

def validate_landscape(root,manifest,check):
    entries=[e for e in manifest['assets'] if e['family']=='landscape']
    expected={f'landscape.{m}_{kind}_lod{d}' for m in MATERIALS for kind in ['plain','corners']+(['cliff_corners','cliff_wall'] if m in CLIFFS else []) for d in DENSITIES}
    check({e['id'] for e in entries}==expected and len(entries)==len(expected),'Detailed terrain inventory mismatch')
    for e in entries:
        name=e['asset_id']; kind=e['kind']; size=e['density']; count=e['variants_per_orientation']; frames=e['frames']
        check(e['frame']==[size,size] and e['logical_frame']==[16,16],f'Landscape scale contract: {e["id"]}')
        check(frames==(4 if name in ANIMATED and not kind.startswith('cliff') else 1),f'Landscape animation frame count: {e["id"]}')
        check(count==(4 if kind=='plain' else 3 if kind=='cliff_wall' else 16),f'Landscape rows: {e["id"]}')
        a=np.array(Image.open(root/'assets'/e['files'][1])); masks=kind.endswith('corners')
        for oi,orientation in enumerate(e['orientations']):
            rows=[a[(oi*count+r)*size:(oi*count+r+1)*size] for r in range(count)]
            for row,strip in enumerate(rows):
                if masks: check((strip[:,:,3]==0).all() if row==0 else (strip[:,:,3]>0).any(),f'Landscape empty-mask coverage: {e["id"]}:{row}')
                else: check((strip[:,:,3]==255).all(),f'Landscape full tile coverage: {e["id"]}:{row}')
                tiles=[strip[:,f*size:(f+1)*size] for f in range(frames)]
                for f,tile in enumerate(tiles): check(len(np.unique(tile[tile[:,:,3]>0,0]))<=16,f'Landscape colour budget: {e["id"]}:{row}:{f}')
                if not(masks and row==0): check(len({t.tobytes() for t in tiles})==frames,f'Landscape repeated animation frame: {e["id"]}:{row}')
            if not masks:
                check(len({r.tobytes() for r in rows})==count,f'Landscape duplicate variants: {e["id"]}')
                for tile in rows:
                    for f in range(frames):
                        t=tile[:,f*size:(f+1)*size]; check(np.array_equal(t[0],t[-1]) and np.array_equal(t[:,0],t[:,-1]),f'Landscape plain seam: {e["id"]}')
            else:
                for f in range(frames):
                    tiles=[r[:,f*size:(f+1)*size] for r in rows]
                    for left in range(16):
                        for right in range(16):
                            if (bool(left&2),bool(left&4))==(bool(right&1),bool(right&8)):
                                check(np.array_equal(tiles[left][:,-1],tiles[right][:,0]),f'Landscape E/W seam: {e["id"]}:{left}:{right}:{f}')
                            if (bool(left&8),bool(left&4))==(bool(right&1),bool(right&2)):
                                check(np.array_equal(tiles[left][-1],tiles[right][0]),f'Landscape S/N seam: {e["id"]}:{left}:{right}:{f}')
                    cells=np.random.default_rng(412).integers(0,2,(10,13)); grid=cells[:-1,:-1]+2*cells[:-1,1:]+4*cells[1:,1:]+8*cells[1:,:-1]
                    for y in range(9):
                        for x in range(12):
                            t=tiles[grid[y,x]]
                            if x<11: check(np.array_equal(t[:,-1],tiles[grid[y,x+1]][:,0]),f'Random landscape E/W seam: {e["id"]}')
                            if y<8: check(np.array_equal(t[-1],tiles[grid[y+1,x]][0]),f'Random landscape S/N seam: {e["id"]}')
        if size==64: check((root/f'assets/_preview/landscape_{name}_{kind}.png').exists(),f'Missing landscape review: {e["id"]}')
    check((root/'assets/_preview/landscape_random_tilings.png').exists(),'Missing landscape tiling overview')
    check((root/'assets/_preview/landscape_recolours.png').exists(),'Missing landscape recolour review')
