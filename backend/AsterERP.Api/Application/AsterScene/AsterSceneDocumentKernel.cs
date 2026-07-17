using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AsterERP.Contracts.AsterScene;

namespace AsterERP.Api.Application.AsterScene;

public static class AsterSceneDocumentKernel
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };
    private static readonly HashSet<string> AllowedAssetKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio",
        "decal",
        "document",
        "hdri",
        "image",
        "material",
        "mesh",
        "model",
        "panorama",
        "prefab",
        "preset",
        "texture",
        "video"
    };
    private static readonly HashSet<string> AllowedModifierTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "array",
        "bend",
        "boolean",
        "editPoly",
        "mirror",
        "shell",
        "subdivide",
        "taper",
        "twist",
        "uvwMap",
        "xform"
    };

    public static string CreateDefaultDocumentJson(string projectId, string projectName)
    {
        var document = new
        {
            meta = new
            {
                product = "AsterScene",
                title = projectName,
                createdAt = DateTime.UtcNow,
                updatedAt = DateTime.UtcNow
            },
            revision = 1,
            identity = new
            {
                projectId,
                documentId = $"doc_{Guid.NewGuid():N}",
                locale = "zh-CN"
            },
            assets = Array.Empty<object>(),
            actors = new[]
            {
                new
                {
                    id = "actor_floor",
                    name = "Floor",
                    type = "mesh",
                    components = new[] { "transform_floor", "mesh_floor", "material_floor" }
                }
            },
            components = new object[]
            {
                new
                {
                    id = "transform_floor",
                    type = "transform",
                    position = new { x = 0, y = -0.05, z = 0 },
                    rotation = new { x = 0, y = 0, z = 0 },
                    scale = new { x = 6, y = 0.1, z = 6 }
                },
                new
                {
                    id = "mesh_floor",
                    type = "mesh",
                    geometryId = "geo_floor",
                    castShadow = false,
                    receiveShadow = true
                },
                new
                {
                    id = "material_floor",
                    type = "materialBinding",
                    materialId = "mat_default"
                }
            },
            materials = new[]
            {
                new
                {
                    id = "mat_default",
                    name = "Default Matte",
                    type = "pbr",
                    baseColor = "#8a9099",
                    roughness = 0.72,
                    metallic = 0
                }
            },
            geometries = new[]
            {
                new
                {
                    id = "geo_floor",
                    type = "box",
                    parameters = new { width = 1, height = 1, depth = 1 }
                }
            },
            uv = new
            {
                layouts = Array.Empty<object>()
            },
            interactions = new
            {
                hotspots = Array.Empty<object>(),
                blueprints = Array.Empty<object>(),
                nav = new { mode = "orbit", collision = true }
            },
            timeline = new
            {
                sequences = Array.Empty<object>(),
                tracks = Array.Empty<object>()
            },
            runtime = new
            {
                entrySceneId = "scene_main",
                camera = new
                {
                    mode = "orbit",
                    position = new { x = 4, y = 3, z = 6 },
                    target = new { x = 0, y = 0, z = 0 },
                    fov = 55
                },
                scenes = new[]
                {
                    new
                    {
                        id = "scene_main",
                        name = "Main Scene",
                        actors = new[] { "actor_floor" }
                    }
                }
            },
            publish = new
            {
                visibility = "Private",
                slug = (string?)null,
                license = "standard-remix"
            },
            quality = new
            {
                gates = new[] { "document", "asset", "security", "performance" },
                performanceBudget = new { mobileFps = 30, desktopFps = 45, firstInteractiveSeconds = 4 }
            },
            extensions = new Dictionary<string, object?>()
        };

        return JsonSerializer.Serialize(document, JsonOptions);
    }

    public static string NormalizeJson(JsonElement document)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            document.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    public static string ComputeHash(string json)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    public static AsterSceneValidationResultDto Validate(JsonElement document)
    {
        var result = new AsterSceneValidationResultDto();
        if (document.ValueKind != JsonValueKind.Object)
        {
            result.Errors.Add(Issue("DocumentRootInvalid", "$", "SceneDocument must be a JSON object."));
            result.IsValid = false;
            return result;
        }

        RequireObject(document, "meta", "$.meta", result);
        RequireObject(document, "identity", "$.identity", result);
        RequireArray(document, "assets", "$.assets", result);
        RequireArray(document, "actors", "$.actors", result);
        RequireArray(document, "components", "$.components", result);
        RequireArray(document, "materials", "$.materials", result);
        RequireArray(document, "geometries", "$.geometries", result);
        RequireObject(document, "uv", "$.uv", result);
        RequireObject(document, "interactions", "$.interactions", result);
        RequireObject(document, "timeline", "$.timeline", result);
        RequireObject(document, "runtime", "$.runtime", result);
        RequireObject(document, "publish", "$.publish", result);
        RequireObject(document, "quality", "$.quality", result);
        RequireObject(document, "extensions", "$.extensions", result);

        if (document.TryGetProperty("meta", out var meta))
        {
            if (meta.TryGetProperty("schemaVersion", out _))
            {
                result.Errors.Add(Issue("SchemaVersionRemoved", "$.meta.schemaVersion", "SceneDocument no longer accepts schemaVersion."));
            }

            if (!meta.TryGetProperty("product", out var product) ||
                !string.Equals(product.GetString(), "AsterScene", StringComparison.Ordinal))
            {
                result.Errors.Add(Issue("ProductInvalid", "$.meta.product", "SceneDocument product must be AsterScene."));
            }
        }

        if (!TryGetEntrySceneId(document, out var entrySceneId) || string.IsNullOrWhiteSpace(entrySceneId))
        {
            result.Errors.Add(Issue("EntrySceneRequired", "$.runtime.entrySceneId", "Runtime entrySceneId is required."));
        }

        if (document.TryGetProperty("runtime", out var runtime) &&
            runtime.TryGetProperty("scenes", out var scenes) &&
            scenes.ValueKind == JsonValueKind.Array)
        {
            if (scenes.GetArrayLength() == 0)
            {
                result.Errors.Add(Issue("SceneRequired", "$.runtime.scenes", "At least one runtime scene is required."));
            }
            else
            {
                ValidateSceneGraph(document, scenes, entrySceneId, result);
            }
        }

        ValidateAssetRefs(document, result);
        ValidateGeometries(document, result);
        ValidateMaterials(document, result);
        ValidateTimeline(document, result);
        ValidateHotspots(document, result);

        if (document.TryGetProperty("assets", out var assets) &&
            assets.ValueKind == JsonValueKind.Array &&
            assets.GetArrayLength() == 0)
        {
            result.Warnings.Add(Issue("AssetEmpty", "$.assets", "The document has no runtime assets yet.", "warning"));
        }

        result.IsValid = result.Errors.Count == 0;
        return result;
    }

    public static bool TryGetEntrySceneId(JsonElement document, out string entrySceneId)
    {
        entrySceneId = string.Empty;
        if (!document.TryGetProperty("runtime", out var runtime) ||
            !runtime.TryGetProperty("entrySceneId", out var value))
        {
            return false;
        }

        entrySceneId = value.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(entrySceneId);
    }

    private static void RequireObject(JsonElement document, string property, string path, AsterSceneValidationResultDto result)
    {
        if (!document.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            result.Errors.Add(Issue("ObjectRequired", path, $"{property} must be an object."));
        }
    }

    private static void RequireArray(JsonElement document, string property, string path, AsterSceneValidationResultDto result)
    {
        if (!document.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            result.Errors.Add(Issue("ArrayRequired", path, $"{property} must be an array."));
        }
    }

    private static void ValidateSceneGraph(
        JsonElement document,
        JsonElement scenes,
        string entrySceneId,
        AsterSceneValidationResultDto result)
    {
        var actorIds = ReadStringIdSet(document, "actors");
        var componentIds = ReadStringIdSet(document, "components");
        var materialIds = ReadStringIdSet(document, "materials");
        var geometryIds = ReadStringIdSet(document, "geometries");
        var sceneIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var scene in scenes.EnumerateArray())
        {
            if (!scene.TryGetProperty("id", out var sceneIdValue) ||
                sceneIdValue.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(sceneIdValue.GetString()))
            {
                result.Errors.Add(Issue("SceneIdRequired", "$.runtime.scenes", "Every runtime scene must have an id."));
                continue;
            }

            var sceneId = sceneIdValue.GetString()!;
            sceneIds.Add(sceneId);
            if (!scene.TryGetProperty("actors", out var sceneActors) || sceneActors.ValueKind != JsonValueKind.Array)
            {
                result.Errors.Add(Issue("SceneActorsRequired", "$.runtime.scenes[].actors", "Every runtime scene must declare actor ids."));
                continue;
            }

            foreach (var actorRef in sceneActors.EnumerateArray())
            {
                var actorId = actorRef.ValueKind == JsonValueKind.String ? actorRef.GetString() : null;
                if (string.IsNullOrWhiteSpace(actorId) || !actorIds.Contains(actorId))
                {
                    result.Errors.Add(Issue("SceneActorMissing", "$.runtime.scenes[].actors", $"Actor {actorId ?? "<empty>"} is referenced by a scene but is not declared."));
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(entrySceneId) && !sceneIds.Contains(entrySceneId))
        {
            result.Errors.Add(Issue("EntrySceneMissing", "$.runtime.entrySceneId", $"Entry scene {entrySceneId} does not exist in runtime.scenes."));
        }

        if (document.TryGetProperty("actors", out var actors) && actors.ValueKind == JsonValueKind.Array)
        {
            foreach (var actor in actors.EnumerateArray())
            {
                if (!actor.TryGetProperty("components", out var components) || components.ValueKind != JsonValueKind.Array)
                {
                    result.Errors.Add(Issue("ActorComponentsRequired", "$.actors[].components", "Every actor must declare component ids."));
                    continue;
                }

                foreach (var componentRef in components.EnumerateArray())
                {
                    var componentId = componentRef.ValueKind == JsonValueKind.String ? componentRef.GetString() : null;
                    if (string.IsNullOrWhiteSpace(componentId) || !componentIds.Contains(componentId))
                    {
                        result.Errors.Add(Issue("ActorComponentMissing", "$.actors[].components", $"Component {componentId ?? "<empty>"} is referenced by an actor but is not declared."));
                    }
                }
            }
        }

        ValidateComponentReferences(document, geometryIds, materialIds, result);
        ValidateSceneTypesAndEnvironment(document, sceneIds, result);
    }

    private static void ValidateSceneTypesAndEnvironment(
        JsonElement document,
        HashSet<string> sceneIds,
        AsterSceneValidationResultDto result)
    {
        var assetKinds = ReadAssetKindMap(document);
        if (!document.TryGetProperty("runtime", out var runtime) ||
            !runtime.TryGetProperty("scenes", out var scenes) ||
            scenes.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var scene in scenes.EnumerateArray())
        {
            var sceneId = ReadString(scene, "id");
            var sceneType = ReadString(scene, "type");
            if (!string.IsNullOrWhiteSpace(sceneType) &&
                !sceneType.Equals("model3d", StringComparison.OrdinalIgnoreCase) &&
                !sceneType.Equals("panorama720", StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add(Issue("SceneTypeInvalid", "$.runtime.scenes[].type", $"Scene {sceneId} has unsupported type {sceneType}."));
            }

            if (!sceneType.Equals("panorama720", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!scene.TryGetProperty("environment", out var environment) || environment.ValueKind != JsonValueKind.Object)
            {
                result.Errors.Add(Issue("PanoramaEnvironmentRequired", "$.runtime.scenes[].environment", $"Panorama scene {sceneId} requires environment."));
                continue;
            }

            var panoramaAssetId = ReadString(environment, "panoramaAssetId");
            if (string.IsNullOrWhiteSpace(panoramaAssetId))
            {
                result.Errors.Add(Issue("PanoramaAssetRequired", "$.runtime.scenes[].environment.panoramaAssetId", $"Panorama scene {sceneId} requires a panorama asset."));
                continue;
            }

            if (!assetKinds.TryGetValue(panoramaAssetId, out var kind) ||
                !kind.Equals("panorama", StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add(Issue("PanoramaAssetInvalid", "$.runtime.scenes[].environment.panoramaAssetId", $"Asset {panoramaAssetId} must be declared with kind panorama."));
            }
        }

        if (sceneIds.Count == 0)
        {
            result.Errors.Add(Issue("SceneRequired", "$.runtime.scenes", "At least one runtime scene is required."));
        }
    }

    private static void ValidateAssetRefs(JsonElement document, AsterSceneValidationResultDto result)
    {
        if (!document.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in assets.EnumerateArray())
        {
            var id = ReadString(asset, "id");
            var kind = ReadString(asset, "kind");
            if (string.IsNullOrWhiteSpace(id))
            {
                result.Errors.Add(Issue("AssetIdRequired", "$.assets[].id", "Every asset reference requires an id."));
                continue;
            }

            if (!ids.Add(id))
            {
                result.Errors.Add(Issue("AssetDuplicate", "$.assets[].id", $"Asset {id} is declared more than once."));
            }

            if (string.IsNullOrWhiteSpace(kind) || !AllowedAssetKinds.Contains(kind))
            {
                result.Errors.Add(Issue("AssetKindInvalid", "$.assets[].kind", $"Asset {id} has unsupported kind {kind}."));
            }
        }
    }

    private static void ValidateGeometries(JsonElement document, AsterSceneValidationResultDto result)
    {
        if (!document.TryGetProperty("geometries", out var geometries) || geometries.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var assetKinds = ReadAssetKindMap(document);
        foreach (var geometry in geometries.EnumerateArray())
        {
            var id = ReadString(geometry, "id");
            var type = ReadString(geometry, "type");
            if (!type.Equals("editableMesh", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!geometry.TryGetProperty("editableMeshRef", out var editableMeshRef) || editableMeshRef.ValueKind != JsonValueKind.Object)
            {
                result.Errors.Add(Issue("EditableMeshRefRequired", "$.geometries[].editableMeshRef", $"Editable mesh geometry {id} requires editableMeshRef."));
                continue;
            }

            var assetId = ReadString(editableMeshRef, "assetId");
            if (!string.IsNullOrWhiteSpace(assetId) &&
                (!assetKinds.TryGetValue(assetId, out var assetKind) || !assetKind.Equals("mesh", StringComparison.OrdinalIgnoreCase)))
            {
                result.Errors.Add(Issue("EditableMeshAssetInvalid", "$.geometries[].editableMeshRef.assetId", $"Editable mesh geometry {id} must reference a mesh asset."));
            }

            if (editableMeshRef.TryGetProperty("inline", out var inlinePayload))
            {
                ValidateInlineMesh(id, inlinePayload, result);
            }

            if (string.IsNullOrWhiteSpace(assetId) && !editableMeshRef.TryGetProperty("inline", out _))
            {
                result.Errors.Add(Issue("EditableMeshPayloadMissing", "$.geometries[].editableMeshRef", $"Editable mesh geometry {id} requires inline payload or mesh assetId."));
            }
        }
    }

    private static void ValidateInlineMesh(string geometryId, JsonElement payload, AsterSceneValidationResultDto result)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("vertices", out var vertices) ||
            vertices.ValueKind != JsonValueKind.Array ||
            !payload.TryGetProperty("faces", out var faces) ||
            faces.ValueKind != JsonValueKind.Array)
        {
            result.Errors.Add(Issue("EditableMeshPayloadInvalid", "$.geometries[].editableMeshRef.inline", $"Editable mesh geometry {geometryId} payload requires vertices and faces."));
            return;
        }

        if (vertices.GetArrayLength() < 3)
        {
            result.Errors.Add(Issue("EditableMeshVertexCountInvalid", "$.geometries[].editableMeshRef.inline.vertices", $"Editable mesh geometry {geometryId} needs at least three vertices."));
        }

        if (faces.GetArrayLength() < 1)
        {
            result.Errors.Add(Issue("EditableMeshFaceCountInvalid", "$.geometries[].editableMeshRef.inline.faces", $"Editable mesh geometry {geometryId} needs at least one face."));
        }

        var vertexCount = vertices.GetArrayLength();
        foreach (var face in faces.EnumerateArray())
        {
            if (!face.TryGetProperty("vertices", out var faceVertices) || faceVertices.ValueKind != JsonValueKind.Array || faceVertices.GetArrayLength() < 3)
            {
                result.Errors.Add(Issue("EditableMeshFaceInvalid", "$.geometries[].editableMeshRef.inline.faces[].vertices", $"Editable mesh geometry {geometryId} contains a face with fewer than three vertices."));
                continue;
            }

            foreach (var vertexRef in faceVertices.EnumerateArray())
            {
                if (!vertexRef.TryGetInt32(out var vertexIndex) || vertexIndex < 0 || vertexIndex >= vertexCount)
                {
                    result.Errors.Add(Issue("EditableMeshVertexRefInvalid", "$.geometries[].editableMeshRef.inline.faces[].vertices", $"Editable mesh geometry {geometryId} contains an invalid vertex index."));
                }
            }
        }
    }

    private static void ValidateMaterials(JsonElement document, AsterSceneValidationResultDto result)
    {
        if (!document.TryGetProperty("materials", out var materials) || materials.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var assetKinds = ReadAssetKindMap(document);
        foreach (var material in materials.EnumerateArray())
        {
            var materialId = ReadString(material, "id");
            if (!material.TryGetProperty("pbr", out var pbr) ||
                pbr.ValueKind != JsonValueKind.Object ||
                !pbr.TryGetProperty("textureSlots", out var textureSlots) ||
                textureSlots.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var slot in textureSlots.EnumerateObject())
            {
                if (slot.Value.ValueKind != JsonValueKind.Object)
                {
                    result.Errors.Add(Issue("MaterialTextureSlotInvalid", "$.materials[].pbr.textureSlots", $"Material {materialId} texture slot {slot.Name} must be an object."));
                    continue;
                }

                var assetId = ReadString(slot.Value, "assetId");
                if (string.IsNullOrWhiteSpace(assetId))
                {
                    continue;
                }

                if (!assetKinds.TryGetValue(assetId, out var kind) ||
                    (!kind.Equals("texture", StringComparison.OrdinalIgnoreCase) &&
                     !kind.Equals("image", StringComparison.OrdinalIgnoreCase) &&
                     !kind.Equals("hdri", StringComparison.OrdinalIgnoreCase)))
                {
                    result.Errors.Add(Issue("MaterialTextureAssetInvalid", "$.materials[].pbr.textureSlots[].assetId", $"Material {materialId} texture slot {slot.Name} references a non-texture asset."));
                }
            }
        }
    }

    private static void ValidateTimeline(JsonElement document, AsterSceneValidationResultDto result)
    {
        if (!document.TryGetProperty("timeline", out var timeline) ||
            timeline.ValueKind != JsonValueKind.Object ||
            !timeline.TryGetProperty("tracks", out var tracks) ||
            tracks.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var targetIds = ReadStringIdSet(document, "actors");
        targetIds.UnionWith(ReadStringIdSet(document, "materials"));
        if (document.TryGetProperty("interactions", out var interactions) &&
            interactions.TryGetProperty("hotspots", out var hotspots) &&
            hotspots.ValueKind == JsonValueKind.Array)
        {
            foreach (var hotspot in hotspots.EnumerateArray())
            {
                var hotspotId = ReadString(hotspot, "id");
                if (!string.IsNullOrWhiteSpace(hotspotId))
                {
                    targetIds.Add(hotspotId);
                }
            }
        }

        foreach (var track in tracks.EnumerateArray())
        {
            var trackId = ReadString(track, "id");
            var targetId = ReadString(track, "targetId");
            var property = ReadString(track, "property");
            if (string.IsNullOrWhiteSpace(trackId))
            {
                result.Errors.Add(Issue("TimelineTrackIdRequired", "$.timeline.tracks[].id", "Timeline track id is required."));
            }

            if (string.IsNullOrWhiteSpace(property))
            {
                result.Errors.Add(Issue("TimelineTrackPropertyRequired", "$.timeline.tracks[].property", $"Timeline track {trackId} property is required."));
            }

            if (string.IsNullOrWhiteSpace(targetId) || !targetIds.Contains(targetId))
            {
                result.Errors.Add(Issue("TimelineTrackTargetMissing", "$.timeline.tracks[].targetId", $"Timeline track {trackId} target {targetId} does not exist."));
            }

            if (!track.TryGetProperty("keyframes", out var keyframes) || keyframes.ValueKind != JsonValueKind.Array)
            {
                result.Errors.Add(Issue("TimelineKeyframesRequired", "$.timeline.tracks[].keyframes", $"Timeline track {trackId} requires keyframes."));
                continue;
            }

            foreach (var keyframe in keyframes.EnumerateArray())
            {
                if (!keyframe.TryGetProperty("frame", out var frame) || !frame.TryGetInt32(out var frameValue) || frameValue < 0)
                {
                    result.Errors.Add(Issue("TimelineKeyframeFrameInvalid", "$.timeline.tracks[].keyframes[].frame", $"Timeline track {trackId} contains an invalid frame."));
                }
            }
        }
    }

    private static void ValidateHotspots(JsonElement document, AsterSceneValidationResultDto result)
    {
        if (!document.TryGetProperty("interactions", out var interactions) ||
            !interactions.TryGetProperty("hotspots", out var hotspots) ||
            hotspots.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var sceneIds = document.TryGetProperty("runtime", out var runtime) &&
                       runtime.TryGetProperty("scenes", out var scenes) &&
                       scenes.ValueKind == JsonValueKind.Array
            ? scenes.EnumerateArray()
                .Select(item => ReadString(item, "id"))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : [];
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var hotspot in hotspots.EnumerateArray())
        {
            var id = ReadString(hotspot, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                result.Errors.Add(Issue("HotspotIdRequired", "$.interactions.hotspots[].id", "Every hotspot requires an id."));
                continue;
            }

            if (!ids.Add(id))
            {
                result.Errors.Add(Issue("HotspotDuplicate", "$.interactions.hotspots[].id", $"Hotspot {id} is declared more than once."));
            }

            var sceneId = ReadString(hotspot, "sceneId");
            if (!string.IsNullOrWhiteSpace(sceneId) && !sceneIds.Contains(sceneId))
            {
                result.Errors.Add(Issue("HotspotSceneMissing", "$.interactions.hotspots[].sceneId", $"Hotspot {id} references missing scene {sceneId}."));
            }

            var type = ReadString(hotspot, "type");
            if (string.IsNullOrWhiteSpace(type))
            {
                type = "navigate";
            }

            if (!new[] { "navigate", "info", "media", "url", "action" }.Contains(type, StringComparer.OrdinalIgnoreCase))
            {
                result.Errors.Add(Issue("HotspotTypeInvalid", "$.interactions.hotspots[].type", $"Hotspot {id} has unsupported type {type}."));
            }

            if (type.Equals("navigate", StringComparison.OrdinalIgnoreCase))
            {
                var target = ReadString(hotspot, "target");
                if (string.IsNullOrWhiteSpace(target) || !sceneIds.Contains(target))
                {
                    result.Errors.Add(Issue("HotspotTargetMissing", "$.interactions.hotspots[].target", $"Hotspot {id} target scene is missing."));
                }
            }

            if (type.Equals("url", StringComparison.OrdinalIgnoreCase) &&
                hotspot.TryGetProperty("payload", out var payload) &&
                payload.TryGetProperty("url", out var url) &&
                url.ValueKind == JsonValueKind.String &&
                !IsSafeHotspotUrl(url.GetString()))
            {
                result.Errors.Add(Issue("HotspotUrlDenied", "$.interactions.hotspots[].payload.url", $"Hotspot {id} URL is not allowed."));
            }
        }
    }

    private static void ValidateComponentReferences(
        JsonElement document,
        HashSet<string> geometryIds,
        HashSet<string> materialIds,
        AsterSceneValidationResultDto result)
    {
        if (!document.TryGetProperty("components", out var components) || components.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var component in components.EnumerateArray())
        {
            if (component.TryGetProperty("geometryId", out var geometryIdValue))
            {
                var geometryId = geometryIdValue.GetString();
                if (string.IsNullOrWhiteSpace(geometryId) || !geometryIds.Contains(geometryId))
                {
                    result.Errors.Add(Issue("GeometryMissing", "$.components[].geometryId", $"Geometry {geometryId ?? "<empty>"} is referenced but is not declared."));
                }
            }

            if (component.TryGetProperty("materialId", out var materialIdValue))
            {
                var materialId = materialIdValue.GetString();
                if (string.IsNullOrWhiteSpace(materialId) || !materialIds.Contains(materialId))
                {
                    result.Errors.Add(Issue("MaterialMissing", "$.components[].materialId", $"Material {materialId ?? "<empty>"} is referenced but is not declared."));
                }
            }

            if (ReadString(component, "type").Equals("materialSlots", StringComparison.OrdinalIgnoreCase) &&
                component.TryGetProperty("slots", out var slots) &&
                slots.ValueKind == JsonValueKind.Array)
            {
                foreach (var slot in slots.EnumerateArray())
                {
                    var slotMaterialId = ReadString(slot, "materialId");
                    if (string.IsNullOrWhiteSpace(slotMaterialId) || !materialIds.Contains(slotMaterialId))
                    {
                        result.Errors.Add(Issue("MaterialMissing", "$.components[].slots[].materialId", $"Material slot references missing material {slotMaterialId ?? "<empty>"}."));
                    }
                }
            }

            if (ReadString(component, "type").Equals("modifierStack", StringComparison.OrdinalIgnoreCase))
            {
                ValidateModifierStack(component, result);
            }
        }
    }

    private static void ValidateModifierStack(JsonElement component, AsterSceneValidationResultDto result)
    {
        if (!component.TryGetProperty("modifiers", out var modifiers) || modifiers.ValueKind != JsonValueKind.Array)
        {
            result.Errors.Add(Issue("ModifierStackRequired", "$.components[].modifiers", "Modifier stack component requires modifiers array."));
            return;
        }

        foreach (var modifier in modifiers.EnumerateArray())
        {
            var id = ReadString(modifier, "id");
            var type = ReadString(modifier, "type");
            if (string.IsNullOrWhiteSpace(id))
            {
                result.Errors.Add(Issue("ModifierIdRequired", "$.components[].modifiers[].id", "Modifier id is required."));
            }

            if (string.IsNullOrWhiteSpace(type) || !AllowedModifierTypes.Contains(type))
            {
                result.Errors.Add(Issue("ModifierTypeInvalid", "$.components[].modifiers[].type", $"Modifier {id} has unsupported type {type}."));
            }
        }
    }

    private static HashSet<string> ReadStringIdSet(JsonElement document, string property)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!document.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
            {
                var value = id.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result.Add(value);
                }
            }
        }

        return result;
    }

    private static Dictionary<string, string> ReadAssetKindMap(JsonElement document)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!document.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            var id = ReadString(asset, "id");
            var kind = ReadString(asset, "kind");
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(kind))
            {
                result[id] = kind;
            }
        }

        return result;
    }

    private static string ReadString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static bool IsSafeHotspotUrl(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.StartsWith("/works/", StringComparison.OrdinalIgnoreCase) &&
               !value.Contains("..", StringComparison.Ordinal);
    }

    private static AsterSceneValidationIssueDto Issue(string code, string path, string message, string severity = "error")
    {
        return new AsterSceneValidationIssueDto
        {
            Code = code,
            Path = path,
            Message = message,
            Severity = severity
        };
    }
}
