extends SceneTree

func _initialize():
	call_deferred("run")

func run():
	var args = OS.get_cmdline_user_args()
	ProjectSettings.load_resource_pack(args[0])
	var viewport = SubViewport.new()
	viewport.size = Vector2i(1024,1024)
	viewport.transparent_bg = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var body = ClassDB.instantiate("SpineSprite")
	body.position = Vector2(512,870)
	body.scale = Vector2.ONE * 1.6
	body.set_skeleton_data_res(load("res://YUWatcherForms/animations/characters/watcher/watcher_skel_data_divinity.tres"))
	viewport.add_child(body)
	await process_frame
	body.get_animation_state().set_animation("Idle", true, 0)
	var pose = Node2D.new()
	var source = GDScript.new()
	source.source_code = FileAccess.get_file_as_string(args[1])
	if source.reload() != OK:
		quit(1)
		return
	pose.set_script(source)
	pose.set_meta("texture_path", args[2])
	body.add_child(pose)
	await create_timer(0.5).timeout
	await RenderingServer.frame_post_draw
	viewport.get_texture().get_image().save_png(args[3])
	print("POSE_PREVIEW_SAVED")
	quit()
