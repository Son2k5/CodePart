using System.Buffers.Binary;

namespace CodePath.Application.Admin.Queries;

internal readonly record struct AdminCursor(DateTime Timestamp, Guid Id)
{
    public static string Encode(DateTime timestamp, Guid id)
    {
        Span<byte> bytes = stackalloc byte[24];
        BinaryPrimitives.WriteInt64BigEndian(bytes, timestamp.ToUniversalTime().Ticks);
        id.TryWriteBytes(bytes[8..]);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static bool TryDecode(string? value, out AdminCursor cursor)
    {
        cursor = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');

        try
        {
            var bytes = Convert.FromBase64String(base64);
            if (bytes.Length != 24)
                return false;

            var ticks = BinaryPrimitives.ReadInt64BigEndian(bytes.AsSpan(0, 8));
            if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
                return false;

            cursor = new AdminCursor(
                new DateTime(ticks, DateTimeKind.Utc),
                new Guid(bytes.AsSpan(8, 16)));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
