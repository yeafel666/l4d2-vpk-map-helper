using System;
using System.Collections.Generic;
using System.Text;

namespace L4D2Vpk
{
    /// <summary>战役定义中的一关</summary>
    public class MissionMap
    {
        public string MapName = "";
        public string DisplayName = "";
        public int Order;          // 在 missions 文件里的序号（1 起）
    }

    /// <summary>从 missions/*.txt 提取出的战役信息</summary>
    public class CampaignInfo
    {
        public string Name = "";           // 内部代号，如 prodeus
        public string Title = "";          // 显示标题，如 Prodeus Requiem
        public string Description = "";
        public string SourceFile = "";
        public List<MissionMap> Maps = new List<MissionMap>();

        /// <summary>从 missions 文本解析战役定义；失败返回 null</summary>
        public static CampaignInfo Parse(string text, string sourceFile)
        {
            KVNode root;
            try { root = KeyValues.Parse(text); }
            catch { return null; }
            if (root == null) return null;

            KVNode mission = root.Find("mission");
            if (mission == null) mission = root;

            CampaignInfo ci = new CampaignInfo();
            ci.SourceFile = sourceFile;
            ci.Name = mission.ValueOf("Name");
            ci.Title = mission.ValueOf("DisplayTitle");
            ci.Description = mission.ValueOf("Description");

            KVNode modes = mission.Find("modes");
            if (modes != null)
            {
                // 优先 coop（合作模式），其次 versus
                KVNode mode = modes.Find("coop");
                if (mode == null) mode = modes.Find("versus");
                if (mode == null) mode = modes.Find("l4d1");
                if (mode != null) ReadMode(mode, ci);
            }
            if (ci.Maps.Count == 0)
            {
                // 兜底：直接找顶层带 "Map" 的数字键
                ReadMode(mission, ci);
            }
            return ci;
        }

        private static void ReadMode(KVNode mode, CampaignInfo ci)
        {
            List<MissionMap> found = new List<MissionMap>();
            foreach (KVNode c in mode.Children)
            {
                int ord;
                if (!int.TryParse(c.Key, out ord)) continue;
                if (!c.IsBlock) continue;

                string map = c.ValueOf("Map");
                if (map.Length == 0) continue;

                MissionMap mm = new MissionMap();
                mm.MapName = map;
                mm.DisplayName = c.ValueOf("DisplayName");
                mm.Order = ord;
                found.Add(mm);
            }
            found.Sort(delegate (MissionMap a, MissionMap b) { return a.Order.CompareTo(b.Order); });
            ci.Maps.AddRange(found);
        }
    }
}
