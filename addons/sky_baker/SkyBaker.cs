using Godot;

#if TOOLS
[Tool]
public partial class SkyBaker : Node3D
{
	[Export]
	public NodePath SourceWorldEnvironmentPath { get; set; }

	[Export]
	public Vector3 CapturePosition { get; set; } = Vector3.Zero;

	[Export(PropertyHint.Range, "0.001,1000,0.001,or_greater")]
	public float CaptureNear { get; set; } = 0.01f;

	[Export(PropertyHint.Range, "0.01,100000,0.01,or_greater")]
	public float CaptureFar { get; set; } = 100000.0f;

	[Export(PropertyHint.Range, "128,4096,128")]
	public int FaceSize { get; set; } = 1024;

	[Export]
	public string TextureOutputPath { get; set; } = "res://sky_baked.png";

	[Export]
	public string MaterialOutputPath { get; set; } = "res://sky_baked.tres";

	[Export]
	public string SkyOutputPath { get; set; } = "res://sky_baked_sky.tres";

	[Export]
	public bool ApplyMaterialToWorldEnvironment { get; set; } = false;

	[ExportToolButton("Bake Sky Texture and Material")]
	public Callable BakeButton => Callable.From(BakeSky);

	public void BakeSky()
	{
		if (FaceSize < 16 || CaptureNear <= 0.0f || CaptureFar <= CaptureNear || string.IsNullOrWhiteSpace(TextureOutputPath))
		{
			GD.PushError("SkyBaker requires a valid face size, capture near/far range, and texture output path.");
			return;
		}

		WorldEnvironment source = FindSourceWorldEnvironment();
		if (source?.Environment?.Sky == null)
		{
			GD.PushError("SkyBaker could not find a WorldEnvironment with a Sky resource.");
			return;
		}

		Image panorama = RenderPanorama(source.Environment);
		Error imageError = panorama.SavePng(TextureOutputPath);
		if (imageError != Error.Ok)
		{
			GD.PushError($"Could not save baked sky texture to {TextureOutputPath}: {imageError}");
			return;
		}

		EditorInterface.Singleton.GetResourceFilesystem().Scan();
		Texture2D texture = ResourceLoader.Load<Texture2D>(
			TextureOutputPath,
			cacheMode: ResourceLoader.CacheMode.Ignore
		);
		if (texture == null)
			texture = ImageTexture.CreateFromImage(panorama);

		var material = new PanoramaSkyMaterial
		{
			Panorama = texture,
			EnergyMultiplier = source.Environment.BackgroundEnergyMultiplier
		};

		if (!string.IsNullOrWhiteSpace(MaterialOutputPath))
		{
			Error materialError = ResourceSaver.Save(material, MaterialOutputPath);
			if (materialError != Error.Ok)
			{
				GD.PushError($"Could not save baked sky material to {MaterialOutputPath}: {materialError}");
				return;
			}
		}

		var sky = new Sky
		{
			SkyMaterial = material,
			RadianceSize = source.Environment.Sky.RadianceSize
		};
		if (!string.IsNullOrWhiteSpace(SkyOutputPath))
		{
			Error skyError = ResourceSaver.Save(sky, SkyOutputPath);
			if (skyError != Error.Ok)
			{
				GD.PushError($"Could not save baked sky to {SkyOutputPath}: {skyError}");
				return;
			}
		}

		if (ApplyMaterialToWorldEnvironment)
			ApplySkyWithUndo(source.Environment, sky);

		EditorInterface.Singleton.GetResourceFilesystem().Scan();
		GD.Print($"Baked sky texture to {TextureOutputPath}");
		if (!string.IsNullOrWhiteSpace(MaterialOutputPath))
			GD.Print($"Baked sky material to {MaterialOutputPath}");
		if (!string.IsNullOrWhiteSpace(SkyOutputPath))
			GD.Print($"Baked sky to {SkyOutputPath}");
	}

	private void ApplySkyWithUndo(Environment environment, Sky sky)
	{
		EditorUndoRedoManager undoRedo = SkyBakerPlugin.UndoRedo;
		if (undoRedo == null)
		{
			GD.PushError("SkyBaker could not access the editor undo manager.");
			return;
		}

		Sky previousSky = environment.Sky;
		undoRedo.CreateAction("Apply Baked Sky", UndoRedo.MergeMode.Disable, environment);
		undoRedo.AddDoMethod(this, nameof(SetSky), environment, sky);
		undoRedo.AddUndoMethod(this, nameof(SetSky), environment, previousSky);
		undoRedo.AddDoReference(sky);
		if (previousSky != null)
			undoRedo.AddUndoReference(previousSky);
		undoRedo.CommitAction();
	}

	private void SetSky(Environment environment, Sky sky)
	{
		environment.Sky = sky;
	}

