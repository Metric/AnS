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
            const int MAX_GROUP_SIZE = 5000;

            string zero = ((uint)0).ToBase93();
            string defaultSep = ",";
            string regionData = zero + zero;
            string zeroZero = zero + zero;
            string zeroZeroZeroZero = zero + zero + zero + zero;

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
                    Dictionary<string, Dictionary<string, List<ItemGroup>>> all = new Dictionary<string, Dictionary<string, List<ItemGroup>>>();
                    List<ConnectedRealm> realms = selected[region];

                    string regID = "\"" + region.ToUpper() + "\"";
                    writer.WriteLine("\ts[" + regID + "] = {};");

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

                            string rkey = UnpackKey(k);
                            string[] split = rkey.Split(":");
                            string subkey = $"{split[0]}:{split[1]}";
                            Dictionary<string, List<ItemGroup>> groups = null;
                            all.TryGetValue(subkey, out groups);

                            groups ??= new Dictionary<string, List<ItemGroup>>();
                            all[subkey] = groups;

                            List<ItemGroup> group = null;
                            groups.TryGetValue(k, out group);
                            group ??= new List<ItemGroup>();
                            groups[k] = group;
                            group.Add(new ItemGroup(r, UnpackValues(d), i));
                        }

                        realm.Close();

                        GC.Collect();
                    }

                    if (includeRegion && regionDB.Keys.Count() > 0)
                    {
                        int tcount = 1;
                        int offset = 0;

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
                            if(!subgroups.ContainsKey(k))
                            {
                                subgroups[k] = null;
                            }
                        }

                        GC.Collect();

                        Debug.WriteLine(all.Count);

                        foreach (string sk in all.Keys)
                        {
                            List<ItemGroup> g = null;

                            if (offset % MAX_GROUP_SIZE == 0)
                            {
                                writer.WriteLine($"\tsd[{tcount++}] = function()");
                            }

                            string results = "";

                            for (int i = 0; i < realms.Count; ++i)
                            {
                                string sep = "";
                                string packData = "";

                                foreach (string k in all[sk].Keys)
                                {
                                    g = all[sk][k];

                                    string regionValue = regionDB.GetString(k);

                                    if (!string.IsNullOrEmpty(regionValue))
                                    {
                                        uint[] values = UnpackRegionValues(regionValue);
                                        regionData = values[0].ToBase93() + values[1].ToBase93();
                                    }
                                    else
                                    {
                                        regionData = zeroZero;
                                    }

                                    var r = realms[i];
                                    var rg = g != null ? g.Find(m => m.realm.id == r.id) : null;

                                    if (rg != null)
                                    {
                                        var rdat = rg.data;
                                        var dat = rdat[1].ToBase93() + rdat[2].ToBase93() + rdat[3].ToBase93() + rdat[0].ToBase93() + regionData;
                                        packData += sep + UnpackKey(k) + defaultSep + dat;
                                    }
                                    else
                                    {
                                        var dat = zeroZeroZeroZero + regionData;
                                        packData += sep + UnpackKey(k) + defaultSep + dat;
                                    }

                                    sep = defaultSep;
                                }

                                results += $"{(results.Length > 0 ? defaultSep : string.Empty)}{WrapLuaBinary(packData)}";
                            }

                            writer.WriteLine($"\t\ts[{regID}][\"{sk}\"] = select(t[rid], {results});");
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

                        foreach(string sk in all.Keys)
                        {
                            List<ItemGroup> g = null;

                            if (offset % MAX_GROUP_SIZE == 0)
                            {
                                writer.WriteLine($"\tsd[{tcount++}] = function()");
                            }

                            string results = "";

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
                                        packData += sep + UnpackKey(k) + defaultSep + dat;
                                    }
                                    else
                                    {
                                        var dat = zeroZeroZeroZero + regionData;
                                        packData += sep + UnpackKey(k) + defaultSep + dat;
                                    }

                                    sep = defaultSep;
                                }

                                results += $"{(results.Length > 0 ? defaultSep : string.Empty)}{WrapLuaBinary(packData)}";
                            }

                            writer.WriteLine($"\t\ts[{regID}][\"{sk}\"] = select(t[rid], {results});");

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
                writer.Close();

                GC.Collect();
            }
        }
    }
}
