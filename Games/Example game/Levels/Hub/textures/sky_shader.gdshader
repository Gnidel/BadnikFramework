shader_type sky;
render_mode use_half_res_pass;

// ─────────────────────────────────────────────
//  PARAMETERS
// ─────────────────────────────────────────────
uniform vec3  sky_top_color    : source_color = vec3(0.05, 0.18, 0.55);
uniform vec3  sky_horizon_color: source_color = vec3(0.45, 0.72, 0.98);
uniform vec3  sky_bottom_color : source_color = vec3(0.70, 0.85, 1.00);

uniform vec3  sun_color        : source_color = vec3(1.00, 0.95, 0.80);
uniform float sun_size                        = 0.004;
uniform float sun_glow_size                   = 0.15;
uniform float sun_glow_intensity              = 0.6;

uniform vec3  cloud_color_top  : source_color = vec3(1.00, 1.00, 1.00);
uniform vec3  cloud_color_base : source_color = vec3(0.75, 0.80, 0.90);
uniform vec3  cloud_shadow_color: source_color= vec3(0.50, 0.55, 0.68);

uniform float cloud_coverage   = 0.48;   // 0 = clear, 1 = overcast
uniform float cloud_sharpness  = 2.8;    // edge crispness
uniform float cloud_speed      = 0.015;  // horizontal drift speed
uniform float cloud_height     = 0.18;   // altitude cutoff above horizon
uniform float cloud_thickness  = 0.35;   // vertical extent of cloud band
uniform float cloud_scale      = 2.2;    // tiling scale

uniform sampler2D noise_tex    : hint_default_white, repeat_enable;

// ─────────────────────────────────────────────
//  UTILITY
// ─────────────────────────────────────────────
float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

// 5-octave FBM using a texture for smooth GPU-friendly noise
float fbm(vec2 uv) {
	float v   = 0.0;
	float amp = 0.5;
	float freq = 1.0;
	for (int i = 0; i < 5; i++) {
		v   += amp * (texture(noise_tex, uv * freq).r * 2.0 - 1.0);
		freq *= 2.1;
		amp  *= 0.48;
	}
	return v;
}

// ─────────────────────────────────────────────
//  HALF-RES PASS  (clouds, expensive sampling)
// ─────────────────────────────────────────────
void sky() {
	// --- Direction & helpers ---
	vec3  dir      = EYEDIR;
	float up       = dir.y;               // -1 = nadir, +1 = zenith
	float horizon  = 1.0 - abs(up);      //  0 = pole,   1 = horizon

	// ── Sky gradient ──────────────────────────────────────────
	vec3 sky;
	if (up > 0.0) {
		sky = mix(sky_horizon_color, sky_top_color, pow(up, 0.45));
	} else {
		sky = mix(sky_horizon_color, sky_bottom_color, pow(-up, 0.6));
	}

	// Subtle horizon haze band
	float haze = pow(clamp(1.0 - abs(up) * 3.5, 0.0, 1.0), 3.0);
	sky = mix(sky, sky_horizon_color * 1.08, haze * 0.55);

	// ── Sun disc & glow ───────────────────────────────────────
	vec3  sun_dir   = normalize(LIGHT0_DIRECTION);
	float sun_dot   = dot(dir, sun_dir);
	float sun_disc  = smoothstep(1.0 - sun_size, 1.0 - sun_size * 0.5, sun_dot);
	float sun_glow  = pow(clamp(sun_dot, 0.0, 1.0), 1.0 / sun_glow_size);

	vec3 col = sky;
	col      = mix(col, sun_color * 1.6, sun_glow * sun_glow_intensity);
	col      = mix(col, sun_color * 2.5, sun_disc);

	// ── Clouds ────────────────────────────────────────────────
	// Only render above a threshold to avoid stretching at horizon
	float cloud_band = smoothstep(cloud_height - cloud_thickness * 0.5,
	                              cloud_height + cloud_thickness * 0.5,
	                              up);
	if (cloud_band > 0.001) {
		// Project onto a virtual cloud dome
		vec2 cloud_uv  = (dir.xz / max(up, 0.01)) * cloud_scale;
		cloud_uv      += vec2(TIME * cloud_speed, TIME * cloud_speed * 0.3);

		// Two layers for depth
		float n1 = fbm(cloud_uv);
		float n2 = fbm(cloud_uv * 1.7 + vec2(3.1, 7.3));
		float noise = n1 * 0.7 + n2 * 0.3;

		// Threshold → coverage
		float density = smoothstep(1.0 - cloud_coverage,
		                           1.0 - cloud_coverage + (1.0 / cloud_sharpness),
		                           noise * 0.5 + 0.5);

		density *= cloud_band;

		// Lighting: sun illumination angle
		float sun_up   = dot(sun_dir, vec3(0, 1, 0));
		float sun_lit  = clamp(sun_up * 2.0 + 0.5, 0.0, 1.0);
		float rim      = pow(clamp(sun_dot * 0.5 + 0.5, 0.0, 1.0), 3.0) * 0.4;

		vec3 cloud_col = mix(cloud_shadow_color,
		                     mix(cloud_color_base, cloud_color_top, sun_lit),
		                     smoothstep(0.3, 0.8, noise * 0.5 + 0.5));

		// Silver lining from sun
		cloud_col = mix(cloud_col, sun_color * 1.2, rim * density);

		col = mix(col, cloud_col, density * 0.96);
	}

	// ── Stars (visible only high up when sun is below horizon) ──
	float star_mask = clamp(-dot(sun_dir, vec3(0,1,0)) * 3.0, 0.0, 1.0)
	                * clamp((up - 0.15) / 0.4, 0.0, 1.0);
	if (star_mask > 0.001) {
		vec3  star_uv3  = normalize(dir);
		vec2  star_uv   = vec2(atan(star_uv3.x, star_uv3.z), asin(star_uv3.y));
		float star_n    = hash(floor(star_uv * 180.0));
		float star_size = pow(star_n, 300.0);
		col = mix(col, vec3(1.0), star_size * star_mask);
	}

	COLOR = col;
}
