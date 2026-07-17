using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AsterERP.Api.Infrastructure.Abp.Settings;
using Volo.Abp.Settings;
using Volo.Abp.Sms;

namespace AsterERP.Api.Infrastructure.Messaging;

public sealed class AsterErpSmsSender(
    IHttpClientFactory httpClientFactory,
    ISettingProvider settingProvider,
    ILogger<AsterErpSmsSender> logger) : ISmsSender
{
    private const string AliyunProvider = "Aliyun";
    private const string TencentProvider = "Tencent";
    private const string AliyunEndpoint = "https://dysmsapi.aliyuncs.com/";
    private const string TencentEndpoint = "https://sms.tencentcloudapi.com/";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task SendAsync(SmsMessage smsMessage)
    {
        var provider = await GetRequiredSettingAsync(AsterErpSettingNames.SmsProvider, "短信 Provider 未配置");
        if (provider.Equals(AliyunProvider, StringComparison.OrdinalIgnoreCase))
        {
            await SendAliyunAsync(smsMessage);
            return;
        }

        if (provider.Equals(TencentProvider, StringComparison.OrdinalIgnoreCase))
        {
            await SendTencentAsync(smsMessage);
            return;
        }

        throw new InvalidOperationException($"短信 Provider {provider} 不受支持");
    }

    private async Task SendAliyunAsync(SmsMessage smsMessage)
    {
        var accessKeyId = await GetRequiredSettingAsync(AsterErpSettingNames.SmsAliyunAccessKeyId, "阿里云短信 AccessKeyId 未配置");
        var accessKeySecret = await GetRequiredSettingAsync(AsterErpSettingNames.SmsAliyunAccessKeySecret, "阿里云短信 AccessKeySecret 未配置");
        var signName = await GetRequiredSettingAsync(AsterErpSettingNames.SmsAliyunSignName, "阿里云短信签名未配置");
        var templateCode = await GetRequiredSettingAsync(AsterErpSettingNames.SmsAliyunTemplateCode, "阿里云短信模板码未配置");
        var templateParamName = await GetSettingOrDefaultAsync(AsterErpSettingNames.SmsAliyunTemplateParamName, "content");
        var parameters = BuildAliyunParameters(smsMessage, accessKeyId, signName, templateCode, templateParamName);
        var signature = CreateAliyunSignature(parameters, accessKeySecret);
        var requestUri = $"{AliyunEndpoint}?Signature={PercentEncode(signature)}&{BuildAliyunCanonicalQuery(parameters)}";
        var httpClient = httpClientFactory.CreateClient(nameof(AsterErpSmsSender));
        var response = await httpClient.GetAsync(requestUri);
        var responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"阿里云短信请求失败：HTTP {(int)response.StatusCode} {responseText}");
        }

        using var document = JsonDocument.Parse(responseText);
        var code = TryGetString(document.RootElement, "Code");
        if (!string.Equals(code, "OK", StringComparison.OrdinalIgnoreCase))
        {
            var message = TryGetString(document.RootElement, "Message") ?? code ?? "未知错误";
            throw new InvalidOperationException($"阿里云短信发送失败：{message}");
        }

        logger.LogInformation("Aliyun SMS sent: phone={PhoneNumber}", smsMessage.PhoneNumber);
    }

    private async Task SendTencentAsync(SmsMessage smsMessage)
    {
        var secretId = await GetRequiredSettingAsync(AsterErpSettingNames.SmsTencentSecretId, "腾讯云短信 SecretId 未配置");
        var secretKey = await GetRequiredSettingAsync(AsterErpSettingNames.SmsTencentSecretKey, "腾讯云短信 SecretKey 未配置");
        var smsSdkAppId = await GetRequiredSettingAsync(AsterErpSettingNames.SmsTencentSdkAppId, "腾讯云短信 SmsSdkAppId 未配置");
        var signName = await GetRequiredSettingAsync(AsterErpSettingNames.SmsTencentSignName, "腾讯云短信签名未配置");
        var templateId = await GetRequiredSettingAsync(AsterErpSettingNames.SmsTencentTemplateId, "腾讯云短信模板 ID 未配置");
        var region = await GetSettingOrDefaultAsync(AsterErpSettingNames.SmsTencentRegion, "ap-guangzhou");
        var payload = JsonSerializer.Serialize(new
        {
            PhoneNumberSet = new[] { smsMessage.PhoneNumber },
            SmsSdkAppId = smsSdkAppId,
            SignName = signName,
            TemplateId = templateId,
            TemplateParamSet = new[] { smsMessage.Text }
        }, JsonOptions);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var authorization = CreateTencentAuthorization(payload, secretId, secretKey, timestamp);
        using var request = new HttpRequestMessage(HttpMethod.Post, TencentEndpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("TC3-HMAC-SHA256", authorization);
        request.Headers.Add("X-TC-Action", "SendSms");
        request.Headers.Add("X-TC-Timestamp", timestamp.ToString(CultureInfo.InvariantCulture));
        request.Headers.Add("X-TC-Version", "2021-01-11");
        request.Headers.Add("X-TC-Region", region);
        var httpClient = httpClientFactory.CreateClient(nameof(AsterErpSmsSender));
        var response = await httpClient.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"腾讯云短信请求失败：HTTP {(int)response.StatusCode} {responseText}");
        }

        using var document = JsonDocument.Parse(responseText);
        var responseElement = document.RootElement.GetProperty("Response");
        if (responseElement.TryGetProperty("Error", out var errorElement))
        {
            var code = TryGetString(errorElement, "Code") ?? "Unknown";
            var message = TryGetString(errorElement, "Message") ?? "未知错误";
            throw new InvalidOperationException($"腾讯云短信发送失败：{code} {message}");
        }

        logger.LogInformation("Tencent SMS sent: phone={PhoneNumber}", smsMessage.PhoneNumber);
    }

    private static SortedDictionary<string, string> BuildAliyunParameters(
        SmsMessage smsMessage,
        string accessKeyId,
        string signName,
        string templateCode,
        string templateParamName)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var templateParam = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            [templateParamName] = smsMessage.Text
        }, JsonOptions);

        return new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["AccessKeyId"] = accessKeyId,
            ["Action"] = "SendSms",
            ["Format"] = "JSON",
            ["PhoneNumbers"] = smsMessage.PhoneNumber,
            ["RegionId"] = "cn-hangzhou",
            ["SignName"] = signName,
            ["SignatureMethod"] = "HMAC-SHA1",
            ["SignatureNonce"] = Guid.NewGuid().ToString("N"),
            ["SignatureVersion"] = "1.0",
            ["TemplateCode"] = templateCode,
            ["TemplateParam"] = templateParam,
            ["Timestamp"] = timestamp,
            ["Version"] = "2017-05-25"
        };
    }

    private static string CreateAliyunSignature(SortedDictionary<string, string> parameters, string accessKeySecret)
    {
        var canonicalQuery = BuildAliyunCanonicalQuery(parameters);
        var stringToSign = $"GET&%2F&{PercentEncode(canonicalQuery)}";
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes($"{accessKeySecret}&"));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
    }

    private static string BuildAliyunCanonicalQuery(SortedDictionary<string, string> parameters)
    {
        return string.Join("&", parameters.Select(item => $"{PercentEncode(item.Key)}={PercentEncode(item.Value)}"));
    }

    private static string CreateTencentAuthorization(string payload, string secretId, string secretKey, long timestamp)
    {
        const string service = "sms";
        const string host = "sms.tencentcloudapi.com";
        const string canonicalUri = "/";
        const string canonicalQueryString = "";
        const string signedHeaders = "content-type;host;x-tc-action";
        var date = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var canonicalHeaders = $"content-type:application/json; charset=utf-8\nhost:{host}\nx-tc-action:sendsms\n";
        var hashedPayload = Sha256Hex(payload);
        var canonicalRequest = $"POST\n{canonicalUri}\n{canonicalQueryString}\n{canonicalHeaders}\n{signedHeaders}\n{hashedPayload}";
        var credentialScope = $"{date}/{service}/tc3_request";
        var stringToSign = $"TC3-HMAC-SHA256\n{timestamp}\n{credentialScope}\n{Sha256Hex(canonicalRequest)}";
        var secretDate = HmacSha256(Encoding.UTF8.GetBytes($"TC3{secretKey}"), date);
        var secretService = HmacSha256(secretDate, service);
        var secretSigning = HmacSha256(secretService, "tc3_request");
        var signature = Convert.ToHexString(HmacSha256(secretSigning, stringToSign)).ToLowerInvariant();
        return $"Credential={secretId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
    }

    private async Task<string> GetRequiredSettingAsync(string settingName, string message)
    {
        var value = await settingProvider.GetOrNullAsync(settingName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(message);
        }

        return value.Trim();
    }

    private async Task<string> GetSettingOrDefaultAsync(string settingName, string defaultValue)
    {
        var value = await settingProvider.GetOrNullAsync(settingName);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
    }

    private static string PercentEncode(string value)
    {
        return Uri.EscapeDataString(value)
            .Replace("+", "%20", StringComparison.Ordinal)
            .Replace("*", "%2A", StringComparison.Ordinal)
            .Replace("%7E", "~", StringComparison.Ordinal);
    }

    private static string Sha256Hex(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static byte[] HmacSha256(byte[] key, string value)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }
}
