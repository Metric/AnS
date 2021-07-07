using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;
using Newtonsoft.Json;
using VCDiff.Decoders;
using VCDiff.Shared;
using VCDiff.Includes;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.IO.Compression;
using System.Diagnostics;

namespace AnS.Data
{
    public enum AnsMode
    {
        Limited = 0,
        Full = 1,
    }

    public class DataSettings
    {
        public bool includeRegion = true;
        public AnsMode mode = AnsMode.Full;
    }

    public class DataSource
    {
        #region Windows REGISTRY
        const string REG_HKEY = "HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Blizzard Entertainment\\World of Warcraft";
        const string REG_HKEY_VALUE = "InstallPath";
        #endregion

        #region MacWinFolders
        const string WIN_INTERFACE_FOLDER = "Interface\\Addons";
        const string MAC_INTERFACE_FOLDER = "World of Warcraft/_retail_/Interface/Addons";
        #endregion

        public const string ADDON_NAME = "AnsAuctionData";

        const string US_GIST_URL = "https://raw.githubusercontent.com/Metric/AnSDataService/master/realms/us-connected.json";
        const string EU_GIST_URL = "https://raw.githubusercontent.com/Metric/AnSDataService/master/realms/eu-connected.json";

        #region LUA
        public const string LUA_FILE = "Data.lua";
        public const string LUA_CACHE_PATH = "cache";

#if DEBUG
        const string LUA_URL = "http://localhost:3000/lua/connected/{0}";
#else
        const string LUA_URL = "https://wow.us.auctions.arcanistry.com/lua/connected/{0}";
#endif
        const string LUA_URL_MODIFIED = LUA_URL + "/modified";
#endregion

#region RAW DB
        public const string DB_FILE = "{0}-{1}.kvdb";
        public const string DB_REGION_FILE = "{0}.kvdb";

#if DEBUG
        const string DB_URL = "http://localhost:3000/full/{0}/connected/{1}";
#else
        const string DB_URL = "https://wow.us.auctions.arcanistry.com/full/{0}/connected/{1}";  
#endif
        const string DB_URL_MODIFIED = DB_URL + "/modified";
#endregion

        public const string REGION_KEY = "Region";

        public delegate void DownloadProgress(float p);
        public static event DownloadProgress OnProgress;

        protected const string LENGTH_HEADER = "X-Expected-Length";

        public static string DirectoryPath
        {
            get
            {
                string directoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AnS");
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
                return directoryPath;
            }
        }

        public static string MacApplicationPath
        {
            get
            {
                string directoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
                return directoryPath;
            }
        }

        public static string SettingsPath
        {
            get
            {
                string directoryPath = DirectoryPath;
                string fpath = Path.Combine(directoryPath, "settings.json");
                return fpath;
            }
        }


        public static string WoWPath
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    try
                    {
                        string path = (string)Registry.GetValue(REG_HKEY, REG_HKEY_VALUE, null);

                        if (string.IsNullOrEmpty(path))
                        {
                            return null;
                        }

                        return Path.Combine(path, WIN_INTERFACE_FOLDER, ADDON_NAME);
                    }
                    catch (Exception e) 
                    {
                        Debug.WriteLine(e.ToString());
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    string path = MacApplicationPath;
                    return Path.Combine(path, MAC_INTERFACE_FOLDER, ADDON_NAME);
                }

