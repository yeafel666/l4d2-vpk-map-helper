using System;
using System.IO;
using System.Text;

class TreeDump
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        string file = args[0];
        int from = args.Length > 1 ? int.Parse(args[1]) : 0;
        int to = args.Length > 2 ? int.Parse(args[2]) : int.MaxValue;
        string filter = args.Length > 3 ? args[3] : null;

        using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (BinaryReader br = new BinaryReader(fs))
        {
            br.ReadUInt32(); uint ver = br.ReadUInt32(); uint treeSize = br.ReadUInt32();
            long ts = 12; if (ver == 2) { br.ReadUInt32(); br.ReadUInt32(); br.ReadUInt32(); br.ReadUInt32(); ts = 28; }
            fs.Seek(ts, SeekOrigin.Begin);
            byte[] t = br.ReadBytes((int)treeSize);

            int p = 0;
            while (p < t.Length)
            {
                int s = p;
                while (p < t.Length && t[p] != 0) p++;
                if (p >= t.Length) break;
                string str = Encoding.UTF8.GetString(t, s, p - s);
                bool printable = true;
                foreach (char c in str) if (c < 32 || c > 126) { printable = false; break; }
                p++; // 跳过 0
                bool inRange = (s >= from && s <= to);
                bool hit = filter == null || (printable && str.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
                if (inRange || hit)
                {
                    Console.WriteLine(string.Format("0x{0:X6}  len={1,-4} {2}  \"{3}\"",
                        s, (p - s), printable ? "txt" : "BIN", printable ? str : ("<" + BitConverter.ToString(t, s, Math.Min(p - s, 16)) + ">")));
                }
            }
        }
    }
}
