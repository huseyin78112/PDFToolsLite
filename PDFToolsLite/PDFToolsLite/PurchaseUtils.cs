using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Services.Store;

namespace PDFToolsLite
{
    public static class PurchaseUtils
    {
        public static StoreContext PurchaseContext;
        public const string SponsorshipStoreID = "9N2QJSVCDBMB";
        public static async Task<StorePurchaseResult> RequestSponsorshipPurchaseAsync()
        {
            return await PurchaseContext.RequestPurchaseAsync(SponsorshipStoreID);
        }
        public static async Task SponsorshipErrorDialog()
        {
            await Utils.ShowContentDialog("Error", "An error occurred during the sponsorship purchase.");
        }
        public static async Task<bool> CheckSponsorshipAsync()
        {
            StoreAppLicense appLicense = await PurchaseContext.GetAppLicenseAsync();
            foreach (var addOnLicense in appLicense.AddOnLicenses)
            {
                StoreLicense license = addOnLicense.Value;
                if (license.SkuStoreId.ToUpperInvariant().StartsWith(SponsorshipStoreID))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
