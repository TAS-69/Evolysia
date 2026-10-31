"""Room composition, visible inventory and civilisation coverage contracts."""
import json
from .civilisation import CONTAINERS, RESOURCES, OBJECTS, DENSITIES, FLOORS, WALLS, ROOFS, OUTDOOR, VEHICLES

ROADMAP={
    'foraging':{
        'food_water':['gathering_ground','butchery','drying_rack','water_barrel','spring_access'],
        'shelter_health':['camp','windbreak','lean_to','bed','hearth','wash_basin','latrine'],
        'craft_storage':['basket','backpack','knapping_block','chopping_block','hide_scraper','bone_awl','storage_pit']},
    'stonecraft':{
        'craft':['stone_workshop','stone_drill','grinding_slab','millstone','spear_rack','net_rack','canoe'],
        'community':['home','meeting_house','burial_ground','care_shelter']},
    'agriculture':{
        'food':['granary','grain_bin','grain_sack','barn','animal_pen','irrigation_gate','well','bakery','oven','apiary'],
        'craft':['pottery_workshop','potter_wheel','kiln','weaver','loom','dye_vat','tannery','reed_workshop'],
        'logistics':['chest','crate','pallet','handcart','sledge','raft','dock','bridge','market_stall']},
    'metallurgy':{
        'materials':['smithy','forge','anvil','ore_sorting_table','smelter','charcoal_clamp','foundry','casting_bench','quarry','mine_head'],
        'community':['infirmary','school','archive','bath_house','water_cistern','sewer_outlet'],
        'logistics':['wagon','pack_saddle','warehouse','trade_office','harbour','sailboat']},
    'mechanisation':{
        'production':['machine_shop','lathe','drill_press','power_loom','sawmill','paper_mill','steam_engine','boiler','factory'],
        'networks':['pump_house','water_tower','rail_station','locomotive','freight_wagon','canal_lock','dry_dock'],
        'care':['hospital','fire_station','waste_sorting','housing_block']},
    'electrification':{
        'power':['generator','turbine','substation','power_line','battery_bank','solar_panel','wind_turbine'],
        'food_water':['refrigerated_store','water_treatment','sewage_treatment','silo','greenhouse','commercial_kitchen'],
        'transport':['truck','bus','car','airfield','aircraft','container_port','cargo_ship'],
        'services':['clinic','laboratory','library','communications_mast','electronics_bench','concrete_plant']},
    'information':{
        'research':['research_lab','console','server_rack','chip_fabricator','sensor_station','medical_scanner'],
        'networks':['distribution_centre','cold_chain_container','electric_vehicle','recycling_plant','wastewater_reuse'],
        'culture':['theatre','music_studio','sports_ground','civic_hall','botanical_garden']},
    'advanced':{
        'automation':['future_fabricator','robot_arm','autonomous_cart','cargo_pod','additive_printer','drone_dock','robot_repair_bay'],
        'life_support':['biotech_vat','hydroponic_rack','vertical_farm','bioreactor','clean_room','prosthetics_bench'],
        'energy':['fusion_chamber','thermal_storage','high_density_battery','grid_controller']},
    'space':{
        'habitation':['pressure_habitat','airlock','oxygen_recycler','radiation_shelter','orbital_greenhouse'],
        'industry':['regolith_processor','vacuum_furnace','orbital_foundry','fuel_processor','repair_drone'],
        'transport':['launch_pad','landing_pad','rover','orbital_dock','cargo_lander','interplanetary_vehicle']}}

def placed(asset,x,y,**extra): return {'asset_id':asset,'cell':[x,y],**extra}

