using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NoteLab.Infrastructure;

public sealed record SafeHttpResponse(Uri FinalUri, string ContentType, byte[] Body, HttpStatusCode StatusCode)
{
    public string ReadText() => Encoding.UTF8.GetString(Body);
}

public sealed class SafeHttpException(string message) : Exception(message);

/// <summary>HTTP(S) client with DNS/IP validation, redirect revalidation and bounded response bodies.</summary>
public sealed class SafeHttpClient : IDisposable
{
    public const int MaxBytes = 10 * 1024 * 1024;
    private readonly HttpClient _client;
    private readonly bool _allowLoopback;

    public SafeHttpClient(IConfiguration config)
    {
        _allowLoopback = config.GetValue<bool>("Sources:SafeHttp:AllowLoopbackForTests") &&
            (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Testing" ||
             Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Testing");
        var handler = new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(10), PooledConnectionLifetime = TimeSpan.FromMinutes(2) };
        handler.ConnectCallback = ConnectAsync;
        _client = CreateClient(handler);
    }

    internal SafeHttpClient(HttpMessageHandler handler)
    {
        _allowLoopback = false;
        _client = CreateClient(handler);
    }

    public async Task<SafeHttpResponse> GetAsync(string value, CancellationToken ct, bool allowJson = false)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)) throw new SafeHttpException("URL inválida.");
        for (int redirects = 0; ; redirects++)
        {
            ValidateUri(uri);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using HttpResponseMessage response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
            {
                if (redirects >= 5) throw new SafeHttpException("Limite de redirecionamentos excedido.");
                uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
                continue;
            }
            string mediaType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? "application/octet-stream";
            if (response.Content.Headers.ContentLength > MaxBytes) throw new SafeHttpException("Resposta excede 10 MB.");
            await using Stream input = await response.Content.ReadAsStreamAsync(ct);
            using var output = new MemoryStream();
            byte[] buffer = new byte[32 * 1024];
            int read;
            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                if (output.Length + read > MaxBytes) throw new SafeHttpException("Resposta excede 10 MB.");
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            byte[] body = output.ToArray();
            if (body.Length > 0 && !AllowedContentType(mediaType, allowJson))
                throw new SafeHttpException("Tipo de conteúdo não permitido para leitura de fontes.");
            return new SafeHttpResponse(uri, mediaType, body, response.StatusCode);
        }
    }

    public void Dispose() => _client.Dispose();

    private static HttpClient CreateClient(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NoteeSourceMetadata/1.0 (+https://notee.app)");
        return client;
    }

    private void ValidateUri(Uri uri)
    {
        bool testLoopbackHost = _allowLoopback &&
            ((IPAddress.TryParse(uri.Host, out IPAddress? testAddress) && IPAddress.IsLoopback(testAddress)) ||
             uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase));
        if ((uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            ((!testLoopbackHost) && ((uri.Scheme == Uri.UriSchemeHttp && uri.Port != 80) ||
             (uri.Scheme == Uri.UriSchemeHttps && uri.Port != 443))))
            throw new SafeHttpException("Somente URLs http(s) nas portas 80 ou 443 são permitidas.");
        if (IPAddress.TryParse(uri.Host, out IPAddress? literal))
        {
            if (IsBlocked(literal) && !(_allowLoopback && IPAddress.IsLoopback(literal)))
                throw new SafeHttpException("Endereço de rede local ou reservado bloqueado.");
            return;
        }
        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) && !_allowLoopback)
            throw new SafeHttpException("Endereço local bloqueado.");
    }

    private async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        IPAddress[] addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
        foreach (IPAddress address in addresses)
        {
            if (IsBlocked(address) && !(_allowLoopback && IPAddress.IsLoopback(address))) continue;
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { socket.Dispose(); throw; }
            catch (SocketException) { socket.Dispose(); }
        }
        throw new SafeHttpException("DNS aponta para endereço local/reservado ou não foi possível conectar.");
    }

    private static bool AllowedContentType(string type, bool allowJson) => type is "text/html" or "application/xhtml+xml" or "application/pdf" or
        "text/plain" or "text/xml" or "application/xml" || (allowJson && type is "application/json" or "application/ld+json") || type.StartsWith("text/", StringComparison.Ordinal);

    private static bool IsBlocked(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            byte[] v6 = ip.GetAddressBytes();
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast || ip.IsIPv6Teredo ||
                ip.Equals(IPAddress.IPv6None) || ip.Equals(IPAddress.IPv6Any) || (v6[0] & 0xfe) == 0xfc ||
                (v6[0] == 0x20 && v6[1] == 0x01 && v6[2] == 0x0d && v6[3] == 0xb8);
        }
        byte[] b = ip.GetAddressBytes();
        return b[0] == 0 || b[0] == 10 || b[0] == 127 || b[0] >= 224 ||
               (b[0] == 169 && b[1] == 254) || (b[0] == 172 && b[1] is >= 16 and <= 31) ||
               (b[0] == 192 && b[1] == 168) || (b[0] == 100 && b[1] is >= 64 and <= 127) ||
               (b[0] == 192 && b[1] == 0) || (b[0] == 192 && b[1] == 88 && b[2] == 99) ||
               (b[0] == 192 && b[1] == 2 && b[2] == 0) || (b[0] == 198 && b[1] is 18 or 19) ||
               (b[0] == 198 && b[1] == 51 && b[2] == 100) || (b[0] == 203 && b[1] == 0 && b[2] == 113);
    }
}
