#if !STORE_BUILD
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Win32.SafeHandles;

namespace CursorSeam
{
    internal sealed class UpdateChecker : IDisposable
    {
        private const string LatestRelease = "https://api.github.com/repos/nekomimiyks/CursorSeam/releases/latest";
        private const string DownloadRoot = "https://github.com/nekomimiyks/CursorSeam/releases/download/";
        private const int MaxInstallerBytes = 32 * 1024 * 1024;
        private static readonly Version CurrentVersion = typeof(UpdateChecker).Assembly.GetName().Version;
        private readonly HttpClient client = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false, UseCookies = false, SslProtocols = SslProtocols.Tls12
        })
        {
            Timeout = TimeSpan.FromSeconds(12),
            MaxResponseContentBufferSize = 256 * 1024
        };

        internal UpdateChecker()
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CursorSeam/" + CurrentVersion.ToString(3));
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        }

        internal async Task<UpdateRelease> CheckAsync(CancellationToken token)
        {
            try
            {
                using (HttpResponseMessage response = await client.GetAsync(LatestRelease, token).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode) return null;
                    using (Stream body = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        return ReadUpdate(body, CurrentVersion);
                }
            }
            catch (HttpRequestException) { return null; }
            catch (OperationCanceledException) { return null; }
            catch (IOException) { return null; }
            catch (SerializationException) { return null; }
            catch (XmlException) { return null; }
            catch (ObjectDisposedException) { return null; }
        }

        internal static UpdateRelease ReadUpdate(Stream body, Version current)
        {
            var release = (Release)new DataContractJsonSerializer(typeof(Release)).ReadObject(body);
            if (release == null || release.Draft || release.Prerelease || string.IsNullOrEmpty(release.Tag) ||
                release.Tag[0] != 'v' || release.Assets == null) return null;

            string number = release.Tag.Substring(1);
            Version version;
            if (!Version.TryParse(number, out version) || version.Build < 0) return null;
            if (version.Revision < 0) version = new Version(version.Major, version.Minor, version.Build, 0);
            if (number != version.ToString(3) && number != version.ToString(4)) return null;
            string installer = "CursorSeam-" + number + "-Setup.exe";
            if (version <= current) return null;
            Asset match = release.Assets.FirstOrDefault(asset => asset != null && asset.Name == installer && asset.State == "uploaded" &&
                asset.Size > 0 && asset.Size <= MaxInstallerBytes && IsSha256(asset.Digest));
            return match == null ? null : new UpdateRelease(version, release.Tag, installer, match.Size, match.Digest.Substring(7));
        }

        private static bool IsSha256(string value)
        {
            return value != null && value.Length == 71 && value.StartsWith("sha256:", StringComparison.Ordinal) &&
                value.Skip(7).All(Uri.IsHexDigit);
        }

        internal async Task<string> DownloadAsync(UpdateRelease update, CancellationToken token)
        {
            string directory = Path.Combine(Path.GetTempPath(), "CursorSeam-Update-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, update.Installer);
            try
            {
                using (var response = await GetInstallerAsync(new Uri(DownloadRoot + update.Tag + "/" + update.Installer), token).ConfigureAwait(false))
                using (token.Register(response.Dispose))
                {
                    response.EnsureSuccessStatusCode();
                    if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value != update.Size)
                        throw new InvalidDataException();
                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
                    {
                        byte[] buffer = new byte[65536];
                        int total = 0, count;
                        while ((count = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) != 0)
                        {
                            total += count;
                            if (total > update.Size) throw new InvalidDataException();
                            await file.WriteAsync(buffer, 0, count, token).ConfigureAwait(false);
                        }
                        if (total != update.Size) throw new InvalidDataException();
                    }
                }
                token.ThrowIfCancellationRequested();
                if (!MatchesDigest(path, update.Digest)) throw new InvalidDataException();
                // Preserve Windows' normal security checks for an Internet download.
                MarkInternetDownload(path);
                return path;
            }
            catch
            {
                DeleteInstaller(path);
                throw;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security,
            uint creation, uint flags, IntPtr template);

        private static void MarkInternetDownload(string path)
        {
            const uint genericWrite = 0x40000000, createAlways = 2, normalFile = 0x80;
            using (var handle = CreateFile(path + ":Zone.Identifier", genericWrite, 0, IntPtr.Zero, createAlways, normalFile, IntPtr.Zero))
            {
                if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
                using (var stream = new FileStream(handle, FileAccess.Write))
                {
                    byte[] zone = Encoding.ASCII.GetBytes("[ZoneTransfer]\r\nZoneId=3\r\n");
                    stream.Write(zone, 0, zone.Length);
                }
            }
        }

        private async Task<HttpResponseMessage> GetInstallerAsync(Uri address, CancellationToken token)
        {
            for (int redirects = 0; redirects <= 3; redirects++)
            {
                if (address.Scheme != Uri.UriSchemeHttps || !address.IsDefaultPort || address.UserInfo.Length != 0 ||
                    (address.Host != "github.com" && address.Host != "release-assets.githubusercontent.com" &&
                     address.Host != "objects.githubusercontent.com" && address.Host != "github-releases.githubusercontent.com"))
                    throw new InvalidDataException();
                var result = await client.GetAsync(address, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                int status = (int)result.StatusCode;
                if (status != 301 && status != 302 && status != 303 && status != 307 && status != 308) return result;
                using (result)
                {
                    Uri next = result.Headers.Location;
                    if (next == null) throw new InvalidDataException();
                    address = new Uri(address, next);
                }
            }
            throw new InvalidDataException();
        }

        internal static bool MatchesDigest(string path, string expected)
        {
            using (var file = File.OpenRead(path))
            using (var sha256 = SHA256.Create())
                return string.Equals(BitConverter.ToString(sha256.ComputeHash(file)).Replace("-", ""), expected, StringComparison.OrdinalIgnoreCase);
        }

        internal static void DeleteInstaller(string path)
        {
            try
            {
                File.Delete(path);
                Directory.Delete(Path.GetDirectoryName(path));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        public void Dispose() { client.Dispose(); }

        [DataContract]
        private sealed class Release
        {
            [DataMember(Name = "tag_name")] public string Tag { get; set; }
            [DataMember(Name = "draft")] public bool Draft { get; set; }
            [DataMember(Name = "prerelease")] public bool Prerelease { get; set; }
            [DataMember(Name = "assets")] public Asset[] Assets { get; set; }
        }

        [DataContract]
        private sealed class Asset
        {
            [DataMember(Name = "name")] public string Name { get; set; }
            [DataMember(Name = "state")] public string State { get; set; }
            [DataMember(Name = "size")] public int Size { get; set; }
            [DataMember(Name = "digest")] public string Digest { get; set; }
        }
    }

    internal sealed class UpdateRelease
    {
        internal readonly Version Version;
        internal readonly string Tag, Installer, Digest;
        internal readonly int Size;

        internal UpdateRelease(Version version, string tag, string installer, int size, string digest)
        {
            Version = version;
            Tag = tag;
            Installer = installer;
            Size = size;
            Digest = digest;
        }
    }
}
#endif
