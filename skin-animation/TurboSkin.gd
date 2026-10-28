extends RefCounted
# Only original electric accessories are drawn; the original body stays absent.
var source
var helper
var native
var skeleton

func bind(body, accessory, rig):
    source = body
    helper = accessory
    native = rig
    skeleton = helper.get_skeleton()
    filter_slots()

func filter_slots():
    for slot in skeleton.get_slots():
        var name = slot.get_data().get_name()
        var allowed = name.begins_with("deathspark-") or name.begins_with("roundspark") or name == "defect_flare" or name in ["glow1","glow2","glow3","glow4"]
        if not allowed: slot.set_attachment(null)

func restore():
    native.restore()
    helper.hide()
    filter_slots()

func apply_pose(age, core_active):
    native.apply_pose(age,core_active)
    filter_slots()
    var target = source.get_skeleton().find_bone("bone2")
    var torso = skeleton.find_bone("torso")
    var scale = 1.0
    helper.position = Vector2(target.get_world_x(),target.get_world_y())-Vector2(torso.get_world_x(),torso.get_world_y())*scale
    helper.scale = Vector2.ONE*scale
    helper.show()
    helper.update_skeleton(0.0)
    for child in helper.get_children():
        if child.get_class() == "SpineMesh2D": child.queue_redraw()

func release(_restore):
    native.release(false)
    if is_instance_valid(helper): helper.queue_free()
    helper = null
    source = null
    native = null
