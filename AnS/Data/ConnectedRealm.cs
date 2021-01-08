using System;
using System.Collections.Generic;
using System.Text;

namespace AnS.Data
{
    public class Realm
    {
        public int id;
        public Dictionary<string,string> name;
        public string slug;
    }

    public class ConnectedRealm
    {
        public int id;
        public List<Realm> realms;
    }
}
