using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;

namespace BadnikFramework.Tools.HsonImport
{
    // HSON -> Badnik Framework scene importer.
    //
    // Import runs in four passes, in this specific order (see each pass's region below
    // for why the order matters): 1) spawn nodes flat under Output, 2) resolve each
    // object's GLOBAL transform by walking the HSON parent chain ourselves, 3) apply
    // per-type corrections (these assume a flat/global frame, same as the original
    // script), 4) finally reparent into the real Godot hierarchy, preserving the
    // now-corrected global transform. Also applies scale and isExcluded, which were
    // previously ignored entirely.
    [Tool]
    public partial class HsonImportNode : Node
    {
        #region Source

        [ExportGroup("Source")]
        [Export(PropertyHint.MultilineText)]
        public string SourceJson { get; set; } = "";

        // One .hson path per line. All listed files - plus SourceJson, if also set - are
        // loaded together and merged into a single GUID-addressable pool, so an object in
        // one file can reference an object (parent, actions/objectIds, a camera "target",
        // etc.) defined in another file. Real stages are often split across multiple HSON
        // files (e.g. one per area), and this is how that gets stitched back together.
        [Export(PropertyHint.MultilineText)]
        public string SourceFilePaths { get; set; } = "";

        [Export] public Node3D Output { get; set; }

        #endregion

        #region Actions

        [ExportGroup("Actions")]
        [ExportToolButton("Import HSON")]
        public Callable ImportButton => Callable.From(Import);

        [ExportToolButton("Clear Output")]
        public Callable ClearButton => Callable.From(ClearOutput);

        #endregion

        #region Import Options

        [ExportGroup("Import Options")]
        [Export] public bool LoadOnStart = false;
        [Export] public bool HedgehogEngineCorrections = true;
        [Export] public bool ImportPaths = true;
        [Export] public bool ImportObjects = true;

        // New: reconstruct the HSON parent/child hierarchy as actual Godot node parenting,
        // instead of dumping every spawned object flat under Output.
        [Export] public bool ImportParenting = true;

        // New: apply the HSON "scale" property to spawned nodes (previously ignored entirely).
        [Export] public bool ImportScale = true;

        // New: objects with isExcluded=true in the HSON data are meant to be disabled/unused
        // and are skipped by default (previously imported unconditionally).
        [Export] public bool SkipExcludedObjects = true;

        #endregion

        #region Type Mapping

        // Key: lowercased HSON object type, Value: prefab scene path.
        [ExportGroup("Type Mapping")]
        [Export] public Godot.Collections.Dictionary<string, string> Mapping { get; set; } = new();

        // Key: lowercased HSON path type (setParameter/pathType), Value: prefab scene path.
        [Export] public Godot.Collections.Dictionary<string, string> PathMapping { get; set; } = new();

        #endregion

        // Merged pool of every object loaded across all sources (see TryLoadProjects),
        // keyed by HSON id. Objects in one loaded file can reference objects in another
        // (parent, actions/objectIds, camera "target", ...) - they're all resolved
        // against this single pool regardless of which file they actually came from.
        private readonly System.Collections.Generic.Dictionary<Guid, libHSON.Object> _objects = new();

        // Populated during import: HSON object id -> spawned Godot node. Used for the
        // parenting pass, and for path node lookups.
        private readonly System.Collections.Generic.Dictionary<Guid, Node3D> _spawned = new();

        public override void _Ready()
        {
            if (!LoadOnStart || Engine.IsEditorHint()) return;
            Import();
        }

        #region Public Actions

        public void ClearOutput()
        {
            if (Output == null) return;
            foreach (var child in Output.GetChildren())
            {
                if (Engine.IsEditorHint())
                {
                    child.Free();
                }
                else
                {
                    child.QueueFree();
                }
            }
        }

