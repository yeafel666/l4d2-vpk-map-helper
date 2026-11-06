using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using L4D2Vpk;

class MainForm : Form
{
    private Panel dropPanel;
    private Label dropLabel;
    private ListView fileList;
    private ListView resultList;
    private ComboBox sortCombo;
    private CheckBox verifyBox;
    private Label statLabel;
    private Button btnCopyAll;
    private Button btnExport;
    private Button btnClear;
    private Button btnRemove;
    private Button btnBrowse;
    private ToolTip tip;

    private readonly List<string> loadedFiles = new List<string>();
    private ParseResult lastResult;

    private static readonly Color BackColorMain = Color.FromArgb(245, 246, 248);
    private static readonly Color Accent = Color.FromArgb(0, 120, 215);

    public MainForm()
    {
        BuildUi();
    }

    private void BuildUi()
    {
        Text = "求生之路2 开图指令助手 - Yeafel制作";
        ClientSize = new Size(1000, 740);
        MinimumSize = new Size(860, 640);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BackColorMain;
        Font = new Font("Microsoft YaHei UI", 9F);
        // 高 DPI 显示器下整体等比放大，避免固定像素的行高/坐标裁切文字
        // 用 Dpi 模式而非 Font 模式：Font 模式会按中文字体度量算出 >1 的因子，
        // 在 96 DPI 机器上也把窗口放大 18%，反而挤掉屏幕空间
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        AllowDrop = true;
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
        DragLeave += OnDragLeave;

        // ---------- 标题 ----------
        // 先建好两个 Label，用 AutoSize 后的"实际字体高度"推出容器高度与副标题 y 坐标，
        // 不再硬编码像素 —— 原来的 46px 容器放不下 15F 粗体标题 + 副标题，
        // 副标题被压在标题上、底部又被裁掉半行（用户反馈"这行字显示不全"）
        Label title = new Label();
        title.Text = "求生之路2 · 第三方战役开图指令助手";
        title.Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold);
        title.ForeColor = Color.FromArgb(32, 38, 48);
        title.AutoSize = true;

        Label sub = new Label();
        sub.Text = "把战役 VPK 拖进来 → 自动解析出每一关的 map 开图指令（按第一关到最后一关排序）";
        sub.ForeColor = Color.FromArgb(110, 118, 130);
        sub.AutoSize = true;

        int titleH = Math.Max(title.Height, title.PreferredHeight);
        int subH = Math.Max(sub.Height, sub.PreferredHeight);
        int headerH = titleH + subH + 10;      // 标题 + 副标题 + 上下留白