	private WorldEnvironment FindSourceWorldEnvironment()
	{
		Node sceneRoot = EditorInterface.Singleton.GetEditedSceneRoot();

		if (SourceWorldEnvironmentPath != null && !SourceWorldEnvironmentPath.IsEmpty)
		{
			WorldEnvironment relativeResult = GetNodeOrNull<WorldEnvironment>(SourceWorldEnvironmentPath);
			if (relativeResult != null)
				return relativeResult;

			return sceneRoot?.GetNodeOrNull<WorldEnvironment>(SourceWorldEnvironmentPath);
		}

		Node searchRoot = sceneRoot ?? Owner ?? GetParent();
		if (searchRoot != null && searchRoot != this)
			return FindWorldEnvironment(searchRoot);

		return null;
	}

	private WorldEnvironment FindWorldEnvironment(Node node)
	{
		if (node is WorldEnvironment worldEnvironment)
			return worldEnvironment;

		foreach (Node child in node.GetChildren())
		{
			WorldEnvironment result = FindWorldEnvironment(child);
			if (result != null)
				return result;
		}

		return null;
	}

	private Image RenderPanorama(Environment sourceEnvironment)
	{
		var viewport = new SubViewport
		{
			Size = new Vector2I(FaceSize, FaceSize),
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
			TransparentBg = false,
			Msaa3D = Viewport.Msaa.Disabled
		};
		var worldEnvironment = new WorldEnvironment
		{
			Environment = (Environment)sourceEnvironment.Duplicate(true)
		};
		var camera = new Camera3D
		{
			Fov = 90.0f,
			Near = CaptureNear,
			Far = CaptureFar,
			KeepAspect = Camera3D.KeepAspectEnum.Width
		};

		AddChild(viewport);
		viewport.AddChild(worldEnvironment);
		viewport.AddChild(camera);
		camera.Current = true;
		camera.GlobalPosition = CapturePosition;
		camera.ForceUpdateTransform();

		Image[] faces = new Image[6];
		Vector3[] directions =
		{
			Vector3.Right,
			Vector3.Left,
			Vector3.Up,
			Vector3.Down,
			Vector3.Back,
			Vector3.Forward
		};
		Vector3[] ups =
		{
			Vector3.Up,
			Vector3.Up,
			Vector3.Back,
			Vector3.Forward,
			Vector3.Up,
			Vector3.Up
		};

		for (int face = 0; face < directions.Length; face++)
		{
			camera.GlobalTransform = new Transform3D(
				Basis.LookingAt(directions[face], ups[face]),
				CapturePosition
			);
			camera.ForceUpdateTransform();
			RenderingServer.ForceDraw(false);
			RenderingServer.ForceDraw(false);
			faces[face] = viewport.GetTexture().GetImage();
		}

		Image panorama = Image.CreateEmpty(FaceSize * 2, FaceSize, false, Image.Format.Rgba8);
		Basis cameraBasis;
		for (int y = 0; y < FaceSize; y++)
		{
			float latitude = Mathf.Pi * (0.5f - (y + 0.5f) / FaceSize);
			float latitudeCos = Mathf.Cos(latitude);
			for (int x = 0; x < FaceSize * 2; x++)
			{
				float longitude = Mathf.Pi * ((x + 0.5f) / FaceSize - 1.0f);
				Vector3 direction = new Vector3(
					Mathf.Sin(longitude) * latitudeCos,
					Mathf.Sin(latitude),
					Mathf.Cos(longitude) * latitudeCos
				).Normalized();

				int face = SelectFace(direction);
				cameraBasis = Basis.LookingAt(directions[face], ups[face]);
				float depth = -direction.Dot(cameraBasis.Z);
				float faceX = 0.5f + direction.Dot(cameraBasis.X) / (2.0f * depth);
				float faceY = 0.5f - direction.Dot(cameraBasis.Y) / (2.0f * depth);
				panorama.SetPixel(x, y, SampleBilinear(faces[face], faceX, faceY));
			}
		}

		viewport.QueueFree();
		return panorama;
	}

	private Color SampleBilinear(Image image, float u, float v)
	{
		float x = Mathf.Clamp(u * FaceSize - 0.5f, 0.0f, FaceSize - 1.0f);
		float y = Mathf.Clamp(v * FaceSize - 0.5f, 0.0f, FaceSize - 1.0f);
		int x0 = (int)x;
		int y0 = (int)y;
		int x1 = Mathf.Min(x0 + 1, FaceSize - 1);
		int y1 = Mathf.Min(y0 + 1, FaceSize - 1);
		float xBlend = x - x0;
		float yBlend = y - y0;

		Color top = image.GetPixel(x0, y0).Lerp(image.GetPixel(x1, y0), xBlend);
		Color bottom = image.GetPixel(x0, y1).Lerp(image.GetPixel(x1, y1), xBlend);
		return top.Lerp(bottom, yBlend);
	}

	private int SelectFace(Vector3 direction)
	{
		float x = Mathf.Abs(direction.X);
		float y = Mathf.Abs(direction.Y);
		float z = Mathf.Abs(direction.Z);

		if (x >= y && x >= z)
			return direction.X >= 0.0f ? 0 : 1;
		if (y >= z)
			return direction.Y >= 0.0f ? 2 : 3;
		return direction.Z >= 0.0f ? 4 : 5;
	}
}
#else
public partial class SkyBaker : Node3D { }
#endif
