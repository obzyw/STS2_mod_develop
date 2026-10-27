extends Node2D

# Original painted assets; native flame materials; armor uses the character's own skinned draw surfaces.
var body: Node2D
var frame := Rect2(-100, -280, 200, 280)
var flame := 0
var plating := 0
var alive := true
var held: Node
var held_kind := ""
var dragging := false
var clock := 0.0
var fire_cast := -10.0
var stone_cast := -10.0
var fire_alpha := 0.0
var stone_alpha := 0.0
var preview_alpha := 0.0
var plates: Array = []
var rig: Node2D
var bolt := false
var source: Node2D
var target: Node2D
var start_point := Vector2.ZERO
var end_point := Vector2.ZERO
var bolt_age := 0.0
var cast_count := 0
var retaliation_count := 0
var armor_anchor_count := 0
var active_bones := {}
var last_bone_scan := -1.0
var fragments: Array[Sprite2D] = []
var fire_nodes: Array[Node2D] = []
var coatings: Array = []
var trail_sprite: Sprite2D
var trail_material: ShaderMaterial
var fire_scene: PackedScene
var wall_frames: Array[Sprite2D] = []
var material_cache := {}
var coated_slots := {}
var coated_meshes := {}
var last_tick_usec := 0

func _ready():
	RenderingServer.frame_pre_draw.connect(_before_draw)

func sync_actor(actor_body: Node2D, bounds: Rect2, fire_amount: int, stone_amount: int, living: bool):
	body = actor_body
	if bounds.size.x > 10 and bounds.size.y > 10:
		frame = bounds
	if fire_amount > flame:
		fire_cast = clock # Also covers restored powers and powers given by other sources.
	if stone_amount > plating:
		stone_cast = clock
	flame = fire_amount
	plating = stone_amount
	alive = living
	if not alive:
		held = null
		held_kind = ""

func select_card(holder: Node, kind: String, is_drag: bool):
	held = holder
	held_kind = kind
	dragging = is_drag

func unselect_card(holder: Node, force: bool):
	if held == holder and (force or not dragging):
		held = null
		held_kind = ""
		dragging = false

func cast(kind: String):
	cast_count += 1
	held = null
	held_kind = ""
	dragging = false
	if kind == "fire":
		fire_cast = clock
	else:
		stone_cast = clock

func launch(from: Node2D, to: Node2D):
	bolt = true
	source = from
	target = to
	start_point = to_local(source.global_position)
	end_point = to_local(target.global_position)
	retaliation_count += 1
	trail_sprite = Sprite2D.new()
	var white := GradientTexture2D.new()
	white.width = 256
	white.height = 64
	white.gradient = Gradient.new()
	white.gradient.colors = PackedColorArray([Color.WHITE,Color.WHITE])
	trail_sprite.texture = white
	trail_material = ShaderMaterial.new()
	trail_material.shader = get_meta("trail_shader")
	trail_material.set_shader_parameter("noise_map",load("res://images/vfx/fire/basic_fire_noise.png"))
	trail_sprite.material = trail_material
	add_child(trail_sprite)

func _process(delta: float):
	# Presentation fades use wall time, so SpeedX / Engine.time_scale cannot
	# compress a visible transition into a single frame.
	var now := Time.get_ticks_usec()
	delta = minf(float(now-last_tick_usec)/1000000.0,.1) if last_tick_usec > 0 else 0.0
	last_tick_usec = now
	clock += delta
	if bolt:
		bolt_age += delta
		if is_instance_valid(source):
			start_point = to_local(source.global_position)
		if is_instance_valid(target):
			end_point = to_local(target.global_position)
		if bolt_age > 0.55:
			queue_free()
		update_bolt()
		return
	if not is_instance_valid(held) or not held.is_inside_tree():
		held = null
		held_kind = ""
	fire_alpha = move_toward(fire_alpha, 1.0 if flame > 0 else 0.0, delta * (4.0 if flame > 0 else 1.0/.65))
	stone_alpha = move_toward(stone_alpha, 1.0 if plating > 0 else 0.0, delta * 3.2)
	preview_alpha = move_toward(preview_alpha, 1.0 if held_kind != "" else 0.0, delta * 5.0)
	visible = alive and (fire_alpha > 0 or stone_alpha > 0 or held_kind != "" or clock - fire_cast < 0.8 or clock - stone_cast < 0.7)