        TableLayoutPanel root = new TableLayoutPanel();
        root.Dock = DockStyle.Fill;
        root.ColumnCount = 1;
        root.RowCount = 6;
        root.Padding = new Padding(10);
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, headerH)); // 标题
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132F));  // 拖放区
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 148F));  // 文件列表
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));   // 排序栏
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));   // 结果
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));   // 按钮栏
        Controls.Add(root);

        Panel titlePanel = new Panel();
        titlePanel.Dock = DockStyle.Fill;
        title.Location = new Point(2, 0);
        titlePanel.Controls.Add(title);
        sub.Location = new Point(4, titleH + 2);
        titlePanel.Controls.Add(sub);
        root.Controls.Add(titlePanel, 0, 0);

        // ---------- 拖放区 ----------
        dropPanel = new Panel();
        dropPanel.Dock = DockStyle.Fill;
        dropPanel.BackColor = Color.White;
        dropPanel.AllowDrop = true;
        dropPanel.DragEnter += OnDragEnter;
        dropPanel.DragDrop += OnDragDrop;
        dropPanel.DragLeave += OnDragLeave;
        dropPanel.Paint += OnDropPanelPaint;

        dropLabel = new Label();
        dropLabel.Dock = DockStyle.Fill;
        dropLabel.TextAlign = ContentAlignment.MiddleCenter;
        dropLabel.Text =
            "把 .vpk 文件拖到这里\r\n\r\n" +
            "★ 可以一次拖入多个 VPK 文件 —— 同一张大地图常被拆成多个 VPK，会自动合并解析\r\n" +
            "（也可以直接拖到本程序的图标上；或用右下角「浏览添加」按钮）";
        dropLabel.ForeColor = Color.FromArgb(120, 128, 140);
        dropLabel.Font = new Font("Microsoft YaHei UI", 10F);
        dropPanel.Controls.Add(dropLabel);
        root.Controls.Add(dropPanel, 0, 1);

        // ---------- 文件列表 ----------
        TableLayoutPanel fileRow = new TableLayoutPanel();
        fileRow.Dock = DockStyle.Fill;
        fileRow.ColumnCount = 2;
        fileRow.RowCount = 1;
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        fileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        fileRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        fileList = new ListView();
        fileList.Dock = DockStyle.Fill;
        fileList.View = View.Details;
        fileList.FullRowSelect = true;
        fileList.HideSelection = false;
        fileList.MultiSelect = true;
        fileList.GridLines = true;
        fileList.BorderStyle = BorderStyle.FixedSingle;
        fileList.AllowDrop = true;
        fileList.DragEnter += OnDragEnter;
        fileList.DragDrop += OnDragDrop;
        fileList.DragLeave += OnDragLeave;
        fileList.Columns.Add("已载入的 VPK 文件", 200);
        fileList.Columns.Add("状态", 250);
        fileList.Columns.Add("地图数", 65);
        fileList.Columns.Add("大小(MB)", 85);
        fileList.Columns.Add("完整路径", 300);
        fileRow.Controls.Add(fileList, 0, 0);

        Panel fileBtns = new Panel();
        fileBtns.Dock = DockStyle.Fill;
        btnBrowse = new Button();
        btnBrowse.Text = "浏览添加...";
        btnBrowse.Dock = DockStyle.Top;
        btnBrowse.Height = 30;
        btnBrowse.Click += OnBrowse;
        fileBtns.Controls.Add(btnBrowse);
        btnRemove = new Button();
        btnRemove.Text = "移除选中";
        btnRemove.Dock = DockStyle.Top;
        btnRemove.Height = 30;
        btnRemove.Click += OnRemoveSelected;
        fileBtns.Controls.Add(btnRemove);
        fileRow.Controls.Add(fileBtns, 1, 0);
        root.Controls.Add(fileRow, 0, 2);

        // ---------- 排序栏 ----------
        Panel sortRow = new Panel();
        sortRow.Dock = DockStyle.Fill;
        Label sortLabel = new Label();
        sortLabel.Text = "排序方式：";
        sortLabel.AutoSize = true;
        sortLabel.Location = new Point(2, 9);
        sortRow.Controls.Add(sortLabel);

        sortCombo = new ComboBox();
        sortCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        sortCombo.Location = new Point(78, 5);
        sortCombo.Size = new Size(170, 24);
        sortCombo.Items.Add("自动（推荐）");
        sortCombo.Items.Add("按关卡名");
        sortCombo.Items.Add("按 VPK 内顺序");
        sortCombo.SelectedIndex = 0;
        sortCombo.SelectedIndexChanged += delegate { RefreshResults(); };
        sortRow.Controls.Add(sortCombo);

        verifyBox = new CheckBox();
        verifyBox.Text = "严格校验（只认 VBSP 魔数正确的 .bsp）";
        verifyBox.Checked = true;
        verifyBox.AutoSize = true;
        verifyBox.Location = new Point(262, 9);
        verifyBox.CheckedChanged += delegate { RefreshResults(); };
        sortRow.Controls.Add(verifyBox);

        statLabel = new Label();
        statLabel.Text = "";
        statLabel.AutoSize = true;
        statLabel.ForeColor = Color.FromArgb(110, 118, 130);
        statLabel.Location = new Point(500, 9);
        sortRow.Controls.Add(statLabel);
        root.Controls.Add(sortRow, 0, 3);

        // ---------- 结果 ----------
        resultList = new ListView();
        resultList.Dock = DockStyle.Fill;
        resultList.View = View.Details;
        resultList.FullRowSelect = true;
        resultList.GridLines = true;
        resultList.HideSelection = false;
        resultList.BorderStyle = BorderStyle.FixedSingle;
        resultList.AllowDrop = true;
        resultList.DragEnter += OnDragEnter;
        resultList.DragDrop += OnDragDrop;
        resultList.DragLeave += OnDragLeave;
        resultList.Columns.Add("序号", 60);
        resultList.Columns.Add("开图指令（复制这个）", 250);
        resultList.Columns.Add("关卡名", 200);
        resultList.Columns.Add("所属战役", 200);
        resultList.Columns.Add("来源文件", 260);
        resultList.DoubleClick += OnResultDoubleClick;
        root.Controls.Add(resultList, 0, 4);

        // ---------- 按钮栏 ----------
        Panel btnRow = new Panel();
        btnRow.Dock = DockStyle.Fill;
        btnCopyAll = new Button();
        btnCopyAll.Text = "复制全部开图指令";
        btnCopyAll.Size = new Size(160, 34);
        btnCopyAll.Location = new Point(2, 7);
        btnCopyAll.BackColor = Accent;
        btnCopyAll.ForeColor = Color.White;
        btnCopyAll.FlatStyle = FlatStyle.Flat;
        btnCopyAll.FlatAppearance.BorderSize = 0;
        btnCopyAll.Click += OnCopyAll;
        btnRow.Controls.Add(btnCopyAll);

        btnExport = new Button();
        btnExport.Text = "导出 TXT 报告";
        btnExport.Size = new Size(130, 34);
        btnExport.Location = new Point(170, 7);
        btnExport.Click += OnExport;
        btnRow.Controls.Add(btnExport);

        btnClear = new Button();
        btnClear.Text = "清空全部";
        btnClear.Size = new Size(110, 34);
        btnClear.Location = new Point(308, 7);
        btnClear.Click += OnClearAll;
        btnRow.Controls.Add(btnClear);

        tip = new ToolTip();
        tip.SetToolTip(btnCopyAll, "复制所有关卡的 map 指令，每行一条，可直接粘到服务器控制台");
        tip.SetToolTip(resultList, "双击任意一行可复制该关的开图指令");

        root.Controls.Add(btnRow, 0, 5);
    }

    // ================= 拖放 =================

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effect = DragDropEffects.Copy;
            dropPanel.BackColor = Color.FromArgb(226, 240, 255);
            dropLabel.ForeColor = Accent;
        }
        else
        {
            e.Effect = DragDropEffects.None;
        }
    }

    private void OnDragLeave(object sender, EventArgs e)
    {
        dropPanel.BackColor = Color.White;
        dropLabel.ForeColor = Color.FromArgb(120, 128, 140);
    }

    private void OnDragDrop(object sender, DragEventArgs e)
    {
        dropPanel.BackColor = Color.White;
        dropLabel.ForeColor = Color.FromArgb(120, 128, 140);

        string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (paths == null) return;
        AddFiles(paths);
    }

    private void OnDropPanelPaint(object sender, PaintEventArgs e)
    {
        using (Pen pen = new Pen(Color.FromArgb(170, 190, 215)))
        {
            pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
            Rectangle r = dropPanel.ClientRectangle;
            r.Inflate(-3, -3);
            e.Graphics.DrawRectangle(pen, r);
        }
    }

    // ================= 文件管理 =================

    public void AddFiles(string[] paths)
    {
        List<string> vpk = new List<string>();
        List<string> ignored = new List<string>();
        int fromFolders = 0;

        foreach (string p in paths)
        {
            if (string.IsNullOrEmpty(p)) continue;

            // 拖进来的是文件夹 → 递归找里面的 .vpk
            if (Directory.Exists(p))
            {
                string[] found;
                try { found = Directory.GetFiles(p, "*.vpk", SearchOption.AllDirectories); }
                catch { found = new string[0]; }
                Array.Sort(found, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < found.Length; i++)
                {
                    if (loadedFiles.Contains(found[i]) || vpk.Contains(found[i])) continue;
                    vpk.Add(found[i]);
                }
                fromFolders += found.Length;
                continue;
            }

            if (!File.Exists(p)) { ignored.Add(p + "（不存在）"); continue; }
            if (!p.ToLowerInvariant().EndsWith(".vpk")) { ignored.Add(Path.GetFileName(p) + "（不是 .vpk）"); continue; }
            if (loadedFiles.Contains(p)) continue;
            vpk.Add(p);
        }

        if (vpk.Count == 0)
        {
            if (ignored.Count > 0)
                MessageBox.Show(this, "以下文件未加入：\r\n" + string.Join("\r\n", ignored.ToArray()),
                                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        loadedFiles.AddRange(vpk);
        RefreshResults();

        if (fromFolders > 0 && vpk.Count > 0)
            statLabel.Text = statLabel.Text + "（含从文件夹中找到的 VPK）";
    }

    private void OnBrowse(object sender, EventArgs e)
    {
        using (OpenFileDialog dlg = new OpenFileDialog())
        {
            dlg.Title = "选择第三方战役的 VPK 文件（可多选）";
            dlg.Filter = "VPK 文件 (*.vpk)|*.vpk|所有文件 (*.*)|*.*";
            dlg.Multiselect = true;
            if (dlg.ShowDialog(this) == DialogResult.OK) AddFiles(dlg.FileNames);
        }
    }

    private void OnRemoveSelected(object sender, EventArgs e)
    {
        if (fileList.SelectedItems.Count == 0) return;
        List<string> kill = new List<string>();
        foreach (ListViewItem it in fileList.SelectedItems) kill.Add(it.Tag as string);
        for (int i = loadedFiles.Count - 1; i >= 0; i--)
            if (kill.Contains(loadedFiles[i])) loadedFiles.RemoveAt(i);
        RefreshResults();
    }

    private void OnClearAll(object sender, EventArgs e)
    {
        loadedFiles.Clear();
        RefreshResults();
    }

    // ================= 解析与显示 =================

    private void RefreshResults()
    {
        fileList.Items.Clear();
        resultList.Items.Clear();
        resultList.Groups.Clear();
        lastResult = null;

        if (loadedFiles.Count == 0)
        {
            statLabel.Text = "";
            btnCopyAll.Enabled = false;
            btnExport.Enabled = false;
            return;
        }
        btnCopyAll.Enabled = true;
        btnExport.Enabled = true;

        SortMode mode = (SortMode)sortCombo.SelectedIndex;
        bool verify = verifyBox.Checked;

        Cursor old = Cursor;
        Cursor = Cursors.WaitCursor;
        ParseResult res;
        try
        {
            res = MapCollector.Collect(loadedFiles, mode, verify);
        }
        catch (Exception ex)
        {
            Cursor = old;
            MessageBox.Show(this, "解析失败：\r\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        finally
        {
            Cursor = old;
        }
        lastResult = res;

        foreach (FileStatus st in res.Files)
        {
            ListViewItem it = new ListViewItem(st.Name);
            it.SubItems.Add(st.Ok ? st.Message : "✗ " + st.Message);
            it.SubItems.Add(st.MapCount > 0 ? st.MapCount.ToString() : "-");
            it.SubItems.Add(st.SizeMB.ToString("0.0"));
            it.SubItems.Add(st.Path);
            it.Tag = st.Path;
            it.ToolTipText = st.Path;
            if (!st.Ok) it.ForeColor = Color.FromArgb(190, 40, 40);
            else if (st.MapCount == 0) it.ForeColor = Color.FromArgb(150, 120, 20);
            fileList.Items.Add(it);
        }

        Dictionary<string, ListViewGroup> groupMap = new Dictionary<string, ListViewGroup>(StringComparer.OrdinalIgnoreCase);
        int idx = 0;
        string curGroup = null;
        foreach (MapItem mi in res.Maps)
        {
            if (mi.GroupKey != curGroup)
            {
                curGroup = mi.GroupKey;
                if (!groupMap.ContainsKey(curGroup))
                {
                    ListViewGroup g = new ListViewGroup(mi.GroupTitle);
                    groupMap[curGroup] = g;
                    resultList.Groups.Add(g);
                }
            }
            idx++;
            ListViewItem it = new ListViewItem(idx.ToString());
            it.SubItems.Add("map " + mi.MapName);
            it.SubItems.Add(mi.DisplayName);
            it.SubItems.Add(mi.CampaignTitle.Length > 0 ? mi.CampaignTitle : mi.CampaignName);
            it.SubItems.Add(mi.SourceFile);
            it.Tag = "map " + mi.MapName;
            if (mi.DisplayName.Length > 0) it.ForeColor = Color.FromArgb(32, 38, 48);
            if (mi.VerifyUnavailable)
            {
                it.ForeColor = Color.FromArgb(170, 110, 10);
                it.ToolTipText = "该地图的数据在独立分卷里，当前没有找到那个分卷文件，\r\n" +
                                 "所以没能校验文件头。把分卷文件一起拖进来即可完成校验。";
            }
            resultList.Items.Add(it);
            if (groupMap.ContainsKey(curGroup)) resultList.Items[resultList.Items.Count - 1].Group = groupMap[curGroup];
        }

        int total = res.Maps.Count;
        int gcount = groupMap.Count;
        statLabel.Text = total > 0
            ? "共解析出 " + total + " 张地图 / " + gcount + " 个战役"
            : "已载入 " + loadedFiles.Count + " 个 VPK，但没有解析出地图";
    }

    // ================= 输出 =================

    private void OnCopyAll(object sender, EventArgs e)
    {
        if (lastResult == null || lastResult.Maps.Count == 0) return;
        StringBuilder sb = new StringBuilder();
        foreach (MapItem mi in lastResult.Maps)
            sb.Append("map ").Append(mi.MapName).Append("\r\n");
        CopyToClipboard(sb.ToString(), "已复制 " + lastResult.Maps.Count + " 条开图指令");
    }

    private void OnResultDoubleClick(object sender, EventArgs e)
    {
        if (resultList.SelectedItems.Count == 0) return;
        string cmd = resultList.SelectedItems[0].Tag as string;
        if (string.IsNullOrEmpty(cmd)) return;
        CopyToClipboard(cmd, "已复制：" + cmd);
    }

    private void CopyToClipboard(string text, string okMsg)
    {
        try
        {
            Clipboard.SetText(text);
            MessageBox.Show(this, okMsg, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "复制失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnExport(object sender, EventArgs e)
    {
        if (lastResult == null || lastResult.Maps.Count == 0) return;
        using (SaveFileDialog dlg = new SaveFileDialog())
        {
            dlg.Title = "导出开图指令报告";
            dlg.Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*";
            dlg.FileName = "开图指令.txt";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("求生之路2 第三方战役开图指令");
            sb.AppendLine("生成时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("来源文件: " + string.Join(", ", loadedFiles.ToArray()));
            sb.AppendLine(new string('=', 60));

            string cur = null;
            int n = 0;
            foreach (MapItem mi in lastResult.Maps)
            {
                if (mi.GroupKey != cur)
                {
                    cur = mi.GroupKey;
                    sb.AppendLine();
                    sb.AppendLine("【" + mi.GroupTitle + "】");
                }
                n++;
                sb.AppendLine("第" + n + "关   map " + mi.MapName +
                              (mi.DisplayName.Length > 0 ? "    " + mi.DisplayName : "") +
                              "    (" + mi.SourceFile + ")");
            }
            sb.AppendLine();
            sb.AppendLine(new string('=', 60));
            sb.AppendLine("纯指令版（可直接复制到服务器控制台）:");
            sb.AppendLine();
            foreach (MapItem mi in lastResult.Maps)
                sb.AppendLine("map " + mi.MapName);

            try
            {
                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show(this, "已导出到：\r\n" + dlg.FileName, "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "导出失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
