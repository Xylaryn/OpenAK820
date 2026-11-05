using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace OpenAK820.Services;

public sealed record GifFrame(Bitmap Image, int DelayMs);

/// <summary>Reads images and animated GIFs into 128x128 frames, and writes animated GIFs. No extra libraries.</summary>
public static class GifCodec
{
    public const int Size = 128;

    public static List<GifFrame> Load(string path)
    {
        using var source = Image.FromFile(path);
        var frames = new List<GifFrame>();
        var dimension = new FrameDimension(source.FrameDimensionsList[0]);
        var count = source.GetFrameCount(dimension);
        var delays = new int[count];
        if (count > 1 && source.PropertyIdList.Contains(0x5100))
        {
            var bytes = source.GetPropertyItem(0x5100)!.Value!;
            for (int i = 0; i < count && (i + 1) * 4 <= bytes.Length; i++)
                delays[i] = BitConverter.ToInt32(bytes, i * 4) * 10;
        }
        for (int i = 0; i < count; i++)
        {
            source.SelectActiveFrame(dimension, i);
            frames.Add(new GifFrame(Fit(source), Math.Clamp(delays[i] <= 0 ? 100 : delays[i], 30, 500)));
        }
        return frames;
    }

    /// <summary>Scales an image to fill the screen, cropping the overflow, as a square screen needs.</summary>
    public static Bitmap Fit(Image source)
    {
        var bitmap = new Bitmap(Size, Size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Black);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var scale = Math.Max(Size / (double)source.Width, Size / (double)source.Height);
        var w = (int)Math.Ceiling(source.Width * scale);
        var h = (int)Math.Ceiling(source.Height * scale);
        // Without this, bicubic scaling blends the picture's edge with the black behind it.
        using var attributes = new ImageAttributes();
        attributes.SetWrapMode(WrapMode.TileFlipXY);
        g.DrawImage(source, new Rectangle((Size - w) / 2, (Size - h) / 2, w, h), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
        return bitmap;
    }

    public static unsafe ushort[] ToRgb565(Bitmap image)
    {
        var pixels = new ushort[Size * Size];
        var data = image.LockBits(new Rectangle(0, 0, Size, Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < Size; y++)
            {
                var row = (byte*)data.Scan0 + y * data.Stride;
                for (int x = 0; x < Size; x++)
                {
                    var (b, g, r) = (row[x * 4], row[x * 4 + 1], row[x * 4 + 2]);
                    pixels[y * Size + x] = Protocol.Encoders.ToRgb565(r, g, b);
                }
            }
        }
        finally { image.UnlockBits(data); }
        return pixels;
    }

    public static void Save(string path, IReadOnlyList<GifFrame> frames)
    {
        using var stream = File.Create(path);
        using var w = new BinaryWriter(stream);
        w.Write("GIF89a"u8);
        w.Write((ushort)Size); w.Write((ushort)Size);
        w.Write((byte)0x70); w.Write((byte)0); w.Write((byte)0);
        // Loop forever.
        w.Write(new byte[] { 0x21, 0xFF, 0x0B });
        w.Write("NETSCAPE2.0"u8);
        w.Write(new byte[] { 3, 1, 0, 0, 0 });
        foreach (var frame in frames)
        {
            var (palette, indices) = Quantize(frame.Image);
            w.Write(new byte[] { 0x21, 0xF9, 4, 0 });
            w.Write((ushort)Math.Max(frame.DelayMs / 10, 1));
            w.Write(new byte[] { 0, 0 });
            w.Write((byte)0x2C);
            w.Write((ushort)0); w.Write((ushort)0);
            w.Write((ushort)Size); w.Write((ushort)Size);
            w.Write((byte)0x87); // Local table of 256 colours.
            w.Write(palette);
            WriteLzw(w, indices);
        }
        w.Write((byte)0x3B);
    }

    /// <summary>Uses a fixed 6-7-6 colour cube, which is fast and good enough for a 128-pixel screen.</summary>
    private static unsafe (byte[] Palette, byte[] Indices) Quantize(Bitmap image)
    {
        var palette = new byte[768];
        for (int r = 0, i = 0; r < 6; r++)
        for (int g = 0; g < 7; g++)
        for (int b = 0; b < 6; b++, i++)
        {
            palette[i * 3] = (byte)(r * 255 / 5);
            palette[i * 3 + 1] = (byte)(g * 255 / 6);
            palette[i * 3 + 2] = (byte)(b * 255 / 5);
        }
        var indices = new byte[Size * Size];
        var data = image.LockBits(new Rectangle(0, 0, Size, Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < Size; y++)
            {
                var row = (byte*)data.Scan0 + y * data.Stride;
                for (int x = 0; x < Size; x++)
                {
                    int b = (row[x * 4] * 5 + 127) / 255, g = (row[x * 4 + 1] * 6 + 127) / 255, r = (row[x * 4 + 2] * 5 + 127) / 255;
                    indices[y * Size + x] = (byte)(r * 42 + g * 6 + b);
                }
            }
        }
        finally { image.UnlockBits(data); }
        return (palette, indices);
    }

    private static void WriteLzw(BinaryWriter w, byte[] indices)
    {
        const int MinCode = 8, Clear = 1 << MinCode, End = Clear + 1;
        w.Write((byte)MinCode);
        var block = new List<byte>(255);
        int bitBuffer = 0, bitCount = 0, width = MinCode + 1, next = End + 1;
        var table = new Dictionary<int, int>();

        void Emit(int code)
        {
            bitBuffer |= code << bitCount;
            bitCount += width;
            while (bitCount >= 8)
            {
                block.Add((byte)bitBuffer);
                bitBuffer >>= 8;
                bitCount -= 8;
                if (block.Count == 255) Flush();
            }
        }
        void Flush()
        {
            if (block.Count == 0) return;
            w.Write((byte)block.Count);
            w.Write(block.ToArray());
            block.Clear();
        }

        Emit(Clear);
        int prefix = indices[0];
        for (int i = 1; i < indices.Length; i++)
        {
            int suffix = indices[i], key = (prefix << 8) | suffix;
            if (table.TryGetValue(key, out var code)) { prefix = code; continue; }
            Emit(prefix);
            if (next < 4096)
            {
                // The width grows after the code that would no longer fit has been assigned.
                if (next == (1 << width) && width < 12) width++;
                table[key] = next++;
            }
            else
            {
                Emit(Clear);
                table.Clear();
                next = End + 1;
                width = MinCode + 1;
            }
            prefix = suffix;
        }
        Emit(prefix);
        Emit(End);
        if (bitCount > 0) block.Add((byte)bitBuffer);
        Flush();
        w.Write((byte)0);
    }
}
