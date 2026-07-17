using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AsterERP.Api.Application.Runtime.ExpressionFunctions;
using AsterERP.Contracts.Runtime;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Runtime;

public sealed partial class RuntimeExpressionHelperCatalog
{
    public bool Supports(string helperName)
    {
        if (string.IsNullOrWhiteSpace(helperName))
        {
            return false;
        }

        try
        {
            _ = Apply(null, new RuntimeExpressionHelperDto
            {
                Args = new Dictionary<string, object?>(),
                Name = helperName
            });
            return true;
        }
        catch (ValidationException exception) when (exception.Message.StartsWith("变量辅助函数不支持:", StringComparison.Ordinal))
        {
            return false;
        }
        catch
        {
            return true;
        }
    }

    public object? Apply(object? value, RuntimeExpressionHelperDto helper)
    {
        var name = NormalizeName(helper.Name);
        return name switch
        {
            "trim" => ToText(value)?.Trim(),
            "ltrim" => ToText(value)?.TrimStart(),
            "rtrim" => ToText(value)?.TrimEnd(),
            "normalizewhitespace" => WhitespaceRegex().Replace(ToText(value)?.Trim() ?? string.Empty, " "),
            "upper" => ToText(value)?.ToUpperInvariant(),
            "lower" => ToText(value)?.ToLowerInvariant(),
            "length" => CountValue(value),
            "substring" => Substring(value, helper.Args),
            "left" => Left(value, helper.Args),
            "right" => Right(value, helper.Args),
            "replace" => Replace(value, helper.Args),
            "remove" => Remove(value, helper.Args),
            "prefix" => $"{ToText(ReadArg(helper.Args, "prefix"))}{ToText(value)}",
            "suffix" => $"{ToText(value)}{ToText(ReadArg(helper.Args, "suffix"))}",
            "padleft" => Pad(value, helper.Args, true),
            "padright" => Pad(value, helper.Args, false),
            "splittake" => SplitTake(value, helper.Args),
            "defaultifempty" => IsEmpty(value) ? ReadArg(helper.Args, "value") ?? ReadArg(helper.Args, "defaultValue") : value,
            "capitalize" => Capitalize(ToText(value) ?? string.Empty),
            "titlecase" => string.Join(' ', SplitWords(value).Select(Capitalize)),
            "camelcase" => DelimitedCase(value, "camel"),
            "snakecase" => DelimitedCase(value, "snake"),
            "kebabcase" => DelimitedCase(value, "kebab"),
            "removespaces" => SpaceRegex().Replace(ToText(value) ?? string.Empty, string.Empty),
            "removelinebreaks" => LineBreakRegex().Replace(ToText(value) ?? string.Empty, string.Empty),
            "striphtml" => HtmlTagRegex().Replace(ToText(value) ?? string.Empty, string.Empty),
            "onlydigits" => NonDigitRegex().Replace(ToText(value) ?? string.Empty, string.Empty),
            "onlyletters" => NonLetterRegex().Replace(ToText(value) ?? string.Empty, string.Empty),
            "truncate" => Truncate(value, helper.Args),
            "textbefore" => TextBefore(value, helper.Args),
            "textafter" => TextAfter(value, helper.Args),
            "textbetween" => TextBetween(value, helper.Args),
            "ensureprefix" => EnsureAffix(value, helper.Args, true),
            "ensuresuffix" => EnsureAffix(value, helper.Args, false),
            "removeprefix" => RemoveAffix(value, helper.Args, true),
            "removesuffix" => RemoveAffix(value, helper.Args, false),
            "repeat" => RepeatText(value, helper.Args),
            "takelines" => TakeLines(value, helper.Args),
            "normalizenewlines" => NormalizeNewLines(value),
            "containschinese" => ChineseRegex().IsMatch(ToText(value) ?? string.Empty),
            "onlychinese" => NonChineseRegex().Replace(ToText(value) ?? string.Empty, string.Empty),
            "tostring" => ToText(value),
            "tonumber" => ToNumber(value),
            "toboolean" => ToBoolean(value),
            "tointeger" => decimal.ToInt64(decimal.Truncate(ToNumber(value) ?? 0)),
            "toarray" => ToEnumerable(value).ToArray(),
            "toobject" => ToObject(value),
            "nullifempty" => IsEmpty(value) ? null : value,
            "emptytonull" => IsEmpty(value) ? null : value,
            "parsecurrency" => ParseCurrency(value),
            "encodeuri" => Uri.EscapeDataString(ToText(value) ?? string.Empty),
            "decodeuri" => Uri.UnescapeDataString(ToText(value) ?? string.Empty),
            "base64encode" => Convert.ToBase64String(Encoding.UTF8.GetBytes(ToText(value) ?? string.Empty)),
            "base64decode" => DecodeBase64(value),
            "abs" => Math.Abs(ToNumber(value) ?? 0),
            "round" => Round(value, helper.Args),
            "ceil" => Math.Ceiling(ToNumber(value) ?? 0),
            "floor" => Math.Floor(ToNumber(value) ?? 0),
            "fixed" => Fixed(value, helper.Args),
            "currency" => Currency(value, helper.Args),
            "percent" => Percent(value, helper.Args),
            "add" => (ToNumber(value) ?? 0) + (ToNumber(ReadArg(helper.Args, "value")) ?? 0),
            "subtract" => (ToNumber(value) ?? 0) - (ToNumber(ReadArg(helper.Args, "value")) ?? 0),
            "multiply" => (ToNumber(value) ?? 0) * (ToNumber(ReadArg(helper.Args, "value")) ?? 0),
            "divide" => Divide(value, helper.Args),
            "clamp" => Clamp(value, helper.Args),
            "min" => Math.Min(ToNumber(value) ?? 0, ToNumber(ReadArg(helper.Args, "value")) ?? 0),
            "max" => Math.Max(ToNumber(value) ?? 0, ToNumber(ReadArg(helper.Args, "value")) ?? 0),
            "modulo" => Modulo(value, helper.Args),
            "power" => Math.Pow((double)(ToNumber(value) ?? 0), (double)(ToNumber(ReadArg(helper.Args, "value")) ?? 0)),
            "negate" => -(ToNumber(value) ?? 0),
            "parsedate" => ParseDate(value),
            "formatdate" => FormatDate(value, helper.Args),
            "now" => DateTime.UtcNow,
            "adddays" => AddDate(value, helper.Args, "days"),
            "addhours" => AddDate(value, helper.Args, "hours"),
            "addminutes" => AddDate(value, helper.Args, "minutes"),
            "addseconds" => AddDate(value, helper.Args, "seconds"),
            "addmonths" => AddDate(value, helper.Args, "months"),
            "addyears" => AddDate(value, helper.Args, "years"),
            "addweeks" => AddDate(value, helper.Args, "weeks"),
            "startofday" => DayBoundary(value, true),
            "endofday" => DayBoundary(value, false),
            "startofmonth" => PeriodBoundary(value, "month", true),
            "endofmonth" => PeriodBoundary(value, "month", false),
            "startofyear" => PeriodBoundary(value, "year", true),
            "endofyear" => PeriodBoundary(value, "year", false),
            "year" => ParseDate(value)?.Year,
            "month" => ParseDate(value)?.Month,
            "day" => ParseDate(value)?.Day,
            "today" => DateTime.UtcNow.Date,
            "yesterday" => DateTime.UtcNow.Date.AddDays(-1),
            "tomorrow" => DateTime.UtcNow.Date.AddDays(1),
            "weekofyear" => WeekOfYear(value),
            "weekday" => Weekday(value),
            "quarter" => Quarter(value),
            "formatquarter" => FormatQuarter(value),
            "ageyears" => AgeYears(value),
            "diffdays" => DiffDate(value, helper.Args, "days"),
            "diffhours" => DiffDate(value, helper.Args, "hours"),
            "diffminutes" => DiffDate(value, helper.Args, "minutes"),
            "diffseconds" => DiffDate(value, helper.Args, "seconds"),
            "timestamp" => ToTimestamp(value),
            "fromtimestamp" => FromTimestamp(value),
            "jsonpath" => JsonPath(value, helper.Args),
            "parsejson" => ParseJson(value),
            "stringifyjson" => JsonSerializer.Serialize(NormalizeJsonValue(value)),
            "objectkeys" => ToObject(value).Keys.ToArray(),
            "objectvalues" => ToObject(value).Values.ToArray(),
            "first" => ToEnumerable(value).FirstOrDefault(),
            "last" => ToEnumerable(value).LastOrDefault(),
            "nth" => Nth(value, helper.Args),
            "count" => CountValue(value),
            "take" => ToEnumerable(value).Take(ToInt(ReadArg(helper.Args, "count")) ?? 0).ToArray(),
            "skip" => ToEnumerable(value).Skip(ToInt(ReadArg(helper.Args, "count")) ?? 0).ToArray(),
            "slice" => Slice(value, helper.Args),
            "page" => Page(value, helper.Args),
            "reverse" => ToEnumerable(value).Reverse().ToArray(),
            "distinct" => Distinct(value),
            "compact" => ToEnumerable(value).Where(item => !IsEmpty(item)).ToArray(),
            "filternotempty" => ToEnumerable(value).Where(item => !IsEmpty(item)).ToArray(),
            "emptyarrayifnull" => IsEmpty(value) ? Array.Empty<object?>() : ToEnumerable(value).ToArray(),
            "join" => string.Join(ToText(ReadArg(helper.Args, "separator")) ?? ",", ToEnumerable(value).Select(item => item?.ToString() ?? string.Empty)),
            "mapfield" => MapField(value, helper.Args),
            "filterequals" => FilterByField(value, helper.Args, false),
            "filtercontains" => FilterByField(value, helper.Args, true),
            "rejectequals" => RejectByField(value, helper.Args),
            "findbyfield" => FilterByField(value, helper.Args, false).FirstOrDefault(),
            "containsitem" => ToEnumerable(value).Any(item => string.Equals(ToText(item), ToText(ReadArg(helper.Args, "value")), StringComparison.OrdinalIgnoreCase)),
            "sortby" => SortByField(value, helper.Args),
            "uniqueby" => UniqueBy(value, helper.Args),
            "groupby" => GroupBy(value, helper.Args),
            "flatten" => ToEnumerable(value).SelectMany(FlattenOneLevel).ToArray(),
            "objectpath" => RuntimeExpressionPathReader.Read(value, ToText(ReadArg(helper.Args, "path")) ?? string.Empty),
            "haspath" => RuntimeExpressionPathReader.Read(value, ToText(ReadArg(helper.Args, "path")) ?? string.Empty) is not null,
            "pickfields" => PickFields(value, helper.Args),
            "omitfields" => OmitFields(value, helper.Args),
            "sum" => Aggregate(value, helper.Args, "sum"),
            "average" => Aggregate(value, helper.Args, "average"),
            "minby" => Aggregate(value, helper.Args, "min"),
            "maxby" => Aggregate(value, helper.Args, "max"),
            "coalesce" => Coalesce(value, helper.Args),
            "isempty" => IsEmpty(value),
            "isnotempty" => !IsEmpty(value),
            "isnull" => value is null,
            "isnotnull" => value is not null,
            "istrue" => ToBoolean(value),
            "isfalse" => !ToBoolean(value),
            "isstring" => NormalizeJsonValue(value) is string,
            "isboolean" => NormalizeJsonValue(value) is bool,
            "isarray" => IsArray(value),
            "isobject" => NormalizeJsonValue(value) is IReadOnlyDictionary<string, object?> or IDictionary<string, object?> || value is JsonElement { ValueKind: JsonValueKind.Object },
            "not" => !ToBoolean(value),
            "and" => ToBoolean(value) && ToBoolean(ReadArg(helper.Args, "value")),
            "or" => ToBoolean(value) || ToBoolean(ReadArg(helper.Args, "value")),
            "ifelse" => ToBoolean(value) ? ReadArg(helper.Args, "whenTrue") : ReadArg(helper.Args, "whenFalse"),
            "equals" => string.Equals(ToText(value), ToText(ReadArg(helper.Args, "value")), StringComparison.OrdinalIgnoreCase),
            "notequals" => !string.Equals(ToText(value), ToText(ReadArg(helper.Args, "value")), StringComparison.OrdinalIgnoreCase),
            "greaterthan" => CompareValues(value, ReadArg(helper.Args, "value")) > 0,
            "greaterorequals" => CompareValues(value, ReadArg(helper.Args, "value")) >= 0,
            "lessthan" => CompareValues(value, ReadArg(helper.Args, "value")) < 0,
            "lessorequals" => CompareValues(value, ReadArg(helper.Args, "value")) <= 0,
            "contains" => ToText(value)?.Contains(ToText(ReadArg(helper.Args, "value")) ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true,
            "startswith" => ToText(value)?.StartsWith(ToText(ReadArg(helper.Args, "value")) ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true,
            "endswith" => ToText(value)?.EndsWith(ToText(ReadArg(helper.Args, "value")) ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true,
            "inlist" => InList(value, helper.Args),
            "isnumber" => ToNumber(value) is not null,
            "isinteger" => IsInteger(value),
            "ispositive" => (ToNumber(value) ?? 0) > 0,
            "isnegative" => (ToNumber(value) ?? 0) < 0,
            "iszero" => (ToNumber(value) ?? decimal.MinValue) == 0,
            "isdate" => ParseDate(value) is not null,
            "isemail" => EmailRegex().IsMatch(ToText(value) ?? string.Empty),
            "isphone" => PhoneRegex().IsMatch(NonDigitRegex().Replace(ToText(value) ?? string.Empty, string.Empty)),
            "isurl" => IsUrl(value),
            "isguid" => Guid.TryParse(ToText(value), out _),
            "isjson" => IsJson(value),
            "ispostalcode" => PostalCodeRegex().IsMatch(ToText(value) ?? string.Empty),
            "minlength" => (ToText(value) ?? string.Empty).Length >= (ToInt(ReadArg(helper.Args, "length")) ?? 0),
            "maxlength" => (ToText(value) ?? string.Empty).Length <= (ToInt(ReadArg(helper.Args, "length")) ?? int.MaxValue),
            "between" => Between(value, helper.Args),
            "maskphone" => MaskText(ToText(value) ?? string.Empty, 3, 4, "****"),
            "maskemail" => MaskEmail(value),
            "maskidcard" => MaskText(ToText(value) ?? string.Empty, 4, 4, "**********"),
            "maskbankcard" => MaskText(ToText(value) ?? string.Empty, 4, 4, " **** **** "),
            "maskname" => MaskName(value),
            "masktext" => MaskText(
                ToText(value) ?? string.Empty,
                ToInt(ReadArg(helper.Args, "start")) ?? 2,
                ToInt(ReadArg(helper.Args, "end")) ?? 2,
                ToText(ReadArg(helper.Args, "mask")) ?? "***"),
            "booleantext" => ToBoolean(value) ? ToText(ReadArg(helper.Args, "trueText")) ?? "是" : ToText(ReadArg(helper.Args, "falseText")) ?? "否",
            "yesno" => ToBoolean(value) ? "是" : "否",
            "enableddisabled" => ToBoolean(value) ? "启用" : "禁用",
            "successfail" => ToBoolean(value) ? "成功" : "失败",
            "mapvalue" => MapValue(value, helper.Args),
            _ => throw new ValidationException($"变量辅助函数不支持: {helper.Name}", ErrorCodes.ParameterInvalid)
        };
    }

    private static string NormalizeName(string value) =>
        RuntimeExpressionFunctionCatalog.NormalizeHelperName(value);

    private static object? ReadArg(IReadOnlyDictionary<string, object?> args, string key)
    {
        if (!args.TryGetValue(key, out var value))
        {
            return null;
        }

        return NormalizeJsonValue(value);
    }

    private static string? ToText(object? value) =>
        NormalizeJsonValue(value)?.ToString();

    private static object? NormalizeJsonValue(object? value)
    {
        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
                JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => JsonSerializer.Deserialize<object?>(element.GetRawText())
            };
        }

        return value;
    }

