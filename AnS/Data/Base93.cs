using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AnS.Data
{
    public class Base93
    {
        private const string CHARS = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ !?\"'`^#$%@&*+=/.:;|\\_<>[]{}()~";

        public static string Encode(uint num)
        {
            string result = "";
            int len = CHARS.Length;
            int index = 0;
            while (num >= Math.Pow(len, index))
            {
                index++;
            }
            index--;
            while (index >= 0)
            {
                uint pow = (uint)Math.Pow(len, index);
                uint div = num / pow;
                result += CHARS[(int)div];
                num -= pow * div;
                index--;
            }
            return result+"-";
        }

        public static uint Decode(string s, out string remainder)
        {
            uint result = 0;
            int len = CHARS.Length;
            int index = 0;
            var chars = s.Split('-', StringSplitOptions.RemoveEmptyEntries)[0].ToCharArray();
            var reversed = chars.Reverse();
            foreach (char c in reversed)
            {
                uint pow = (uint)Math.Pow(len, index);
                uint ind = (uint)CHARS.IndexOf(c);
                result += pow * ind;
                index++;
            }
            remainder = s.Substring(index + 1);
            return result;
        }
    }
}
