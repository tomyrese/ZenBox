using System.Windows;
using System.Windows.Media;
using ZenBox.Models;

namespace ZenBox.Services;

public static class BrandLogoFactory
{
    private static readonly Dictionary<AppSection, ImageSource> LogoStore = new();

    public static ImageSource GetLogo(AppSection section)
    {
        if (LogoStore.TryGetValue(section, out var existing))
        {
            return existing;
        }

        var created = section switch
        {
            AppSection.YouTube => CreateYouTubeLogo(),
            AppSection.Netflix => CreateNetflixLogo(),
            AppSection.Spotify => CreateSpotifyLogo(),
            AppSection.TikTok => CreateTikTokLogo(),
            AppSection.Facebook => CreateFacebookLogo(),
            AppSection.FptPlay => CreateFptPlayLogo(),
            AppSection.VtvGo => CreateVtvLogo(),
            AppSection.ZingMp3 => CreateZingMp3Logo(),
            AppSection.AppleMusic => CreateAppleMusicLogo(),
            AppSection.PrimeVideo => CreatePrimeVideoLogo(),
            AppSection.Instagram => CreateInstagramLogo(),
            AppSection.SoundCloud => CreateSoundCloudLogo(),
            AppSection.Twitch => CreateTwitchLogo(),
            AppSection.WebBrowser => CreateGoogleLogo(),
            AppSection.OfflineMovies => CreateMoviesLogo(),
            AppSection.OfflineMusic => CreateMusicLogo(),
            AppSection.SystemConfig => CreateConfigLogo(),
            _ => CreateYouTubeLogo()
        };

        LogoStore[section] = created;
        return created;
    }