    private static object? Substring(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var text = ToText(value) ?? string.Empty;
        var start = ToInt(ReadArg(args, "start")) ?? 0;
        var length = ToInt(ReadArg(args, "length"));
        if (start < 0 || start >= text.Length)
        {
            return string.Empty;
        }

        return length is null ? text[start..] : text.Substring(start, Math.Min(length.Value, text.Length - start));
    }

    private static object? Replace(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var oldValue = ToText(ReadArg(args, "oldValue")) ?? ToText(ReadArg(args, "from")) ?? string.Empty;
        var newValue = ToText(ReadArg(args, "newValue")) ?? ToText(ReadArg(args, "to")) ?? string.Empty;
        return string.IsNullOrEmpty(oldValue) ? ToText(value) : (ToText(value) ?? string.Empty).Replace(oldValue, newValue, StringComparison.Ordinal);
    }

    private static object? Left(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var text = ToText(value) ?? string.Empty;
        var length = Math.Clamp(ToInt(ReadArg(args, "length")) ?? 0, 0, text.Length);
        return text[..length];
    }

    private static object? Right(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var text = ToText(value) ?? string.Empty;
        var length = Math.Clamp(ToInt(ReadArg(args, "length")) ?? 0, 0, text.Length);
        return length == 0 ? string.Empty : text[^length..];
    }

