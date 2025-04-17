using OpenCvSharp;
using System.IO;

namespace TeamProject.Models
{
    public static class ImageHelper
    {
        public static MemoryStream ToMemoryStream(this Mat mat)
        {
            var ms = new MemoryStream();
            Cv2.ImEncode(".png", mat, out var imageBytes);
            ms.Write(imageBytes, 0, imageBytes.Length);
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }
    }
}