        public void Import()
        {
            if (Output == null)
            {
                GD.PushError("HsonImportNode: 'Output' is not set; nothing to import into.");
                return;
            }

            ClearOutput();
            _spawned.Clear();

            if (!TryLoadProjects())
            {
                return;
            }

            SpawnObjects();
            ApplyGlobalTransforms();
            ApplyTypeSpecificCorrections();
            ApplyParenting();

            GD.Print($"HsonImport: imported {_spawned.Count} object(s) from HSON.");
        }

        #endregion

        #region Loading

        // Loads every source (each line of SourceFilePaths, plus SourceJson if set) and
        // merges their objects into _objects. Stops and reports the specific source on
        // the first parse failure, rather than silently importing a partial merge.
        private bool TryLoadProjects()
        {
            _objects.Clear();
            bool loadedAny = false;

            var paths = (SourceFilePaths ?? "")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var path in paths)
            {
                if (!TryLoadOneProject(() => libHSON.Project.FromFile(ProjectSettings.GlobalizePath(path)), path))
                {
                    return false;
                }
                loadedAny = true;
            }

            if (!string.IsNullOrEmpty(SourceJson))
            {
                if (!TryLoadOneProject(() =>
                {
                    var input = System.Text.Encoding.UTF8.GetBytes(SourceJson);
                    return libHSON.Project.FromData(input);
                }, "<pasted JSON>"))
                {
                    return false;
                }
                loadedAny = true;
            }

            if (!loadedAny)
            {
                GD.PushWarning("HsonImportNode: no SourceFilePaths or SourceJson set.");
                return false;
            }

            GD.Print($"HsonImport: {_objects.Count} object(s) loaded across all sources.");
            return true;
        }

        private bool TryLoadOneProject(Func<libHSON.Project> load, string label)
        {
            libHSON.Project project;
            try
            {
                project = load();
            }
            catch (Exception e)
            {
                GD.PushError($"HsonImportNode: failed to parse HSON data from '{label}' ({e.GetType().Name}): {e.Message}");
                return false;
            }

            if (project.Metadata != null && !string.IsNullOrEmpty(project.Metadata.Name))
            {
                GD.Print($"HsonImport: loaded \"{project.Metadata.Name}\" from '{label}'"
                    + (string.IsNullOrEmpty(project.Metadata.Version) ? "" : $" (v{project.Metadata.Version})"));
            }

            foreach (var obj in project.Objects)
            {
                if (!_objects.TryAdd(obj.Id, obj))
                {
                    GD.PushWarning($"HsonImportNode: object id '{obj.Id}' from '{label}' collides with one "
                        + "already loaded from another source; keeping the first one loaded and skipping "
                        + "this duplicate.");
                }
            }

            return true;
        }

        #endregion

        #region Pass 1: spawn nodes

        private void SpawnObjects()
        {
            foreach (var newObject in _objects.Values)
            {
                if (SkipExcludedObjects && newObject.IsExcluded)
                {
                    continue;
                }

                var newGameObject = TryCreateNode(newObject);
                if (newGameObject == null)
                {
                    continue;
                }

                // Parent flat under Output for now; ApplyParentingAndTransforms will
                // re-parent as needed in pass 2. Nodes need *some* valid parent in the
                // tree before Reparent()/Owner assignment works correctly.
                Output.AddChild(newGameObject);
                if (Engine.IsEditorHint())
                {
                    newGameObject.Owner = GetTree().EditedSceneRoot;
                }

                if (newObject.HasSpecifiedName)
                {
                    newGameObject.Name = newObject.Name;
                }

                _spawned[newObject.Id] = newGameObject;
            }
        }

