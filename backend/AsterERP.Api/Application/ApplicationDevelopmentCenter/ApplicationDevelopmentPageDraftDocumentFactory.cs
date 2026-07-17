using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDevelopmentCenter;

namespace AsterERP.Api.Application.ApplicationDevelopmentCenter;

internal static class ApplicationDevelopmentPageDraftDocumentFactory
{
    public static string Create(
        string pageCode,
        string pageName,
        string pageType = ApplicationDevelopmentPageTypes.Standard,
        IReadOnlyList<ApplicationDevelopmentPageParameterDto>? pageParameters = null)
    {
        var rootId = $"{NormalizeElementId(pageCode)}_root";
        var normalizedPageType = ApplicationDevelopmentPageTypes.IsValid(pageType)
            ? pageType
            : ApplicationDevelopmentPageTypes.Standard;
        var rootType = normalizedPageType switch
        {
            ApplicationDevelopmentPageTypes.Dialog => "modal.dialog",
            ApplicationDevelopmentPageTypes.Drawer => "modal.drawer",
            _ => "layout.page"
        };
        var rootWidth = normalizedPageType switch
        {
            ApplicationDevelopmentPageTypes.Dialog => "640px",
            ApplicationDevelopmentPageTypes.Drawer => "520px",
            _ => "100%"
        };
        var parameters = pageParameters ?? [];
        return ApplicationDataCenterJson.Serialize(new
        {
            actions = Array.Empty<object>(),
            apiBindings = Array.Empty<object>(),
            dataSources = Array.Empty<object>(),
            documentId = pageCode,
            elements = new Dictionary<string, object?>
            {
                [rootId] = new
                {
                    children = Array.Empty<string>(),
                    events = Array.Empty<object>(),
                    id = rootId,
                    layout = new { minHeight = normalizedPageType == ApplicationDevelopmentPageTypes.Standard ? "720px" : "240px", width = rootWidth },
                    name = pageName,
                    parentId = (string?)null,
                    permission = new { },
                    props = new { title = pageName },
                    style = new
                    {
                        backgroundColor = "#ffffff",
                        borderColor = "#e2e8f0",
                        borderRadius = "2px",
                        borderWidth = "1px",
                        padding = "14px"
                    },
                    type = rootType,
                    validation = Array.Empty<object>()
                }
            },
            metadata = new { pageCode, pageName },
            modals = normalizedPageType == ApplicationDevelopmentPageTypes.Dialog || normalizedPageType == ApplicationDevelopmentPageTypes.Drawer
                ? new object[] { new { id = pageCode, name = pageName, rootElementId = rootId, type = normalizedPageType } }
                : Array.Empty<object>(),
            pages = new[]
            {
                new { id = pageCode, name = pageName, rootElementId = rootId }
            },
            permissions = new { },
            printBindings = Array.Empty<object>(),
            runtimeContext = new
            {
                pageCode,
                pageName,
                pageParameters = parameters,
                pageType = normalizedPageType
            },
            pageParameters = parameters,
            pageType = normalizedPageType,
            revision = 1,
            sqlBindings = Array.Empty<object>(),
            styleTokens = new { },
            variables = new[]
            {
                new { id = "system.currentUser", name = "当前用户", source = "system", valueType = "json" },
                new { id = "row.current", name = "当前行", source = "currentRow", valueType = "json" },
                new { id = "form.values", name = "表单字段", source = "formField", valueType = "json" }
            },
            workflowBindings = Array.Empty<object>()
        });
    }

    private static string NormalizeElementId(string pageCode)
    {
        var chars = pageCode.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_').ToArray();
        var normalized = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? "designer_page" : normalized;
    }
}
