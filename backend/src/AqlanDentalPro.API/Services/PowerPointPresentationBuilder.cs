using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using SkiaSharp;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace AqlanDentalPro.API.Services;

internal static class PowerPointPresentationBuilder
{
    // 4:3 (screen4x3) and palette matched to the reference clinical case-presentation deck.
    private const long SlideWidth = 9_144_000;
    private const long SlideHeight = 6_858_000;
    private const long Margin = 360_000;
    private const string Navy = "2D2DB9";       // reference dark blue (titles)
    private const string Blue = "3333CC";       // reference primary blue (accents)
    private const string Green = "00CC99";      // reference green accent
    private const string LightBlue = "CCCCFF";  // reference light panel
    private const string TextColor = "1A1A1A";  // near-black body text
    private const string Muted = "808080";      // reference grey
    private const string White = "FFFFFF";
    private const string Border = "B2B2B2";      // reference grey border

    public static byte[] Build(OrthoCasePresentationDocument model)
    {
        using var stream = new MemoryStream();
        using (var document = PresentationDocument.Create(
                   stream,
                   PresentationDocumentType.Presentation,
                   true))
        {
            var presentationPart = document.AddPresentationPart();
            presentationPart.Presentation = new P.Presentation();

            var slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>();
            var slideLayoutPart = slideMasterPart.AddNewPart<SlideLayoutPart>();
            var themePart = slideMasterPart.AddNewPart<ThemePart>();

            themePart.Theme = CreateTheme();
            slideLayoutPart.SlideLayout = CreateSlideLayout();
            slideLayoutPart.AddPart(slideMasterPart);

            // Embed the generated "ocean blue" background on the master so every slide
            // inherits the reference deck's themed background.
            var backgroundPart = slideMasterPart.AddImagePart(ImagePartType.Png);
            using (var bgStream = new MemoryStream(RenderOceanBackground(1280, 960)))
                backgroundPart.FeedData(bgStream);
            var backgroundRelId = slideMasterPart.GetIdOfPart(backgroundPart);
            slideMasterPart.SlideMaster = CreateSlideMaster(
                slideMasterPart.GetIdOfPart(slideLayoutPart),
                backgroundRelId);

            var masterId = presentationPart.GetIdOfPart(slideMasterPart);
            presentationPart.Presentation.Append(
                new P.SlideMasterIdList(
                    new P.SlideMasterId { Id = 2_147_483_648U, RelationshipId = masterId }),
                new P.SlideIdList(),
                new P.SlideSize
                {
                    Cx = (int)SlideWidth,
                    Cy = (int)SlideHeight,
                    Type = SlideSizeValues.Screen4x3,
                },
                new P.NotesSize { Cx = 6_858_000, Cy = 9_144_000 },
                new P.DefaultTextStyle());

            uint slideId = 256;
            foreach (var content in model.Slides)
            {
                var slidePart = presentationPart.AddNewPart<SlidePart>();
                slidePart.AddPart(slideLayoutPart);
                slidePart.Slide = CreateSlide(slidePart, model, content);

                presentationPart.Presentation.SlideIdList!.Append(
                    new P.SlideId
                    {
                        Id = slideId++,
                        RelationshipId = presentationPart.GetIdOfPart(slidePart),
                    });
            }

            presentationPart.Presentation.Save();
        }

        return stream.ToArray();
    }

