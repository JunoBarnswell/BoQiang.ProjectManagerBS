using System.Diagnostics;
using System.Text.Json.Nodes;
using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using Xunit;

namespace AsterERP.Api.Tests;

[Collection("LowCodePerformance")]
public sealed class LowCodePerformanceBenchmarkTests
{
    private static readonly int[] NodeCounts = [100, 500, 1000, 2000];
    private const int WarmupCount = 1;
    private const int SampleCount = 5;

    [Fact]
    [Trait("Category", "LowCodePerformance")]
    public void Canonicalize_and_validate_document_baseline_is_measured_for_all_node_counts()
    {
        var validator = new ApplicationDevelopmentSchemaValidator();
        var results = new List<object>();

        foreach (var nodeCount in NodeCounts)
        {
            var document = CreateDocument(nodeCount);
            var samples = new List<double>();
            for (var run = 0; run < WarmupCount + SampleCount; run++)
            {
                var stopwatch = Stopwatch.StartNew();
                var canonical = ApplicationDesignerCanonicalJson.NormalizeObject(document.ToJsonString());
                validator.ValidateDraft(canonical);
                _ = ApplicationDesignerCanonicalJson.ComputeHash(canonical);
                stopwatch.Stop();
                if (run >= WarmupCount) samples.Add(stopwatch.Elapsed.TotalMilliseconds);
            }

            var p95 = Percentile(samples, 0.95);
            results.Add(new
            {
                nodeCount,
                samplesMs = samples,
                p95Ms = p95,
                peakWorkingSetBytes = Environment.WorkingSet,
                rawCommand = "dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~LowCodePerformanceBenchmarkTests",
                capturedAt = DateTimeOffset.UtcNow
            });

            Assert.True(p95 <= BudgetFor(nodeCount), $"Node count {nodeCount} exceeded canonicalize/validate p95 budget {BudgetFor(nodeCount)} ms: {p95} ms.");
        }

        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
        {
            format = "astererp.low-code.backend-performance.evidence",
            status = "Measured",
            warmupCount = WarmupCount,
            sampleCount = SampleCount,
            scenarios = results
        }));
    }

    private static JsonObject CreateDocument(int nodeCount)
    {
        var elements = new JsonObject { ["root"] = Element("root", null) };
        for (var index = 1; index < nodeCount; index++)
        {
            var parentId = index <= 2 ? "root" : $"node-{index / 2}";
            var id = $"node-{index}";
            elements[id] = Element(id, parentId);
            elements[parentId]!["children"]!.AsArray().Add(id);
        }

        return new JsonObject
        {
            ["actions"] = new JsonArray(),
            ["apiBindings"] = new JsonArray(),
            ["dataSources"] = new JsonArray(),
            ["documentId"] = $"backend-performance-{nodeCount}",
            ["elements"] = elements,
            ["metadata"] = new JsonObject(),
            ["modals"] = new JsonArray(),
            ["pageParameters"] = new JsonArray(),
            ["pages"] = new JsonArray(new JsonObject { ["id"] = "page-1", ["name"] = "Performance", ["rootElementId"] = "root" }),
            ["permissions"] = new JsonObject(),
            ["revision"] = 1,
            ["runtimeContext"] = new JsonObject(),
            ["styleTokens"] = new JsonObject(),
            ["variables"] = new JsonArray(),
            ["workflowBindings"] = new JsonArray()
        };
    }

    private static JsonObject Element(string id, string? parentId) => new()
    {
        ["children"] = new JsonArray(),
        ["events"] = new JsonArray(),
        ["id"] = id,
        ["layout"] = new JsonObject { ["display"] = "block" },
        ["parentId"] = parentId,
        ["props"] = new JsonObject { ["value"] = $"value-{id.Replace("node-", string.Empty, StringComparison.Ordinal)}" },
        ["type"] = "text.paragraph"
    };

    private static double BudgetFor(int nodeCount) => nodeCount switch
    {
        100 => 100,
        500 => 250,
        1000 => 500,
        2000 => 1000,
        _ => throw new ArgumentOutOfRangeException(nameof(nodeCount))
    };

    private static double Percentile(IReadOnlyList<double> values, double quantile)
    {
        var sorted = values.OrderBy(value => value).ToArray();
        var index = Math.Min(sorted.Length - 1, Math.Max(0, (int)Math.Ceiling(sorted.Length * quantile) - 1));
        return Math.Round(sorted[index], 3);
    }
}
