@tool
extends MenuButton

const SETTING_GENERATE_RAIL_MESH := "badnik_framework_extras/generate_rail_mesh/enabled"
const SETTING_GENERATE_RAIL_MESH_COLOR := "badnik_framework_extras/generate_rail_mesh/color"
const SPRING_PREFAB_PATH := "res://SonicAssets/Prefabs/Interactables/[interactable]_spring_red.tscn"
const BOOST_PAD_PREFAB_PATH := "res://SonicAssets/Prefabs/Interactables/[interactable]_speed_pad.tscn"
const DASH_RING_PREFAB_PATH := "res://SonicAssets/Prefabs/Interactables/[interactable]_dash_ring.tscn"
const RING_PREFAB_PATH := "res://SonicAssets/Prefabs/Items/[item]_ring.tscn"
const GRIND_RAIL_SCRIPT_PATH := "res://Scripts/Interactables/GrindRail.cs"
const SPLINE_2D_TRIGGER_SCRIPT_PATH := "res://Scripts/Interactables/Spline2DTrigger.cs"
const COLLISION_SHAPE_FROM_CSG_SCRIPT_PATH := "res://Scripts/Utils/CollissionShapeFromCSGPolygon.cs"

# Set by BadnikFrameworkExtras right after instantiation. Left untyped
# (rather than "EditorUndoRedoManager") on purpose so this script never
# forces a type resolution of anything defined elsewhere at parse time.
var undo_redo: Variant

var place_objects_submenu: PopupMenu
var generate_curve_submenu: PopupMenu
var curve_ops_submenu: PopupMenu
# Each entry: {"submenu": PopupMenu, "callable": Callable} so _exit_tree can
# disconnect them all without needing one named variable per launcher type.
var launcher_submenus: Array = []


func _enter_tree() -> void:
	text = "BF Curve tools"

	place_objects_submenu = PopupMenu.new()
	place_objects_submenu.name = "PlaceObjectsSubmenu"
	place_objects_submenu.add_item("Convert to grind rail", 0)
	place_objects_submenu.add_item("Convert to autorun", 1)
	_add_launcher_chain_menu(
		place_objects_submenu,
		"Make spring chain",
		"SpringScaleSubmenu",
		SPRING_PREFAB_PATH,
		"Make Spring Chain"
	)
	_add_launcher_chain_menu(
		place_objects_submenu,
		"Make boost pad chain",
		"BoostPadScaleSubmenu",
		BOOST_PAD_PREFAB_PATH,
		"Make Boost Pad Chain"
	)
	_add_launcher_chain_menu(
		place_objects_submenu,
		"Make dash ring chain",
		"DashRingScaleSubmenu",
		DASH_RING_PREFAB_PATH,
		"Make Dash Ring Chain"
	)
	place_objects_submenu.add_item("Place rings...", 9)
	place_objects_submenu.id_pressed.connect(_on_place_objects_menu_select)
	get_popup().add_child(place_objects_submenu)
	get_popup().add_submenu_item("Place Objects", "PlaceObjectsSubmenu")

	generate_curve_submenu = PopupMenu.new()
	generate_curve_submenu.name = "GenerateCurveSubmenu"
	generate_curve_submenu.add_item("Generate loop...", 7)
	generate_curve_submenu.add_item("Generate corkscrew...", 8)
	generate_curve_submenu.id_pressed.connect(_on_generate_curve_menu_select)
	get_popup().add_child(generate_curve_submenu)
	get_popup().add_submenu_item("Generate Curve", "GenerateCurveSubmenu")

	curve_ops_submenu = PopupMenu.new()
	curve_ops_submenu.name = "CurveOpsSubmenu"
	curve_ops_submenu.add_item("Flatten curve", 2)
	curve_ops_submenu.add_item("Reverse curve direction", 4)
	curve_ops_submenu.add_item("Resample curve...", 5)
	curve_ops_submenu.add_item("Simplify curve...", 6)
	curve_ops_submenu.id_pressed.connect(_on_curve_ops_menu_select)
	get_popup().add_child(curve_ops_submenu)
	get_popup().add_submenu_item("Curve Operations", "CurveOpsSubmenu")

	EditorInterface.get_selection().selection_changed.connect(_on_selection_change)
	_on_selection_change() # To refresh visibility


# Builds a "1x-5x scale" submenu for one launcher-chain type (spring, boost
# pad, dash ring, ...) and attaches it under parent_menu as a submenu item.
func _add_launcher_chain_menu(
	parent_menu: PopupMenu,
	item_label: String,
	submenu_name: String,
	prefab_path: String,
	action_label: String
) -> void:
	var submenu := PopupMenu.new()
	submenu.name = submenu_name
	for factor in range(1, 6):
		submenu.add_item("%dx scale" % factor, factor)
	var callable := _on_launcher_scale_selected.bind(prefab_path, action_label)
	submenu.id_pressed.connect(callable)
	launcher_submenus.append({"submenu": submenu, "callable": callable})

	parent_menu.add_child(submenu)
	parent_menu.add_submenu_item(item_label, submenu_name)


func _exit_tree() -> void:
	place_objects_submenu.id_pressed.disconnect(_on_place_objects_menu_select)
	generate_curve_submenu.id_pressed.disconnect(_on_generate_curve_menu_select)
	curve_ops_submenu.id_pressed.disconnect(_on_curve_ops_menu_select)
	for entry in launcher_submenus:
		entry["submenu"].id_pressed.disconnect(entry["callable"])
	EditorInterface.get_selection().selection_changed.disconnect(_on_selection_change)


func _on_place_objects_menu_select(id: int) -> void:
	match id:
		0:
			_convert_to_grind_rail()
		1:
			_convert_to_autorun()
		9:
			_prompt_place_rings()


func _on_generate_curve_menu_select(id: int) -> void:
	match id:
		7:
			_prompt_generate_loop()
		8:
			_prompt_generate_corkscrew()


