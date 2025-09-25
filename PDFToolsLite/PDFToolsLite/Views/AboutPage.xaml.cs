using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Services.Store;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace PDFToolsLite.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AboutPage : Page
    {
        public AboutPage()
        {
            InitializeComponent();
#if DEBUG
            SetAsSponsor();
            CheckingSponsorshipProgressRing.Visibility = Visibility.Collapsed;
            BecomeASponsor.Visibility = Visibility.Visible;
#endif
        }

        private bool _isSponsor = false;

        private async void BecomeASponsor_Click(object sender, RoutedEventArgs e)
        {
            if (_isSponsor)
            {
                DataPackage dp = new DataPackage();
                dp.SetText("I just sponsored PDF Tools Lite! A great free tool for merging and converting PDFs.");
                Clipboard.SetContent(dp);
                await Utils.ShowContentDialog("Copied", "The message has been copied. You can now share it with your network.");
            }
            else
            {
                StorePurchaseResult result = await PurchaseUtils.RequestSponsorshipPurchaseAsync();
                if (result.Status == StorePurchaseStatus.Succeeded)
                {
                    SetAsSponsor();
                }
                else if (result.Status == StorePurchaseStatus.NotPurchased)
                {

                }
                else
                {
                    await PurchaseUtils.SponsorshipErrorDialog();
                }
            }
        }
        private void SetAsSponsor()
        {
            _isSponsor = true;
            BecomeASponsor.Content = "Copy message";
            SponsorCard.Description = "You're already a sponsor. Thank you! You can share your sponsorship with your network by copying the message.";
        }
        public async void CheckSponsorshipAndSet()
        {
            if (await PurchaseUtils.CheckSponsorshipAsync())
            {
                SetAsSponsor();
            }
            CheckingSponsorshipProgressRing.Visibility = Visibility.Collapsed;
            BecomeASponsor.Visibility = Visibility.Visible;
        }
    }
}
