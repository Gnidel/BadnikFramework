@tool
class_name Trail3D
extends MeshInstance3D

## Trail3D
##
## Drop this node anywhere in your scene (usually as a child of whatever
## should leave a trail — a hoverboard, a sword, a spaceship...) and it will
## draw a ribbon behind it as it moves. No setup required beyond adding the
## node; everything else is optional cosmetic tuning.
##
## How it works: every frame (while `emitting` is true) the node records its
## own world position if it has moved further than `min_vertex_distance`
## since the last recorded point. Old points are discarded once they are
## older than `lifetime` seconds. Each frame the whole trail is rebuilt into
## a ribbon mesh, with color/alpha/width per point driven by curves sampled
## by the point's age (0.0 = brand new / head of the trail, 1.0 = about to
## disappear / tail of the trail).
##
## The ribbon does NOT get its width direction from a fixed "up" vector.
## Instead each vertex pair sits at the same position and a small built-in
## shader pushes them apart sideways relative to whichever camera is
## currently rendering (using the CAMERA_POSITION_WORLD shader built-in).
## This is resolved per draw call by the renderer, so it's automatically
## correct for every camera/viewport at once — including local split-screen
## multiplayer — without any script-side camera lookups.

## Whether the trail is currently recording new points. Turn this off to
## freeze / stop the trail (existing points will still fade out and expire).
@export var emitting: bool = true

## How long (in seconds) a point stays part of the trail before it is
## removed. Bigger = longer trail (given constant movement speed).
@export_range(0.05, 10.0, 0.01, "suffix:s") var lifetime: float = 1.0

## Minimum distance the node must travel before a new point is recorded.
## Lower values = smoother trail but more geometry.
@export_range(0.0, 2.0, 0.005, "suffix:m") var min_vertex_distance: float = 0.05

## Safety cap on how many points can exist at once, regardless of lifetime.
@export_range(2, 1024, 1) var max_points: int = 256

@export_group("Shape")
## Width multiplier over the life of the trail. X axis = age (0 = head,
## 1 = tail), Y axis = multiplier applied to base_width. Leave empty for a
## constant width. A typical trail curve starts high near 0 and tapers to 0
## near 1.
@export var width_curve: Curve
## Base width of the ribbon in meters (multiplied by width_curve if set).
@export_range(0.0, 20.0, 0.01, "suffix:m") var base_width: float = 0.5
## Only used as a fallback when the camera is looking straight down the
## trail's direction (a genuine camera-facing offset is undefined in that
## exact case). You'll basically never need to touch this.
@export var fallback_up: Vector3 = Vector3.UP

@export_group("Look")
## Optional texture stretched along the length of the trail. UV.x runs
## along the trail (0 = head, tiled by texture_tiling), UV.y runs across
## the width (0/1 at the two edges). If left empty, a plain white 1x1
## texture is used so color_gradient/alpha_curve alone still work.
@export var texture: Texture2D
## Color over the life of the trail, sampled by age (0 = head, 1 = tail).
## The gradient's own alpha is combined with alpha_curve below.
@export var color_gradient: Gradient
## Additional alpha multiplier over the life of the trail, sampled by age.
## Handy for a clean fade-out independent of the color gradient. Leave
## empty to just use the gradient's alpha.
@export var alpha_curve: Curve
## How many times the texture repeats along the trail's length.
@export_range(0.1, 32.0, 0.1) var texture_tiling: float = 1.0
@export var unshaded: bool = true:
	set(value):
		unshaded = value
		_dirty_shader = true
@export var double_sided: bool = true:
	set(value):
		double_sided = value
		_dirty_shader = true

var _points: Array[Dictionary] = []
var _material: ShaderMaterial
var _dirty_shader: bool = true
var _white_texture: ImageTexture


func _ready() -> void:
	# A previous version of this script set top_level = true at runtime,
	# which — because this is a @tool script running in the editor — got
	# saved as a property override into your scene file. Force it back off
	# here so the node reliably inherits its parent's transform again,
	# regardless of what's saved on disk.
	top_level = false
	mesh = ArrayMesh.new()
	_ensure_material()


func _process(delta: float) -> void:
	_age_points(delta)
	if emitting:
		_try_add_point()
	_rebuild_mesh()


func _try_add_point() -> void:
	var pos: Vector3 = global_transform.origin
	if _points.is_empty() or pos.distance_to(_points[-1]["pos"]) >= min_vertex_distance:
		_points.append({"pos": pos, "age": 0.0})
		if _points.size() > max_points:
			_points.pop_front()


func _age_points(delta: float) -> void:
	for p in _points:
		p["age"] += delta
	while _points.size() > 0 and _points[0]["age"] > lifetime:
		_points.pop_front()