def blueprints():
    common={'size_cells':[7,6],'entrance_cell':[3,5],'walkable_cells':[[x,y] for y in range(1,5) for x in range(1,6)]}
    definitions={
        'camp':('earth',None,None,['rest','prepare_food','gather','craft_hand_tools'],[
            placed('hearth',3,2),placed('bed',1,1),placed('bed',5,1),placed('knapping_block',1,3),
            placed('basket',2,1,resource='food',count=3),placed('backpack',5,3,resource='stone',count=2)]),
        'granary':('plank','wood','thatch',['store_grain','dry_food','dispatch_goods'],[
            placed('grain_bin',1,1,resource='grain',count=9),placed('grain_bin',2,1,resource='grain',count=5),
            placed('grain_bin',4,1,resource='grain',count=8),placed('grain_bin',5,1,resource='grain',count=2),
            placed('grain_sack',1,3),placed('grain_sack',2,3),placed('chest',5,3,resource='grain',count=6),
            placed('handcart',4,3,resource='grain',count=5)]),
        'pottery_workshop':('earth','wattle','thatch',['shape_clay','fire_ceramics'],[
            placed('kiln',1,1),placed('shelf',5,1),placed('potter_wheel',2,3),placed('stool',2,2),
            placed('crate',4,1,resource='clay',count=4),placed('pot',5,3),placed('water_barrel',4,3)]),
        'weaver':('reed_mat','wood','thatch',['spin_fiber','weave_cloth','store_textiles'],[
            placed('loom',1,1),placed('loom',3,1),placed('shelf',5,1),placed('stool',1,2),
            placed('basket',5,3,resource='cloth',count=4),placed('table',2,4),placed('chest',4,3,resource='cloth',count=5)]),
        'smithy':('flagstone','stone','tile',['forge_metal','repair_tools'],[
            placed('forge',1,1),placed('anvil',2,3),placed('workbench',5,1),placed('water_barrel',1,4),
            placed('handcart',5,4,resource='copper_ore',count=7),placed('chest',4,2,resource='iron_ingot',count=5)]),
        'home':('plank','wood','thatch',['rest','prepare_food','store_personal_goods'],[
            placed('bed',1,1),placed('bed',2,1),placed('hearth',5,1),placed('table',2,2),
            placed('stool',4,1),placed('stool',2,3),placed('chest',5,3,resource='cloth',count=3),placed('wash_basin',5,4)]),
        'bakery':('flagstone','fired_brick','tile',['mill_grain','bake_food'],[
            placed('oven',1,1),placed('millstone',3,1),placed('workbench',5,1),placed('table',2,3),
            placed('grain_bin',5,3,resource='grain',count=7),placed('basket',4,3,resource='food',count=4)]),
        'infirmary':('plank','mudbrick','tile',['care','rest','prepare_medicine'],[
            placed('bed',1,1),placed('bed',3,1),placed('bed',5,1),placed('wash_basin',1,4),
            placed('table',3,3),placed('chest',5,4,resource='medicine',count=6)]),
        'machine_shop':('concrete','fired_brick','metal',['machine_parts','maintain_equipment'],[
            placed('lathe',1,1),placed('workbench',3,1),placed('generator',5,1),placed('shelf',1,3),
            placed('pallet',4,3,resource='iron_ingot',count=6),placed('handcart',5,4,resource='components',count=7)]),
        'research_lab':('metal_deck','glass','metal',['research','analyse','culture_biological_samples'],[
            placed('console',1,1),placed('biotech_vat',3,1),placed('biotech_vat',5,1),placed('table',1,3),
            placed('wash_basin',3,3),placed('cargo_pod',5,4,resource='medicine',count=6)]),
        'future_fabricator':('metal_deck','metal','metal',['fabricate_parts','automate_handling'],[
            placed('robot_arm',1,1),placed('robot_arm',3,1),placed('console',5,1),placed('generator',1,4),
            placed('cargo_pod',4,3,resource='components',count=9),placed('pallet',5,4,resource='iron_ingot',count=6)])}
    definitions.update({
        'royal_hall':('flagstone','limestone','copper',['govern','receive_visitors','ceremony'],[placed('throne',3,1),placed('lectern',1,1),placed('ceremonial_screen',5,1),placed('chest',1,3,resource='cloth',count=5),placed('brazier',3,3),placed('chest',5,3,resource='gold_ore',count=4)]),
        'council_hall':('plank','stone','wood_shingle',['deliberate','record','administer'],[placed('table',3,1),placed('stool',1,1),placed('stool',5,1),placed('bookcase',1,3),placed('lectern',3,3),placed('chest',5,3,resource='cloth',count=3)]),
        'ritual_hall':('flagstone','granite','slate',['ritual','gather','remember'],[placed('altar',3,1),placed('brazier',1,1),placed('ceremonial_screen',5,1),placed('pot',1,3),placed('lectern',3,3),placed('chest',5,3,resource='herbs',count=4)]),
        'ceremonial_hall':('ceramic_tile','adobe','tile',['celebrate','perform','ceremony'],[placed('ceremonial_screen',1,1),placed('lectern',3,1),placed('ceremonial_screen',5,1),placed('table',1,3),placed('brazier',3,3),placed('chest',5,3,resource='cloth',count=4)]),
        'magic_workshop':('basalt','basalt','glass',['prepare_foci','study_magic','experiment'],[placed('brazier',1,1),placed('alchemy_table',3,1),placed('bookcase',5,1),placed('dye_vat',1,3),placed('chest',3,3,resource='quartz',count=5),placed('crate',5,3,resource='medicine',count=3)]),
        'fort_gatehouse':('flagstone','stone','slate',['control_access','defend','store_arms'],[placed('weapons_rack',1,1),placed('armour_stand',3,1),placed('table',5,1),placed('crate',1,3,resource='iron_ingot',count=4),placed('chest',3,3,resource='components',count=4),placed('water_barrel',5,3)]),
        'barracks':('plank','stone','tile',['rest','equip','train'],[placed('bed',1,1),placed('bed',3,1),placed('bed',5,1),placed('weapons_rack',1,3),placed('armour_stand',3,3),placed('chest',5,3,resource='cloth',count=6)]),
        'watch_post':('plank','palisade','wood_shingle',['observe','warn','defend'],[placed('stool',1,1),placed('telescope',3,1),placed('lectern',5,1),placed('weapons_rack',1,3),placed('chest',5,3,resource='grain',count=3)]),
        'construction_yard':('packed_clay',None,None,['shape_materials','lift','construct'],[placed('scaffold',1,1),placed('construction_crane',3,1),placed('workbench',5,1),placed('pallet',1,3,resource='log',count=6),placed('handcart',3,3,resource='stone',count=6),placed('crate',5,3,resource='clay',count=4)]),
        'carpentry_workshop':('plank','wood','bark',['saw','join_wood','repair'],[placed('sawbench',1,1),placed('chopping_block',3,1),placed('tool_rack',5,1),placed('pallet',1,3,resource='log',count=6),placed('workbench',3,3),placed('crate',5,3,resource='log',count=3)]),
        'fishing_hut':('bamboo','reed','reed',['fish','dry_fish','repair_nets'],[placed('fish_drying_rack',1,1),placed('workbench',3,1),placed('water_barrel',5,1),placed('basket',1,3,resource='fish',count=4),placed('chest',3,3,resource='dried_fish',count=5),placed('crate',5,3,resource='rope',count=3)]),
        'ranch':('earth','wood','thatch',['care_for_animals','store_feed'],[placed('feed_trough',1,1),placed('grain_bin',3,1,resource='grain',count=8),placed('crate',5,1,resource='grain',count=3),placed('bed',1,3),placed('water_barrel',3,3),placed('handcart',5,3,resource='grain',count=5)]),
        'market':('packed_path',None,None,['trade','display_goods','weigh'],[placed('market_stall',1,1),placed('market_stall',3,1),placed('market_stall',5,1),placed('basket',1,3,resource='apple',count=4),placed('basket',3,3,resource='carrot',count=4),placed('basket',5,3,resource='fish',count=4)]),
        'industrial_hall':('concrete','concrete','metal',['machine','power','assemble'],[placed('lathe',1,1),placed('press',3,1),placed('generator',5,1),placed('workbench',1,3),placed('battery_bank',3,3),placed('pallet',5,3,resource='steel',count=6)]),
        'shipyard':('packed_path',None,None,['build_watercraft','repair_hulls','load'],[placed('sawbench',1,1),placed('construction_crane',3,1),placed('raft',5,1),placed('scaffold',1,3),placed('chest',3,3,resource='rope',count=5),placed('handcart',5,3,resource='log',count=6)]),
        'transport_depot':('concrete','metal','metal',['dispatch','load','maintain_transport'],[placed('windlass',1,1),placed('workbench',3,1),placed('shelf',5,1),placed('cargo_pod',1,3,resource='components',count=8),placed('handcart',3,3,resource='iron_ingot',count=6),placed('cargo_pod',5,3,resource='food',count=6)]),
        'brew_house':('packed_clay','adobe','tile',['ferment','mill','store_drinks'],[placed('water_barrel',1,1),placed('brew_vat',3,1),placed('grain_bin',5,1,resource='barley',count=8),placed('stone_quern',1,3),placed('cooking_pot',3,3),placed('table',5,3)]),
        'medical_clinic':('concrete','sandstone','glass',['care','sterilise','prepare_medicine'],[placed('medical_table',1,1),placed('medical_table',3,1),placed('wash_basin',5,1),placed('cargo_pod',1,3,resource='medicine',count=6),placed('bookcase',3,3),placed('console',5,3)]),
        'library':('plank','limestone','tile',['teach','research','preserve_records'],[placed('bookcase',1,1),placed('lectern',3,1),placed('bookcase',5,1),placed('table',1,3),placed('stool',3,3),placed('chest',5,3,resource='cloth',count=4)]),
        'rural_home':('reed_mat','wattle','thatch',['rest','prepare_food','store_personal_goods'],[placed('bed',1,1),placed('hearth',3,1),placed('shelf',5,1),placed('basket',1,3,resource='roots',count=4),placed('table',3,3),placed('chest',5,3,resource='cloth',count=4)])})
    result=[]
    for name,(floor,wall,roof,roles,objects) in definitions.items():
        occupied={tuple(o['cell']) for o in objects}
        walkable=[cell for cell in common['walkable_cells'] if tuple(cell) not in occupied]+[[3,5]]
        actor=next(cell for cell in [[3,3],[3,2],[3,4],[4,3]] if tuple(cell) not in occupied)
        actor_outfit={'camp':'forager','smithy':'smith','pottery_workshop':'potter','weaver':'potter','bakery':'cook','infirmary':'healer','machine_shop':'engineer','research_lab':'researcher','future_fabricator':'spaceworker','royal_hall':'royal','council_hall':'court','ritual_hall':'ceremonial','ceremonial_hall':'ceremonial','magic_workshop':'mage','fort_gatehouse':'guard','barracks':'infantry','watch_post':'guard','construction_yard':'mason','carpentry_workshop':'woodworker','fishing_hut':'fisher','market':'merchant','industrial_hall':'mechanic','shipyard':'sailor','transport_depot':'engineer','brew_house':'cook','medical_clinic':'healer','library':'scholar'}.get(name,'farmer')
        result.append({'id':name,'asset_id':name,**common,'floor':floor,'wall':wall,'roof':roof,'capabilities':roles,
                       'objects':objects,'actors':[placed('person_detail',*actor)],'walkable_cells':walkable,
                       'actor_outfit':actor_outfit,
                       'roof_policy':'removable_per_room','cutaway_policy':'hide_south_wall','navigation_status':'illustrative_layout_not_navigation_baked',
                       'entrance_visual':None if not wall else 'door_'+('metal' if wall in {'metal','glass'} else 'wood')+'_open',
                       'production_building':False})
    return result

