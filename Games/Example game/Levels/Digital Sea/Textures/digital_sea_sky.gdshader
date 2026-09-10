shader_type sky;

// =====================================================================
//  DIGITAL SEA — Sky Shader
// =====================================================================

// ── Uniforms (no group_uniforms — not supported in sky shaders) ───────

uniform float time_scale        : hint_range(0.0, 4.0,   0.01) = 1.0;

uniform vec3  sun_direction                                      = vec3(0.0, -0.35, -1.0);
uniform vec3  sun_color         : source_color                   = vec3(1.0, 0.82, 0.35);
uniform vec3  sun_edge_color    : source_color                   = vec3(1.0, 0.28, 0.05);
uniform float sun_radius        : hint_range(0.01, 0.25, 0.001) = 0.055;
uniform float sun_glow_radius   : hint_range(0.05, 0.8,  0.01)  = 0.38;
uniform float sun_glow_power    : hint_range(0.5,  6.0,  0.1)   = 2.2;
uniform float scanline_count    : hint_range(2.0,  60.0, 1.0)   = 26.0;
uniform float scanline_speed    : hint_range(0.0,  4.0,  0.1)   = 1.2;
uniform float scanline_opacity  : hint_range(0.0,  1.0,  0.01)  = 0.22;

uniform vec3  color_zenith      : source_color = vec3(0.02, 0.04, 0.16);
uniform vec3  color_mid         : source_color = vec3(0.06, 0.32, 0.52);
uniform vec3  color_horizon     : source_color = vec3(0.12, 0.65, 0.60);
uniform vec3  color_sunset      : source_color = vec3(1.00, 0.56, 0.18);
uniform vec3  color_sunbase     : source_color = vec3(1.00, 0.22, 0.08);

uniform float grid_opacity      : hint_range(0.0, 1.0,  0.01)  = 0.55;
uniform float grid_cell_size    : hint_range(0.01, 0.4, 0.001) = 0.07;
uniform float grid_speed        : hint_range(-2.0, 2.0,  0.01)  = 0.35;
uniform vec3  grid_color        : source_color                  = vec3(0.0, 1.0, 0.76);
uniform float grid_horizon_y    : hint_range(-0.5, 0.5, 0.01)  = -0.08;

uniform float rain_opacity      : hint_range(0.0, 1.0,  0.01) = 0.45;
uniform float rain_speed        : hint_range(0.0, 6.0,  0.1)  = 2.8;
uniform float rain_columns      : hint_range(4.0, 64.0, 1.0)  = 28.0;
uniform vec3  rain_color        : source_color                  = vec3(0.0, 1.0, 0.72);

uniform float cloud_opacity     : hint_range(0.0, 1.0, 0.01) = 0.68;
uniform float cloud_speed       : hint_range(0.0, 1.0, 0.01) = 0.06;
uniform float cloud_pixel_edge  : hint_range(0.0, 1.0, 0.01) = 0.30;

uniform float star_count        : hint_range(0.0, 1.0, 0.01) = 0.75;
uniform float star_twinkle      : hint_range(0.0, 1.0, 0.01) = 0.55;

uniform float vignette_strength : hint_range(0.0, 1.0, 0.01) = 0.38;

// ── Helpers ───────────────────────────────────────────────────────────