    private static P.Slide CreateSlide(
        SlidePart slidePart,
        OrthoCasePresentationDocument document,
        PresentationSlideContent content)
    {
        var shapeTree = CreateShapeTree();
        var nextId = 2U;

        AddBackground(shapeTree, ref nextId);
        AddHeader(shapeTree, ref nextId, content.Definition.Title, document.CaseNumber);
        AddFooter(shapeTree, ref nextId, document, content.Definition.Order);

        if (content.Definition.Type == OrthoPresentationSlideType.Title)
        {
            AddTitleSlide(shapeTree, ref nextId, document);
        }
        else if (content.Definition.Type == OrthoPresentationSlideType.ThankYou)
        {
            AddThankYouSlide(shapeTree, ref nextId, document);
        }
        else if (content.Definition.Type == OrthoPresentationSlideType.BeforeAfter && content.Images.Count >= 2)
        {
            AddBeforeAfterContent(slidePart, shapeTree, ref nextId, content);
        }
        else if (content.Images.Count > 0)
        {
            AddImageContent(slidePart, shapeTree, ref nextId, content);
        }
        else if (content.Table is not null)
        {
            AddTableContent(shapeTree, ref nextId, content);
        }
        else if (content.Lines.Count > 0)
        {
            AddBulletContent(shapeTree, ref nextId, content.Lines);
        }
        else
        {
            AddEmptyState(shapeTree, ref nextId);
        }

        return new P.Slide(
            new P.CommonSlideData(shapeTree),
            new P.ColorMapOverride(new A.MasterColorMapping()));
    }

    private static P.ShapeTree CreateShapeTree() =>
        new(
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                new P.NonVisualGroupShapeDrawingProperties(),
                new ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(
                new A.TransformGroup(
                    new A.Offset { X = 0, Y = 0 },
                    new A.Extents { Cx = 0, Cy = 0 },
                    new A.ChildOffset { X = 0, Y = 0 },
                    new A.ChildExtents { Cx = 0, Cy = 0 })));

    private static void AddBackground(P.ShapeTree tree, ref uint id)
    {
        // No background rectangle — the themed "ocean blue" background comes from the
        // slide master so it shows on every slide.
    }

    private static void AddHeader(
        P.ShapeTree tree,
        ref uint id,
        string title,
        string caseNumber)
    {
        // Centered title near the top, matching the reference deck.
        tree.Append(CreateTextBox(
            id++,
            "SlideTitle",
            Margin,
            200_000,
            SlideWidth - (Margin * 2),
            470_000,
            title,
            2400,
            White,
            true,
            A.TextAlignmentTypeValues.Center));

        tree.Append(CreateTextBox(
            id++,
            "CaseNumber",
            Margin,
            720_000,
            SlideWidth - (Margin * 2),
            230_000,
            $"رقم الحالة: {caseNumber}",
            1000,
            "DCE6FF",
            false,
            A.TextAlignmentTypeValues.Center));
    }

    private static void AddFooter(
        P.ShapeTree tree,
        ref uint id,
        OrthoCasePresentationDocument document,
        int slideNumber)
    {
        // Footer carries the clinic identity + contact (Settings) — same as the PDF reports.
        var footer = string.IsNullOrWhiteSpace(document.Contact)
            ? $"{document.ClinicName}   |   {slideNumber}"
            : $"{document.ClinicName}   |   {document.Contact}   |   {slideNumber}";
        tree.Append(CreateTextBox(
            id++,
            "FooterClinic",
            Margin,
            SlideHeight - 330_000,
            SlideWidth - (Margin * 2),
            170_000,
            footer,
            850,
            "DCE6FF",
            false,
            A.TextAlignmentTypeValues.Center));
    }

    private static void AddTitleSlide(
        P.ShapeTree tree,
        ref uint id,
        OrthoCasePresentationDocument document)
    {
        // White text directly on the themed background (reference title layout).
        tree.Append(CreateTextBox(
            id++, "ClinicName", 900_000, 1_350_000, SlideWidth - 1_800_000, 460_000,
            document.ClinicName, 1600, White, true, A.TextAlignmentTypeValues.Center));
        tree.Append(CreateTextBox(
            id++, "Title", 900_000, 2_150_000, SlideWidth - 1_800_000, 760_000,
            "عرض حالة تقويمية", 3400, White, true, A.TextAlignmentTypeValues.Center));
        tree.Append(CreateTextBox(
            id++, "Patient", 900_000, 3_080_000, SlideWidth - 1_800_000, 480_000,
            document.PatientName, 2200, "FFF2A8", true, A.TextAlignmentTypeValues.Center));
        var doctorLine = string.IsNullOrWhiteSpace(document.LeadDoctorTitle)
            ? document.LeadDoctor
            : $"{document.LeadDoctor} — {document.LeadDoctorTitle}";
        tree.Append(CreateTextBox(
            id++, "Doctor", 900_000, 3_820_000, SlideWidth - 1_800_000, 440_000,
            doctorLine, 1550, White, true, A.TextAlignmentTypeValues.Center));
        if (!string.IsNullOrWhiteSpace(document.LeadDoctorCredentials))
            tree.Append(CreateTextBox(
                id++, "Credentials", 900_000, 4_320_000, SlideWidth - 1_800_000, 400_000,
                document.LeadDoctorCredentials, 1150, "DCE6FF", false, A.TextAlignmentTypeValues.Center));
    }

