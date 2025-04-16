using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace TeamProject.Models;

public static class DefectChecker
{
    public static (List<Defect> defects, Mat resultImage) FindDefectsWithDraw(string imagePath, int threshold)
    {
        var defects = new List<Defect>();

        var src = new Mat(imagePath, ImreadModes.Color);
        if (src.Empty())
            return (defects, src);

        using var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(3, 3), 0);

        using var binary = new Mat();
        Cv2.Threshold(blurred, binary, threshold, 255, ThresholdTypes.BinaryInv);

        using var morphed = new Mat();
        var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        Cv2.MorphologyEx(binary, morphed, MorphTypes.Close, kernel);

        Cv2.FindContours(binary, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        foreach (var contour in contours)
        {
            var rect = Cv2.BoundingRect(contour);
            double area = Cv2.ContourArea(contour);
            double perimeter = Cv2.ArcLength(contour, true);
            double aspectratio = (double)rect.Width / rect.Height;
            double circularity = perimeter == 0 ? 0 : 4 * Math.PI * area / (perimeter * perimeter);

            bool tooSmall = area < 20;
            bool badAspect = aspectratio < 0.2 || aspectratio > 5.0;
            bool notRound = circularity < 0.2;

            if (tooSmall || badAspect || notRound)
                continue;

            defects.Add(new Defect
            {
                X = rect.X,
                Y = rect.Y,
                Width = rect.Width,
                Height = rect.Height,
            });

            Cv2.Rectangle(src, rect, Scalar.Yellow, 2);
        }

        return (defects, src);
    }

    public static MemoryStream ToMemoryStream(this Mat mat)
    {
        var ms = new MemoryStream();
        Cv2.ImEncode(".bmp", mat, out var imageBytes);
        ms.Write(imageBytes, 0, imageBytes.Length);
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }
}
