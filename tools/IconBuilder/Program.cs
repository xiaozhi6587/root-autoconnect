using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
var entries = new List<(int Size, byte[] Png)>();
foreach (var size in new[] { 16, 24, 32, 48, 64, 128, 256 })
{
    var visual = new DrawingVisual();
    using (var context = visual.RenderOpen())
    {
        context.PushTransform(new ScaleTransform(size / 256.0, size / 256.0));
        context.DrawRoundedRectangle(Brushes.Black, null, new Rect(8, 8, 240, 240), 52, 52);
        var pen = new Pen(Brushes.White, 17) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        context.DrawGeometry(null, pen, Geometry.Parse("M91,80 L49,128 L91,176 M165,80 L207,128 L165,176 M111,128 L145,128"));
        context.Pop();
    }
    var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
    bitmap.Render(visual);
    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using var data = new MemoryStream(); encoder.Save(data); entries.Add((size, data.ToArray()));
    if (size == 256) File.WriteAllBytes(Path.ChangeExtension(output, ".png"), data.ToArray());
}
using var writer = new BinaryWriter(File.Create(output));
writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)entries.Count);
var offset = 6 + 16 * entries.Count;
foreach (var (size, png) in entries)
{
    writer.Write((byte)(size == 256 ? 0 : size)); writer.Write((byte)(size == 256 ? 0 : size));
    writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
    writer.Write(png.Length); writer.Write(offset); offset += png.Length;
}
foreach (var (_, png) in entries) writer.Write(png);
Console.WriteLine("Created monochrome 16–256px icon: " + output);
