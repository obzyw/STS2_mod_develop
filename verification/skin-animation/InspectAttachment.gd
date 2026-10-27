extends SceneTree
func _initialize(): call_deferred("inspect")
func inspect():
    var args := OS.get_cmdline_user_args()
    ProjectSettings.load_resource_pack(args[0])
    var sprite = ClassDB.instantiate("SpineSprite")
    sprite.set_skeleton_data_res(load(args[1]))
    root.add_child(sprite)
    await process_frame
    var skeleton = sprite.get_skeleton()
    var report := {}
    var data = sprite.get_skeleton_data_res()
    var file = data.get_skeleton_file_res()
    report["file"] = {"path":file.resource_path,"properties":file.get_property_list(),"methods":file.get_method_list()}
    for slot in skeleton.get_slots():
        if slot.get_data().get_name() not in ["5","6","7","8","10","11"]: continue
        var attachment = slot.get_attachment()
        if attachment == null: continue
        var methods := []
        for method in attachment.get_method_list():
            methods.append(method)
        report[slot.get_data().get_name()] = {"class":attachment.get_class(),"methods":methods,"bone":slot.get_bone().get_data().get_bone_name()}
    FileAccess.open(args[2],FileAccess.WRITE).store_string(JSON.stringify(report,"  "))
    quit(0)
