using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Row = System.Collections.Generic.Dictionary<string, object>;

namespace PBZ.Native
{
    internal sealed class DetailsWindow : Form
    {
        readonly Label heading = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
        readonly Label current = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
        readonly TabControl tabs = new TabControl { Dock = DockStyle.Fill };
        readonly RichTextBox overview = Reader(), flow = Reader(), evidence = Reader();
        readonly TreeView outline = new TreeView { Dock = DockStyle.Fill, HideSelection = false, ShowLines = false, FullRowSelect = true, BorderStyle = BorderStyle.None };
        readonly TextBox search = new TextBox { Width = 180 };
        readonly NumericUpDown fontSize = new NumericUpDown { Minimum = 10, Maximum = 20, Value = 12, Width = 55 };
        readonly Label searchResult = new Label { AutoSize = true, Margin = new Padding(4, 7, 3, 0) };
        readonly Dictionary<string, Point> positions = new Dictionary<string, Point>();
        readonly List<int> stepOffsets = new List<int>();
        Row document;
        string documentKey;
        bool updating;
        public string DocumentId { get { return document.S("id"); } }
        public event EventHandler RefreshRequested;
        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        static extern IntPtr ScrollMessage(IntPtr window, int message, IntPtr unused, ref Point position);
        static RichTextBox Reader()
        {
            return new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, DetectUrls = true, BorderStyle = BorderStyle.None,
                BackColor = Color.White, ForeColor = Color.FromArgb(30, 40, 48), WordWrap = true, ScrollBars = RichTextBoxScrollBars.Vertical,
                Font = new Font("Microsoft YaHei UI", 12), HideSelection = false };
        }
        public DetailsWindow()
        {
            Text = "任务与节点检查详情";
            Font = new Font("Microsoft YaHei UI", 10);
            AutoScaleMode = AutoScaleMode.Dpi;
            Size = new Size(1080, 800);
            MinimumSize = new Size(760, 540);
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = true;
            BackColor = Color.FromArgb(242, 245, 247);
            heading.Font = new Font(Font.FontFamily, 15, FontStyle.Bold);
            var frame = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(14, 8, 14, 12) };
            frame.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            frame.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            frame.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            frame.Controls.Add(heading, 0, 0);
            frame.Controls.Add(current, 0, 1);
            frame.Controls.Add(tabs, 0, 2);
            var statusTab = new TabPage("当前情况") { Padding = new Padding(14), BackColor = Color.White };
            statusTab.Controls.Add(overview);
            var flowTab = new TabPage("完整流程") { Padding = new Padding(8), BackColor = Color.White };
            var evidenceTab = new TabPage("判断依据与来源") { Padding = new Padding(14), BackColor = Color.White };
            evidenceTab.Controls.Add(evidence);
            tabs.TabPages.AddRange(new[] { statusTab, flowTab, evidenceTab });
            var flowFrame = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            flowFrame.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            flowFrame.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
            var find = new Button { Text = "查找下一处", AutoSize = true };
            tools.Controls.Add(new Label { Text = "正文搜索", AutoSize = true, Margin = new Padding(0, 7, 3, 0) });
            tools.Controls.Add(search);
            tools.Controls.Add(find);
            tools.Controls.Add(new Label { Text = "字号", AutoSize = true, Margin = new Padding(10, 7, 3, 0) });
            tools.Controls.Add(fontSize);
            tools.Controls.Add(searchResult);
            var split = new SplitContainer { Dock = DockStyle.Fill, Width = 950, SplitterDistance = 250, Panel1MinSize = 140, Panel2MinSize = 260 };
            split.Panel1.Controls.Add(outline);
            var paper = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 8, 18, 8), BackColor = Color.White };
            paper.SizeChanged += delegate { int margin = Math.Max(18, (paper.ClientSize.Width - 840) / 2); paper.Padding = new Padding(margin, 8, margin, 8); };
            paper.Controls.Add(flow);
            split.Panel2.Controls.Add(paper);
            flowFrame.Controls.Add(tools, 0, 0);
            flowFrame.Controls.Add(split, 0, 1);
            flowTab.Controls.Add(flowFrame);
            Controls.Add(frame);
            tabs.SelectedIndex = 1;
            find.Click += delegate { FindNext(); };
            search.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; FindNext(); } };
            fontSize.ValueChanged += delegate { flow.ZoomFactor = overview.ZoomFactor = evidence.ZoomFactor = (float)fontSize.Value / 12; };
            outline.AfterSelect += delegate(object sender, TreeViewEventArgs e)
            {
                if (updating || e.Node == null) return;
                flow.Select((int)e.Node.Tag, 0);
                flow.ScrollToCaret();
            };
            foreach (RichTextBox reader in new[] { overview, flow, evidence }) reader.LinkClicked += delegate(object sender, LinkClickedEventArgs e) { Window.OpenLink(e.LinkText); };
            KeyPreview = true;
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Control && e.KeyCode == Keys.F) { e.SuppressKeyPress = true; tabs.SelectedIndex = 1; search.Focus(); search.SelectAll(); }
                else if (e.KeyCode == Keys.F5) { e.SuppressKeyPress = true; if (RefreshRequested != null) RefreshRequested(this, EventArgs.Empty); }
            };
        }
        public bool FindNext()
        {
            string query = search.Text.Trim();
            if (query == "") return false;
            int start = flow.SelectionStart + flow.SelectionLength;
            int found = flow.Find(query, start, RichTextBoxFinds.None);
            if (found < 0 && start > 0) found = flow.Find(query, 0, RichTextBoxFinds.None);
            searchResult.Text = found < 0 ? "未找到" : "";
            if (found >= 0) flow.ScrollToCaret();
            return found >= 0;
        }
        static string StepLabel(Row step)
        {
            return step.S("state") == "done" ? "已完成" : step.S("state") == "current" ? "正在进行" : step.S("state") == "pending" ? "未完成" : step.S("stateLabel");
        }
        static Color StepColor(Row step)
        {
            switch (step.S("state"))
            {
                case "done": return Color.FromArgb(39, 125, 58);
                case "current": return Color.FromArgb(153, 107, 0);
                case "failed": case "interrupted": return Color.FromArgb(163, 51, 41);
                default: return Color.FromArgb(107, 114, 128);
            }
        }
        void Append(string text, bool bold = false, Color? color = null, bool newLine = true)
        {
            flow.SelectionStart = flow.TextLength;
            flow.SelectionLength = 0;
            using (var font = new Font("Microsoft YaHei UI", bold ? 13 : 12, bold ? FontStyle.Bold : FontStyle.Regular)) flow.SelectionFont = font;
            flow.SelectionColor = color ?? Color.FromArgb(30, 40, 48);
            flow.AppendText(text + (newLine ? "\r\n" : ""));
        }
        public void ShowDocument(Row next, string context)
        {
            string key = context + "|" + next.S("id");
            if (object.ReferenceEquals(document, next) && documentKey == key) return;
            if (document != null && flow.IsHandleCreated)
            {
                Point previousPosition = Point.Empty;
                ScrollMessage(flow.Handle, 0x4dd, IntPtr.Zero, ref previousPosition); // EM_GETSCROLLPOS
                positions[documentKey] = previousPosition;
            }
            document = next; documentKey = key;
            Text = next.S("title") + " · 任务与节点检查详情";
            heading.Text = next.S("title") + "  ·  " + next.S("stateLabel");
            string reason = next.S("reason");
            current.ForeColor = reason == "" ? Color.FromArgb(30, 40, 48) : Color.FromArgb(163, 51, 41);
            current.Text = TaskDetails.Join(next.S("phase"), reason == "" ? (next.Flag("reference") ? "资料说明：" : "当前进度：") + next.S("next") : "失败／中断原因：" + reason.Split('\n')[0]);
            overview.Text = TaskDetails.Join(next.S("summary"), next.S("progress"), "", reason == "" ? "" : "失败／中断原因\r\n" + reason,
                next.S("recovery") == "" ? "" : "补救方式\r\n" + next.S("recovery"), next.S("impact") == "" ? "" : "影响\r\n" + next.S("impact"),
                "注意事项\r\n" + string.Join("\r\n", next.Strings("notes")), (next.Flag("reference") ? "浏览模式\r\n" : "所选存档与槽位\r\n") + context);
            evidence.Text = TaskDetails.Join("判断依据", next.S("evidence"), "逐步判定", string.Join("\r\n", next.Rows("steps").Select((s, i) =>
                (i + 1) + ". " + s.S("title") + " · " + s.S("stateLabel") + "\r\n" + s.S("proof"))),
                "资料来源", string.Join("\r\n", next.Strings("sources")), "适用游戏构建：26266538、26367565。位置和分支尚未核实的内容会明确标注。");
            updating = true;
            outline.BeginUpdate();
            try
            {
                outline.Nodes.Clear(); flow.Clear(); stepOffsets.Clear();
                Row[] steps = next.Rows("steps");
                string section = "", previousMap = null;
                var visitedMaps = new HashSet<string>();
                var stepNodes = new List<TreeNode>();
                TreeNode mapNode = null;
                for (int i = 0; i < steps.Length; i++)
                {
                    Row step = steps[i];
                    string map = step.S("map").Or(step.S("mapName"));
                    if (map != previousMap)
                    {
                        string mapTitle = step.S("mapName") + (visitedMaps.Add(map) ? "" : " · 回访");
                        flow.SelectionIndent = 0;
                        mapNode = outline.Nodes.Add(mapTitle);
                        mapNode.Tag = flow.TextLength;
                        Append("地图：" + mapTitle, true, Color.FromArgb(25, 91, 122));
                        Append("");
                        flow.SelectionIndent = 16;
                        previousMap = map;
                        section = "";
                    }
                    if (step.S("section") != "" && step.S("section") != section) { section = step.S("section"); Append(section, true); Append(""); }
                    stepOffsets.Add(flow.TextLength);
                    string title = (i + 1) + ". " + step.S("title"), label = StepLabel(step);
                    TreeNode stepNode = mapNode.Nodes.Add(title + " [" + label + "]");
                    stepNode.Tag = flow.TextLength;
                    stepNode.ForeColor = StepColor(step);
                    stepNodes.Add(stepNode);
                    Append(title + "  ", true, null, false);
                    Append("[" + label + "]", true, StepColor(step));
                    Append("地点：" + step.S("place"));
                    Append(step.S("text"));
                    Append("");
                }
                flow.SelectionIndent = 0;
                if (steps.Length == 0) Append(string.Join("\r\n", next.Strings("notes")).Or("暂无可读取的流程；请核对游戏日志。"));
                Append("注意事项", true);
                Append(string.Join("\r\n", next.Strings("notes")));
                Append("资料来源", true);
                Append(string.Join("\r\n", next.Strings("sources")));
                int active = Array.FindIndex(steps, s => s.Flag("current"));
                outline.ExpandAll();
                outline.SelectedNode = steps.Length == 0 ? null : stepNodes[Math.Max(0, active)];
                flow.ZoomFactor = (float)fontSize.Value / 12;
                Point position;
                if (positions.TryGetValue(key, out position)) ScrollMessage(flow.Handle, 0x4de, IntPtr.Zero, ref position); // EM_SETSCROLLPOS
                else { flow.Select(active >= 0 ? stepOffsets[active] : 0, 0); flow.ScrollToCaret(); }
            }
            finally { outline.EndUpdate(); updating = false; }
        }
    }
}
