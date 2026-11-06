using System;
using System.IO;
using System.Text;
using L4D2Vpk;

class Debug1
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        string f = args[0];
        using (FileStream fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (BinaryReader br = new BinaryReader(fs))
        {
            br.ReadUInt32(); uint ver = br.ReadUInt32(); uint treeSize = br.ReadUInt32();
            long treeStart = 12; if (ver == 2) { br.ReadUInt32(); br.ReadUInt32(); br.ReadUInt32(); br.ReadUInt32(); treeStart = 28; }
            fs.Seek(treeStart, SeekOrigin.Begin);
            byte[] t = br.ReadBytes((int)treeSize);
            Console.WriteLine("treeSize=" + treeSize);

            // 手工走一遍，输出每个组的边界
            int p = 0; int end = t.Length; int g = 0;
            while (p < end && g < 400)
            {
                string ext = RS(t, ref p, end);
                if (ext.Length == 0) { Console.WriteLine("[TREE END @0x" + p.ToString("X") + "]"); break; }
                string path = RS(t, ref p, end);
                int nf = 0;
                while (p < end)
                {
                    int before = p;
                    string name = RS(t, ref p, end);
                    if (name.Length == 0)
                    {
                        Console.WriteLine("  group ext=" + ext + " path=" + path + " files=" + nf +
                            "  组结束 @0x" + p.ToString("X") + " 下一字节=" + (p < end ? "0x" + t[p].ToString("X2") : "EOF") +
                            " 剩余待解析=" + (end - p));
                        if (p < end && t[p] == 0) p++;
                        break;
                    }
                    if (p + 18 > end) { Console.WriteLine("  !! 截断"); return; }
                    p += 18; nf++;
                }
                g++;
            }
            Console.WriteLine();
            Console.WriteLine("停止位置附近十六进制:");
            int s = Math.Max(0, p - 96);
            for (int o = s; o < Math.Min(t.Length, p + 160); o += 16)
            {
                string line = "", asc = "";
                for (int i = 0; i < 16; i++)
                {
                    if (o + i < t.Length) { byte v = t[o + i]; line += string.Format("{0:X2} ", v); asc += (v >= 32 && v < 127) ? (char)v : '.'; }
                }
                Console.WriteLine(string.Format("{0:X6}  {1}  {2}", o, line, asc));
            }
        }
    }

    static string RS(byte[] t, ref int p, int end)
    {
        int s = p;
        while (p < end && t[p] != 0) p++;
        string r = Encoding.UTF8.GetString(t, s, p - s);
        if (p < end) p++;
        return r;
    }
}
