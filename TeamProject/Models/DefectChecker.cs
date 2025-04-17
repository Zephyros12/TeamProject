using OpenCvSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace TeamProject.Models;

public static class DefectChecker
{
    public static (List<Defect> defects, Mat resultImage) FindDefectsWithDraw(string imagePath, int threshold)
    {
        var defects = new ConcurrentBag<Defect>();

        var src = new Mat(imagePath, ImreadModes.Grayscale);
        if (src.Empty())
            return (new List<Defect>(), src);

        int patchSize = 512;
        int cols = src.Cols;
        int rows = src.Rows;

        Parallel.For(0, (int)Math.Ceiling((double)rows / patchSize), py =>
        {
            for (int px = 0; px < Math.Ceiling((double)cols / patchSize); px++)
            {
                int x = px * patchSize;
                int y = py * patchSize;
                int width = Math.Min(patchSize, cols - x);
                int height = Math.Min(patchSize, rows - y);

                var roi = new Rect(x, y, width, height);
                using var patch = new Mat(src, roi);

                var localDefects = ProcessPatch(patch);
                foreach (var d in localDefects)
                {
                    defects.Add(new Defect
                    {
                        X = d.X + x,
                        Y = d.Y + y,
                        Width = d.Width,
                        Height = d.Height
                    });
                }
            }
        });

        var result = new Mat();
        Cv2.CvtColor(src, result, ColorConversionCodes.GRAY2BGR);

        foreach (var defect in defects)
        {
            Cv2.Rectangle(result, new Rect(defect.X, defect.Y, defect.Width, defect.Height), Scalar.Yellow, 2);
        }

        return (new List<Defect>(defects), result);
    }

    private static List<Defect> ProcessPatch(Mat patch)
    {
        var list = new List<Defect>();

        using var binary = new Mat();
        Cv2.Threshold(patch, binary, 30, 255, ThresholdTypes.BinaryInv);

        var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        using var morphed = new Mat();
        Cv2.MorphologyEx(binary, morphed, MorphTypes.Close, kernel);

        Cv2.FindContours(morphed, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        foreach (var contour in contours)
        {
            var rect = Cv2.BoundingRect(contour);
            double area = Cv2.ContourArea(contour);

            if (area < 2 || area > 500)
                continue;

            list.Add(new Defect
            {
                X = rect.X,
                Y = rect.Y,
                Width = rect.Width,
                Height = rect.Height
            });
        }

        return list;
    }

    public static MemoryStream ToMemoryStream(this Mat mat)
    {
        var ms = new MemoryStream();
        Cv2.ImEncode(".png", mat, out var imageBytes);
        ms.Write(imageBytes, 0, imageBytes.Length);
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }
}