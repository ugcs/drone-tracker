using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UGCS.Sdk.Protocol;

namespace ugcs_at.UGCS
{
    public static class LicenseHelper
    {
        private const string BuyPattern = @"(buy=(?<limitation>[^\,\']+))";
        private const string VersionPattern = @"(version \<b\>(?<version>[^\,\'\<]+)\</b\>)";
        private const int LICENSE_CONSTRAINT_ERROR = 1001;

        public static string GetLicenseLimitation(string messageText)
        {
            var match = Regex.Match(messageText, BuyPattern);
            if (!match.Success) return string.Empty;
            var limitation = match.Groups["limitation"].Value;
            return limitation;
        }

        public static string GetVersionName(string messageText)
        {
            var match = Regex.Match(messageText, VersionPattern);
            return match.Success ? match.Groups["version"].Value : string.Empty;
        }

        public static bool IsLicenseError(Exception ex)
        {
            return ex is ServerException serverException && 
                   serverException.Error != null && 
                   serverException.Error.ErrorCode == LICENSE_CONSTRAINT_ERROR;
        }
    }


}