func _on_curve_ops_menu_select(id: int) -> void:
	match id:
		2:
			_flatten_curve()
		4:
			_reverse_curve_direction()
		5:
			_prompt_resample_curve()
		6:
			_prompt_simplify_curve()


func _on_launcher_scale_selected(scale_factor: int, prefab_path: String, action_label: String) -> void:
	_make_launcher_chain(prefab_path, action_label, scale_factor)


func _on_selection_change() -> void:
	visible = false
	var selected_nodes := EditorInterface.get_selection().get_selected_nodes()
	for selected_node in selected_nodes:
		if selected_node is Path3D:
			visible = true
			break


# Shows a small "label + spin box" dialog with a sensible pre-filled default.
# on_confirm is called with the chosen float value if the user confirms;
# nothing happens on cancel.
func _prompt_float(
	dialog_title: String,
	label_text: String,
	default_value: float,
	min_value: float,
	max_value: float,
	step: float,
	on_confirm: Callable
) -> void:
	var dialog := ConfirmationDialog.new()
	dialog.title = dialog_title

	var container := HBoxContainer.new()
	container.add_theme_constant_override("separation", 8)

	var label := Label.new()
	label.text = label_text
	container.add_child(label)

	var spin_box := SpinBox.new()
	spin_box.min_value = min_value
	spin_box.max_value = max_value
	spin_box.step = step
	spin_box.value = default_value
	spin_box.custom_minimum_size = Vector2(100, 0)
	container.add_child(spin_box)

	dialog.add_child(container)
	dialog.confirmed.connect(func():
		on_confirm.call(spin_box.value)
		dialog.queue_free()
	)
	dialog.canceled.connect(func(): dialog.queue_free())

	EditorInterface.get_base_control().add_child(dialog)
	dialog.popup_centered()


# ---------------------------------------------------------------------------
# Convert to grind rail
# ---------------------------------------------------------------------------

func _convert_to_grind_rail() -> void:
	var selected_nodes := EditorInterface.get_selection().get_selected_nodes()
	for selected_node in selected_nodes:
		if selected_node is Path3D:
			_grind_rail_for_node(selected_node as Path3D)


func _build_grind_rail_nodes(rail_color: Color, generate_visual: bool) -> Dictionary:
	var visual: CSGPolygon3D = null
	if generate_visual:
		visual = CSGPolygon3D.new()
		visual.polygon = PackedVector2Array([
			Vector2(-0.1, -0.1),
			Vector2(-0.1, 0.0),
			Vector2(0.1, 0.0),
			Vector2(0.1, -0.1),
		])
		visual.mode = CSGPolygon3D.MODE_PATH
		visual.path_node = NodePath("..")
		visual.name = "RailVisual"
		visual.path_interval = 1.0
		visual.path_simplify_angle = 1.0
		visual.path_continuous_u = true
		visual.path_local = true
		visual.path_interval_type = CSGPolygon3D.PATH_INTERVAL_DISTANCE

		var material := StandardMaterial3D.new()
		material.albedo_color = rail_color
		visual.material = material

	var hitbox := CSGPolygon3D.new()
	hitbox.polygon = PackedVector2Array([
		Vector2(-1.0, 0.0),
		Vector2(-1.0, 1.0),
		Vector2(1.0, 1.0),
		Vector2(1.0, 0.0),
	])
	hitbox.mode = CSGPolygon3D.MODE_PATH
	hitbox.path_node = NodePath("..")
	hitbox.name = "RailHitbox"
	hitbox.path_interval = 1.0
	hitbox.path_simplify_angle = 5.0
	hitbox.path_continuous_u = true
	hitbox.path_local = true
	hitbox.path_interval_type = CSGPolygon3D.PATH_INTERVAL_DISTANCE
	hitbox.visible = false

	var area := Area3D.new()
	area.name = "Area3D"
	area.collision_layer = 256 # TriggersForPlayer
	area.collision_mask = 2 # Player

	var shape := CollisionShape3D.new()
	shape.shape = ConcavePolygonShape3D.new()
	shape.name = "CollisionShape3D"
	area.add_child(shape)

	return {
		"visual": visual,
		"hitbox": hitbox,
		"area": area,
		"shape": shape,
	}


func _grind_rail_for_node(path3d: Path3D) -> void:
	var scene_root := EditorInterface.get_edited_scene_root()
	var editor_settings := EditorInterface.get_editor_settings()
	var generate_visual: bool = editor_settings.get_setting(SETTING_GENERATE_RAIL_MESH)
	var rail_color: Color = editor_settings.get_setting(SETTING_GENERATE_RAIL_MESH_COLOR)

	var old_visual := path3d.get_node_or_null("RailVisual")
	var old_hitbox := path3d.get_node_or_null("RailHitbox")
	var old_area := path3d.get_node_or_null("Area3D")
	var old_script: Script = path3d.get_script()

	var built := _build_grind_rail_nodes(rail_color, generate_visual)

	var ctx := {
		"path3d": path3d,
		"scene_root": scene_root,
		"old_visual": old_visual,
		"old_hitbox": old_hitbox,
		"old_area": old_area,
		"old_script": old_script,
		"new_visual": built["visual"],
		"new_hitbox": built["hitbox"],
		"new_area": built["area"],
		"new_shape": built["shape"],
		"grind_rail_script": load(GRIND_RAIL_SCRIPT_PATH),
		"on_curve_changed": Callable(path3d, "OnCurveChanged"),
	}

	undo_redo.create_action("Convert to Grind Rail", UndoRedo.MERGE_DISABLE, path3d)
	undo_redo.add_do_method(self, "_do_grind_rail", ctx)
	undo_redo.add_undo_method(self, "_undo_grind_rail", ctx)
	if old_visual: undo_redo.add_undo_reference(old_visual)
	if old_hitbox: undo_redo.add_undo_reference(old_hitbox)
	if old_area: undo_redo.add_undo_reference(old_area)
	if ctx["new_visual"]: undo_redo.add_do_reference(ctx["new_visual"])
	undo_redo.add_do_reference(ctx["new_hitbox"])
	undo_redo.add_do_reference(ctx["new_area"])
	undo_redo.commit_action()


