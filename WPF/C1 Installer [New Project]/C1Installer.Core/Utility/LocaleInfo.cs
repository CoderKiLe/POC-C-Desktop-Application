namespace C1Installer.Core.Utility
{
    public static class LocaleInfo
    {
        public static readonly string Key;

        static LocaleInfo()
        {
            #if LOCALE_KR
                Key = "KR";
            #elif LOCALE_JP
                Key = "JP";
            #elif LOCALE_CN
                Key = "CN";
            #else
                Key = "US";
            #endif
        }

        /// <summary>
        /// Returns the API base path or localized resource segment for the current build locale.
        /// </summary>
        public static string GetApiPrefix() => Key.ToLowerInvariant();

        /// <summary>
        /// Checks if the build was compiled for the given locale key (e.g., "KR", "US").
        /// </summary>
        public static bool Is(string localeKey) =>
            Key.Equals(localeKey, StringComparison.OrdinalIgnoreCase);
    }
}