        private Node3D TryCreateNode(libHSON.Object newObject)
        {
            var lowerType = newObject.Type.ToLower();

            if (ImportPaths && lowerType == "path")
            {
                var pathType = newObject.GetParameter("setParameter/pathType")?.ValueString?.ToLower();
                if (pathType != null && PathMapping.TryGetValue(pathType, out var pathScenePath))
                {
                    return LoadAndInstantiate(pathScenePath, newObject);
                }
                // Path with an unmapped pathType: fall through to skip, same as original behavior.
                return null;
            }

            if (!newObject.HasSpecifiedType || !Mapping.TryGetValue(lowerType, out var scenePath))
            {
                // Some HSON types are valid but aren't meant to become separate scene
                // objects (e.g. PathNode - it's baked into its parent Path's curve instead).
                return null;
            }

            if (!ImportObjects)
            {
                return null;
            }

            return LoadAndInstantiate(scenePath, newObject);
        }

        private Node3D LoadAndInstantiate(string scenePath, libHSON.Object newObject)
        {
            var res = ResourceLoader.Load<PackedScene>(scenePath);
            if (res == null)
            {
                GD.PushWarning($"HsonImportNode: prefab at '{scenePath}' for object "
                    + $"'{(newObject.HasSpecifiedName ? newObject.Name : newObject.Id.ToString())}' "
                    + "could not be loaded; skipping.");
                return null;
            }

            var instance = res.Instantiate<Node3D>();
            if (instance == null)
            {
                GD.PushWarning($"HsonImportNode: prefab at '{scenePath}' does not root in a Node3D; skipping.");
            }
            return instance;
        }

        #endregion

        #region Pass 2: global (flat) transform

        // Cache of HSON object id -> resolved global (position, rotation, scale), computed
        // by walking the HSON parent chain. Keyed by id so shared ancestors in deep/wide
        // hierarchies aren't recomputed repeatedly.
        private readonly System.Collections.Generic.Dictionary<Guid, (Vector3 Pos, Quaternion Rot, Vector3 Scale)> _globalTransformCache = new();

        // Sets every spawned node's Position/Basis/Scale to its resolved GLOBAL transform
        // (still flat under Output at this point - see ApplyParenting for why).
        //
        // Global is computed by walking hsonObject.Parent ourselves (not via Godot's scene
        // tree, and not via libHSON's own GlobalTransform, which is a System.Numerics
        // row-vector Matrix4x4 that isn't safe to convert into Godot's column-vector Basis
        // without care). This also means a child's world position/rotation comes out
        // correct even if its immediate HSON parent wasn't itself imported as a scene node
        // (unmapped type / excluded) - the data-level chain is still walked in full.
        private void ApplyGlobalTransforms()
        {
            _globalTransformCache.Clear();

            foreach (var (id, node) in _spawned)
            {
                var hsonObject = _objects[id];
                var (pos, rot, scale) = GetGlobalTransform(hsonObject, new HashSet<Guid>());

                node.Position = pos;
                node.Basis = new Basis(rot);

                if (ImportScale)
                {
                    node.Scale = scale;
                }

                if (hsonObject.HasSpecifiedIsEditorVisible)
                {
                    node.Visible = hsonObject.IsEditorVisible;
                }
            }
        }

        private (Vector3 Pos, Quaternion Rot, Vector3 Scale) GetGlobalTransform(libHSON.Object hsonObject, HashSet<Guid> visiting)
        {
            if (_globalTransformCache.TryGetValue(hsonObject.Id, out var cached))
            {
                return cached;
            }

            var localPos = hsonObject.HasSpecifiedPosition ? hsonObject.LocalPosition.ToVector3() : Vector3.Zero;
            var localRot = hsonObject.HasSpecifiedRotation ? hsonObject.LocalRotation.ToQuaternion() : Quaternion.Identity;
            var localScale = hsonObject.HasSpecifiedScale ? hsonObject.LocalScale.ToVector3() : Vector3.One;

            (Vector3 Pos, Quaternion Rot, Vector3 Scale) result;

            if (ImportParenting && hsonObject.HasSpecifiedParent && hsonObject.Parent != null)
            {
                if (!visiting.Add(hsonObject.Id))
                {
                    GD.PushError($"HsonImportNode: parent cycle detected involving object "
                        + $"'{(hsonObject.HasSpecifiedName ? hsonObject.Name : hsonObject.Id.ToString())}'; "
                        + "treating it as unparented for this import.");
                    result = (localPos, localRot, localScale);
                }
                else
                {
                    var parent = GetGlobalTransform(hsonObject.Parent, visiting);
                    visiting.Remove(hsonObject.Id);

                    result = (
                        parent.Pos + parent.Rot * (parent.Scale * localPos),
                        parent.Rot * localRot,
                        parent.Scale * localScale
                    );
                }
            }
            else
            {
                result = (localPos, localRot, localScale);
            }

            _globalTransformCache[hsonObject.Id] = result;
            return result;
        }

