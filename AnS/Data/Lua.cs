using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using VCDiff.Shared;

namespace AnS.Data
{ 
    public class Lua
    {
        protected class ItemGroup
        {
            public ConnectedRealm realm;
            public uint[] data;
            public int index;

            public ItemGroup(ConnectedRealm r, uint[] d, int idx)
            {
                data = d;
                realm = r;
                index = idx;
            }
        }

        protected static string WrapLuaBinary(string s)
        {
            if (s.Contains("===]") || s.Contains("[===") || s.Contains("]===") || s.Contains("===["))
            {
                return "[====[" + s + "]====]";
            }
            else if (s.Contains("==]") || s.Contains("[==") || s.Contains("]==") || s.Contains("==["))
            {
                return "[===[" + s + "]===]";
            }
            else if (s.Contains("=]") || s.Contains("[=") || s.Contains("]=") || s.Contains("=["))
            {
                return "[==[" + s + "]==]";
            }
            else if(s.Contains("]") || s.Contains("["))
            {
                return "[=[" + s + "]=]";
            }

            return "[[" + s + "]]";
        }

        protected static uint[] UnpackRegionValues(string v)
        {
            uint[] values = new uint[2];
            StringUnpacker buffer = new StringUnpacker(v);

            for (int i = 0; i < 2 && buffer.CanRead; ++i)
            {
                values[i] = buffer.NextUInt();
            }

            return values;
        }

        protected static uint[] UnpackValues(string v)
        {
            uint[] values = new uint[4];
            StringUnpacker buffer = new StringUnpacker(v);

            buffer.Position += 8; //skip sum
            buffer.Position += 4; //skip num
            buffer.Position += 4 * 7; //skip avgs
            buffer.Position += 2; //skip day and day index

            if (!buffer.CanRead)
            {
                return values;
            }

            for (int i = 0; i < 4 && buffer.CanRead; ++i)
            {
                values[i] = buffer.NextUInt();
            }

            return values;
        }

        protected static string UnpackKey(string k)
        {
            string data = k.Substring(1).Base64Decode().GetString();
            StringUnpacker buffer = new StringUnpacker(data);
            uint id = buffer.NextUInt();
            if (buffer.CanRead)
            {
                // pet based
                if (k[0] == 'p')
                {
                    byte plevel = buffer.NextByte();
                    if (buffer.CanRead)
                    {
                        byte pquality = buffer.NextByte();
                        return $"p:{id}:{plevel}:{pquality}";
                    }
                    else
                    {
                        return $"p:{id}:{plevel}";
                    }
                }
                // regulare item
                else
                {
                    byte bcount = buffer.NextByte();
                    ushort[] bonuses = new ushort[bcount];

                    if (bcount > 0)
                    {
                        for (byte i = 0; i < bcount && buffer.CanRead; ++i)
                        {
                            bonuses[i] = buffer.NextUShort();
                        }

                        if (!buffer.CanRead)
                        {
                            return $"i:{id}::{bcount}:{bonuses.Join(":")}";
                        }
                    }

                    if (!buffer.CanRead)
                    {
                        return $"i:{id}";
                    }

                    int mcount = buffer.NextByte() * 2;
                    uint[] mods = new uint[mcount];
                    for(int i = 0; i < mcount && buffer.CanRead; i+=2)
                    {
                        mods[i] = buffer.NextByte();
                        mods[i + 1] = buffer.NextUInt();
                    }

                    if (bcount > 0)
                    {
                        return $"i:{id}::{bcount}:{bonuses.Join(":")}:{(mcount / 2)}:{mods.Join(":")}";
                    }
                    else
                    {
                        return $"i:{id}:::{(mcount / 2)}:{mods.Join(":")}";
                    }
                }
            }

            if (k[0] == 'p')
            {
                return $"p:{id}";
            }
            else
            {
                return $"i:{id}";
            }
        }

