using System;
using System.Collections.Generic;

namespace KeePassLib
{
    public class PwGroup
    {
        private KeePassLib.Collections.StringDictionaryEx m_dCustomData = new KeePassLib.Collections.StringDictionaryEx();

        public KeePassLib.Collections.StringDictionaryEx CustomData
        {
            get { return m_dCustomData; }
            set { m_dCustomData = value; }
        }
    }
    
    public class PwDatabase
    {
        private KeePassLib.Collections.StringDictionaryEx m_dCustomData = new KeePassLib.Collections.StringDictionaryEx();

        public KeePassLib.Collections.StringDictionaryEx CustomData
        {
            get { return m_dCustomData; }
            set { m_dCustomData = value; }
        }
    }
}

namespace KeePassLib
{
    public class PwUuid
    {
        private byte[] m_bytes;

        public PwUuid(byte[] bytes)
        {
            m_bytes = bytes;
        }

        public byte[] UuidBytes => m_bytes;
    }
}

namespace KeePassLib.Collections
{
    public class StringDictionaryEx
    {
        private Dictionary<string, string> m_dict = new Dictionary<string, string>();

        public void Set(string key, string value)
        {
            m_dict[key] = value;
        }

        public string Get(string key)
        {
            if (m_dict.TryGetValue(key, out string val))
                return val;
            return null;
        }
    }
}
