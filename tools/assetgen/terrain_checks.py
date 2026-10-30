"""Exhaustive compatible edge checks plus seeded random dual-grid seam tests."""
import numpy as np
from PIL import Image
from families.terrain import MATERIALS, geometry

def validate_terrain(root,manifest,check):
    entries=[e for e in manifest['assets'] if e['family']=='terrain']
    for material in MATERIALS:
        for kind in ['plain','corners']:
            check(any(e['id']==f'terrain.{material}_{kind}' for e in entries),f'Missing terrain {material}_{kind}')
    for e in entries:
        a=np.asarray(Image.open(root/'assets'/e['files'][0]))
        n=len(e['sheet_rows']); frames=e['frames']
        tiles=[[a[r*16:(r+1)*16,f*16:(f+1)*16] for f in range(frames)] for r in range(n)]
        for r in range(n):
            for f in range(frames):
                check(len(np.unique(tiles[r][f].reshape(-1,4),axis=0))<=17,f'Too many colours: {e["id"]}')
        if 'plain' in e['tags'] or 'cliff_wall' in e['tags']:
            check(len({b''.join(t.tobytes() for t in row) for row in tiles})==n,f'Duplicate variants: {e["id"]}')
        if 'plain' in e['tags']:
            for group in range(len(e['orientations'])):
                variants=tiles[group*4:(group+1)*4]
                for f in range(frames):
                    for first in variants:
                        for second in variants:
                            check(bool(np.array_equal(first[f][:,-1],second[f][:,0])),f'Plain X seam: {e["id"]}')
                            check(bool(np.array_equal(first[f][-1],second[f][0])),f'Plain Y seam: {e["id"]}')
        if frames>1:
            for r in range(n):
                if np.any(tiles[r][0][:,:,3]):
                    check(len({t.tobytes() for t in tiles[r]})==frames,f'Duplicate animation frames: {e["id"]} row {r}')
        if not e['masks']: continue
        check(e['masks']==list(range(16)),f'Incomplete masks: {e["id"]}')
        for group in range(n//16):
            for f in range(frames):
                block=[tiles[group*16+m][f] for m in range(16)]
                for m in range(16):
                    expected,_=geometry(m,e['material'] in {'deep_water','water','shallow_water'} and 'cliff' not in e['id'])
                    check(bool(np.array_equal(block[m][:,:,3]>0,expected)),f'Wrong corner coverage: {e["id"]}:{m}')
                for left in range(16):
                    for right in range(16):
                        if bool(left&2)==bool(right&1) and bool(left&4)==bool(right&8):
                            check(bool(np.array_equal(block[left][:,-1],block[right][:,0])),f'Horizontal seam {e["id"]} {left}/{right}')
                        if bool(left&8)==bool(right&1) and bool(left&4)==bool(right&2):
                            check(bool(np.array_equal(block[left][-1],block[right][0])),f'Vertical seam {e["id"]} {left}/{right}')
                rng=np.random.default_rng(20261030)
                cells=rng.integers(0,2,(25,25))
                masks=cells[:-1,:-1]+2*cells[:-1,1:]+4*cells[1:,1:]+8*cells[1:,:-1]
                for y in range(23):
                    for x in range(23):
                        here=block[masks[y,x]]
                        check(bool(np.array_equal(here[:,-1],block[masks[y,x+1]][:,0])),f'Random X seam: {e["id"]}')
                        check(bool(np.array_equal(here[-1],block[masks[y+1,x]][0])),f'Random Y seam: {e["id"]}')
