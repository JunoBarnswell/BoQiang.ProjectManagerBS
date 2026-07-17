using System.Text.Json;
using AsterERP.Api.Application.AsterScene;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class AsterSceneDocumentKernelTests
{
    [Fact]
    public void CreateDefaultDocumentJson_ReturnsValidSceneDocument()
    {
        var json = AsterSceneDocumentKernel.CreateDefaultDocumentJson("project-1", "Factory Tour");
        var document = AsterSceneDocumentKernel.ParseJson(json);

        var validation = AsterSceneDocumentKernel.Validate(document);

        Assert.True(validation.IsValid);
        Assert.Empty(validation.Errors);
        Assert.Equal("scene_main", document.GetProperty("runtime").GetProperty("entrySceneId").GetString());
        Assert.Equal("project-1", document.GetProperty("identity").GetProperty("projectId").GetString());
    }

    [Fact]
    public void Validate_RejectsVersionedSchemaAndMissingEntryScene()
    {
        using var json = JsonDocument.Parse(
            """
            {
              "meta": { "schemaVersion": "legacy", "product": "AsterScene", "title": "Legacy" },
              "revision": 1,
              "identity": { "projectId": "project-1", "documentId": "doc-1", "locale": "zh-CN" },
              "assets": [],
              "actors": [],
              "components": [],
              "materials": [],
              "geometries": [],
              "uv": { "layouts": [] },
              "interactions": { "hotspots": [], "blueprints": [], "nav": {} },
              "timeline": { "sequences": [], "tracks": [] },
              "runtime": { "entrySceneId": "", "scenes": [] },
              "publish": { "visibility": "Private", "license": "standard-remix" },
              "quality": {},
              "extensions": {}
            }
            """);

        var validation = AsterSceneDocumentKernel.Validate(json.RootElement);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, item => item.Code == "SchemaVersionRemoved");
        Assert.Contains(validation.Errors, item => item.Code == "EntrySceneRequired");
        Assert.Contains(validation.Errors, item => item.Code == "SceneRequired");
    }

    [Fact]
    public void NormalizeJson_ProducesStableHashForEquivalentDocuments()
    {
        using var compact = JsonDocument.Parse("""{"b":2,"a":{"c":3}}""");
        using var pretty = JsonDocument.Parse(
            """
            {
              "b": 2,
              "a": {
                "c": 3
              }
            }
            """);

        var compactHash = AsterSceneDocumentKernel.ComputeHash(AsterSceneDocumentKernel.NormalizeJson(compact.RootElement));
        var prettyHash = AsterSceneDocumentKernel.ComputeHash(AsterSceneDocumentKernel.NormalizeJson(pretty.RootElement));

        Assert.Equal(compactHash, prettyHash);
    }

    [Fact]
    public void Validate_AcceptsPanoramaSceneWithNavigateHotspot()
    {
        using var json = JsonDocument.Parse(
            """
            {
              "meta": { "product": "AsterScene", "title": "Panorama Hall" },
              "revision": 1,
              "identity": { "projectId": "project-1", "documentId": "doc-1", "locale": "zh-CN" },
              "assets": [
                { "id": "asset_panorama", "kind": "panorama", "url": "/uploads/panorama.jpg" }
              ],
              "actors": [],
              "components": [],
              "materials": [],
              "geometries": [],
              "uv": { "layouts": [] },
              "interactions": {
                "hotspots": [
                  {
                    "id": "hotspot_foyer",
                    "label": "Foyer",
                    "sceneId": "scene_panorama",
                    "target": "scene_panorama",
                    "type": "navigate",
                    "spherical": { "yaw": 20, "pitch": 0 }
                  }
                ],
                "blueprints": [],
                "nav": {}
              },
              "timeline": { "sequences": [], "tracks": [] },
              "runtime": {
                "entrySceneId": "scene_panorama",
                "camera": {},
                "scenes": [
                  {
                    "id": "scene_panorama",
                    "name": "720 Foyer",
                    "type": "panorama720",
                    "actors": [],
                    "environment": {
                      "panoramaAssetId": "asset_panorama",
                      "projection": "equirectangular",
                      "stereo": "mono",
                      "initialYaw": 0,
                      "initialPitch": 0,
                      "fov": 70
                    }
                  }
                ]
              },
              "publish": { "visibility": "Public", "license": "standard-remix" },
              "quality": {},
              "extensions": {}
            }
            """);

        var validation = AsterSceneDocumentKernel.Validate(json.RootElement);

        Assert.True(validation.IsValid);
        Assert.Empty(validation.Errors);
    }

    [Fact]
    public void Validate_RejectsInvalidPanoramaAndHotspotReferences()
    {
        using var json = JsonDocument.Parse(
            """
            {
              "meta": { "product": "AsterScene", "title": "Broken Panorama" },
              "revision": 1,
              "identity": { "projectId": "project-1", "documentId": "doc-1", "locale": "zh-CN" },
              "assets": [
                { "id": "asset_panorama", "kind": "model", "url": "/uploads/panorama.jpg" }
              ],
              "actors": [],
              "components": [],
              "materials": [],
              "geometries": [],
              "uv": { "layouts": [] },
              "interactions": {
                "hotspots": [
                  { "id": "hotspot_missing", "label": "Missing", "sceneId": "scene_missing", "target": "scene_missing", "type": "navigate" },
                  { "id": "hotspot_link", "label": "Link", "sceneId": "scene_panorama", "target": "", "type": "url", "payload": { "url": "https://example.com" } }
                ],
                "blueprints": [],
                "nav": {}
              },
              "timeline": { "sequences": [], "tracks": [] },
              "runtime": {
                "entrySceneId": "scene_panorama",
                "camera": {},
                "scenes": [
                  {
                    "id": "scene_panorama",
                    "name": "720 Foyer",
                    "type": "panorama720",
                    "actors": [],
                    "environment": { "panoramaAssetId": "asset_panorama" }
                  }
                ]
              },
              "publish": { "visibility": "Public", "license": "standard-remix" },
              "quality": {},
              "extensions": {}
            }
            """);

        var validation = AsterSceneDocumentKernel.Validate(json.RootElement);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, item => item.Code == "PanoramaAssetInvalid");
        Assert.Contains(validation.Errors, item => item.Code == "HotspotSceneMissing");
        Assert.Contains(validation.Errors, item => item.Code == "HotspotTargetMissing");
        Assert.Contains(validation.Errors, item => item.Code == "HotspotUrlDenied");
    }
}
