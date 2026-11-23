using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace CursorSeam
{
    internal sealed class SettingsBackup
    {
        internal const string Extension = "cursorseam";
        private const string Root = "CursorSeamBackup";
        private const int FormatVersion = 1;
        internal readonly List<Display> Displays;
        internal readonly AppPreferences Preferences;

        internal SettingsBackup(List<Display> displays, AppPreferences preferences)
        {
            Displays = displays;
            Preferences = preferences;
        }

        internal void Export(string path)
        {
            CursorSeam.Preferences.SaveDocument(path, new XElement(Root, new XAttribute("version", FormatVersion),
                CursorSeam.Preferences.CreateLayout(Displays, Preferences)));
        }

        internal static SettingsBackup Read(string path, List<Display> displays)
        {
            var options = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 262144 };
            XElement root;
            using (var reader = XmlReader.Create(path, options)) root = XElement.Load(reader);
            XElement layout = root.Element("layout");
            if (root.Name != Root || (string)root.Attribute("version") != FormatVersion.ToString() || layout == null)
                throw new InvalidDataException();
            XElement settings = layout.Element("settings");
            if (settings == null) throw new InvalidDataException();
            string language = (string)settings.Attribute("language");
            if (language == null || (language.Length != 0 && !UiText.Languages.Any(item => item.Key == language)))
                throw new InvalidDataException();
            var matched = CursorSeam.Preferences.MatchLayout(layout, displays);
            if (matched == null) throw new InvalidDataException();
            return new SettingsBackup(matched, new AppPreferences
            {
                TrayResident = (bool)settings.Attribute("trayResident"),
                Language = language,
                CheckUpdates = (bool?)settings.Attribute("checkUpdates")
            });
        }
    }
}