func _do_grind_rail(ctx: Dictionary) -> void:
	var path3d: Path3D = ctx["path3d"]
	var scene_root: Node = ctx["scene_root"]
	var old_visual = ctx["old_visual"]
	var old_hitbox = ctx["old_hitbox"]
	var old_area = ctx["old_area"]
	var new_visual = ctx["new_visual"]
	var new_hitbox = ctx["new_hitbox"]
	var new_area = ctx["new_area"]
	var new_shape = ctx["new_shape"]
	var on_curve_changed: Callable = ctx["on_curve_changed"]

	if old_visual: path3d.remove_child(old_visual)
	if old_hitbox: path3d.remove_child(old_hitbox)
	if old_area: path3d.remove_child(old_area)

	if new_visual:
		path3d.add_child(new_visual)
		new_visual.owner = scene_root
	path3d.add_child(new_hitbox)
	new_hitbox.owner = scene_root
	path3d.add_child(new_area)
	new_area.owner = scene_root
	new_shape.owner = scene_root

	path3d.set_script(ctx["grind_rail_script"])
	path3d.set("BoundingBox", new_shape)
	path3d.set("RailHitboxMesh", new_hitbox)
	if new_visual:
		path3d.set("RailVisibleMesh", new_visual)
	path3d.set("Hitbox", new_area)
	if path3d.has_signal("CurveChanged") and not path3d.is_connected("CurveChanged", on_curve_changed):
		path3d.connect("CurveChanged", on_curve_changed)


func _undo_grind_rail(ctx: Dictionary) -> void:
	var path3d: Path3D = ctx["path3d"]
	var scene_root: Node = ctx["scene_root"]
	var old_visual = ctx["old_visual"]
	var old_hitbox = ctx["old_hitbox"]
	var old_area = ctx["old_area"]
	var new_visual = ctx["new_visual"]
	var new_hitbox = ctx["new_hitbox"]
	var new_area = ctx["new_area"]
	var on_curve_changed: Callable = ctx["on_curve_changed"]

	if path3d.has_signal("CurveChanged") and path3d.is_connected("CurveChanged", on_curve_changed):
		path3d.disconnect("CurveChanged", on_curve_changed)

	path3d.remove_child(new_area)
	path3d.remove_child(new_hitbox)
	if new_visual:
		path3d.remove_child(new_visual)

	path3d.set_script(ctx["old_script"])

	if old_visual:
		path3d.add_child(old_visual)
		old_visual.owner = scene_root
	if old_hitbox:
		path3d.add_child(old_hitbox)
		old_hitbox.owner = scene_root
	if old_area:
		path3d.add_child(old_area)
		old_area.owner = scene_root


# ---------------------------------------------------------------------------
# Convert to autorun
# ---------------------------------------------------------------------------

func _convert_to_autorun() -> void:
	var selected_nodes := EditorInterface.get_selection().get_selected_nodes()
	for selected_node in selected_nodes:
		if selected_node is Path3D:
			_autorun_for_node(selected_node as Path3D)


func _autorun_for_node(path3d: Path3D) -> void:
	var scene_root := EditorInterface.get_edited_scene_root()

	var old_hitbox := path3d.get_node_or_null("CSGPolygon3DHitbox")
	var old_area := path3d.get_node_or_null("Area3D")

	var new_hitbox := CSGPolygon3D.new()
	new_hitbox.polygon = PackedVector2Array([
		Vector2(-5.0, -5.0),
		Vector2(-5.0, 5.0),
		Vector2(5.0, 5.0),
		Vector2(5.0, -5.0),
	])
	new_hitbox.mode = CSGPolygon3D.MODE_PATH
	new_hitbox.path_node = NodePath("..")
	new_hitbox.name = "CSGPolygon3DHitbox"
	new_hitbox.path_interval = 1.0
	new_hitbox.path_simplify_angle = 5.0
	new_hitbox.path_continuous_u = true
	new_hitbox.path_local = true
	new_hitbox.path_interval_type = CSGPolygon3D.PATH_INTERVAL_DISTANCE
	new_hitbox.visible = false

	var spline_2d_trigger = load(SPLINE_2D_TRIGGER_SCRIPT_PATH).new()
	spline_2d_trigger.name = "Area3D"
	# collision_layer / collision_mask are inherited from the built-in Area3D,
	# not custom Spline2DTrigger properties, so GDScript needs the engine's
	# snake_case names here rather than the C#-side PascalCase names.
	spline_2d_trigger.collision_layer = 256 # TriggersForPlayer
	spline_2d_trigger.collision_mask = 2 # Player
	spline_2d_trigger.Path = path3d
	spline_2d_trigger.ReleaseOnDistanceFromPath = 20

	var shape := CollisionShape3D.new()
	shape.shape = ConcavePolygonShape3D.new()
	shape.name = "CollisionShape3D"
	spline_2d_trigger.add_child(shape)

	# Separate node because CSGPolygon3D is... just weird to work with.
	var copy_shape = load(COLLISION_SHAPE_FROM_CSG_SCRIPT_PATH).new()
	copy_shape.name = "CopyShape"
	copy_shape.Shape = shape
	copy_shape.CsgMesh = new_hitbox
	shape.add_child(copy_shape)

	var ctx := {
		"path3d": path3d,
		"scene_root": scene_root,
		"old_hitbox": old_hitbox,
		"old_area": old_area,
		"new_hitbox": new_hitbox,
		"spline_2d_trigger": spline_2d_trigger,
		"shape": shape,
		"copy_shape": copy_shape,
	}

	undo_redo.create_action("Convert to Autorun", UndoRedo.MERGE_DISABLE, path3d)
	undo_redo.add_do_method(self, "_do_autorun", ctx)
	undo_redo.add_undo_method(self, "_undo_autorun", ctx)
	if old_hitbox: undo_redo.add_undo_reference(old_hitbox)
	if old_area: undo_redo.add_undo_reference(old_area)
	undo_redo.add_do_reference(new_hitbox)
	undo_redo.add_do_reference(spline_2d_trigger)
	undo_redo.commit_action()


