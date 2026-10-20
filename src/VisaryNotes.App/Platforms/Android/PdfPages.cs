using Android.Graphics;
using Android.Graphics.Pdf;
using Android.OS;

namespace VisaryNotes.App.Platforms.Android;

/// <summary>
/// Draws PDF pages for Original view with the Android PDF renderer, which shows them exactly as made.
/// Pages are rendered one after another off the UI thread and cached as JPEG files, so a long PDF shows
/// its first pages at once and reopening it is instant (docs/SPEC.md 7.4).
/// </summary>
public static class PdfPages
{
    /// <summary>Longest edge of a rendered page; enough to stay sharp when pinch zoomed about two times.</summary>
    private const int MaxEdgePx = 3000;

    public static Task RenderAsync(string pdfPath, string cacheKey, int targetWidthPx, Func<int, string, Task> deliver, CancellationToken cancel) =>
        Task.Run(async () =>
        {
            var dir = System.IO.Path.Combine(FileSystem.CacheDirectory, "pdf", cacheKey);
            Directory.CreateDirectory(dir);
            using var fd = ParcelFileDescriptor.Open(new Java.IO.File(pdfPath), ParcelFileMode.ReadOnly)!;
            using var renderer = new PdfRenderer(fd);
            var width = Math.Clamp(targetWidthPx, 600, 2000);
            for (var i = 0; i < renderer.PageCount; i++)
            {
                cancel.ThrowIfCancellationRequested();
                var file = System.IO.Path.Combine(dir, $"{i + 1}-{width}.jpg");
                if (!File.Exists(file))
                {
                    using var page = renderer.OpenPage(i)!;
                    var scale = (double)width / Math.Max(1, page.Width);
                    var w = width;
                    var h = (int)Math.Round(page.Height * scale);
                    if (h > MaxEdgePx)
                    {
                        w = (int)Math.Round(w * (double)MaxEdgePx / h);
                        h = MaxEdgePx;
                    }
                    using var bitmap = Bitmap.CreateBitmap(Math.Max(1, w), Math.Max(1, h), Bitmap.Config.Argb8888!)!;
                    // PDF pages are transparent where nothing is drawn; paper is white.
                    bitmap.EraseColor(global::Android.Graphics.Color.White);
                    page.Render(bitmap, null, null, PdfRenderMode.ForDisplay);
                    var temp = file + ".tmp";
                    await using (var stream = File.Create(temp))
                        await bitmap.CompressAsync(Bitmap.CompressFormat.Jpeg!, 88, stream);
                    bitmap.Recycle();
                    File.Move(temp, file, overwrite: true);
                }
                await deliver(i + 1, new Uri(file).AbsoluteUri);
            }
        }, cancel);
}
