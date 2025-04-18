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

    public static (List<Defect> defects, Mat resultImage) FindDefectsWithDraw(Mat cropped, int threshold, Point offset)
    {
        var defects = new List<Defect>();
        int patchSize = 512, stride = 512;
        int cols = cropped.Cols, rows = cropped.Rows;

        var meanProfile = Enumerable.Range(0, rows)
            .Select(y => cropped.Row(y).Mean().Val0)
            .ToArray();

        var boundaryY = Enumerable.Range(1, rows - 2)
            .Where(y => Math.Abs(meanProfile[y] - meanProfile[y - 1]) > 20)
            .ToList();

        for (int py = 0; py < rows / stride; py++)
        {
            for (int px = 0; px < cols / stride; px++)
            {
                int x = px * stride;
                int y = py * stride;
                int width = Math.Min(patchSize, cols - x);
                int height = Math.Min(patchSize, rows - y);
                var roi = new Rect(x, y, width, height);

                using var patch = new Mat(cropped, roi);
                var localDefects = ProcessPatch(patch, boundaryY, y);

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

    private static List<Defect> ProcessPatch(Mat patch, List<int> boundaryY, int patchOffsetY)
    {
        var darkDefects = DarkDefectChecker.Find(patch, boundaryY, patchOffsetY);
        var brightDefects = BrightDefectChecker.Find(patch);
        return darkDefects.Concat(brightDefects).ToList();
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