    private static object? Remove(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var target = ToText(ReadArg(args, "value")) ?? string.Empty;
        return string.IsNullOrEmpty(target)
            ? ToText(value)
            : (ToText(value) ?? string.Empty).Replace(target, string.Empty, StringComparison.Ordinal);
    }

    private static object? Pad(object? value, IReadOnlyDictionary<string, object?> args, bool left)
    {
        var text = ToText(value) ?? string.Empty;
        var length = ToInt(ReadArg(args, "length")) ?? text.Length;
        var padChar = (ToText(ReadArg(args, "char")) ?? " ").FirstOrDefault(' ');
        return left ? text.PadLeft(length, padChar) : text.PadRight(length, padChar);
    }

    private static object? SplitTake(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var separator = ToText(ReadArg(args, "separator"));
        var parts = (ToText(value) ?? string.Empty).Split(string.IsNullOrEmpty(separator) ? "," : separator, StringSplitOptions.None);
        var index = ToInt(ReadArg(args, "index")) ?? 0;
        return index >= 0 && index < parts.Length ? parts[index] : string.Empty;
    }

    private static string Capitalize(string value) =>
        string.IsNullOrEmpty(value) ? string.Empty : string.Concat(value[..1].ToUpperInvariant(), value[1..].ToLowerInvariant());