                return null;
            }
        }

        public static string CachePath
        {
            get
            {
                return Path.Combine(DirectoryPath, LUA_CACHE_PATH);
            }
        }

        public static string DataLuaPath
        {
            get
            {
                return Path.Combine(WoWPath, LUA_FILE);
            }
        }

        public static int CurrentDiffVersion
        {
            get
            {
                return DateTime.UtcNow.Hour;
            }
        }

        protected static string SelectedFilePath
        {
            get
            {
                string directoryPath = DirectoryPath;
                string fpath = Path.Combine(directoryPath, "selected-realms.json");
                return fpath;
            }
        }

        /// <summary>
        /// Do not use on main thread
        /// Also cache response afterwards
        /// </summary>
        /// <returns></returns>
        public static List<ConnectedRealm> US
        {
            get
            {
                try
                {
                    bool queued = false;
                    string data = GetDataFromUrl(US_GIST_URL, out queued);

                    if (!string.IsNullOrEmpty(data))
                    {
                        return JsonConvert.DeserializeObject<List<ConnectedRealm>>(data);
                    }
                }
                catch (Exception e) 
                {
                    Debug.WriteLine(e.ToString());
                }

                return new List<ConnectedRealm>();
            }
        }


        /// <summary>
        /// Do not use on main thread
        /// Also cache response afterwards
        /// </summary>
        /// <returns></returns>
        public static List<ConnectedRealm> EU
        {
            get
            {
                try
                {
                    bool queued = false;
                    string data = GetDataFromUrl(EU_GIST_URL, out queued);

                    if (!string.IsNullOrEmpty(data))
                    {
                        return JsonConvert.DeserializeObject<List<ConnectedRealm>>(data);
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                }

                return new List<ConnectedRealm>();
            }
        }

        protected static string GetDataFromUrl(string url, out bool queued)
        {
            queued = false;
            string data = null;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        using (Stream dataStream = response.GetResponseStream())
                        { 
                            Stream realStream = dataStream;
                            if (!string.IsNullOrEmpty(response.ContentEncoding))
                            {
                                if (response.ContentEncoding.ToLower().Contains("gzip"))
                                    realStream = new GZipStream(dataStream, CompressionMode.Decompress);
                                else if (response.ContentEncoding.ToLower().Contains("deflate"))
                                    realStream = new DeflateStream(dataStream, CompressionMode.Decompress);
                            }

                            using (StreamReader reader = new StreamReader(realStream))
                            {
                                data = reader.ReadToEnd();
                            }

                            realStream.Close();
                        }
                    }
                    else if(response.StatusCode == HttpStatusCode.Accepted)
                    {
                        queued = true;
                    }

                    response.Close();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }

            return data;
        }

        protected static bool GetBytesFromUrl(string url, out bool queued, FileStream stream)
        {
            bool success = false;
            queued = false;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                { 
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        using (Stream dataStream = response.GetResponseStream())
                        {
                            Stream realStream = dataStream;

                            long totalBytes = 0;

                            string expectedLength = response.Headers.Get(LENGTH_HEADER);
                            if (string.IsNullOrEmpty(expectedLength))
                            {
                                expectedLength = response.Headers.Get(LENGTH_HEADER.ToLower());
                            }

                            if (!string.IsNullOrEmpty(expectedLength))
                            {
                                long.TryParse(expectedLength, out totalBytes);
                            }

                            if (!string.IsNullOrEmpty(response.ContentEncoding))
                            {
                                if (response.ContentEncoding.ToLower().Contains("gzip"))
                                    realStream = new GZipStream(dataStream, CompressionMode.Decompress);
                                else if (response.ContentEncoding.ToLower().Contains("deflate"))
                                    realStream = new DeflateStream(dataStream, CompressionMode.Decompress);
                            }

                            int len = 0;
                            long read = 0;
                            byte[] buffer = new byte[2048 * 2048];

                            while (realStream.CanRead && (len = realStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                read += len;
                                stream.Write(buffer, 0, len);
                                if (totalBytes > 0)
                                {
                                    OnProgress?.Invoke((float)read / (float)totalBytes);
                                }
                            }

                            realStream.Close();

                            //esnure proper length is set
                            //for the underlying file
                            //thus truncating it
                            stream.SetLength(stream.Position);
                            stream.Close();

                            success = true;
                        }
                    }
                    else if (response.StatusCode == HttpStatusCode.Accepted)
                    {
                        queued = true;
                    }

                    response.Close();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }

            return success;
        }

        protected static DataSettings settings;
        public static DataSettings Settings
        {
            get
            {
                if (settings == null)
                {
                    string fpath = SettingsPath;

                    try
                    {
                        if (File.Exists(fpath))
                        {
                            string data = File.ReadAllText(fpath);
                            settings = JsonConvert.DeserializeObject<DataSettings>(data);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.ToString());
                    }
                }

                if (settings == null)
                {
                    settings = new DataSettings();
                }

                return settings;
            }
        }

        public static Dictionary<string, List<ConnectedRealm>> Selected
        {
            get
            {
                string fpath = SelectedFilePath;

                try
                {
                    if (File.Exists(fpath))
                    {
                        string data = File.ReadAllText(fpath);
                        return JsonConvert.DeserializeObject<Dictionary<string, List<ConnectedRealm>>>(data);
                    }
                }
                catch (Exception e) 
                {
                    Debug.WriteLine(e.ToString());
                }

                return new Dictionary<string, List<ConnectedRealm>>();
            }
        }

        public static void Store(DataSettings settings)
        {
            string fpath = SettingsPath;
            try
            {
                File.WriteAllText(fpath, JsonConvert.SerializeObject(settings));
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }
        }

        public static void Store(Dictionary<string, List<ConnectedRealm>> realms)
        {
            string fpath = SelectedFilePath;

            try
            {
                File.WriteAllText(fpath, JsonConvert.SerializeObject(realms));
            }
            catch (Exception e) 
            {
                Debug.WriteLine(e.ToString());
            }
        }