func _do_autorun(ctx: Dictionary) -> void:
	var path3d: Path3D = ctx["path3d"]
	var scene_root: Node = ctx["scene_root"]
	var old_hitbox = ctx["old_hitbox"]
	var old_area = ctx["old_area"]
	var new_hitbox = ctx["new_hitbox"]
	var spline_2d_trigger = ctx["spline_2d_trigger"]
	var shape = ctx["shape"]
	var copy_shape = ctx["copy_shape"]

	if old_hitbox: path3d.remove_child(old_hitbox)
	if old_area: path3d.remove_child(old_area)

	path3d.add_child(new_hitbox)
	new_hitbox.owner = scene_root
	path3d.add_child(spline_2d_trigger)
	spline_2d_trigger.owner = scene_root
	shape.owner = scene_root
	copy_shape.owner = scene_root


func _undo_autorun(ctx: Dictionary) -> void:
	var path3d: Path3D = ctx["path3d"]
	var scene_root: Node = ctx["scene_root"]
	var old_hitbox = ctx["old_hitbox"]
	var old_area = ctx["old_area"]
	var new_hitbox = ctx["new_hitbox"]
	var spline_2d_trigger = ctx["spline_2d_trigger"]

	path3d.remove_child(spline_2d_trigger)
	path3d.remove_child(new_hitbox)

	if old_hitbox:
		path3d.add_child(old_hitbox)
		old_hitbox.owner = scene_root
	if old_area:
		path3d.add_child(old_area)
		old_area.owner = scene_root


# ---------------------------------------------------------------------------
# Flatten curve
# ---------------------------------------------------------------------------

func _flatten_curve() -> void:
	var selected_nodes := EditorInterface.get_selection().get_selected_nodes()
	for selected_node in selected_nodes:
		if not (selected_node is Path3D):
			continue
		var path3d := selected_node as Path3D
		if path3d.curve == null or path3d.curve.point_count == 0:
			continue
		_flatten_curve_for_node(path3d)


func _curve_snapshot(curve: Curve3D) -> Array:
	var snapshot := []
	for i in range(curve.point_count):
		snapshot.append({
			"pos": curve.get_point_position(i),
			"in": curve.get_point_in(i),
			"out": curve.get_point_out(i),
		})
	return snapshot


func _curve_apply(curve: Curve3D, snapshot: Array) -> void:
	for i in range(snapshot.size()):
		curve.set_point_position(i, snapshot[i]["pos"])
		curve.set_point_in(i, snapshot[i]["in"])
		curve.set_point_out(i, snapshot[i]["out"])


func _do_curve_snapshot(ctx: Dictionary) -> void:
	_curve_apply(ctx["curve"], ctx["after"])


func _undo_curve_snapshot(ctx: Dictionary) -> void:
	_curve_apply(ctx["curve"], ctx["before"])


# Unlike _curve_apply (which assumes the same point count before and after,
# and just moves existing points), this rebuilds the curve from scratch.
# Needed for resample/simplify, which change how many points exist.
func _curve_apply_full(curve: Curve3D, snapshot: Array) -> void:
	curve.clear_points()
	for point in snapshot:
		curve.add_point(point["pos"], point["in"], point["out"])


func _do_curve_snapshot_full(ctx: Dictionary) -> void:
	_curve_apply_full(ctx["curve"], ctx["after"])


func _undo_curve_snapshot_full(ctx: Dictionary) -> void:
	_curve_apply_full(ctx["curve"], ctx["before"])


func _flatten_curve_for_node(path3d: Path3D) -> void:
	var curve := path3d.curve
	var before := _curve_snapshot(curve)

	var average_y := 0.0
	for point in before:
		average_y += point["pos"].y
	average_y /= before.size()

	var after := []
	for point in before:
		var pos: Vector3 = point["pos"]
		pos.y = average_y
		var point_in: Vector3 = point["in"]
		point_in.y = 0
		var point_out: Vector3 = point["out"]
		point_out.y = 0
		after.append({"pos": pos, "in": point_in, "out": point_out})

	var ctx := {"curve": curve, "before": before, "after": after}

	undo_redo.create_action("Flatten Curve", UndoRedo.MERGE_DISABLE, path3d)
	undo_redo.add_do_method(self, "_do_curve_snapshot", ctx)
	undo_redo.add_undo_method(self, "_undo_curve_snapshot", ctx)
	undo_redo.commit_action()


# ---------------------------------------------------------------------------
# Reverse curve direction
# ---------------------------------------------------------------------------

func _reverse_curve_direction() -> void:
	var selected_nodes := EditorInterface.get_selection().get_selected_nodes()
	for selected_node in selected_nodes:
		if not (selected_node is Path3D):
			continue
		var path3d := selected_node as Path3D
		if path3d.curve == null or path3d.curve.point_count == 0:
			continue
		_reverse_curve_for_node(path3d)


func _reverse_curve_for_node(path3d: Path3D) -> void:
	var curve := path3d.curve
	var before := _curve_snapshot(curve)
	var count := before.size()

	var after := []
	after.resize(count)
	for i in range(count):
		var source: Dictionary = before[i]
		var j := count - 1 - i
		# Reversing direction also swaps and flips the in/out tangent
		# handles, since what used to be the "arriving" handle becomes
		# the "leaving" handle once the point order is flipped.
		after[j] = {
			"pos": source["pos"],
			"in": -source["out"],
			"out": -source["in"],
		}

	var ctx := {"curve": curve, "before": before, "after": after}

	undo_redo.create_action("Reverse Curve Direction", UndoRedo.MERGE_DISABLE, path3d)
	undo_redo.add_do_method(self, "_do_curve_snapshot", ctx)
	undo_redo.add_undo_method(self, "_undo_curve_snapshot", ctx)
	undo_redo.commit_action()


