using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.Storage.Pickers;
using PdfSharpCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf.IO;
using SixLabors.ImageSharp.Formats.Jpeg;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Services.Store;
using Windows.Storage;
using Windows.Storage.Streams;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace PDFToolsLite
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            SetStatus("Ready.");
            ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);
            _context = StoreContext.GetDefault();
            InitializeWithWindow.Initialize(_context, WindowNative.GetWindowHandle(this));
            PurchaseUtils.PurchaseContext = _context;
            Utils.MainWindow = this;
        }
        private StoreContext _context = null;
        public ObservableCollection<FileItem> Files { get; } = new();
        // -------------------------
        // WindowId / common helpers
        // -------------------------
        private WindowId GetWindowId()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            return Win32Interop.GetWindowIdFromWindow(hwnd);
        }

        private void SetStatus(string text) => StatusText.Text = text;
        private void SetProgress(double v) => Progress.Value = v;

        // -------------------------
        // Pickers (PATH-based)
        // -------------------------
        private async Task<List<string>> PickPdfPathsAsync(bool allowMulti = true)
        {
            var picker = new FileOpenPicker(GetWindowId())
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add(".pdf");

            if (allowMulti)
            {
                var results = await picker.PickMultipleFilesAsync();
                return results?.Select(r => r.Path).ToList() ?? new List<string>();
            }
            else
            {
                var r = await picker.PickSingleFileAsync();
                return r is null ? new List<string>() : new List<string> { r.Path };
            }
        }

        private async Task<List<string>> PickImagePathsAsync()
        {
            var picker = new FileOpenPicker(GetWindowId())
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.PicturesLibrary
            };
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".bmp");
            var results = await picker.PickMultipleFilesAsync();
            return results?.Select(r => r.Path).ToList() ?? new List<string>();
        }

        private async Task<string?> PickSavePdfPathAsync(string suggested = "Output.pdf")
        {
            var saver = new FileSavePicker(GetWindowId())
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = Path.GetFileNameWithoutExtension(suggested),
                DefaultFileExtension = ".pdf",
                CommitButtonText = "Save"
            };
            saver.FileTypeChoices.Add("PDF document", new List<string> { ".pdf" });

            var r = await saver.PickSaveFileAsync();   // PickFileResult
            return r?.Path;
        }

        private async Task<string?> PickSavePngPathAsync(string suggestedBaseName)
        {
            var saver = new FileSavePicker(GetWindowId())
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                SuggestedFileName = suggestedBaseName,
                DefaultFileExtension = ".png",
                CommitButtonText = "Save PNG"
            };
            saver.FileTypeChoices.Add("PNG image", new List<string> { ".png" });

            var r = await saver.PickSaveFileAsync();
            return r?.Path;
        }

        // -------------------------
        // Button handlers
        // -------------------------
        private async void AddPdfs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var paths = await PickPdfPathsAsync(allowMulti: true);
                if (paths.Count == 0) return;

                foreach (var p in paths)
                {
                    int pageCount = await GetPdfPageCountAsync(p);
                    Files.Add(new FileItem
                    {
                        FileName = Path.GetFileName(p),
                        FullPath = p,
                        PageCount = pageCount
                    });
                }
                SetStatus($"Added {paths.Count} file(s).");
            }
            catch (Exception ex)
            {
                SetStatus("Failed to add PDFs: " + ex.Message);
            }
        }

        private async void Merge_Click(object sender, RoutedEventArgs e)
        {
            if (Files.Count < 2) { SetStatus("Add at least two PDFs to merge."); return; }

            var savePath = await PickSavePdfPathAsync("merged.pdf");
            if (string.IsNullOrWhiteSpace(savePath)) return;

            try
            {
                SetStatus("Merging...");
                SetProgress(10);

                using var output = new PdfSharpCore.Pdf.PdfDocument();
                foreach (var item in Files)
                {
                    using var input = PdfReader.Open(item.FullPath, PdfDocumentOpenMode.Import);
                    for (int i = 0; i < input.PageCount; i++)
                        output.AddPage(input.Pages[i]);
                }

                using var fs = File.Open(savePath!, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                output.Save(fs, closeStream: true);

                SetProgress(100);
                SetStatus("Merged successfully.");
            }
            catch (Exception ex)
            {
                SetStatus("Merge failed: " + ex.Message);
            }
            finally { SetProgress(0); }
        }

        private async void SplitExtract_Click(object sender, RoutedEventArgs e)
        {
            var sel = (FileItem)FilesList.SelectedItem;
            if (sel == null) { SetStatus("Select a source PDF from the list."); return; }

            var ranges = ParseRanges(PageRangeBox.Text, sel.PageCount);
            if (ranges.Count == 0) { SetStatus("Enter a valid page range (e.g. 1-3,5)."); return; }

            var savePath = await PickSavePdfPathAsync($"extract_{Path.GetFileNameWithoutExtension(sel.FileName)}.pdf");
            if (string.IsNullOrWhiteSpace(savePath)) return;

            try
            {
                SetStatus("Extracting pages...");
                using var src = PdfReader.Open(sel.FullPath, PdfDocumentOpenMode.Import);
                using var dst = new PdfSharpCore.Pdf.PdfDocument();

                foreach (var p in ranges.Distinct().OrderBy(x => x))
                {
                    int idx = p - 1;
                    if (idx >= 0 && idx < src.PageCount)
                        dst.AddPage(src.Pages[idx]);
                }

                using var fs = File.Open(savePath!, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                dst.Save(fs, closeStream: true);

                SetStatus("Extracted successfully.");
            }
            catch (Exception ex)
            {
                SetStatus("Extract failed: " + ex.Message);
            }
        }

        private async void DeletePages_Click(object sender, RoutedEventArgs e)
        {
            var sel = (FileItem)FilesList.SelectedItem;
            if (sel == null) { SetStatus("Select a source PDF from the list."); return; }

            var del = new HashSet<int>(ParseRanges(PageRangeBox.Text, sel.PageCount));
            if (del.Count == 0) { SetStatus("Enter pages to delete (e.g. 2,4,6-9)."); return; }

            var savePath = await PickSavePdfPathAsync($"deleted_{Path.GetFileNameWithoutExtension(sel.FileName)}.pdf");
            if (string.IsNullOrWhiteSpace(savePath)) return;

            try
            {
                SetStatus("Deleting pages...");
                using var src = PdfReader.Open(sel.FullPath, PdfDocumentOpenMode.Import);
                using var dst = new PdfSharpCore.Pdf.PdfDocument();

                for (int i = 0; i < src.PageCount; i++)
                {
                    int oneBased = i + 1;
                    if (!del.Contains(oneBased))
                        dst.AddPage(src.Pages[i]);
                }

                using var fs = File.Open(savePath!, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                dst.Save(fs, closeStream: true);

                SetStatus("Pages deleted.");
            }
            catch (Exception ex)
            {
                SetStatus("Delete failed: " + ex.Message);
            }
        }

        private async void Rotate_Click(object sender, RoutedEventArgs e)
        {
            var sel = (FileItem)FilesList.SelectedItem;
            if (sel == null) { SetStatus("Select a source PDF from the list."); return; }

            var pages = ParseRanges(PageRangeBox.Text, sel.PageCount);
            if (pages.Count == 0) { SetStatus("Enter pages to rotate (e.g. 1-3)."); return; }

            int angle = GetRotateAngle();
            if (angle == 0) { SetStatus("Choose a rotate angle."); return; }

            var savePath = await PickSavePdfPathAsync($"rotated_{Path.GetFileNameWithoutExtension(sel.FileName)}.pdf");
            if (string.IsNullOrWhiteSpace(savePath)) return;

            try
            {
                SetStatus("Rotating pages...");
                using var src = PdfReader.Open(sel.FullPath, PdfDocumentOpenMode.Import);
                using var dst = new PdfSharpCore.Pdf.PdfDocument();

                for (int i = 0; i < src.PageCount; i++)
                {
                    var page = src.Pages[i];
                    var newPage = dst.AddPage(page);
                    if (pages.Contains(i + 1))
                    {
                        int current = page.Elements.GetInteger("/Rotate");
                        newPage.Elements.SetInteger("/Rotate", (current + angle) % 360);
                    }
                }

                using var fs = File.Open(savePath!, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                dst.Save(fs, closeStream: true);

                SetStatus("Pages rotated.");
            }
            catch (Exception ex)
            {
                SetStatus("Rotate failed: " + ex.Message);
            }
        }

        private async void ImagesToPdf_Click(object sender, RoutedEventArgs e)
        {
            var imagePaths = await PickImagePathsAsync();
            if (imagePaths.Count == 0) return;

            var savePath = await PickSavePdfPathAsync("images.pdf");
            if (string.IsNullOrWhiteSpace(savePath)) return;

            try
            {
                SetStatus("Converting images to PDF...");
                using var doc = new PdfSharpCore.Pdf.PdfDocument();
                var a4 = PageSizeConverter.ToSize(PageSize.A4);

                foreach (var imgPath in imagePaths)
                {
                    using var img = SixLabors.ImageSharp.Image.Load(imgPath);

                    var page = doc.AddPage();
                    page.Width = a4.Width;
                    page.Height = a4.Height;

                    using var ms = new MemoryStream();
                    img.Save(ms, new JpegEncoder { Quality = 90 });
                    ms.Position = 0;

                    using var gfx = XGraphics.FromPdfPage(page);
                    var ximg = XImage.FromStream(() => ms);

                    double scale = Math.Min(page.Width / ximg.PixelWidth, page.Height / ximg.PixelHeight);
                    double w = ximg.PixelWidth * scale;
                    double h = ximg.PixelHeight * scale;
                    double x = (page.Width - w) / 2;
                    double y = (page.Height - h) / 2;

                    gfx.DrawImage(ximg, x, y, w, h);
                }

                using var fs = File.Open(savePath!, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                doc.Save(fs, closeStream: true);

                SetStatus("Images converted to PDF.");
            }
            catch (Exception ex)
            {
                SetStatus("Images→PDF failed: " + ex.Message);
            }
        }

        private async void PdfToPng_Click(object sender, RoutedEventArgs e)
        {
            // Prefer selected item; fallback to picker
            string? pdfPath = (FilesList.SelectedItem as FileItem)?.FullPath;
            if (string.IsNullOrWhiteSpace(pdfPath))
            {
                var pickOne = await PickPdfPathsAsync(allowMulti: false);
                if (pickOne.Count == 0) return;
                pdfPath = pickOne[0];
            }

            var pngPath = await PickSavePngPathAsync(Path.GetFileNameWithoutExtension(pdfPath) + "_page1");
            if (string.IsNullOrWhiteSpace(pngPath)) return;

            try
            {
                SetStatus("Rendering first page to PNG...");
                await RenderPdfFirstPageToPngAsync(pdfPath!, pngPath!, maxSide: 1800);
                SetStatus("Exported PNG successfully.");
            }
            catch (Exception ex)
            {
                SetStatus("PDF→PNG failed: " + ex.Message);
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            ContentGrid.Visibility = Visibility.Collapsed;
            AppAboutPage.Visibility = Visibility.Visible;
            AppTitleBar.IsBackButtonVisible = true;
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            var idx = FilesList.SelectedIndex;
            if (idx > 0)
            {
                var item = Files[idx];
                Files.RemoveAt(idx);
                Files.Insert(idx - 1, item);
                FilesList.SelectedIndex = idx - 1;
            }
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            var idx = FilesList.SelectedIndex;
            if (idx >= 0 && idx < Files.Count - 1)
            {
                var item = Files[idx];
                Files.RemoveAt(idx);
                Files.Insert(idx + 1, item);
                FilesList.SelectedIndex = idx + 1;
            }
        }

        private void ClearList_Click(object sender, RoutedEventArgs e)
        {
            Files.Clear();
            SetStatus("List cleared.");
        }

        // -------------------------
        // Core helpers
        // -------------------------
        private async Task<int> GetPdfPageCountAsync(string pdfPath)
        {
            try
            {
                using var doc = PdfReader.Open(pdfPath, PdfDocumentOpenMode.ReadOnly);
                return doc.PageCount;
            }
            catch
            {
                var sf = await StorageFile.GetFileFromPathAsync(pdfPath);
                var pdf = await PdfDocument.LoadFromFileAsync(sf);
                return (int)pdf.PageCount;
            }
        }

        private static List<int> ParseRanges(string spec, int max)
        {
            var result = new List<int>();
            if (string.IsNullOrWhiteSpace(spec)) return result;

            var parts = spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var p in parts)
            {
                if (p.Contains('-'))
                {
                    var ab = p.Split('-', 2, StringSplitOptions.TrimEntries);
                    if (int.TryParse(ab[0], out int a) && int.TryParse(ab[1], out int b))
                    {
                        if (a > b) (a, b) = (b, a);
                        for (int i = Math.Max(1, a); i <= Math.Min(max, b); i++)
                            result.Add(i);
                    }
                }
                else if (int.TryParse(p, out int one))
                {
                    if (one >= 1 && one <= max) result.Add(one);
                }
            }
            return result.Distinct().OrderBy(x => x).ToList();
        }

        private int GetRotateAngle()
        {
            var sel = (RotateAngleBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
            return int.TryParse(sel, out int angle) ? angle : 0;
        }

        private static async Task RenderPdfFirstPageToPngAsync(string pdfPath, string pngPath, uint maxSide = 1600)
        {
            var sf = await StorageFile.GetFileFromPathAsync(pdfPath);
            var pdf = await PdfDocument.LoadFromFileAsync(sf);
            if (pdf.PageCount == 0) throw new InvalidOperationException("No pages.");
            using var page = pdf.GetPage(0);

            using var mem = new InMemoryRandomAccessStream();
            var opts = new PdfPageRenderOptions
            {
                DestinationWidth = maxSide,
                DestinationHeight = maxSide
            };
            await page.RenderToStreamAsync(mem, opts);
            mem.Seek(0);

            // Create/overwrite output PNG via path
            using var outStream = await FileRandomAccessStream.OpenAsync(
                pngPath,
                FileAccessMode.ReadWrite,
                StorageOpenOptions.None,
                FileOpenDisposition.CreateAlways);

            // Transcode to PNG
            var decoder = await BitmapDecoder.CreateAsync(mem);
            var encoder = await BitmapEncoder.CreateForTranscodingAsync(outStream, decoder);
            await encoder.FlushAsync();
        }

        private void AppTitleBar_BackRequested(TitleBar sender, object args)
        {
            AppTitleBar.IsBackButtonVisible = false;
            AppAboutPage.Visibility = Visibility.Collapsed;
            ContentGrid.Visibility = Visibility.Visible;
        }
    }
}