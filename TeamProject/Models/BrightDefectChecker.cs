using OpenCvSharp;
using System;
using System.Collections.Generic;

namespace TeamProject.Models;

public static class BrightDefectChecker
{
    public static List<Defect> Find(Mat patch)
    {
        var list = new List<Defect>();
        var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(7, 7));

        // 어두운 배경만 마스킹
        var darkMask = new Mat();
        Cv2.InRange(patch, new Scalar(0), new Scalar(100), darkMask);
        using var masked = new Mat();
        patch.CopyTo(masked, darkMask);

        // 밝은 점 TopHat (반전 이미지 기반)
        using var inverted = new Mat();
        Cv2.BitwiseNot(masked, inverted);
        using var background = new Mat();
        Cv2.MorphologyEx(inverted, background, MorphTypes.Open, kernel);
        using var lightDefect = new Mat();
        Cv2.Subtract(inverted, background, lightDefect);

        // DoG
        using var blurSmall = new Mat();
        using var blurLarge = new Mat();
        Cv2.GaussianBlur(masked, blurSmall, new Size(3, 3), 1);
        Cv2.GaussianBlur(masked, blurLarge, new Size(9, 9), 2);
        using var dog = new Mat();
        Cv2.Subtract(blurSmall, blurLarge, dog);
        Cv2.Threshold(dog, dog, 10, 255, ThresholdTypes.Tozero);

        // 병합
        using var combined = new Mat();
        Cv2.AddWeighted(lightDefect, 1.0, dog, 1.0, 0, combined);
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

            if (area < 5 || area > 600 ||
                aspectRatio < 0.3 || aspectRatio > 3.0 ||
                circularity < 0.6 ||
                rect.X < 5 || rect.Y < 5 || rect.Right > patch.Width - 5 || rect.Bottom > patch.Height - 5)
                continue;

            using var roiMat = new Mat(patch, rect);
            Scalar mean, stddev;
            Cv2.MeanStdDev(roiMat, out mean, out stddev);
            if (mean.Val0 < 20 || mean.Val0 > 255 || stddev.Val0 < 2.5)
                continue;

            list.Add(new Defect
            {
                X = rect.X,
                Y = rect.Y,
                Width = rect.Width,
                Height = rect.Height,
                Type = "Bright"
            });
        }

        return list;
    }
}
