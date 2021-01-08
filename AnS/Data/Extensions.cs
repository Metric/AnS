using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace AnS.Data
{
    public static class StringExtension
    {
        public static string Base64(this string s)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            return Convert.ToBase64String(bytes);
        }

        public static byte[] Base64Decode(this string s)
        {
            return Convert.FromBase64String(s);
        }

        public static List<long> ToLongList(this string s, string sep = ",")
        {
            List<long> l = new List<long>();
            string[] split = s.Split(sep);

            foreach(string v in split)
            {
                long r = 0;
                long.TryParse(v, out r);
                l.Add(r);
            }

            return l;
        }
    }

    public static class DateTimeExtension
    {
        public static double UnixTimeStamp
        {
            get
            {
                return DateTime.UtcNow
                   .Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                   .TotalMilliseconds;
            }
        }
    }

    public static class ListExtension
    { 
        public static string Pack(this byte[] b)
        {
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(b);
            }

            string s = "";
            for (int i = 0; i < b.Length; ++i)
            {
                s += (char)b[i];
            }

            return s;
        }

        public static string Join(this byte[] b)
        {
            string s = "";
            string sep = "";

            for(int i = 0; i < b.Length; ++i)
            {
                s += sep + b[i];
                sep = ",";
            }

            return s;
        }

        public static string JoinNames(this List<Realm> l, string separator = ",")
        {
            string sep = "";
            StringBuilder builder = new StringBuilder();
            foreach (Realm i in l)
            {
                builder.Append(sep);
                builder.Append(i.name.Values.First());
                sep = separator;
            }

            return builder.ToString();
        }

        public static List<string> Names(this List<Realm> l)
        {
            List<string> names = new List<string>();
            foreach(Realm r in l)
            {
                names.Add(r.name.Values.First());
            }
            return names;
        }

        public static string Join(this List<int> l, string separator = ",")
        {
            string sep = "";
            StringBuilder builder = new StringBuilder();
            foreach(int i in l)
            {
                builder.Append(sep);
                builder.Append(i);
                sep = separator;
            }

            return builder.ToString();
        }

        public static string Join(this List<string> l, string separator = ",")
        {
            string sep = "";
            StringBuilder builder = new StringBuilder();
            foreach (string i in l)
            {
                builder.Append(sep);
                builder.Append(i);
                sep = separator;
            }

            return builder.ToString();
        }

        public static string Join(this List<long> l, string separator = ",")
        {
            StringBuilder builder = new StringBuilder();
            string sep = "";

            foreach (long v in l)
            {
                builder.Append(sep);
                builder.Append(v);
                sep = separator;
            }

            return builder.ToString();
        }

        public static string Join(this ushort[] l, string separator = ",")
        {
            StringBuilder builder = new StringBuilder();
            string sep = "";

            foreach(ushort u in l)
            {
                builder.Append(sep);
                builder.Append(u);
                sep = separator;
            }

            return builder.ToString();
        }

        public static string Join(this uint[] l, string separator = ",")
        {
            StringBuilder builder = new StringBuilder();
            string sep = "";

            foreach (uint u in l)
            {
                builder.Append(sep);
                builder.Append(u);
                sep = separator;
            }

            return builder.ToString();
        }

        public static long Reduce(this List<long> l)
        {
            long t = 0;
            foreach (long v in l)
            {
                t += v;
            }
            return t;
        }

        public static int Reduce(this List<int> l)
        {
            int t = 0;
            foreach(int v in l)
            {
                t += v;
            }
            return t;
        }
    }

    public static class ByteExtension
    {
        public static float GetFloat(this byte[] b)
        {
            return BitConverter.ToSingle(b);
        }

        public static ushort GetUShort(this byte[] b)
        {
            return BitConverter.ToUInt16(b);
        }

        public static uint GetUInt(this byte[] b)
        {
            return BitConverter.ToUInt32(b);
        }

        public static int GetInt(this byte[] b)
        {
            return BitConverter.ToInt32(b);
        }

        public static long GetLong(this byte[] b)
        {
            return BitConverter.ToInt64(b);
        }

        public static ulong GetULong(this byte[] b)
        {
            return BitConverter.ToUInt64(b);
        }

        public static string GetString(this byte[] b)
        {
            return Encoding.UTF8.GetString(b);
        }
    }

    public static class LongExtension
    {
        public static string Base64(this long l)
        {
            byte[] bytes = BitConverter.GetBytes(l);
            return Convert.ToBase64String(bytes);
        }

        public static long Reduce(this long[] l)
        {
            long t = 0;
            foreach (long v in l)
            {
                t += v;
            }
            return t;
        }
    }

    public static class IntExtension
    {
        public static string Base64(this int i)
        {
            byte[] bytes = BitConverter.GetBytes(i);
            return Convert.ToBase64String(bytes);
        }

        public static string ToBase93(this uint i)
        {
            return Base93.Encode(i);
        }
    }

    public static class FloatExtension
    {
        public static string Base64(this float f)
        {
            byte[] bytes = BitConverter.GetBytes(f);
            return Convert.ToBase64String(bytes);
        }
    }

    public static class DoubleExtension
    {
        public static long ToLong(this double f)
        {
            return (long)f;
        }
    }

    public static class ObjectExtension
    {
        public static double ToDouble(this object o)
        {
            if (o is double)
            {
                return (double)o;
            }

            return 0;
        }

        public static long ToLong(this object o)
        {
            if (o is long)
            {
                return (long)o;
            }
            else if(o is double)
            {
                return (long)(double)o;
            }

            return 0;
        }
    }
}