func _before_draw():
	if bolt or not is_inside_tree():
		return
	# Action mods can hand the body over after this node's _process. Resolve and
	# coat at the render boundary, before the new body first reaches the screen.
	if visible:
		var current := find_rig(body)
		if current != rig:
			release_coat()
			rig = current
			refresh_active_bones()
			build_plates()
			build_fragments()
		elif clock - last_bone_scan > .06:
			refresh_active_bones()
		update_fragments()
		update_fire()
	if stone_alpha > 0 and is_instance_valid(rig):
		prepare_coat()
	update_coat()
	if stone_alpha <= 0 and not coatings.is_empty():
		release_coat()

func find_rig(fallback: Node2D) -> Node2D:
	# Skin/stance mods may park the base Spine and display a sibling instead.
	var display = get_meta("render_root",null)
	if is_instance_valid(display) and display != fallback:
		if display.has_method("get_skeleton") or display is Skeleton2D:
			return display
		var presented := find_visible_spine(display)
		if presented != null:
			return presented
	var found := find_visible_spine(get_parent())
	return found if found != null else fallback

func find_visible_spine(root: Node) -> Node2D:
	for child in root.get_children():
		if child == self:
			continue
		if child is CanvasItem and (not child.is_visible_in_tree() or child.modulate.a < .03):
			continue
		if child is Node2D and child.is_visible_in_tree() and child.modulate.a > 0.1:
			if child is Skeleton2D and child.get_bone_count() > 0:
				return child
			if child.has_method("get_skeleton"):
				var skeleton = child.get_skeleton()
				if skeleton != null and skeleton.has_method("get_bones") and skeleton.get_bones().size() >= 8 and has_visible_mesh(child):
					return child
		var nested := find_visible_spine(child)
		if nested != null:
			return nested
	return null

func has_visible_mesh(sprite: Node) -> bool:
	# Some presentation mods hide only the base SpineMesh2D children, leaving
	# the Spine root visible and nesting their replacement under that same root.
	for mesh in sprite.get_children():
		if mesh.get_class() == "SpineMesh2D" and mesh.is_visible_in_tree() and mesh.modulate.a*mesh.self_modulate.a > .01:
			return true
	return false

func bone_transform(bone) -> Transform2D:
	if bone is Bone2D:
		return rig.global_transform.affine_inverse() * bone.global_transform
	return Transform2D(Vector2(bone.get_a(), bone.get_c()), Vector2(bone.get_b(), bone.get_d()), Vector2(bone.get_world_x(), bone.get_world_y()))

func bone_key(bone) -> String:
	return str(bone.name) if bone is Bone2D else bone.get_data().get_bone_name()

func bone_length(bone) -> float:
	return bone.get_length() if bone is Bone2D else bone.get_data().get_length()

func refresh_active_bones():
	active_bones.clear()
	last_bone_scan = clock
	if rig is Skeleton2D:
		for i in rig.get_bone_count():
			var bone = rig.get_bone(i)
			if bone.is_visible_in_tree():
				active_bones[bone_key(bone)] = true
		return
	if not is_instance_valid(rig) or not rig.has_method("get_skeleton") or rig.get_skeleton() == null:
		return
	for slot in rig.get_skeleton().get_slots():
		if slot.get_attachment() == null or slot.get_color().a < .01:
			continue
		var bone = slot.get_bone()
		while bone != null:
			var key: String = bone_key(bone)
			if active_bones.has(key):
				break
			active_bones[key] = true
			bone = bone.get_parent()

