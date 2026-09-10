# Sky Baker

Sky Baker adds a `SkyBaker` node to the Godot editor. Add the node to a scene with a `WorldEnvironment`, configure its output paths, and click **Bake Sky Texture and Material**.

The baker captures the sky from six directions at `CapturePosition`, converts the result to a 2:1 equirectangular PNG, creates a `PanoramaSkyMaterial` resource using that texture, and saves a `Sky` resource with the material already assigned.

`CaptureNear` and `CaptureFar` control which level geometry is visible to the temporary capture camera. Set `CaptureFar` low enough to exclude nearby geometry when the sky is rendered from a finite-distance setup.

Enable `ApplyMaterialToWorldEnvironment` to assign the generated material to the source `WorldEnvironment` immediately after baking. The assignment is registered with the editor undo manager, so Ctrl+Z restores the previous sky material.

`SkyOutputPath` controls the ready-to-use `Sky` resource output. When applying the result to the source environment, the complete generated `Sky` is assigned and undo restores the previous `Sky` resource.