    private static void AddThankYouSlide(
        P.ShapeTree tree,
        ref uint id,
        OrthoCasePresentationDocument document)
    {
        tree.Append(CreateTextBox(
            id++,
            "Thanks",
            Margin,
            2_100_000,
            SlideWidth - (Margin * 2),
            900_000,
            "شكراً لكم",
            4300,
            White,
            true,
            A.TextAlignmentTypeValues.Center));
        tree.Append(CreateTextBox(
            id++,
            "Clinic",
            Margin,
            3_200_000,
            SlideWidth - (Margin * 2),
            600_000,
            document.ClinicName,
            2000,
            "DCE6FF",
            false,
            A.TextAlignmentTypeValues.Center));
    }

    private static void AddBulletContent(
        P.ShapeTree tree,
        ref uint id,
        IReadOnlyList<string> lines)
    {
        var limited = lines.Where(line => !string.IsNullOrWhiteSpace(line)).Take(12).ToList();
        if (limited.Count == 0) { AddEmptyState(tree, ref id); return; }

        // White rounded content panel so the dark text reads on the themed background.
        const long panelX = 560_000;
        const long panelTop = 1_080_000;
        var panelW = SlideWidth - (panelX * 2);
        var panelBottom = SlideHeight - 640_000;
        tree.Append(CreateRoundRectangle(id++, "ContentPanel", panelX, panelTop, panelW, panelBottom - panelTop, White, Border));

        var innerX = panelX + 220_000;
        var innerW = panelW - 440_000;
        var areaTop = panelTop + 200_000;
        var areaHeight = (panelBottom - panelTop) - 400_000;
        var height = Math.Min(560_000, areaHeight / Math.Max(1, limited.Count));
        var y = areaTop;

        foreach (var line in limited)
        {
            tree.Append(CreateTextBox(
                id++,
                $"Line{id}",
                innerX,
                y,
                innerW,
                height,
                $"• {line}",
                1500,
                TextColor,
                false,
                A.TextAlignmentTypeValues.Right));
            y += height;
        }
    }

    private static void AddTableContent(
        P.ShapeTree tree,
        ref uint id,
        PresentationSlideContent content)
    {
        var table = content.Table!;
        var columns = Math.Max(1, table.Headers.Count);
        var rows = table.Rows.Take(10).ToList();
        var x = 650_000L;
        var y = 1_150_000L;
        var width = SlideWidth - 1_300_000;
        var columnWidth = width / columns;
        var rowHeight = Math.Min(470_000L, 4_850_000L / Math.Max(2, rows.Count + 1));

        for (var column = 0; column < columns; column++)
        {
            tree.Append(CreateRectangle(
                id++,
                $"HeaderCell{column}",
                x + (column * columnWidth),
                y,
                columnWidth,
                rowHeight,
                Navy,
                Border));
            tree.Append(CreateTextBox(
                id++,
                $"HeaderText{column}",
                x + (column * columnWidth) + 40_000,
                y + 30_000,
                columnWidth - 80_000,
                rowHeight - 60_000,
                table.Headers[column],
                1050,
                White,
                true,
                A.TextAlignmentTypeValues.Center));
        }

        for (var row = 0; row < rows.Count; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var cellY = y + ((row + 1) * rowHeight);
                tree.Append(CreateRectangle(
                    id++,
                    $"Cell{row}_{column}",
                    x + (column * columnWidth),
                    cellY,
                    columnWidth,
                    rowHeight,
                    row % 2 == 0 ? "F8FAFC" : White,
                    Border));
                tree.Append(CreateTextBox(
                    id++,
                    $"CellText{row}_{column}",
                    x + (column * columnWidth) + 40_000,
                    cellY + 25_000,
                    columnWidth - 80_000,
                    rowHeight - 50_000,
                    column < rows[row].Count ? rows[row][column] : string.Empty,
                    950,
                    TextColor,
                    false,
                    A.TextAlignmentTypeValues.Center));
            }
        }