func build_plates():
	plates.clear()
	armor_anchor_count = 0
	if not is_instance_valid(rig):
		return
	var bones: Array = []
	var attachment_roles := {}
	if rig is Skeleton2D:
		for i in rig.get_bone_count():
			bones.append(rig.get_bone(i))
	elif rig.has_method("get_skeleton"):
		var skeleton = rig.get_skeleton()
		if skeleton != null:
			bones = skeleton.get_bones()
			for slot in skeleton.get_slots():
				var attachment = slot.get_attachment()
				if attachment == null:
					continue
				var label: String = attachment.get_attachment_name().to_lower()
				var key: String = slot.get_bone().get_data().get_bone_name()
				if "torso" in label or label == "bod" or label == "body" or "chest" in label:
					attachment_roles[key] = "torso"
				elif ("leg" in label or "arm" in label) and not ("cloak" in label or "armor" in label or "flap" in label):
					attachment_roles[key] = "limb"
			# The installed Regent cosmetic uses numbered anatomy inside a throne/minion rig.
			# Identify the rig topology, not the player's class; never plate its throne or carriers.
			if skeleton.find_bone("throne_bone") != null and skeleton.find_bone("6") != null and skeleton.find_bone("minia14") != null:
				attachment_roles = {"4":"torso","5":"torso","6":"torso","24":"limb","19":"limb","48":"limb","13":"limb","71":"limb","72":"limb","75":"limb","76":"limb"}
	# Named anatomy gets actual limb-centred plates. This works across base characters and skins
	# retaining their rig, without assuming an Ironclad character or swapping anyone's appearance.
	for bone in bones:
		var bone_name: String = bone_key(bone).to_lower()
		var length: float = bone_length(bone)
		var key: String = bone_key(bone)
		var torso: bool = bone_name.begins_with("bod_") or bone_name.begins_with("body") or "torso" in bone_name or "chest" in bone_name or (bone is Bone2D and bone_name == "hips") or attachment_roles.get(key,"") == "torso"
		var limb: bool = (("arm" in bone_name or "leg" in bone_name) and ("upper" in bone_name or "lower" in bone_name)) or "forearm" in bone_name or "thigh" in bone_name or "shin" in bone_name or (bone is Bone2D and (bone_name == "arms" or bone_name.begins_with("sleeve"))) or attachment_roles.get(key,"") == "limb"
		if length < 10 or not (torso or limb) or "ik" in bone_name:
			continue
		var radius := Vector2(length * .30, length * (.42 if torso else .29))
		var torso_width := length * .42
		if torso:
			var axes: Transform2D = global_transform.affine_inverse() * rig.global_transform * bone_transform(bone)
			torso_width = clampf(frame.size.x * .14 / maxf(axes.y.length(),.01),length*.38,length*1.25)
		for t in [.23, .70]:
			if torso:
				for side in [-1.0, 1.0]:
					plates.append({"bone":bone,"offset":Vector2(length*t,side*torso_width*.45),"rest":Vector2.ZERO,"index":plates.size(),"radius":Vector2(radius.x,torso_width*.55),"anatomy":true})
			else:
				plates.append({"bone":bone,"offset":Vector2(length*t,0),"rest":Vector2.ZERO,"index":plates.size(),"radius":radius,"anatomy":true})
	if plates.size() >= 4:
		armor_anchor_count = plates.size()
		return
	plates.clear()
	# Head and weapons remain recognizable; chest, shoulders, arms and legs receive separate plates.
	var sites := [Vector2(.40,.37), Vector2(.58,.37), Vector2(.45,.48), Vector2(.60,.49),
		Vector2(.35,.51), Vector2(.69,.50), Vector2(.31,.62), Vector2(.73,.62),
		Vector2(.46,.59), Vector2(.59,.60), Vector2(.44,.70), Vector2(.61,.70),
		Vector2(.40,.82), Vector2(.64,.82), Vector2(.39,.92), Vector2(.65,.92)]
	for i in sites.size():
		var pos: Vector2 = frame.position + sites[i] * frame.size
		var nearest = null
		var best := INF
		var local_point: Vector2 = rig.to_local(to_global(pos))
		for bone in bones:
			if not active_bones.has(bone_key(bone)):
				continue
			var bt := bone_transform(bone)
			if absf(bt.determinant()) < 0.0001:
				continue
			# Midpoints favour the limb itself over its parent's joint; names are not character-specific.
			var length: float = bone_length(bone)
			if length < 8:
				continue
			var mid: Vector2 = bt * Vector2(length * 0.45, 0)
			var distance := mid.distance_squared_to(local_point)
			if distance < best:
				best = distance
				nearest = bone
		var offset := Vector2.ZERO
		if nearest != null:
			offset = bone_transform(nearest).affine_inverse() * local_point
			armor_anchor_count += 1
		plates.append({"bone": nearest, "offset": offset, "rest": pos, "index": i,"anatomy":false})

func plate_position(plate: Dictionary) -> Vector2:
	if plate.bone != null and is_instance_valid(rig):
		return to_local(rig.to_global(bone_transform(plate.bone) * plate.offset))
	return plate.rest

