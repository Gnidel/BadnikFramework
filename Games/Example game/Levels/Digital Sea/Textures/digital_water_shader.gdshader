// ============================================================
//  Water Shader for Godot 4 — Compatibility Renderer
//  World-position based — works on any large flat plane
//  ---- PERFORMANCE-OPTIMIZED VERSION ----
//
//  Changes from the original (see comments marked "OPT:"):
//   • Ripple normals now use dFdx/dFdy instead of 3x manual
//     epsilon-offset fbm() calls -> ~66% fewer noise evaluations
//   • fbm() takes an octave count; caustics/foam use 2 octaves
//     instead of 4 (ripples still use 3)
//   • Removed inverse(MODEL_MATRIX) per-vertex (assumes the water
//     mesh is not rotated / non-uniformly scaled — only translated,
//     which is the normal case for a flat water plane). Wave
//     displacement is added directly to local VERTEX instead.
//   • Dropped from 4 Gerstner waves to 3 (the 4th contributed only
//     ~30% amplitude and was the least visually significant)
//
//  If your water plane actually IS rotated or non-uniformly scaled
//  in the scene tree, see the note above vertex() before using this.
// ============================================================

shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, diffuse_lambert, specular_schlick_ggx;

// ── Depth & Visibility ──────────────────────────────────────
uniform sampler2D DEPTH_TEXTURE : hint_depth_texture, filter_linear_mipmap;
// NOTE: on the Mobile renderer, hint_depth_texture reads back as 0
// everywhere when MSAA is enabled (open Godot engine bug as of 4.4).
// If you hit that on a target device, either disable MSAA for that
// platform or set this to false from a platform-detection script to
// fall back to a fixed "always deep water" look instead of broken
// depth-based effects.
uniform bool  depth_fade_enabled = true;
uniform float depth_fade_start  : hint_range(0.0, 5.0)   = 0.4;
uniform float depth_fade_end    : hint_range(0.5, 30.0)  = 6.0;
uniform vec4  shallow_color     : source_color             = vec4(0.05, 0.45, 0.55, 0.55);
uniform vec4  deep_color        : source_color             = vec4(0.01, 0.10, 0.25, 0.97);

// ── Waves ───────────────────────────────────────────────────
uniform float wave_speed        : hint_range(0.0, 4.0)   = 0.9;
uniform float wave_height       : hint_range(0.0, 3.0)   = 0.55;
uniform float wave_steepness    : hint_range(0.0, 1.0)   = 0.45;
uniform float wave_length       : hint_range(1.0, 60.0)  = 12.0;
uniform float shore_depth_min   : hint_range(0.0, 5.0)   = 0.3;
uniform float shore_depth_max   : hint_range(0.5, 10.0)  = 2.5;

// ── World Scroll ─────────────────────────────────────────────
uniform vec2  scroll_direction   = vec2(0.0, 1.0);
uniform float scroll_speed       : hint_range(0.0, 20.0) = 0.0;
uniform vec2  scroll_offset      = vec2(0.0, 0.0);

// ── Surface Detail ──────────────────────────────────────────
uniform float ripple_scale      : hint_range(0.5, 20.0)  = 6.0;
uniform float ripple_strength   : hint_range(0.0, 1.0)   = 0.35;
uniform float ripple_speed      : hint_range(0.0, 3.0)   = 0.5;

// ── Foam ────────────────────────────────────────────────────
uniform vec4  foam_color        : source_color             = vec4(0.92, 0.96, 1.0, 1.0);
uniform float foam_shore_depth  : hint_range(0.0, 3.0)   = 0.6;
uniform float foam_crest_thresh : hint_range(0.0, 1.0)   = 0.6;
uniform float foam_softness     : hint_range(0.01, 1.0)  = 0.25;

// ── Reflection / Fresnel ────────────────────────────────────
uniform vec4  sky_color         : source_color             = vec4(0.45, 0.72, 0.90, 1.0);
uniform float fresnel_power     : hint_range(1.0, 10.0)  = 4.5;
uniform float reflectivity      : hint_range(0.0, 1.0)   = 0.55;

// ── Specular / Sun ──────────────────────────────────────────
uniform float roughness         : hint_range(0.0, 1.0)   = 0.06;
uniform float specular          : hint_range(0.0, 1.0)   = 0.9;

// ── Sub-surface Scattering (fake) ───────────────────────────
uniform vec4  sss_color         : source_color             = vec4(0.0, 0.85, 0.65, 1.0);
uniform float sss_strength      : hint_range(0.0, 1.0)   = 0.35;

// ── Caustics ────────────────────────────────────────────────
uniform float caustic_scale     : hint_range(1.0, 30.0)  = 8.0;
uniform float caustic_strength  : hint_range(0.0, 1.0)   = 0.18;
uniform float caustic_speed     : hint_range(0.0, 2.0)   = 0.4;