    private static string DelimitedCase(object? value, string mode)
    {
        var words = SplitWords(value);
        if (mode == "camel")
        {
            return string.Concat(words.Select((word, index) => index == 0 ? word.ToLowerInvariant() : Capitalize(word)));
        }

        var delimiter = mode == "snake" ? "_" : "-";
        return string.Join(delimiter, words.Select(word => word.ToLowerInvariant()));
    }

    private static IReadOnlyList<string> SplitWords(object? value) =>
        WordSplitRegex()
            .Split(CamelCaseBoundaryRegex().Replace(ToText(value) ?? string.Empty, "$1 $2"))
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();

    private static object? Truncate(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var text = ToText(value) ?? string.Empty;
        var length = ToInt(ReadArg(args, "length")) ?? 0;
        if (length <= 0 || text.Length <= length)
        {
            return text;
        }

        return text[..length] + (ToText(ReadArg(args, "suffix")) ?? "...");
    }

    private static string TextBefore(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var text = ToText(value) ?? string.Empty;
        var separator = ToText(ReadArg(args, "separator")) ?? string.Empty;
        if (string.IsNullOrEmpty(separator))
        {
            return text;
        }

        var index = text.IndexOf(separator, StringComparison.Ordinal);
        return index < 0 ? text : text[..index];
    }

    private static string TextAfter(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var text = ToText(value) ?? string.Empty;
        var separator = ToText(ReadArg(args, "separator")) ?? string.Empty;
        if (string.IsNullOrEmpty(separator))
        {
            return string.Empty;
        }

        var index = text.IndexOf(separator, StringComparison.Ordinal);
        return index < 0 ? string.Empty : text[(index + separator.Length)..];
    }

    private static string TextBetween(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var text = ToText(value) ?? string.Empty;
        var start = ToText(ReadArg(args, "start")) ?? string.Empty;
        var end = ToText(ReadArg(args, "end")) ?? string.Empty;
        var startIndex = string.IsNullOrEmpty(start) ? 0 : text.IndexOf(start, StringComparison.Ordinal);
        if (startIndex < 0)
        {
            return string.Empty;
        }

        var contentStart = startIndex + start.Length;
        var endIndex = string.IsNullOrEmpty(end) ? text.Length : text.IndexOf(end, contentStart, StringComparison.Ordinal);
        return endIndex < 0 ? string.Empty : text[contentStart..endIndex];
    }

    private static string EnsureAffix(object? value, IReadOnlyDictionary<string, object?> args, bool prefix)
    {
        var text = ToText(value) ?? string.Empty;
        var affix = ToText(ReadArg(args, prefix ? "prefix" : "suffix")) ?? string.Empty;
        if (string.IsNullOrEmpty(affix))
        {
            return text;
        }

        return prefix
            ? text.StartsWith(affix, StringComparison.Ordinal) ? text : affix + text
            : text.EndsWith(affix, StringComparison.Ordinal) ? text : text + affix;
    }