# ---------------------------------------------------------------------------
# Resample curve
# ---------------------------------------------------------------------------

func _prompt_resample_curve() -> void:
	_prompt_float("Resample Curve", "Point spacing (m):", 1.0, 0.05, 100.0, 0.05, _resample_curve_selected)


func _resample_curve_selected(spacing: float) -> void:
	var selected_nodes := EditorInterface.get_selection().get_selected_nodes()
	for selected_node in selected_nodes:
		if not (selected_node is Path3D):
			continue
		var path3d := selected_node as Path3D
		if path3d.curve == null or path3d.curve.point_count < 2:
			continue
		_resample_curve_for_node(path3d, spacing)


func _resample_curve_for_node(path3d: Path3D, spacing: float) -> void:
	var curve := path3d.curve
	var before := _curve_snapshot(curve)
	var baked_length := curve.get_baked_length()
	if baked_length <= 0.0:
		return

	var point_count: int = max(2, int(round(baked_length / spacing)) + 1)
	# Rough Catmull-Rom-style handle length; keeps the resampled curve
	# reasonably smooth without needing a full tangent-fit solve.
	var handle_length := spacing / 3.0
	var tangent_probe: float = min(spacing * 0.25, baked_length * 0.01)

	var after := []
	for i in range(point_count):
		var offset: float = (float(i) / float(point_count - 1)) * baked_length
		var pos: Vector3 = curve.sample_baked(offset)

		var offset_prev: float = clamp(offset - tangent_probe, 0.0, baked_length)
		var offset_next: float = clamp(offset + tangent_probe, 0.0, baked_length)
		var pos_prev: Vector3 = curve.sample_baked(offset_prev)
		var pos_next: Vector3 = curve.sample_baked(offset_next)

		var tangent := pos_next - pos_prev
		if tangent.length() > 0.0001:
			tangent = tangent.normalized()
		else:
			tangent = Vector3.ZERO

		after.append({
			"pos": pos,
			"in": -tangent * handle_length,
			"out": tangent * handle_length,
		})

	var ctx := {"curve": curve, "before": before, "after": after}

	undo_redo.create_action("Resample Curve", UndoRedo.MERGE_DISABLE, path3d)
	undo_redo.add_do_method(self, "_do_curve_snapshot_full", ctx)
	undo_redo.add_undo_method(self, "_undo_curve_snapshot_full", ctx)
	undo_redo.commit_action()


# ---------------------------------------------------------------------------
# Simplify curve
# ---------------------------------------------------------------------------

func _prompt_simplify_curve() -> void:
	_prompt_float("Simplify Curve", "Angle tolerance (degrees):", 5.0, 0.1, 90.0, 0.1, _simplify_curve_selected)


func _simplify_curve_selected(angle_tolerance_deg: float) -> void:
	var selected_nodes := EditorInterface.get_selection().get_selected_nodes()
	for selected_node in selected_nodes:
		if not (selected_node is Path3D):
			continue
		var path3d := selected_node as Path3D
		if path3d.curve == null or path3d.curve.point_count < 3:
			continue
		_simplify_curve_for_node(path3d, angle_tolerance_deg)


func _simplify_curve_for_node(path3d: Path3D, angle_tolerance_deg: float) -> void:
	var curve := path3d.curve
	var before := _curve_snapshot(curve)
	var angle_tolerance_rad := deg_to_rad(angle_tolerance_deg)

	# Keep a point if the curve bends more than the tolerance there;
	# always keep both endpoints. This measures against the last KEPT
	# point (not the last original point), so tolerance doesn't reset
	# every single point and near-straight runs collapse properly.
	var after := [before[0]]
	for i in range(1, before.size() - 1):
		var prev_pos: Vector3 = after[after.size() - 1]["pos"]
		var this_pos: Vector3 = before[i]["pos"]
		var next_pos: Vector3 = before[i + 1]["pos"]

		var dir_in := this_pos - prev_pos
		var dir_out := next_pos - this_pos
		if dir_in.length() < 0.0001 or dir_out.length() < 0.0001:
			after.append(before[i])
			continue

		var angle: float = dir_in.normalized().angle_to(dir_out.normalized())
		if angle > angle_tolerance_rad:
			after.append(before[i])
	after.append(before[before.size() - 1])

	if after.size() < 2:
		after = before

	var ctx := {"curve": curve, "before": before, "after": after}

	undo_redo.create_action("Simplify Curve", UndoRedo.MERGE_DISABLE, path3d)
	undo_redo.add_do_method(self, "_do_curve_snapshot_full", ctx)
	undo_redo.add_undo_method(self, "_undo_curve_snapshot_full", ctx)
	undo_redo.commit_action()


# ---------------------------------------------------------------------------
# Make launcher chain (springs / boost pads / dash rings)
#
# All three share the same DirectionalLauncher script and placement logic;
# only the prefab differs. See _add_launcher_chain_menu for how each type's
# 1x-5x submenu is wired up to this.
# ---------------------------------------------------------------------------

func _make_launcher_chain(prefab_path: String, action_label: String, scale_factor: int) -> void:
	var selected_nodes := EditorInterface.get_selection().get_selected_nodes()
	for selected_node in selected_nodes:
		if not (selected_node is Path3D):
			continue
		var path3d := selected_node as Path3D
		if path3d.curve == null or path3d.curve.point_count <= 1:
			continue
		_launcher_chain_for_node(path3d, prefab_path, action_label, scale_factor)


