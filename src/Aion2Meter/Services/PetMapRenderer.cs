using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Aion2Meter.Services;

internal static class PetMapRenderer
{
    private const int PreviewWidth = 520;
    private const int PreviewHeight = 300;
    private static readonly Dictionary<string, BitmapSource?> Tiles = new(StringComparer.OrdinalIgnoreCase);

    public static BitmapSource? Render(
        PetMapLocation location,
        string tilesRoot,
        MeterNavigationPosition? player = null)
    {
        if (location.Definition is not { } map || location.Points.Count == 0)
        {
            return null;
        }

        double whole = map.Tile * map.Grid;
        var points = location.Points.Select(point =>
        {
            double x = (map.OffsetX + map.Scale * point.X) / whole;
            double y = (map.OffsetY + map.Scale * point.Y) / whole;
            return (X: x, Y: y);
        }).Where(point => double.IsFinite(point.X) && double.IsFinite(point.Y)).ToArray();
        if (points.Length == 0)
        {
            return null;
        }

        (double X, double Y)? playerPoint = player is null
            ? null
            : ((map.OffsetX + map.Scale * player.X) / whole, (map.OffsetY + map.Scale * player.Y) / whole);
        (double X, double Y)? destination = playerPoint is not { } current
            ? null
            : points.MinBy(point =>
                (point.X - current.X) * (point.X - current.X) +
                (point.Y - current.Y) * (point.Y - current.Y));
        (double left, double top, double width, double height) = playerPoint is { } me && destination is { } nearest
            ? Crop([me, nearest])
            : Crop(points);
        var drawing = new DrawingVisual();
        using (DrawingContext context = drawing.RenderOpen())
        {
            var background = new LinearGradientBrush(
                Color.FromRgb(12, 18, 23),
                Color.FromRgb(24, 32, 37),
                new Point(0, 0),
                new Point(1, 1));
            context.DrawRectangle(background, null, new Rect(0, 0, PreviewWidth, PreviewHeight));

            int c0 = Math.Max(0, (int)Math.Floor(left * map.Grid));
            int c1 = Math.Min(map.Grid - 1, (int)Math.Floor((left + width) * map.Grid));
            int r0 = Math.Max(0, (int)Math.Floor(top * map.Grid));
            int r1 = Math.Min(map.Grid - 1, (int)Math.Floor((top + height) * map.Grid));
            for (int col = c0; col <= c1; col++)
            {
                for (int row = r0; row <= r1; row++)
                {
                    if (LoadTile(tilesRoot, map, col, row) is not { } tile)
                    {
                        continue;
                    }

                    double tileLeft = ((double)col / map.Grid - left) / width * PreviewWidth;
                    double tileTop = ((double)row / map.Grid - top) / height * PreviewHeight;
                    double tileWidth = (1d / map.Grid) / width * PreviewWidth;
                    double tileHeight = (1d / map.Grid) / height * PreviewHeight;
                    context.DrawImage(tile, new Rect(tileLeft, tileTop, tileWidth, tileHeight));
                }
            }

            DrawVignette(context);
            if (playerPoint is { } routeStart && destination is { } routeEnd)
            {
                DrawRoute(context, routeStart, routeEnd, left, top, width, height);
            }
            foreach ((double x, double y) in points)
            {
                double px = (x - left) / width * PreviewWidth;
                double py = (y - top) / height * PreviewHeight;
                if (px < -12 || py < -20 || px > PreviewWidth + 12 || py > PreviewHeight + 12)
                {
                    continue;
                }
                DrawPin(context, px, py);
            }

            if (playerPoint is { } playerOnMap)
            {
                double px = (playerOnMap.X - left) / width * PreviewWidth;
                double py = (playerOnMap.Y - top) / height * PreviewHeight;
                if (px is >= -14 and <= PreviewWidth + 14 && py is >= -14 and <= PreviewHeight + 14)
                {
                    DrawPlayer(context, px, py);
                }
            }

            DrawOverview(context, map, left, top, width, height, points, playerPoint);
        }

        var target = new RenderTargetBitmap(PreviewWidth, PreviewHeight, 96, 96, PixelFormats.Pbgra32);
        target.Render(drawing);
        target.Freeze();
        return target;
    }

