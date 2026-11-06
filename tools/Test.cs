using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using L4D2Vpk;

class Test
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        foreach (string f in args)
        {
            Console.WriteLine("=== " + Path.GetFileName(f));
            Vpk v = Vpk.Parse(f);
            if (!v.Ok) { Console.WriteLine("  解析失败: " + v.Error); continue; }
            Console.WriteLine("  ver=" + v.Version + " treeSize=" + v.TreeSize + " entries=" + v.Entries.Count);

            // 按目录分组统计
            Dictionary<string, int> groups = new Dictionary<string, int>();
            foreach (VpkEntry e in v.Entries)
            {
                string k = e.DirName + " | ." + e.Ext;
                if (!groups.ContainsKey(k)) groups[k] = 0;
                groups[k] = groups[k] + 1;
            }
            List<string> keys = new List<string>(groups.Keys);
            keys.Sort();
            Console.WriteLine("  --- 目录/扩展名分布 (" + keys.Count + " 组) ---");
            foreach (string k in keys)
            {
                Console.WriteLine("      " + k.PadRight(50) + groups[k]);
            }

            // maps 下的 bsp
            List<VpkEntry> maps = new List<VpkEntry>();
            foreach (VpkEntry e in v.Entries)
                if (e.IsBsp && (e.DirName == "maps" || e.DirName.EndsWith("/maps"))) maps.Add(e);
            Console.WriteLine("  --- maps 下的 .bsp (" + maps.Count + ") ---");
            foreach (VpkEntry e in maps)
            {
                Console.WriteLine(string.Format("      {0,-28} len={1,10}  off={2,10}  VBSP={3}",
                    e.FullName, e.Length, e.Offset, v.VerifyBsp(e) ? "OK" : "失败"));
            }

            // missions 目录
            List<VpkEntry> missions = new List<VpkEntry>();
            foreach (VpkEntry e in v.Entries)
                if (e.DirName.StartsWith("missions", StringComparison.OrdinalIgnoreCase)) missions.Add(e);
            if (missions.Count > 0)
            {
                Console.WriteLine("  --- missions 内容 ---");
                foreach (VpkEntry e in missions)
                {
                    Console.WriteLine("      >>> " + e.FullName + " (" + e.Length + " 字节)");
                    string txt = v.ReadEntryText(e, 8000);
                    string[] lines = txt.Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string ln = lines[i].TrimEnd('\r');
                        if (ln.Trim().Length > 0) Console.WriteLine("          " + ln);
                        if (i > 200) { Console.WriteLine("          ...(截断)"); break; }
                    }
                }
            }
            Console.WriteLine();
        }
    }
}
