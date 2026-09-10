using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Godot;

public partial class ShaderPrecompilator : Node
{
    [Export]
    public RichTextLabel TextLabel;

    [Export]
    public MeshInstance3D[] MeshInstances;

    [Export]
    public string NextScene;

    [Export]
    public bool ScanTscnFiles = false;
    private List<Material> materials = new List<Material>();
    private List<string> resources = new List<string>();
    private Dictionary<string, ResourceLoadHint> resourceLoadHints = new Dictionary<
        string,
        ResourceLoadHint
    >(StringComparer.OrdinalIgnoreCase);
    private HashSet<ulong> queuedMaterialIds = new HashSet<ulong>();
    private int currentMaterial = 0;
    private int currentResource = 0;
    private string currentResourcePath = string.Empty;
    private readonly object materialScanStatusLock = new object();
    private int materialScanDisplayCurrentResource = 0;
    private int materialScanDisplayTotalResources = 0;
    private string materialScanDisplayCurrentResourcePath = string.Empty;
    private bool materialScanHadFailure = false;
    private int materialScanFailureCount = 0;
    private string materialScanFailureMessage = string.Empty;
    private double donePhaseDelayRemaining = 0.0;
    private string donePhaseStatusText = string.Empty;
    private const string ShaderPrecompileStatePath = "user://shader_precompile_state.txt";
    private const string ShaderPrecompileStateVersion = "2";
    private const string TempMaterialCacheDirectory = "user://shader_precompile_cache";
    private const ulong ScanLoadTimeoutMilliseconds = 15000;

    private static readonly Regex TextResourceRootPattern = new Regex(
        "^\\[gd_resource\\s+type=\"(?<type>[^\"]+)\"",
        RegexOptions.Multiline | RegexOptions.Compiled
    );
    private static readonly Regex TextResourceSubResourcePattern = new Regex(
        "^\\[sub_resource\\s+type=\"(?<type>[^\"]+)\"",
        RegexOptions.Multiline | RegexOptions.Compiled
    );
    private static readonly Regex TextResourceSubResourceHeaderPattern = new Regex(
        "^\\[sub_resource\\s+type=\"(?<type>[^\"]+)\".*?id=\"(?<id>[^\"]+)\".*\\]$",
        RegexOptions.Compiled
    );
    private static readonly Regex TextResourceExternalResourcePattern = new Regex(
        "^\\[ext_resource\\s+type=\"(?<type>[^\"]+)\".*?path=\"(?<path>[^\"]+)\"",
        RegexOptions.Multiline | RegexOptions.Compiled
    );
    private static readonly Regex ImportedScenePathPattern = new Regex(
        "^path=\"(?<path>[^\"]+)\"",
        RegexOptions.Multiline | RegexOptions.Compiled
    );
    private static readonly Regex ImportedMaterialFallbackPattern = new Regex(
        "\"use_external/enabled\":\\s*(?<enabled>true|false),\\s*\"use_external/fallback_path\":\\s*\"(?<path>[^\"]*)\"",
        RegexOptions.Singleline | RegexOptions.Compiled
    );
    private static readonly Regex SubResourceReferencePattern = new Regex(
        "SubResource\\(\"(?<id>[^\"]+)\"\\)",
        RegexOptions.Compiled
    );

    enum Phase
    {
        INIT,
        SCAN,
        MATERIALS,
        SHADERS,
        DONE,
    }

    enum ResourceLoadHint
    {
        Auto,
        Material,
        PackedScene,
        Mesh,
    }

    private sealed class TextSubResourceSection
    {
        public TextSubResourceSection(string typeName, string id, string headerLine)
        {
            TypeName = typeName;
            Id = id;
            HeaderLine = headerLine;
        }

        public string TypeName { get; }
        public string Id { get; }
        public string HeaderLine { get; }
        public List<string> BodyLines { get; } = new List<string>();
    }

    private int currentPhaseValue = (int)Phase.INIT;
    private Phase currentPhase
    {
        get => (Phase)Volatile.Read(ref currentPhaseValue);
        set => Volatile.Write(ref currentPhaseValue, (int)value);
    }

    public override void _Ready()
    {
        TextLabel.Text = "Loading...";
    }

    private bool IsScannableResource(string file)
    {
        return IsTextResource(file) || IsBinaryResource(file) || IsImportedSceneSource(file);
    }

