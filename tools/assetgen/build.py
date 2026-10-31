"""Build selected families without discarding other manifest entries."""
import argparse
import json
from pathlib import Path
from families import palette, terrain, overlays, flora, fauna, people, portraits, structures, civilisation, wardrobe, landscape
from preview import build_preview

ROOT=Path(__file__).resolve().parents[2]

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('families',nargs='+',choices=['palette','terrain','overlays','flora','fauna','people','portraits','structures','civilisation','wardrobe','landscape','all'])
    parser.add_argument('--seed',type=int,default=0)
    args=parser.parse_args()
    path=ROOT/'assets/manifest.json'
    previous=json.loads(path.read_text())['assets'] if path.exists() else []
    order=['palette','terrain','overlays','flora','fauna','people','portraits','structures','civilisation','wardrobe','landscape']
    selected=order.copy() if 'all' in args.families else list(dict.fromkeys(args.families))
    selected.sort(key=order.index)
    if any(f!='palette' for f in selected) and 'palette' not in selected and not (ROOT/'assets/palette/palette.json').exists():
        raise SystemExit('Build palette before other families')
    entries=[]
    for name in selected:
        entries.extend({'palette':palette,'terrain':terrain,'overlays':overlays,'flora':flora,'fauna':fauna,'people':people,'portraits':portraits,'structures':structures,'civilisation':civilisation,'wardrobe':wardrobe,'landscape':landscape}[name].build(args.seed))
    manifest={'version':1,'assets':[e for e in previous if e['family'] not in selected]+entries}
    path.write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
    build_preview(ROOT)
    if any(e['family']=='terrain' for e in manifest['assets']):
        from terrain_preview import build_terrain_preview
        build_terrain_preview(ROOT)
    if any(e['family']=='overlays' for e in manifest['assets']):
        from overlay_preview import build_overlay_preview
        build_overlay_preview(ROOT)
    if any(e['family']=='flora' for e in manifest['assets']):
        from flora_preview import build_flora_preview
        build_flora_preview(ROOT)
    if any(e['family']=='fauna' for e in manifest['assets']):
        from fauna_preview import build_fauna_preview
        build_fauna_preview(ROOT)
    if any(e['family']=='people' for e in manifest['assets']):
        from people_preview import build_people_preview
        build_people_preview(ROOT)
    if any(e['family']=='portraits' for e in manifest['assets']):
        from portrait_preview import build_portrait_preview
        build_portrait_preview(ROOT)
    if any(e['family']=='structures' for e in manifest['assets']):
        from structure_preview import build_structure_preview
        build_structure_preview(ROOT)
    if any(e['family']=='civilisation' for e in manifest['assets']):
        from civilisation_preview import build_civilisation_preview
        build_civilisation_preview(ROOT)
    if any(e['family']=='wardrobe' for e in manifest['assets']):
        from wardrobe_preview import build_wardrobe_preview
        build_wardrobe_preview(ROOT)
    if any(e['family']=='landscape' for e in manifest['assets']):
        from landscape_preview import build_landscape_preview
        build_landscape_preview(ROOT)
    print(f'Built {", ".join(selected)}: {len(entries)} assets')

if __name__=='__main__': main()
