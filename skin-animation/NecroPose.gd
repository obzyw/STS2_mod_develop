extends RefCounted
# Original skin poses; only the installed skin supplies character artwork.
var source
var sprite
var data
var skeleton
var meshes := []

func bind(body):
    source = body
    data = body.get_skeleton_data_res()
    sprite = ClassDB.instantiate("SpineSprite")
    sprite.name = "YUNecroActionDisplay"
    sprite.set_skeleton_data_res(data)
    sprite.set_update_mode(2)
    body.get_parent().add_child(sprite)
    skeleton = sprite.get_skeleton()
    if body.get_skeleton().get_skin(): skeleton.set_skin(body.get_skeleton().get_skin())
    skeleton.set_scale_x(body.get_skeleton().get_scale_x())
    skeleton.set_scale_y(body.get_skeleton().get_scale_y())
    for child in sprite.get_children():
        if child.get_class() == "SpineMesh2D":
            child.set_process_internal(false)
            meshes.append(child)
    return sprite

func turn(key: String, amount: float):
    var bone = skeleton.find_bone(key)
    if bone: bone.set_rotation(bone.get_rotation()+amount)

func pose(clip: String, phase: float, age: float):
    sprite.transform = source.transform
    var name := "idle_loop"
    var at := age
    if "Reaper_idle" not in clip:
        if "Scourge" in clip: name = "attack"
        elif "Dirge" in clip or "Neurosurge" in clip or "Invoke" in clip: name = "cast_mighty"
        else: name = "cast"
    var animation = data.find_animation(name)
    skeleton.set_to_setup_pose()
    animation.apply(skeleton,-1.0,fposmod(at,animation.get_duration()) if name == "idle_loop" else phase*animation.get_duration(),name == "idle_loop",[],1.0,2,0)
    var pulse: float = sin(clampf(phase,0.0,1.0)*PI)
    if "GraveWarden" in clip:
        turn("33",-7*pulse)
        turn("12",-16*pulse)
        turn("13",28*pulse)
    elif "NegativePulse" in clip:
        turn("33",-12*pulse)
        turn("2",8*pulse)
        turn("12",-25*pulse)
    elif "Dirge" in clip:
        turn("33",4*sin(age*5.5)*pulse)
        turn("2",6*sin(age*3.0)*pulse)
        turn("14",12*sin(age*8.0)*pulse)
    elif "Invoke" in clip:
        turn("33",-8*pulse)
        turn("12",-12*pulse)
        turn("13",16*pulse)
    elif "Neurosurge" in clip:
        turn("2",-11*pulse)
        turn("33",9*pulse)
    elif "Scourge" in clip:
        turn("33",12*pulse)
        turn("59",-10*pulse)
    for bone in skeleton.get_bones(): bone.update_world_transform()
    sprite.update_skeleton(0.0)
    for mesh in meshes: mesh.queue_redraw()

func release():
    if is_instance_valid(sprite): sprite.queue_free()
    sprite = null
    source = null