    private bool IsTextSceneResource(string path)
    {
        return path.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase);
    }

    private bool ShouldScanResourcePath(string path)
    {
        return ScanTscnFiles || !IsTextSceneResource(path);
    }

    private bool IsTextResource(string path)
    {
        return path.EndsWith(".tres", StringComparison.OrdinalIgnoreCase)
            || (ScanTscnFiles && IsTextSceneResource(path));
    }

    private bool IsBinaryResource(string path)
    {
        return path.EndsWith(".res", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".scn", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsImportedSceneSource(string path)
    {
        return path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".blend", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".dae", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsMaterialTypeName(string typeName)
    {
        return typeName.Contains("Material", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsSceneTypeName(string typeName)
    {
        return typeName.Equals("PackedScene", StringComparison.OrdinalIgnoreCase)
            || typeName.EndsWith("Scene", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsMeshTypeName(string typeName)
    {
        return typeName.Contains("Mesh", StringComparison.OrdinalIgnoreCase);
    }

    private string CombineResourcePath(string directoryPath, string entryName)
    {
        return directoryPath.EndsWith("/", StringComparison.Ordinal)
            ? directoryPath + entryName
            : directoryPath + "/" + entryName;
    }

    private string GetDependencyPath(string dependency)
    {
        if (string.IsNullOrEmpty(dependency))
            return string.Empty;

        int separatorIndex = dependency.LastIndexOf("::", StringComparison.Ordinal);
        return separatorIndex >= 0 ? dependency[(separatorIndex + 2)..] : dependency;
    }

    private bool TryQueueDependencyResources(string path)
    {
        bool queuedAny = false;

        foreach (string dependency in ResourceLoader.GetDependencies(path))
        {
            string dependencyPath = GetDependencyPath(dependency);
            if (string.IsNullOrEmpty(dependencyPath) || !IsScannableResource(dependencyPath))
                continue;

            if (TryQueueResourcePathIfExists(dependencyPath))
            {
                queuedAny = true;
            }
        }

        return queuedAny;
    }

    private bool ResourcePathExists(string path, ResourceLoadHint hint = ResourceLoadHint.Auto)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        ResourceLoadHint resolvedHint =
            hint == ResourceLoadHint.Auto ? GetDefaultLoadHint(path) : hint;
        string typeHint = GetLoadTypeHint(resolvedHint);

        return (!string.IsNullOrEmpty(typeHint) && ResourceLoader.Exists(path, typeHint))
            || ResourceLoader.Exists(path);
    }

    private bool TryQueueResourcePathIfExists(
        string path,
        ResourceLoadHint hint = ResourceLoadHint.Auto
    )
    {
        if (!ResourcePathExists(path, hint))
            return false;

        QueueResourcePath(path, hint);
        return true;
    }

    private bool ShouldSkipDirectory(string path)
    {
        return path.Equals("res://.godot", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("res://.godot/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("res://android", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("res://android/", StringComparison.OrdinalIgnoreCase);
    }

    private ResourceLoadHint GetDefaultLoadHint(string path)
    {
        if (
            path.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".scn", StringComparison.OrdinalIgnoreCase)
        )
            return ResourceLoadHint.PackedScene;

        return ResourceLoadHint.Auto;
    }

    private ResourceLoadHint MergeLoadHint(ResourceLoadHint currentHint, ResourceLoadHint newHint)
    {
        if (newHint == ResourceLoadHint.Auto || newHint == currentHint)
            return currentHint;

        if (currentHint == ResourceLoadHint.Auto)
            return newHint;

        return currentHint;
    }

    private void QueueResourcePath(string path, ResourceLoadHint hint = ResourceLoadHint.Auto)
    {
        if (string.IsNullOrEmpty(path) || !ShouldScanResourcePath(path))
            return;

        if (resourceLoadHints.TryGetValue(path, out ResourceLoadHint currentHint))
        {
            resourceLoadHints[path] = MergeLoadHint(currentHint, hint);
            return;
        }

        resourceLoadHints[path] = hint == ResourceLoadHint.Auto ? GetDefaultLoadHint(path) : hint;
        resources.Add(path);
        UpdateMaterialScanProgress();
    }

    private string TryReadTextFile(string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
            return null;

        return file.GetAsText();
    }

    private string GetStableHash(string text)
    {
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 1099511628211UL;
            }

            return hash.ToString("x16");
        }
    }

    private string GetTempMaterialResourcePath(string sourcePath, string materialId)
    {
        return TempMaterialCacheDirectory
            + "/"
            + GetStableHash(sourcePath)
            + "_"
            + materialId
            + ".tres";
    }

    private bool TryWriteTextFile(string path, string contents)
    {
        Error dirError = DirAccess.MakeDirRecursiveAbsolute(path.GetBaseDir());
        if (dirError != Error.Ok && dirError != Error.AlreadyExists)
            return false;

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
            return false;

        file.StoreString(contents);
        return true;
    }

    private string GetShaderPrecompileStateKey()
    {
        string gameVersion = ProjectSettings
            .GetSetting("application/config/version", "0.0.0")
            .AsString();
        return ShaderPrecompileStateVersion + "|" + gameVersion;
    }

    private bool HasCompletedShaderPrecompile()
    {
        if (!FileAccess.FileExists(ShaderPrecompileStatePath))
            return false;

        try
        {
            using var file = FileAccess.Open(ShaderPrecompileStatePath, FileAccess.ModeFlags.Read);
            if (file == null)
                return false;

            return string.Equals(
                file.GetAsText().StripEdges(),
                GetShaderPrecompileStateKey(),
                StringComparison.Ordinal
            );
        }
        catch (Exception exception)
        {
            GD.PushWarning("Failed to read shader precompile state: " + exception.Message);
            return false;
        }
    }

    private void StoreCompletedShaderPrecompile()
    {
        try
        {
            using var file = FileAccess.Open(ShaderPrecompileStatePath, FileAccess.ModeFlags.Write);
            if (file == null)
                return;

            file.StoreString(GetShaderPrecompileStateKey());
        }
        catch (Exception exception)
        {
            GD.PushWarning("Failed to store shader precompile state: " + exception.Message);
        }
    }

    private void ResetMaterialScanState()
    {
        materials.Clear();
        resources.Clear();
        resourceLoadHints.Clear();
        queuedMaterialIds.Clear();
        currentMaterial = 0;
        currentResource = 0;
        currentResourcePath = string.Empty;

        lock (materialScanStatusLock)
        {
            materialScanDisplayCurrentResource = 0;
            materialScanDisplayTotalResources = 0;
            materialScanDisplayCurrentResourcePath = string.Empty;
            materialScanHadFailure = false;
            materialScanFailureCount = 0;
            materialScanFailureMessage = string.Empty;
        }

        donePhaseDelayRemaining = 0.0;
        donePhaseStatusText = string.Empty;
    }

    private void UpdateMaterialScanProgress()
    {
        lock (materialScanStatusLock)
        {
            materialScanDisplayCurrentResource = currentResource;
            materialScanDisplayTotalResources = resources.Count;
            materialScanDisplayCurrentResourcePath = currentResourcePath;
        }
    }

    private void GetMaterialScanProgress(
        out int scannedResources,
        out int totalResources,
        out string scanningPath
    )
    {
        lock (materialScanStatusLock)
        {
            scannedResources = materialScanDisplayCurrentResource;
            totalResources = materialScanDisplayTotalResources;
            scanningPath = materialScanDisplayCurrentResourcePath;
        }
    }

    private void RecordMaterialScanFailure(Exception exception, string path = "")
    {
        string failureMessage = string.IsNullOrEmpty(path)
            ? exception.Message
            : path + ": " + exception.Message;

        lock (materialScanStatusLock)
        {
            materialScanHadFailure = true;
            materialScanFailureCount++;
            materialScanFailureMessage = failureMessage;
        }
    }

    private bool TryGetMaterialScanFailure(out string failureMessage, out int failureCount)
    {
        lock (materialScanStatusLock)
        {
            failureMessage = materialScanFailureMessage;
            failureCount = materialScanFailureCount;
            return materialScanHadFailure;
        }
    }

    private string GetLoadTypeHint(ResourceLoadHint hint)
    {
        return hint switch
        {
            ResourceLoadHint.Material => "Material",
            ResourceLoadHint.PackedScene => "PackedScene",
            ResourceLoadHint.Mesh => "Mesh",
            _ => string.Empty,
        };
    }

    private Resource LoadResourceForScan(
        string path,
        ResourceLoadHint hint,
        ResourceLoader.CacheMode cacheMode = ResourceLoader.CacheMode.Reuse
    )
    {
        ulong startTicks = Time.GetTicksMsec();
        Error requestError = ResourceLoader.LoadThreadedRequest(
            path,
            GetLoadTypeHint(hint),
            false,
            cacheMode
        );
        if (requestError != Error.Ok)
        {
            throw new InvalidOperationException(
                "Threaded resource load request failed with " + requestError + "."
            );
        }

        while (true)
        {
            switch (ResourceLoader.LoadThreadedGetStatus(path))
            {
                case ResourceLoader.ThreadLoadStatus.Loaded:
                    Resource resource = ResourceLoader.LoadThreadedGet(path);
                    if (resource == null)
                    {
                        throw new InvalidOperationException(
                            "Threaded resource load returned null."
                        );
                    }

                    return resource;
                case ResourceLoader.ThreadLoadStatus.Failed:
                    throw new InvalidOperationException("Threaded resource load failed.");
                case ResourceLoader.ThreadLoadStatus.InvalidResource:
                    throw new InvalidOperationException(
                        "Threaded resource load reported an invalid resource."
                    );
                case ResourceLoader.ThreadLoadStatus.InProgress:
                    if (Time.GetTicksMsec() - startTicks > ScanLoadTimeoutMilliseconds)
                    {
                        throw new InvalidOperationException(
                            "Threaded resource load timed out after "
                                + ScanLoadTimeoutMilliseconds
                                + " ms."
                        );
                    }

                    Thread.Yield();
                    break;
                default:
                    throw new InvalidOperationException(
                        "Threaded resource load returned an unexpected status."
                    );
            }
        }
    }

    private void BeginDonePhase(string statusText = "Loading...", double delaySeconds = 0.0)
    {
        donePhaseStatusText = statusText;
        donePhaseDelayRemaining = Math.Max(0.0, delaySeconds);
        currentPhase = Phase.DONE;
    }

    private void StartMaterialScanThread()
    {
        Task.Run(() =>
        {
            try
            {
                ScanDirectory("res://");
                UpdateMaterialScanProgress();

                while (currentResource < resources.Count)
                {
                    currentResourcePath = resources[currentResource];
                    UpdateMaterialScanProgress();

                    try
                    {
                        ProcessResourcePath(currentResourcePath);
                    }
                    catch (Exception exception)
                    {
                        RecordMaterialScanFailure(exception, currentResourcePath);
                    }

                    currentResource++;
                }

                currentResourcePath = string.Empty;
                UpdateMaterialScanProgress();
            }
            catch (Exception exception)
            {
                RecordMaterialScanFailure(exception);
            }
            finally
            {
                currentPhase = Phase.SHADERS;
            }
        });
    }

    private Dictionary<string, TextSubResourceSection> ParseTextSubResources(
        string resourceText,
        out List<TextSubResourceSection> materialSections,
        out List<string> externalResourceLines
    )
    {
        materialSections = new List<TextSubResourceSection>();
        externalResourceLines = new List<string>();
        Dictionary<string, TextSubResourceSection> subResources = new Dictionary<
            string,
            TextSubResourceSection
        >(StringComparer.Ordinal);
        TextSubResourceSection currentSection = null;

        foreach (string rawLine in resourceText.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');

            if (line.StartsWith("[ext_resource ", StringComparison.Ordinal))
            {
                currentSection = null;
                externalResourceLines.Add(line);
                continue;
            }

            Match subResourceHeaderMatch = TextResourceSubResourceHeaderPattern.Match(line);
            if (subResourceHeaderMatch.Success)
            {
                currentSection = new TextSubResourceSection(
                    subResourceHeaderMatch.Groups["type"].Value,
                    subResourceHeaderMatch.Groups["id"].Value,
                    line
                );

                subResources[currentSection.Id] = currentSection;
                if (IsMaterialTypeName(currentSection.TypeName))
                {
                    materialSections.Add(currentSection);
                }

                continue;
            }

            if (line.StartsWith("[", StringComparison.Ordinal))
            {
                currentSection = null;
                continue;
            }

            currentSection?.BodyLines.Add(line);
        }

        return subResources;
    }

    private bool CollectReferencedSubResources(
        TextSubResourceSection section,
        Dictionary<string, TextSubResourceSection> subResources,
        HashSet<string> visitedIds,
        List<TextSubResourceSection> orderedSections
    )
    {
        foreach (string line in section.BodyLines)
        {
            foreach (Match match in SubResourceReferencePattern.Matches(line))
            {
                string referencedId = match.Groups["id"].Value;
                if (string.IsNullOrEmpty(referencedId) || !visitedIds.Add(referencedId))
                    continue;

                if (
                    !subResources.TryGetValue(
                        referencedId,
                        out TextSubResourceSection dependencySection
                    )
                )
                    return false;

                if (
                    !CollectReferencedSubResources(
                        dependencySection,
                        subResources,
                        visitedIds,
                        orderedSections
                    )
                )
                    return false;

                orderedSections.Add(dependencySection);
            }
        }

        return true;
    }

    private bool TryBuildStandaloneMaterialText(
        TextSubResourceSection materialSection,
        Dictionary<string, TextSubResourceSection> subResources,
        List<string> externalResourceLines,
        out string materialText
    )
    {
        List<TextSubResourceSection> dependencySections = new List<TextSubResourceSection>();
        if (
            !CollectReferencedSubResources(
                materialSection,
                subResources,
                new HashSet<string>(StringComparer.Ordinal),
                dependencySections
            )
        )
        {
            materialText = null;
            return false;
        }

        StringBuilder builder = new StringBuilder();
        int loadSteps = Math.Max(1, 1 + externalResourceLines.Count + dependencySections.Count);
        builder.Append("[gd_resource type=\"");
        builder.Append(materialSection.TypeName);
        builder.Append("\" load_steps=");
        builder.Append(loadSteps);
        builder.AppendLine(" format=3]");
        builder.AppendLine();

        foreach (string externalResourceLine in externalResourceLines)
        {
            builder.AppendLine(externalResourceLine);
        }

        if (externalResourceLines.Count > 0)
        {
            builder.AppendLine();
        }

        foreach (TextSubResourceSection dependencySection in dependencySections)
        {
            builder.AppendLine(dependencySection.HeaderLine);
            foreach (string bodyLine in dependencySection.BodyLines)
            {
                builder.AppendLine(bodyLine);
            }
            builder.AppendLine();
        }

        builder.AppendLine("[resource]");
        foreach (string bodyLine in materialSection.BodyLines)
        {
            builder.AppendLine(bodyLine);
        }

        materialText = builder.ToString();
        return true;
    }

    private bool TryLoadBuiltInMaterialsFromTextResource(string path, string resourceText)
    {
        Dictionary<string, TextSubResourceSection> subResources = ParseTextSubResources(
            resourceText,
            out List<TextSubResourceSection> materialSections,
            out List<string> externalResourceLines
        );
        if (materialSections.Count == 0)
            return false;

        Dictionary<TextSubResourceSection, string> materialTexts =
            new Dictionary<TextSubResourceSection, string>();
        foreach (TextSubResourceSection materialSection in materialSections)
        {
            if (
                !TryBuildStandaloneMaterialText(
                    materialSection,
                    subResources,
                    externalResourceLines,
                    out string materialText
                )
            )
                return false;

            materialTexts[materialSection] = materialText;
        }

        List<Material> loadedMaterials = new List<Material>();

        foreach (KeyValuePair<TextSubResourceSection, string> materialEntry in materialTexts)
        {
            string tempMaterialPath = GetTempMaterialResourcePath(path, materialEntry.Key.Id);
            if (!TryWriteTextFile(tempMaterialPath, materialEntry.Value))
                return false;

            Material material;
            try
            {
                material =
                    LoadResourceForScan(
                        tempMaterialPath,
                        ResourceLoadHint.Material,
                        ResourceLoader.CacheMode.IgnoreDeep
                    ) as Material;
            }
            catch (Exception)
            {
                return false;
            }

            if (material == null)
                return false;

            loadedMaterials.Add(material);
        }

        foreach (Material material in loadedMaterials)
        {
            QueueMaterial(material);
        }

        return true;
    }

    private bool TryProcessImportedSceneSource(string path)
    {
        if (!IsImportedSceneSource(path))
            return false;

        string importText = TryReadTextFile(path + ".import");
        if (string.IsNullOrEmpty(importText))
            return TryQueueDependencyResources(path);

        Match importedSceneMatch = ImportedScenePathPattern.Match(importText);
        string importedScenePath = importedSceneMatch.Success
            ? importedSceneMatch.Groups["path"].Value
            : null;

        bool hasMaterialMappings = importText.Contains("\"materials\":", StringComparison.Ordinal);
        bool sawMappedMaterial = false;
        bool allMappedMaterialsExternal = hasMaterialMappings;

        foreach (Match match in ImportedMaterialFallbackPattern.Matches(importText))
        {
            sawMappedMaterial = true;

            bool usesExternalMaterial = string.Equals(
                match.Groups["enabled"].Value,
                "true",
                StringComparison.OrdinalIgnoreCase
            );
            string fallbackPath = match.Groups["path"].Value;

            if (usesExternalMaterial && !string.IsNullOrEmpty(fallbackPath))
            {
                TryQueueResourcePathIfExists(fallbackPath, ResourceLoadHint.Material);
            }
            else
            {
                allMappedMaterialsExternal = false;
            }
        }

        if (!hasMaterialMappings || !sawMappedMaterial || !allMappedMaterialsExternal)
        {
            if (!string.IsNullOrEmpty(importedScenePath))
            {
                TryQueueResourcePathIfExists(importedScenePath, ResourceLoadHint.PackedScene);
            }
        }

        return true;
    }

    private bool TryProcessTextResource(string path)
    {
        if (!IsTextResource(path))
            return false;

        string resourceText = TryReadTextFile(path);
        if (string.IsNullOrEmpty(resourceText))
            return false;

        Match rootResourceMatch = TextResourceRootPattern.Match(resourceText);
        if (rootResourceMatch.Success && IsMaterialTypeName(rootResourceMatch.Groups["type"].Value))
        {
            LoadResourceAndCollectMaterials(path, ResourceLoadHint.Material);
            return true;
        }

        foreach (Match match in TextResourceExternalResourcePattern.Matches(resourceText))
        {
            string dependencyType = match.Groups["type"].Value;
            string dependencyPath = match.Groups["path"].Value;

            if (IsMaterialTypeName(dependencyType))
            {
                QueueResourcePath(dependencyPath, ResourceLoadHint.Material);
            }
            else if (IsSceneTypeName(dependencyType))
            {
                QueueResourcePath(dependencyPath, ResourceLoadHint.PackedScene);
            }
            else if (IsMeshTypeName(dependencyType))
            {
                QueueResourcePath(dependencyPath, ResourceLoadHint.Mesh);
            }
        }

        foreach (Match match in TextResourceSubResourcePattern.Matches(resourceText))
        {
            if (!IsMaterialTypeName(match.Groups["type"].Value))
                continue;

            if (TryLoadBuiltInMaterialsFromTextResource(path, resourceText))
                return true;

            LoadResourceAndCollectMaterials(path, GetDefaultLoadHint(path));
            return true;
        }

        return true;
    }

    private void LoadResourceAndCollectMaterials(string path, ResourceLoadHint hint)
    {
        Resource resource = LoadResourceForScan(path, hint);
        CollectMaterialsFromResource(resource);
    }

    private void ProcessResourcePath(string path)
    {
        if (!ShouldScanResourcePath(path))
            return;

        if (TryProcessImportedSceneSource(path))
            return;

        if (TryProcessTextResource(path))
            return;

        ResourceLoadHint hint = resourceLoadHints.TryGetValue(path, out ResourceLoadHint queuedHint)
            ? queuedHint
            : GetDefaultLoadHint(path);

        LoadResourceAndCollectMaterials(path, hint);
    }

    private void QueueMaterial(Material material)
    {
        if (material == null)
            return;

        ulong materialId = material.GetInstanceId();
        if (!queuedMaterialIds.Add(materialId))
            return;

        materials.Add(material);
    }

    private void CollectMaterialsFromResource(Resource resource)
    {
        if (resource == null)
            return;

        CollectMaterialsFromObject(resource, new HashSet<ulong>());
    }

    private void CollectMaterialsFromObject(GodotObject godotObject, HashSet<ulong> visitedObjects)
    {
        if (godotObject == null)
            return;

        ulong objectId = godotObject.GetInstanceId();
        if (!visitedObjects.Add(objectId))
            return;

        if (godotObject is Material material)
        {
            QueueMaterial(material);
        }

        if (godotObject is PackedScene packedScene)
        {
            CollectMaterialsFromSceneState(packedScene.GetState(), visitedObjects);
            return;
        }

        if (godotObject is Mesh mesh)
        {
            CollectMaterialsFromMesh(mesh, visitedObjects);
            return;
        }

        if (godotObject is not Resource)
            return;

        foreach (Godot.Collections.Dictionary property in godotObject.GetPropertyList())
        {
            if (!property.ContainsKey("name"))
                continue;

            string propertyName = property["name"].AsString();
            if (string.IsNullOrEmpty(propertyName) || propertyName == "script")
                continue;

            try
            {
                CollectMaterialsFromVariant(godotObject.Get(propertyName), visitedObjects);
            }
            catch (Exception)
            {
                // Ignore properties that cannot be read and keep scanning the rest.
            }
        }
    }

    private void CollectMaterialsFromSceneState(
        SceneState sceneState,
        HashSet<ulong> visitedObjects
    )
    {
        if (sceneState == null)
            return;

        SceneState baseSceneState = sceneState.GetBaseSceneState();
        if (baseSceneState != null)
        {
            CollectMaterialsFromSceneState(baseSceneState, visitedObjects);
        }

        for (int nodeIndex = 0; nodeIndex < sceneState.GetNodeCount(); nodeIndex++)
        {
            if (sceneState.GetNodeInstance(nodeIndex) is PackedScene childScene)
            {
                CollectMaterialsFromObject(childScene, visitedObjects);
            }

            for (
                int propertyIndex = 0;
                propertyIndex < sceneState.GetNodePropertyCount(nodeIndex);
                propertyIndex++
            )
            {
                CollectMaterialsFromVariant(
                    sceneState.GetNodePropertyValue(nodeIndex, propertyIndex),
                    visitedObjects
                );
            }
        }
    }

    private void CollectMaterialsFromMesh(Mesh mesh, HashSet<ulong> visitedObjects)
    {
        for (int surfaceIndex = 0; surfaceIndex < mesh.GetSurfaceCount(); surfaceIndex++)
        {
            CollectMaterialsFromObject(mesh.SurfaceGetMaterial(surfaceIndex), visitedObjects);
        }
    }

    private void CollectMaterialsFromVariant(Variant value, HashSet<ulong> visitedObjects)
    {
        switch (value.Obj)
        {
            case null:
                return;
            case Godot.Collections.Array array:
                foreach (Variant item in array)
                {
                    CollectMaterialsFromVariant(item, visitedObjects);
                }
                return;
            case Godot.Collections.Dictionary dictionary:
                foreach (Variant item in dictionary.Values)
                {
                    CollectMaterialsFromVariant(item, visitedObjects);
                }
                return;
            case GodotObject godotObject:
                CollectMaterialsFromObject(godotObject, visitedObjects);
                return;
        }
    }

    private void ScanDirectory(string path)
    {
        try
        {
            foreach (string entry in ResourceLoader.ListDirectory(path))
            {
                if (string.IsNullOrEmpty(entry))
                    continue;

                bool isDirectory = entry.EndsWith("/", StringComparison.Ordinal);
                string name = isDirectory ? entry[..^1] : entry;
                if (string.IsNullOrEmpty(name) || name[0] == '.')
                    continue;

                string fullPath = CombineResourcePath(path, name);

                if (isDirectory)
                {
                    if (ShouldSkipDirectory(fullPath))
                        continue;

                    ScanDirectory(fullPath);
                }
                else if (IsScannableResource(name))
                {
                    QueueResourcePath(fullPath);
                }
            }

            return;
        }
        catch (Exception) { }

        using var dir = DirAccess.Open(path);

        if (dir == null)
            return;

        dir.ListDirBegin();

        while (true)
        {
            string file = dir.GetNext();

            if (file == "")
                break;

            if (file[0] == '.')
                continue;

            string fullPath = CombineResourcePath(path, file);

            if (dir.CurrentIsDir())
            {
                if (ShouldSkipDirectory(fullPath))
                    continue;

                ScanDirectory(fullPath);
            }
            else if (IsScannableResource(file))
            {
                QueueResourcePath(fullPath);
            }
        }

        dir.ListDirEnd();
    }

    public override void _Process(double delta)
    {
        switch (currentPhase)
        {
            case Phase.INIT:
            {
                TextLabel.Text = "Scanning resources...";
                currentPhase = Phase.SCAN;
                break;
            }
            case Phase.SCAN:
            {
                if (HasCompletedShaderPrecompile())
                {
                    ResourceLoader.LoadThreadedRequest(NextScene);
                    TextLabel.Text = "Loading...";
                    BeginDonePhase();
                    break;
                }

                ResetMaterialScanState();
                ResourceLoader.LoadThreadedRequest(NextScene);
                currentPhase = Phase.MATERIALS;
                StartMaterialScanThread();

                TextLabel.Text = "Scanning materials...";
                break;
            }
            case Phase.MATERIALS:
            {
                GetMaterialScanProgress(
                    out int scannedResources,
                    out int totalResources,
                    out string scanningPath
                );
                bool materialScanFailed = TryGetMaterialScanFailure(
                    out string failureMessage,
                    out int failureCount
                );
                string failureSuffix = materialScanFailed
                    ? "\nSkipped: " + failureCount + "\nLast failure: " + failureMessage
                    : string.Empty;
                TextLabel.Text =
                    "Scanning materials... ("
                    + scannedResources
                    + "/"
                    + totalResources
                    + ")\n"
                    + scanningPath
                    + failureSuffix;
                break;
            }
            case Phase.SHADERS:
            {
                bool materialScanFailed = TryGetMaterialScanFailure(
                    out string failureMessage,
                    out int failureCount
                );

                if (materials.Count == 0)
                {
                    if (materialScanFailed)
                    {
                        GD.PushWarning(
                            "Shader precompilation scan skipped "
                                + failureCount
                                + " resource(s). Last failure: "
                                + failureMessage
                                + ". Skipping cached completion marker."
                        );
                        BeginDonePhase(
                            "Shader precompilation failed.\nSkipped "
                                + failureCount
                                + " resource(s).\nLast failure: "
                                + failureMessage,
                            2.0
                        );
                    }
                    else
                    {
                        GD.PushWarning(
                            "Shader precompilation found no materials; skipping cached completion marker."
                        );
                        BeginDonePhase("Shader precompilation found no materials.", 1.0);
                    }
                    break;
                }

                if (currentMaterial >= materials.Count)
                {
                    if (materialScanFailed)
                    {
                        GD.PushWarning(
                            "Shader precompilation scan skipped "
                                + failureCount
                                + " resource(s). Last failure: "
                                + failureMessage
                                + ". Skipping cached completion marker."
                        );
                        BeginDonePhase(
                            "Shader precompilation incomplete.\nCompiled "
                                + materials.Count
                                + " material(s).\nSkipped "
                                + failureCount
                                + " resource(s).\nLast failure: "
                                + failureMessage,
                            2.0
                        );
                    }
                    else
                    {
                        StoreCompletedShaderPrecompile();
                        BeginDonePhase();
                    }
                    break;
                }

                for (int i = 0; i < MeshInstances.Length; i++)
                {
                    if (currentMaterial >= materials.Count)
                        return;
                    int percentage = (int)((float)currentMaterial / this.materials.Count * 100);
                    string failureSuffix = materialScanFailed
                        ? "\nSkipped: " + failureCount
                        : string.Empty;
                    TextLabel.Text =
                        "Compiling shaders... ("
                        + percentage
                        + "%)\n"
                        + currentMaterial
                        + "/"
                        + this.materials.Count
                        + failureSuffix /*+ materials[currentMaterial].ResourcePath*/
                    ;
                    MeshInstances[i].MaterialOverride = materials[currentMaterial];
                    currentMaterial++;
                }

                break;
            }
            case Phase.DONE:
            {
                if (donePhaseDelayRemaining > 0.0)
                {
                    donePhaseDelayRemaining = Math.Max(0.0, donePhaseDelayRemaining - delta);
                    TextLabel.Text = donePhaseStatusText;
                    if (donePhaseDelayRemaining > 0.0)
                    {
                        break;
                    }
                }

                TextLabel.Text = "Loading...";
                if (
                    ResourceLoader.LoadThreadedGetStatus(NextScene)
                    == ResourceLoader.ThreadLoadStatus.Loaded
                )
                {
                    var nextLevelPackedScene = ResourceLoader.LoadThreadedGet(NextScene);
                    GetTree().ChangeSceneToPacked((PackedScene)nextLevelPackedScene);
                }
                break;
            }
        }
    }
}