    private static string RemoveAffix(object? value, IReadOnlyDictionary<string, object?> args, bool prefix)
    {
        var text = ToText(value) ?? string.Empty;
        var affix = ToText(ReadArg(args, prefix ? "prefix" : "suffix")) ?? string.Empty;
        if (string.IsNullOrEmpty(affix))
        {
            return text;
        }

        return prefix
            ? text.StartsWith(affix, StringComparison.Ordinal) ? text[affix.Length..] : text
            : text.EndsWith(affix, StringComparison.Ordinal) ? text[..^affix.Length] : text;
    }

    private static string RepeatText(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var count = Math.Clamp(ToInt(ReadArg(args, "count")) ?? 0, 0, 1000);
        return string.Concat(Enumerable.Repeat(ToText(value) ?? string.Empty, count));
    }

    private static string TakeLines(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var count = Math.Max(ToInt(ReadArg(args, "count")) ?? 0, 0);
        return string.Join('\n', NormalizeNewLines(value).Split('\n').Take(count));
    }

    private static string NormalizeNewLines(object? value) =>
        (ToText(value) ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static IReadOnlyDictionary<string, object?> ToObject(object? value)
    {
        var normalized = NormalizeJsonValue(value);
        if (normalized is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            return readOnlyDictionary;
        }

        if (normalized is IDictionary<string, object?> dictionary)
        {
            return new Dictionary<string, object?>(dictionary, StringComparer.OrdinalIgnoreCase);
        }

        if (normalized is JsonElement { ValueKind: JsonValueKind.Object } element)
        {
            return element.EnumerateObject()
                .ToDictionary(property => property.Name, property => NormalizeJsonValue(property.Value), StringComparer.OrdinalIgnoreCase);
        }

        if (normalized is string text && !string.IsNullOrWhiteSpace(text))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<JsonElement>(text);
                if (parsed.ValueKind == JsonValueKind.Object)
                {
                    return parsed.EnumerateObject()
                        .ToDictionary(property => property.Name, property => NormalizeJsonValue(property.Value), StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (JsonException)
            {
                return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            }
        }

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    private static string DecodeBase64(object? value)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(ToText(value) ?? string.Empty));
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }

    private static decimal ParseCurrency(object? value)
    {
        var normalized = CurrencyNoiseRegex().Replace(ToText(value) ?? string.Empty, string.Empty);
        return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var number) ? number : 0;
    }