        #endregion

        #region Pass 4: re-parent (preserving the now-corrected global transform)

        // Runs *after* ApplyTypeSpecificCorrections, on purpose: those corrections
        // (RotateObjectLocal/Translate for dashpanel, jumpboard, ring, etc.) are tuned
        // as if every object were flat/unparented, and they mutate each node's own local
        // basis. Reparenting before applying them would make the correction compose with
        // the parent's already-corrected orientation instead of the object's own resolved
        // global one, causing rotations (and any offsets derived from the node's Basis) to
        // stack incorrectly down a hierarchy - e.g. two 180-degree corrections in a row
        // cancel out relative to an already-corrected parent, leaving a child looking
        // exactly opposite to it. Reparenting last, with keepGlobalTransform: true, lets
        // Godot recompute the correct local transform needed to preserve the corrected
        // global one, instead of us having to re-derive it by hand.
        private void ApplyParenting()
        {
            if (!ImportParenting) return;

            foreach (var (id, node) in _spawned)
            {
                var hsonObject = _objects[id];
                if (!hsonObject.HasSpecifiedParent || hsonObject.Parent == null) continue;

                if (_spawned.TryGetValue(hsonObject.Parent.Id, out var parentNode))
                {
                    node.Reparent(parentNode, true);
                }
                else
                {
                    GD.PushWarning($"HsonImportNode: object '{node.Name}' has a parent in the HSON "
                        + "data, but that parent wasn't imported as a scene node (unmapped type or "
                        + "excluded) - leaving it under Output instead (its world position/rotation "
                        + "is still correct).");
                }
            }
        }

        #endregion

        #region Pass 3: per-type corrections

