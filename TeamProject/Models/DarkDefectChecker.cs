using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TeamProject.Models;

public static class DarkDefectChecker
{
    public static List<Defect> Find(Mat patch, List<int> globalBoundaryY, int patchOffsetY)
    {
        var list = new List<Defect>();
        var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(7, 7));

        // 회색띠 제거
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
        Cv2.AddWeighted(darkDefect, 1.0, dog, 1.0, 0, combined);
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

            list.Add(new Defect
            { 
                X = rect.X,
                Y = rect.Y,
                Width = rect.Width,
                Height = rect.Height,
                Type = "Dark"
            });
        }

        return list;
    }
}