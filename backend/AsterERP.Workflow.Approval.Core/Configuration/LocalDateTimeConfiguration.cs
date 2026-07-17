using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace AsterERP.Workflow.Approval.Core.Configuration;

public static class LocalDateTimeConfiguration
{
    public static IServiceCollection AddAsterERPFlowDateTime(this IServiceCollection services, string pattern = "yyyy-MM-dd HH:mm:ss")
    {
        services.Configure<JsonSerializerOptions>(options =>
        {
            options.Converters.Add(new DateTimeJsonConverter(pattern));
        });
        return services;
    }
}

public class DateTimeJsonConverter : JsonConverter<DateTime>
{
    private readonly string _pattern;

    public DateTimeJsonConverter(string pattern)
    {
        _pattern = pattern;
    }

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dateStr = reader.GetString();
        if (DateTime.TryParse(dateStr, out var date))
        {
            return date;
        }
        return default;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(_pattern));
    }
}