        private void ApplyTypeSpecificCorrections()
        {
            foreach (var (id, newGameObject) in _spawned)
            {
                var newObject = _objects[id];

                switch (newObject.Type.ToLower())
                {
                    case "path":
                        SetPathParams(newGameObject, newObject);
                        break;
                    case "ring":
                        newGameObject.Translate(newGameObject.Basis.Y * 0.5f);      // To spawn slightly above ground instead of in.
                        break;
                    case "superring":
                        newGameObject.Translate(newGameObject.Basis.Y * 0.5f);      // To spawn slightly above ground instead of in.
                        break;
                    case "spring":
                        newGameObject.RotateObjectLocal(Vector3.Right, Mathf.Pi / 2);  // Rotate because BF prefab is different from HE
                        SetSpringParams(newGameObject, newObject);
                        break;
                    case "widespring":
                        if (HedgehogEngineCorrections)
                        {
                            newGameObject.RotateObjectLocal(Vector3.Right, -newGameObject.Rotation.X + Mathf.Pi / 2);   // In HE they always launch them up, but they are positioned diagonally for visuals. Here we rotate them to point where they launch.
                        }
                        SetSpringParams(newGameObject, newObject);
                        break;
                    case "jumpboard":
                        if (HedgehogEngineCorrections)
                        {
                            newGameObject.RotateObjectLocal(Vector3.Up, Mathf.Pi);  // Rotate because BF prefab is different from HE
                        }
                        SetJumpboardParams(newGameObject, newObject);
                        break;
                    case "dashpanel":
                        if (HedgehogEngineCorrections)
                        {
                            newGameObject.RotateObjectLocal(Vector3.Up, Mathf.Pi);  // Rotate because BF prefab is different from HE
                        }
                        SetDashPanelParams(newGameObject, newObject);
                        break;
                    case "dashroller":
                        if (HedgehogEngineCorrections)
                        {
                            newGameObject.RotateObjectLocal(Vector3.Up, -Mathf.Pi);  // Rotate because BF prefab is different from HE
                        }
                        SetDashPanelParams(newGameObject, newObject);
                        break;
                    case "dashring":
                        SetSpringParams(newGameObject, newObject);
                        break;
                    case "upreel":
                        if (HedgehogEngineCorrections)
                        {
                            newGameObject.RotateObjectLocal(Vector3.Up, Mathf.Pi);  // Rotate because BF prefab is different from HE
                            newGameObject.Position += newGameObject.Basis.Z; // HE and BF prefab difference too
                        }
                        SetUpreelParams(newGameObject, newObject);
                        break;
                    case "balloon":
                        SetBalloonParams(newGameObject, newObject);
                        break;
                    case "grindbooster":
                        if (HedgehogEngineCorrections)
                        {
                            newGameObject.RotateObjectLocal(Vector3.Up, Mathf.Pi);  // Rotate because BF prefab is different from HE
                        }
                        SetSpringParams(newGameObject, newObject);
                        break;
                    case "redring":
                        newGameObject.Translate(newGameObject.Basis.Y * 0.5f);      // To spawn slightly above ground instead of in.
                        SetRedRingParams(newGameObject, newObject);
                        break;
                    case "bouncepad": // NOT IN HEDGEHOG ENGINE GAMES! It's the giant propeller spring from Sonic Dream Team.
                        SetSpringParams(newGameObject, newObject);
                        break;
                }
            }
        }

        #endregion

        #region Type-specific parameter setup
        // Unchanged from the original importer, other than swapping the internal-only
        // GetParameterFromName(...) for the public GetParameter(...), which handles both
        // plain names and "a/b/c" paths and doesn't rely on libHSON being compiled into
        // the same assembly. See README "What changed and why" #6.

        private void SetSpringParams(Node3D dirLauncher, libHSON.Object hsonObject)
        {
            var firstSpeed = hsonObject.GetParameter("firstSpeed");
            if (firstSpeed != null)
            {
                dirLauncher.Set("MinVelocity", firstSpeed.ValueFloatingPoint);
                dirLauncher.Set("MaxVelocity", firstSpeed.ValueFloatingPoint);
            }

            var speed = hsonObject.GetParameter("Speed");
            if (speed != null)
            {
                dirLauncher.Set("MinVelocity", speed.ValueFloatingPoint);
                dirLauncher.Set("MaxVelocity", speed.ValueFloatingPoint);
            }

            var ooc = hsonObject.GetParameter("outOfControl");
            if (ooc != null)
            {
                dirLauncher.Set("LockInputTime", ooc.ValueFloatingPoint);
            }

            var keepVelocityDistance = hsonObject.GetParameter("keepVelocityDistance");
            if (keepVelocityDistance != null)
            {
                dirLauncher.Set("KeepVelocityDistance", keepVelocityDistance.ValueFloatingPoint);
                dirLauncher.Set("Snap", true);
            }

            var keepVelocityTime = hsonObject.GetParameter("KeepVelocity");
            if (keepVelocityTime != null)
            {
                dirLauncher.Set("KeepVelocityTime", keepVelocityTime.ValueFloatingPoint);
            }
        }

