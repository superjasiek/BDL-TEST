using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BdlGusExporterWPF
{
    public class JsonCache
    {
        private readonly string _cacheDir;

        public JsonCache(string cacheDirectory = "api_cache")
        {
            _cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, cacheDirectory);
            if (!Directory.Exists(_cacheDir))
            {
                Directory.CreateDirectory(_cacheDir);
            }
        }

        private string GetCacheFilePath(string url)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(url));
                var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                return Path.Combine(_cacheDir, $"{hashString}.json");
            }
        }

        public bool TryGet(string url, out string json)
        {
            var filePath = GetCacheFilePath(url);
            if (File.Exists(filePath))
            {
                json = File.ReadAllText(filePath);
                return true;
            }

            json = null;
            return false;
        }

        public void Set(string url, string json)
        {
            var filePath = GetCacheFilePath(url);
            File.WriteAllText(filePath, json);
        }

        public void Clear()
        {
            var directory = new DirectoryInfo(_cacheDir);
            foreach (var file in directory.GetFiles())
            {
                file.Delete();
            }
        }
    }
}
