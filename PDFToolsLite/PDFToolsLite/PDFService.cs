using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PDFToolsLite
{
    public static class PdfService
    {
        public static void Merge(string[] inputFiles, string outputPath)
        {
            using var output = new PdfDocument();
            foreach (var file in inputFiles)
            {
                using var input = PdfReader.Open(file, PdfDocumentOpenMode.Import);
                for (int i = 0; i < input.PageCount; i++)
                {
                    output.AddPage(input.Pages[i]);
                }
            }
            output.Save(outputPath);
        }
        public static List<int> ParseRanges(string rangeSpec, int max)
        {
            var result = new List<int>();
            if (string.IsNullOrWhiteSpace(rangeSpec)) return result;
            var parts = rangeSpec.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                if (p.Contains('-'))
                {
                    var ab = p.Split('-', 2);
                    int a = int.Parse(ab[0]);
                    int b = int.Parse(ab[1]);
                    for (int i = Math.Max(1, a); i <= Math.Min(max, b); i++) result.Add(i);
                }
                else
                {
                    int i = int.Parse(p);
                    if (i >= 1 && i <= max) result.Add(i);
                }
            }
            return result.Distinct().OrderBy(x => x).ToList();
        }
        public static void Extract(string inputFile, IEnumerable<int> pages1Based, string outputPath)
        {
            using var src = PdfReader.Open(inputFile, PdfDocumentOpenMode.Import);
            using var dst = new PdfDocument();
            foreach (var p in pages1Based)
            {
                int idx = p - 1;
                if (idx >= 0 && idx < src.PageCount) dst.AddPage(src.Pages[idx]);
            }
            dst.Save(outputPath);
        }
        public static void DeletePages(string inputFile, IEnumerable<int> pagesToDelete1Based, string outputPath)
        {
            using var src = PdfReader.Open(inputFile, PdfDocumentOpenMode.Import);
            using var dst = new PdfDocument();
            var del = new HashSet<int>(pagesToDelete1Based);
            for (int i = 0; i < src.PageCount; i++)
            {
                int oneBased = i + 1;
                if (!del.Contains(oneBased)) dst.AddPage(src.Pages[i]);
            }
            dst.Save(outputPath);
        }
        public static void Rotate(string inputFile, IEnumerable<int> pages1Based, int angle, string outputPath)
        {
            using var src = PdfReader.Open(inputFile, PdfDocumentOpenMode.Import);
            using var dst = new PdfDocument();
            for (int i = 0; i < src.PageCount; i++)
            {
                var page = src.Pages[i];
                var newPage = dst.AddPage(page);
                if (pages1Based.Contains(i + 1))
                {
                    int current = page.Elements.GetInteger("/Rotate");
                    newPage.Elements.SetInteger("/Rotate", (current + angle) % 360);
                }
            }
            dst.Save(outputPath);
        }
        public static void ImagesToPdf(IEnumerable<string> imageFiles, string outputPath)
        {
            using var doc = new PdfSharpCore.Pdf.PdfDocument();
            foreach (var imgPath in imageFiles)
            {
                using var image = SixLabors.ImageSharp.Image.Load(imgPath);
                var page = doc.AddPage();
                page.Width = PdfSharpCore.PageSizeConverter.ToSize(PdfSharpCore.PageSize.A4).Width;
                page.Height = PdfSharpCore.PageSizeConverter.ToSize(PdfSharpCore.PageSize.A4).Height;

                using var gfx = PdfSharpCore.Drawing.XGraphics.FromPdfPage(page);
                using var ms = new MemoryStream();
                image.SaveAsJpeg(ms); ms.Position = 0;

                var ximg = PdfSharpCore.Drawing.XImage.FromStream(() => ms);
                double scale = Math.Min(page.Width / ximg.PixelWidth, page.Height / ximg.PixelHeight);
                double w = ximg.PixelWidth * scale, h = ximg.PixelHeight * scale;
                double x = (page.Width - w) / 2, y = (page.Height - h) / 2;
                gfx.DrawImage(ximg, x, y, w, h);
            }
            doc.Save(outputPath);
        }
    }
}