        /// <summary>
        /// Do not run on main thread
        /// </summary>
        /// <param name="realms"></param>
        /// <param name="region"></param>
        /// <returns></returns>
        public static void Build(Dictionary<string, List<ConnectedRealm>> selected, FileStream stream, bool includeRegion = true)
        {
            const int MAX_GROUP_SIZE = 20000;

            using (StreamWriter writer = new StreamWriter(stream))
            {
                string directoryPath = DataSource.DirectoryPath;

                writer.WriteLine("local Ans = select(2, ...);");
                writer.WriteLine("local R = {};");
                writer.WriteLine("Ans.Realms = R;");
                writer.WriteLine("R.F = function(region, rid)");
                writer.WriteLine("\tlocal t = {};");
                writer.WriteLine("\tlocal s = {};");
                writer.WriteLine("\tlocal sd = {};");

                foreach (string region in selected.Keys)
                {
                    List<ConnectedRealm> realms = selected[region];

                    string regID = "\"" + region.ToUpper() + "\"";
                    writer.WriteLine("\ts[" + regID + "] = {};");

                    Dictionary<string, List<ItemGroup>> groups = new Dictionary<string, List<ItemGroup>>();

                    KVDB regionDB = new KVDB(directoryPath, region);
                    if (includeRegion)
                    {
                        regionDB.Load();
                    }

                    for (int i = 0; i < realms.Count; ++i)
                    {
                        ConnectedRealm r = realms[i];
                        // skip selected region dbs
                        // since that is already handled above
                        if (region.Equals(r.realms.JoinNames()) || r.id < 0)
                        {
                            continue;
                        }

                        KVDB realm = new KVDB(directoryPath, region + "-" + r.id);
                        realm.Load();

                        foreach(Realm rr in r.realms)
                        {
                            List<string> names = rr.name.Values.Distinct().ToList();
                            foreach(string n in names)
                            {
                                writer.WriteLine("\tt[\"" + region.ToUpper() + "-" + n + "\"] = " + (i + 1) + ";");
                            }
                        }

                        foreach(string k in realm.Keys)
                        {
                            if (string.IsNullOrEmpty(k))
                            {
                                continue;
                            }

                            string d = realm.GetString(k);

                            if (string.IsNullOrEmpty(d))
                            {
                                continue;
                            }

                            List<ItemGroup> group = null;
                            groups.TryGetValue(k, out group);
                            group ??= new List<ItemGroup>();
                            groups[k] = group;
                            group.Add(new ItemGroup(r, UnpackValues(d), i));
                        }

                        realm.Close();
                    }

                    if (includeRegion && regionDB.Keys.Count() > 0)
                    {
                        int tcount = 1;
                        int offset = 0;

                        Dictionary<string, Dictionary<string, List<ItemGroup>>> all = new Dictionary<string, Dictionary<string, List<ItemGroup>>>();

                        Debug.WriteLine(regionDB.Keys.Count());

                        foreach(string k in regionDB.Keys)
                        {
                            Dictionary<string, List<ItemGroup>> subgroups = null;
                            string rkey = UnpackKey(k);
                            string[] split = rkey.Split(":");
                            string subkey = $"{split[0]}:{split[1]}";
                            all.TryGetValue(subkey, out subgroups);
                            subgroups ??= new Dictionary<string, List<ItemGroup>>();
                            all[subkey] = subgroups;

                            List<ItemGroup> group = null;
                            groups.TryGetValue(k, out group);
                            subgroups[k] = group;
                        }

                        Debug.WriteLine(all.Count);

                        foreach (string sk in all.Keys)
                        {
                            List<ItemGroup> g = null;

                            if (offset % MAX_GROUP_SIZE == 0)
                            {
                                writer.WriteLine($"\tsd[{tcount++}] = function()");
                            }

                            string zero = ((uint)0).ToBase93();

                            List<string> results = new List<string>();

                            for (int i = 0; i < realms.Count; ++i)
                            {
                                string sep = "";
                                string packData = "";

                                foreach (string k in all[sk].Keys)
                                {
                                    g = all[sk][k];

                                    string regionData = "";
                                    string regionValue = regionDB.GetString(k);

                                    if (!string.IsNullOrEmpty(regionValue))
                                    {
                                        uint[] values = UnpackRegionValues(regionValue);
                                        regionData = values[0].ToBase93() + values[1].ToBase93();
                                    }
                                    else
                                    {
                                        regionData = zero + zero;
                                    }

                                    var r = realms[i];
                                    var rg = g != null ? g.Find(m => m.realm.id == r.id) : null;

                                    if (rg != null)
                                    {
                                        var rdat = rg.data;
                                        var dat = rdat[1].ToBase93() + rdat[2].ToBase93() + rdat[3].ToBase93() + rdat[0].ToBase93() + regionData;
                                        packData += sep + UnpackKey(k) + "," + dat;
                                    }
                                    else
                                    {
                                        var dat = zero + zero + zero + zero + regionData;
                                        packData += sep + UnpackKey(k) + "," + dat;
                                    }

                                    sep = ",";
                                }

                                results.Add(WrapLuaBinary(packData));
                            }

                            writer.WriteLine($"\t\ts[{regID}][\"{sk}\"] = select(t[rid], {results.Join(",")});");
                            ++offset;

                            if (offset % MAX_GROUP_SIZE == 0)
                            {
                                writer.WriteLine("\tend;");
                            }
                        }

                        if (offset % MAX_GROUP_SIZE != 0)
                        {
                            writer.WriteLine("\tend;");
                        }
                    }
                    else
                    {
                        int tcount = 1;
                        int offset = 0;

                        Dictionary<string, Dictionary<string, List<ItemGroup>>> all = new Dictionary<string, Dictionary<string, List<ItemGroup>>>();

                        foreach (string k in groups.Keys)
                        {
                            Dictionary<string, List<ItemGroup>> subgroups = null;
                            string rkey = UnpackKey(k);
                            string[] split = rkey.Split(":");
                            string subkey = $"{split[0]}:{split[1]}";
                            all.TryGetValue(subkey, out subgroups);
                            subgroups ??= new Dictionary<string, List<ItemGroup>>();
                            all[subkey] = subgroups;
                            subgroups[rkey] = groups[k];
                        }

                        foreach(string sk in all.Keys)
                        {
                            List<ItemGroup> g = null;

                            if (offset % 20000 == 0)
                            {
                                writer.WriteLine($"\tsd[{tcount++}] = function()");
                            }

                            string zero = ((uint)0).ToBase93();
                            string regionData = zero + zero;

                            List<string> results = new List<string>();

                            for (int i = 0; i < realms.Count; ++i)
                            {
                                string sep = "";
                                string packData = "";

                                foreach (string k in all[sk].Keys)
                                {
                                    g = all[sk][k];

                                    var r = realms[i];
                                    var rg = g != null ? g.Find(m => m.realm.id == r.id) : null;

                                    if (rg != null)
                                    {
                                        var rdat = rg.data;
                                        var dat = rdat[1].ToBase93() + rdat[2].ToBase93() + rdat[3].ToBase93() + rdat[0].ToBase93() + regionData;
                                        packData += sep + k + "," + dat;
                                    }
                                    else
                                    {
                                        var dat = zero + zero + zero + zero + regionData;
                                        packData += sep + k + "," + dat;
                                    }

                                    sep = ",";
                                }

                                results.Add(WrapLuaBinary(packData));
                            }

                            writer.WriteLine($"\t\ts[{regID}][\"{sk}\"] = select(t[rid], {results.Join(",")});");

                            ++offset;

                            if (offset % MAX_GROUP_SIZE == 0)
                            {
                                writer.WriteLine("\tend;");
                            }
                        }

                        if (offset % MAX_GROUP_SIZE != 0)
                        {
                            writer.WriteLine("\tend;");
                        }
                    }

                    regionDB.Close();
                }

                writer.WriteLine("if (t[rid]) then");
                writer.WriteLine("\tfor i = 1, #sd do");
                writer.WriteLine("\t\tsd[i]();");
                writer.WriteLine("\tend");
                writer.WriteLine("end");

                writer.WriteLine("wipe(sd)");

                writer.WriteLine("R[rid] = s[region] or {}");
                writer.WriteLine("end;");
                writer.Flush();
            }
        }