#region Local DB
        public static DateTime? GetLocalDataModified(int id, string region)
        {
            string fname = string.Format(id >= 0 ? DB_FILE : DB_REGION_FILE, region, id);
            string fpath = Path.Combine(CachePath, fname);

            if (File.Exists(fpath))
            {
                return File.GetLastWriteTimeUtc(fpath);
            }

            return null;
        }

        public static DateTime? GetLocalDataModified(params int[] id)
        {
            Array.Sort(id);

            bool regionIncluded = false;
            uint[] transformed = new uint[id.Length];
            for (int i = 0; i < id.Length; ++i)
            {
                if (id[i] < 0)
                {
                    regionIncluded = true;
                    continue;
                }

                transformed[i] = (uint)id[i];
            }

            string fname = regionIncluded ? "-" + transformed.VariableHexHash() :  transformed.VariableHexHash();
            string fpath = Path.Combine(CachePath, fname);

            if (File.Exists(fpath))
            {
                return File.GetLastWriteTimeUtc(fpath);
            }

            return null;
        }
#endregion

#region Server DB
        public static DateTime? GetServerDataModified(int id, string region)
        {
            string url = string.Format(DB_URL_MODIFIED, region, id);
            string data = GetDataFromUrl(url, out _);

            if (!string.IsNullOrEmpty(data))
            {
                double t = -1;
                if (double.TryParse(data, out t))
                {
                    //this is already utc date time
                    return new DateTime(1970, 1, 1).AddTicks((long)t * 10000);
                }
            }

            return null;
        }

        /// <summary>
        /// Do not run on main thread
        /// </summary>
        /// <returns></returns>
        public static DateTime? GetServerDataModified(params int[] id)
        {
            Array.Sort(id);

            string url = string.Format(LUA_URL_MODIFIED, id.Join(","));
            string data = GetDataFromUrl(url, out _);

            if (!string.IsNullOrEmpty(data))
            {
                double t = -1;
                if (double.TryParse(data, out t))
                {
                    //this is already utc date time
                    return new DateTime(1970, 1, 1).AddTicks((long)t * 10000);
                }
            }

            return null;
        }

        public static bool GetServerData(int id, string region)
        {
            string url = string.Format(DB_URL, region, id);

            string fname = string.Format(id >= 0 ? DB_FILE : DB_REGION_FILE, region, id);
            string cachePath = CachePath;

            if (!Directory.Exists(cachePath))
            {
                Directory.CreateDirectory(cachePath);
            }

            string fpath = Path.Combine(cachePath, fname);
            
            bool success = false;

            try
            {
                //ReadWrite is required as access type
                //Otherwise the contents of the cache will be deleted when it shouldn't
                //In case the server returns status 202,403,404,500 etc
                using (FileStream fstream = new FileStream(fpath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    success = GetBytesFromUrl(url, out _, fstream);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.StackTrace.ToString());
            }

            return success;
        }

        /// <summary>
        /// Do no use on main thread
        /// </summary>
        /// <param name="id"></param>
        /// <param name="region"></param>
        public static bool GetServerData(out bool queued, params int[] id)
        {
            Array.Sort(id);

            queued = false;

            string ids = id.Join(",");
            string url = string.Format(LUA_URL, ids);
            string basePath = CachePath;

            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            bool regionIncluded = false;
            uint[] transformed = new uint[id.Length];
            for (int i = 0; i < id.Length; ++i)
            {
                if (id[i] < 0)
                {
                    regionIncluded = true;
                    continue;
                }

                transformed[i] = (uint)id[i];
            }

            string fname = regionIncluded ? "-" + transformed.VariableHexHash() : transformed.VariableHexHash();

            string cachePath = Path.Combine(basePath, fname);
            bool success = false;

            try
            {
                //ReadWrite is required as access type
                //Otherwise the contents of the cache will be deleted when it shouldn't
                //In case the server returns status 202,403,404,500 etc
                using (FileStream fstream = new FileStream(cachePath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    success = GetBytesFromUrl(url, out queued, fstream);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.StackTrace.ToString());
            }

            if ((success && File.Exists(cachePath)) 
                || (!queued && !success && File.Exists(cachePath)))
            {
                try
                {
                    string fpath = DataLuaPath;
                    File.Copy(cachePath, fpath, true);

                    //this is for the second or case
                    //where in we have already got the latest lua
                    //previously but we are switching back to the local
                    //cache version from another lua
                    //so we still want to signal success
                    //as we restored the cached version
                    success = true;
                }
                catch (Exception e)
                {
                    success = false;
                    Debug.WriteLine(e.StackTrace.ToString());
                }
            }

            return success;
        }
#endregion
    }
}
