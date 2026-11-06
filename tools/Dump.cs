using System;
using System.Collections.Generic;
using System.Text;
using L4D2Vpk;

class Dump
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        foreach (string f in args)
        {
            Vpk v = Vpk.Parse(f);
            Console.WriteLine("=== " + f);
            if (!v.Ok) { Console.WriteLine("  解析失败: " + v.Error); continue; }
            Console.WriteLine("  版本=" + v.Version + " treeSize=" + v.TreeSize + " 已消费=" + v.TreeConsumed +
                              " 条目数=" + v.Entries.Count + (v.TreeConsumed == v.TreeSize ? "  [树对齐 OK]" : "  [树未对齐!]"));

            // 按 (path, ext) 分组
            Dictionary<string, List<VpkEntry>> groups = new Dictionary<string, List<VpkEntry>>();
            foreach (VpkEntry e in v.Entries)
            {
                string k = e.Path + " | ." + e.Ext;
                if (!groups.ContainsKey(k)) groups[k] = new List<VpkEntry>();
                groups[k].Add(e);
            }
            List<string> keys = new List<string>(groups.Keys);
            keys.Sort();
            foreach (string k in keys)
            {
                Console.WriteLine("  [" + k + "]  x" + groups[k].Count);
            }

            // 单独列出 maps 组
            Console.WriteLine("  --- maps 相关 ---");
            foreach (VpkEntry e in v.Entries)
            {
                if (e.Path.ToLower() == "maps" && e.Ext.ToLower() == "bsp")
                {
                    byte[] head = v.ReadData(e, 8);
                    string magic = Encoding.ASCII.GetString(head);
                    Console.WriteLine(string.Format("    {0,-28} off={1,-12} len={2,-12} magic='{3}'",
                        e.FullName, e.Offset, e.Length, magic.Replace("\0", "\\0")));
                }
            }
            Console.WriteLine();
        }
    }
}
