using OpenCvSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TeamProject.Models;

public static class DefectChecker
{
    static DefectChecker()
    {
        Cv2.SetNumThreads(0);
    }

    public static (List<Defect> defects, Mat resultImage) FindDefectsWithDraw(string imagePath, int threshold)
    {
        var rawDefects = new ConcurrentBag<Defect>();

        var src = new Mat(imagePath, ImreadModes.Grayscale);
        if (src.Empty())
            return (new List<Defect>(), src);

        int patchSize = 256;
        int cols = src.Cols;
        int rows = src.Rows;

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };

        Parallel.For(0, rows / patchSize, options, py =>
        {
            for (int px = 0; px < cols / patchSize; px++)
            {
                int x = px * patchSize;
                int y = py * patchSize;
                int width = Math.Min(patchSize, cols - x);
                int height = Math.Min(patchSize, rows - y);

                var roi = new Rect(x, y, width, height);
                using var patch = new Mat(src, roi);

                var localDefects = ProcessPatchWithDualTopHat(patch);
                foreach (var d in localDefects)
                {
                    rawDefects.Add(new Defect
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

        foreach (var defect in rawDefects)
        {
            Cv2.Rectangle(result, new Rect(defect.X, defect.Y, defect.Width, defect.Height), Scalar.Yellow, 2);
        }

        return (rawDefects.ToList(), result);
    }

    private static List<Defect> ProcessPatchWithDualTopHat(Mat patch)
    {
        var list = new List<Defect>();

        var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(7, 7));

        // 1. 회색 영역 (밝기 80~200) → 어두운 점 검출용
        var grayMask = new Mat();
        Cv2.InRange(patch, new Scalar(80), new Scalar(200), grayMask);
        using var grayMasked = new Mat();
        patch.CopyTo(grayMasked, grayMask);

        using var backgroundGray = new Mat();
        Cv2.MorphologyEx(grayMasked, backgroundGray, MorphTypes.Open, kernel);
        using var darkDefect = new Mat();
        Cv2.Subtract(grayMasked, backgroundGray, darkDefect);

        // 2. 검은 영역 (밝기 0~80) → 밝은 점 검출용
        var blackMask = new Mat();
        Cv2.InRange(patch, new Scalar(0), new Scalar(80), blackMask);
        using var inverted = new Mat();
        Cv2.BitwiseNot(patch, inverted);
        using var blackMasked = new Mat();
        inverted.CopyTo(blackMasked, blackMask);

        using var backgroundLight = new Mat();
        Cv2.MorphologyEx(blackMasked, backgroundLight, MorphTypes.Open, kernel);
        using var lightDefect = new Mat();
        Cv2.Subtract(blackMasked, backgroundLight, lightDefect);

        // 3. 병합
        using var combined = new Mat();
        Cv2.Add(darkDefect, lightDefect, combined);
        Cv2.Normalize(combined, combined, 0, 255, NormTypes.MinMax);

        using var binary = new Mat();
        Cv2.Threshold(combined, binary, 5, 255, ThresholdTypes.Binary);

        Cv2.FindContours(binary, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        foreach (var contour in contours)
        {
            var rect = Cv2.BoundingRect(contour);
            double area = Cv2.ContourArea(contour);
            double perimeter = Cv2.ArcLength(contour, true);
            double aspectRatio = (double)rect.Width / rect.Height;
            double circularity = perimeter == 0 ? 0 : 4 * Math.PI * area / (perimeter * perimeter);

            bool tooSmall = area < 1;
            bool tooLarge = area > 1000;
            bool badAspect = aspectRatio < 0.2 || aspectRatio > 5.0;
            bool notRound = circularity < 0.6;

            if (tooSmall || tooLarge || badAspect || notRound)
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
