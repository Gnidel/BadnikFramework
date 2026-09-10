@tool
class_name BadnikFrameworkExtras
extends EditorPlugin

const SETTING_GENERATE_RAIL_MESH := "badnik_framework_extras/generate_rail_mesh/enabled"
const SETTING_GENERATE_RAIL_MESH_COLOR := "badnik_framework_extras/generate_rail_mesh/color"

# Left untyped on purpose: the menu script attaches its own extra property
# (undo_redo) that MenuButton doesn't have, and a statically-typed
# "MenuButton" variable would make that assignment a compile error.
var menu: Variant


func _enter_tree() -> void:
	var script_dir: String = get_script().resource_path.get_base_dir()
	var menu_script: Variant = load(script_dir.path_join("path_3d_menu.gd"))
	menu = menu_script.new()
	menu.undo_redo = get_undo_redo()
	add_control_to_container(CustomControlContainer.CONTAINER_SPATIAL_EDITOR_MENU, menu)

	_register_settings()


func _exit_tree() -> void:
	remove_control_from_container(CustomControlContainer.CONTAINER_SPATIAL_EDITOR_MENU, menu)
	menu.queue_free()


func _register_settings() -> void:
	var editor_settings := EditorInterface.get_editor_settings()

	_ensure_setting(
		editor_settings,
		SETTING_GENERATE_RAIL_MESH,
		true,
		TYPE_BOOL,
		"Generate rail mesh"
	)
	_ensure_setting(
		editor_settings,
		SETTING_GENERATE_RAIL_MESH_COLOR,
		Color(1, 0, 0, 1),
		TYPE_COLOR,
		"Generate rail mesh color"
	)


# Creates the setting with its default value if missing, and also resets it
# to the default if it exists but holds a value of the wrong type (e.g. from
# a corrupted editor config or a stale value left by a previous plugin
# version). Either way, add_property_info is (re-)applied so the settings UI
# always shows the right widget for it.
func _ensure_setting(
	editor_settings: EditorSettings,
	setting_name: String,
	default_value: Variant,
	expected_type: int,
	hint_string: String
) -> void:
	var needs_reset := not editor_settings.has_setting(setting_name)

	if not needs_reset:
		var current_value: Variant = editor_settings.get_setting(setting_name)
		if typeof(current_value) != expected_type:
			push_warning(
				"BadnikFrameworkExtras: setting '%s' had an unexpected type, resetting to default."
				% setting_name
			)
			needs_reset = true

	if needs_reset:
		editor_settings.set_setting(setting_name, default_value)

	editor_settings.add_property_info({
		"name": setting_name,
		"type": expected_type,
		"hint": PROPERTY_HINT_NONE,
		"hint_string": hint_string,
	})
