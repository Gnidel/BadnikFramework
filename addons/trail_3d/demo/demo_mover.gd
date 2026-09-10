extends Node3D
## Quick test rig: attach this to a Node3D that has a Trail3D child (plus
## some visible mesh, e.g. a MeshInstance3D box) to see the trail in action.
## Moves the node in a swooping figure-eight so you can watch the ribbon
## curve and taper.

@export var radius: float = 4.0
@export var speed: float = 1.5
@export var bob_height: float = 0.5

var _t: float = 0.0


func _process(delta: float) -> void:
	_t += delta * speed
	position = Vector3(
		sin(_t) * radius,
		sin(_t * 2.0) * bob_height,
		sin(_t * 2.0) * radius * 0.5
	)
	look_at(position + Vector3(cos(_t) * radius, 0.0, cos(_t * 2.0) * radius * 0.5), Vector3.UP)
