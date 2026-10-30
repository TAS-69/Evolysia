"""Validate the current foundation and common manifest contracts."""
import json
import math
from pathlib import Path
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]

def main():
    errors=[]
    def check(condition,message):
        if not condition: errors.append(message)
    data=json.loads((ROOT/'assets/palette/palette.json').read_text())
    manifest=json.loads((ROOT/'assets/manifest.json').read_text())
    colours={tuple(c) for c in data['colours']}
    slots=data['slots']; indices={s['index'] for s in slots}
    check(len(indices)==len(slots) and indices==set(range(len(slots))) and len(slots)<=256,'Invalid slot registry')
    check(len({s['name'] for s in slots})==len(slots),'Duplicate slot names')
    for name,ramp in data['ramps'].items():
        check(all(tuple(c) in colours for c in ramp),f'Ramp missing from palette: {name}')
    ids=set(); files=set()
    for entry in manifest['assets']:
        ident=entry['id']
        check(ident not in ids,f'Duplicate ID: {ident}'); ids.add(ident)
        w,h=entry['frame']; x,y=entry['pivot']
        check(0<=x<w and 0<=y<h,f'Pivot outside frame: {ident}')
        side=json.loads((ROOT/'assets'/entry['sidecar']).read_text())
        for key in ['frame','pivot','directions','frames','fps','files','sheet_rows','recolor_slots','anims','seasonal_states','wind_sway','composite_over','animation','mirror_left','coat_lut','form','source_species','row_lookup','hand_anchors','row_pivots','profiles','variants','sim_actions','clip_rule','atlas_columns','asset_id','state','lighting','materials','emissive_slots','glyph_anchor','fire_window']:
            if key not in entry: continue
            check(side[key]==entry[key],f'Sidecar differs: {ident}.{key}')
        for file in entry['files']:
            check(file not in files,f'Duplicate output: {file}'); files.add(file)
            image=Image.open(ROOT/'assets'/file)
            check(image.mode=='RGBA',f'Not PNG RGBA: {file}')
            a=np.asarray(image)
            expected_size=(w*entry['atlas_columns'],h*math.ceil(len(entry['sheet_rows'])/entry['atlas_columns'])) if 'atlas_columns' in entry else (w*entry['frames'],h*len(entry.get('sheet_rows',entry['directions'])))
            check(image.size==expected_size,f'Sheet dimensions: {file}')
            check(bool(np.isin(a[:,:,3],[0,255]).all()),f'Partial alpha: {file}')
            visible=a[a[:,:,3]>0]
            if file.endswith('_idx.png'):
                check(all(int(c[0]) in indices and c[1]==c[2]==0 for c in visible),f'Invalid index: {file}')
                ref=np.asarray(Image.open(ROOT/'assets'/file.replace('_idx.png','.png')))
                check(bool(np.array_equal(a[:,:,3],ref[:,:,3])),f'Index alpha mismatch: {file}')
                for s in slots:
                    selected=(a[:,:,0]==s['index']) & (a[:,:,3]>0)
                    check(bool((ref[selected,:3]==s['reference']).all()),f'Index/reference colour mismatch: {file}')
            else:
                check(all(tuple(c[:3]) in colours for c in visible),f'Off-palette: {file}')
    for png in (ROOT/'assets').rglob('*.png'):
        relative=png.relative_to(ROOT/'assets').as_posix()
        if not relative.startswith('_preview/'):
            check(relative in files,f'PNG missing from manifest: {relative}')
    if any(e['family']=='terrain' for e in manifest['assets']):
        from terrain_checks import validate_terrain
        validate_terrain(ROOT,manifest,check)
    if any(e['family']=='overlays' for e in manifest['assets']):
        from overlay_checks import validate_overlays
        validate_overlays(ROOT,manifest,check)
    if any(e['family']=='flora' for e in manifest['assets']):
        from flora_checks import validate_flora
        validate_flora(ROOT,manifest,check)
    if any(e['family']=='fauna' for e in manifest['assets']):
        from fauna_checks import validate_fauna
        validate_fauna(ROOT,manifest,check)
    if any(e['family']=='people' for e in manifest['assets']):
        from people_checks import validate_people
        validate_people(ROOT,manifest,check)
    if any(e['family']=='portraits' for e in manifest['assets']):
        from portrait_checks import validate_portraits
        validate_portraits(ROOT,manifest,check)
    if any(e['family']=='structures' for e in manifest['assets']):
        from structure_checks import validate_structures
        validate_structures(ROOT,manifest,check)
    for name,meta in data['luts'].items():
        a=np.asarray(Image.open(ROOT/f'assets/palette/{name}.png'))
        check(len(set(meta['rows']))==len(meta['rows']),f'Duplicate LUT labels: {name}')
        check(bool((a[:,:len(slots),3]==255).all()),f'Missing LUT slots: {name}')
        check(bool((a[:,len(slots):,3]==0).all()),f'Unreserved slots: {name}')
        for slot in slots:
            if slot['name'].startswith('fixed_'):
                check(bool((a[:,slot['index'],:3]==slot['reference']).all()),f'Fixed colour changed: {name}')
        preview=ROOT/f'assets/_preview/{name}.png'
        check(preview.exists(),f'Missing preview: {name}')
    nature=data['luts']['lut_nature']['rows']
    check(len(nature)==70,'Expected 70 biome-season rows')
    check(set(data['luts'])=={'lut_nature','lut_skin','lut_hair','lut_cloth','lut_culture'},'Missing required LUT')
    for kind,count in [('skin',8),('hair',8),('cloth',13),('culture',12)]:
        check(len(data['luts'][f'lut_{kind}']['rows'])==count,f'Wrong {kind} row count')
    def luminance(value):
        c=np.array(list(bytes.fromhex(value.lstrip('#'))))/255
        c=np.where(c<=.04045,c/12.92,((c+.055)/1.055)**2.4)
        return float(c @ np.array([.2126,.7152,.0722]))
    for colour in ['bone','parchment']:
        ratio=(luminance(data['ui'][colour])+.05)/(luminance(data['ui']['panel'])+.05)
        check(ratio>=4.5,f'UI contrast below AA: {colour}')
    gpl=(ROOT/'assets/palette/evolysia.gpl').read_text().splitlines()[4:]
    check([tuple(map(int,line.split()[:3])) for line in gpl]==data_colours(data),'GPL differs from JSON')
    for file in (ROOT/'assets/_preview').glob('*.png'):
        check(file.stat().st_size<=8*1024*1024,f'Preview exceeds 8 MB: {file.name}')
    if errors:
        print('\n'.join('ERROR: '+e for e in errors)); raise SystemExit(1)
    print(f'PASS: zero errors; {len(ids)} assets, {len(colours)} colours, {len(slots)} slots, 70 nature rows')

def data_colours(data): return [tuple(c) for c in data['colours']]

if __name__=='__main__': main()
