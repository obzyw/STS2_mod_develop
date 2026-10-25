extends Node2D

# Keep both original rigs alive during a short visual blend; gameplay already uses the new stance.
var previous
var current
var previous_transform: Transform2D
var current_transform: Transform2D
var previous_color: Color
var current_color: Color
var from_stance = ""
var to_stance = ""
var elapsed = 0.0
var duration = 0.56
var running = false
var progress = 0.0
var completed_count = 0
var started_count = 0
var last_completed = ""
var tone = Color(0.7, 0.55, 1.0)

func start(old_body, new_body, source_stance, target_stance):
	finish()
	previous = old_body
	current = new_body
	from_stance = source_stance
	to_stance = target_stance
	previous_transform = previous.transform
	current_transform = current.transform
	previous_color = previous.modulate
	current_color = current.modulate
	transform = current_transform
	z_index = current.z_index + 2
	match to_stance:
		"Calm":
			duration = 0.58
			tone = Color(0.48, 0.76, 1.0)
		"Wrath":
			duration = 0.44
			tone = Color(1.0, 0.25, 0.36)
		"Divinity":
			duration = 0.68
			tone = Color(1.0, 0.79, 0.36)
		_:
			duration = 0.52
			tone = Color(0.76, 0.58, 1.0)
	elapsed = 0.0
	progress = 0.0
	running = true
	started_count += 1
	previous.visible = true
	current.visible = true
	if previous != current:
		var color = current_color
		color.a = 0.0
		current.modulate = color
	set_process(true)
	queue_redraw()

func _process(delta):
	if not running:
		return
	if not is_instance_valid(current) or not current.visible:
		finish()
		return
	var track = current.get_animation_state().get_current(0)
	if track != null and track.get_animation().get_name() != "Idle":
		finish() # Let the original hit, attack and death animations own the body.
		return
	elapsed += delta
	progress = minf(elapsed / duration, 1.0)
	var t = progress
	var blend = smoothstep(0.16, 0.58, t)
	var settle = 1.0 - smoothstep(0.44, 1.0, t)
	var anticipation = sin(PI * clampf(t / 0.26, 0.0, 1.0))
	if is_instance_valid(previous) and previous != current:
		# Parked rigs have disabled processing; only advance their visual skeleton during the blend.
		previous.update_skeleton(delta)
		previous.transform = previous_transform
		previous.scale *= Vector2(1.0 + 0.018 * anticipation, 1.0 - 0.025 * anticipation)
		previous.position.y += 2.5 * anticipation
		var color = previous_color
		color.a *= 1.0 - blend
		previous.modulate = color
		previous.visible = t < 0.60
		color = current_color
		color.a *= blend
		current.modulate = color
	current.transform = current_transform
	match to_stance:
		"Wrath":
			current.position.x += (-8.0 + 11.0 * smoothstep(0.18, 0.5, t)) * settle
			current.rotation -= 0.015 * settle
			current.scale *= Vector2(1.0 + 0.022 * settle, 1.0 - 0.015 * settle)
		"Divinity":
			current.position.y += 9.0 * settle
			current.scale *= Vector2(1.0 - 0.018 * settle, 1.0 + 0.018 * settle)
		"Calm":
			current.position.y -= 3.5 * settle
			current.scale *= Vector2(1.0 + 0.012 * settle, 1.0 - 0.012 * settle)
		_:
			current.position.y -= (7.0 if from_stance == "Divinity" else 2.0) * settle
	queue_redraw()
	if progress >= 1.0:
		completed_count += 1
		last_completed = to_stance
		finish()

func _draw():
	if not running:
		return
	var t = progress
	var intensity = pow(sin(PI * t), 1.4) * 0.60
	if intensity < 0.005:
		return
	var color = tone
	color.a = intensity
	match to_stance:
		"Calm":
			ellipse(Vector2(0, -28 - 35 * t), Vector2(34 + 55 * t, 10 + 13 * t), color, 1.7)
			color.a *= 0.55
			ellipse(Vector2(0, -16 - 22 * t), Vector2(22 + 42 * t, 7 + 9 * t), color, 1.0)
		"Wrath":
			var points = PackedVector2Array()
			for i in 25:
				var angle = lerpf(-2.5, 0.6, i / 24.0) + t * 0.8
				points.append(Vector2(cos(angle) * (65 + 20 * t), -190 + sin(angle) * 54))
			draw_polyline(points, color, 2.8, true)
		"Divinity":
			ellipse(Vector2(0, -194 - 22 * t), Vector2(42 + 46 * t, 19 + 12 * t), color, 2.0)
			var lavender = Color(0.78, 0.43, 1.0, intensity * 0.7)
			ellipse(Vector2(0, -190 - 22 * t), Vector2(33 + 36 * t, 14 + 10 * t), lavender, 1.2)
		_:
			ellipse(Vector2(0, -148), Vector2(62 * (1.0 - t) + 12, 18 * (1.0 - t) + 3), color, 1.3)
	for i in 7:
		var angle = i * TAU / 7.0 + t * 0.7
		var radius = (42 + 34 * t) if to_stance != "Normal" else (72 - 43 * t)
		var point = Vector2(cos(angle) * radius, -163 + sin(angle) * 67 - 26 * t)
		var size = maxf(0.4, 1.3 + 1.4 * sin(PI * t + i * 0.2))
		draw_colored_polygon(PackedVector2Array([point + Vector2(0,-size*1.7),point + Vector2(size,0),point + Vector2(0,size*1.7),point - Vector2(size,0)]), color)

func ellipse(center, radius, color, width):
	var points = PackedVector2Array()
	for i in 49:
		var angle = i * TAU / 48.0
		points.append(center + Vector2(cos(angle) * radius.x, sin(angle) * radius.y))
	draw_polyline(points, color, width, true)

func finish():
	if not running:
		return
	running = false
	if is_instance_valid(previous) and previous != current:
		previous.transform = previous_transform
		previous.modulate = previous_color
		previous.visible = false
	if is_instance_valid(current):
		current.transform = current_transform
		current.modulate = current_color
	set_process(false)
	queue_redraw()

func _exit_tree():
	finish()
