using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace L4D2Vpk
{
    public enum SortMode
    {
        Auto = 0,      // 自动：优先用 missions 权威顺序，其次命名规则
        ByName = 1,    // 按关卡名自然排序
        ByOffset = 2   // 按 VPK 内存储偏移（= 打包顺序）
    }

    /// <summary>一张地图（.bsp）</summary>
    public class MapItem
    {
        public string MapName = "";        // 开图用的地图名，如 requiem_01
        public string DisplayName = "";    // 关卡名，如 Aleph
        public string CampaignName = "";   // 战役代号，如 prodeus
        public string CampaignTitle = "";  // 战役标题
        public string SourceFile = "";     // 来源 vpk
        public bool HasAuthorOrder;        // 是否来自 missions 定义
        public int AuthorOrder;
        public int Chapter;
        public int Level;
        public long Offset;                // VPK 数据区偏移
        public long Length;
        public bool Verified;              // VBSP 魔数校验通过
        public bool VerifyUnavailable;     // 数据分卷缺失，无法校验（但条目本身可信）
        public bool InMapsDir;             // 是否位于 maps/ 目录下
        public string GroupKey = "";
        public string GroupTitle = "";
    }

    /// <summary>单个文件的解析状态</summary>
    public class FileStatus
    {
        public string Path = "";
        public string Name = "";
        public bool Ok;
        public string Message = "";
        public int MapCount;
        public int EntryCount;
        public double SizeMB;
    }

    public class ParseResult
    {
        public List<MapItem> Maps = new List<MapItem>();
        public List<FileStatus> Files = new List<FileStatus>();
        public List<string> Groups = new List<string>();   // 分组标题，按显示顺序
        public bool AnyCampaignFound;
    }

    /// <summary>从多个 VPK 中收集地图并排序</summary>
    public static class MapCollector
    {
        private static readonly Regex RxChapterMap = new Regex(@"^c(\d+)m(\d+)", RegexOptions.IgnoreCase);
        private static readonly Regex RxTailNumber = new Regex(@"(\d+)\s*$");

        public static ParseResult Collect(List<string> files, SortMode mode, bool verify)
        {
            ParseResult res = new ParseResult();

            List<MapItem> all = new List<MapItem>();
            Dictionary<string, string> authorOrder = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> authorDisplay = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> authorCampaign = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> authorCampaignTitle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string f in files)
            {
                FileStatus st = new FileStatus();
                st.Path = f;
                st.Name = Path.GetFileName(f);
                try
                {
                    st.SizeMB = new FileInfo(f).Length / 1024.0 / 1024.0;
                }
                catch { st.SizeMB = 0; }

                Vpk v = Vpk.Parse(f);
                if (!v.Ok)
                {
                    st.Ok = false;
                    st.Message = v.Error;
                    string hint = DetectDataVolume(f);
                    if (hint != null) st.Message = hint;
                    res.Files.Add(st);
                    continue;
                }
                st.Ok = true;
                st.EntryCount = v.Entries.Count;
                st.Message = "解析成功";
                int sibs = v.SiblingArchiveCount;
                if (sibs > 0) st.Message = "解析成功（已自动关联 " + sibs + " 个数据分卷）";

                // ---- 读取战役定义 ----
                foreach (VpkEntry e in v.Entries)
                {
                    if (!e.DirName.StartsWith("missions", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.Equals(e.Ext, "txt", StringComparison.OrdinalIgnoreCase)) continue;
                    string txt = v.ReadEntryText(e, 262144);
                    CampaignInfo ci = CampaignInfo.Parse(txt, st.Name);
                    if (ci == null) continue;
                    res.AnyCampaignFound = true;
                    foreach (MissionMap mm in ci.Maps)
                    {
                        authorOrder[mm.MapName] = mm.Order.ToString();
                        authorDisplay[mm.MapName] = mm.DisplayName;
                        if (ci.Name.Length > 0) authorCampaign[mm.MapName] = ci.Name;
                        if (ci.Title.Length > 0) authorCampaignTitle[mm.MapName] = ci.Title;
                    }
                }

                // ---- 收集 .bsp ----
                int fileMapCount = 0;
                foreach (VpkEntry e in v.Entries)
                {
                    if (!e.IsBsp) continue;
                    bool inMaps = string.Equals(e.DirName, "maps", StringComparison.OrdinalIgnoreCase);
                    if (!inMaps && e.DirName.EndsWith("/maps", StringComparison.OrdinalIgnoreCase)) inMaps = true;

                    MapItem mi = new MapItem();
                    mi.MapName = e.Name;
                    mi.SourceFile = st.Name;
                    mi.Offset = e.Offset;
                    mi.Length = e.Length;
                    mi.InMapsDir = inMaps;

                    if (verify)
                    {
                        string vf;
                        long vp;
                        if (!v.TryLocate(e, out vf, out vp))
                        {
                            // 数据分卷缺失：条目本身来自目录树，仍然可信，只是无法校验
                            mi.VerifyUnavailable = true;
                        }
                        else
                        {
                            mi.Verified = v.VerifyBsp(e);
                            if (!mi.Verified) continue;   // 校验不通过 → 确实不是地图
                        }
                    }

                    if (authorOrder.ContainsKey(e.Name))
                    {
                        mi.HasAuthorOrder = true;
                        mi.AuthorOrder = int.Parse(authorOrder[e.Name]);
                        mi.DisplayName = authorDisplay.ContainsKey(e.Name) ? authorDisplay[e.Name] : "";
                        mi.CampaignName = authorCampaign.ContainsKey(e.Name) ? authorCampaign[e.Name] : "";
                        mi.CampaignTitle = authorCampaignTitle.ContainsKey(e.Name) ? authorCampaignTitle[e.Name] : "";
                    }

                    ParseNameOrder(mi);
                    all.Add(mi);
                    fileMapCount++;
                }
                st.MapCount = fileMapCount;
                if (fileMapCount == 0) st.Message = "解析成功（未找到地图 .bsp）";
                else st.Message = "解析成功，找到 " + fileMapCount + " 张地图";
                if (sibs > 0) st.Message += "，已自动关联 " + sibs + " 个数据分卷";
                res.Files.Add(st);
            }

            // ---- 去重（同名地图保留第一条）----
            List<MapItem> uniq = new List<MapItem>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < all.Count; i++)
            {
                if (!seen.Add(all[i].MapName)) continue;
                uniq.Add(all[i]);
            }

            // ---- 分组 ----
            Dictionary<string, List<MapItem>> groups = new Dictionary<string, List<MapItem>>(StringComparer.OrdinalIgnoreCase);
            foreach (MapItem mi in uniq)
            {
                string gk;
                string gt;
                if (mi.CampaignName.Length > 0)
                {
                    gk = "campaign:" + mi.CampaignName.ToLowerInvariant();
                    gt = mi.CampaignTitle.Length > 0 ? mi.CampaignTitle : mi.CampaignName;
                }
                else
                {
                    gk = "prefix:" + NamePrefix(mi.MapName).ToLowerInvariant();
                    gt = NamePrefix(mi.MapName);
                }
                mi.GroupKey = gk;
                mi.GroupTitle = gt;
                if (!groups.ContainsKey(gk)) groups[gk] = new List<MapItem>();
                groups[gk].Add(mi);
            }

            // ---- 排序 ----
            foreach (KeyValuePair<string, List<MapItem>> kv in groups)
                SortGroup(kv.Value, mode);

            // 组间排序：按组内第一张图的排序键
            List<string> gkeys = new List<string>(groups.Keys);
            gkeys.Sort(delegate (string a, string b)
            {
                MapItem ma = groups[a][0];
                MapItem mb = groups[b][0];
                int c = CompareMap(ma, mb, mode);
                if (c != 0) return c;
                return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });

            foreach (string gk in gkeys)
            {
                List<MapItem> list = groups[gk];
                string title = list[0].GroupTitle;
                if (title.Length == 0) title = gk;
                res.Groups.Add(title);
                res.Maps.AddRange(list);
            }

            return res;
        }

        private static void SortGroup(List<MapItem> list, SortMode mode)
        {
            if (mode == SortMode.ByOffset)
            {
                list.Sort(delegate (MapItem a, MapItem b) { return a.Offset.CompareTo(b.Offset); });
                return;
            }
            if (mode == SortMode.ByName)
            {
                list.Sort(delegate (MapItem a, MapItem b) { return NaturalCompare(a.MapName, b.MapName); });
                return;
            }
            // 自动
            list.Sort(delegate (MapItem a, MapItem b) { return CompareMap(a, b, mode); });
        }

        private static int CompareMap(MapItem a, MapItem b, SortMode mode)
        {
            if (mode == SortMode.ByOffset) return a.Offset.CompareTo(b.Offset);
            if (mode == SortMode.ByName) return NaturalCompare(a.MapName, b.MapName);

            // 自动：有 missions 权威顺序的排前面
            if (a.HasAuthorOrder != b.HasAuthorOrder) return a.HasAuthorOrder ? -1 : 1;
            if (a.HasAuthorOrder && b.HasAuthorOrder)
            {
                int c = a.AuthorOrder.CompareTo(b.AuthorOrder);
                if (c != 0) return c;
            }
            int c2 = a.Chapter.CompareTo(b.Chapter);
            if (c2 != 0) return c2;
            int c3 = a.Level.CompareTo(b.Level);
            if (c3 != 0) return c3;
            return a.Offset.CompareTo(b.Offset);
        }

        /// <summary>
        /// 识别"数据分卷"文件（*_001.vpk 这类没有目录树的纯数据文件），
        /// 并提示用户应该把哪个 _dir.vpk 一起拖进来。返回 null 表示不是分卷。
        /// </summary>
        public static string DetectDataVolume(string path)
        {
            try
            {
                string fn = Path.GetFileNameWithoutExtension(path);
                int us = fn.LastIndexOf('_');
                if (us <= 0) return null;
                ushort idx;
                if (!ushort.TryParse(fn.Substring(us + 1), out idx)) return null;

                string dir = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(dir)) dir = ".";
                string dirFile = Path.Combine(dir, fn.Substring(0, us) + "_dir.vpk");
                if (File.Exists(dirFile))
                    return "这是数据分卷 " + Path.GetFileName(path) +
                           "，地图目录在 " + Path.GetFileName(dirFile) +
                           " 里 —— 请把 " + Path.GetFileName(dirFile) + " 一起拖进来";
                return "这看起来是数据分卷 " + Path.GetFileName(path) +
                       "（不含目录树），无法单独解析出关卡";
            }
            catch { return null; }
        }

        /// <summary>从地图名推断章节号与关卡号</summary>
        private static void ParseNameOrder(MapItem mi)
        {
            Match m = RxChapterMap.Match(mi.MapName);
            if (m.Success)
            {
                mi.Chapter = int.Parse(m.Groups[1].Value);
                mi.Level = int.Parse(m.Groups[2].Value);
                return;
            }
            m = RxTailNumber.Match(mi.MapName);
            if (m.Success)
            {
                mi.Chapter = 0;
                mi.Level = int.Parse(m.Groups[1].Value);
                return;
            }
            mi.Chapter = 0;
            mi.Level = 0;
        }

        /// <summary>取地图名的公共前缀（去掉尾部数字），用于无战役定义时分组</summary>
        private static string NamePrefix(string name)
        {
            int i = name.Length;
            while (i > 0 && (char.IsDigit(name[i - 1]) || name[i - 1] == '_')) i--;
            if (i == name.Length) return name;
            if (i == 0) return name;
            return name.Substring(0, i);
        }

        /// <summary>自然排序：数字段按数值比较</summary>
        public static int NaturalCompare(string a, string b)
        {
            int i = 0, j = 0;
            while (i < a.Length && j < b.Length)
            {
                char ca = a[i], cb = b[j];
                if (char.IsDigit(ca) && char.IsDigit(cb))
                {
                    int si = i, sj = j;
                    while (i < a.Length && char.IsDigit(a[i])) i++;
                    while (j < b.Length && char.IsDigit(b[j])) j++;
                    string na = a.Substring(si, i - si).TrimStart('0');
                    string nb = b.Substring(sj, j - sj).TrimStart('0');
                    if (na.Length != nb.Length) return na.Length - nb.Length;
                    int r = string.CompareOrdinal(na, nb);
                    if (r != 0) return r;
                    continue;
                }
                int r2 = char.ToLowerInvariant(ca).CompareTo(char.ToLowerInvariant(cb));
                if (r2 != 0) return r2;
                i++; j++;
            }
            return (a.Length - i) - (b.Length - j);
        }
    }
}
