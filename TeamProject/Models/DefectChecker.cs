using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TeamProject.Models;

public static class DefectChecker
{
    static DefectChecker()
    {
        Cv2.SetNumThreads(0);
    }

    // ✅ 잘린 이미지 + 오프셋 기준 검사
    public static (List<Defect> defects, Mat resultImage) FindDefectsWithDraw(Mat cropped, int threshold, Point offset)
    {
        var defects = new List<Defect>();
        int patchSize = 512, stride = 512;

        var meanProfile = Enumerable.Range(0, cropped.Rows)
            .Select(y => cropped.Row(y).Mean().Val0)
            .ToArray();

        var boundaryY = Enumerable.Range(1, cropped.Rows - 2)
            .Where(y => Math.Abs(meanProfile[y] - meanProfile[y - 1]) > 20)
            .ToList();

        for (int py = 0; py < cropped.Rows / stride; py++)
        {
            for (int px = 0; px < cropped.Cols / stride; px++)
            {
                int x = px * stride;
                int y = py * stride;
                int width = Math.Min(patchSize, cropped.Cols - x);
                int height = Math.Min(patchSize, cropped.Rows - y);

                var roi = new Rect(x, y, width, height);
                using var patch = new Mat(cropped, roi);

                var localDefects = ProcessPatchWithDualTopHatAndDoG(patch, boundaryY, y);

                foreach (var d in localDefects)
                {
                    defects.Add(new Defect
                    {
                        X = d.X + x + offset.X,
                        Y = d.Y + y + offset.Y,
                        Width = d.Width,
                        Height = d.Height
                    });
                }
            }
        }

        // 시각화용 이미지
        var result = new Mat();
        Cv2.CvtColor(cropped, result, ColorConversionCodes.GRAY2BGR);

        foreach (var defect in defects)
        {
            int px = defect.X - offset.X;
            int py = defect.Y - offset.Y;

            int padding = 30;
            int x = Math.Max(px - padding, 0);
            int y = Math.Max(py - padding, 0);
            int width = Math.Min(defect.Width + padding * 2, result.Cols - x);
            int height = Math.Min(defect.Height + padding * 2, result.Rows - y);

            Cv2.Rectangle(result, new Rect(x, y, width, height), Scalar.Yellow, 2);
        }

        return (defects, result);
    }

    private static List<Defect> ProcessPatchWithDualTopHatAndDoG(Mat patch, List<int> globalBoundaryY, int patchOffsetY)
    {
        var list = new List<Defect>();
        var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(7, 7));

        // 회색 띠 제거
        var mask = new Mat();
        Cv2.InRange(patch, new Scalar(150), new Scalar(200), mask);
        Cv2.BitwiseNot(mask, mask);
        using var filteredPatch = new Mat();
        patch.CopyTo(filteredPatch, mask);

        // 어두운 점 TopHat
        var grayMask = new Mat();
        Cv2.InRange(filteredPatch, new Scalar(80), new Scalar(200), grayMask);
        using var grayMasked = new Mat();
        filteredPatch.CopyTo(grayMasked, grayMask);
        using var backgroundGray = new Mat();
        Cv2.MorphologyEx(grayMasked, backgroundGray, MorphTypes.Open, kernel);
        using var darkDefect = new Mat();
        Cv2.Subtract(grayMasked, backgroundGray, darkDefect);

        // 밝은 점 TopHat
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

        // DoG
        using var blurSmall = new Mat();
        using var blurLarge = new Mat();
        Cv2.GaussianBlur(filteredPatch, blurSmall, new Size(3, 3), 1);
        Cv2.GaussianBlur(filteredPatch, blurLarge, new Size(9, 9), 2);
        using var dog = new Mat();
        Cv2.Subtract(blurSmall, blurLarge, dog);
        Cv2.Threshold(dog, dog, 10, 255, ThresholdTypes.Tozero);

        // 병합
        using var combined = new Mat();
        Cv2.AddWeighted(darkDefect, 1.0, lightDefect, 1.5, 0, combined);
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

            if (area < 5 || area > 800 ||
                aspectRatio < 0.2 || aspectRatio > 4.0 ||
                circularity < 0.7 ||
                rect.X < 5 || rect.Y < 5 || rect.Right > patch.Width - 5 || rect.Bottom > patch.Height - 5)
                continue;

            using var roiMat = new Mat(filteredPatch, rect);
            Scalar mean = Cv2.Mean(roiMat);
            if (mean.Val0 < 20 || mean.Val0 > 240)
                continue;

            int roiTop = patchOffsetY + rect.Y;
            int roiBottom = roiTop + rect.Height;
            if (globalBoundaryY.Any(b => b >= roiTop && b <= roiBottom))
                continue;

            list.Add(new Defect { X = rect.X, Y = rect.Y, Width = rect.Width, Height = rect.Height });
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