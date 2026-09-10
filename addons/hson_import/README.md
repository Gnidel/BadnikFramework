# HSON Import (Godot 4 addon)

Imports [HSON](https://github.com/hedge-dev/hson) layouts into Badnik Framework scenes,
using a vendored copy of [libHSON-csharp](https://github.com/hedge-dev/libHSON-csharp).

## Install

1. Copy the whole `addons/hson_import/` folder into your project's `res://addons/`.
2. Build the project (Godot needs to compile the C# once before the plugin shows up).
3. **Project > Project Settings > Plugins**, enable "HSON Import".
4. In any scene, **Add Child Node > HsonImportNode**. It'll appear with an import-style
   icon, no more manually attaching a script to a bare `Node`.

`libHSON-csharp` is vendored as source (under `addons/hson_import/libHSON/`, MIT-licensed,
`LICENSE.txt` included) rather than referenced as a compiled assembly. This is required,
not just convenient — see point 6 below.

## Usage

1. Select the `HsonImportNode`, set **Output** to the `Node3D` you want the layout spawned into.
2. Either paste HSON JSON into **Source Json**, or point **Source File Path** at a `.hson`
   file (the file path takes priority if both are set). You can use multiple HSON files
   in separate lines - the HSONs will be combined.
3. Fill in **Mapping** (HSON type -> prefab scene, lowercase keys) and **Path Mapping**
   (HSON `pathType` -> prefab scene, lowercase keys) as needed.
4. Click **Import HSON** in the inspector. **Clear Output** wipes the output node's children
   without re-importing.
