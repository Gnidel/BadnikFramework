using System;
using System.Collections.Generic;
using Godot;

public partial class Portal : Node3D
{
    [Export]
    public Node3D CameraLocation;

    [Export]
    public Portal ConnectedPortal;

    [Export]
    public bool AllowEntry = true;

    public List<Node3D> IgnoredBodies = new List<Node3D>();

    // Maximum distance the SubViewport camera is allowed to travel behind
    // this portal's plane. Limits perspective distortion at large viewing
    // distances. Set to 0 or negative to disable.
    [Export]
    public float MaxRenderCameraDistance = 0;

    // --- Per-player rendering state ---
    private class PortalView
    {
        public SubViewport Viewport;
        public Camera3D Camera;
        public MeshInstance3D Mesh;
        public bool MaterialReady;
    }

    private readonly Dictionary<int, PortalView> _views = new();

    // Shared shader (created once, reused by all portal instances)
    private static Shader _sharedShader;
    private static Shader SharedShader
    {
        get
        {
            if (_sharedShader == null)
            {
                _sharedShader = new Shader();
                _sharedShader.Code =
                    @"
shader_type spatial;
render_mode unshaded, cull_back, depth_draw_opaque, shadows_disabled;

uniform sampler2D viewport_texture : source_color, filter_linear_mipmap;

void fragment() {
	ALBEDO = texture(viewport_texture, SCREEN_UV).rgb;
}
";
            }
            return _sharedShader;
        }
    }

    // Portal identity within its pair (0 or 1). Determines which render layer
    // bits to use so a portal's SubViewport camera never sees its own mesh
    // but can see the connected portal's mesh (enabling recursion).
    private int _identity = -1;
    private int Identity
    {
        get
        {
            if (_identity < 0 && ConnectedPortal != null)
                _identity = GetInstanceId() < ConnectedPortal.GetInstanceId() ? 0 : 1;
            return _identity;
        }
    }

    // Render layer bit for a (playerId, portalIdentity) pair.
    // Uses bits 10-17 (layers 11-18), supporting up to 4 split-screen players.
    private static uint LayerBit(int playerId, int identity) =>
        1u << (10 + playerId * 2 + identity);

    private MeshInstance3D _originalMesh;

    private Transform3D GetPortalWarpTransform()
    {
        return ConnectedPortal.GlobalTransform
            * new Transform3D(new Basis(Vector3.Up, Mathf.Pi), Vector3.Zero)
            * GlobalTransform.AffineInverse();
    }

    public override void _Ready()
    {
        // Keep the original mesh as a template but hide it —
        // per-player clones replace it at runtime.
        _originalMesh = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
        if (_originalMesh != null)
            _originalMesh.Visible = false;

        // Disable RemoteTransform3D sync — we drive cameras directly
        if (CameraLocation is RemoteTransform3D rt)
            rt.RemotePath = new NodePath();
    }

    // --- Per-player view management ---

    private PortalView GetOrCreateView(int playerId)
    {
        if (_views.TryGetValue(playerId, out var existing))
            return existing;

        var view = new PortalView();

        // SubViewport — always renders so recursive texture is never stale
        view.Viewport = new SubViewport();
        view.Viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        view.Viewport.GuiDisableInput = true;
        view.Viewport.World3D = GetWorld3D();
        AddChild(view.Viewport);

        // Camera — sees world layers (0-9) only. Portal meshes are excluded
        // because SCREEN_UV sampling creates broken feedback for recursive rendering
        // (each recursion level has a different projection, so UVs don't align).
        view.Camera = new Camera3D();
        view.Camera.CullMask = 0x3FFu;
        view.Viewport.AddChild(view.Camera);

        // MeshInstance — clone of the template mesh, on this player+identity's layer
        view.Mesh = new MeshInstance3D();
        if (_originalMesh != null)
        {
            view.Mesh.Mesh = _originalMesh.Mesh;
            view.Mesh.Transform = _originalMesh.Transform;
        }
        view.Mesh.Layers = LayerBit(playerId, Identity);
        AddChild(view.Mesh);

        _views[playerId] = view;
        return view;
    }