func build_fragments():
	for sprite in fragments:
		if is_instance_valid(sprite):
			sprite.queue_free()
	fragments.clear()
	var regions: Array = get_meta("regions")
	for i in plates.size():
		var sprite := Sprite2D.new()
		var texture := AtlasTexture.new()
		texture.atlas = get_meta("shards")
		texture.region = regions[(i * 7 + 3) % regions.size()]
		sprite.texture = texture
		sprite.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
		add_child(sprite)
		fragments.append(sprite)

func update_fragments():
	var center := frame.get_center()
	var age := clock-stone_cast
	var merge := smoothstep(0.0,.62,age)
	var in_flight := age >= 0 and age < .72
	var preview := held_kind == "stone"
	for i in fragments.size():
		var sprite: Sprite2D = fragments[i]
		var plate: Dictionary = plates[i]
		var permitted: bool = plate.bone == null or active_bones.has(bone_key(plate.bone))
		sprite.visible = permitted and (preview or in_flight)
		if not sprite.visible:
			continue
		var angle := i*2.39996 + clock*.42
		var orbit := center + Vector2(cos(angle)*frame.size.x*.72,sin(angle)*frame.size.y*.42)
		var attached := plate_position(plate)
		var weight := merge if in_flight else 0.0
		sprite.position = orbit.lerp(attached,weight)
		var perspective := .72 + .24*sin(angle)
		var size := clampf(frame.size.y * .13,22.0,48.0)*perspective
		sprite.scale = Vector2.ONE * size / maxf(sprite.texture.get_size().x,sprite.texture.get_size().y)
		sprite.rotation = sin(angle*.7)*.55 * (1-weight)
		sprite.z_index = 0 if sin(angle) >= 0 or weight > .55 else -1
		var light := .62 + .25 * (sin(angle)*.5+.5)
		sprite.modulate = Color(light,light,light,(1-smoothstep(.34,.70,age)) if in_flight else preview_alpha*.94)

func anatomy(label: String) -> bool:
	var value := label.to_lower()
	for exclude in ["head","face","hair","mask","eye","weapon","sword","dagger","staff","shadow","cape","cloak","belt","minion","mini","ribbon","skirt"]:
		if exclude in value:
			return false
	for include in ["body","bod","torso","chest","breast","bust","boob","arm","leg","hand","hip","foot","knee","shoulder"]:
		if include in value:
			return true
	return false

func unnamed_body_slot(slot) -> bool:
	# Numeric attachment rigs have no anatomical labels. Select their central body
	# geometry, excluding the head band, root overlays and unusually long weapons.
	var bone = slot.get_bone()
	var transform := global_transform.affine_inverse() * rig.global_transform * bone_transform(bone)
	var length := bone_length(bone)
	if length < 1 or transform.x.length()*length > frame.size.y*.5:
		return false
	var midpoint := transform * Vector2(length*.45,0)
	var normalized := (midpoint-frame.position)/frame.size
	return normalized.x > .16 and normalized.x < .84 and normalized.y > .30 and normalized.y < .94

func coat_material(base: Material = null) -> ShaderMaterial:
	if base != null and not base is ShaderMaterial:
		return null
	var cache_key := str(base.get_instance_id()) if base != null else "default"
	if base is ShaderMaterial and base.shader != null:
		cache_key += ":"+str(base.shader.get_instance_id())
	if material_cache.has(cache_key):
		return material_cache[cache_key].material
	var material := ShaderMaterial.new()
	var uniforms: Array = []
	if base is ShaderMaterial and base.shader != null:
		# Preserve the author's full fragment and alpha, including shaders relying
		# on Godot's implicit COLOR texture sample instead of assigning texture RGB.
		var code: String = base.shader.code
		var fragment := RegEx.new()
		fragment.compile("void\\s+fragment\\s*\\(\\s*\\)\\s*\\{")
		var entry := fragment.search(code)
		if entry == null:
			return null
		var closing := entry.get_end()
		var depth := 1
		while closing < code.length() and depth > 0:
			if code[closing] == "{": depth += 1
			if code[closing] == "}": depth -= 1
			closing += 1
		if depth != 0:
			return null
		var finish := "COLOR = edv_finish(COLOR,texture(TEXTURE,UV),UV,TEXTURE);"
		var fragment_body := code.substr(entry.get_end(),closing-entry.get_end()-1)
		fragment_body = fragment_body.replace("return;",finish+"return;")
		code = code.substr(0,entry.get_end())+fragment_body+finish+code.substr(closing-1)
		var insertion := RegEx.new()
		insertion.compile("void\\s+(vertex|fragment|light)\\s*\\(")
		var first := insertion.search(code)
		if first == null:
			return null
		code = code.insert(first.get_start(),coat_sampling_source())
		material.shader = Shader.new()
		material.shader.code = code
		for item in base.shader.get_shader_uniform_list():
			uniforms.append(item.name)
	else:
		material.shader = get_meta("coat_shader")
	material.set_shader_parameter("stone_surface",get_meta("surface"))
	material.set_shader_parameter("edv_strength",0.0)
	material_cache[cache_key] = {"material":material,"source":base,"uniforms":uniforms}
	return material

