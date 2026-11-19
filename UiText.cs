using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;

[assembly: NeutralResourcesLanguage("ja")]

namespace CursorSeam
{
    internal static class UiText
    {
        private static readonly ResourceManager Strings = new ResourceManager("CursorSeam.UiStrings", typeof(UiText).Assembly);
        private static readonly CultureInfo WindowsCulture = CultureInfo.CurrentUICulture;
        private static readonly Dictionary<string, ResourceSet> Sets = new Dictionary<string, ResourceSet>();
        internal static readonly KeyValuePair<string, string>[] Languages = {
            new KeyValuePair<string, string>("ja", "日本語"), new KeyValuePair<string, string>("en", "English"),
            new KeyValuePair<string, string>("ko", "한국어"), new KeyValuePair<string, string>("zh-Hans", "简体中文"),
            new KeyValuePair<string, string>("zh-Hant", "繁體中文"), new KeyValuePair<string, string>("de", "Deutsch"),
            new KeyValuePair<string, string>("fr", "Français"), new KeyValuePair<string, string>("es", "Español"),
            new KeyValuePair<string, string>("pt-BR", "Português (Brasil)")
        };
        private static string active = Resolve(WindowsCulture);
        internal static string Preference { get; private set; }

        internal static void SelectLanguage(string preferred)
        {
            Preference = Languages.FirstOrDefault(item => string.Equals(item.Key, preferred, StringComparison.OrdinalIgnoreCase)).Key ?? "";
            active = Preference.Length == 0 ? Resolve(WindowsCulture) : Preference;
        }

        internal static string Resolve(CultureInfo culture)
        {
            for (CultureInfo current = culture; !string.IsNullOrEmpty(current.Name); current = current.Parent)
            {
                string supported = Languages.FirstOrDefault(item => string.Equals(item.Key, current.Name, StringComparison.OrdinalIgnoreCase)).Key;
                if (supported != null) return supported;
            }
            if (culture.TwoLetterISOLanguageName == "pt") return "pt-BR";
            if (culture.TwoLetterISOLanguageName == "zh") return "zh-Hans";
            return "en";
        }

        internal static string Get(string key)
        {
            if (active != "ja")
            {
                ResourceSet set;
                if (!Sets.TryGetValue(active, out set))
                {
                    using (var stream = typeof(UiText).Assembly.GetManifestResourceStream("CursorSeam.UiStrings." + active + ".resources"))
                        set = stream == null ? null : new ResourceSet(stream);
                    Sets.Add(active, set);
                }
                string value = set == null ? null : set.GetString(key);
                if (value != null) return value;
            }
            return Strings.GetString(key, CultureInfo.GetCultureInfo("ja"));
        }

        internal static string Format(string key, params object[] values)
        {
            return string.Format(CultureInfo.CurrentCulture, Get(key), values);
        }
    }
}
