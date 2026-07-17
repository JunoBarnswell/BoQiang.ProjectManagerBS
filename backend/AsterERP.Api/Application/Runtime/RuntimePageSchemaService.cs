using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using AsterERP.Api.Application.ApplicationDevelopmentCenter.Runtime;
using AsterERP.Contracts.ApplicationDevelopmentCenter;
using AsterERP.Shared;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.Runtime;
using SqlSugar;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AsterERP.Api.Application.Runtime;

public sealed class RuntimePageSchemaService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    ApplicationDevelopmentSchemaCompiler schemaCompiler,
    ApplicationDevelopmentSchemaValidator schemaValidator) : IRuntimePageSchemaService
{
    public async Task<RuntimePageSchemaResponse> GetPublishedPageAsync(
        string pageCode,
        string? previewPageId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPageCode = NormalizePageCode(pageCode);
        if (!currentUser.IsAsterErpAuthenticated())
        {
            throw new ValidationException("请先登录", ErrorCodes.AuthenticationRequired);
        }

        var tenantId = currentUser.GetAsterErpTenantId();
        var appCode = currentUser.GetAsterErpAppCode();
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(appCode))
        {
            throw new ValidationException("请先选择租户应用工作区", ErrorCodes.PermissionDenied);
        }

        appCode = appCode.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(previewPageId))
        {
            return await BuildPreviewSchemaAsync(normalizedPageCode, previewPageId!, tenantId!, appCode, cancellationToken);
        }

        var db = databaseAccessor.GetCurrentDb();
        var page = (await db.Queryable<ApplicationDevelopmentPageEntity>()
                .Where(item =>
                    !item.IsDeleted &&
                    item.TenantId == tenantId &&
                    item.AppCode == appCode &&
                    item.PageCode == normalizedPageCode &&
                    item.Status == "Published")
                .Take(1)
                .ToListAsync(cancellationToken))
                .FirstOrDefault()
                ?? throw new NotFoundException("运行时页面配置不存在", ErrorCodes.RuntimePageSchemaNotFound);
        var document = (await db.Queryable<ApplicationDesignerDocumentEntity>()
                .Where(item =>
                    !item.IsDeleted &&
                    item.TenantId == tenantId &&
                    item.AppCode == appCode &&
                    item.PageId == page.Id &&
                    item.Status == "Published" &&
                    item.PublishedArtifactId != null &&
                    item.PublishedArtifactId != "")
                .Take(1)
                .ToListAsync(cancellationToken))
                .FirstOrDefault()
            ?? throw new NotFoundException("正式 DesignerDocument 不存在", ErrorCodes.RuntimePageSchemaNotFound);
        var artifact = (await db.Queryable<ApplicationDesignerRuntimeArtifactEntity>()
                .Where(item =>
                    !item.IsDeleted &&
                    item.TenantId == tenantId &&
                    item.AppCode == appCode &&
                    item.Id == document.PublishedArtifactId &&
                    item.DocumentId == document.Id &&
                    item.Status == "Published")
                .Take(1)
                .ToListAsync(cancellationToken))
                .FirstOrDefault()
            ?? throw new NotFoundException("正式 RuntimeArtifact 不存在", ErrorCodes.RuntimePageSchemaNotFound);
        EnsurePublishedArtifactPointer(document, artifact);
        if (!currentUser.HasAsterErpPermission(PermissionCodes.BuildAppRuntimePagePermission(normalizedPageCode, "view")))
        {
            throw new ValidationException("无权限访问该运行时页面", ErrorCodes.PermissionDenied);
        }

        if (string.IsNullOrWhiteSpace(artifact.ArtifactJson) ||
            global::System.Text.Encoding.UTF8.GetByteCount(artifact.ArtifactJson) > ApplicationDevelopmentSchemaValidator.RuntimeMaximumBytes)
        {
            throw new ValidationException("运行时页面配置无效", ErrorCodes.DesignerSchemaInvalid);
        }

        ValidateRuntimeArtifact(artifact.ArtifactJson);

        return new RuntimePageSchemaResponse(
            artifact.Id,
            page.TenantId,
            page.AppCode,
            page.PageCode,
            page.PageName,
            page.PageType,
            ReadModelCode(artifact.ArtifactJson),
            PermissionCodes.BuildAppRuntimePagePermission(normalizedPageCode, "view"),
            artifact.RevisionNumber,
            artifact.ArtifactJson);
    }

    private async Task<RuntimePageSchemaResponse> BuildPreviewSchemaAsync(string normalizedPageCode, string previewPageId, string tenantId, string appCode, CancellationToken cancellationToken)
    {
        if (!currentUser.HasAsterErpPermission(PermissionCodes.AppDevelopmentCenterDesignerPreview))
            throw new ValidationException("Preview permission is required", ErrorCodes.PermissionDenied);

        var page = (await databaseAccessor.GetCurrentDb().Queryable<ApplicationDevelopmentPageEntity>()
            .Where(item => !item.IsDeleted && item.TenantId == tenantId && item.AppCode == appCode && item.Id == previewPageId)
            .Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
            ?? throw new NotFoundException("Preview page was not found", ErrorCodes.RuntimePageSchemaNotFound);
        if (!string.Equals(page.PageCode, normalizedPageCode, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Preview page code does not match route", ErrorCodes.RuntimePageSchemaInvalid);

        var document = (await databaseAccessor.GetCurrentDb().Queryable<ApplicationDesignerDocumentEntity>()
                .Where(item => !item.IsDeleted &&
                               item.TenantId == tenantId &&
                               item.AppCode == appCode &&
                               item.PageId == page.Id)
                .OrderBy(item => item.CreatedTime, OrderByType.Desc)
                .Take(1)
                .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("Preview DesignerDocument was not found", ErrorCodes.RuntimePageSchemaNotFound);
        if (string.IsNullOrWhiteSpace(document.DocumentJson))
        {
            throw new ValidationException("Preview DesignerDocument is invalid", ErrorCodes.RuntimePageSchemaInvalid);
        }

        var schemaJson = schemaCompiler.CompileSchema(page.PageCode, page.PageName, page.PageType,
            ReadPageParameters(page.PageParametersJson), document.DocumentJson, page.PermissionConfigJson);
        ValidateRuntimeArtifact(schemaJson);
        return new RuntimePageSchemaResponse(page.Id, page.TenantId, page.AppCode, page.PageCode,
            page.PageName, page.PageType, null,
            PermissionCodes.BuildAppRuntimePagePermission(page.PageCode, "view"), 0, schemaJson);
    }

    private static List<ApplicationDevelopmentPageParameterDto> ReadPageParameters(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        try { return JsonSerializer.Deserialize<List<ApplicationDevelopmentPageParameterDto>>(value) ?? []; }
        catch (JsonException) { return []; }
    }

    private void ValidateRuntimeArtifact(string schemaJson)
    {
        try
        {
            using var schemaDocument = global::System.Text.Json.JsonDocument.Parse(schemaJson);
            if (!schemaDocument.RootElement.TryGetProperty("document", out var documentElement))
            {
                throw new ValidationException("运行时页面配置缺少 document", ErrorCodes.DesignerSchemaInvalid);
            }

            EnsureArtifactMetadata(schemaDocument.RootElement, documentElement);
            var artifact = JsonNode.Parse(schemaDocument.RootElement.GetRawText())?.AsObject()
                ?? throw new ValidationException("Runtime artifact is invalid", ErrorCodes.DesignerSchemaInvalid);
            RuntimeArtifactContractValidator.Validate(artifact);
            schemaValidator.ValidateRuntimeArtifact(documentElement.GetRawText());
        }
        catch (global::System.Text.Json.JsonException exception)
        {
            throw new ValidationException($"运行时页面配置不是合法 JSON：{exception.Message}", ErrorCodes.DesignerSchemaInvalid);
        }
    }

    private static void EnsureArtifactMetadata(JsonElement schema, JsonElement document)
    {
        if (!schema.TryGetProperty("revision", out var revision) || revision.ValueKind != JsonValueKind.Number || revision.GetInt32() < 1 ||
            !schema.TryGetProperty("artifactHash", out var hash) || hash.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(hash.GetString()) ||
            !schema.TryGetProperty("signature", out var signature) || signature.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(signature.GetString()) ||
            !schema.TryGetProperty("compilerVersion", out var compilerVersion) || compilerVersion.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(compilerVersion.GetString()) ||
            !schema.TryGetProperty("manifestTypes", out var manifests) || manifests.ValueKind != JsonValueKind.Array ||
            !schema.TryGetProperty("manifest", out var declarations) || declarations.ValueKind != JsonValueKind.Array)
        {
            throw new ValidationException("Runtime artifact metadata is incomplete", ErrorCodes.DesignerSchemaInvalid);
        }

        var manifestTypes = manifests.EnumerateArray().Select(item => item.GetString()).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!).ToArray();
        if (manifestTypes.Length == 0 || manifestTypes.Length != manifests.GetArrayLength() || manifestTypes.Distinct(StringComparer.Ordinal).Count() != manifestTypes.Length)
        {
            throw new ValidationException("Runtime artifact manifestTypes is invalid", ErrorCodes.DesignerSchemaInvalid);
        }
        ValidateManifestDeclarations(manifestTypes, declarations);

        var expectedHash = hash.GetString()!;
        if (!expectedHash.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Runtime artifact hash format is invalid", ErrorCodes.DesignerSchemaInvalid);
        }

        var documentNode = JsonNode.Parse(document.GetRawText())
            ?? throw new ValidationException("Runtime artifact document is invalid", ErrorCodes.DesignerSchemaInvalid);
        var actualHash = ApplicationDesignerCanonicalJson.ComputeRuntimeArtifactHash(documentNode);
        if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Runtime artifact integrity check failed", ErrorCodes.DesignerSchemaInvalid);
        }

        var manifestJson = BuildManifestSignatureJson(manifestTypes, declarations);
        var documentId = document.TryGetProperty("documentId", out var documentIdElement) ? documentIdElement.GetString() ?? string.Empty : string.Empty;
        var expectedSignature = ApplicationDesignerCanonicalJson.ComputeSignature(
            documentId,
            expectedHash,
            ApplicationDesignerCanonicalJson.ComputeHash(manifestJson),
            compilerVersion.GetString() ?? string.Empty,
            revision.GetInt32().ToString(global::System.Globalization.CultureInfo.InvariantCulture));
        if (!string.Equals(signature.GetString(), expectedSignature, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Runtime artifact signature verification failed", ErrorCodes.DesignerSchemaInvalid);
        }
    }

    private static string BuildManifestSignatureJson(IReadOnlyList<string> manifestTypes, JsonElement declarations)
    {
        var declarationNode = JsonNode.Parse(declarations.GetRawText())
            ?? throw new ValidationException("Runtime artifact manifest declarations are invalid", ErrorCodes.DesignerSchemaInvalid);
        return ApplicationDesignerCanonicalJson.NormalizeRuntimeObject(new JsonObject
        {
            ["types"] = JsonSerializer.SerializeToNode(manifestTypes),
            ["declarations"] = declarationNode
        }.ToJsonString());
    }

    private static void ValidateManifestDeclarations(IReadOnlyList<string> manifestTypes, JsonElement declarations)
    {
        if (declarations.GetArrayLength() == 0)
        {
            throw new ValidationException("Runtime artifact manifest declarations cannot be empty", ErrorCodes.DesignerSchemaInvalid);
        }

        var declarationTypes = declarations.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.Object && item.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String ? type.GetString() : null)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
        if (declarationTypes.Length != declarations.GetArrayLength() ||
            declarationTypes.Distinct(StringComparer.Ordinal).Count() != declarationTypes.Length ||
            !manifestTypes.OrderBy(item => item, StringComparer.Ordinal).SequenceEqual(
                declarationTypes.OrderBy(item => item, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new ValidationException("Runtime artifact manifest declarations do not match manifestTypes", ErrorCodes.DesignerSchemaInvalid);
        }
    }

    private static string NormalizePageCode(string pageCode)
    {
        string normalized;
        try
        {
            normalized = Uri.UnescapeDataString(pageCode).Trim();
        }
        catch (UriFormatException)
        {
            throw new ValidationException("页面编码格式无效");
        }

        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 128)
        {
            throw new ValidationException("页面编码不能为空且长度不能超过 128");
        }

        if (!global::System.Text.RegularExpressions.Regex.IsMatch(normalized, "^[A-Za-z][A-Za-z0-9_.:-]*$"))
        {
            throw new ValidationException("页面编码格式无效");
        }

        return normalized;
    }

    private static bool IsPublishedArtifactMetadataConsistent(
        ApplicationDesignerDocumentEntity document,
        ApplicationDesignerRuntimeArtifactEntity artifact)
    {
        if (!string.Equals(document.PublishedArtifactId, artifact.Id, StringComparison.Ordinal) ||
            !string.Equals(artifact.DocumentId, document.Id, StringComparison.Ordinal) ||
            !string.Equals(artifact.RevisionId, document.CurrentRevisionId, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(document.CurrentRevisionId) ||
            string.IsNullOrWhiteSpace(document.DocumentHash) ||
            string.IsNullOrWhiteSpace(artifact.ArtifactHash) ||
            string.IsNullOrWhiteSpace(artifact.ManifestHash) ||
            string.IsNullOrWhiteSpace(artifact.SignatureHash) ||
            artifact.RevisionNumber < 1 ||
            !string.Equals(artifact.TargetHash, document.DocumentHash, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            using var artifactDocument = JsonDocument.Parse(artifact.ArtifactJson);
            var root = artifactDocument.RootElement;
            var artifactHash = root.GetProperty("artifactHash").GetString();
            var signature = root.GetProperty("signature").GetString();
            var compilerVersion = root.GetProperty("compilerVersion").GetString();
            var migrationRevision = root.GetProperty("migrationRevision").GetString();
            var revision = root.GetProperty("revision").GetInt32();
            var manifestTypes = root.GetProperty("manifestTypes")
                .EnumerateArray()
                .Select(item => item.GetString() ?? string.Empty)
                .ToArray();
            var manifestJson = BuildManifestSignatureJson(manifestTypes, root.GetProperty("manifest"));
            var manifestHash = ApplicationDesignerCanonicalJson.ComputeHash(manifestJson);
            return string.Equals(artifactHash, artifact.ArtifactHash, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(signature, artifact.SignatureHash, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(compilerVersion, artifact.CompilerRevision, StringComparison.Ordinal) &&
                   string.Equals(migrationRevision, artifact.MigrationRevision, StringComparison.Ordinal) &&
                   revision == artifact.RevisionNumber &&
                   string.Equals(manifestHash, artifact.ManifestHash, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    private static string? ReadModelCode(string artifactJson)
    {
        using var document = JsonDocument.Parse(artifactJson);
        return document.RootElement.TryGetProperty("modelCode", out var modelCode) &&
               modelCode.ValueKind == JsonValueKind.String &&
               !string.IsNullOrWhiteSpace(modelCode.GetString())
            ? modelCode.GetString()
            : null;
    }

    private static void EnsurePublishedArtifactPointer(
        ApplicationDesignerDocumentEntity document,
        ApplicationDesignerRuntimeArtifactEntity artifact)
    {
        var isValid = IsPublishedArtifactMetadataConsistent(document, artifact);
        if (!isValid)
        {
            throw new ValidationException("运行时页面权限配置无效", ErrorCodes.RuntimePageSchemaInvalid);
        }
    }
}
