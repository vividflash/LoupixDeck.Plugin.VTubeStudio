using System.Buffers.Binary;
using LoupixDeck.Plugin.VTubeStudio.Vts;

namespace LoupixDeck.Plugin.VTubeStudio.Tests;

public class IconTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static byte[] ReadIcon()
    {
        using var stream = typeof(VtsService).Assembly.GetManifestResourceStream(VtsService.IconResource);
        Assert.NotNull(stream);
        using var ms = new MemoryStream();
        stream!.CopyTo(ms);
        return ms.ToArray();
    }

    [Fact]
    public void Embedded_icon_is_a_128x128_png()
    {
        var png = ReadIcon();

        Assert.True(png.AsSpan(0, 8).SequenceEqual(PngSignature));
        Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(png, 12, 4));
        Assert.Equal(128, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(128, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
    }
}
