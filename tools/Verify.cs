using System;
using System.Collections.Generic;
using System.Text;
using L4D2Vpk;

class Verify
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        List<string> files = new List<string>();
        foreach (string a in args) files.Add(a);

        foreach (SortMode mode in new SortMode[] { SortMode.Auto, SortMode.ByName, SortMode.ByOffset })
        {
            Console.WriteLine("################ 排序方式 = " + ModeName(mode));
            ParseResult res = MapCollector.Collect(files, mode, true);

            foreach (FileStatus st in res.Files)
                Console.WriteLine("  [" + (st.Ok ? "OK " : "ERR") + "] " + st.Name.PadRight(20) +
                                  st.Message.PadRight(34) + "地图数=" + st.MapCount + "  条目=" + st.EntryCount);

            string cur = null;
            int n = 0;
            foreach (MapItem mi in res.Maps)
            {
                if (mi.GroupKey != cur)
                {
                    cur = mi.GroupKey;
                    Console.WriteLine("  ── 战役【" + mi.GroupTitle + "】" +
                                      (mi.CampaignName.Length > 0 ? "  (campaign: " + mi.CampaignName + ")" : ""));
                }
                n++;
                Console.WriteLine("     " + n.ToString().PadLeft(2) + ". map " + mi.MapName.PadRight(16) +
                                  (mi.DisplayName.Length > 0 ? mi.DisplayName.PadRight(20) : "") +
                                  "  ch=" + mi.Chapter + " lv=" + mi.Level +
                                  (mi.HasAuthorOrder ? "  权威序=" + mi.AuthorOrder : "  权威序=-") +
                                  "  off=" + mi.Offset);
            }
            Console.WriteLine("  合计 " + res.Maps.Count + " 张地图 / " + res.Groups.Count + " 个战役");
            Console.WriteLine();
        }

        // 复制全部的纯指令文本
        ParseResult r2 = MapCollector.Collect(files, SortMode.Auto, true);
        StringBuilder sb = new StringBuilder();
        foreach (MapItem mi in r2.Maps) sb.Append("map ").Append(mi.MapName).Append("\r\n");
        Console.WriteLine("################ 「复制全部开图指令」输出内容:");
        Console.Write(sb.ToString());
    }

    static string ModeName(SortMode m)
    {
        if (m == SortMode.Auto) return "自动（推荐）";
        if (m == SortMode.ByName) return "按关卡名";
        return "按 VPK 内顺序";
    }
}
