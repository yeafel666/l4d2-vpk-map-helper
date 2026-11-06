using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

class IconGen
{
    static void Main(string[] args)
    {
        string outPath = args[0];
        int[] sizes = new int[] { 16, 24, 32, 48, 64, 256 };

        List<byte[]> pngs = new List<byte[]>();
        foreach (int s in sizes)
        {
            using (Bitmap bmp = new Bitmap(s, s))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                Draw(g, s);
                using (MemoryStream ms = new MemoryStream())
                {
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    pngs.Add(ms.ToArray());
                }
            }
        }

        using (FileStream fs = new FileStream(outPath, FileMode.Create, FileAccess.Write))
        using (BinaryWriter w = new BinaryWriter(fs))
        {
            // ICONDIR
            w.Write((ushort)0);           // reserved
            w.Write((ushort)1);           // type = icon
            w.Write((ushort)sizes.Length);

            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));  // width
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));  // height
                w.Write((byte)0);           // colors
                w.Write((byte)0);           // reserved
                w.Write((ushort)1);         // planes
                w.Write((ushort)32);        // bitcount
                w.Write(pngs[i].Length);    // bytes
                w.Write(offset);
                offset += pngs[i].Length;
            }
            for (int i = 0; i < sizes.Length; i++) w.Write(pngs[i]);
        }
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("图标已生成: " + outPath + "  (" + new FileInfo(outPath).Length + " 字节)");
    }

    static void Draw(Graphics g, int s)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        g.Clear(Color.Transparent);

        float r = s * 0.18f;
        RectangleF rect = new RectangleF(s * 0.04f, s * 0.04f, s * 0.92f, s * 0.92f);

        using (GraphicsPath path = RoundedRect(rect, r))
        using (LinearGradientBrush br = new LinearGradientBrush(rect,
                   Color.FromArgb(30, 120, 220), Color.FromArgb(10, 70, 160), 45f))
        {
            g.FillPath(br, path);
        }

        // 三条白色"关卡列表"横条
        float pad = s * 0.22f;
        float bh = Math.Max(1f, s * 0.11f);
        float gap = s * 0.20f;
        float[] wfrac = new float[] { 0.56f, 0.44f, 0.32f };
        for (int i = 0; i < 3; i++)
        {
            float y = pad + i * gap;
            float bw = (s - 2 * pad) * wfrac[i];
            RectangleF bar = new RectangleF(pad, y, bw, bh);
            using (GraphicsPath bp = RoundedRect(bar, bh / 2f))
            using (SolidBrush bb = new SolidBrush(Color.FromArgb(235, 245, 255)))
            {
                g.FillPath(bb, bp);
            }
        }

        // 右下角小圆点（代表"复制/指令"）
        if (s >= 24)
        {
            float d = s * 0.20f;
            using (SolidBrush gb = new SolidBrush(Color.FromArgb(90, 220, 120)))
                g.FillEllipse(gb, s - pad - d + s * 0.06f, s - pad - d + s * 0.02f, d, d);
        }
    }

    static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        GraphicsPath p = new GraphicsPath();
        float d = radius * 2f;
        if (d > r.Width) d = r.Width;
        if (d > r.Height) d = r.Height;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
