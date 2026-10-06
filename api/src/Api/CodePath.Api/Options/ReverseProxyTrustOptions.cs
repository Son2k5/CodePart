using System.Net;

namespace CodePath.Api.Options;

public sealed class ReverseProxyTrustOptions
{
    public const string SectionName = "ReverseProxy";

    public string[] KnownNetworks { get; init; } = [];
    public string[] KnownProxies { get; init; } = [];
    public int ForwardLimit { get; init; } = 1;

    public bool HasValidAddresses() =>
        KnownNetworks.All(IsValidNetwork) &&
        KnownProxies.All(value => IPAddress.TryParse(value, out _));

    public static bool IsValidNetwork(string value)
    {
        var parts = value.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !IPAddress.TryParse(parts[0], out var address)
            || !int.TryParse(parts[1], out var prefix))
        {
            return false;
        }

        var maximumPrefix = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            ? 32
            : 128;
        return prefix >= 0 && prefix <= maximumPrefix;
    }
}