    private static (double Left, double Top, double Width, double Height) Crop((double X, double Y)[] points)
    {
        double x0 = points.Min(point => point.X);
        double x1 = points.Max(point => point.X);
        double y0 = points.Min(point => point.Y);
        double y1 = points.Max(point => point.Y);
        double width = Math.Max(0.10, (x1 - x0) * 1.35 + 0.035);
        double height = Math.Max(0.10, (y1 - y0) * 1.35 + 0.035);
        double aspect = (double)PreviewWidth / PreviewHeight;
        if (width / height < aspect)
        {
            width = height * aspect;
        }
        else
        {
            height = width / aspect;
        }

        width = Math.Min(1d, width);
        height = Math.Min(1d, height);
        double centerX = (x0 + x1) / 2d;
        double centerY = (y0 + y1) / 2d;
        double left = Math.Clamp(centerX - width / 2d, 0d, 1d - width);
        double top = Math.Clamp(centerY - height / 2d, 0d, 1d - height);
        return (left, top, width, height);
    }

    private static BitmapSource? LoadTile(string tilesRoot, PetMapDefinition map, int col, int row)
    {
        string cacheKey = $"{map.Key}/{col}_{row}";
        if (Tiles.TryGetValue(cacheKey, out BitmapSource? cached))
        {
            return cached;
        }

        try
        {
            string folder = Path.Combine(tilesRoot, map.Key);
            string colorPath = Path.Combine(folder, $"{col}_{row}.jpg");
            string alphaPath = Path.Combine(folder, $"{col}_{row}_a.png");
            if (!File.Exists(colorPath) || !File.Exists(alphaPath))
            {
                return Tiles[cacheKey] = null;
            }

            BitmapSource color = Decode(colorPath, Math.Min(map.Tile, 640));
            BitmapSource alpha = Decode(alphaPath, Math.Min(map.Tile, 640));
            return Tiles[cacheKey] = Combine(color, alpha);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException or UriFormatException)
        {
            return Tiles[cacheKey] = null;
        }
    }

