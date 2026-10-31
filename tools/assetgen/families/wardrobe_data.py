
import json
from .wardrobe import PARTS, ORDER, HAIR, TOPS

def build_data(out,seed):
    def write(name,data): (out/name).write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8')
    jobs=[
        ('forager','hide_wrap','wrap_skirt','sandals',None,'woven_hat',None,'basket'),
        ('farmer','farmer_smock','work_trousers','work_boots',None,'wide_brim_hat','tool_belt','hoe'),
        ('fisher','fisher_coat','shorts','water_shoes','rain_cape','cloth_cap',None,'fishing_rod'),
        ('hunter','hunter_vest','leggings','leather_boots','short_cape','hood','belt_pouch','bow'),
        ('woodworker','work_shirt','work_trousers','work_boots',None,'cloth_cap','tool_belt','axe'),
        ('mason','linen_shirt','trousers','work_boots',None,'work_helmet','tool_belt','hammer'),
        ('potter','artisan_apron','loose_trousers','sandals',None,'headwrap',None,'chisel'),
        ('smith','smith_apron','work_trousers','work_boots',None,'leather_helmet','goggles','tongs'),
        ('cook','cook_apron','trousers','soft_shoes',None,'cook_hat',None,'knife'),
        ('healer','healer_coat','loose_trousers','soft_shoes',None,'veil','necklace','medicine_bag'),
        ('scholar','scholar_robe','trousers','soft_shoes',None,'scholar_cap','spectacles','book'),
        ('traveller','traveller_coat','leggings','leather_boots','traveller_mantle','hood','backpack_straps','staff'),
        ('merchant','merchant_coat','formal_trousers','ceremonial_shoes',None,'woven_hat','belt_pouch','measuring_rod'),
        ('guard','guard_tabard','padded_trousers','metal_boots',None,'guard_helmet',None,'spear'),
        ('ceremonial','ceremonial_robe','long_skirt','ceremonial_shoes','ceremonial_mantle','ceremonial_headdress','ceremonial_sash','staff'),
        ('royal','royal_robe','formal_trousers','ceremonial_shoes','royal_mantle','royal_crown','royal_chain','orb_focus'),
        ('court','court_coat','formal_trousers','ceremonial_shoes',None,'royal_circlet','necklace','construction_plan'),
        ('winter','winter_parka','fur_leggings','fur_boots','fur_mantle','fur_hat','scarf','spear'),
        ('desert','desert_robe','loose_trousers','sandals','desert_shawl','headwrap','face_wrap','staff'),
        ('sailor','sailor_shirt','shorts','water_shoes',None,'cloth_cap','scarf','net'),
        ('engineer','engineer_jacket','work_trousers','work_boots',None,'work_helmet','goggles','wrench'),
        ('mechanic','mechanic_coverall','work_trousers','work_boots',None,'cloth_cap','tool_belt','wrench'),
        ('researcher','lab_coat','formal_trousers','soft_shoes',None,'cloth_cap','spectacles','book'),
        ('pilot','flight_suit','environment_trousers','environment_boots',None,'visor_helmet','respirator','measuring_rod'),
        ('spaceworker','environment_suit','environment_trousers','environment_boots','insulated_overcoat','environment_helmet','respirator','energy_tool'),
        ('mage','mage_robe','long_skirt','cloth_wraps','hooded_cloak','mage_hat','beads','wand'),
        ('ranger','long_tunic','leggings','leather_boots','short_cape','hood','belt_pouch','crossbow'),
        ('infantry','guard_tabard','padded_trousers','metal_boots',None,'iron_helmet','tool_belt','sword'),
        ('firekeeper','woven_tunic','wrap_skirt','sandals','short_cape','feather_band','beads','torch'),
        ('ceremony_guard','ceremonial_robe','padded_trousers','ceremonial_shoes','ceremonial_mantle','copper_helmet','ceremonial_sash','shield'),
        ('industrial_guard','work_shirt','work_trousers','metal_boots','officer_coat','visor_helmet','respirator','rifle'),
        ('future_guard','environment_suit','environment_trousers','environment_boots',None,'environment_helmet','goggles','energy_blade')]
    outfits=[]
    for i,(name,top,legs,feet,outer,head,access,item) in enumerate(jobs):
        pieces={'body':'base','hair_back':None,'hair_front':'short','legwear':legs,'footwear':feet,'top':top,'outerwear':outer,'armour':None,'body_paint':None,'headwear':head,'accessory':access,'held_item':item}
        if name in {'guard','infantry','ceremony_guard','industrial_guard','future_guard'}: pieces['armour']={'guard':'padded','infantry':'iron_plate','ceremony_guard':'copper_scale','industrial_guard':'composite','future_guard':'powered'}[name]
        dyes={layer:{'main':(i+li*3)%13,'trim':(i*3+li+5)%13} for li,layer in enumerate(ORDER) if layer not in {'body','hair_front','hair_back','body_paint'}}
        outfits.append({'id':name,'pieces':pieces,'dyes':dyes,'skin':3,'hair':2,'culture':i%12,'editable':True})
    write('customisation.json',{'version':1,'seed':seed,'layer_order':ORDER,'parts':PARTS,'outfits':outfits,
          'hair_styles':{'shaved':{'front':None,'back':None},**{name:{'front':name,'back':name if name in {'long','braided','mane','locks','twin_braids','high_tail','bob','coils','tied_back','topknot'} else None} for name in HAIR}},
          'selection_policy':'every_piece_can_be_overridden_or_removed_independently_outfits_are_presets_not_class_locks',
          'colour_policy':'each_piece_has_its_own_main_and_trim_cloth_LUT_rows_skin_hair_and_culture_are_independent',
          'dye_groups':{'main':'cloth_a','trim':'cloth_b','culture':'culture'},
          'dye_lut_rows':13,'skin_rows':8,'hair_rows':8,'culture_rows':12,
          'body_paint_clip':'visible_skin_after_all_clothing_armour_and_outerwear',
          'major_layer_order_compatibility':'hair_back_body_clothing_subparts_body_paint_hair_front_accessories_held_item',
          'mirror_side_left':'mirror_pixels_pivots_and_hand_anchors_together',
          'animation_rig':'people_shared_logical_pose_coordinates_same_action_ids_all_ages_builds_and_supported_directions',
          'magic':'fictional_orbs_gems_staves_and_energy_forms_no_real_symbols_or_alphabetic_runes',
          'runtime_status':'art_and_reference_compositor_only_no_game_inventory_or_equipment_implementation'})
