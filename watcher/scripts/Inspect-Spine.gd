extends SceneTree

func _initialize():
	var result = {}
	for type in ["SpineSlot", "SpineAttachment", "SpineBone", "SpineSkeleton", "SpineSkeletonDataResource"]:
		var methods = []
		for method in ClassDB.class_get_method_list(type, true):
			methods.append(method.name)
		result[type] = methods
	var file = FileAccess.open(OS.get_cmdline_user_args()[0], FileAccess.WRITE)
	file.store_string(JSON.stringify(result, "  "))
	file.close()
	quit()
