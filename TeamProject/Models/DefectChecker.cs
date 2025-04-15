using OpenCvSharp;
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
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);

        using var binary = new Mat();
        Cv2.Threshold(blurred, binary, threshold, 255, ThresholdTypes.BinaryInv);

        Cv2.FindContours(binary, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        foreach (var contour in contours)
        {
            var rect = Cv2.BoundingRect(contour);

            if (rect.Width < 5 || rect.Height < 5)
                continue;

            defects.Add(new Defect
            {
                X = rect.X,
                Y = rect.Y,
                Width = rect.Width,
                Height = rect.Height
            });

            Cv2.Rectangle(src, rect, Scalar.Red, 2);
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