        private void SetJumpboardParams(Node3D dirLauncherNode, libHSON.Object hsonObject)
        {
            // Note: It works very differently from Hedgehog Engine (at least in Frontiers). HE makes movement go on path
            // according to height and distance. BF doesn't launch using set paths, but is more physics based instead.
            // These values are approximations to achieve similar movement, but don't guarantee identical movement.

            var dirLauncher = dirLauncherNode.GetNode<DirectionalLauncher>(".");

            var firstSpeed = hsonObject.GetParameter("impulseSpeedOn");
            if (firstSpeed != null)
            {
                dirLauncher.MinVelocity = (float)firstSpeed.ValueFloatingPoint;
                dirLauncher.MaxVelocity = (float)firstSpeed.ValueFloatingPoint;
            }

            var ooc = hsonObject.GetParameter("outOfControl");
            var motionTime = hsonObject.GetParameter("motionTime");
            if (ooc != null && motionTime != null)
            {
                dirLauncher.LockInputTime = ooc.ValueFloatingPoint;
            }

            var distanceX = hsonObject.GetParameter("distanceX");
            var distanceY = hsonObject.GetParameter("height");
            var distanceZ = hsonObject.GetParameter("distance");
            if (distanceX != null && distanceY != null && distanceZ != null && motionTime != null)
            {
                var gravity = 60f;  // BF defaults
                var velocityZ = (distanceZ.ValueFloatingPoint * 1.2f) / (motionTime.ValueFloatingPoint);  // Adding constant because it kept undershooting. It's guestimate because I don't know what's the factor. Maybe global air drag?
                var velocityY = ((distanceY.ValueFloatingPoint + gravity * motionTime.ValueFloatingPoint) / (motionTime.ValueFloatingPoint));

                var velocity = (float)Mathf.Sqrt(velocityZ * velocityZ + velocityY * velocityY);
                var dir = new Vector3(0, (float)velocityY, -(float)velocityZ).Normalized();

                dirLauncher.RelativeVelocityDir = dir;
                dirLauncher.MinVelocity = velocity;
                dirLauncher.MaxVelocity = velocity;
            }

            var size = hsonObject.GetParameter("size");
            if (size != null)
            {
                switch (size.ValueString)  // NOTE: These are just guestimates
                {
                    case "SIZE_S":
                        dirLauncherNode.Scale = new Vector3(1, 1, 1);
                        break;
                    case "SIZE_M":
                        dirLauncherNode.Scale = new Vector3(2, 2, 2);
                        break;
                    case "SIZE_L":
                        dirLauncherNode.Scale = new Vector3(5, 5, 5);
                        break;
                }
            }

            dirLauncher.KeepVelocityTime = 0.3f;
        }

        private void SetDashPanelParams(Node3D dirLauncher, libHSON.Object hsonObject)
        {
            var firstSpeed = hsonObject.GetParameter("speed");
            if (firstSpeed != null)
            {
                dirLauncher.Set("MinVelocity", firstSpeed.ValueFloatingPoint);
                dirLauncher.Set("MaxVelocity", firstSpeed.ValueFloatingPoint);
            }

            var ooc = hsonObject.GetParameter("ocTime");
            if (ooc != null)
            {
                dirLauncher.Set("LockInputTime", ooc.ValueFloatingPoint);
            }
        }

