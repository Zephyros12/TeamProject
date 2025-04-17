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

        var meanProfile = new double[rows];
        for (int y = 0; y < rows; y++)
        {
            meanProfile[y] = src.Row(y).Mean().Val0;
        }

        var boundaryY = new List<int>();
        for (int y = 1; y < rows - 1; y++)
        {
            double diff = Math.Abs(meanProfile[y] - meanProfile[y - 1]);
            if (diff > 20) boundaryY.Add(y);
        }

        int stride = patchSize;
        var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

        Parallel.For(0, rows / stride, options, py =>
        {
            for (int px = 0; px < cols / stride; px++)
            {
                int x = px * stride;
                int y = py * stride;
                int width = Math.Min(patchSize, cols - x);
                int height = Math.Min(patchSize, rows - y);

                var roi = new Rect(x, y, width, height);
                using var patch = new Mat(src, roi);

                var localDefects = ProcessPatchWithDualTopHatAndDoG(patch, boundaryY, y);
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
            Cv2.Rectangle(result, new Rect(defect.X, defect.Y, defect.Width, defect.Height), Scalar.Yellow, 4);
        }

        return (rawDefects.ToList(), result);
    }

    private static List<Defect> ProcessPatchWithDualTopHatAndDoG(Mat patch, List<int> globalBoundaryY, int patchOffsetY)
    {
        var list = new List<Defect>();
        var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(7, 7));

        // 💡 회색 띠 마스킹 (밝기 150~200 제거)
        var mask = new Mat();
        Cv2.InRange(patch, new Scalar(150), new Scalar(200), mask);
        Cv2.BitwiseNot(mask, mask);
        using var filteredPatch = new Mat();
        patch.CopyTo(filteredPatch, mask);

        var grayMask = new Mat();
        Cv2.InRange(filteredPatch, new Scalar(80), new Scalar(200), grayMask);
        using var grayMasked = new Mat();
        filteredPatch.CopyTo(grayMasked, grayMask);

        using var backgroundGray = new Mat();
        Cv2.MorphologyEx(grayMasked, backgroundGray, MorphTypes.Open, kernel);
        using var darkDefect = new Mat();
        Cv2.Subtract(grayMasked, backgroundGray, darkDefect);

        var blackMask = new Mat();
        Cv2.InRange(filteredPatch, new Scalar(0), new Scalar(80), blackMask);
        using var inverted = new Mat();
        Cv2.BitwiseNot(filteredPatch, inverted);
        using var blackMasked = new Mat();
        inverted.CopyTo(blackMasked, blackMask);

        using var backgroundLight = new Mat();
        Cv2.MorphologyEx(blackMasked, backgroundLight, MorphTypes.Open, kernel);
        using var lightDefect = new Mat();
        Cv2.Subtract(blackMasked, backgroundLight, lightDefect);

        using var blurSmall = new Mat();
        using var blurLarge = new Mat();
        Cv2.GaussianBlur(filteredPatch, blurSmall, new Size(3, 3), 1);
        Cv2.GaussianBlur(filteredPatch, blurLarge, new Size(9, 9), 2);
        using var dog = new Mat();
        Cv2.Subtract(blurSmall, blurLarge, dog);
        Cv2.Threshold(dog, dog, 10, 255, ThresholdTypes.Tozero);

        using var combined = new Mat();
        Cv2.Add(darkDefect, lightDefect, combined);
        Cv2.Add(combined, dog, combined);
        Cv2.Normalize(combined, combined, 0, 255, NormTypes.MinMax);

        using var binary = new Mat();
        Cv2.Threshold(combined, binary, 10, 255, ThresholdTypes.Binary);

        Cv2.FindContours(binary, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        foreach (var contour in contours)
        {
            var rect = Cv2.BoundingRect(contour);
            double area = Cv2.ContourArea(contour);
            double perimeter = Cv2.ArcLength(contour, true);
            double aspectRatio = (double)rect.Width / rect.Height;
            double circularity = perimeter == 0 ? 0 : 4 * Math.PI * area / (perimeter * perimeter);

            bool tooSmall = area < 5;
            bool tooLarge = area > 800;
            bool badAspect = aspectRatio < 0.2 || aspectRatio > 4.0;
            bool notRound = circularity < 0.7;
            bool isNearBorder = rect.X < 5 || rect.Y < 5 || rect.Right > patch.Width - 5 || rect.Bottom > patch.Height - 5;
            if (tooSmall || tooLarge || badAspect || notRound || isNearBorder)
                continue;

            using var roiMat = new Mat(filteredPatch, rect);
            Scalar mean = Cv2.Mean(roiMat);
            if (mean.Val0 < 20 || mean.Val0 > 240)
                continue;

            int roiTop = patchOffsetY + rect.Y;
            int roiBottom = roiTop + rect.Height;
            bool overlapsBoundary = globalBoundaryY.Any(b => b >= roiTop && b <= roiBottom);
            if (overlapsBoundary)
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