    private void SetupViewMaterial(PortalView view, int playerId)
    {
        if (view.MaterialReady || ConnectedPortal == null)
            return;

        // Need the connected portal's SubViewport for this player to exist first
        if (!ConnectedPortal._views.TryGetValue(playerId, out var connectedView))
            return;

        var viewportTex = connectedView.Viewport.GetTexture();
        if (viewportTex == null)
            return;

        var shaderMat = new ShaderMaterial();
        shaderMat.Shader = SharedShader;
        shaderMat.SetShaderParameter("viewport_texture", viewportTex);
        view.Mesh.MaterialOverride = shaderMat;
        view.MaterialReady = true;
    }

    public void OnBodyEntered(Node3D other)
    {
        if (IgnoredBodies.Contains(other))
        {
            return;
        }
        var Player = other.GetNodeOrNull<PlayerController>(".");
        if (Player != null && !IgnoredBodies.Contains(other))
        {
            // Use the same warp transform for player, velocity, and camera.
            // This places the player in front of the exit portal, facing outward,
            // preserving relative offset (e.g. entering bottom-left exits bottom-left).
            var warp = GetPortalWarpTransform();

            Player.GlobalTransform = warp * Player.GlobalTransform;
            Player.LinearVelocity = warp.Basis * Player.LinearVelocity;
            Player.GroundNormal = warp.Basis * Player.GroundNormal;

            if (PlayerCamera.Instances.ContainsKey(Player.PlayerID))
            {
                var cam = PlayerCamera.Instances[Player.PlayerID];
                cam.GlobalTransform = warp * cam.GlobalTransform;
                cam.ApplyPortalWarp(warp.Basis);
            }

            ConnectedPortal.IgnoredBodies.Add(other);
            this.IgnoredBodies.Remove(other);
        }
    }

    public void OnBodyExit(Node3D other)
    {
        if (IgnoredBodies.Contains(other))
        {
            IgnoredBodies.Remove(other);
            return;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        var ignoredBodiesCopy = new List<Node3D>(IgnoredBodies);
        // Corner case: players moving reeeeally slowly into the portal and therefore not teleporting.
        foreach (var other in ignoredBodiesCopy)
        {
            var rb = other.GetNodeOrNull<RigidBody3D>(".");
            if (rb != null)
            {
                if (rb.LinearVelocity.Normalized().Dot(-this.GlobalBasis.Z) > 0)
                {
                    rb.LinearVelocity =
                        rb.LinearVelocity.ProjectOnPlane(this.GlobalBasis.Z)
                        + this.GlobalBasis.Z
                            * rb.LinearVelocity.Project(this.GlobalBasis.Z).Length();
                    if (IgnoredBodies.Contains(rb))
                    {
                        IgnoredBodies.Remove(rb);
                    }
                }
            }
        }
    }

    public override void _Process(double delta)
    {
        if (ConnectedPortal == null)
            return;

        foreach (var cam in PlayerCamera.Instances)
        {
            var playerId = cam.Key;
            var playerCam = cam.Value;

            var view = GetOrCreateView(playerId);

            // Ensure this player's camera can see portal meshes on their layers
            playerCam.CullMask |= LayerBit(playerId, 0) | LayerBit(playerId, 1);

            if (!view.MaterialReady)
                SetupViewMaterial(view, playerId);

            // Match SubViewport resolution to this player's viewport
            var mainViewport = playerCam.GetViewport();
            if (mainViewport != null)
            {
                var mainSize = mainViewport.GetVisibleRect().Size;
                var targetSize = new Vector2I((int)mainSize.X, (int)mainSize.Y);
                if (view.Viewport.Size != targetSize)
                    view.Viewport.Size = targetSize;
            }

            // Position camera using connected portal's warp transform
            var camWarp = ConnectedPortal.GetPortalWarpTransform();
            var virtualCamTransform = camWarp * playerCam.GlobalTransform;

            // Optionally clamp how far behind the portal surface the render camera can go
            if (MaxRenderCameraDistance > 0)
            {
                var portalNormal = GlobalBasis.Z;
                var offset = virtualCamTransform.Origin - GlobalPosition;
                var behindDist = -offset.Dot(portalNormal);
                if (behindDist > MaxRenderCameraDistance)
                {
                    virtualCamTransform.Origin +=
                        portalNormal * (MaxRenderCameraDistance - behindDist);
                }
            }

            view.Camera.GlobalTransform = virtualCamTransform;
            view.Camera.Fov = playerCam.Fov;
            view.Camera.Near = playerCam.Near;
            view.Camera.Far = playerCam.Far;
        }
    }
}
