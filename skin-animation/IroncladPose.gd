extends RefCounted
# Retarget defensive gestures on the subscribed skin, preserving foot IK.
var sprite
var skeleton
var data
var source

func bind(body, parent):
    source = body
    data = body.get_skeleton_data_res()
    sprite = ClassDB.instantiate("SpineSprite")
    sprite.name = "YUIroncladGuardDisplay"
    sprite.set_skeleton_data_res(data)
    sprite.set_update_mode(2)
    parent.add_child(sprite)
    skeleton = sprite.get_skeleton()
    if body.get_skeleton().get_skin(): skeleton.set_skin(body.get_skeleton().get_skin())
    skeleton.set_scale_x(body.get_skeleton().get_scale_x())
    skeleton.set_scale_y(body.get_skeleton().get_scale_y())
    return sprite

func turn(name, degrees):
    var bone = skeleton.find_bone(name)
    if bone: bone.set_rotation(bone.get_rotation()+degrees)

func pose(kind, seconds, breath):
    skeleton.set_to_setup_pose()
    var idle = data.find_animation("idle_loop")
    if idle: idle.apply(skeleton,-1.0,fposmod(seconds,idle.get_duration()),true,[],1.0,2,0)
    for ik in skeleton.get_ik_constraints():
        var names = []
        for bone in ik.get_bones(): names.append(bone.get_data().get_bone_name())
        if "23" in names or "arm" in names: ik.set_mix(0.0)
    var raised = smoothstep(0.0,.45,seconds)
    var recoil = 0.0
    if kind == "normal-impact" or kind == "guard-break":
        raised = 1.0
        recoil = sin(clampf(seconds/.42,0.0,1.0)*PI)
    if kind == "impervious": raised = smoothstep(0.0,.55,seconds)
    turn("5",-7.0*raised-5.0*recoil+breath*1.2)
    turn("1",5.0*raised)
    turn("23",-32.0*raised-8.0*recoil)
    turn("30",-50.0*raised+12.0*recoil)
    turn("34",30.0*raised)
    turn("arm",12.0*raised)
    turn("28",-10.0*raised)
    if kind == "impervious":
        turn("23",-12.0*raised)
        turn("arm",-12.0*raised)
    for slot in skeleton.get_slots():
        var name = slot.get_data().get_name()
        if name == "shadow" or name.begins_with("slash") or name.begins_with("zaps"):
            slot.set_attachment(null)
    var color = source.get_skeleton().get_color()
    skeleton.set_color(Color(1.0,1.0,1.0,color.a))
    for bone in skeleton.get_bones(): bone.update_world_transform()
    sprite.update_skeleton(0.0)
    for child in sprite.get_children():
        if child.get_class() == "SpineMesh2D": child.queue_redraw()

func align_shield(native_sprite):
    var native_skeleton = native_sprite.get_skeleton()
    for slot in native_skeleton.get_slots():
        var name = slot.get_data().get_name()
        if not (name.begins_with("guard_") or name.begins_with("sweep_")):
            slot.set_attachment(null)
    var hand = skeleton.find_bone("34")
    var anchor = native_skeleton.find_slot("top hand").get_bone()
    var target = Vector2(hand.get_world_x(),hand.get_world_y())
    var origin = Vector2(anchor.get_world_x(),anchor.get_world_y())
    native_sprite.scale = Vector2.ONE*.70
    native_sprite.position = target-origin*.70
    native_sprite.update_skeleton(0.0)

func gold(material):
    sprite.set_normal_material(material)
    for slot in skeleton.get_slots():
        # Gold conversion flags: category 1, unattenuated original color.
        var color = slot.get_color()
        slot.set_color(Color(1.0/7.0,1.0,1.0,color.a))
    sprite.update_skeleton(0.0)

func release():
    if is_instance_valid(sprite): sprite.queue_free()
    sprite = null
    source = null
