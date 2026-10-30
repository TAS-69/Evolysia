"""Overlay completeness, edge compatibility and native pixel contracts."""
import numpy as np
from PIL import Image
from overlay_preview import network

EXPECTED={'river_stream':(16,4,16),'river_river':(16,4,16),'river_great_river':(16,4,16),
    'road_trampled_path':(16,1,16),'road_packed_dirt_road':(16,1,16),'road_stone_road':(16,1,16),
    'territory_border':(16,1,16),'snow_cover':(16,1,16),'snow_cover_dusting':(1,1,16),
    'frost':(3,1,16),'wet_ground':(3,1,16),'ash_fall':(3,1,16),'burned_ground':(3,1,16),
    'flood_water':(3,4,16),'crack_16':(4,1,16),'crack_32':(4,1,32),
    'crater_32':(1,1,32),'crater_48':(1,1,48),'footprint_trail':(4,1,16)}

def validate_overlays(root,manifest,check):
    entries=[e for e in manifest['assets'] if e['family']=='overlays']
    check({e['id'].split('.',1)[1] for e in entries}==set(EXPECTED),'Overlay asset set incomplete')
    for e in entries:
        name=e['id'].split('.',1)[1]; w,h=e['frame']; rows=len(e['sheet_rows']); frames=e['frames']
        check((rows,frames,w)==EXPECTED[name] and w==h,f'Overlay size/frame contract: {name}')
        a=np.array(Image.open(root/'assets'/e['files'][0]))
        tiles=[[a[r*h:(r+1)*h,f*w:(f+1)*w] for f in range(frames)] for r in range(rows)]
        if name!='territory_border':
            check(len({b''.join(t.tobytes() for t in row) for row in tiles})==rows,f'Duplicate overlay variants: {name}')
        for r in range(rows):
            for tile in tiles[r]:
                check(len(np.unique(tile[tile[:,:,3]>0,:3],axis=0))<=16,f'Overlay colour limit: {name}')
            if frames>1:
                check(len({t.tobytes() for t in tiles[r]})==frames,f'Duplicate overlay animation frames: {name}:{r}')
        if e['set_type']=='connection':
            check(e['masks']==list(range(16)) and e['mask_bits']=={'n':1,'e':2,'s':4,'w':8},f'Connection mask contract: {name}')
            for f in range(frames):
                block=[row[f] for row in tiles]
                for m in range(16):
                    for bit,edge in [(1,block[m][0]),(2,block[m][:,-1]),(4,block[m][-1]),(8,block[m][:,0])]:
                        check(bool(np.any(edge[:,3]))==bool(m&bit),f'Wrong connection port: {name}:{m}:{bit}')
                    for n in range(16):
                        if bool(m&2)==bool(n&8): check(np.array_equal(block[m][:,-1],block[n][:,0]),f'Connection X seam: {name}')
                        if bool(m&4)==bool(n&1): check(np.array_equal(block[m][-1],block[n][0]),f'Connection Y seam: {name}')
                masks=network(412,24)
                for y in range(23):
                    for x in range(23):
                        tile=block[masks[y,x]]
                        check(np.array_equal(tile[:,-1],block[masks[y,x+1]][:,0]),f'Random network X seam: {name}')
                        check(np.array_equal(tile[-1],block[masks[y+1,x]][0]),f'Random network Y seam: {name}')
        if e['set_type']=='corner':
            check(e['masks']==list(range(16)),f'Incomplete corner masks: {name}')
            block=[row[0] for row in tiles]
            for m in range(16):
                for n in range(16):
                    if bool(m&2)==bool(n&1) and bool(m&4)==bool(n&8):
                        check(np.array_equal(block[m][:,-1],block[n][:,0]),f'Corner X seam: {name}')
                    if bool(m&8)==bool(n&1) and bool(m&4)==bool(n&2):
                        check(np.array_equal(block[m][-1],block[n][0]),f'Corner Y seam: {name}')
            rng=np.random.default_rng(510)
            cells=rng.integers(0,2,(25,25))
            masks=cells[:-1,:-1]+2*cells[:-1,1:]+4*cells[1:,1:]+8*cells[1:,:-1]
            for y in range(23):
                for x in range(23):
                    tile=block[masks[y,x]]
                    check(np.array_equal(tile[:,-1],block[masks[y,x+1]][:,0]),f'Random corner X seam: {name}')
                    check(np.array_equal(tile[-1],block[masks[y+1,x]][0]),f'Random corner Y seam: {name}')
        if name=='territory_border':
            check(not np.any(tiles[0][0][:,:,3]) and not np.any(tiles[15][0][:,:,3]),'Solid territory cells must have no border')
            check(all(np.any(tiles[r][0][:,:,3]) for r in range(1,15)),'Missing territory contour')
        if e.get('tileable'):
            for row in tiles:
                for tile in row:
                    check(np.array_equal(tile[0],tile[-1]) and np.array_equal(tile[:,0],tile[:,-1]),f'Surface overlay seam: {name}')
        check((root/f'assets/_preview/{e["id"].replace(".","_")}.png').exists(),f'Missing overlay preview: {name}')
