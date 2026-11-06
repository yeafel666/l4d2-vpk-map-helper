using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

class Entry
{
    public string Path;
    public string Name;
    public string Ext;
    public uint Length;
    public uint Offset;
    public int Archive;
    public int MetaLen;
}

class Program
{
    static List<Entry> Entries = new List<Entry>();

    static void Main(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("=== " + args[i] + " ===");
            Entries.Clear();
            try { Parse(args[i]); }
            catch (Exception ex) { Console.WriteLine("ERROR: " + ex.Message); continue; }
            Console.WriteLine("total entries: " + Entries.Count);
            // 按目录聚合
            Dictionary<string, int> dirs = new Dictionary<string, int>();
            foreach (Entry e in Entries)
            {
                string key = e.Path;
                if (!dirs.ContainsKey(key)) dirs[key] = 0;
                dirs[key]++;
            }
            List<string> keys = new List<string>(dirs.Keys);
            keys.Sort();
            foreach (string k in keys)
            {
                Console.WriteLine("  DIR [" + (k == "" ? "<root>" : k) + "]  files=" + dirs[k]);
                foreach (Entry e in Entries)
                {
                    if (e.Path == k)
                        Console.WriteLine("      " + e.Name + "." + e.Ext + "   len=" + e.Length + " arch=" + e.Archive);
                }
            }
            Console.WriteLine();
        }
    }

    static void Parse(string file)
    {
        byte[] all = File.ReadAllBytes(file);
        uint sig = BitConverter.ToUInt32(all, 0);
        uint ver = BitConverter.ToUInt32(all, 4);
        uint treeSize = BitConverter.ToUInt32(all, 8);
        if (sig != 0x55AA1234 && sig != 0x55AA1235)
            throw new Exception("not a vpk, sig=0x" + sig.ToString("X8"));
        int p = 12;
        if (ver == 2)
        {
            p += 16; // data section size + 3 md5 sections
        }
        ReadTree(all, p, "");
    }

    static void ReadTree(byte[] all, int p, string path)
    {
        while (true)
        {
            string ext = ReadString(all, ref p);
            if (ext.Length == 0) break;
            string epath = ReadString(all, ref p);
            string name = ReadString(all, ref p);
            uint len = BitConverter.ToUInt32(all, p); p += 4;
            uint off = BitConverter.ToUInt32(all, p); p += 4;
            int arch = BitConverter.ToUInt16(all, p); p += 2;
            int metaLen = BitConverter.ToUInt16(all, p); p += 2;
            p += metaLen;
            Entry e = new Entry();
            e.Path = path;
            e.Name = name;
            e.Ext = ext;
            e.Length = len;
            e.Offset = off;
            e.Archive = arch;
            e.MetaLen = metaLen;
            Entries.Add(e);

            // 目录项后紧跟其子目录的树
            if (epath.Length > 0)
            {
                string sub = (path.Length > 0) ? path + "/" + epath : epath;
                ReadTree(all, p, sub);
            }
        }
    }

    static string ReadString(byte[] b, ref int p)
    {
        int start = p;
        while (p < b.Length && b[p] != 0) p++;
        string s = Encoding.UTF8.GetString(b, start, p - start);
        p++; // skip null
        return s;
    }
}