func coat_sampling_source() -> String:
	return """
uniform sampler2D stone_surface : source_color, repeat_enable, filter_linear_mipmap;
uniform float edv_strength = 0.0;
uniform float edv_activation = 0.0;
vec4 edv_finish(vec4 rendered, vec4 original, vec2 edv_uv, sampler2D edv_tex) {
    vec3 stone = texture(stone_surface, edv_uv * vec2(textureSize(edv_tex,0)) / 220.0).rgb;
    float volume = dot(original.rgb,vec3(0.25,0.55,0.20));
    stone *= mix(0.76,1.22,smoothstep(0.035,0.83,volume));
    stone *= mix(0.74,1.0,smoothstep(0.015,0.12,volume));
    vec3 tint = clamp(rendered.rgb / max(original.rgb,vec3(0.08)),vec3(0.0),vec3(1.5));
    return vec4(mix(rendered.rgb,stone*tint,edv_strength),rendered.a);
}
"""

func update_coat():
	var changed := false
	for binding in coatings:
		if not is_instance_valid(binding.node):
			continue
		var actual = binding.node.get(binding.property)
		var owned = binding.material
		if actual != binding.material:
			binding.previous = actual # Another mod acquired the surface while armor was active.
		var base = binding.previous
		if base == null and binding.property == "normal_material" and is_instance_valid(rig):
			base = rig.get("normal_material")
		var material := coat_material(base)
		binding.material = material
		if material != null:
			if actual != material:
				binding.node.set(binding.property,material)
				changed = true
		elif actual == owned:
			binding.node.set(binding.property,binding.previous)
			changed = true
	for entry in material_cache.values():
		if entry.source is ShaderMaterial:
			for uniform in entry.uniforms:
				entry.material.set_shader_parameter(uniform,entry.source.get_shader_parameter(uniform))
		entry.material.set_shader_parameter("edv_strength",stone_alpha * smoothstep(.10,.65,clock-stone_cast))
		entry.material.set_shader_parameter("edv_activation",1.0-smoothstep(.15,.8,clock-stone_cast))
	if changed:
		refresh_frozen_meshes()

func refresh_frozen_meshes():
	# Manually sampled presentation rigs disable native mesh processing. A material
	# binding change needs one submission; uniform animation needs no pose update.
	if is_instance_valid(rig) and rig.has_method("get_update_mode") and rig.get_update_mode() == 2:
		rig.update_skeleton(0.0)
		for child in rig.get_children():
			if child.get_class() == "SpineMesh2D":
				child.queue_redraw()

