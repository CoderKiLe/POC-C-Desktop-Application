namespace C1Installer.Core.Constant
{
    /// <summary>
    /// Holds constants for JSON file names, directories, and cloud URLs.
    /// </summary>
    public static class JsonFileConstant
    {
        public const string LocalDirectory = @"C:\ComponentOne\";

        public const string RemoteRootBase =
           @"https://gcteststorage.blob.core.windows.net/c1installer/Mock/";

        //File names for Release Version and its Sha256 text
        public const string Sha256FileNameRelease = "sha256.txt";
        public const string ReleaseVersionFileName = "ReleaseVersion.json";

        //File names for year-wise release configuration and its Sha256 text
        public const string Sha256FileNameReleaseConfiguration = "sha256.txt";

        //App version Json file
        public const string AppVersionFileName = "AppVersion.json";
        



        //List of new resturcutred JSON file
        public const string ReleaseVersionFileNameUS = "ProductVersion.US.json";
        public const string ReleaseVersionFileNameJP = "ProductVersion.JP.json";
        public const string ReleaseVersionFileNameCN = "ProductVersion.CN.json";
        public const string ReleaseVersionFileNameKR = "ProductVersion.KR.json";

        //List of old JSON files
        public const string legacyVersionFileNameUS = "c1ControlPanelEN.json";
        public const string legacyVersionFileNameJP = "c1ControlPanelJP.json";
        public const string legacyVersionFileNameKR = "c1ControlPanelKR.json";



        public static string GetFileNameForLocale(string localeKey) => localeKey switch
        {
            "KR" => ReleaseVersionFileNameKR,
            "JP" => ReleaseVersionFileNameJP,
            "CN" => ReleaseVersionFileNameCN,
            _ => ReleaseVersionFileNameUS
        };

        public static string GetLegacyFileNameForLocale(string localeKey) => localeKey switch
        {
            "KR" => legacyVersionFileNameKR,
            "JP" => legacyVersionFileNameJP,
            _ => legacyVersionFileNameUS
        };


    }
}