// ============================================================
//  Utility: smooth hash (no textures needed)
// ============================================================
float hash(vec2 p) {
    p = fract(p * vec2(127.1, 311.7));
    p += dot(p, p + 19.19);
    return fract(p.x * p.y);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(
        mix(hash(i + vec2(0,0)), hash(i + vec2(1,0)), u.x),
        mix(hash(i + vec2(0,1)), hash(i + vec2(1,1)), u.x),
        u.y);
}

// OPT: octave count is now a parameter. Cheap effects (caustics, foam
// edge noise) use 2 octaves; ripples use 3. Original always used 4.
float fbm(vec2 p, int octaves) {
    float v = 0.0, a = 0.5;
    for (int i = 0; i < octaves; i++) {
        v += a * noise(p);
        p  = p * 2.1 + vec2(1.3, 0.7);
        a *= 0.5;
    }
    return v;
}

vec2 get_world_scroll() {
    return scroll_direction * scroll_speed * TIME + scroll_offset;
}

// ============================================================
//  Gerstner Wave
// ============================================================
void gerstner_wave(
    vec2  xz,
    vec2  dir,
    float wavelength,
    float steepness,
    float amplitude,
    float speed,
    float t,
    inout vec3 position,
    inout vec3 tangent,
    inout vec3 binormal)
{
    float k  = TAU / wavelength;
    float c  = sqrt(9.8 / k);
    float f  = k * (dot(dir, xz) - c * speed * t);
    float Q  = steepness / (k * amplitude + 0.001);

    position.x += Q * amplitude * dir.x * cos(f);
    position.z += Q * amplitude * dir.y * cos(f);
    position.y +=     amplitude          * sin(f);

    float WA = k * amplitude;
    tangent  += vec3(-Q * WA * dir.x * dir.x * sin(f),
                      WA * dir.x * cos(f),
                     -Q * WA * dir.x * dir.y * sin(f));
    binormal += vec3(-Q * WA * dir.x * dir.y * sin(f),
                      WA * dir.y * cos(f),
                     -Q * WA * dir.y * dir.y * sin(f));
}

// ============================================================
//  VERTEX
// ============================================================
varying vec3  v_world_pos;
varying float v_wave_height_n;

// OPT: This whole shader assumes the water mesh node is only
// TRANSLATED in the scene (no rotation / non-uniform scale). That's
// true for the vast majority of flat water planes. Under that
// assumption, a world-space displacement is identical to the same
// displacement applied in local space, so we can add it straight to
// VERTEX and skip computing inverse(MODEL_MATRIX) on every vertex.
// If your plane IS rotated/scaled, revert to transforming through
// inverse(MODEL_MATRIX) as in the original.
void vertex() {
    vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    float t    = TIME * wave_speed;

    vec2 sample_xz = world.xz + get_world_scroll();

    vec3 pos     = world;
    vec3 tangent = vec3(1, 0, 0);
    vec3 binorm  = vec3(0, 0, 1);

    float amp = wave_height;
    float wl  = wave_length;

    // OPT: dropped the 4th (smallest, ~30% amplitude) wave — its
    // contribution was barely visible relative to its cost.
    gerstner_wave(sample_xz, normalize(vec2( 1.0,  0.3)), wl * 1.00, wave_steepness, amp * 1.00, 1.00, t, pos, tangent, binorm);
    gerstner_wave(sample_xz, normalize(vec2(-0.6,  1.0)), wl * 0.73, wave_steepness, amp * 0.65, 1.05, t, pos, tangent, binorm);
    gerstner_wave(sample_xz, normalize(vec2( 0.3, -1.0)), wl * 0.51, wave_steepness, amp * 0.45, 0.95, t, pos, tangent, binorm);

    v_wave_height_n = clamp((pos.y - world.y) / (wave_height + 0.001), 0.0, 1.0);
    v_world_pos = pos;

    // OPT: add displacement directly instead of round-tripping
    // through inverse(MODEL_MATRIX). displacement == pos - world.
    VERTEX += (pos - world);

    vec3 N  = normalize(cross(binorm, tangent));
    TANGENT = normalize((MODEL_MATRIX * vec4(tangent, 0.0)).xyz);
    BINORMAL= normalize((MODEL_MATRIX * vec4(binorm,  0.0)).xyz);
    NORMAL  = normalize((MODEL_MATRIX * vec4(N,       0.0)).xyz);
}