    private static BitmapImage Decode(string path, int width)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.DecodePixelWidth = width;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static BitmapSource Combine(BitmapSource color, BitmapSource alphaMask)
    {
        var convertedColor = new FormatConvertedBitmap(color, PixelFormats.Bgra32, null, 0);
        var convertedMask = new FormatConvertedBitmap(alphaMask, PixelFormats.Gray8, null, 0);
        int width = convertedColor.PixelWidth;
        int height = convertedColor.PixelHeight;
        var pixels = new byte[width * height * 4];
        convertedColor.CopyPixels(pixels, width * 4, 0);
        var alpha = new byte[convertedMask.PixelWidth * convertedMask.PixelHeight];
        convertedMask.CopyPixels(alpha, convertedMask.PixelWidth, 0);
        for (int y = 0; y < height; y++)
        {
            int maskY = Math.Min(convertedMask.PixelHeight - 1, y * convertedMask.PixelHeight / height);
            for (int x = 0; x < width; x++)
            {
                int maskX = Math.Min(convertedMask.PixelWidth - 1, x * convertedMask.PixelWidth / width);
                int index = (y * width + x) * 4;
                byte value = alpha[maskY * convertedMask.PixelWidth + maskX];
                pixels[index] = (byte)(pixels[index] * value / 255);
                pixels[index + 1] = (byte)(pixels[index + 1] * value / 255);
                pixels[index + 2] = (byte)(pixels[index + 2] * value / 255);
                pixels[index + 3] = value;
            }
        }

        BitmapSource result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, pixels, width * 4);
        result.Freeze();
        return result;
    }

    private static void DrawPin(DrawingContext context, double x, double y)
    {
        const double radius = 7.5;
        const double height = 18;
        var geometry = new StreamGeometry();
        using (StreamGeometryContext path = geometry.Open())
        {
            path.BeginFigure(new Point(x, y), true, true);
            path.BezierTo(new Point(x - radius * 0.4, y - height * 0.45), new Point(x - radius, y - height * 0.62), new Point(x - radius, y - height), true, false);
            path.ArcTo(new Point(x + radius, y - height), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
            path.BezierTo(new Point(x + radius, y - height * 0.62), new Point(x + radius * 0.4, y - height * 0.45), new Point(x, y), true, false);
        }
        geometry.Freeze();
        context.DrawGeometry(new SolidColorBrush(Color.FromRgb(214, 168, 63)), new Pen(Brushes.White, 1.4), geometry);
        context.DrawEllipse(Brushes.White, null, new Point(x, y - height), radius * 0.34, radius * 0.34);
    }

    private static void DrawRoute(
        DrawingContext context,
        (double X, double Y) start,
        (double X, double Y) end,
        double left,
        double top,
        double width,
        double height)
    {
        Point From((double X, double Y) point) => new(
            (point.X - left) / width * PreviewWidth,
            (point.Y - top) / height * PreviewHeight);
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(190, 92, 224, 232)), 2)
        {
            DashStyle = new DashStyle([3d, 3d], 0)
        };
        context.DrawLine(pen, From(start), From(end));
    }

    private static void DrawPlayer(DrawingContext context, double x, double y)
    {
        var glow = new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(Color.FromArgb(180, 105, 237, 244), 0),
                new GradientStop(Color.FromArgb(0, 105, 237, 244), 1)
            }
        };
        context.DrawEllipse(glow, null, new Point(x, y), 15, 15);
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(96, 225, 235)), new Pen(Brushes.White, 1.5), new Point(x, y), 5.5, 5.5);
        context.DrawEllipse(Brushes.White, null, new Point(x, y), 1.8, 1.8);
    }

    private static void DrawVignette(DrawingContext context)
    {
        var vignette = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.45),
            GradientOrigin = new Point(0.5, 0.45),
            RadiusX = 0.72,
            RadiusY = 0.82
        };
        vignette.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.55));
        vignette.GradientStops.Add(new GradientStop(Color.FromArgb(128, 3, 8, 11), 1));
        context.DrawRectangle(vignette, null, new Rect(0, 0, PreviewWidth, PreviewHeight));
    }

    private static void DrawOverview(
        DrawingContext context,
        PetMapDefinition map,
        double left,
        double top,
        double width,
        double height,
        IReadOnlyList<(double X, double Y)> points,
        (double X, double Y)? player)
    {
        const double size = 78;
        const double margin = 8;
        var area = new Rect(PreviewWidth - size - margin, PreviewHeight - size - margin, size, size);
        context.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(220, 12, 18, 23)), new Pen(new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), 1), area, 2, 2);
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(34, 255, 255, 255)), 0.6);
        for (int cell = 1; cell < map.Grid; cell++)
        {
            double offset = size * cell / map.Grid;
            context.DrawLine(gridPen, new Point(area.X + offset, area.Y), new Point(area.X + offset, area.Bottom));
            context.DrawLine(gridPen, new Point(area.X, area.Y + offset), new Point(area.Right, area.Y + offset));
        }

        var pointBrush = new SolidColorBrush(Color.FromArgb(210, 214, 168, 63));
        foreach ((double x, double y) in points)
        {
            context.DrawEllipse(pointBrush, null, new Point(area.X + x * size, area.Y + y * size), 1.5, 1.5);
        }

        if (player is { } me)
        {
            context.DrawEllipse(
                new SolidColorBrush(Color.FromRgb(96, 225, 235)),
                new Pen(Brushes.White, 0.8),
                new Point(area.X + me.X * size, area.Y + me.Y * size),
                2.4,
                2.4);
        }

        context.DrawRectangle(
            null,
            new Pen(new SolidColorBrush(Color.FromRgb(234, 190, 78)), 1.6),
            new Rect(area.X + left * size, area.Y + top * size, Math.Max(3, width * size), Math.Max(3, height * size)));
    }
}
