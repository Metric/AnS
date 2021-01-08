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
    public class DataSource
    {
        public const string DB_FILE = "{0}-{1}.kvdb";
        public const string DB_DIFF_FILE = "{0}-{1}.kvdb.diff";
        public const string DB_REGION_FILE = "{0}.kvdb";
        public const string DB_REGION_DIFF_FILE = "{0}.kvdb.diff";

        const string REG_HKEY = "HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Blizzard Entertainment\\World of Warcraft";
        const string REG_HKEY_VALUE = "InstallPath";

        const string WIN_INTERFACE_FOLDER = "Interface\\Addons";
        const string MAC_INTERFACE_FOLDER = "World of Warcraft/_retail_/Interface/Addons";

        public const string ADDON_NAME = "AnsAuctionData";

        const string US_GIST_URL = "https://raw.githubusercontent.com/Metric/AnSDataService/master/realms/us-connected.json";
        const string EU_GIST_URL = "https://raw.githubusercontent.com/Metric/AnSDataService/master/realms/eu-connected.json";
        const string DB_URL = "https://wow.us.auctions.arcanistry.com/full/{0}/connected/{1}";  //"http://localhost:3000/full/{0}/connected/{1}";
        const string DB_URL_MODIFIED = DB_URL + "/modified";
        const string DB_DIFF_URL = "http://localhost:3000/diff/{0}/connected/{1}/v/{2}";
        const string DB_DIFF_URL_MODIFIED = DB_DIFF_URL + "/modified";

        public const string REGION_KEY = "Region";

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
                    string data = GetDataFromUrl(US_GIST_URL);

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
                    string data = GetDataFromUrl(EU_GIST_URL);

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

        protected static string GetDataFromUrl(string url)
        {
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

                    response.Close();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }

            return data;
        }

        protected static byte[] GetBytesFromUrl(string url)
        {
            byte[] data = null;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (MemoryStream ms = new MemoryStream()) 
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

                            int len = 0;
                            byte[] buffer = new byte[2048 * 2048];
                            while (realStream.CanRead && (len = realStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                ms.Write(buffer, 0, len);
                            }
                            data = ms.ToArray();

                            realStream.Close();
                        }
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
        public static bool HasLocalData(int id, string region)
        {
            string fname = string.Format(id >= 0 ? DB_FILE : DB_REGION_FILE, region, id);
            string fpath = Path.Combine(DirectoryPath, fname);

            return File.Exists(fpath);
        }

        public static byte[] GetLocalData(int id, string region)
        {
            string fname = string.Format(id >= 0 ? DB_FILE : DB_REGION_FILE, region, id);
            string fpath = Path.Combine(DirectoryPath, fname);

            if (File.Exists(fpath))
            {
                try
                {
                    return File.ReadAllBytes(fpath);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                }
            }

            return null;
        }

        public static DateTime GetLocalDataModified(int id, string region)
        {
            string fname = string.Format(id >= 0 ? DB_FILE : DB_REGION_FILE, region, id);
            string fpath = Path.Combine(DirectoryPath, fname);

            if (File.Exists(fpath))
            {
                return File.GetLastWriteTimeUtc(fpath);
            }

            return DateTime.UtcNow.Subtract(new TimeSpan(0,1,0,0));
        }

        public static DateTime GetLocalDataDiffModified(int id, string region)
        {
            string fname = string.Format(id >= 0 ? DB_DIFF_FILE : DB_REGION_DIFF_FILE, region, id);
            string fpath = Path.Combine(DirectoryPath, fname);

            if (File.Exists(fpath))
            {
                return File.GetLastWriteTimeUtc(fpath);
            }

            return DateTime.UtcNow.Subtract(new TimeSpan(0, 1, 0, 0));
        }

        public static bool HasLocalDataDiff(int id, string region)
        {
            string fname = string.Format(id >= 0 ? DB_DIFF_FILE : DB_REGION_DIFF_FILE, region, id);
            string fpath = Path.Combine(DirectoryPath, fname);

            return File.Exists(fpath);
        }

        public static byte[] GetLocalDataDiff(int id, string region)
        {
            string fname = string.Format(id >= 0 ? DB_DIFF_FILE : DB_REGION_DIFF_FILE, region, id);
            string fpath = Path.Combine(DirectoryPath, fname);

            try
            {
                if (File.Exists(fpath))
                {
                    return File.ReadAllBytes(fpath);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }

            return null;
        }

        /// <summary>
        /// Do not run on main thread
        /// </summary>
        /// <param name="id"></param>
        /// <param name="region"></param>
        public static bool Merge(int id, string region)
        {
            if(!HasLocalData(id, region) || !HasLocalDataDiff(id, region))
            {
                return false;
            }

            byte[] dictData = GetLocalData(id, region);
            byte[] deltaData = GetLocalDataDiff(id, region);

            if (dictData == null || deltaData == null)
            {
                return false;
            }

            try
            {
                using (ByteBuffer dictBuffer = new ByteBuffer(dictData))
                using (ByteBuffer deltaBuffer = new ByteBuffer(deltaData))
                using (MemoryStream ms = new MemoryStream())
                {
                    VCDecoder decoder = new VCDecoder(dictBuffer, deltaBuffer, ms);
                    VCDiffResult result = decoder.Start();

                    if (result != VCDiffResult.SUCCESS)
                    {
                        return false;
                    }

                    long bytesWritten = 0;
                    result = decoder.Decode(out bytesWritten);

                    if (result != VCDiffResult.SUCCESS)
                    {
                        return false;
                    }

                    string fname = string.Format(id >= 0 ? DB_FILE : DB_REGION_FILE, region, id);
                    string fpath = Path.Combine(DirectoryPath, fname);
                    File.WriteAllBytes(fpath, ms.ToArray());
                    return true;
                }
            }
            catch (Exception e) 
            {
                Debug.WriteLine(e.ToString());
            }

            return false;
        }

        #endregion

        #region Server DB
        /// <summary>
        /// Do not on main thread
        /// </summary>
        /// <param name="id"></param>
        /// <param name="region"></param>
        /// <returns></returns>
        public static DateTime? GetServerDataDiffModified(int id, string region, int v)
        {
            string url = string.Format(DB_DIFF_URL_MODIFIED, region, id, v);
            string data = GetDataFromUrl(url);

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
        /// <param name="id"></param>
        /// <param name="region"></param>
        /// <returns></returns>
        public static DateTime? GetServerDataModified(int id, string region)
        {
            string url = string.Format(DB_URL_MODIFIED, region, id);
            string data = GetDataFromUrl(url);

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
        /// Do no use on main thread
        /// </summary>
        /// <param name="id"></param>
        /// <param name="region"></param>
        public static bool GetServerData(int id, string region)
        {
            string url = string.Format(DB_URL, region, id);
            string data = GetDataFromUrl(url);

            if (string.IsNullOrEmpty(data))
            {
                return false;
            }

            string fname = string.Format(id >= 0 ? DB_FILE : DB_REGION_FILE, region, id);
            string fpath = Path.Combine(DirectoryPath, fname);
            File.WriteAllText(fpath, data);
            return true;
        }

        /// <summary>
        /// Do not use on main thread
        /// </summary>
        /// <param name="id"></param>
        /// <param name="region"></param>
        /// <param name="v"></param>
        public static bool GetServerDataDiff(int id, string region, int v)
        {
            string url = string.Format(DB_DIFF_URL, region, id, v);
            byte[] data = GetBytesFromUrl(url);
            if (data == null)
            {
                return false;
            }

            string fname = string.Format(id >= 0 ? DB_DIFF_FILE : DB_REGION_DIFF_FILE, region, id);
            string fpath = Path.Combine(DirectoryPath, fname);
            File.WriteAllBytes(fpath, data);
            return true;
        }
        #endregion
    }
}
