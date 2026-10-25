extends SceneTree

func _initialize():
	call_deferred("run")

func run():
	var args = OS.get_cmdline_user_args()
	ProjectSettings.load_resource_pack(args[0])
	DirAccess.make_dir_recursive_absolute(args[2])
	var viewport = SubViewport.new()
	viewport.size = Vector2i(768,768)
	viewport.transparent_bg = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var bodies = {}
	for stance in ["Normal", "Calm", "Wrath", "Divinity"]:
		var body = ClassDB.instantiate("SpineSprite")
		body.position = Vector2(384,650)
		body.scale = Vector2.ONE * 1.45
		var suffix = "" if stance == "Normal" else "_" + stance.to_lower()
		body.set_skeleton_data_res(load("res://YUWatcherForms/animations/characters/watcher/watcher_skel_data" + suffix + ".tres"))
		viewport.add_child(body)
		body.visible = false
		body.get_animation_state().set_animation("Idle", true, 0)
		# Optional installed pose keeps the isolated preview consistent with this project's divine arms.
		if stance == "Divinity" and args.size() >= 5:
			var pose_source = GDScript.new()
			pose_source.source_code = FileAccess.get_file_as_string(args[3])
			if pose_source.reload() != OK:
				quit(1)
				return
			var pose = Node2D.new()
			pose.set_script(pose_source)
			pose.set_meta("texture_path", args[4])
			body.add_child(pose)
		bodies[stance] = body
	var source = GDScript.new()
	source.source_code = FileAccess.get_file_as_string(args[1])
	if source.reload() != OK:
		quit(1)
		return
	var fx = Node2D.new()
	fx.set_script(source)
	viewport.add_child(fx)
	var routes = [["Normal","Calm"],["Calm","Wrath"],["Wrath","Divinity"],["Divinity","Normal"]]
	for route in routes:
		for body in bodies.values():
			body.visible = false
		var previous = bodies[route[0]]
		var current = bodies[route[1]]
		previous.visible = true
		previous.process_mode = Node.PROCESS_MODE_DISABLED
		current.process_mode = Node.PROCESS_MODE_INHERIT
		fx.start(previous,current,route[0],route[1])
		fx.set_process(false)
		for i in 17:
			fx._process(fx.duration / 16.0)
			await RenderingServer.frame_post_draw
			viewport.get_texture().get_image().save_png(args[2].path_join("%s-%s-%02d.png" % [route[0],route[1],i]))
		fx.finish()
	print("STANCE_PREVIEW_SAVED")
	quit()
