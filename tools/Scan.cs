using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        string file = args[0];
        string pattern = args.Length > 1 ? args[1] : @"c[1-9]m[0-9]{1,2}_[A-Za-z0-9_]+";
        Regex re = new Regex(pattern);
        byte[] b = File.ReadAllBytes(file);
        Console.WriteLine("file=" + file + "  size=" + b.Length);

        // 把整个文件当 latin1 字符串处理（ASCII 区一一对应）
        string s = Encoding.GetEncoding(28591).GetString(b);
        Dictionary<string, int> hits = new Dictionary<string, int>();
        List<int> positions = new List<int>();
        foreach (Match m in re.Matches(s))
        {
            if (!hits.ContainsKey(m.Value)) hits[m.Value] = 0;
            hits[m.Value]++;
            if (positions.Count < 5) positions.Add(m.Index);
        }
        Console.WriteLine("distinct matches: " + hits.Count);
        List<string> keys = new List<string>(hits.Keys);
        keys.Sort();
        foreach (string k in keys) Console.WriteLine("   " + k + "   x" + hits[k]);

        foreach (int pos in positions)
        {
            Console.WriteLine();
            Console.WriteLine("--- context @0x" + pos.ToString("X") + " ---");
            int start = Math.Max(0, pos - 48);
            int end = Math.Min(b.Length, pos + 80);
            for (int o = start; o < end; o += 16)
            {
                string line = "", asc = "";
                for (int i = 0; i < 16; i++)
                {
                    if (o + i < b.Length)
                    {
                        byte v = b[o + i];
                        line += string.Format("{0:X2} ", v);
                        asc += (v >= 32 && v < 127) ? (char)v : '.';
                    }
                }
                Console.WriteLine(string.Format("{0:X6}  {1}  {2}", o, line, asc));
            }
        }
    }
}
