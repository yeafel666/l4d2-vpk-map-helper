using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

class MapFind
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        foreach (string f in args)
        {
            Console.WriteLine("=== " + f);
            try { Run(f); }
            catch (Exception ex) { Console.WriteLine("  错误: " + ex.Message); }
            Console.WriteLine();
        }
    }

    static void Run(string file)
    {
        byte[] tree; long dataStart; long fileLen;
        using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (BinaryReader br = new BinaryReader(fs))
        {
            uint sig = br.ReadUInt32(); uint ver = br.ReadUInt32(); uint treeSize = br.ReadUInt32();
            long ts = 12; if (ver == 2) { br.ReadUInt32(); br.ReadUInt32(); br.ReadUInt32(); br.ReadUInt32(); ts = 28; }
            fs.Seek(ts, SeekOrigin.Begin);
            tree = br.ReadBytes((int)treeSize);
            dataStart = ts + treeSize;
            fileLen = fs.Length;
            Console.WriteLine("  ver=" + ver + " treeSize=" + treeSize + " dataStart=" + dataStart + " fileLen=" + fileLen);

            // 找出所有 "maps\0" 出现处，检查其前一个字符串是否为合法扩展名
            List<int> groups = new List<int>();
            for (int i = 0; i + 5 <= tree.Length; i++)
            {
                if (tree[i] == 'm' && tree[i + 1] == 'a' && tree[i + 2] == 'p' && tree[i + 3] == 's' && tree[i + 4] == 0)
                {
                    // 回溯找 ext 字符串
                    int e = i - 1;
                    if (e < 0 || tree[e] != 0) continue;
                    int s = e;
                    while (s > 0 && tree[s - 1] != 0) s--;
                    string ext = Encoding.UTF8.GetString(tree, s, e - s);
                    if (ext.Length >= 2 && ext.Length <= 12 && IsAlnum(ext)) groups.Add(i + 5);
                }
            }
            Console.WriteLine("  找到 " + groups.Count + " 个 maps 组候选");

            foreach (int gstart in groups)
            {
                // 从该组起点向后走文件条目
                int p = gstart;
                int count = 0;
                List<string> found = new List<string>();
                while (p < tree.Length && count < 200)
                {
                    string name = RS(tree, ref p);
                    if (name.Length == 0) break;
                    if (p + 18 > tree.Length) break;
                    uint off = BitConverter.ToUInt32(tree, p + 8);
                    uint len = BitConverter.ToUInt32(tree, p + 12);
                    p += 18;
                    count++;
                    // VBSP 铁证
                    bool ok = false;
                    if (dataStart + off + 4 <= fileLen)
                    {
                        using (FileStream fs2 = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            fs2.Seek(dataStart + off, SeekOrigin.Begin);
                            byte[] b = new byte[4];
                            fs2.Read(b, 0, 4);
                            ok = (b[0] == 'V' && b[1] == 'B' && b[2] == 'S' && b[3] == 'P');
                        }
                    }
                    if (ok) found.Add(name + " (" + (len / 1024 / 1024) + "MB, off=" + off + ")");
                    else if (found.Count == 0) break;   // 第一条就失败 → 不是地图组
                }
                if (found.Count > 0)
                {
                    Console.WriteLine("  --- maps 组 @0x" + gstart.ToString("X") + " 命中 " + found.Count + " 个 .bsp ---");
                    foreach (string s2 in found) Console.WriteLine("      " + s2);
                }
            }
        }
    }

    static bool IsAlnum(string s)
    {
        foreach (char c in s) if (!char.IsLetterOrDigit(c) && c != '_') return false;
        return true;
    }

    static string RS(byte[] t, ref int p)
    {
        int s = p;
        while (p < t.Length && t[p] != 0) p++;
        string r = Encoding.UTF8.GetString(t, s, p - s);
        if (p < t.Length) p++;
        return r;
    }
}
