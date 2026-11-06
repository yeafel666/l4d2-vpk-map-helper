using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

class Brute
{
    class Res { public int H, S; public int groups, files; public int mapsFound; public bool vbspOk; public string Note; }

    static byte[] tree; static int end; static long dataStart; static string file;

    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        file = args[0];
        using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (BinaryReader br = new BinaryReader(fs))
        {
            br.ReadUInt32(); uint ver = br.ReadUInt32(); uint treeSize = br.ReadUInt32();
            long ts = 12; if (ver == 2) { br.ReadUInt32(); br.ReadUInt32(); br.ReadUInt32(); br.ReadUInt32(); ts = 28; }
            fs.Seek(ts, SeekOrigin.Begin);
            tree = br.ReadBytes((int)treeSize);
            end = tree.Length;
            dataStart = ts + treeSize;
            Console.WriteLine("treeSize=" + treeSize + " dataStart=" + dataStart + " fileLen=" + fs.Length);

            List<Res> results = new List<Res>();
            for (int H = 12; H <= 24; H++)
                for (int S = 0; S <= 4; S++)
                {
                    Res r;
                    if (TryParse(H, S, out r)) results.Add(r);
                }

            Console.WriteLine();
            Console.WriteLine("通过结构校验的组合：");
            foreach (Res r in results)
                Console.WriteLine(string.Format("  头长H={0,2}  组尾额外字节S={0}  组数={1,4} 文件数={2,6} 地图条目={3,3} VBSP校验={4}  {5}",
                    r.H, r.S, r.groups, r.files, r.mapsFound, r.vbspOk ? "全部通过" : "部分失败", r.Note));
        }
    }

    static bool TryParse(int H, int S, out Res res)
    {
        res = new Res(); res.H = H; res.S = S;
        int p = 0, groups = 0, files = 0, maps = 0, vbspOk = 0, vbspBad = 0;
        List<int> mapEntries = new List<int>();
        try
        {
            while (p < end)
            {
                string ext = RS(ref p);
                if (ext.Length == 0) break;
                if (!PlausibleExt(ext)) { res.Note = "扩展名不合理:" + ext; return false; }
                string path = RS(ref p);
                if (!PlausiblePath(path)) { res.Note = "路径不合理:" + path; return false; }
                groups++;
                while (p < end)
                {
                    string name = RS(ref p);
                    if (name.Length == 0) { p += S; break; }
                    if (p + H > end) { res.Note = "截断"; return false; }
                    if (!PlausibleName(name)) { res.Note = "文件名不合理:" + name; return false; }
                    int entryPos = p;
                    uint off = BitConverter.ToUInt32(tree, p + 8);
                    uint len = BitConverter.ToUInt32(tree, p + 12);
                    p += H;
                    files++;
                    if (path.Equals("maps", StringComparison.OrdinalIgnoreCase) &&
                        ext.Equals("bsp", StringComparison.OrdinalIgnoreCase))
                    {
                        maps++; mapEntries.Add(entryPos);
                    }
                }
            }
        }
        catch (Exception) { res.Note = "异常"; return false; }

        if (p != end) { res.Note = "未对齐(差 " + (end - p) + ")"; return false; }

        // VBSP 铁证校验
        foreach (int epos in mapEntries)
        {
            uint off = BitConverter.ToUInt32(tree, epos + 8);
            uint len = BitConverter.ToUInt32(tree, epos + 12);
            using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                fs.Seek(dataStart + off, SeekOrigin.Begin);
                byte[] b = new byte[4];
                fs.Read(b, 0, 4);
                if (b[0] == 'V' && b[1] == 'B' && b[2] == 'S' && b[3] == 'P') vbspOk++;
                else vbspBad++;
            }
        }
        res.groups = groups; res.files = files; res.mapsFound = maps;
        res.vbspOk = (vbspBad == 0 && maps > 0);
        res.Note = "VBSP通过" + vbspOk + "/失败" + vbspBad;
        return true;
    }

    static bool PlausibleExt(string s)
    {
        if (s.Length == 0 || s.Length > 12) return false;
        foreach (char c in s) if (!char.IsLetterOrDigit(c) && c != '_') return false;
        return true;
    }
    static bool PlausiblePath(string s)
    {
        if (s.Length > 200) return false;
        foreach (char c in s) if (c < 32 || c > 126) return false;
        return true;
    }
    static bool PlausibleName(string s)
    {
        if (s.Length == 0 || s.Length > 120) return false;
        foreach (char c in s) if (c < 32 || c > 126) return false;
        return true;
    }

    static string RS(ref int p)
    {
        int s = p;
        while (p < end && tree[p] != 0) p++;
        string r = Encoding.UTF8.GetString(tree, s, p - s);
        if (p < end) p++;
        return r;
    }
}
