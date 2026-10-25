extends Node2D

# Replace only the arms/sleeves. The original face, torso, legs and all FX stay on their Spine rig.
var body
var arms: Array[Polygon2D] = []
var saved_slots = []
var saved_staff = []
var elapsed = 0.0
var active = false
var masked_names = ["ba", "28", "16", "20", "22", "23", "5"]

func _ready():
	body = get_parent()
	var path = get_meta("texture_path")
	var image = Image.load_from_file(path)
	var texture = ImageTexture.create_from_image(image)
	make_arm(texture, [[455,260],[442,340],[453,397],[445,450],[392,500],[365,600],[320,780],[270,940],[245,1000],[140,1090],[0,1150],[20,1050],[150,860],[220,720],[255,600],[285,500],[125,480],[130,425],[180,410],[258,445],[282,390],[290,335],[360,355],[421,283]], Vector2(455,270), Vector2(-12,-272))
	make_arm(texture, [[735,375],[795,402],[820,470],[918,447],[1010,470],[1020,525],[914,556],[893,650],[935,850],[1000,1060],[1024,1240],[908,1150],[842,1000],[793,860],[761,700],[718,534],[704,455]], Vector2(735,390), Vector2(25,-267))
	body.before_animation_state_apply.connect(restore_pose)
	body.before_world_transforms_change.connect(apply_pose)
	visible = false

func make_arm(texture, points, anchor, target):
	var poly = Polygon2D.new()
	var vertices = PackedVector2Array()
	for point in points:
		vertices.append(Vector2(point[0], point[1]))
	poly.polygon = vertices
	poly.uv = vertices
	poly.texture = texture
	poly.scale = Vector2.ONE * 0.24
	poly.position = target - anchor * 0.24
	poly.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	poly.set_meta("rest_position", poly.position)
	add_child(poly)
	arms.append(poly)

func _process(delta):
	elapsed += delta
	# Small cloth follow-through; no position changes to the character, halo or other effect nodes.
	for i in arms.size():
		arms[i].position = arms[i].get_meta("rest_position") + Vector2(0, sin(elapsed * 1.7 + i * 0.25) * 0.6)

func restore_pose(_sprite):
	for item in saved_slots:
		item[0].set_color(item[1])
	saved_slots.clear()
	if not saved_staff.is_empty():
		var bone = saved_staff[0]
		bone.set_x(saved_staff[1])
		bone.set_y(saved_staff[2])
		bone.set_rotation(saved_staff[3])
		saved_staff.clear()

func apply_pose(_sprite):
	var resource_path = body.get_skeleton_data_res().resource_path
	var supported = resource_path.begins_with("res://YUWatcherForms/") and "_divinity" in resource_path
	var track = body.get_animation_state().get_current(0)
	active = supported and track != null and track.get_animation().get_name() == "Idle"
	if active and has_meta("action_check"):
		active = not get_meta("action_check").call()
	visible = active
	if not active:
		return
	var skeleton = body.get_skeleton()
	for slot in skeleton.get_slots():
		var attachment = slot.get_attachment()
		if attachment != null and attachment.get_attachment_name() in masked_names:
			var color = slot.get_color()
			saved_slots.append([slot, color])
			color.a = 0
			slot.set_color(color)
	var staff = skeleton.find_bone("26")
	saved_staff = [staff, staff.get_x(), staff.get_y(), staff.get_rotation()]
	var parent = staff.get_parent()
	var target = Vector2(122, -235) - Vector2(parent.get_world_x(), parent.get_world_y())
	var determinant = parent.get_a() * parent.get_d() - parent.get_b() * parent.get_c()
	staff.set_x((target.x * parent.get_d() - target.y * parent.get_b()) / determinant)
	staff.set_y((target.y * parent.get_a() - target.x * parent.get_c()) / determinant)
	var sign = 1.0 if staff.get_a() * staff.get_d() - staff.get_b() * staff.get_c() >= 0 else -1.0
	staff.set_rotation(staff.get_rotation() + 90 * sign)
	skeleton.update_world_transform(3)

func _exit_tree():
	if is_instance_valid(body):
		restore_pose(body)
		if body.before_animation_state_apply.is_connected(restore_pose):
			body.before_animation_state_apply.disconnect(restore_pose)
		if body.before_world_transforms_change.is_connected(apply_pose):
			body.before_world_transforms_change.disconnect(apply_pose)

func audit():
	var halo_alpha = -1.0
	for slot in body.get_skeleton().get_slots():
		var attachment = slot.get_attachment()
		if attachment != null and attachment.get_attachment_name() == "fa":
			halo_alpha = slot.get_color().a
	return {"active": active, "halo_alpha": halo_alpha, "arm_layers": arms.size(), "masked_slots": saved_slots.size()}
