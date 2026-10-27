extends SceneTree

func _initialize(): call_deferred("render")

func render():
	var args := OS.get_cmdline_user_args()
	ProjectSettings.load_resource_pack(args[0])
	var code := FileAccess.get_file_as_string(args[2])
	for method in ["custom","finale_ghost","departure"]:
		code = code.replace("func "+method+"(","func yu_original_"+method+"(")
	var script := GDScript.new()
	script.source_code = code+"\n"+FileAccess.get_file_as_string(args[3])
	if script.reload() != OK: quit(1);return
	var profiles := ["thrust","punch","down-normal","native-attack","finale-normal"]
	for stage in ["classic","normal","hurt","critical"]:
		var viewport := SubViewport.new()
		viewport.size = Vector2i(1800,1800)
		viewport.transparent_bg = false
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		var background := ColorRect.new()
		background.size = Vector2(1800,1800)
		background.color = Color(.12,.14,.18)
		viewport.add_child(background)
		var data = load("res://YUSilentThreeStages/characters/silent/"+stage+"/rig.tres")
		var original = ClassDB.instantiate("SpineSprite")
		original.set_skeleton_data_res(data)
		root.add_child(original)
		await process_frame
		original.hide()
		var readers := []
		for column in profiles.size():
			for row in 3:
				var reader = script.new()
				var sprite = reader.bind(original,viewport)
				reader.clips = {"thrust":.5,"punch":.5,"down-normal":.65,"native-attack":.5,"finale-normal":1.4}
				var length: float = reader.clip_duration(profiles[column])
				var at: float = [.12,.32,.72][row]*length
				reader.custom(profiles[column],at)
				var factor := .40
				sprite.scale = Vector2.ONE*factor
				sprite.position = Vector2(column*360+170,row*600+530)
				var label := Label.new()
				label.text = "%s %.2fs" % [profiles[column],at]
				label.position = Vector2(column*360+20,row*600+15)
				label.add_theme_font_size_override("font_size",22)
				viewport.add_child(label)
				readers.append(reader)
		await process_frame
		await RenderingServer.frame_post_draw
		viewport.get_texture().get_image().save_png(args[1].path_join("silent-poses-"+stage+".png"))
		for reader in readers: reader.release()
		original.queue_free()
		viewport.queue_free()
		await process_frame
	quit(0)
