using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace L4D2Vpk
{
    /// <summary>VPK 目录树中的一个文件条目</summary>
    public class VpkEntry
    {
        public string Ext = "";        // 扩展名，不含点
        public string Dir = "";        // 目录，如 "maps"；根目录为 ""
        public string Name = "";       // 文件名，不含扩展名
        public uint Crc32;
        public ushort PreloadLength;
        public ushort ArchiveIndex;
        public uint Offset;            // 数据区内偏移（相对 DataStart）
        public uint Length;            // 文件长度
        public string SourceFile = ""; // 来源 vpk 文件路径

        public string DirName
        {
            get { return (Dir == "" || Dir == " ") ? "" : Dir.Replace('\\', '/'); }
        }

        /// <summary>完整路径，如 "maps/requiem_01.bsp"</summary>
        public string FullName
        {
            get
            {
                string d = DirName;
                return (d.Length > 0 ? d + "/" : "") + Name + "." + Ext;
            }
        }

        public bool IsBsp
        {
            get { return string.Equals(Ext, "bsp", StringComparison.OrdinalIgnoreCase); }
        }
    }

    /// <summary>VPK 文件解析器（Valve Package v1 / v2）</summary>
    public class Vpk
    {
        public string FilePath = "";
        public uint Version;
        public uint TreeSize;
        public long HeaderLength;   // 目录树起始偏移（v1=12, v2=28）
        public long DataStart;      // 数据区起始偏移
        public long FileSizeOnDisk;
        public List<VpkEntry> Entries = new List<VpkEntry>();
        public string Error = "";
        public bool Ok;

        private const int ENTRY_HEADER_SIZE = 18;   // crc32(4)+preload(2)+archiveIndex(2)+offset(4)+length(4)+suffix(2)
        private const ushort SUFFIX_MARKER = 0xffff;

        public static Vpk Parse(string path)
        {
            Vpk v = new Vpk();
            v.FilePath = path;
            try
            {
                v.ParseInternal();
                v.Ok = true;
            }
            catch (Exception ex)
            {
                v.Ok = false;
                v.Error = ex.Message;
            }
            return v;
        }

        private void ParseInternal()
        {
            using (FileStream fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (BinaryReader br = new BinaryReader(fs))
            {
                uint sig = br.ReadUInt32();
                Version = br.ReadUInt32();
                TreeSize = br.ReadUInt32();

                if (sig != 0x55AA1234 && sig != 0x55AA1235)
                    throw new Exception("不是有效的 VPK 文件（签名 0x" + sig.ToString("X8") + "）");
                if (Version != 1 && Version != 2)
                    throw new Exception("不支持的 VPK 版本: " + Version);

                HeaderLength = 12;
                if (Version == 2)
                {
                    br.ReadUInt32(); // fileDataSectionSize
                    br.ReadUInt32(); // archiveMD5SectionSize
                    br.ReadUInt32(); // otherMD5SectionSize
                    br.ReadUInt32(); // signatureSectionSize
                    HeaderLength = 28;
                }

                DataStart = HeaderLength + TreeSize;
                FileSizeOnDisk = fs.Length;

                fs.Seek(HeaderLength, SeekOrigin.Begin);
                byte[] tree = br.ReadBytes((int)TreeSize);
                if (tree.Length != (int)TreeSize)
                    throw new Exception("VPK 文件被截断，读不到完整目录树");

                ParseTree(tree);
            }
        }

        /// <summary>
        /// 解析嵌套目录树：
        ///   ext\0 → ( path\0 → ( name\0 + 18字节头 )* → \0 )* → \0 )* → \0
        /// 空扩展名 = 整棵树结束；空路径名 = 该扩展名结束；空文件名 = 该路径结束。
        /// </summary>
        private void ParseTree(byte[] t)
        {
            int p = 0;
            int end = t.Length;
            string ext = "";

            while (true)
            {
                ext = ReadString(t, ref p);
                if (ext.Length == 0) break;            // 树结束

                while (true)
                {
                    string dir = ReadString(t, ref p);
                    if (dir.Length == 0) break;         // 该扩展名结束

                    while (true)
                    {
                        string name = ReadString(t, ref p);
                        if (name.Length == 0) break;    // 该路径结束

                        if (p + ENTRY_HEADER_SIZE > end)
                            throw new Exception("VPK 目录树结构异常（条目被截断）");

                        VpkEntry e = new VpkEntry();
                        e.Ext = ext;
                        e.Dir = dir;
                        e.Name = name;
                        e.Crc32 = BitConverter.ToUInt32(t, p);
                        e.PreloadLength = BitConverter.ToUInt16(t, p + 4);
                        e.ArchiveIndex = BitConverter.ToUInt16(t, p + 6);
                        e.Offset = BitConverter.ToUInt32(t, p + 8);
                        e.Length = BitConverter.ToUInt32(t, p + 12);
                        e.SourceFile = FilePath;
                        p += ENTRY_HEADER_SIZE;

                        Entries.Add(e);
                    }
                }
            }
        }

        private static string ReadString(byte[] t, ref int p)
        {
            if (p >= t.Length) return "";
            int start = p;
            while (p < t.Length && t[p] != 0) p++;
            string s = Encoding.UTF8.GetString(t, start, p - start);
            if (p < t.Length) p++;   // 跳过结尾的 \0
            return s;
        }

        /// <summary>读取条目在磁盘上的绝对起始偏移（仅对本文件内的条目有效）</summary>
        public long AbsoluteOffset(VpkEntry e)
        {
            return DataStart + e.Offset;
        }

        // ---------- 分卷（split archive）支持 ----------
        // Valve 的分卷结构：xxx_dir.vpk 持有目录树；xxx_001.vpk 等分卷是"裸数据"，
        // 连 VPK 头都没有，条目的 offset 相对分卷文件起点 0 计算。
        private Dictionary<ushort, string> archiveMap;   // archiveIndex → 分卷路径
        private List<string> archiveFiles;                // 同目录下所有分卷
        private bool archiveScanned;

        /// <summary>同目录下的数据分卷文件（不含本文件）</summary>
        private void ScanArchives()
        {
            if (archiveScanned) return;
            archiveScanned = true;
            archiveMap = new Dictionary<ushort, string>();
            archiveFiles = new List<string>();
            try
            {
                string dir = Path.GetDirectoryName(FilePath);
                if (string.IsNullOrEmpty(dir)) dir = ".";
                string self = Path.GetFileName(FilePath);

                string[] all = Directory.GetFiles(dir, "*.vpk");
                Array.Sort(all, StringComparer.OrdinalIgnoreCase);

                string baseName = Path.GetFileNameWithoutExtension(FilePath);   // 如 xxx_dir / pak01_dir
                string prefix = baseName.EndsWith("_dir", StringComparison.OrdinalIgnoreCase)
                                ? baseName.Substring(0, baseName.Length - 4)
                                : baseName;

                foreach (string f in all)
                {
                    if (string.Equals(f, self, StringComparison.OrdinalIgnoreCase)) continue;
                    string fn = Path.GetFileNameWithoutExtension(f);           // 如 xxx_001 / pak01_001
                    int us = fn.LastIndexOf('_');
                    if (us < 0) continue;
                    ushort idx;
                    if (!ushort.TryParse(fn.Substring(us + 1), out idx)) continue;
                    string head = fn.Substring(0, us);
                    // 只认同前缀的分卷（xxx_dir ↔ xxx_001），避免把别的包的卷拉进来
                    if (!string.Equals(head, prefix, StringComparison.OrdinalIgnoreCase)) continue;
                    archiveFiles.Add(f);
                    if (!archiveMap.ContainsKey(idx)) archiveMap[idx] = f;
                }
            }
            catch { }
        }

        /// <summary>解析某个 archiveIndex 的数据所在文件；null 表示分卷缺失</summary>
        public string ResolveArchive(ushort archiveIndex)
        {
            if (archiveIndex == 0x7fff) return FilePath;
            ScanArchives();
            if (archiveMap == null) return null;
            string s;
            if (archiveMap.TryGetValue(archiveIndex, out s)) return s;
            return null;
        }

        /// <summary>已自动关联的数据分卷数量</summary>
        public int SiblingArchiveCount
        {
            get { ScanArchives(); return archiveFiles == null ? 0 : archiveFiles.Count; }
        }

        /// <summary>
        /// 定位条目数据：返回所在文件与文件内偏移。
        /// 数据分卷缺失时返回 false（调用方应放宽校验，而不是丢弃地图）。
        /// </summary>
        public bool TryLocate(VpkEntry e, out string file, out long pos)
        {
            if (e.ArchiveIndex == 0x7fff)
            {
                file = FilePath;
                pos = DataStart + e.Offset;
                return true;
            }
            string s = ResolveArchive(e.ArchiveIndex);
            if (s == null) { file = null; pos = 0; return false; }
            file = s;
            pos = e.Offset;          // 分卷是裸数据，偏移从 0 起算
            return true;
        }

        /// <summary>读取条目的原始字节（最多 maxBytes）</summary>
        public byte[] ReadEntryBytes(VpkEntry e, int maxBytes)
        {
            string file;
            long pos;
            if (!TryLocate(e, out file, out pos)) return new byte[0];
            using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                fs.Seek(pos, SeekOrigin.Begin);
                int n = (int)Math.Min((long)maxBytes, e.Length);
                byte[] buf = new byte[n];
                int read = 0;
                while (read < n)
                {
                    int r = fs.Read(buf, read, n - read);
                    if (r <= 0) break;
                    read += r;
                }
                return buf;
            }
        }

        /// <summary>校验条目数据是否以 "VBSP" 开头（.bsp 地图文件的真实铁证）</summary>
        public bool VerifyBsp(VpkEntry e)
        {
            try
            {
                string file;
                long pos;
                if (!TryLocate(e, out file, out pos)) return false;
                using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    fs.Seek(pos, SeekOrigin.Begin);
                    byte[] b = new byte[4];
                    int read = 0;
                    while (read < 4)
                    {
                        int r = fs.Read(b, read, 4 - read);
                        if (r <= 0) break;
                        read += r;
                    }
                    return read == 4 && b[0] == 'V' && b[1] == 'B' && b[2] == 'S' && b[3] == 'P';
                }
            }
            catch { return false; }
        }

        /// <summary>读取文本条目内容</summary>
        public string ReadEntryText(VpkEntry e, int maxBytes)
        {
            byte[] b = ReadEntryBytes(e, maxBytes);
            return Encoding.UTF8.GetString(b);
        }
    }
}