        private void SetPathParams(Node3D splineObject, libHSON.Object hsonObject)
        {
            splineObject.Position = Vector3.Zero;

            Path3D path = splineObject.GetNodeOrNull<Path3D>(".");
            path.Curve = new Curve3D();
            path.Curve.ClearPoints();
            var childrenUuids = hsonObject.GetParameter("setParameter/nodeList").ValueArray;
            for (int i = 0; i < childrenUuids.Count; i++)
            {
                var uuid = childrenUuids[i];
                var hsonNode = _objects[Guid.Parse(uuid.ValueString)];

                var newPosition = hsonNode.LocalPosition.ToVector3();

                path.Curve.AddPoint(newPosition);
            }

            bool isLoop = false;
            if (hsonObject.GetParameter("setParameter/isLoopPath").ValueBoolean)
            {
                var uuid = childrenUuids[0];
                var hsonNode = _objects[Guid.Parse(uuid.ValueString)];

                var newPosition = hsonNode.LocalPosition.ToVector3();
                path.Curve.AddPoint(newPosition);

                isLoop = true;
            }

            if (hsonObject.GetParameter("setParameter/pathType").ValueString == "GR_PATH")  // TODO: Think whenever it should be grind path BEFORE spawning it
            {
                var grindRail = path.GetNodeOrNull<GrindRail>(".");
                if (grindRail != null)
                {
                    grindRail.Loop = hsonObject.GetParameter("setParameter/isLoopPath").ValueBoolean;
                }
            }

            // Set tilts
            for (int i = 0; i < childrenUuids.Count; i++)
            {
                var offset = path.Curve.GetClosestOffset(path.Curve.GetPointPosition(i));
                var pwr = path.Curve.SampleBakedWithRotation(offset, false, true);
                Vector3 pointUp = pwr.Basis.Y;

                var uuid = childrenUuids[i];
                var hsonNode = _objects[Guid.Parse(uuid.ValueString)];
                var hsonNodeRotation = hsonNode.LocalRotation.ToQuaternion();

                var rotationOffset = new Quaternion(
                    (hsonNodeRotation * Vector3.Up).ProjectOnPlane(pwr.Basis.Z).Normalized(),
                    pointUp.ProjectOnPlane(hsonNodeRotation * Vector3.Forward).Normalized()).Normalized();

                var angle = -rotationOffset.GetAngle();

                path.Curve.SetPointTilt(i, angle);

                // 360 degree problem correction
                // Angle is within range from -180 degrees to 180. That means, for example, instead of going from 179 degrees it will jump to -179 instead of 181, reversing the direction.
                if (i == 0) continue;
                var prevTilt = path.Curve.GetPointTilt(i - 1);
                int fullRotations = (int)(prevTilt / (Mathf.Pi * 2));
                angle += fullRotations * Mathf.Pi;
                var angleDiff = Mathf.Abs(angle - prevTilt);
                if (angleDiff > Mathf.Abs(angle - 360 - prevTilt))
                {
                    angle -= 360;
                }
                else if (angleDiff > Mathf.Abs(angle - 360 - prevTilt))
                {
                    angle += 360;
                }
                path.Curve.SetPointTilt(i, angle);
            }
        }

        private void SetUpreelParams(Node3D upreel, libHSON.Object hsonObject)
        {
            var length = hsonObject.GetParameter("length");
            if (length != null)
            {
                Path3D path = upreel.GetNodeOrNull<Path3D>(".");
                path.Curve = new Curve3D();
                path.Curve.ClearPoints();

                path.Curve.AddPoint(-upreel.Basis.Y * (float)length.ValueFloatingPoint);
                path.Curve.AddPoint(Vector3.Zero);
            }
        }

        private void SetBalloonParams(Node3D balloonNode, libHSON.Object hsonObject)
        {
            Balloon balloon = balloonNode.GetNodeOrNull<Balloon>(".");
            if (balloon == null) return;
            var upSpeed = hsonObject.GetParameter("upSpeed");
            if (upSpeed != null)
            {
                balloon.RelativeEjectVelocity = new Vector3(0, (float)upSpeed.ValueFloatingPoint, 0);
            }
        }

        private void SetRedRingParams(Node3D redRingNode, libHSON.Object hsonObject)
        {
            CollectableItem item = redRingNode.GetNodeOrNull<CollectableItem>(".");
            if (item == null) return;
            var itemId = hsonObject.GetParameter("ItemId");
            if (itemId != null)
            {
                item.ItemName = "RedRing" + (itemId.ValueSignedInteger + 1).ToString();
            }
        }

        #endregion
    }
}

public static class SystemNumericsConvert
{
    public static Vector3 ToVector3(this System.Numerics.Vector3 p)
    {
        return new Vector3(p.X, p.Y, p.Z);
    }

    public static Quaternion ToQuaternion(this System.Numerics.Quaternion p)
    {
        return new Quaternion(p.X, p.Y, p.Z, p.W).Normalized();
    }
}
