using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// 生成一个"分卷 VPK"测试样本：
//   split_dir.vpk  —— 目录树 + 自含条目（archive_index = 0x7fff）
//   split_001.vpk  —— 纯数据分卷（无 VPK 头，偏移从 0 起算）
// 用于验证工具能否跨分卷正确找到 .bsp 并做 VBSP 校验。
class SplitGen
{
    class Item
    {
        public string Ext, Dir, Name;
        public ushort ArchiveIndex;
        public byte[] Data;
    }

    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        string outDir = args[0];
        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

        string mission =
            "\"mission\"\r\n{\r\n" +
            "\t\"Name\"\t\t\"testcamp\"\r\n" +
            "\t\"DisplayTitle\"\t\"Test Campaign\"\r\n" +
            "\t\"modes\"\r\n\t{\r\n\t\t\"coop\"\r\n\t\t{\r\n" +
            "\t\t\t\"1\"\r\n\t\t\t{\r\n\t\t\t\t\"Map\" \"alpha_01\"\r\n\t\t\t\t\"DisplayName\" \"Alpha One\"\r\n\t\t\t}\r\n" +
            "\t\t\t\"2\"\r\n\t\t\t{\r\n\t\t\t\t\"Map\" \"alpha_02\"\r\n\t\t\t\t\"DisplayName\" \"Alpha Two\"\r\n\t\t\t}\r\n" +
            "\t\t}\r\n\t}\r\n}\r\n";

        List<Item> items = new List<Item>();
        items.Add(new Item { Ext = "bsp", Dir = "maps", Name = "alpha_02", ArchiveIndex = 0x7fff, Data = MakeBsp(2000) });
        items.Add(new Item { Ext = "bsp", Dir = "maps", Name = "alpha_01", ArchiveIndex = 1, Data = MakeBsp(3000) });
        items.Add(new Item { Ext = "txt", Dir = "missions", Name = "testcamp", ArchiveIndex = 0x7fff, Data = Encoding.UTF8.GetBytes(mission) });
        // 一个不是地图的 .bsp（魔数不对）→ 应被严格校验剔除
        items.Add(new Item { Ext = "bsp", Dir = "maps", Name = "fake_01", ArchiveIndex = 0x7fff, Data = Encoding.ASCII.GetBytes("NOTABSP at all, definitely not a map file") });
        // 一个正常素材条目（确保多扩展名/多目录解析不乱）
        items.Add(new Item { Ext = "txt", Dir = "materials/x", Name = "note", ArchiveIndex = 0x7fff, Data = Encoding.UTF8.GetBytes("hello") });

        // ---- 分配偏移 ----
        long dirDataLen = 0;
        foreach (Item it in items)
            if (it.ArchiveIndex == 0x7fff) dirDataLen += it.Data.Length;

        // ---- 组装目录树 ----
        MemoryStream tree = new MemoryStream();
        // 扩展名分组：bsp / txt
        string[] exts = new string[] { "bsp", "txt" };
        foreach (string ext in exts)
        {
            Write(tree, Encoding.UTF8.GetBytes(ext));
            // 该扩展名下的路径分组
            List<string> dirs = new List<string>();
            foreach (Item it in items)
                if (it.Ext == ext && !dirs.Contains(it.Dir)) dirs.Add(it.Dir);
            foreach (string d in dirs)
            {
                Write(tree, Encoding.UTF8.GetBytes(d));
                foreach (Item it in items)
                {
                    if (it.Ext != ext || it.Dir != d) continue;
                    Write(tree, Encoding.UTF8.GetBytes(it.Name));
                    long off = 0;
                    if (it.ArchiveIndex == 0x7fff)
                    {
                        off = dirDataLen > 0 ? 0 : 0;
                        // 依次累加
                        off = 0;
                        foreach (Item p in items)
                        {
                            if (p.ArchiveIndex != 0x7fff) continue;
                            if (p == it) break;
                            off += p.Data.Length;
                        }
                    }
                    else
                    {
                        off = 0;
                        foreach (Item p in items)
                        {
                            if (p.ArchiveIndex == 0x7fff) continue;
                            if (p == it) break;
                            off += p.Data.Length;
                        }
                    }
                    byte[] hdr = new byte[18];
                    // crc32 留 0, preload 0, archiveIndex, offset, length, suffix 0xffff
                    BitConverter.GetBytes(it.ArchiveIndex).CopyTo(hdr, 6);
                    BitConverter.GetBytes((uint)off).CopyTo(hdr, 8);
                    BitConverter.GetBytes((uint)it.Data.Length).CopyTo(hdr, 12);
                    BitConverter.GetBytes((ushort)0xffff).CopyTo(hdr, 16);
                    tree.Write(hdr, 0, 18);
                }
                tree.WriteByte(0);   // 结束该路径的文件名列表
            }
            tree.WriteByte(0);       // 结束该扩展名的路径列表
        }
        tree.WriteByte(0);           // 结束整棵树

        byte[] treeBytes = tree.ToArray();

        // ---- 写 dir 文件 ----
        string dirFile = Path.Combine(outDir, "split_dir.vpk");
        using (FileStream fs = new FileStream(dirFile, FileMode.Create, FileAccess.Write))
        using (BinaryWriter w = new BinaryWriter(fs))
        {
            w.Write((uint)0x55AA1234);
            w.Write((uint)1);
            w.Write((uint)treeBytes.Length);
            fs.Write(treeBytes, 0, treeBytes.Length);
            foreach (Item it in items)
                if (it.ArchiveIndex == 0x7fff) fs.Write(it.Data, 0, it.Data.Length);
        }

        // ---- 写 001 分卷（裸数据，无头）----
        string volFile = Path.Combine(outDir, "split_001.vpk");
        using (FileStream fs = new FileStream(volFile, FileMode.Create, FileAccess.Write))
        {
            foreach (Item it in items)
                if (it.ArchiveIndex != 0x7fff) fs.Write(it.Data, 0, it.Data.Length);
        }

        Console.WriteLine("已生成:");
        Console.WriteLine("  " + dirFile + "  (" + new FileInfo(dirFile).Length + " 字节, tree=" + treeBytes.Length + ")");
        Console.WriteLine("  " + volFile + "  (" + new FileInfo(volFile).Length + " 字节, 裸数据)");
        Console.WriteLine("  期望结果: 3 个 bsp 条目中 alpha_01(在分卷) / alpha_02(在dir) / fake_01(魔数不对→应剔除)");
    }

    static byte[] MakeBsp(int size)
    {
        byte[] b = new byte[size];
        byte[] magic = Encoding.ASCII.GetBytes("VBSP");
        Array.Copy(magic, b, 4);
        for (int i = 4; i < size; i++) b[i] = (byte)(i % 251);
        return b;
    }

    static void Write(Stream s, byte[] b) { s.Write(b, 0, b.Length); s.WriteByte(0); }
}
