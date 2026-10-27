extends SceneTree

# Parse using the game's own native engine, before launching the no-save combat fixture.
func _initialize():
	var args := OS.get_cmdline_user_args()
	if args.size() != 1:
		quit(2)
		return
	var script := GDScript.new()
	script.source_code = FileAccess.get_file_as_string(args[0])
	var error := script.reload()
	print("Elemental script parse: ", error)
	quit(0 if error == OK else 1)
