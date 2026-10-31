extends SceneTree

func _initialize():
	var source = FileAccess.get_file_as_string("res://../evolysia-logo.svg")
	if source.is_empty():
		push_error("Cannot read the official logo")
		quit(1)
		return
	var logo = Image.new()
	var error = logo.load_svg_from_string(source, 1.0)
	if error != OK:
		push_error("Cannot rasterise the official logo: %s" % error)
		quit(1)
		return
	DirAccess.make_dir_recursive_absolute("res://branding")
	error = logo.save_png("res://branding/evolysia-logo.png")
	var notices = {
		"version": Engine.get_version_info(),
		"license": Engine.get_license_text(),
		"components": Engine.get_copyright_info(),
		"licenses": Engine.get_license_info(),
	}
	var file = FileAccess.open("res://branding/godot-notices.json", FileAccess.WRITE)
	file.store_string(JSON.stringify(notices, "  "))
	print("Official logo: %s x %s" % [logo.get_width(), logo.get_height()])
	quit(0 if error == OK else 1)
