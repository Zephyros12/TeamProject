using OpenCvSharp;
using System;
using System.Collections.Generic;

namespace TeamProject.Models;

public static class BrightDefectChecker
{
    public static List<Defect> Find(Mat patch)
    {
        var list = new List<Defect>();

        // 1. Sobel Gradient 계산
        using var gradX = new Mat();
        using var gradY = new Mat();
        using var absGradX = new Mat();
        using var absGradY = new Mat();
        Cv2.Sobel(patch, gradX, MatType.CV_16S, 1, 0);
        Cv2.Sobel(patch, gradY, MatType.CV_16S, 0, 1);
        Cv2.ConvertScaleAbs(gradX, absGradX);
        Cv2.ConvertScaleAbs(gradY, absGradY);

        using var gradient = new Mat();
        Cv2.AddWeighted(absGradX, 0.5, absGradY, 0.5, 0, gradient);

        // 2. 밝은 마스크 추출
        using var brightMask = new Mat();
        Cv2.Threshold(patch, brightMask, 200, 255, ThresholdTypes.Binary);

        // 3. 경계 + 밝기 마스크 조합
        using var candidate = new Mat();
        Cv2.BitwiseAnd(gradient, brightMask, candidate);

        // 4. 이진화 (gradient 강한 부분만)
        using var binary = new Mat();
        Cv2.Threshold(candidate, binary, 15, 255, ThresholdTypes.Binary);

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