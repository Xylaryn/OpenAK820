using System.Numerics;
using System.Runtime.InteropServices;

namespace OpenAK820.Services;

/// <summary>Captures what the PC is playing using WASAPI loopback. Produces band levels between 0 and 1.</summary>
public sealed class AudioLoopback : IDisposable
{
    public const int BandCount = 16;
    private const int FftSize = 1024;
    private readonly double[] _bands = new double[BandCount];
    private readonly object _gate = new();
    private Thread? _thread;
    private volatile bool _stop;

    public double[] Bands { get { lock (_gate) return (double[])_bands.Clone(); } }
    public string? Error { get; private set; }

    public void Start()
    {
        _stop = false;
        Error = null;
        _thread = new Thread(Run) { IsBackground = true, Name = "audio-loopback" };
        _thread.SetApartmentState(ApartmentState.MTA);
        _thread.Start();
    }

    public void Stop()
    {
        _stop = true;
        _thread?.Join(1000);
        _thread = null;
        lock (_gate) Array.Clear(_bands);
    }

    public void Dispose() => Stop();

    private void Run()
    {
        try { Capture(); }
        catch (Exception e) { Error = e.Message; }
    }

    private unsafe void Capture()
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(0, 1, out var device));
        var iid = typeof(IAudioClient).GUID;
        Marshal.ThrowExceptionForHR(device.Activate(ref iid, 1, IntPtr.Zero, out var clientObj));
        var client = (IAudioClient)clientObj;
        Marshal.ThrowExceptionForHR(client.GetMixFormat(out var formatPtr));
        var format = Marshal.PtrToStructure<WaveFormatEx>(formatPtr);
        // AUDCLNT_STREAMFLAGS_LOOPBACK, with a 100 ms shared-mode buffer.
        Marshal.ThrowExceptionForHR(client.Initialize(0, 0x00020000, 1_000_000, 0, formatPtr, IntPtr.Zero));
        var captureIid = typeof(IAudioCaptureClient).GUID;
        Marshal.ThrowExceptionForHR(client.GetService(ref captureIid, out var captureObj));
        var capture = (IAudioCaptureClient)captureObj;
        Marshal.ThrowExceptionForHR(client.Start());

        var isFloat = format.BitsPerSample == 32; // The shared-mode mix format is 32-bit float on all current Windows.
        var window = new double[FftSize];
        var fft = new Complex[FftSize];
        int filled = 0;
        try
        {
            while (!_stop)
            {
                Thread.Sleep(10);
                while (capture.GetNextPacketSize(out var frames) == 0 && frames > 0)
                {
                    if (capture.GetBuffer(out var data, out var count, out var flags, out _, out _) != 0) break;
                    if ((flags & 2) == 0 && isFloat)
                    {
                        var samples = (float*)data;
                        for (int i = 0; i < count; i++)
                        {
                            double mono = 0;
                            for (int c = 0; c < format.Channels; c++) mono += samples[i * format.Channels + c];
                            window[filled++] = mono / format.Channels;
                            if (filled == FftSize)
                            {
                                Analyse(window, fft, format.SamplesPerSec);
                                filled = FftSize / 2;
                                Array.Copy(window, FftSize / 2, window, 0, FftSize / 2);
                            }
                        }
                    }
                    capture.ReleaseBuffer(count);
                }
            }
        }
        finally
        {
            client.Stop();
            Marshal.FreeCoTaskMem(formatPtr);
        }
    }

    private void Analyse(double[] samples, Complex[] fft, int rate)
    {
        for (int i = 0; i < FftSize; i++)
        {
            var hann = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (FftSize - 1));
            fft[i] = new Complex(samples[i] * hann, 0);
        }
        Transform(fft);
        lock (_gate)
        {
            for (int b = 0; b < BandCount; b++)
            {
                // Log-spaced bands from 40 Hz to 16 kHz.
                var lo = 40 * Math.Pow(400, b / (double)BandCount);
                var hi = 40 * Math.Pow(400, (b + 1) / (double)BandCount);
                int from = Math.Clamp((int)(lo * FftSize / rate), 1, FftSize / 2 - 1);
                int to = Math.Clamp((int)(hi * FftSize / rate), from + 1, FftSize / 2);
                double peak = 0;
                for (int i = from; i < to; i++) peak = Math.Max(peak, fft[i].Magnitude);
                var level = Math.Clamp((20 * Math.Log10(peak * 4 / FftSize + 1e-9) + 60) / 60, 0, 1);
                _bands[b] = Math.Max(level, _bands[b] * 0.82); // Rise instantly, fall gently.
            }
        }
    }

    public static void Transform(Complex[] a)
    {
        int n = a.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) (a[i], a[j]) = (a[j], a[i]);
        }
        for (int len = 2; len <= n; len <<= 1)
        {
            var step = Complex.FromPolarCoordinates(1, -2 * Math.PI / len);
            for (int i = 0; i < n; i += len)
            {
                var w = Complex.One;
                for (int k = 0; k < len / 2; k++)
                {
                    var u = a[i + k];
                    var v = a[i + k + len / 2] * w;
                    a[i + k] = u + v;
                    a[i + k + len / 2] = u - v;
                    w *= step;
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct WaveFormatEx { public ushort Tag, Channels; public int SamplesPerSec, BytesPerSec; public ushort BlockAlign, BitsPerSample, Size; }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] private class MMDeviceEnumerator { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, int mask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int context, IntPtr activation, [MarshalAs(UnmanagedType.IUnknown)] out object result);
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioClient
    {
        [PreserveSig] int Initialize(int shareMode, int flags, long bufferDuration, long periodicity, IntPtr format, IntPtr session);
        [PreserveSig] int GetBufferSize(out int frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out int frames);
        [PreserveSig] int IsFormatSupported(int mode, IntPtr format, out IntPtr closest);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr handle);
        [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    [ComImport, Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out int frames, out int flags, out long position, out long qpc);
        [PreserveSig] int ReleaseBuffer(int frames);
        [PreserveSig] int GetNextPacketSize(out int frames);
    }
}