func _rebuild_mesh() -> void:
	if not (mesh is ArrayMesh):
		mesh = ArrayMesh.new()
	var am := mesh as ArrayMesh
	am.clear_surfaces()

	var count := _points.size()
	if count < 2:
		return

	var verts := PackedVector3Array()
	var normals := PackedVector3Array()
	var uvs := PackedVector2Array()
	var colors := PackedColorArray()
	var indices := PackedInt32Array()

	# Convert the whole trail's world-space history into this node's local
	# space *once per rebuild*, using the current global_transform. Since
	# this happens fresh every frame, it automatically compensates for
	# however the node (and its parents) have moved since the point was
	# recorded — this is what makes the trail follow the parent correctly.
	var local_pos := PackedVector3Array()
	local_pos.resize(count)
	for i in range(count):
		local_pos[i] = to_local(_points[i]["pos"])

	for i in range(count):
		var p: Dictionary = _points[i]
		var pos: Vector3 = local_pos[i]
		var age_fraction: float = clamp(p["age"] / maxf(lifetime, 0.0001), 0.0, 1.0)

		# Tangent along the trail (in local space); the shader uses this +
		# the current camera's position to figure out which way is
		# "sideways", so the ribbon always faces the camera edge-on.
		var tangent: Vector3
		if i == 0:
			tangent = local_pos[i + 1] - pos
		elif i == count - 1:
			tangent = pos - local_pos[i - 1]
		else:
			tangent = local_pos[i + 1] - local_pos[i - 1]
		if tangent.length_squared() < 0.0000001:
			tangent = Vector3.FORWARD
		tangent = tangent.normalized()

		var w := base_width
		if width_curve:
			w *= width_curve.sample(age_fraction)
		var half := w * 0.5

		# Both vertices of this cross-section start at the exact same
		# local position. The shader pushes them apart sideways, facing
		# whichever camera is currently drawing this mesh.
		verts.append(pos)
		verts.append(pos)
		normals.append(tangent)
		normals.append(tangent)

		var u := (float(i) / float(maxi(count - 1, 1))) * texture_tiling
		# UV.y temporarily carries the *signed* half-width (not a texture
		# coordinate yet) — the shader consumes it to offset the vertex,
		# then rewrites UV.y to a proper 0/1 texture coordinate.
		uvs.append(Vector2(u, half))
		uvs.append(Vector2(u, -half))

		var col := Color(1.0, 1.0, 1.0, 1.0)
		if color_gradient:
			col = color_gradient.sample(age_fraction)
		if alpha_curve:
			col.a *= alpha_curve.sample(age_fraction)
		colors.append(col)
		colors.append(col)

	for i in range(count - 1):
		var a := i * 2
		var b := i * 2 + 1
		var c := (i + 1) * 2
		var d := (i + 1) * 2 + 1
		indices.append(a)
		indices.append(b)
		indices.append(c)
		indices.append(b)
		indices.append(d)
		indices.append(c)

	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = verts
	arrays[Mesh.ARRAY_NORMAL] = normals
	arrays[Mesh.ARRAY_TEX_UV] = uvs
	arrays[Mesh.ARRAY_COLOR] = colors
	arrays[Mesh.ARRAY_INDEX] = indices

	am.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)

	_ensure_material()
	am.surface_set_material(0, _material)


func _ensure_material() -> void:
	if not _material:
		_material = ShaderMaterial.new()
		_material.shader = Shader.new()
		_dirty_shader = true
	if _dirty_shader:
		_material.shader.code = _build_shader_code()
		_dirty_shader = false
	_material.set_shader_parameter("albedo_texture", texture if texture else _get_white_texture())
	_material.set_shader_parameter("fallback_up", fallback_up)


func _build_shader_code() -> String:
	var render_modes := ["blend_mix", "depth_draw_opaque"]
	render_modes.append("unshaded" if unshaded else "diffuse_lambert")
	render_modes.append("cull_disabled" if double_sided else "cull_back")
	var modes_str := ", ".join(render_modes)
	return """
shader_type spatial;
render_mode %s;

uniform sampler2D albedo_texture : source_color, filter_linear, repeat_enable;
uniform vec3 fallback_up = vec3(0.0, 1.0, 0.0);

void vertex() {
	// VERTEX/NORMAL are in local (model) space here, same as any normal
	// mesh — this keeps the standard transform/culling pipeline intact,
	// so parenting and frustum culling both just work as usual.
	vec3 tangent_dir = normalize(NORMAL);

	// Bring the current camera's position into this node's local space so
	// the "face the camera" math works without ever touching a specific
	// camera in script. This is evaluated per draw call by the renderer,
	// so it's automatically correct for every viewport/camera at once —
	// including local split-screen multiplayer.
	vec3 cam_local = (inverse(MODEL_MATRIX) * vec4(CAMERA_POSITION_WORLD, 1.0)).xyz;

	vec3 to_camera = cam_local - VERTEX;
	vec3 view_dir = (length(to_camera) > 0.0001) ? normalize(to_camera) : fallback_up;

	vec3 side = cross(tangent_dir, view_dir);
	if (length(side) < 0.0001) {
		side = cross(tangent_dir, fallback_up);
	}
	side = normalize(side);

	float half_width_signed = UV.y;
	VERTEX += side * half_width_signed;
	UV.y = half_width_signed >= 0.0 ? 1.0 : 0.0;
}

void fragment() {
	vec4 tex_color = texture(albedo_texture, UV);
	ALBEDO = tex_color.rgb * COLOR.rgb;
	ALPHA = tex_color.a * COLOR.a;
}
""" % modes_str


func _get_white_texture() -> Texture2D:
	if not _white_texture:
		var img := Image.create(1, 1, false, Image.FORMAT_RGBA8)
		img.fill(Color(1.0, 1.0, 1.0, 1.0))
		_white_texture = ImageTexture.create_from_image(img)
	return _white_texture


## Instantly removes all recorded points (the trail disappears immediately
## instead of fading out). Useful when teleporting the emitter.
func clear_trail() -> void:
	_points.clear()