func _launcher_chain_for_node(path3d: Path3D, prefab_path: String, action_label: String, scale_factor: int) -> void:
	var scene_root := EditorInterface.get_edited_scene_root()
	var launcher_scale := Vector3(scale_factor, scale_factor, scale_factor)
	var launcher_prefab := load(prefab_path) as PackedScene

	var old_children := path3d.get_children()

	var new_launchers := []
	for i in range(path3d.curve.point_count - 1):
		var this_point: Vector3 = path3d.global_transform * path3d.curve.get_point_position(i)
		var next_point: Vector3 = path3d.global_transform * path3d.curve.get_point_position(i + 1)

		var launcher = launcher_prefab.instantiate()
		launcher.Snap = true
		launcher.MinVelocity = 100
		launcher.MaxVelocity = 100
		launcher.KeepVelocityDistance = this_point.distance_to(next_point)
		launcher.scale = launcher_scale
		new_launchers.append({
			"node": launcher,
			"position": this_point,
			"look_at": next_point,
		})

	var ctx := {
		"path3d": path3d,
		"scene_root": scene_root,
		"old_children": old_children,
		"new_launchers": new_launchers,
	}

	undo_redo.create_action("%s (%dx)" % [action_label, scale_factor], UndoRedo.MERGE_DISABLE, path3d)
	undo_redo.add_do_method(self, "_do_launcher_chain", ctx)
	undo_redo.add_undo_method(self, "_undo_launcher_chain", ctx)
	for child in old_children:
		undo_redo.add_undo_reference(child)
	for entry in new_launchers:
		undo_redo.add_do_reference(entry["node"])
	undo_redo.commit_action()


func _do_launcher_chain(ctx: Dictionary) -> void:
	var path3d: Path3D = ctx["path3d"]
	var scene_root: Node = ctx["scene_root"]
	for child in ctx["old_children"]:
		path3d.remove_child(child)
	for entry in ctx["new_launchers"]:
		var launcher = entry["node"]
		path3d.add_child(launcher)
		launcher.owner = scene_root
		launcher.global_position = entry["position"]
		launcher.look_at(entry["look_at"])


func _undo_launcher_chain(ctx: Dictionary) -> void:
	var path3d: Path3D = ctx["path3d"]
	for entry in ctx["new_launchers"]:
		path3d.remove_child(entry["node"])
	for child in ctx["old_children"]:
		path3d.add_child(child)


# ---------------------------------------------------------------------------
# Place rings
#
# Unlike the launcher chains (one per curve point), rings are spaced evenly
# by distance along the curve, since they're meant to be picked up in a
# steady stream rather than mark specific control points.
# ---------------------------------------------------------------------------

func _prompt_place_rings() -> void:
	_prompt_fields("Place Rings", [
		{"label": "Spacing (m):", "default": 2.0, "min": 0.1, "max": 50.0, "step": 0.1},
		{"label": "Height offset (m):", "default": 0.5, "min": -10.0, "max": 10.0, "step": 0.1},
	], _on_place_rings_confirmed)


func _on_place_rings_confirmed(values: Array) -> void:
	var spacing: float = values[0]
	var height_offset: float = values[1]

	var selected_nodes := EditorInterface.get_selection().get_selected_nodes()
	for selected_node in selected_nodes:
		if not (selected_node is Path3D):
			continue
		var path3d := selected_node as Path3D
		if path3d.curve == null or path3d.curve.get_baked_length() <= 0.0:
			continue
		_place_rings_for_node(path3d, spacing, height_offset)


func _place_rings_for_node(path3d: Path3D, spacing: float, height_offset: float) -> void:
	var scene_root := EditorInterface.get_edited_scene_root()
	var ring_prefab := load(RING_PREFAB_PATH) as PackedScene
	var curve := path3d.curve
	var baked_length := curve.get_baked_length()
	# global_transform.basis rotates a direction into world space without
	# applying position/scale, which is what we want for an up vector.
	var rotation_basis := path3d.global_transform.basis.orthonormalized()

	var old_children := path3d.get_children()

	var new_rings := []
	var offset := 0.0
	while offset <= baked_length:
		var local_pos: Vector3 = curve.sample_baked(offset)
		var local_up: Vector3 = curve.sample_baked_up_vector(offset, true) # true: respect tilt
		var world_pos: Vector3 = path3d.global_transform * local_pos
		var world_up: Vector3 = rotation_basis * local_up
		world_pos += world_up * height_offset

		var ring = ring_prefab.instantiate()
		new_rings.append({"node": ring, "position": world_pos})
		offset += spacing

	var ctx := {
		"path3d": path3d,
		"scene_root": scene_root,
		"old_children": old_children,
		"new_rings": new_rings,
	}

	undo_redo.create_action("Place Rings (%.1fm)" % spacing, UndoRedo.MERGE_DISABLE, path3d)
	undo_redo.add_do_method(self, "_do_place_rings", ctx)
	undo_redo.add_undo_method(self, "_undo_place_rings", ctx)
	for child in old_children:
		undo_redo.add_undo_reference(child)
	for entry in new_rings:
		undo_redo.add_do_reference(entry["node"])
	undo_redo.commit_action()


func _do_place_rings(ctx: Dictionary) -> void:
	var path3d: Path3D = ctx["path3d"]
	var scene_root: Node = ctx["scene_root"]
	for child in ctx["old_children"]:
		path3d.remove_child(child)
	for entry in ctx["new_rings"]:
		var ring = entry["node"]
		path3d.add_child(ring)
		ring.owner = scene_root
		ring.global_position = entry["position"]


func _undo_place_rings(ctx: Dictionary) -> void:
	var path3d: Path3D = ctx["path3d"]
	for entry in ctx["new_rings"]:
		path3d.remove_child(entry["node"])
	for child in ctx["old_children"]:
		path3d.add_child(child)


# ---------------------------------------------------------------------------
# Generate loop / corkscrew
#
# Both append new points to the end of the selected Path3D's existing curve
# (continuing from wherever it currently ends), rather than replacing it.
#
# The tricky part isn't the geometry, it's the orientation: Godot's automatic
# curve up-vectors use a twist-minimizing algorithm that deliberately keeps
# the frame from rotating as you move along the curve. That's the opposite
# of what a loop needs -- the "up" direction has to continuously rotate to
# keep pointing toward the loop's center, otherwise the player ends up
# oriented wrong partway around. So for each generated point, this computes
# the exact "should point toward the center" up vector from the parametric
# formula, compares it to whatever Godot's default frame computed, and
# bakes the difference into that point's "tilt" -- which is precisely what
# tilt is for.
# ---------------------------------------------------------------------------

