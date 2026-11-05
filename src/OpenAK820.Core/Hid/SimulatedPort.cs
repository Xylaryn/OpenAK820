namespace OpenAK820.Hid;

/// <summary>A pretend keyboard that records traffic. Used by tests and the app's <c>--simulate</c> mode.</summary>
public sealed class SimulatedPort : IHidPort
{
    private readonly object _gate = new();
    private byte[] _lastFeature = new byte[65];
    public List<byte[]> Features { get; } = new();
    public List<byte[]> Writes { get; } = new();
    /// <summary>Status byte returned for commands. Set to FF to imitate a busy keyboard.</summary>
    public byte Status { get; set; } = 1;
    public int Battery { get; set; } = 87;

    public void SetFeature(byte[] report)
    {
        lock (_gate)
        {
            Features.Add((byte[])report.Clone());
            _lastFeature = (byte[])report.Clone();
            _lastFeature[4] = Status;
        }
    }

    /// <summary>When set, replies show this frame, imitating another program talking to the keyboard.</summary>
    public byte[]? EchoOverride { get; set; }

    public void GetFeature(byte[] report)
    {
        lock (_gate) Array.Copy(EchoOverride ?? _lastFeature, report, Math.Min(report.Length, _lastFeature.Length));
    }

    public void Write(byte[] report)
    {
        lock (_gate) Writes.Add((byte[])report.Clone());
    }

    public int Read(byte[] buffer, int timeoutMs)
    {
        lock (_gate)
        {
            if (Writes.Count == 0) return 0;
            var last = Writes[^1];
            if (last.Length == 33)
            {
                buffer[0] = 0;
                Array.Copy(last, 1, buffer, 1, 3);
                if (last[1] == 0x20) buffer[4] = (byte)Battery;
                return 33;
            }
            return buffer.Length;
        }
    }

    public void Dispose() { }
}
