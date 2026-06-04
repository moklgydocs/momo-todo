using System.Drawing;
using System.Drawing.Imaging;

var bmp = new Bitmap(32, 32);
using var g = Graphics.FromImage(bmp);
g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

// Pink circle background
using var bg = new SolidBrush(Color.FromArgb(255, 255, 143, 165));
g.FillEllipse(bg, 1, 1, 30, 30);

// Checkmark
using var pen = new Pen(Color.White, 3) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
g.DrawLine(pen, 8, 16, 14, 22);
g.DrawLine(pen, 14, 22, 24, 10);

// Save as ICO
using var ms = new MemoryStream();
bmp.Save(ms, ImageFormat.Png);
var pngData = ms.ToArray();

var icoHeader = new byte[]
{
    0,0, 1,0, 1,0,   // ICO header
    32,32, 0, 0, 1,0, 32,0,   // Image entry: 32x32, 32-bit
    (byte)(pngData.Length & 0xFF),
    (byte)((pngData.Length >> 8) & 0xFF),
    (byte)((pngData.Length >> 16) & 0xFF),
    (byte)((pngData.Length >> 24) & 0xFF),
    22,0,0,0  // Offset to PNG data (22 = header size)
};

var path = args.Length > 0 ? args[0] : "Assets/icon.ico";
File.WriteAllBytes(path, icoHeader.Concat(pngData).ToArray());
Console.WriteLine($"Icon created: {path}");