func prepare_coat():
	var added := false
	if rig is Skeleton2D:
		for mesh in rig.get_parent().get_children():
			if (mesh is Polygon2D or mesh is Sprite2D) and anatomy(str(mesh.name)):
				if coated_meshes.has(mesh.get_instance_id()):
					continue
				var material := coat_material(mesh.material)
				coatings.append({"node":mesh,"previous":mesh.material,"material":material,"property":"material","created":false})
				if material != null:
					mesh.material = material
				coated_meshes[mesh.get_instance_id()] = true
				added = true
	elif rig.has_method("get_skeleton"):
		var skeleton = rig.get_skeleton()
		if skeleton == null:
			return
		var numbered := skeleton.find_bone("throne_bone") != null and skeleton.find_bone("minia14") != null
		var main_bones := ["4","5","6","24","19","48","13","71","72","75","76"]
		var named_count := 0
		for slot in skeleton.get_slots():
			var attachment = slot.get_attachment()
			if attachment != null and anatomy(bone_key(slot.get_bone())+" "+attachment.get_attachment_name()):
				named_count += 1
		# A single named chest detail does not provide names for a numbered body.
		var positional := not numbered and named_count < 3
		var holders := {}
		for child in rig.get_children():
			if child.get_class() == "SpineSlotNode":
				holders[str(child.get("slot_name"))] = child
		for slot in skeleton.get_slots():
			var attachment = slot.get_attachment()
			var slot_name: String = slot.get_data().get_name()
			if coated_slots.has(slot_name) and is_instance_valid(coated_slots[slot_name]):
				continue
			var key := bone_key(slot.get_bone())
			var label: String = key+" "+slot_name+(" "+attachment.get_attachment_name() if attachment != null else "")
			# Bind named body slots even when their idle attachment is null. Cast,
			# block, attack and hurt timelines can enable these parked body parts.
			if not ((numbered and key in main_bones) or (not numbered and anatomy(label)) or (positional and attachment != null and unnamed_body_slot(slot))):
				continue
			var holder: Node = holders.get(slot_name)
			var created := holder == null
			if created:
				holder = ClassDB.instantiate("SpineSlotNode")
				holder.name = "ElementalStoneSlot"
				rig.add_child(holder)
				holder.set("slot_name",slot_name)
			var previous = holder.get("normal_material")
			var base = previous if previous != null else rig.get("normal_material")
			var material := coat_material(base)
			coatings.append({"node":holder,"previous":previous,"material":material,"property":"normal_material","created":created})
			if material != null:
				holder.set("normal_material",material)
			coated_slots[slot_name] = holder
			added = true
	if added:
		refresh_frozen_meshes()

func release_coat():
	for binding in coatings:
		if not is_instance_valid(binding.node):
			continue
		if binding.created:
			if binding.node.get(binding.property) == binding.material:
				binding.node.set(binding.property,binding.previous)
			binding.node.queue_free()
		elif binding.node.get(binding.property) == binding.material:
			binding.node.set(binding.property,binding.previous)
	coatings.clear()
	material_cache.clear()
	coated_slots.clear()
	coated_meshes.clear()
	refresh_frozen_meshes()

func make_fire() -> Node2D:
	if fire_scene == null:
		fire_scene = load("res://scenes/vfx/fires/vfx_stepped_fire.tscn")
	var fire: Node2D = fire_scene.instantiate()
	add_child(fire)
	fire.visible = false
	for child in fire.get_children():
		if child is GPUParticles2D:
			child.emitting = false
	return fire

func fire_show(fire: Node2D, amount: float):
	fire.visible = amount > .01
	fire.modulate.a = amount
	for child in fire.get_children():
		if child is GPUParticles2D:
			child.emitting = fire.visible

func update_fire():
	var fire_preview := held_kind == "fire"
	var age := clock-fire_cast
	var burst := 1.0-smoothstep(.16,.9,age)
	if wall_frames.is_empty() and (fire_alpha > .01 or age < .9):
		for i in 2:
			var wall := Sprite2D.new()
			wall.texture = get_meta("fire_wall")
			wall.hframes = 4
			wall.vframes = 4
			wall.z_index = 0 # Scene order puts battle VFX below pause/modal UI.
			wall.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
			add_child(wall)
			wall_frames.append(wall)
	var wall_phase := clock*16.0
	for i in wall_frames.size():
		var wall := wall_frames[i]
		wall.frame = (int(wall_phase)+i)%16
		var blend := fposmod(wall_phase,1.0)
		var opacity := smoothstep(0.0,1.0,fire_alpha)
		wall.modulate.a = maxf(opacity*.82,burst if flame > 0 else 0.0)*(blend if i == 1 else 1.0-blend)
		wall.visible = wall.modulate.a > .001
		var height := frame.size.y*(1.08+burst*.15)
		wall.scale = Vector2(height*.84,height)/(wall.texture.get_size()/4.0)
		wall.position = Vector2(frame.end.x+frame.size.x*.85,frame.end.y-height*.47)
	if fire_nodes.is_empty() and (fire_preview or fire_alpha > .01 or age < .9):
		for i in 9:
			fire_nodes.append(make_fire())
	if fire_nodes.is_empty():
		return
	var center := frame.get_center()
	for i in 9:
		var fire: Node2D = fire_nodes[i]
		if i < 3:
			# Fine fire at the feet supports the painted wall without an opaque white core.
			var amount := maxf(fire_alpha*.20,burst*.30)
			fire_show(fire,amount)
			fire.z_index = -1
			fire.position = Vector2(frame.end.x-8+(i-1)*22,frame.end.y+3)
			fire.rotation = -.12 + i*.08
			fire.scale = Vector2(.10,.12)*clampf(frame.size.y/290,.7,1.4)
		else:
			fire_show(fire,preview_alpha*.8 if fire_preview else 0.0)
			var angle := (i-3)*TAU/6+clock*.65
			var phase := fposmod(clock*.65+i*.173,1.0)
			var radius := lerpf(frame.size.x*.90,frame.size.x*.30,phase)
			fire.position = center+Vector2(cos(angle)*radius,sin(angle)*radius*.62)
			fire.rotation = (center-fire.position).angle()+PI/2
			fire.scale = Vector2(.09,.21)*clampf(frame.size.y/290,.7,1.4)
			fire.z_index = 0 if sin(angle)>0 else -1