    private static decimal? ToNumber(object? value)
    {
        var normalized = NormalizeJsonValue(value);
        return normalized switch
        {
            null => null,
            decimal decimalValue => decimalValue,
            double doubleValue => Convert.ToDecimal(doubleValue, CultureInfo.InvariantCulture),
            float floatValue => Convert.ToDecimal(floatValue, CultureInfo.InvariantCulture),
            int intValue => intValue,
            long longValue => longValue,
            _ when decimal.TryParse(normalized.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    private static bool ToBoolean(object? value)
    {
        var normalized = NormalizeJsonValue(value);
        return normalized switch
        {
            bool booleanValue => booleanValue,
            decimal decimalValue => decimalValue != 0,
            double doubleValue => Math.Abs(doubleValue) > double.Epsilon,
            float floatValue => Math.Abs(floatValue) > float.Epsilon,
            int intValue => intValue != 0,
            long longValue => longValue != 0,
            _ => BooleanTextValues.Contains(ToText(normalized)?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static object? Round(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var number = ToNumber(value);
        if (number is null)
        {
            return null;
        }

        var digits = ToInt(ReadArg(args, "digits")) ?? 0;
        return Math.Round(number.Value, digits, MidpointRounding.AwayFromZero);
    }

    private static object? Fixed(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var number = ToNumber(value);
        if (number is null)
        {
            return null;
        }

        var digits = Math.Clamp(ToInt(ReadArg(args, "digits")) ?? 2, 0, 8);
        return number.Value.ToString($"F{digits}", CultureInfo.InvariantCulture);
    }

    private static object? Currency(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var number = ToNumber(value);
        if (number is null)
        {
            return null;
        }

        var symbol = ToText(ReadArg(args, "symbol")) ?? "¥";
        return $"{symbol}{number.Value.ToString("N2", CultureInfo.InvariantCulture)}";
    }

    private static object? Percent(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var number = ToNumber(value);
        if (number is null)
        {
            return null;
        }

        var digits = Math.Clamp(ToInt(ReadArg(args, "digits")) ?? 2, 0, 8);
        return (number.Value * 100).ToString($"F{digits}", CultureInfo.InvariantCulture) + "%";
    }

    private static object? Divide(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var right = ToNumber(ReadArg(args, "value")) ?? 0;
        return right == 0 ? 0 : (ToNumber(value) ?? 0) / right;
    }

    private static object? Modulo(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var right = ToNumber(ReadArg(args, "value")) ?? 0;
        return right == 0 ? 0 : (ToNumber(value) ?? 0) % right;
    }

    private static object? Clamp(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var number = ToNumber(value) ?? 0;
        var min = ToNumber(ReadArg(args, "min"));
        var max = ToNumber(ReadArg(args, "max"));
        if (min is not null && number < min)
        {
            return min;
        }

        if (max is not null && number > max)
        {
            return max;
        }

        return number;
    }

    private static DateTime? ParseDate(object? value)
    {
        if (value is DateTime dateTime)
        {
            return dateTime;
        }

        return DateTime.TryParse(ToText(value), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed
            : null;
    }

    private static object? AddDate(object? value, IReadOnlyDictionary<string, object?> args, string key)
    {
        var date = ParseDate(value) ?? DateTime.UtcNow;
        var amount = ToInt(ReadArg(args, key)) ?? 0;
        return key switch
        {
            "seconds" => date.AddSeconds(amount),
            "minutes" => date.AddMinutes(amount),
            "hours" => date.AddHours(amount),
            "weeks" => date.AddDays(amount * 7),
            "months" => date.AddMonths(amount),
            "years" => date.AddYears(amount),
            _ => date.AddDays(amount)
        };
    }

    private static object? DayBoundary(object? value, bool start)
    {
        var date = ParseDate(value) ?? DateTime.UtcNow;
        return start ? date.Date : date.Date.AddDays(1).AddTicks(-1);
    }

    private static object? PeriodBoundary(object? value, string unit, bool start)
    {
        var date = ParseDate(value) ?? DateTime.UtcNow;
        if (unit == "year")
        {
            var startOfYear = new DateTime(date.Year, 1, 1, 0, 0, 0, date.Kind);
            return start ? startOfYear : startOfYear.AddYears(1).AddTicks(-1);
        }

        var startOfMonth = new DateTime(date.Year, date.Month, 1, 0, 0, 0, date.Kind);
        return start ? startOfMonth : startOfMonth.AddMonths(1).AddTicks(-1);
    }

    private static object DiffDate(object? value, IReadOnlyDictionary<string, object?> args, string unit)
    {
        var left = ParseDate(value);
        var right = ParseDate(ReadArg(args, "value"));
        if (left is null || right is null)
        {
            return 0;
        }

        var diff = left.Value - right.Value;
        return unit switch
        {
            "seconds" => (int)diff.TotalSeconds,
            "minutes" => (int)diff.TotalMinutes,
            "hours" => (int)diff.TotalHours,
            _ => (int)diff.TotalDays
        };
    }

    private static int? WeekOfYear(object? value)
    {
        var date = ParseDate(value);
        return date is null ? null : ISOWeek.GetWeekOfYear(date.Value);
    }

    private static int? Weekday(object? value)
    {
        var date = ParseDate(value);
        return date is null ? null : (int)date.Value.DayOfWeek;
    }

    private static int? Quarter(object? value)
    {
        var date = ParseDate(value);
        return date is null ? null : ((date.Value.Month - 1) / 3) + 1;
    }

    private static string? FormatQuarter(object? value)
    {
        var date = ParseDate(value);
        return date is null ? null : $"{date.Value.Year}Q{((date.Value.Month - 1) / 3) + 1}";
    }

    private static int? AgeYears(object? value)
    {
        var birth = ParseDate(value);
        if (birth is null)
        {
            return null;
        }

        var today = DateTime.UtcNow.Date;
        var age = today.Year - birth.Value.Year;
        if (today.Month < birth.Value.Month || (today.Month == birth.Value.Month && today.Day < birth.Value.Day))
        {
            age--;
        }

        return age;
    }

    private static object? ToTimestamp(object? value)
    {
        var date = ParseDate(value);
        return date is null ? null : new DateTimeOffset(DateTime.SpecifyKind(date.Value, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
    }

    private static object? FromTimestamp(object? value)
    {
        var number = ToNumber(value);
        if (number is null)
        {
            return null;
        }

        var timestamp = decimal.ToInt64(decimal.Truncate(number.Value));
        return timestamp < 10_000_000_000
            ? DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime
            : DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime;
    }

    private static object? FormatDate(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var date = ParseDate(value);
        if (date is null)
        {
            return null;
        }

        var format = ToText(ReadArg(args, "format")) ?? "yyyy-MM-dd";
        return date.Value.ToString(format, CultureInfo.InvariantCulture);
    }

    private static object? JsonPath(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var path = ToText(ReadArg(args, "path"));
        if (string.IsNullOrWhiteSpace(path))
        {
            return value;
        }

        return RuntimeExpressionPathReader.Read(value, path.StartsWith("$.") ? path[2..] : path.TrimStart('$', '.'));
    }

    private static object? ParseJson(object? value)
    {
        if (value is not string text || string.IsNullOrWhiteSpace(text))
        {
            return NormalizeJsonValue(value);
        }

        try
        {
            return JsonSerializer.Deserialize<object?>(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<object?> Slice(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var items = ToEnumerable(value).ToArray();
        var start = Math.Clamp(ToInt(ReadArg(args, "start")) ?? 0, 0, items.Length);
        var count = Math.Clamp(ToInt(ReadArg(args, "count")) ?? items.Length, 0, items.Length - start);
        return items.Skip(start).Take(count).ToArray();
    }

    private static object? Nth(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var items = ToEnumerable(value).ToArray();
        var index = ToInt(ReadArg(args, "index")) ?? 0;
        return index >= 0 && index < items.Length ? items[index] : null;
    }

    private static IReadOnlyList<object?> Page(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var items = ToEnumerable(value).ToArray();
        var pageIndex = Math.Max(ToInt(ReadArg(args, "pageIndex")) ?? 1, 1);
        var pageSize = Math.Clamp(ToInt(ReadArg(args, "pageSize")) ?? 20, 1, 1000);
        return items.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToArray();
    }

    private static IReadOnlyList<object?> Distinct(object? value)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        return ToEnumerable(value)
            .Where(item => keys.Add(JsonSerializer.Serialize(NormalizeJsonValue(item))))
            .ToArray();
    }

    private static IReadOnlyList<object?> MapField(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var field = ToText(ReadArg(args, "field")) ?? ToText(ReadArg(args, "path"));
        if (string.IsNullOrWhiteSpace(field))
        {
            return ToEnumerable(value).ToArray();
        }

        return ToEnumerable(value).Select(item => RuntimeExpressionPathReader.Read(item, field)).ToArray();
    }

    private static object? Aggregate(object? value, IReadOnlyDictionary<string, object?> args, string mode)
    {
        var field = ToText(ReadArg(args, "field")) ?? ToText(ReadArg(args, "path"));
        var numbers = ToEnumerable(value)
            .Select(item => string.IsNullOrWhiteSpace(field) ? item : RuntimeExpressionPathReader.Read(item, field))
            .Select(ToNumber)
            .Where(number => number is not null)
            .Select(number => number!.Value)
            .ToArray();
        if (numbers.Length == 0)
        {
            return 0;
        }

        if (mode == "min")
        {
            return numbers.Min();
        }

        if (mode == "max")
        {
            return numbers.Max();
        }

        var sum = numbers.Sum();
        return mode == "average" ? sum / numbers.Length : sum;
    }

    private static IReadOnlyList<object?> FilterByField(object? value, IReadOnlyDictionary<string, object?> args, bool contains)
    {
        var field = ToText(ReadArg(args, "field")) ?? ToText(ReadArg(args, "path"));
        var expected = ToText(ReadArg(args, "value")) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(field))
        {
            return [];
        }

        return ToEnumerable(value)
            .Where(item =>
            {
                var actual = ToText(RuntimeExpressionPathReader.Read(item, field)) ?? string.Empty;
                return contains
                    ? actual.Contains(expected, StringComparison.OrdinalIgnoreCase)
                    : string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
            })
            .ToArray();
    }

    private static IReadOnlyList<object?> RejectByField(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var field = ToText(ReadArg(args, "field")) ?? ToText(ReadArg(args, "path"));
        var expected = ToText(ReadArg(args, "value")) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(field))
        {
            return ToEnumerable(value).ToArray();
        }

        return ToEnumerable(value)
            .Where(item => !string.Equals(
                ToText(RuntimeExpressionPathReader.Read(item, field)),
                expected,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static IReadOnlyList<object?> SortByField(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var field = ToText(ReadArg(args, "field")) ?? ToText(ReadArg(args, "path"));
        var desc = string.Equals(ToText(ReadArg(args, "direction")), "desc", StringComparison.OrdinalIgnoreCase);
        var items = ToEnumerable(value).ToArray();
        return desc
            ? items.OrderByDescending(item => RuntimeExpressionPathReader.Read(item, field ?? string.Empty), Comparer<object?>.Create(CompareNullableValues)).ToArray()
            : items.OrderBy(item => RuntimeExpressionPathReader.Read(item, field ?? string.Empty), Comparer<object?>.Create(CompareNullableValues)).ToArray();
    }

    private static IReadOnlyList<object?> UniqueBy(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var field = ToText(ReadArg(args, "field")) ?? ToText(ReadArg(args, "path"));
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return ToEnumerable(value)
            .Where(item => keys.Add(ToText(RuntimeExpressionPathReader.Read(item, field ?? string.Empty)) ?? string.Empty))
            .ToArray();
    }

    private static IReadOnlyDictionary<string, object?[]> GroupBy(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var field = ToText(ReadArg(args, "field")) ?? ToText(ReadArg(args, "path"));
        return ToEnumerable(value)
            .GroupBy(item => ToText(RuntimeExpressionPathReader.Read(item, field ?? string.Empty)) ?? "未分组", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, object?> PickFields(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var source = ToObject(value);
        var fields = ParseFieldList(ReadArg(args, "fields")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return source
            .Where(item => fields.Contains(item.Key))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, object?> OmitFields(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var removedFields = ParseFieldList(ReadArg(args, "fields")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return ToObject(value)
            .Where(item => !removedFields.Contains(item.Key))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ParseFieldList(object? value) =>
        (ToText(value) ?? string.Empty)
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();

    private static object? Coalesce(object? value, IReadOnlyDictionary<string, object?> args)
    {
        if (!IsEmpty(value))
        {
            return value;
        }

        foreach (var arg in args.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            var normalized = NormalizeJsonValue(arg.Value);
            if (!IsEmpty(normalized))
            {
                return normalized;
            }
        }

        return null;
    }

    private static int CompareValues(object? left, object? right)
    {
        var leftNumber = ToNumber(left);
        var rightNumber = ToNumber(right);
        if (leftNumber is not null && rightNumber is not null)
        {
            return leftNumber.Value.CompareTo(rightNumber.Value);
        }

        var leftDate = ParseDate(left);
        var rightDate = ParseDate(right);
        if (leftDate is not null && rightDate is not null)
        {
            return leftDate.Value.CompareTo(rightDate.Value);
        }

        return string.Compare(ToText(left), ToText(right), StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareNullableValues(object? left, object? right) => CompareValues(left, right);

    private static bool InList(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var candidates = (ToText(ReadArg(args, "values")) ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return candidates.Contains(ToText(value) ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }

    private static bool Between(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var number = ToNumber(value);
        if (number is null)
        {
            return false;
        }

        var min = ToNumber(ReadArg(args, "min"));
        var max = ToNumber(ReadArg(args, "max"));
        return (min is null || number >= min) && (max is null || number <= max);
    }

    private static string MaskEmail(object? value)
    {
        var email = ToText(value) ?? string.Empty;
        var parts = email.Split('@', 2);
        return parts.Length == 2
            ? $"{MaskText(parts[0], 1, 1, "***")}@{parts[1]}"
            : MaskText(email, 2, 2, "***");
    }

    private static string MaskName(object? value)
    {
        var name = ToText(value) ?? string.Empty;
        if (name.Length <= 1)
        {
            return string.IsNullOrEmpty(name) ? string.Empty : "*";
        }

        return name[..1] + new string('*', Math.Max(name.Length - 1, 1));
    }

    private static string MapValue(object? value, IReadOnlyDictionary<string, object?> args)
    {
        var mapping = ToText(ReadArg(args, "mapping")) ?? string.Empty;
        var pairs = mapping
            .Split([';', ',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && string.Equals(parts[0], ToText(value), StringComparison.OrdinalIgnoreCase))
            {
                return parts[1];
            }
        }

        return ToText(ReadArg(args, "defaultValue")) ?? ToText(value) ?? string.Empty;
    }

    private static string MaskText(string value, int start, int end, string mask)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        start = Math.Max(start, 0);
        end = Math.Max(end, 0);
        if (value.Length <= start + end)
        {
            return mask;
        }

        return value[..start] + mask + (end > 0 ? value[^end..] : string.Empty);
    }

    private static IEnumerable<object?> ToEnumerable(object? value)
    {
        var normalized = NormalizeJsonValue(value);
        if (normalized is null || normalized is string)
        {
            return [];
        }

        if (normalized is JsonElement element && element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray().Select(item => NormalizeJsonValue(item)).ToArray();
        }

        if (normalized is global::System.Collections.IEnumerable enumerable)
        {
            return enumerable.Cast<object?>().ToArray();
        }

        return [normalized];
    }

    private static IEnumerable<object?> FlattenOneLevel(object? value)
    {
        var normalized = NormalizeJsonValue(value);
        if (normalized is null || normalized is string)
        {
            return [normalized];
        }

        if (normalized is JsonElement { ValueKind: JsonValueKind.Array } element)
        {
            return element.EnumerateArray().Select(item => NormalizeJsonValue(item)).ToArray();
        }

        if (normalized is global::System.Collections.IEnumerable enumerable)
        {
            return enumerable.Cast<object?>().ToArray();
        }

        return [normalized];
    }

    private static bool IsArray(object? value)
    {
        var normalized = NormalizeJsonValue(value);
        return normalized is JsonElement { ValueKind: JsonValueKind.Array } ||
            normalized is global::System.Collections.IEnumerable and not string;
    }

    private static int? ToInt(object? value) =>
        int.TryParse(ToText(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static bool IsInteger(object? value)
    {
        var number = ToNumber(value);
        return number is not null && decimal.Truncate(number.Value) == number.Value;
    }

    private static bool IsUrl(object? value) =>
        Uri.TryCreate(ToText(value), UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static bool IsJson(object? value)
    {
        if (NormalizeJsonValue(value) is not string text || string.IsNullOrWhiteSpace(text))
        {
            return value is JsonElement { ValueKind: JsonValueKind.Array or JsonValueKind.Object } ||
                value is IReadOnlyDictionary<string, object?> or IDictionary<string, object?>;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind is JsonValueKind.Array or JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static int CountValue(object? value)
    {
        var normalized = NormalizeJsonValue(value);
        return normalized switch
        {
            null => 0,
            string text => text.Length,
            JsonElement { ValueKind: JsonValueKind.Array } element => element.GetArrayLength(),
            global::System.Collections.IEnumerable enumerable => enumerable.Cast<object?>().Count(),
            _ => 1
        };
    }

    private static bool IsEmpty(object? value)
    {
        var normalized = NormalizeJsonValue(value);
        if (normalized is null)
        {
            return true;
        }

        if (normalized is string text)
        {
            return string.IsNullOrWhiteSpace(text);
        }

        return false;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpaceRegex();

    [GeneratedRegex(@"\r?\n")]
    private static partial Regex LineBreakRegex();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\D+")]
    private static partial Regex NonDigitRegex();

    [GeneratedRegex(@"[^A-Za-z]+")]
    private static partial Regex NonLetterRegex();

    [GeneratedRegex(@"[^\d\.-]+")]
    private static partial Regex CurrencyNoiseRegex();

    [GeneratedRegex(@"[\u4e00-\u9fa5]")]
    private static partial Regex ChineseRegex();

    [GeneratedRegex(@"[^\u4e00-\u9fa5]+")]
    private static partial Regex NonChineseRegex();

    [GeneratedRegex(@"([a-z])([A-Z])")]
    private static partial Regex CamelCaseBoundaryRegex();

    [GeneratedRegex(@"[^A-Za-z0-9\u4e00-\u9fa5]+")]
    private static partial Regex WordSplitRegex();

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"^1[3-9]\d{9}$")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"^\d{6}$")]
    private static partial Regex PostalCodeRegex();

    private static readonly string[] BooleanTextValues = ["1", "true", "yes", "y", "on", "是", "启用"];
}
