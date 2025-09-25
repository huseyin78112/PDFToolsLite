using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PDFToolsLite
{
    public static class Utils
    {
        public static Window MainWindow;
        public static async Task<bool> ShowContentDialog(string title, string text)
        {
            if (MainWindow == null)
            {
                return false;
            }
            try
            {
                ContentDialog dialog = new ContentDialog();
                dialog.XamlRoot = MainWindow.Content.XamlRoot;
                dialog.Title = title;
                dialog.Content = text;
                dialog.PrimaryButtonText = "OK";
                dialog.DefaultButton = ContentDialogButton.Primary;
                await dialog.ShowAsync();
            }
            catch
            {
                return false;
            }
            return true;
        }
    }
}
