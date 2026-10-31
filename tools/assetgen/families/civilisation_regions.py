
import json

REQUIREMENTS={
    'politics_royalty':['council_hall','royal_hall','court_house','assembly_house','record_office','diplomatic_hall','treasury','palace','audience_chamber','administration_office','election_hall','public_square'],
    'religion_ceremony_magic':['ritual_hall','ceremonial_hall','memorial_hall','pilgrimage_shelter','festival_ground','processional_way','funerary_workshop','magic_workshop','arcane_observatory','focus_workshop','alchemy_lab','ward_generator','mana_reservoir','ritual_circle'],
    'fortification':['fort_gatehouse','barracks','watch_post','palisade_gate','stone_gate','portcullis','drawbridge','battlement','arrow_slit','rampart','moat','watchtower','signal_tower','keep','citadel','bunker','blast_door','shield_wall','defence_turret'],
    'rural_food':['rural_home','ranch','fishing_hut','orchard_store','market','brew_house','dairy','cheesemaker','smokehouse','fishery','mushroom_house','seed_store','root_cellar','fruit_press','oil_press','butcher_workshop','poultry_coop','goose_pen','duck_pond','stable','fodder_store'],
    'artisan_construction':['carpentry_workshop','construction_yard','ropewalk','basketmaker','leatherworker','tailor','cobbler','glassblower','jeweller','sculptor','instrument_maker','tile_workshop','brickworks','lime_kiln','scaffold','crane','mortar_tub','wheelbarrow','sawhorse','mould_rack'],
    'industry_transport':['industrial_hall','shipyard','transport_depot','ore_washer','ore_crusher','rolling_mill','wire_mill','cement_works','chemical_works','textile_mill','vehicle_workshop','boat_shed','shipwright_workshop','carriage_house','freight_yard','fuel_depot','charging_station','hover_dock'],
    'terrain_detail':['chalk','limestone','granite','slate','shale','sandstone','obsidian','peat','loam','silt','red_clay','yellow_clay','black_soil','gravel','coarse_sand','fine_sand','dune','bog','salt_marsh','mudflat','coral_shore','mangrove_floor','leaf_litter','pine_needles','moss_floor','lichen_rock','frozen_mud','permafrost','rockface_granite','rockface_sandstone','rockface_basalt','rockface_ice'],
    'flora_states':['birch_bare','oak_bare','willow_bare','palm_fruiting','fruit_tree_blossom','fruit_tree_ripe','regional_understory','flowering_meadow','autumn_leaf_litter','tropical_vines','creeping_vines','marsh_ferns','desert_ephemerals'],
    'fauna_variants':['fox','hare','badger','otter','beaver','lynx','leopard','lion','tiger','elephant','rhinoceros','gazelle','antelope','yak','water_buffalo','reindeer','donkey','mule','turkey','pigeon','quail','pheasant','swan','heron','stork','gull','eagle','owl','bat','turtle','frog','salmon','trout','carp','tuna','cod','herring','shark','eel','whale','dolphin']}

STYLES={
    'cold_woodland':{'biomes':['tundra','boreal_forest'],'wall':'wood','roof':'bark','floor':'plank','outfits':['winter','hunter','woodworker'],'resources':['log','stone','hide','fur','fiber']},
    'temperate_woodland':{'biomes':['temperate_forest','temperate_rainforest','temperate_grassland'],'wall':'wattle','roof':'wood_shingle','floor':'plank','outfits':['farmer','potter','traveller'],'resources':['log','clay','stone','fiber','reed']},
    'arid_earth':{'biomes':['hot_desert','cold_desert','shrubland'],'wall':'rammed_earth','roof':'hide','floor':'packed_clay','outfits':['desert','merchant','potter'],'resources':['clay','stone','fiber','hide']},
    'humid_reed':{'biomes':['tropical_rainforest','tropical_seasonal_forest','savanna'],'wall':'bamboo','roof':'reed','floor':'bamboo','outfits':['forager','farmer','fisher'],'resources':['reed','log','fiber','clay']},
    'mountain_stone':{'biomes':['alpine'],'wall':'granite','roof':'slate','floor':'granite','outfits':['winter','mason','hunter'],'resources':['stone','log','hide','fur']},
    'wetland_stilts':{'biomes':['wetland'],'wall':'reed','roof':'reed','floor':'bamboo','outfits':['fisher','forager','sailor'],'resources':['reed','log','fiber','clay']},
    'volcanic_stone':{'biomes':['volcanic'],'wall':'basalt','roof':'tile','floor':'basalt','outfits':['mason','smith','engineer'],'resources':['stone','clay','iron_ore','copper_ore']},
    'polar_shelter':{'biomes':['ice_sheet'],'wall':'snow','roof':'hide','floor':'ice','outfits':['winter','hunter'],'resources':['hide','fur','bone','stone']},
    'coastal_trade':{'biomes':['coast','lake','ocean','deep_ocean'],'wall':'sandstone','roof':'wood_shingle','floor':'plank','outfits':['sailor','fisher','merchant'],'resources':['log','stone','fiber','salt']}}

def build_regions(out,plans):
    variants=[]
    for plan in plans:
        if plan['wall'] is None: continue
        for style,values in STYLES.items():
            variants.append({'id':plan['id']+'__'+style,'base_blueprint':plan['id'],'regional_style':style,
                             'floor':values['floor'],'wall':values['wall'],'roof':values['roof'],
                             'layout_variant':'mirror_x' if len(variants)%2 else 'original',
                             'resource_policy':'choose_only_when_local_or_imported_inputs_and_capabilities_are_available'})
    recipes={
        'wattle':['wood','reed','fiber'],'wood':['log','wood'],'mudbrick':['clay','fiber'],'stone':['stone'],'fired_brick':['clay'],'palisade':['log'],
        'metal':['iron_ingot'],'glass':['glass'],'bamboo':['reed','wood','fiber'],'reed':['reed','fiber'],'hide':['hide','wood','fiber'],
        'rammed_earth':['clay','stone'],'adobe':['clay','fiber'],'sandstone':['stone'],'limestone':['stone'],'granite':['stone'],'basalt':['stone'],
        'ice':['water'],'snow':['water'],'concrete':['stone','clay'],'ceramic':['clay'],'composite':['fiber','advanced_alloy'],'salvage':['iron_ingot','copper_ingot']}
    data={'version':1,'styles':STYLES,'variants':variants,'wall_resource_categories':recipes,
          'selection':'resource_availability_climate_and_cultural_preference_not_ethnicity_or_fixed_era',
          'outfit_policy':'recommendations_only_every_character_piece_and_dye_remains_player_choosable',
          'recipes_status':'input_categories_only_not_quantitative_simulation_recipes',
          'process_requirements':{'fired_brick':['kiln'],'ceramic':['kiln'],'metal':['metalworking'],'glass':['glassworking'],'concrete':['binder_production'],'composite':['advanced_materials']},
          'glyphs':'reserved_overlay_anchors_no_real_world_political_or_religious_symbols',
          'construction_variants':'material_and_mirrored_layout_samples_full_damage_construction_and_multistorey_art_pending'}
    (out/'regional_styles.json').write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8')