// ============================================================
//  FRAGMENT
// ============================================================
void fragment() {
    float t = TIME;

    // ── Reconstruct scene depth → water column depth ──────
    float water_depth;
    if (depth_fade_enabled) {
        float raw_depth   = texture(DEPTH_TEXTURE, SCREEN_UV).r;
        vec4  ndc_behind  = vec4(SCREEN_UV * 2.0 - 1.0, raw_depth, 1.0);
        vec4  view_behind = INV_PROJECTION_MATRIX * ndc_behind;
        float scene_z     = -view_behind.z / view_behind.w;
        float surf_z      = -VERTEX.z;
        water_depth = max(0.0, scene_z - surf_z);
    } else {
        // Depth texture unavailable/unreliable on this platform (e.g.
        // Mobile renderer + MSAA) — treat water as uniformly deep so
        // shore/foam/caustic logic degrades gracefully instead of
        // reacting to a broken all-zero depth read.
        water_depth = depth_fade_end;
    }

    float depth_t = smoothstep(depth_fade_start, depth_fade_end, water_depth);
    float shore_t = smoothstep(shore_depth_min, shore_depth_max, water_depth);

    vec2 sample_xz = v_world_pos.xz + get_world_scroll();

    // ── Surface ripple normals (scrolled world-XZ) ────────
    // OPT: was 3 fbm() calls per layer (center + 2 epsilon offsets)
    // to fake a derivative by hand. dFdx/dFdy give the screen-space
    // derivative of a value that's already being computed anyway,
    // so each layer now costs a single fbm() call.
    vec2 uv1 = sample_xz * ripple_scale * 0.07 + vec2( 1.0,  0.6) * t * ripple_speed;
    vec2 uv2 = sample_xz * ripple_scale * 0.11 + vec2(-0.7,  1.0) * t * ripple_speed * 0.8;

    float h1 = fbm(uv1, 3);
    float h2 = fbm(uv2, 3);

    float fdx = dFdx(h1);
    float fdy = dFdy(h1);
    float fdx2 = dFdx(h2);
    float fdy2 = dFdy(h2);

    vec3 ripple_n = normalize(vec3(
        -(fdx * 0.6 + fdx2 * 0.4) * ripple_strength,
        1.0,
        -(fdy * 0.6 + fdy2 * 0.4) * ripple_strength
    ));

    vec3 N = normalize(
        TANGENT  * ripple_n.x +
        BINORMAL * ripple_n.z +
        NORMAL   * ripple_n.y
    );
    NORMAL = N;

    // ── Fresnel ───────────────────────────────────────────
    vec3  view_dir = normalize(-VERTEX);
    float NdotV    = max(dot(N, view_dir), 0.0);
    float fresnel  = pow(1.0 - NdotV, fresnel_power);

    // ── Water base color (shallow→deep) ──────────────────
    vec4 water_col = mix(shallow_color, deep_color, depth_t);

    // ── Sub-surface scattering at crests ─────────────────
    float sss_mask = v_wave_height_n * shore_t;
    water_col.rgb  = mix(water_col.rgb, sss_color.rgb, sss_mask * sss_strength);

    // ── Caustics (cheap: animated FBM shimmer) ────────────
    // OPT: dropped from 4 octaves to 2 per sample — this is a small
    // shimmer effect, high-frequency detail isn't noticeable here.
    vec2 caust_uv1 = sample_xz / caustic_scale + vec2( 0.8,  0.3) * t * caustic_speed;
    vec2 caust_uv2 = sample_xz / caustic_scale + vec2(-0.4, -0.7) * t * caustic_speed * 1.3;
    float caustic  = abs(fbm(caust_uv1, 2) - fbm(caust_uv2, 2));
    caustic        = 1.0 - smoothstep(0.0, 0.25, caustic);
    caustic       *= (1.0 - depth_t) * (1.0 - fresnel) * caustic_strength;
    water_col.rgb += caustic;

    // ── Foam ─────────────────────────────────────────────
    float shore_foam = 1.0 - smoothstep(0.0, foam_shore_depth, water_depth);
    float crest_foam = smoothstep(foam_crest_thresh - foam_softness,
                                  foam_crest_thresh + foam_softness,
                                  v_wave_height_n) * shore_t;
    // OPT: 2 octaves instead of 4 — this only breaks up a mask edge.
    float foam_noise = fbm(sample_xz * 3.5 + t * 0.3, 2);
    float foam_mask  = clamp(shore_foam + crest_foam, 0.0, 1.0);
    foam_mask        = smoothstep(0.3, 0.7, foam_mask * (0.5 + 0.5 * foam_noise));

    water_col.rgb = mix(water_col.rgb, foam_color.rgb, foam_mask);
    water_col.a   = mix(water_col.a,   foam_color.a,   foam_mask);

    // ── Reflection contribution ───────────────────────────
    vec3 refl_col = sky_color.rgb * reflectivity;
    water_col.rgb = mix(water_col.rgb, refl_col, fresnel * reflectivity);

    // ── Opacity ────────────────────────────────────────────
    float alpha = mix(water_col.a, 1.0, depth_t);
    alpha = max(alpha, foam_mask);

    // ── Output ───────────────────────────────────────────
    ALBEDO     = water_col.rgb;
    ALPHA      = alpha;
    ROUGHNESS  = roughness * (1.0 - foam_mask * 0.8);
    SPECULAR   = specular  * (1.0 - foam_mask * 0.5);
    METALLIC   = 0.0;
}