func update_bolt():
	if trail_sprite == null:
		return
	var t := clampf(bolt_age/.34,0,1)
	var head := start_point.lerp(end_point,t)
	var tail := start_point.lerp(end_point,maxf(0,t-.38))
	var length := maxf(head.distance_to(tail),12.0)
	trail_sprite.position = (head+tail)*.5
	trail_sprite.rotation = (end_point-start_point).angle()
	trail_sprite.scale = Vector2(length/256.0,clampf(length*.18,20,48)/64.0)
	trail_material.set_shader_parameter("age",bolt_age)
	trail_material.set_shader_parameter("intensity",1.0-smoothstep(.35,.55,bolt_age))

func _exit_tree():
	if RenderingServer.frame_pre_draw.is_connected(_before_draw):
		RenderingServer.frame_pre_draw.disconnect(_before_draw)
	release_coat()

func audit() -> Dictionary:
	return {"flame":flame,"plating":plating,"held":held_kind,"fire_alpha":fire_alpha,
		"stone_alpha":stone_alpha,"plates":plates.size(),"anchors":armor_anchor_count,"casts":cast_count,
		"rig":str(rig.get_path()) if is_instance_valid(rig) else "", "bounds":str(frame), "coated_surfaces":coatings.size(), "source_hash":get_script().source_code.sha256_text()}

func coverage_audit() -> Dictionary:
	var submitted := 0
	var active := 0
	var missing: Array[String] = []
	if is_instance_valid(rig):
		var root: Node = rig.get_parent() if rig is Skeleton2D else rig
		for mesh in root.get_children():
			if mesh is CanvasItem and mesh.is_visible_in_tree() and mesh.modulate.a*mesh.self_modulate.a > .01:
				var material = mesh.get("material")
				if material is ShaderMaterial and material.shader != null and "edv_strength" in material.shader.code and float(material.get_shader_parameter("edv_strength")) > .8:
					submitted += 1
		if rig.has_method("get_skeleton") and rig.get_skeleton() != null:
			for slot in rig.get_skeleton().get_slots():
				var attachment = slot.get_attachment()
				if attachment == null or slot.get_color().a < .01:
					continue
				var slot_name: String = slot.get_data().get_name()
				if coated_slots.has(slot_name):
					active += 1
				elif anatomy(slot_name+" "+bone_key(slot.get_bone())+" "+attachment.get_attachment_name()):
					missing.append(slot_name)
	return {"submitted":submitted,"active_coated_slots":active,"unbound_body_slots":missing,"rig":str(rig.get_path()) if is_instance_valid(rig) else ""}

func rig_audit() -> Dictionary:
	var result := {}
	if rig is Skeleton2D:
		var bones := []
		for i in rig.get_bone_count():
			var bone = rig.get_bone(i)
			bones.append([bone_key(bone),bone_length(bone),str(bone_transform(bone))])
		result["bones"] = bones
	elif is_instance_valid(rig) and rig.has_method("get_skeleton"):
		var slots := []
		for slot in rig.get_skeleton().get_slots():
			var attachment = slot.get_attachment()
			if attachment == null:
				continue
			slots.append([slot.get_data().get_name(), attachment.get_attachment_name(), attachment.get_class(), slot.get_bone().get_data().get_bone_name()])
		result["slots"] = slots
		var bones := []
		for bone in rig.get_skeleton().get_bones():
			bones.append([bone.get_data().get_bone_name(),bone.get_data().get_length(),str(bone_transform(bone))])
		result["bones"] = bones
	return result
