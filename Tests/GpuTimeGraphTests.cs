using ShaderEditor.Windows;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Size = System.Windows.Size;

namespace Tests;

[TestClass]
public sealed class GpuTimeGraphTests
{
    [TestMethod]
    public void Measure_WithFiniteHeight_UsesAtLeastMinimumHeight()
    {
        var desiredSize = RunOnStaThread(() =>
        {
            var graph = new GpuTimeGraph();

            graph.Measure(new Size(120, 20));

            return graph.DesiredSize;
        });

        Assert.AreEqual(new Size(120, 40), desiredSize);
    }

    [TestMethod]
    public void Measure_WithInfiniteHeight_UsesDefaultHeight()
    {
        var desiredSize = RunOnStaThread(() =>
        {
            var graph = new GpuTimeGraph();

            graph.Measure(new Size(120, double.PositiveInfinity));

            return graph.DesiredSize;
        });

        Assert.AreEqual(new Size(120, 60), desiredSize);
    }

    [TestMethod]
    public void Render_WithoutSamples_PaintsBackground()
    {
        var bitmap = RenderGraph(null);

        var pixel = GetPixel(bitmap, 0, 0);

        Assert.AreEqual(37, pixel.R);
        Assert.AreEqual(37, pixel.G);
        Assert.AreEqual(38, pixel.B);
        Assert.AreEqual(255, pixel.A);
    }

    [TestMethod]
    public void Render_WithOneSample_DoesNotThrow()
    {
        var bitmap = RenderGraph(new ObservableCollection<double> { 12.5 });

        Assert.AreEqual(120, bitmap.PixelWidth);
        Assert.AreEqual(80, bitmap.PixelHeight);
    }

    [TestMethod]
    public void Render_WithMultipleSamples_DrawsGraphContent()
    {
        var bitmap = RenderGraph(new ObservableCollection<double> { 1, 2, 4 });

        Assert.IsGreaterThan(0, CountNonBackgroundPixels(bitmap));
    }

    [TestMethod]
    public void Render_AfterCollectionChange_UsesUpdatedSamples()
    {
        var samples = new ObservableCollection<double> { 1, 2 };
        var firstBitmap = RenderGraph(samples);

        samples.Add(8);
        var secondBitmap = RenderGraph(samples);

        Assert.IsGreaterThan(0, CountDifferentPixels(firstBitmap, secondBitmap));
    }

    private static RenderTargetBitmap RenderGraph(
        ObservableCollection<double>? samples)
    {
        return RunOnStaThread(() =>
        {
            var graph = new GpuTimeGraph
            {
                Samples = samples
            };

            var size = new Size(120, 80);
            graph.Measure(size);
            graph.Arrange(new Rect(size));

            var bitmap = new RenderTargetBitmap(
                120,
                80,
                96,
                96,
                PixelFormats.Pbgra32);

            bitmap.Render(graph);
            return bitmap;
        });
    }

    private static int CountNonBackgroundPixels(BitmapSource bitmap)
    {
        var pixels = ReadPixels(bitmap);
        var count = 0;

        for (var index = 0; index < pixels.Length; index += 4)
        {
            var isBackground =
                pixels[index] == 38 &&
                pixels[index + 1] == 37 &&
                pixels[index + 2] == 37 &&
                pixels[index + 3] == 255;

            if (!isBackground)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountDifferentPixels(
        BitmapSource first,
        BitmapSource second)
    {
        var firstPixels = ReadPixels(first);
        var secondPixels = ReadPixels(second);
        var count = 0;

        for (var index = 0; index < firstPixels.Length; index++)
        {
            if (firstPixels[index] != secondPixels[index])
            {
                count++;
            }
        }

        return count;
    }

    private static PixelColor GetPixel(BitmapSource bitmap, int x, int y)
    {
        var pixels = ReadPixels(bitmap);
        var index = (y * bitmap.PixelWidth + x) * 4;

        return new PixelColor(
            pixels[index + 2],
            pixels[index + 1],
            pixels[index],
            pixels[index + 3]);
    }

    private static byte[] ReadPixels(BitmapSource bitmap)
    {
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];

        bitmap.CopyPixels(
            pixels,
            bitmap.PixelWidth * 4,
            0);

        return pixels;
    }

    private static TResult RunOnStaThread<TResult>(Func<TResult> action)
    {
        TResult result = default!;
        Exception? exception = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception caughtException)
            {
                exception = caughtException;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        return result;
    }

    private readonly record struct PixelColor(
        byte R,
        byte G,
        byte B,
        byte A);
}