        if (rows.Count == 0)
            AddEmptyState(tree, ref id);
    }

    private static void AddImageContent(
        SlidePart slidePart,
        P.ShapeTree tree,
        ref uint id,
        PresentationSlideContent content)
    {
        var images = content.Images.Take(5).ToList();
        var boxes = GetImageBoxes(images.Count);

        for (var index = 0; index < images.Count; index++)
        {
            var image = images[index];
            var box = boxes[index];
            AddImage(slidePart, tree, ref id, image, box.X, box.Y, box.Width, box.Height);
            tree.Append(CreateTextBox(
                id++,
                $"ImageLabel{index}",
                box.X,
                box.Y + box.Height + 20_000,
                box.Width,
                220_000,
                image.Label,
                850,
                White,
                false,
                    A.TextAlignmentTypeValues.Center));
        }

        if (content.Lines.Count > 0)
        {
            tree.Append(CreateTextBox(
                id++,
                "ImageSummary",
                700_000,
                5_820_000,
                SlideWidth - 1_400_000,
                350_000,
                string.Join(" | ", content.Lines.Take(3)),
                900,
                "DCE6FF",
                false,
                A.TextAlignmentTypeValues.Center));
        }
    }

    // Layout tuned for the 4:3 slide (width 9,144,000).
    private static IReadOnlyList<ImageBox> GetImageBoxes(int count)
    {
        if (count <= 1)
            return [new ImageBox(672_000, 1_150_000, 7_800_000, 4_550_000)];

        if (count == 2)
            return
            [
                new ImageBox(400_000, 1_350_000, 4_140_000, 3_900_000),
                new ImageBox(4_604_000, 1_350_000, 4_140_000, 3_900_000),
            ];

        var boxes = new List<ImageBox>();
        var width = 2_680_000L;
        var height = 2_000_000L;
        var gap = 140_000L;
        var startX = 400_000L;
        for (var i = 0; i < count; i++)
        {
            var row = i / 3;
            var column = i % 3;
            boxes.Add(new ImageBox(
                startX + (column * (width + gap)),
                1_050_000 + (row * (height + 350_000)),
                width,
                height));
        }

        return boxes;
    }

    // Before/after comparison: images arrive interleaved [before, after, …]; each pair
    // is one column with the initial photo on top and the final photo below, each
    // captioned with its own "قبل/بعد — view" label.
    private static void AddBeforeAfterContent(
        SlidePart slidePart,
        P.ShapeTree tree,
        ref uint id,
        PresentationSlideContent content)
    {
        var images = content.Images.Take(6).ToList();
        var pairs = images.Count / 2;
        if (pairs == 0) return;

        const long startX = 700_000;
        var columnWidth = (SlideWidth - 1_400_000) / pairs;
        var imageWidth = columnWidth - 200_000;
        const long imageHeight = 2_000_000;
        const long topY = 1_200_000;
        const long bottomY = 3_650_000;

        for (var p = 0; p < pairs; p++)
        {
            var before = images[p * 2];
            var after = images[(p * 2) + 1];
            var x = startX + (p * columnWidth) + 100_000;

            AddImage(slidePart, tree, ref id, before, x, topY, imageWidth, imageHeight);
            tree.Append(CreateTextBox(id++, $"BeforeLabel{p}", x, topY + imageHeight + 20_000,
                imageWidth, 220_000, before.Label, 950, White, true, A.TextAlignmentTypeValues.Center));

            AddImage(slidePart, tree, ref id, after, x, bottomY, imageWidth, imageHeight);
            tree.Append(CreateTextBox(id++, $"AfterLabel{p}", x, bottomY + imageHeight + 20_000,
                imageWidth, 220_000, after.Label, 950, "C8F5E8", true, A.TextAlignmentTypeValues.Center));
        }
    }

    private static void AddImage(
        SlidePart slidePart,
        P.ShapeTree tree,
        ref uint id,
        PresentationImage image,
        long x,
        long y,
        long width,
        long height)
    {
        var imagePart = slidePart.AddImagePart(DetectImagePartType(image.Bytes));
        using (var imageStream = new MemoryStream(image.Bytes, writable: false))
            imagePart.FeedData(imageStream);

        var relationshipId = slidePart.GetIdOfPart(imagePart);
        var crop = CalculateCrop(image, width, height);

        var transform = new A.Transform2D(
            new A.Offset { X = x, Y = y },
            new A.Extents { Cx = width, Cy = height });
        if (image.FlipHorizontal)
            transform.HorizontalFlip = true;
        if (image.FlipVertical)
            transform.VerticalFlip = true;

        tree.Append(new P.Picture(
            new P.NonVisualPictureProperties(
                new P.NonVisualDrawingProperties { Id = id++, Name = image.Label },
                new P.NonVisualPictureDrawingProperties(
                    new A.PictureLocks { NoChangeAspect = true }),
                new ApplicationNonVisualDrawingProperties()),
            new P.BlipFill(
                new A.Blip { Embed = relationshipId },
                new A.SourceRectangle
                {
                    Left = crop.Left,
                    Right = crop.Right,
                    Top = crop.Top,
                    Bottom = crop.Bottom,
                },
                new A.Stretch(new A.FillRectangle())),
            new P.ShapeProperties(
                transform,
                new A.PresetGeometry(new A.AdjustValueList())
                {
                    Preset = A.ShapeTypeValues.Rectangle,
                })));
    }

    /// <summary>
    /// Builds the DrawingML source rectangle (amount cropped off each edge, in
    /// 1000ths of a percent) that first keeps the doctor's prepared crop region and
    /// then aspect-fills the remaining sub-image into the slide box without distortion.
    /// Exposed internally for unit testing the crop composition.
    /// </summary>
    internal static Crop CalculateCrop(
        int pixelWidth,
        int pixelHeight,
        long boxWidth,
        long boxHeight,
        double cropX = 0,
        double cropY = 0,
        double cropWidth = 1,
        double cropHeight = 1)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0 || boxWidth <= 0 || boxHeight <= 0)
            return new Crop(0, 0, 0, 0);

        // Sanitize the prepared region; fall back to the full image when invalid.
        if (cropWidth <= 0 || cropHeight <= 0 ||
            cropX < 0 || cropY < 0 || cropX + cropWidth > 1.0001 || cropY + cropHeight > 1.0001)
        {
            cropX = 0; cropY = 0; cropWidth = 1; cropHeight = 1;
        }

        // Edges cropped off the original by the prepared region.
        var left = cropX;
        var top = cropY;
        var right = 1 - (cropX + cropWidth);
        var bottom = 1 - (cropY + cropHeight);

        // Aspect-fill the kept sub-image into the box, adding the residual crop
        // within the sub-image's own dimensions (hence the cropWidth/cropHeight scale).
        var subRatio = ((double)pixelWidth * cropWidth) / ((double)pixelHeight * cropHeight);
        var boxRatio = (double)boxWidth / boxHeight;
        if (Math.Abs(subRatio - boxRatio) > 0.001)
        {
            if (subRatio > boxRatio)
            {
                var extra = (1 - (boxRatio / subRatio)) / 2 * cropWidth;
                left += extra;
                right += extra;
            }
            else
            {
                var extra = (1 - (subRatio / boxRatio)) / 2 * cropHeight;
                top += extra;
                bottom += extra;
            }
        }

        return new Crop(
            ToCropUnits(left, right),
            ToCropUnits(right, left),
            ToCropUnits(top, bottom),
            ToCropUnits(bottom, top));
    }

    private static Crop CalculateCrop(PresentationImage image, long boxWidth, long boxHeight) =>
        CalculateCrop(
            image.PixelWidth, image.PixelHeight, boxWidth, boxHeight,
            image.CropX, image.CropY, image.CropWidth, image.CropHeight);

    // Converts an edge fraction to DrawingML crop units (1000ths of a percent),
    // clamped so the pair on an axis never removes the whole image.
    private static int ToCropUnits(double edge, double opposite)
    {
        var value = (int)Math.Round(Math.Clamp(edge, 0, 1) * 100_000);
        var oppositeValue = (int)Math.Round(Math.Clamp(opposite, 0, 1) * 100_000);
        return Math.Clamp(value, 0, Math.Max(0, 99_000 - oppositeValue));
    }

    private static PartTypeInfo DetectImagePartType(byte[] bytes)
    {
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 &&
            bytes[1] == 0x50 &&
            bytes[2] == 0x4E &&
            bytes[3] == 0x47)
            return ImagePartType.Png;

        return ImagePartType.Jpeg;
    }

    private static P.Shape CreateRectangle(
        uint id,
        string name,
        long x,
        long y,
        long width,
        long height,
        string fill,
        string line,
        int lineWidth = 10_000) =>
        new(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = name },
                new P.NonVisualShapeDrawingProperties(),
                new ApplicationNonVisualDrawingProperties()),
            new P.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = x, Y = y },
                    new A.Extents { Cx = width, Cy = height }),
                new A.PresetGeometry(new A.AdjustValueList())
                {
                    Preset = A.ShapeTypeValues.Rectangle,
                },
                new A.SolidFill(new A.RgbColorModelHex { Val = fill }),
                new A.Outline(
                    new A.SolidFill(new A.RgbColorModelHex { Val = line }))
                {
                    Width = lineWidth,
                }));

    private static P.Shape CreateRoundRectangle(
        uint id,
        string name,
        long x,
        long y,
        long width,
        long height,
        string fill,
        string line) =>
        new(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = name },
                new P.NonVisualShapeDrawingProperties(),
                new ApplicationNonVisualDrawingProperties()),
            new P.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = x, Y = y },
                    new A.Extents { Cx = width, Cy = height }),
                new A.PresetGeometry(new A.AdjustValueList())
                {
                    Preset = A.ShapeTypeValues.RoundRectangle,
                },
                new A.SolidFill(new A.RgbColorModelHex { Val = fill }),
                new A.Outline(
                    new A.SolidFill(new A.RgbColorModelHex { Val = line }))
                {
                    Width = 9_000,
                }));

    // Generates the themed "ocean blue" background (radial blue gradient + a soft white
    // arc near the bottom) so the deck visually matches the reference theme — created
    // programmatically (no copyrighted theme asset is redistributed).
    internal static byte[] RenderOceanBackground(int width, int height)
    {
        var info = new SKImageInfo(width, height);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;

        using (var bg = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(width * 0.5f, height * 0.32f),
                width * 0.85f,
                [new SKColor(0x2A, 0x6F, 0xE0), new SKColor(0x0B, 0x39, 0x9B)],
                [0f, 1f],
                SKShaderTileMode.Clamp),
        })
        {
            canvas.DrawRect(0, 0, width, height, bg);
        }

        // Lighter band below a large arc, plus the thin white arc line.
        var cx = width * 0.5f;
        var cy = height * 2.15f;
        var r = height * 1.78f;
        using (var darker = new SKPaint { IsAntialias = true, Color = new SKColor(0x0A, 0x2F, 0x86) })
        {
            using var path = new SKPath();
            path.AddCircle(cx, cy, r);
            canvas.Save();
            canvas.ClipPath(path, antialias: true);
            canvas.DrawRect(0, 0, width, height, darker);
            canvas.Restore();
        }
        using (var arc = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(1.5f, height / 320f),
            Color = SKColors.White.WithAlpha(210),
        })
        {
            canvas.DrawCircle(cx, cy, r, arc);
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 92);
        return data.ToArray();
    }

    private static P.Shape CreateTextBox(
        uint id,
        string name,
        long x,
        long y,
        long width,
        long height,
        string text,
        int fontSize,
        string color,
        bool bold,
        A.TextAlignmentTypeValues alignment)
    {
        var runProperties = new A.RunProperties
        {
            Language = "ar-SA",
            AlternativeLanguage = "en-US",
            FontSize = fontSize,
            Bold = bold,
            Dirty = false,
        };
        runProperties.Append(new A.SolidFill(new A.RgbColorModelHex { Val = color }));
        runProperties.Append(new A.LatinFont { Typeface = "Arial" });
        runProperties.Append(new A.ComplexScriptFont { Typeface = "Arial" });

        var paragraphProperties = new A.ParagraphProperties
        {
            Alignment = alignment,
            RightToLeft = true,
        };

        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = name },
                new P.NonVisualShapeDrawingProperties { TextBox = true },
                new ApplicationNonVisualDrawingProperties()),
            new P.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = x, Y = y },
                    new A.Extents { Cx = width, Cy = height }),
                new A.PresetGeometry(new A.AdjustValueList())
                {
                    Preset = A.ShapeTypeValues.Rectangle,
                },
                new A.NoFill(),
                new A.Outline(new A.NoFill())),
            new P.TextBody(
                new A.BodyProperties
                {
                    Wrap = A.TextWrappingValues.Square,
                    RightToLeftColumns = true,
                    Anchor = A.TextAnchoringTypeValues.Center,
                },
                new A.ListStyle(),
                new A.Paragraph(
                    paragraphProperties,
                    new A.Run(runProperties, new A.Text(text ?? string.Empty)),
                    new A.EndParagraphRunProperties
                    {
                        Language = "ar-SA",
                        FontSize = fontSize,
                    })));
    }

    private static void AddEmptyState(P.ShapeTree tree, ref uint id)
    {
        tree.Append(CreateRectangle(
            id++,
            "Placeholder",
            1_550_000,
            1_650_000,
            SlideWidth - 3_100_000,
            3_250_000,
            "F8FAFC",
            Border,
            18_000));
        tree.Append(CreateTextBox(
            id++,
            "PlaceholderText",
            1_850_000,
            2_600_000,
            SlideWidth - 3_700_000,
            900_000,
            "لا توجد بيانات معتمدة لهذه الشريحة حتى الآن",
            1800,
            Muted,
            false,
            A.TextAlignmentTypeValues.Center));
    }

    private static P.SlideLayout CreateSlideLayout() =>
        new(
            new P.CommonSlideData(CreateShapeTree()) { Name = "Blank" },
            new P.ColorMapOverride(new A.MasterColorMapping()))
        {
            Type = SlideLayoutValues.Blank,
            Preserve = true,
        };

    private static P.SlideMaster CreateSlideMaster(string layoutRelationshipId, string backgroundRelId) =>
        new(
            new P.CommonSlideData(
                new P.Background(
                    new P.BackgroundProperties(
                        new A.BlipFill(
                            new A.Blip { Embed = backgroundRelId },
                            new A.Stretch(new A.FillRectangle())))),
                CreateShapeTree()),
            new P.ColorMap
            {
                Background1 = A.ColorSchemeIndexValues.Light1,
                Text1 = A.ColorSchemeIndexValues.Dark1,
                Background2 = A.ColorSchemeIndexValues.Light2,
                Text2 = A.ColorSchemeIndexValues.Dark2,
                Accent1 = A.ColorSchemeIndexValues.Accent1,
                Accent2 = A.ColorSchemeIndexValues.Accent2,
                Accent3 = A.ColorSchemeIndexValues.Accent3,
                Accent4 = A.ColorSchemeIndexValues.Accent4,
                Accent5 = A.ColorSchemeIndexValues.Accent5,
                Accent6 = A.ColorSchemeIndexValues.Accent6,
                Hyperlink = A.ColorSchemeIndexValues.Hyperlink,
                FollowedHyperlink = A.ColorSchemeIndexValues.FollowedHyperlink,
            },
            new P.SlideLayoutIdList(
                new P.SlideLayoutId
                {
                    Id = 2_147_483_649U,
                    RelationshipId = layoutRelationshipId,
                }),
            new P.TextStyles(
                new P.TitleStyle(),
                new P.BodyStyle(),
                new P.OtherStyle()));

    private static A.Theme CreateTheme() =>
        new(
            new A.ThemeElements(
                new A.ColorScheme(
                    new A.Dark1Color(new A.SystemColor
                    {
                        Val = A.SystemColorValues.WindowText,
                        LastColor = "000000",
                    }),
                    new A.Light1Color(new A.SystemColor
                    {
                        Val = A.SystemColorValues.Window,
                        LastColor = White,
                    }),
                    new A.Dark2Color(new A.RgbColorModelHex { Val = Navy }),
                    new A.Light2Color(new A.RgbColorModelHex { Val = "F3F6F9" }),
                    new A.Accent1Color(new A.RgbColorModelHex { Val = Blue }),
                    new A.Accent2Color(new A.RgbColorModelHex { Val = "3AAFA9" }),
                    new A.Accent3Color(new A.RgbColorModelHex { Val = "F59E0B" }),
                    new A.Accent4Color(new A.RgbColorModelHex { Val = "8B5CF6" }),
                    new A.Accent5Color(new A.RgbColorModelHex { Val = "EF4444" }),
                    new A.Accent6Color(new A.RgbColorModelHex { Val = "14B8A6" }),
                    new A.Hyperlink(new A.RgbColorModelHex { Val = "0563C1" }),
                    new A.FollowedHyperlinkColor(new A.RgbColorModelHex { Val = "954F72" }))
                { Name = "Aqlan Dental" },
                new A.FontScheme(
                    new A.MajorFont(
                        new A.LatinFont { Typeface = "Arial" },
                        new A.EastAsianFont { Typeface = string.Empty },
                        new A.ComplexScriptFont { Typeface = "Arial" }),
                    new A.MinorFont(
                        new A.LatinFont { Typeface = "Arial" },
                        new A.EastAsianFont { Typeface = string.Empty },
                        new A.ComplexScriptFont { Typeface = "Arial" }))
                { Name = "Aqlan Dental" },
                new A.FormatScheme(
                    new A.FillStyleList(
                        new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor }),
                        new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.Accent1 }),
                        new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor })),
                    new A.LineStyleList(
                        new A.Outline(
                            new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor }))
                        { Width = 6_350 },
                        new A.Outline(
                            new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.Accent1 }))
                        { Width = 12_700 },
                        new A.Outline(
                            new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.Accent2 }))
                        { Width = 19_050 }),
                    new A.EffectStyleList(
                        new A.EffectStyle(new A.EffectList()),
                        new A.EffectStyle(new A.EffectList()),
                        new A.EffectStyle(new A.EffectList())),
                    new A.BackgroundFillStyleList(
                        new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.Light1 }),
                        new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.Light2 }),
                        new A.SolidFill(new A.SchemeColor { Val = A.SchemeColorValues.PhColor })))
                { Name = "Aqlan Dental" }))
        { Name = "Aqlan Dental" };

    private sealed record ImageBox(long X, long Y, long Width, long Height);
    internal sealed record Crop(int Left, int Right, int Top, int Bottom);
}
