@tool
extends EditorPlugin

func _enter_tree() -> void:
	add_custom_type(
		"Trail3D",
		"MeshInstance3D",
		preload("res://addons/trail_3d/trail_3d.gd"),
		preload("res://addons/trail_3d/icon.svg")
	)


func _exit_tree() -> void:
	remove_custom_type("Trail3D")