float hash11(float p) {
    p = fract(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return fract(p);
}

float hash21(vec2 p) {
    p = fract(p * vec2(127.1, 311.7));
    p += dot(p, p.yx + 19.19);
    return fract((p.x + p.y) * p.x);
}

float noise2(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(
        mix(hash21(i),              hash21(i + vec2(1.0, 0.0)), u.x),
        mix(hash21(i + vec2(0.0, 1.0)), hash21(i + vec2(1.0, 1.0)), u.x),
        u.y);
}

// OPT: 2 octaves instead of 4 — halves the noise2() calls per fbm2()
// call. Clouds get pixel-quantized afterwards anyway (cloud_pixel_edge),
// so the finer detail from octaves 3-4 wasn't contributing visually.
float fbm2(vec2 p) {
    float v = 0.0;
    float a = 0.5;
    v += a * noise2(p); p *= 2.1; a *= 0.5;
    v += a * noise2(p);
    return v;
}

float remap01(float v, float lo, float hi) {
    return clamp((v - lo) / (hi - lo), 0.0, 1.0);
}

// ── Sky Gradient ──────────────────────────────────────────────────────

vec3 sky_gradient(float elev) {
    float t1 = remap01(elev,  0.0,   1.0);
    float t3 = remap01(elev, -0.38, -0.18);

    vec3 col = mix(color_horizon, color_zenith, t1);
    col = mix(col, color_mid, remap01(elev, 0.0, 0.35));

    float horizFade = exp(-abs(elev) * 9.0);
    col = mix(col, color_sunset, horizFade * 0.75);
    col = mix(col, color_sunbase, t3 * 0.6);

    return col;
}

// ── Sun ───────────────────────────────────────────────────────────────

vec3 draw_sun(vec3 ray, vec3 sun_dir, float t) {
    float d   = dot(ray, sun_dir);
    float ang = acos(clamp(d, -1.0, 1.0));

    vec3 col = vec3(0.0);

    float glow = pow(clamp(1.0 - ang / sun_glow_radius, 0.0, 1.0), sun_glow_power);
    col += mix(sun_edge_color, sun_color, pow(glow, 0.4)) * glow * 1.8;

    float pulse = 0.5 + 0.5 * sin(t * 1.7);
    float halo  = exp(-ang * 12.0) * 0.4 * pulse;
    col += vec3(0.0, 1.0, 0.72) * halo * 0.35;

    // Sun disc — only when inside radius; use step() instead of if()
    float inside = step(ang, sun_radius);
    float rim    = remap01(ang, sun_radius * 0.6, sun_radius);
    vec3  disc   = mix(vec3(1.0, 0.97, 0.88), sun_color, rim);

    vec3 right = normalize(cross(sun_dir, vec3(0.0, 1.0, 0.0)));
    vec3 up    = cross(right, sun_dir);
    float vu   = dot(ray, up);
    float scanPhase = fract(vu / (sun_radius * 2.0) * scanline_count
                            + t * scanline_speed * 0.1);
    float scan = smoothstep(0.35, 0.45, scanPhase) * smoothstep(0.65, 0.55, scanPhase);
    disc = mix(disc, vec3(0.0, 1.0, 0.82), scan * scanline_opacity);

    col = mix(col, disc + col * 0.15, inside);

    return col;
}

// ── Perspective Grid ──────────────────────────────────────────────────

float perspective_grid(vec3 ray, float t) {
    float elev    = ray.y;
    float aboveHorizon = step(grid_horizon_y + 0.12, elev); // 1 if above, 0 if below
    float dist    = clamp(1.0 / max(abs(elev - grid_horizon_y), 0.002), 0.0, 80.0);
    float scroll  = t * grid_speed;
    float u       = ray.x * dist;
    float v       = ray.z * dist + scroll;
    vec2  cell    = vec2(u, v) / grid_cell_size;
    vec2  f       = fract(cell);
    float lineDist = min(min(f.x, 1.0 - f.x), min(f.y, 1.0 - f.y));
    float grid    = 1.0 - smoothstep(0.0, 0.04, lineDist);
    float distFade  = exp(-dist * 0.018);
    float horizFade = 1.0 - remap01(elev, grid_horizon_y - 0.35, grid_horizon_y + 0.12);
    return grid * distFade * horizFade * (1.0 - aboveHorizon);
}

// ── Horizon Seam ──────────────────────────────────────────────────────

float horizon_seam(float elev, float t) {
    float band  = exp(-abs(elev - grid_horizon_y) * 120.0);
    float pulse = 0.75 + 0.25 * sin(t * 3.1 + elev * 10.0);
    return band * pulse;
}

// ── Code Rain ─────────────────────────────────────────────────────────

float code_rain(vec3 ray, float t) {
    float elev    = ray.y;
    float belowMin = step(elev, 0.05); // 1 if elev <= 0.05
    float az      = atan(ray.x, -ray.z) / (2.0 * 3.14159265) + 0.5;
    float col     = floor(az * rain_columns);
    float colF    = fract(az * rain_columns);
    float speed   = (0.5 + hash11(col) * 1.5) * rain_speed;
    float phase   = hash11(col + 13.7) * 20.0;
    float headY   = fract((t * speed + phase) * 0.1);
    float dropElev = 1.0 - headY;
    float dropLen = 0.12 + hash11(col + 7.3) * 0.18;
    float dropDist = elev - dropElev;
    float colWidth = smoothstep(0.45, 0.15, abs(colF - 0.5));
    float trail   = remap01(-dropDist, 0.0, dropLen);
    trail         = trail * trail;
    float flicker = hash11(col + floor(t * 8.0));
    return trail * colWidth * flicker * remap01(elev, 0.05, 0.25) * (1.0 - belowMin);
}

// ── Cloud helpers (unrolled — no dynamic array indexing) ───────────────

float cloud_blob(vec2 uv, vec2 center, float size, float t) {
    vec2  diff = uv - center;
    diff.x    *= 1.6;
    float r    = length(diff) / size;
    float n    = fbm2((uv - center) * 18.0 + t * 0.1) * 0.4;
    return 1.0 - smoothstep(0.55 + n, 1.1 + n, r);
}

float cloud_density(vec2 uv, float t) {
    float d = 0.0;
    d = max(d, cloud_blob(uv, vec2(0.18 + t * cloud_speed * 0.01,  0.22), 0.100, t));
    d = max(d, cloud_blob(uv, vec2(0.62 - t * cloud_speed * 0.008, 0.17), 0.085, t));
    d = max(d, cloud_blob(uv, vec2(0.42 + t * cloud_speed * 0.005, 0.10), 0.065, t));
    d = max(d, cloud_blob(uv, vec2(0.79 + t * cloud_speed * 0.012, 0.28), 0.070, t));
    return d;
}

vec3 draw_clouds(vec3 ray, float t) {
    float belowMin = step(ray.y, 0.04);
    float az      = atan(ray.x, -ray.z) / (2.0 * 3.14159265) + 0.5;
    vec2  uv      = vec2(az, ray.y);
    float density = cloud_density(uv, t);

    vec3  cCol = mix(vec3(0.85, 0.60, 0.38), vec3(1.00, 0.92, 0.72), density);

    float pixSize = 0.006;
    vec2  pix     = floor(uv / pixSize) * pixSize;
    float edgeN   = hash21(pix * 130.0 + t * 0.05);
    float edgeMask = density * (1.0 - density) * 6.0;
    float pixEdge  = step(0.6, edgeN) * edgeMask * cloud_pixel_edge;
    cCol = mix(cCol, vec3(0.0, 1.0, 0.80), pixEdge * 0.55);

    return cCol * density * cloud_opacity * (1.0 - belowMin);
}

// ── Stars ─────────────────────────────────────────────────────────────

float draw_stars(vec3 ray, float t) {
    float belowHorizon = step(ray.y, 0.0);
    float az   = atan(ray.x, -ray.z);
    float el   = asin(clamp(ray.y, -1.0, 1.0));
    vec2  uv   = vec2(az, el) * vec2(8.0, 5.0) * star_count;
    vec2  cell = floor(uv);
    vec2  fr   = fract(uv) - 0.5;
    float h    = hash21(cell);
    float isStar = step(0.93, h);
    float r    = length(fr);
    float twinkleFreq = 1.5 + hash21(cell + 0.5) * 3.0;
    float twinkle = 0.6 + 0.4 * sin(t * twinkleFreq + h * 6.28318);
    twinkle = mix(1.0, twinkle, star_twinkle);
    float star = smoothstep(0.18, 0.0, r) * twinkle * isStar;
    star *= remap01(ray.y, 0.02, 0.18);
    return star * (1.0 - belowHorizon);
}

// ── Main ──────────────────────────────────────────────────────────────

void sky() {
    float t       = TIME * time_scale;
    vec3  ray     = EYEDIR;
    vec3  sun_dir = normalize(-sun_direction);

    // 1. Sky gradient
    vec3 col = sky_gradient(ray.y);

    // 2. Stars — only above the horizon (cheap already, but the below-
    // horizon half of the dome gets skipped entirely rather than
    // computed-then-masked).
    // OPT: guarded.
    if (ray.y > 0.0) {
        float starB  = draw_stars(ray, t);
        float cyanH  = hash21(vec2(floor(atan(ray.x, -ray.z) * 4.0),
                                   floor(asin(clamp(ray.y, -1.0, 1.0)) * 2.0)));
        vec3  starCol = mix(vec3(1.0, 0.97, 0.85), vec3(0.3, 1.0, 0.9), step(0.65, cyanH));
        col = mix(col, starCol, starB * remap01(ray.y, 0.1, 0.5) * 0.9);
    }

    // 3. Code rain — genuinely zero at/below elev 0.05 (belowMin in
    // code_rain), so that lower bound is a safe skip. There is NO
    // reliable upper bound: each column's drop head (dropElev) cycles
    // through the full [0,1] elevation range independently over time,
    // so drops can appear anywhere from just above the horizon up to
    // near the zenith at any given moment. An upper guard here would
    // slice through columns whose heads happen to be higher up.
    // OPT: guarded (lower bound only).
    if (ray.y > 0.02) {
        col += rain_color * code_rain(ray, t) * rain_opacity;
    }

    // 4. Pixel clouds — this is the expensive one (FBM noise). Only
    // visible in a band around the default blob elevations; skipping
    // outside it avoids the FBM calls entirely for most of the dome.
    // OPT: guarded, and fbm2() itself now uses 2 octaves instead of 4.
    if (ray.y > -0.02 && ray.y < 0.45) {
        col += draw_clouds(ray, t);
    }

    // 5. Perspective grid — only visible below (horizon + 0.12).
    // OPT: guarded.
    float gridV = 0.0;
    if (ray.y < grid_horizon_y + 0.15) {
        gridV = perspective_grid(ray, t);
        col = mix(col, col + grid_color, gridV * grid_opacity);
    }

    // 6. Horizon seam
    col += grid_color * horizon_seam(ray.y, t) * 0.35 * grid_opacity;

    // 7. Sun
    col += draw_sun(ray, sun_dir, t);

    // 8. Vignette (EYEDIR-based, no POSITION built-in needed)
    float vigH = abs(ray.x);
    float vigV = abs(ray.y - 0.1);
    float vigF = smoothstep(0.0, 1.0, max(vigH, vigV));
    col *= 1.0 - vignette_strength * vigF * vigF;

    COLOR = max(col, vec3(0.0));
}