        /// <summary>
        /// Do not run on main thread
        /// </summary>
        /// <param name="realm"></param>
        /// <param name="region"></param>
        /// <returns></returns>
        /*public static bool Format(KVDB realm, KVDB region, StreamWriter writer)
        {
            Dictionary<string, Dictionary<string, string>> subs = new Dictionary<string, Dictionary<string, string>>();

            int i = 0;
            foreach (string k in region.Keys)
            {
                string[] split = k.Split(':');
                if (split.Length >= 2)
                {
                    string baseId = split[1];
                    string t = split[0];
                    string bkey = t + ":" + baseId;

                    Dictionary<string, string> s = null;
                    subs.TryGetValue(bkey, out s);
                    s = s ?? new Dictionary<string, string>();
                    subs[bkey] = s;

                    List<object> rdat = realm.GetList(k);
                    List<object> regdat = region.GetList(k);

                    if (rdat != null && rdat.Count >= 4 && regdat != null && regdat.Count >= 6)
                    {
                        s[k] = rdat[0].ToLong() + "," + rdat[1].ToLong() + "," + rdat[2].ToLong() + "," + rdat[3].ToLong() + "," + regdat[0].ToLong() + "," + regdat[1].ToLong() + "," + regdat[2].ToLong() + "," + regdat[3].ToLong() + "," + regdat[5].ToLong();
                        ++i;
                    }
                    else if(regdat != null && regdat.Count >= 6)
                    {
                        s[k] = "0,0,0,0," + regdat[0].ToLong() + "," + regdat[1].ToLong() + "," + regdat[2].ToLong() + "," + regdat[3].ToLong() + "," + regdat[5].ToLong();
                        ++i;
                    }
                    else if(rdat != null && rdat.Count >= 4)
                    {
                        s[k] = rdat[0].ToLong() + "," + rdat[1].ToLong() + "," + rdat[2].ToLong() + "," + rdat[3].ToLong() + ",0,0,0,0,0";
                        ++i;
                    }
                }
            }

            // no region data so try only realm data
            if (i == 0)
            {
                foreach(string k in realm.Keys)
                {
                    string[] split = k.Split(':');
                    if (split.Length >= 2)
                    {
                        string baseId = split[1];
                        string t = split[0];
                        string bkey = t + ":" + baseId;

                        Dictionary<string, string> s = null;
                        subs.TryGetValue(bkey, out s);
                        s = s ?? new Dictionary<string, string>();
                        subs[bkey] = s;

                        List<object> rdat = realm.GetList(k);

                        if (rdat != null && rdat.Count >= 4)
                        {
                            s[k] = rdat[0].ToLong() + "," + rdat[1].ToLong() + "," + rdat[2].ToLong() + "," + rdat[3].ToLong() + ",0,0,0,0,0";
                            ++i;
                        }
                    }
                }
            }

            //no data whatsoever so return false
            if (i == 0)
            {
                return false;
            }

            writer.WriteLine("{");
            writer.Write("[\"rawdata\"] = \"");
            string sep = "";
            foreach(string n in subs.Keys)
            {
                Dictionary<string, string> rs = subs[n];

                if (!string.IsNullOrEmpty(sep))
                {
                    writer.Write(sep);
                }

                writer.Write("[" + n + "]{");

                string innerSep = "";
                foreach(string k in rs.Keys)
                {
                    writer.Write(innerSep + "[" + k + "](" + rs[k] + ")");
                    innerSep = ",";
                }

                writer.Write("}");
                sep = "|";
            }

            writer.Write("\"\r\n}");

            return true;
        }*/
    }
}
