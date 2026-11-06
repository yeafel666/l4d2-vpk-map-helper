using System;
using System.Collections.Generic;
using System.Text;

namespace L4D2Vpk
{
    /// <summary>Valve KeyValues 格式节点</summary>
    public class KVNode
    {
        public string Key = "";
        public string Value = "";
        public bool IsBlock;
        public List<KVNode> Children = new List<KVNode>();

        public KVNode Find(string key)
        {
            for (int i = 0; i < Children.Count; i++)
                if (string.Equals(Children[i].Key, key, StringComparison.OrdinalIgnoreCase))
                    return Children[i];
            return null;
        }

        public string ValueOf(string key)
        {
            KVNode n = Find(key);
            if (n == null) return "";
            return n.IsBlock ? "" : n.Value;
        }
    }

    /// <summary>KeyValues 文本解析器（用于 missions/*.txt 战役定义）</summary>
    public static class KeyValues
    {
        public static KVNode Parse(string text)
        {
            List<string> tk = Tokenize(text);
            int p = 0;
            return ParseBlock(tk, ref p);
        }

        private static KVNode ParseBlock(List<string> tk, ref int p)
        {
            KVNode node = new KVNode();
            node.IsBlock = true;
            while (p < tk.Count)
            {
                string t = tk[p];
                if (t == "}") { p++; break; }
                if (t == "{") { p++; continue; }

                string key = t;
                p++;

                if (p < tk.Count && tk[p] == "{")
                {
                    p++;
                    KVNode child = ParseBlock(tk, ref p);
                    child.Key = key;
                    node.Children.Add(child);
                }
                else if (p < tk.Count)
                {
                    KVNode leaf = new KVNode();
                    leaf.Key = key;
                    leaf.Value = tk[p];
                    leaf.IsBlock = false;
                    p++;
                    node.Children.Add(leaf);
                }
                else
                {
                    break;
                }
            }
            return node;
        }

        private static List<string> Tokenize(string s)
        {
            List<string> res = new List<string>();
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n') { i++; continue; }

                // 行注释 //
                if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
                {
                    while (i < s.Length && s[i] != '\n') i++;
                    continue;
                }
                // 块注释 /* */
                if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < s.Length && !(s[i] == '*' && s[i + 1] == '/')) i++;
                    i += 2;
                    continue;
                }
                if (c == '{' || c == '}') { res.Add(c.ToString()); i++; continue; }

                if (c == '"')
                {
                    i++;
                    StringBuilder sb = new StringBuilder();
                    while (i < s.Length && s[i] != '"')
                    {
                        if (s[i] == '\\' && i + 1 < s.Length)
                        {
                            i++;
                            char e = s[i];
                            if (e == 'n') sb.Append('\n');
                            else if (e == 't') sb.Append('\t');
                            else sb.Append(e);
                            i++;
                        }
                        else { sb.Append(s[i]); i++; }
                    }
                    i++;  // 跳过收尾引号
                    res.Add(sb.ToString());
                    continue;
                }

                int st = i;
                while (i < s.Length && s[i] != ' ' && s[i] != '\t' && s[i] != '\r' && s[i] != '\n'
                       && s[i] != '{' && s[i] != '}' && s[i] != '"') i++;
                if (i > st) res.Add(s.Substring(st, i - st));
            }
            return res;
        }
    }
}
