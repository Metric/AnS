using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace AnS.Data
{
    public class KVDB : IDisposable
    {
        static Regex BigIntTester = new Regex("\\d+n$", RegexOptions.IgnoreCase);

        protected string path;
        protected string name;
        public Dictionary<string, object> Cache { get; set; }

        public IEnumerable<string> Keys
        {
            get
            {
                return Cache.Keys;
            }
        }

        public KVDB(string fpath, string dbname)
        {
            name = dbname;
            path = fpath;
            Cache = new Dictionary<string, object>();
        }

        string GetWritableData(string k, object v)
        {
            int t = 0;
            string outvalue = "";
            if (v is List<long> || v is List<int> || v is List<double> || v is List<float> || v is List<object>) 
            {
                t = 2;
                outvalue = JsonConvert.SerializeObject(v).Base64();
            }
            else if (v is object)
            {
                t = 1;
                outvalue = JsonConvert.SerializeObject(v).Base64();
            }
            else if(v is long || v is float || v is double || v is int || v is short || v is byte || v is ushort || v is uint || v is ulong)
            {
                t = 0;
                outvalue = v.ToString();
            }
            else if(v is string)
            {
                t = 3;
                outvalue = v.ToString().Base64();
            }
            else if (v is bool)
            {
                bool b = (bool)v;
                outvalue = b ? "1" : "0";
            }

            return $"{k};{t};{outvalue}\r\n";
        }

        public object GetObject(string k)
        {
            object o = null;
            Cache.TryGetValue(k, out o);
            return o;
        }

        public List<object> GetList(string k)
        {
            object o = null;
            if (Cache.TryGetValue(k, out o))
            {
                if (o is List<object>)
                {
                    return (List<object>)o;
                }
            }
            return null;
        }

        public string GetString(string k)
        {
            object o = null;
            if (Cache.TryGetValue(k, out o))
            {
                if (o is string)
                {
                    return o.ToString();
                }
            }
            return null;
        }

        public double? GetNumber(string k)
        {
            object o = null;
            if (Cache.TryGetValue(k, out o))
            {
                if (o is double)
                {
                    return (double)o;
                }
            }
            return null;
        }

        object GetValueForType(string v, int t)
        {
            // json based object
            if (t == 1 || t == 2)
            {
                try
                {
                    return JsonConvert.DeserializeObject<List<object>>(v.Base64Decode().GetString());
                }
                catch (Exception e) { }
            }
            // number / bigint
            else if(t == 0)
            {
                double d = 0;
                double.TryParse(v, out d);
                return d;
            }
            // string
            else if(t == 3)
            {
                return v.Base64Decode().GetString();
            }
            // bool
            else if(t == 4)
            {
                int i = 0;
                int.TryParse(v, out i);

                return i == 1;
            }

            return null;
        }

        public void Load()
        {
            if (File.Exists(Path.Combine(path, name + ".kvdb")))
            {
                try
                {
                    string[] s = File.ReadAllLines(Path.Combine(path, name + ".kvdb"));
                    for (int i = 0; i < s.Length; ++i)
                    {
                        string l = s[i];
                        if (!string.IsNullOrEmpty(l) && l[0] != '!')
                        {
                            string[] split = l.Split(';');
                            int t = 0;
                            if (int.TryParse(split[1], out t))
                            {
                                Cache[split[0]] = GetValueForType(split[2], t);
                            }
                        }
                    }
                }
                catch { }
            }
        }

        public override string ToString()
        {
            string data = "";
            foreach(string k in Cache.Keys)
            {
                object v = Cache[k];
                string str = GetWritableData(k, v);
                data += str;
            }
            return data;
        }

        public void Close()
        {
            Cache.Clear();
        }

        public void Flush()
        {
            try
            {
                string data = ToString();
                File.WriteAllText(Path.Combine(path, name + ".kvdb"), data);
            }
            catch (Exception e) { }
        }

        public void Dispose()
        {
            Close();
        }

        ~KVDB()
        {  
            Close();
        }
    }
}
