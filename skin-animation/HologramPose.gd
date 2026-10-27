extends RefCounted
# Snapshot the displayed skin's pose; never sample an original character atlas.
var source
var sprite
var skeleton
var data
var bones
var slots

func bind(body, viewport):
    source = body
    data = body.get_skeleton_data_res()
    sprite = ClassDB.instantiate("SpineSprite")
    sprite.name = "YUHologramSkinCapture"
    sprite.set_skeleton_data_res(data)
    sprite.set_update_mode(2)
    viewport.add_child(sprite)
    skeleton = sprite.get_skeleton()
    if body.get_skeleton().get_skin(): skeleton.set_skin(body.get_skeleton().get_skin())
    skeleton.set_scale_x(body.get_skeleton().get_scale_x())
    skeleton.set_scale_y(body.get_skeleton().get_scale_y())
    bones = skeleton.get_bones()
    slots = skeleton.get_slots()
    return sprite

func capture():
    var source_skeleton = source.get_skeleton()
    var values := PackedFloat32Array()
    for bone in source_skeleton.get_bones():
        values.append_array(PackedFloat32Array([bone.get_x(),bone.get_y(),bone.get_rotation(),bone.get_scale_x(),bone.get_scale_y(),bone.get_shear_x(),bone.get_shear_y()]))
    var attachments := []
    var colors := []
    var deforms := []
    for slot in source_skeleton.get_slots():
        attachments.append(slot.get_attachment())
        colors.append(slot.get_color())
        deforms.append(slot.get_deform())
    var entry = source.get_animation_state().get_current(0)
    return [values,attachments,colors,deforms,entry.get_animation().get_name() if entry and entry.get_animation() else "idle_loop",entry.get_track_time() if entry else 0.0,source_skeleton.get_color()]

func show_pose(snapshot, transform):
    var animation = data.find_animation(snapshot[4])
    skeleton.set_to_setup_pose()
    if animation: animation.apply(skeleton,-1.0,fposmod(snapshot[5],animation.get_duration()),true,[],1.0,2,0)
    var values: PackedFloat32Array = snapshot[0]
    for i in bones.size():
        var bone = bones[i]
        var at: int = i*7
        bone.set_x(values[at]);bone.set_y(values[at+1]);bone.set_rotation(values[at+2])
        bone.set_scale_x(values[at+3]);bone.set_scale_y(values[at+4])
        bone.set_shear_x(values[at+5]);bone.set_shear_y(values[at+6])
    for i in slots.size():
        var slot = slots[i]
        slot.set_attachment(snapshot[1][i])
        slot.set_color(snapshot[2][i])
        if slot.has_method("set_deform"): slot.set_deform(snapshot[3][i])
        if "shadow" in slot.get_data().get_name(): slot.set_attachment(null)
    skeleton.set_color(snapshot[6])
    for bone in bones: bone.update_world_transform()
    sprite.transform = transform
    sprite.update_skeleton(0.0)
    for mesh in sprite.get_children():
        if mesh.get_class() == "SpineMesh2D": mesh.queue_redraw()

func release():
    if is_instance_valid(sprite): sprite.queue_free()
    sprite = null
    source = null
