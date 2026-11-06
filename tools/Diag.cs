using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using L4D2Vpk;

class Diag
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Vpk v = Vpk.Parse(args[0]);
        Console.WriteLine("entries=" + v.Entries.Count + " ok=" + v.Ok + " " + v.Error);

        Dictionary<string, int> extCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (VpkEntry e in v.Entries)
        {
            string k = e.Ext;
            if (!extCount.ContainsKey(k)) extCount[k] = 0;
            extCount[k] = extCount[k] + 1;
        }
        List<string> keys = new List<string>(extCount.Keys);
        keys.Sort();
        Console.WriteLine("--- 扩展名分布 ---");
        foreach (string k in keys) Console.WriteLine("   ." + k.PadRight(12) + extCount[k]);

        Console.WriteLine("--- archiveIndex 分布 ---");
        Dictionary<int, int> ai = new Dictionary<int, int>();
        foreach (VpkEntry e in v.Entries)
        {
            if (!ai.ContainsKey(e.ArchiveIndex)) ai[e.ArchiveIndex] = 0;
            ai[e.ArchiveIndex] = ai[e.ArchiveIndex] + 1;
        }
        List<int> akeys = new List<int>(ai.Keys);
        akeys.Sort();
        foreach (int k in akeys) Console.WriteLine("   archiveIndex=" + k + "  条目=" + ai[k]);

        Console.WriteLine("--- maps 目录下的前 15 个条目 ---");
        int n = 0;
        foreach (VpkEntry e in v.Entries)
        {
            if (e.DirName != "maps") continue;
            Console.WriteLine(string.Format("   {0,-34} ext={1,-6} len={2,10} off={3,9} ai={4} VBSP={5}",
                e.FullName, e.Ext, e.Length, e.Offset, e.ArchiveIndex, v.VerifyBsp(e)));
            n++;
            if (n >= 15) break;
        }
        int mapsTotal = 0, bspTotal = 0, vbspOk = 0;
        foreach (VpkEntry e in v.Entries)
        {
            if (e.DirName != "maps") continue;
            mapsTotal++;
            if (e.IsBsp) { bspTotal++; if (v.VerifyBsp(e)) vbspOk++; }
        }
        Console.WriteLine("maps 目录条目总数=" + mapsTotal + "  其中 .bsp=" + bspTotal + "  VBSP通过=" + vbspOk);
    }
}