# Generic multi-field "label + spin box per row" dialog, used by both the
# loop and corkscrew prompts (and reusable for future multi-parameter tools).
# fields: Array of {"label": String, "default": float, "min": float, "max": float, "step": float}
# on_confirm is called with an Array of chosen values, in the same order as fields.
func _prompt_fields(dialog_title: String, fields: Array, on_confirm: Callable) -> void:
	var dialog := ConfirmationDialog.new()
	dialog.title = dialog_title

	var vbox := VBoxContainer.new()
	vbox.add_theme_constant_override("separation", 6)

	var spin_boxes := []
	for field in fields:
		var row := HBoxContainer.new()
		row.add_theme_constant_override("separation", 8)

		var label := Label.new()
		label.text = field["label"]
		label.custom_minimum_size = Vector2(170, 0)
		row.add_child(label)

		var spin_box := SpinBox.new()
		spin_box.min_value = field["min"]
		spin_box.max_value = field["max"]
		spin_box.step = field["step"]
		spin_box.value = field["default"]
		spin_box.custom_minimum_size = Vector2(100, 0)
		row.add_child(spin_box)
		spin_boxes.append(spin_box)

		vbox.add_child(row)

	dialog.add_child(vbox)
	dialog.confirmed.connect(func():
		var values := []
		for spin_box in spin_boxes:
			values.append(spin_box.value)
		on_confirm.call(values)
		dialog.queue_free()
	)
	dialog.canceled.connect(func(): dialog.queue_free())

	EditorInterface.get_base_control().add_child(dialog)
	dialog.popup_centered()


# Figures out where a generated loop/corkscrew should start: continuing from
# the existing curve's last point and direction if there is one, otherwise
# a sensible default at the node's local origin facing local -Z.
# Returns {"p0": Vector3, "t0": tangent, "u0": up, "r0": right}, an
# orthonormal basis with t0 as the "forward" axis.
func _curve_entry_basis(curve: Curve3D) -> Dictionary:
	var p0: Vector3
	var t0: Vector3

	if curve.point_count >= 2:
		p0 = curve.get_point_position(curve.point_count - 1)
		var prev: Vector3 = curve.get_point_position(curve.point_count - 2)
		var diff := p0 - prev
		t0 = diff.normalized() if diff.length() > 0.0001 else Vector3(0, 0, -1)
	elif curve.point_count == 1:
		p0 = curve.get_point_position(0)
		t0 = Vector3(0, 0, -1)
	else:
		p0 = Vector3.ZERO
		t0 = Vector3(0, 0, -1)

	var reference_up := Vector3.UP
	if abs(t0.dot(reference_up)) > 0.999:
		reference_up = Vector3.RIGHT # t0 is (nearly) vertical, need a different reference

	var u0 := (reference_up - t0 * t0.dot(reference_up)).normalized()
	var r0 := t0.cross(u0).normalized()

	return {"p0": p0, "t0": t0, "u0": u0, "r0": r0}


# Generates a vertical loop: a full circle in the plane spanned by the
# forward and up axes, rotating around the "right" axis. No net advancement
# along forward -- a real loop returns to the same forward position, just
# transiently moving forward/back and up/down along the way.
func _generate_loop_points(basis: Dictionary, radius: float, segments: int, side_drift: float) -> Array:
	var p0: Vector3 = basis["p0"]
	var t0: Vector3 = basis["t0"]
	var u0: Vector3 = basis["u0"]
	var r0: Vector3 = basis["r0"]

	var total_angle := TAU
	var angular_step := TAU / float(segments)
	var handle_length := (radius * angular_step) / 3.0

	var points := []
	var theta := angular_step
	while theta < total_angle + angular_step * 0.5:
		var t: float = min(theta, total_angle)

		var pos: Vector3 = p0 \
			+ u0 * radius * (1.0 - cos(t)) \
			+ t0 * radius * sin(t) \
			+ r0 * side_drift * (t / TAU)
		var tangent: Vector3 = (u0 * sin(t) + t0 * cos(t)) * radius + r0 * (side_drift / TAU)
		tangent = tangent.normalized()
		var desired_up: Vector3 = (u0 * cos(t) - t0 * sin(t)).normalized()

		points.append({
			"pos": pos,
			"tangent": tangent,
			"desired_up": desired_up,
			"handle_in": -tangent * handle_length,
			"handle_out": tangent * handle_length,
		})
		theta += angular_step

	return points


# Generates a corkscrew: a true helix that rotates around the forward axis
# while continuously advancing along it, like a screw thread.
func _generate_corkscrew_points(basis: Dictionary, radius: float, turns: float, pitch_per_turn: float, segments_per_turn: int) -> Array:
	var p0: Vector3 = basis["p0"]
	var t0: Vector3 = basis["t0"]
	var u0: Vector3 = basis["u0"]
	var r0: Vector3 = basis["r0"]

	var total_angle := TAU * turns
	var angular_step := TAU / float(segments_per_turn)
	var arc_len_per_step: float = sqrt(pow(radius * angular_step, 2) + pow(pitch_per_turn * angular_step / TAU, 2))
	var handle_length := arc_len_per_step / 3.0

	var points := []
	var theta := angular_step
	while theta < total_angle + angular_step * 0.5:
		var t: float = min(theta, total_angle)

		var pos: Vector3 = p0 \
			+ t0 * (pitch_per_turn * t / TAU) \
			+ u0 * radius * (1.0 - cos(t)) \
			+ r0 * radius * sin(t)
		var tangent: Vector3 = t0 * (pitch_per_turn / TAU) + (u0 * sin(t) + r0 * cos(t)) * radius
		tangent = tangent.normalized()
		var desired_up: Vector3 = (u0 * cos(t) - r0 * sin(t)).normalized()

		points.append({
			"pos": pos,
			"tangent": tangent,
			"desired_up": desired_up,
			"handle_in": -tangent * handle_length,
			"handle_out": tangent * handle_length,
		})
		theta += angular_step

	return points