def build_data(out,entries,seed):
    def write(name,value): (out/name).write_text(json.dumps(value,indent=2)+'\n',encoding='utf-8')
    profiles={}
    for kind,anchors in CONTAINERS.items():
        profiles[kind]={'layers':{'back':kind+'_back','front':kind+'_front','lid':kind+'_lid'},
                        'slot_anchors_px64':anchors,'visible_slots':len(anchors),'slot_order':'back_to_front',
                        'content_scale':1,'allowed_resource_ids':list(RESOURCES),
                        'closed_policy':'hide_contents_draw_lid_before_front',
                        'transport_mode':'vehicle' if kind=='handcart' else ('equipped' if kind=='backpack' else 'portable' if kind in {'basket','chest','crate','cargo_pod'} else 'stationary'),
                        'logical_attachment':[8,15.75],
                        'equipped_layer':'behind_body' if kind=='backpack' else None,
                        'equipped_local_offset':[0,0] if kind=='backpack' else None,
                        'capacity_semantics':'display_slots_only_simulation_owns_mass_volume_and_stack_counts'}
    write('storage_profiles.json',{'version':1,'coordinate_density':64,'profiles':profiles,
          'inventory_contract':{'source':'authoritative_container_inventory','item_identity':'persistent_item_or_stack_id',
                                'render_order':['back','occupied_slots_sorted_by_y','lid_if_closed','front'],
                                'empty_slots':'draw_nothing','count_overflow':'aggregate_visually_without_discarding_inventory',
                                'visibility':'closed_lids_hide_items_but_do_not_remove_them',
                                'transfer':'simulation_moves_item_identity_then_renderer_reads_new_location',
                                'implemented_here':'asset_layers_and_review_compositor_only'}})
    plans=blueprints(); write('blueprints.json',{'version':1,'logical_cell_px':16,'blueprints':plans})
    vehicle_profiles={}
    for kind,(size,logical) in VEHICLES.items():
        w,h=size; slots=[]; foreground=[]
        if kind=='cargo_ship':
            slots=[{'position':[14,44],'container':'cargo_pod'},{'position':[70,42],'container':'cargo_pod'}]
            foreground=[[28,h-20],[w-10,h-32],[w-4,h-30],[w-21,h-10],[25,h-7],[6,h-42]]
        elif kind in {'truck','rover','hover_carrier','sled'}: slots=[{'position':[8,-10],'container':'crate'}]
        elif kind=='raft': slots=[{'position':[0,-6],'container':'basket'}]
        vehicle_profiles[kind]={'coordinate_density':64,'frame':size,'logical_frame':logical,'attachment_slots':slots,
                                'foreground_polygon':foreground,'draw_order':['vehicle_base','attached_containers','base_pixels_inside_foreground_polygon'],
                                'inventory_owner':'persistent_attached_container_not_vehicle_decoration','payload_in_base_png':False}
    write('vehicle_profiles.json',{'version':1,'profiles':vehicle_profiles,'integration_status':'review_compositor_and_attachment_contract_only'})
    item_ids=[f'grain_stack_{i:03d}' for i in range(1,7)]
    snapshots=[{'granary':item_ids,'cart':[],'backpack':[],'chest':[]},
               {'granary':item_ids[:4],'cart':item_ids[4:],'backpack':[],'chest':[]},
               {'granary':item_ids[:4],'cart':item_ids[4:5],'backpack':item_ids[5:],'chest':[]},
               {'granary':item_ids[:4],'cart':item_ids[4:5],'backpack':[],'chest':item_ids[5:]}]
    write('inventory_example.json',{'version':1,'review_example_only':True,
          'items':{i:{'resource_id':'grain','quantity':10} for i in item_ids},
          'containers':{'granary':'grain_bin','cart':'handcart','backpack':'backpack','chest':'chest'},
          'snapshots':snapshots,'transfer_labels':['stored_in_granary','load_cart','equip_backpack','deliver_to_chest'],
          'runtime_requirement':'simulation_owns_item_identity_quantity_capacity_save_load_and_transfer_atomicity'})
    from .civilisation_regions import build_regions, REQUIREMENTS
    build_regions(out,plans)
    ready=set(OBJECTS)|set(CONTAINERS)|{p['id'] for p in plans}|set(RESOURCES)|set(OUTDOOR)|set(VEHICLES)|{'wall_'+m for m in WALLS}|{'floor_'+m for m in FLOORS}|{'roof_'+m for m in ROOFS}
    catalogue=[]
    for stage,groups in ROADMAP.items():
        for system,names in groups.items():
            for name in names:
                catalogue.append({'id':name,'coverage_stage':stage,'system':system,
                                  'art_status':'sample_ready' if name in ready else 'planned',
                                  'simulation_status':'not_implemented_by_asset_pipeline'})
    known={e['id'] for e in catalogue}
    for system,names in {**REQUIREMENTS,'generated_nature':list(OUTDOOR),'transport_visuals':list(VEHICLES),'stored_resources':list(RESOURCES),'regional_walls':['wall_'+m for m in WALLS],'regional_floors':['floor_'+m for m in FLOORS],'regional_roofs':['roof_'+m for m in ROOFS],'specialist_rooms':[p['id'] for p in plans],'fixtures':OBJECTS}.items():
        for name in names:
            if name in known: continue
            known.add(name); catalogue.append({'id':name,'coverage_stage':'capability_based','system':system,'art_status':'sample_ready' if name in ready else 'planned','simulation_status':'not_implemented_by_asset_pipeline'})
    from .landscape import MATERIALS as LAND_MATERIALS, CLIFFS
    landscape_selectors={name:{'asset_id':name,'kind':'plain'} for name in LAND_MATERIALS}
    landscape_selectors.update({'rockface_'+name:{'asset_id':name,'kind':'cliff_corners'} for name in CLIFFS})
    for name in landscape_selectors:
        if name not in known:
            known.add(name); catalogue.append({'id':name,'coverage_stage':'capability_based','system':'landscape_visuals','simulation_status':'not_implemented_by_asset_pipeline'})
    for row in catalogue:
        if row['system'] in {'terrain_detail','landscape_visuals'} and row['id'] in landscape_selectors:
            row.update(art_status='separate_family_ready',source_family='landscape',visual_selector=landscape_selectors[row['id']])
    write('catalogue.json',{'version':1,'scope':'coverage_plan_not_complete_asset_library',
                          'progression':'capabilities_and_materials_emerge_independently_stages_are_review_groups',
                          'entries':catalogue})
    write('render_contract.json',{'version':1,'seed':seed,'logical_tile_px':16,'authored_density':64,'densities':list(DENSITIES),
          'projection':'top_down_oblique','pixel_filter':'nearest','master_palette':'palette/palette.json',
          'density_selection':{'tile_screen_px_up_to_24':16,'tile_screen_px_up_to_48':32,'tile_screen_px_above_48':64},
          'camera_policy':'snap_pixel_art_views_to_integer_magnification_of_selected_density',
          'planet_view':'aggregate_world_data_and_settlement_markers_not_individual_items',
          'detail_scaling':'render_extent_and_positions_multiply_by_density_over_16',
          'zoom_limit':'64_px_per_tile_is_authored_detail_integer_magnification_reveals_no_further_geometry',
          'roof_occlusion':'hide_roof_per_room_in_close_view_then_cut_away_foreground_walls',
          'ground_shadows':'engine_only','lighting':'top_left','world_item_position':'logical_coordinates_not_screen_coordinates',
          'object_pick_bounds':'logical_cell_with_optional_per_object_mask',
          'existing_ids':'unchanged_new_detail_ids_are_visual_resources_not_new_simulation_actions',
          'integration_status':'review_compositor_only_game_camera_navigation_storage_and_logistics_not_modified',
          'people_status':'one_static_scale_reference_plus_wardrobe_family_for_modular_outfits_shared_rig_ages_builds_directions_and_twenty_animations',
          'structure_condition_status':'detailed_construction_damage_ruin_and_burn_layers_pending',
          'generated_visual_ids':sorted({e['asset_id'] for e in entries})})
