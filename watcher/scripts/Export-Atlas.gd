extends SceneTree

# Uses the installed engine's texture importer; no game scene or save is opened.
func _initialize():
	var args = OS.get_cmdline_user_args()
	if args.size() != 2 or not ProjectSettings.load_resource_pack(args[0]):
		quit(1)
		return
	var texture = load("res://YUWatcherForms/animations/characters/watcher/the_watcher_divinity.png") as Texture2D
	var image = texture.get_image()
	if image.is_compressed():
		image.decompress()
	var error = image.save_png(args[1])
	print("ATLAS_EXPORT=", error)
	quit(0 if error == OK else 1)