# Appends the generated points to path3d's curve, computing and applying
# the corrective tilt at each one, then registers a single undo/redo action
# for the whole append. The actual curve mutation happens here directly
# (not inside the do method) because computing the correct tilt requires
# the points to already be baked into the curve so Godot's default up
# vectors can be queried and compared against.
func _append_generated_points(path3d: Path3D, generated: Array, action_name: String) -> void:
	if generated.is_empty():
		return

	var curve := path3d.curve
	if curve == null:
		curve = Curve3D.new()
		path3d.curve = curve

	curve.up_vector_enabled = true
	var original_count := curve.point_count

	# Pass 1: append with tilt left at 0, purely so Godot has something to
	# bake and we can ask it what its default (twist-minimizing) up vector
	# would be at each of these points.
	for point in generated:
		curve.add_point(point["pos"], point["handle_in"], point["handle_out"])
	curve.get_baked_length() # forces the bake

	# Pass 2: for each point, compare Godot's default up vector against the
	# geometrically correct one (pointing toward the loop/corkscrew's
	# rotation axis) and store the rotation needed to fix it as that
	# point's tilt.
	for i in range(generated.size()):
		var point_index := original_count + i
		var pos: Vector3 = generated[i]["pos"]
		var tangent: Vector3 = generated[i]["tangent"]
		var desired_up: Vector3 = generated[i]["desired_up"]

		var offset: float = curve.get_closest_offset(pos)
		var default_up: Vector3 = curve.sample_baked_up_vector(offset, false)

		var cos_angle: float = clamp(default_up.dot(desired_up), -1.0, 1.0)
		var angle: float = acos(cos_angle)
		var cross_vec: Vector3 = default_up.cross(desired_up)
		if cross_vec.dot(tangent) < 0.0:
			angle = -angle

		curve.set_point_tilt(point_index, angle)

	# Snapshot the final pos/in/out/tilt of just the new points, so undo/redo
	# can cheaply re-add or remove them without recomputing anything.
	var new_points := []
	for i in range(generated.size()):
		var point_index := original_count + i
		new_points.append({
			"pos": curve.get_point_position(point_index),
			"in": curve.get_point_in(point_index),
			"out": curve.get_point_out(point_index),
			"tilt": curve.get_point_tilt(point_index),
		})

	var ctx := {
		"curve": curve,
		"original_count": original_count,
		"new_points": new_points,
	}

	undo_redo.create_action(action_name, UndoRedo.MERGE_DISABLE, path3d)
	undo_redo.add_do_method(self, "_do_curve_append", ctx)
	undo_redo.add_undo_method(self, "_undo_curve_append", ctx)
	# execute=false: the append above already happened directly (it had to,
	# to compute tilt), this just registers it in the undo history.
	undo_redo.commit_action(false)


func _do_curve_append(ctx: Dictionary) -> void:
	var curve: Curve3D = ctx["curve"]
	if curve.point_count > ctx["original_count"]:
		return # already applied (e.g. the initial direct application above)
	for point in ctx["new_points"]:
		curve.add_point(point["pos"], point["in"], point["out"])
		curve.set_point_tilt(curve.point_count - 1, point["tilt"])


func _undo_curve_append(ctx: Dictionary) -> void:
	var curve: Curve3D = ctx["curve"]
	var original_count: int = ctx["original_count"]
	while curve.point_count > original_count:
		curve.remove_point(curve.point_count - 1)


func _prompt_generate_loop() -> void:
	_prompt_fields("Generate Loop", [
		{"label": "Radius (m):", "default": 5.0, "min": 0.5, "max": 100.0, "step": 0.1},
		{"label": "Segments:", "default": 24.0, "min": 8.0, "max": 128.0, "step": 1.0},
		{"label": "Side drift (m):", "default": 5.0, "min": -500.0, "max": 500.0, "step": 0.1},
	], _on_generate_loop_confirmed)


func _on_generate_loop_confirmed(values: Array) -> void:
	var radius: float = values[0]
	var segments: int = int(round(values[1]))
	var side_drift: float = values[2]

	var selected_nodes := EditorInterface.get_selection().get_selected_nodes()
	for selected_node in selected_nodes:
		if not (selected_node is Path3D):
			continue
		var path3d := selected_node as Path3D
		var basis := _curve_entry_basis(path3d.curve if path3d.curve else Curve3D.new())
		var generated := _generate_loop_points(basis, radius, segments, side_drift)
		_append_generated_points(path3d, generated, "Generate Loop")


func _prompt_generate_corkscrew() -> void:
	_prompt_fields("Generate Corkscrew", [
		{"label": "Radius (m):", "default": 3.0, "min": 0.5, "max": 100.0, "step": 0.1},
		{"label": "Turns:", "default": 2.0, "min": 0.1, "max": 20.0, "step": 0.1},
		{"label": "Length per turn (m):", "default": 10.0, "min": 0.5, "max": 200.0, "step": 0.1},
		{"label": "Segments per turn:", "default": 24.0, "min": 8.0, "max": 128.0, "step": 1.0},
	], _on_generate_corkscrew_confirmed)


func _on_generate_corkscrew_confirmed(values: Array) -> void:
	var radius: float = values[0]
	var turns: float = values[1]
	var pitch_per_turn: float = values[2]
	var segments_per_turn: int = int(round(values[3]))

	var selected_nodes := EditorInterface.get_selection().get_selected_nodes()
	for selected_node in selected_nodes:
		if not (selected_node is Path3D):
			continue
		var path3d := selected_node as Path3D
		var basis := _curve_entry_basis(path3d.curve if path3d.curve else Curve3D.new())
		var generated := _generate_corkscrew_points(basis, radius, turns, pitch_per_turn, segments_per_turn)
		_append_generated_points(path3d, generated, "Generate Corkscrew")
