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

        int patchSize = 128;

        for (int y = 0; y < src.Rows; y += patchSize)
        {
            for (int x = 0; x < src.Cols; x += patchSize)
            {
                int width = Math.Min(patchSize, src.Cols - x);
                int height = Math.Min(patchSize, src.Rows - y);
                var roi = new Rect(x, y, width, height);
                var patch = new Mat(src, roi);

                var found = FindDefectsInPatch(patch, threshold);

                foreach (var d in found)
                {
                    var global = new Defect
                    {
                        X = d.X + x,
                        Y = d.Y + y,
                        Width = d.Width,
                        Height = d.Height
                    };
                    defects.Add(global);
                    Cv2.Rectangle(src, new Rect(global.X, global.Y, global.Width, global.Height), Scalar.Yellow, 1);
                }
            }
        }

        return (defects, src);
    }

    private static List<Defect> FindDefectsInPatch(Mat patch, int threshold)
    {
        var defects = new List<Defect>();

        using var gray = new Mat();
        Cv2.CvtColor(patch, gray, ColorConversionCodes.BGR2GRAY);

        using var binary = new Mat();
        Cv2.Threshold(gray, binary, threshold, 255, ThresholdTypes.BinaryInv);

        Cv2.FindContours(binary, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        foreach (var contour in contours)
        {
            var rect = Cv2.BoundingRect(contour);
            double area = Cv2.ContourArea(contour);
            double perimeter = Cv2.ArcLength(contour, true);
            double aspectRatio = (double)rect.Width / rect.Height;
            double circularity = perimeter == 0 ? 0 : 4 * Math.PI * area / (perimeter * perimeter);

            bool tooSmall = area < 1;
            bool tooLarge = area > 2000;
            bool badAspect = aspectRatio < 0.01 || aspectRatio > 100.0;

            if (tooSmall || tooLarge || badAspect)
                continue;

            defects.Add(new Defect
            {
                X = rect.X,
                Y = rect.Y,
                Width = rect.Width,
                Height = rect.Height
            });
        }

        return defects;
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
