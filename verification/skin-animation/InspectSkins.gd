extends SceneTree

func _initialize():
	call_deferred("inspect")

func inspect():
	var args := OS.get_cmdline_user_args()
	var workshop := args[0]
	var output := args[1]
	var packages := {"silent":["3815873270","YUSilentThreeStages"],"defect":["3819703745","YUDefectThreeStages"],"necrobinder":["3819703784","YUNecrobinderThreeStages"],"ironclad":["3815865149","YUIroncladThreeStages"]}
	var report := {}
	var render := args.size() > 2 and args[2] == "render"
	for character in packages:
		var item: Array = packages[character]
		if not ProjectSettings.load_resource_pack(workshop.path_join(item[0]).path_join(item[1]+".pck")):
			push_error("Could not load "+item[1]); quit(1); return
		var variants := {}
		for stage in ["classic","normal","hurt","critical"]:
			var path := "res://%s/characters/%s/%s/rig.tres" % [item[1],character,stage]
			if stage == "classic" and character == "defect": path = "res://%s/characters/%s/classic/normal/rig.tres" % [item[1],character]
			var data = load(path)
			if data == null:
				push_error("Missing rig "+path);quit(1);return
			var sprite = ClassDB.instantiate("SpineSprite")
			sprite.set_skeleton_data_res(data)
			root.add_child(sprite)
			await process_frame
			var skeleton = sprite.get_skeleton()
			if render and stage == "normal":
				var viewport := SubViewport.new()
				viewport.size = Vector2i(900,1000)
				viewport.transparent_bg = false
				viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
				root.add_child(viewport)
				var background := ColorRect.new()
				background.size = Vector2(900,1000)
				background.color = Color(.12,.14,.18)
				viewport.add_child(background)
				sprite.reparent(viewport)
				var bounds: Rect2 = skeleton.get_bounds()
				var factor := 830.0/maxf(bounds.size.x,bounds.size.y)
				sprite.scale = Vector2.ONE*factor
				sprite.position = Vector2(450,500)-bounds.get_center()*factor
				await process_frame
				await RenderingServer.frame_post_draw
				viewport.get_texture().get_image().save_png(output.get_base_dir().path_join(character+"-skin-normal.png"))
				for bone in skeleton.get_bones():
					var point: Vector2 = Vector2(bone.get_world_x(),bone.get_world_y())*factor+sprite.position
					var label := Label.new()
					label.text = bone.get_data().get_bone_name()
					label.position = point
					label.add_theme_font_size_override("font_size",16)
					label.modulate = Color(1,.8,.2)
					viewport.add_child(label)
				await process_frame
				await RenderingServer.frame_post_draw
				viewport.get_texture().get_image().save_png(output.get_base_dir().path_join(character+"-skin-bones.png"))
			var bones := []
			for bone in skeleton.get_bones():
				var b = bone.get_data()
				bones.append({"name":b.get_bone_name(),"parent":b.get_parent().get_index() if b.get_parent() else -1,"x":b.get_x(),"y":b.get_y(),"rotation":b.get_rotation(),"length":b.get_length(),"worldX":bone.get_world_x(),"worldY":bone.get_world_y()})
			var slots := []
			for slot in skeleton.get_slots():
				var s = slot.get_data()
				slots.append({"name":s.get_name(),"bone":s.get_bone_data().get_index(),"attachment":s.get_attachment_name()})
			var animations := []
			for animation in skeleton.get_data().get_animations():
				animations.append({"name":animation.get_name(),"duration":animation.get_duration()})
			var constraints := []
			for ik in skeleton.get_ik_constraints():
				var members := []
				for b in ik.get_bones(): members.append(b.get_data().get_bone_name())
				constraints.append({"name":ik.get_data().get_name(),"bones":members,"target":ik.get_target().get_data().get_bone_name()})
			var methods := []
			for method in skeleton.get_method_list():
				if "draw_order" in method.name: methods.append(method.name)
			variants[stage] = {"resource":path,"hash":skeleton.get_data().get_hash(),"bones":bones,"slots":slots,"animations":animations,"ik":constraints,"drawOrderMethods":methods}
			sprite.queue_free()
			await process_frame
		report[character] = variants
	var file := FileAccess.open(output,FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"  "))
	file.close()
	quit(0)