    private static DrawingGroup CreateCanvas()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(
            Brushes.Transparent,
            null,
            new RectangleGeometry(new Rect(0, 0, 96, 96))));
        return group;
    }

    private static DrawingImage Finalize(DrawingGroup group)
    {
        var img = new DrawingImage(group);
        img.Freeze();
        return img;
    }

    private static SolidColorBrush Solid(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    private static LinearGradientBrush Grad(string startHex, string endHex, double x1 = 0, double y1 = 0, double x2 = 1, double y2 = 1)
    {
        var brush = new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString(startHex),
            (Color)ColorConverter.ConvertFromString(endHex),
            new Point(x1, y1),
            new Point(x2, y2));
        brush.Freeze();
        return brush;
    }

    private static ImageSource CreateYouTubeLogo()
    {
        var g = CreateCanvas();
        g.Children.Add(new GeometryDrawing(
            Grad("#FF0033", "#D90429"),
            null,
            Geometry.Parse("M 86.3,28.8 C 85.4,25.3 82.7,22.6 79.2,21.7 C 73.0,20.0 48.0,20.0 48.0,20.0 C 48.0,20.0 23.0,20.0 16.8,21.7 C 13.3,22.6 10.6,25.3 9.7,28.8 C 8.0,35.0 8.0,48.0 8.0,48.0 C 8.0,48.0 8.0,61.0 9.7,67.2 C 10.6,70.7 13.3,73.4 16.8,74.3 C 23.0,76.0 48.0,76.0 48.0,76.0 C 48.0,76.0 73.0,76.0 79.2,74.3 C 82.7,73.4 85.4,70.7 86.3,67.2 C 88.0,61.0 88.0,48.0 88.0,48.0 C 88.0,48.0 88.0,35.0 86.3,28.8 Z")));
        g.Children.Add(new GeometryDrawing(
            Solid("#FFFFFF"),
            null,
            Geometry.Parse("M 40.0,35.5 L 61.5,48.0 L 40.0,60.5 Z")));
        return Finalize(g);
    }

    private static ImageSource CreateNetflixLogo()
    {
        var g = CreateCanvas();
        g.Children.Add(new GeometryDrawing(
            Solid("#B1060F"),
            null,
            Geometry.Parse("M 22,10 L 37,10 L 37,86 L 22,84 Z M 59,10 L 74,10 L 74,84 L 59,86 Z")));
        g.Children.Add(new GeometryDrawing(
            Grad("#FF1E27", "#E50914", 0, 0, 0.8, 1),
            null,
            Geometry.Parse("M 22,10 L 37.5,10 L 74,84 L 58.5,86 Z")));
        return Finalize(g);
    }

    private static ImageSource CreateSpotifyLogo()
    {
        var g = CreateCanvas();
        g.Children.Add(new GeometryDrawing(
            Grad("#1ED760", "#17B850"),
            null,
            new EllipseGeometry(new Point(48, 48), 40, 40)));

        var pen1 = new Pen(Solid("#070B12"), 6.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var pen2 = new Pen(Solid("#070B12"), 5.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var pen3 = new Pen(Solid("#070B12"), 4.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen1.Freeze();
        pen2.Freeze();
        pen3.Freeze();

        g.Children.Add(new GeometryDrawing(null, pen1, Geometry.Parse("M 25,36 C 40,31 58,33 71,41")));
        g.Children.Add(new GeometryDrawing(null, pen2, Geometry.Parse("M 28,49.5 C 41,45.5 55,47 66.5,54")));
        g.Children.Add(new GeometryDrawing(null, pen3, Geometry.Parse("M 31,62 C 42,59 53,60.5 62,66")));
        return Finalize(g);
    }

    private static ImageSource CreateTikTokLogo()
    {
        var g = CreateCanvas();
        const string notePath = "M 58,14 L 58,58 C 58,66.8 50.8,74 42,74 C 33.2,74 26,66.8 26,58 C 26,49.2 33.2,42 42,42 C 43.8,42 45.5,42.3 47,42.9 L 47,30.5 C 45.4,30.2 43.7,30 42,30 C 26.5,30 14,42.5 14,58 C 14,73.5 26.5,86 42,86 C 57.5,86 70,73.5 70,58 L 70,35.5 C 75.2,39.4 81.6,41.8 88.5,41.8 L 88.5,29.8 C 78.2,29.8 69.8,22.8 68.5,14 Z";
        var baseGeometry = Geometry.Parse(notePath);

        var cyanGroup = new DrawingGroup { Transform = new TranslateTransform(-5.5, -3.5) };
        cyanGroup.Children.Add(new GeometryDrawing(Solid("#25F4EE"), null, baseGeometry));
        g.Children.Add(cyanGroup);

        var redGroup = new DrawingGroup { Transform = new TranslateTransform(1.5, 3.0) };
        redGroup.Children.Add(new GeometryDrawing(Solid("#FE2C55"), null, baseGeometry));
        g.Children.Add(redGroup);

        var whiteGroup = new DrawingGroup { Transform = new TranslateTransform(-2.0, 0) };
        whiteGroup.Children.Add(new GeometryDrawing(Solid("#FFFFFF"), null, baseGeometry));
        g.Children.Add(whiteGroup);

        return Finalize(g);
    }

    private static ImageSource CreateFacebookLogo()
    {
        var g = CreateCanvas();
        g.Children.Add(new GeometryDrawing(
            Grad("#1877F2", "#0866FF"),
            null,
            new EllipseGeometry(new Point(48, 48), 40, 40)));
        g.Children.Add(new GeometryDrawing(
            Solid("#FFFFFF"),
            null,
            Geometry.Parse("M 54.5,61 L 63.5,61 L 65.2,49.5 L 54.5,49.5 L 54.5,42 C 54.5,37.8 56.5,35.2 61.8,35.2 L 66,35.2 L 66,25.2 C 63.5,24.8 59.8,24.4 55.8,24.4 C 46.2,24.4 40.2,30.2 40.2,40.8 L 40.2,49.5 L 30.5,49.5 L 30.5,61 L 40.2,61 L 40.2,87.2 C 42.8,87.7 45.4,88 48,88 C 50.2,88 52.4,87.8 54.5,87.4 Z")));
        return Finalize(g);
    }

    private static ImageSource CreateFptPlayLogo()
    {
        var g = CreateCanvas();
        // Biểu tượng ngọn lửa tam sắc Play đặc trưng của FPT Play (Cam - Xanh lá - Xanh dương)
        g.Children.Add(new GeometryDrawing(
            Grad("#FF7A00", "#FF4D00"),
            null,
            Geometry.Parse("M 22,16 C 22,12 27,10 31,12 L 80,42 C 84,45 84,51 80,54 L 54,70 L 22,42 Z")));
        g.Children.Add(new GeometryDrawing(
            Grad("#00C853", "#009624"),
            null,
            Geometry.Parse("M 22,42 L 54,70 L 31,84 C 27,86 22,84 22,80 Z")));
        g.Children.Add(new GeometryDrawing(
            Grad("#2979FF", "#0D47A1"),
            null,
            Geometry.Parse("M 22,28 L 48,48 L 22,68 Z")));
        return Finalize(g);
    }

    private static ImageSource CreateVtvLogo()
    {
        var g = CreateCanvas();
        g.Children.Add(new GeometryDrawing(
            Grad("#FF3B30", "#DC2626"),
            null,
            Geometry.Parse("M 7,28 L 18,28 L 24,55 L 31,28 L 41,28 L 28,68 L 19,68 Z")));
        g.Children.Add(new GeometryDrawing(
            Grad("#10B981", "#059669"),
            null,
            Geometry.Parse("M 37,28 L 62,28 L 60,37 L 53,37 L 47,68 L 37,68 L 43,37 L 35,37 Z")));
        g.Children.Add(new GeometryDrawing(
            Grad("#38BDF8", "#2563EB"),
            null,
            Geometry.Parse("M 58,28 L 69,28 L 74,55 L 81,28 L 91,28 L 78,68 L 69,68 Z")));
        return Finalize(g);
    }

    private static ImageSource CreateZingMp3Logo()
    {
        var g = CreateCanvas();
        var ringPen = new Pen(Grad("#A855F7", "#06B6D4"), 8.5);
        ringPen.Freeze();
        g.Children.Add(new GeometryDrawing(null, ringPen, new EllipseGeometry(new Point(48, 48), 35, 35)));
        g.Children.Add(new GeometryDrawing(
            Grad("#EC4899", "#8B5CF6"),
            null,
            Geometry.Parse("M 34,33 L 64,33 L 64,41 L 44,57 L 65,57 L 65,65 L 32,65 L 32,57 L 52,41 L 34,41 Z")));
        return Finalize(g);
    }

    private static ImageSource CreateAppleMusicLogo()
    {
        var g = CreateCanvas();
        var coralGrad = Grad("#FA233B", "#FB5C74", 0, 1, 1, 0);
        g.Children.Add(new GeometryDrawing(
            coralGrad,
            null,
            Geometry.Parse("M 36,68 C 36,75.2 30.2,81 23,81 C 15.8,81 10,75.2 10,68 C 10,60.8 15.8,55 23,55 C 25.8,55 28.4,55.9 30.5,57.4 L 30.5,24 L 78,14 L 78,60 C 78,67.2 72.2,73 65,73 C 57.8,73 52,67.2 52,60 C 52,52.8 57.8,47 65,47 C 67.8,47 70.4,47.9 72.5,49.4 L 72.5,27.5 L 36,35.2 Z")));
        return Finalize(g);
    }

    private static ImageSource CreatePrimeVideoLogo()
    {
        var g = CreateCanvas();
        var cyan = Grad("#00A8E1", "#38BDF8");
        var smilePen = new Pen(cyan, 6.2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        smilePen.Freeze();

        // Biểu tượng nút Play kết hợp nụ cười mũi tên xanh ngọc đặc trưng của Prime Video
        g.Children.Add(new GeometryDrawing(
            Solid("#FFFFFF"),
            null,
            Geometry.Parse("M 36,22 L 66,40 L 36,58 Z")));
        g.Children.Add(new GeometryDrawing(null, smilePen, Geometry.Parse("M 18,64 C 36,78 60,78 78,63")));
        g.Children.Add(new GeometryDrawing(cyan, null, Geometry.Parse("M 70,58 L 84,60 L 79,73 Z")));
        return Finalize(g);
    }

    private static ImageSource CreateInstagramLogo()
    {
        var g = CreateCanvas();
        var instaGrad = new LinearGradientBrush
        {
            StartPoint = new Point(0.1, 0.9),
            EndPoint = new Point(0.9, 0.1)
        };
        instaGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FEDA75"), 0.0));
        instaGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FA7E1E"), 0.25));
        instaGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#D62976"), 0.52));
        instaGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#962FBF"), 0.78));
        instaGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#4F5BD5"), 1.0));
        instaGrad.Freeze();

        var strokePen = new Pen(instaGrad, 6.8);
        strokePen.Freeze();

        g.Children.Add(new GeometryDrawing(null, strokePen, new RectangleGeometry(new Rect(14, 14, 68, 68), 20, 20)));
        g.Children.Add(new GeometryDrawing(null, strokePen, new EllipseGeometry(new Point(48, 48), 16.5, 16.5)));
        g.Children.Add(new GeometryDrawing(instaGrad, null, new EllipseGeometry(new Point(66.5, 29.5), 4.8, 4.8)));
        return Finalize(g);
    }

    private static ImageSource CreateSoundCloudLogo()
    {
        var g = CreateCanvas();
        var orange = Grad("#FF7700", "#FF3300", 0, 0, 0, 1);

        var barPen = new Pen(orange, 4.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        barPen.Freeze();
        g.Children.Add(new GeometryDrawing(null, barPen, Geometry.Parse("M 11,52 L 11,64 M 19,42 L 19,65 M 27,36 L 27,65 M 35,30 L 35,65")));

        g.Children.Add(new GeometryDrawing(
            orange,
            null,
            Geometry.Parse("M 43,66 L 75,66 C 82.7,66 89,59.7 89,52 C 89,44.6 83.3,38.6 76.0,38.1 C 73.6,28.8 65.2,22 55.2,22 C 49.8,22 45.2,24.1 43,27.2 Z")));
        return Finalize(g);
    }

    private static ImageSource CreateTwitchLogo()
    {
        var g = CreateCanvas();
        g.Children.Add(new GeometryDrawing(
            Solid("#9146FF"),
            null,
            Geometry.Parse("M 21,12 L 82,12 L 82,55 L 64,73 L 49,73 L 38,84 L 38,73 L 15,73 L 15,24 Z")));
        g.Children.Add(new GeometryDrawing(
            Solid("#FFFFFF"),
            null,
            Geometry.Parse("M 27,20 L 74,20 L 74,51 L 62,63 L 47,63 L 37,73 L 37,63 L 27,63 Z")));
        g.Children.Add(new GeometryDrawing(
            Solid("#9146FF"),
            null,
            Geometry.Parse("M 43,31 L 50,31 L 50,49 L 43,49 Z M 58,31 L 65,31 L 65,49 L 58,49 Z")));
        return Finalize(g);
    }

    private static ImageSource CreateGoogleLogo()
    {
        var g = CreateCanvas();
        var bluePen = new Pen(Solid("#4285F4"), 14);
        var greenPen = new Pen(Solid("#34A853"), 14);
        var yellowPen = new Pen(Solid("#FBBC05"), 14);
        var redPen = new Pen(Solid("#EA4335"), 14);
        bluePen.Freeze();
        greenPen.Freeze();
        yellowPen.Freeze();
        redPen.Freeze();

        g.Children.Add(new GeometryDrawing(null, redPen, Geometry.Parse("M 23.5,33.5 A 28.5,28.5 0 0 1 68.2,27.8")));
        g.Children.Add(new GeometryDrawing(null, yellowPen, Geometry.Parse("M 23.5,62.5 A 28.5,28.5 0 0 1 23.5,33.5")));
        g.Children.Add(new GeometryDrawing(null, greenPen, Geometry.Parse("M 69.5,66.8 A 28.5,28.5 0 0 1 23.5,62.5")));
        g.Children.Add(new GeometryDrawing(null, bluePen, Geometry.Parse("M 76.2,48.0 A 28.5,28.5 0 0 1 69.5,66.8")));
        g.Children.Add(new GeometryDrawing(Solid("#4285F4"), null, new RectangleGeometry(new Rect(48, 41.5, 35, 13), 2, 2)));
        return Finalize(g);
    }

    private static ImageSource CreateMoviesLogo()
    {
        var g = CreateCanvas();
        var goldGrad = Grad("#FBBF24", "#F97316");
        g.Children.Add(new GeometryDrawing(
            goldGrad,
            null,
            Geometry.Parse("M 70,18 L 78,34 L 66,34 L 58,18 L 50,18 L 58,34 L 46,34 L 38,18 L 30,18 L 38,34 L 26,34 L 18,18 L 16,18 C 11.6,18 8,21.6 8,26 L 8,74 C 8,78.4 11.6,82 16,82 L 80,82 C 84.4,82 88,78.4 88,74 L 88,18 Z")));
        g.Children.Add(new GeometryDrawing(
            Solid("#FFFFFF"),
            null,
            Geometry.Parse("M 40,44 L 62,57.5 L 40,71 Z")));
        return Finalize(g);
    }

    private static ImageSource CreateMusicLogo()
    {
        var g = CreateCanvas();
        var musicGrad = Grad("#C084FC", "#EC4899");
        var arcPen = new Pen(musicGrad, 7.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        arcPen.Freeze();

        g.Children.Add(new GeometryDrawing(null, arcPen, Geometry.Parse("M 18,54 L 18,46 A 30,30 0 0 1 78,46 L 78,54")));
        g.Children.Add(new GeometryDrawing(musicGrad, null, new RectangleGeometry(new Rect(12, 48, 14, 28), 7, 7)));
        g.Children.Add(new GeometryDrawing(musicGrad, null, new RectangleGeometry(new Rect(70, 48, 14, 28), 7, 7)));

        var wavePen = new Pen(Solid("#38BDF8"), 5.0) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        wavePen.Freeze();
        g.Children.Add(new GeometryDrawing(null, wavePen, Geometry.Parse("M 37,56 L 37,68 M 48,46 L 48,76 M 59,53 L 59,70")));
        return Finalize(g);
    }

    private static ImageSource CreateConfigLogo()
    {
        var g = CreateCanvas();
        var silverGrad = Grad("#F1F5F9", "#94A3B8");
        g.Children.Add(new GeometryDrawing(
            silverGrad,
            null,
            Geometry.Parse("M 77.8,51.9 C 78.0,50.6 78.1,49.3 78.1,48 C 78.1,46.7 78.0,45.4 77.8,44.1 L 86.2,37.5 C 87.0,36.9 87.2,35.8 86.7,34.9 L 78.7,21.1 C 78.2,20.2 77.1,19.8 76.2,20.2 L 66.3,24.2 C 64.2,22.6 62.0,21.3 59.5,20.3 L 58.0,9.7 C 57.8,8.7 57.0,8 56.0,8 L 40.0,8 C 39.0,8 38.2,8.7 38.0,9.7 L 36.5,20.3 C 34.0,21.3 31.8,22.6 29.7,24.2 L 19.8,20.2 C 18.9,19.8 17.8,20.2 17.3,21.1 L 9.3,34.9 C 8.8,35.8 9.0,36.9 9.8,37.5 L 18.2,44.1 C 18.0,45.4 17.9,46.7 17.9,48 C 17.9,49.3 18.0,50.6 18.2,51.9 L 9.8,58.5 C 9.0,59.1 8.8,60.2 9.3,61.1 L 17.3,74.9 C 17.8,75.8 18.9,76.2 19.8,75.8 L 29.7,71.8 C 31.8,73.4 34.0,74.7 36.5,75.7 L 38.0,86.3 C 38.2,87.3 39.0,88 40.0,88 L 56.0,88 C 57.0,88 57.8,87.3 58.0,86.3 L 59.5,75.7 C 62.0,74.7 64.2,73.4 66.3,71.8 L 76.2,75.8 C 77.1,76.2 78.2,75.8 78.7,74.9 L 86.7,61.1 C 87.2,60.2 87.0,59.1 86.2,58.5 Z")));

        g.Children.Add(new GeometryDrawing(
            Solid("#0B1120"),
            new Pen(Solid("#38BDF8"), 4.5),
            new EllipseGeometry(new Point(48, 48), 14, 14)));
        return Finalize(g);
    }
}
