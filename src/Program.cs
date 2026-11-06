using System;
using System.IO;
using System.Windows.Forms;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        MainForm form = new MainForm();

        // 命令行参数：支持把 vpk 直接拖到本程序图标上（必须在 Run 之前处理）
        if (args != null && args.Length > 0)
        {
            System.Collections.Generic.List<string> vpk = new System.Collections.Generic.List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (File.Exists(args[i]) && args[i].ToLowerInvariant().EndsWith(".vpk"))
                    vpk.Add(args[i]);
            }
            if (vpk.Count > 0) form.AddFiles(vpk.ToArray());
        }

        Application.Run(form);
    }
}
