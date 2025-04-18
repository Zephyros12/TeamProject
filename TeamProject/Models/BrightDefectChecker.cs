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

        // 반전 → 밝은 점 강조
        using var inverted = new Mat();
        Cv2.BitwiseNot(patch, inverted);

        using var background = new Mat();
        Cv2.MorphologyEx(inverted, background, MorphTypes.Open, kernel);

        using var tophat = new Mat();
        Cv2.Subtract(inverted, background, tophat);

        using var binary = new Mat();
        Cv2.Threshold(tophat, binary, 5, 255, ThresholdTypes.Binary);

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

            using var roiMat = new Mat(patch, rect);
            Scalar mean = Cv2.Mean(roiMat);
            if (mean.Val0 < 20 || mean.Val0 > 240)
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
}