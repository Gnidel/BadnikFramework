shader_type spatial;
render_mode unshaded, cull_disabled, depth_test_disabled;

// uniforms must keep in sync with surface water shader above
uniform float sea_height    : hint_range(0.0, 5.0) = 1.3;
uniform float sea_choppy    : hint_range(0.0, 10.0) = 4.0;
uniform float sea_speed     : hint_range(0.0, 5.0)  = 1.5;
uniform float sea_freq      : hint_range(0.0, 0.5)  = 0.08;
uniform float height_offset = 0.0;

// underwater look
uniform vec3  underwater_color     : source_color = vec3(0.02, 0.18, 0.25);
uniform float absorption_strength  : hint_range(0.0, 2.0) = 0.35;   // how fast color gets lost with distance
uniform float deep_tint_strength   : hint_range(0.0, 1.0) = 0.65;   // how strong the final deep color is

// gentle screen wobble
uniform float refraction_amount    : hint_range(0.0, 0.015) = 0.004;
uniform float refraction_speed     : hint_range(0.0, 3.0)   = 0.7;

// slight depth fudge to avoid seeing through near the surface
uniform float water_level_fudge    : hint_range(-1.5, 1.5) = 0.0;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;
uniform sampler2D DEPTH_TEXTURE  : hint_depth_texture,  filter_linear_mipmap;

const int ITER_GEOMETRY = 3;
const mat2 octave_m = mat2(vec2(1.6,1.2), vec2(-1.2,1.6));

float hash12(vec2 p) {
    uvec2 q = uvec2(ivec2(p)) * uvec2(1597334677u, 3812015801u);
    uint n = (q.x ^ q.y) * 1597334677u;
    return float(n) * (1.0 / 4294967295.0);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return -1.0 + 2.0 * mix(
        mix(hash12(i + vec2(0.0,0.0)), hash12(i + vec2(1.0,0.0)), u.x),
        mix(hash12(i + vec2(0.0,1.0)), hash12(i + vec2(1.0,1.0)), u.x), u.y);
}

float sea_octave(vec2 uv, float choppy) {
    uv += noise(uv);
    vec2 wv = 1.0 - abs(sin(uv));
    vec2 swv = abs(cos(uv));
    wv = mix(wv, swv, wv);
    return pow(1.0 - pow(wv.x * wv.y, 0.65), choppy);
}

// world position from vertex shader
varying vec3 world_pos;

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    // calculate local wave height at this fragment position
    vec2 uv = world_pos.xz;
    uv.x *= 0.75;

    float freq = sea_freq;
    float amp  = sea_height;
    float chop = sea_choppy;
    float height = 0.0;

    for (int i = 0; i < ITER_GEOMETRY; i++) {
        float d = sea_octave((uv + TIME * sea_speed)     * freq, chop);
        d +=      sea_octave((uv - TIME * sea_speed)     * freq, chop);
        height += d * amp;
        uv     *= octave_m;
        freq   *= 1.9;
        amp    *= 0.22;
        chop   = mix(chop, 1.0, 0.2);
    }

    float surface_y = height + height_offset + water_level_fudge;

    // if we're above the water, discard this pixel
    if (world_pos.y > surface_y) {
        discard;
    }

    // simple animated screen-space refraction/wobble
    vec2 uv_screen = SCREEN_UV;

    // two directional noise offsets for gentle organic movement
    vec2 noise_dir = vec2(
        noise(uv_screen * 12.0 + TIME * refraction_speed * vec2(1.0, 0.4)),
        noise(uv_screen * 12.0 + TIME * refraction_speed * vec2(0.6, 1.0))
    ) * 2.0 - 1.0;

    vec2 offset = noise_dir * refraction_amount;
    vec2 uv_distorted = uv_screen + offset;

    // sample scene color with distortion
    vec3 scene_color = texture(SCREEN_TEXTURE, uv_distorted).rgb;

    // get approximate distance through water
    // we use the distorted UV to read depth
    float depth_raw = texture(DEPTH_TEXTURE, uv_distorted).x;

    vec3 ndc = vec3(uv_distorted * 2.0 - 1.0, depth_raw);
    #if CURRENT_RENDERER == RENDERER_COMPATIBILITY
    ndc.z = depth_raw * 2.0 - 1.0;
    #endif

    vec4 view_pos = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
    view_pos.xyz /= view_pos.w;
    float scene_view_z = -view_pos.z;

    // approximate water thickness, positive means deeper
    float thickness = scene_view_z - (-VIEW_MATRIX[3].z - world_pos.y); // rough but good enough

    thickness = max(thickness, 0.0);

    // beer-lambert absorption + deep color tint
    // how much light is left after traveling through water
    float transmittance = exp(-thickness * absorption_strength);

    // blend toward deep underwater color
    vec3 tinted = mix(scene_color, underwater_color, deep_tint_strength);

    // applying absorption, darkens & tints farther objects
    vec3 final_color = tinted * transmittance;

    // very simple fog falloff to pure underwater color in extreme distance
    float fog = 1.0 - exp(-thickness * absorption_strength * 0.7);
    final_color = mix(final_color, underwater_color, fog * 0.85);

    ALBEDO = final_color;
}