# Original pose authoring for the YU Silent rig. Appended to the installed
# animation mod's adapter in memory; no original shader or rig indices are reused.
func yu_selected() -> bool:
    return data != null and data.resource_path.begins_with("res://YUSilentThreeStages/")

func yu_update_world():
    for joint in skeleton.get_bones(): joint.update_world_transform()

func yu_offset(name: String, degrees: float):
    var joint = skeleton.find_bone(name)
    if joint: joint.set_rotation(joint.get_rotation()+degrees)

func yu_aim(name: String, degrees: float, weight: float):
    var joint = skeleton.find_bone(name)
    if not joint or not joint.get_parent(): return
    var parent = joint.get_parent()
    var transform := Transform2D(Vector2(parent.get_a(),parent.get_c()),Vector2(parent.get_b(),parent.get_d()),Vector2.ZERO)
    var direction := transform.affine_inverse().basis_xform(Vector2.from_angle(deg_to_rad(degrees)))
    joint.set_rotation(lerp_angle(deg_to_rad(joint.get_rotation()),direction.angle(),weight)*180.0/PI)
    yu_update_world()

func yu_pose(name: String, time: float):
    var length: float = maxf(.2,clip_duration(name))
    var phase: float = clampf(time/length,0.0,1.0)
    var anticipation: float = smoothstep(0.0,.18,phase)*(1.0-smoothstep(.18,.38,phase))
    var strike: float = smoothstep(.12,.28,phase)*(1.0-smoothstep(.5,1.0,phase))
    last_name = "" # A fresh native base prevents accumulated joint deltas.
    var bow: bool = name.begins_with("finale-") and name != "finale-slash"
    sample("idle_loop",time*.3)
    if not bow and name != "punch" and not name.begins_with("return"):
        for slot in slots:
            var info = slot.get_data()
            if info.get_name() in ["w4","w5"]:
                slot.set_attachment(skeleton.get_attachment_by_slot_index(info.get_index(),info.get_attachment_name()))
    # Drive distinct attacks from the skin's idle attachment and cloth layout.
    # Preserve the skin's two foot IK constraints; disabling them detaches its feet.
    if bow:
        var bend: float = sin(phase*PI)*.75
        yu_offset("12",8*bend)
        yu_offset("14",32*bend)
        yu_offset("4",-15*bend)
        yu_offset("16",-10*bend)
        yu_offset("18",15*bend)
        yu_update_world()
        yu_aim("7",55, bend)
        yu_aim("8",35, bend)
        yu_aim("22",120,bend)
        yu_aim("23",155,bend)
    elif name.begins_with("down"):
        # Raised weapon, diagonal descent, planted recovery. Feet stay separate.
        yu_offset("14",-10*anticipation+22*strike)
        yu_offset("4",-12*strike)
        yu_offset("16",-12*strike)
        yu_offset("18",16*strike)
        yu_update_world()
        yu_aim("22",lerpf(-100,35,strike),maxf(anticipation,strike))
        yu_aim("23",lerpf(-65,70,strike),maxf(anticipation,strike))
        yu_aim("7",145,strike*.75)
        yu_aim("8",175,strike*.7)
    elif name == "steel":
        # Fan release: both arms open, then close at different rates.
        var fan: float = sin(phase*PI)
        yu_offset("14",8*fan)
        yu_update_world()
        yu_aim("22",-145,fan)
        yu_aim("23",-165,fan)
        yu_aim("7",-25,fan)
        yu_aim("8",-8,fan)
    elif name == "punch":
        # Compact elbow chamber followed by a straight hand strike.
        yu_offset("14",-7*anticipation+12*strike)
        yu_offset("4",-8*strike)
        yu_update_world()
        yu_aim("7",lerpf(105,0,strike),maxf(anticipation,strike))
        yu_aim("8",lerpf(-90,-6,strike),maxf(anticipation,strike))
        yu_aim("22",110,strike*.6)
        yu_aim("23",-45,strike*.6)
    elif name in ["thrust","native-attack","finale-slash"]:
        var slash: float = 1.5 if name == "finale-slash" else (1.0 if name == "native-attack" else 0.0)
        yu_offset("14",-8*anticipation+(14+slash*9)*strike)
        yu_offset("4",-9*strike)
        yu_offset("16",-8*strike)
        yu_offset("18",12*strike)
        yu_update_world()
        yu_aim("22",lerpf(90,-18+slash*30,strike),maxf(anticipation,strike))
        yu_aim("23",lerpf(-95,-8+slash*50,strike),maxf(anticipation,strike))
        yu_aim("7",140,strike*.6)
        yu_aim("8",170,strike*.6)
        # Dagger meshes are weighted to the wrist, not just the weapon slot bone.
        if name == "thrust": yu_offset("24",90*strike)
    else:
        yu_offset("14",-4*sin(phase*PI))
    yu_update_world()
    sprite.update_skeleton(0.0)
    for mesh in meshes: mesh.queue_redraw()
    last_name = "yu:"+name
    last_time = time

func custom(name, time):
    if not yu_selected(): return yu_original_custom(name,time)
    yu_pose(name,time)

func finale_ghost(name,time):
    if not yu_selected(): return yu_original_finale_ghost(name,time)
    yu_pose(name,time)

func departure(kind,index):
    if not yu_selected(): return yu_original_departure(kind,index)
    var motion: String = {"DownStab":"down-normal","SuckerPunch":"punch","NativeAttack":"native-attack"}.get(kind,"thrust")
    yu_pose(motion,[.05,.12,.2][clampi(index,0,2)])
    return Vector2(number("shadowHeight"),0)

func punch_ghost(time,_index):
    if yu_selected(): yu_pose("punch",time)

func warm_return_actions(): return 0
