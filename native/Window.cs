using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Row = System.Collections.Generic.Dictionary<string, object>;

namespace PBZ.Native
{
    internal sealed class Window : Form
    {
        static readonly string[] Pages = { "节点检查", "地图收集", "全收集品", "全部任务", "成就", "小张珍宝", "资料来源", "任务助手" };
        static readonly int[] PageOrder = { 7, 1, 2, 4, 5, 6 };
        const string FailedQuestsLabel = "已失败或中断";
        static readonly Color Background = Color.FromArgb(22, 29, 37);
        static readonly Color Ink = Color.FromArgb(30, 40, 48);
        static readonly Color Muted = Color.FromArgb(82, 99, 108);
        static readonly Color Danger = Color.FromArgb(163, 51, 41);
        static readonly Row Status = Data.Make("done", "已收集", "missing", "未收集", "unknown", "待核实");
        static readonly Row TemperStatus = Data.Make("done", "已铸魂", "pending", "待铸魂", "unknown", "铸魂状态待核实");
        static readonly StringComparer ColumnTextOrder = StringComparer.Create(CultureInfo.GetCultureInfo("zh-CN"), true);
        static readonly int[] CollectionWidths = { 220, 110, 180, 170, 160 };
        static readonly int[] TemperWidths = { 175, 100, 160, 170, 140 };
        static readonly int[] QuestWidths = { 175, 108, 220, 110, 200, 260 };
        static readonly int[] AchievementWidths = { 190, 108, 298, 365 };
        static readonly int[] HelpWidths = { 220, 150, 210, 390 };
        static readonly int[] NodeWidths = { 230, 120, 160, 440 };
        readonly ComboBox account = Combo(150), slot = Combo(260);
        readonly ComboBox region = Combo(178), category = Combo(144), filter = Combo(156);
        readonly ComboBox node = Combo(230);
        readonly RadioButton progressView = new RadioButton { Text = "当前进度", Appearance = Appearance.Button, AutoSize = true };
        readonly RadioButton allTasksView = new RadioButton { Text = "全部任务", Appearance = Appearance.Button, AutoSize = true };
        readonly FlowLayoutPanel taskViews = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0) };
        readonly RowStyle taskViewRow = new RowStyle(SizeType.Absolute, 36);
        readonly TextBox search = new TextBox { Width = 184 };
        readonly ListBox nav = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.None, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 48 };
        readonly ListView list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false, BorderStyle = BorderStyle.None, UseCompatibleStateImageBehavior = false };
        readonly RichTextBox detail = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, DetectUrls = true, BorderStyle = BorderStyle.None, BackColor = Color.White, ForeColor = Ink, ScrollBars = RichTextBoxScrollBars.Vertical };
        readonly Label overview = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.White, AutoEllipsis = true,
            Text = "资料浏览 · 未读取存档，可查看全部任务和节点检查" };
        readonly RowStyle reminderRow = new RowStyle(SizeType.Absolute, 0);
        readonly Label reminder = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Danger,
            BackColor = Color.FromArgb(255, 235, 226), Padding = new Padding(10, 0, 10, 0), Margin = new Padding(0), Visible = false };
        readonly Panel reminders = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(255, 235, 226), Margin = new Padding(0), Visible = false };
        readonly Button reminderToggle = new Button { Text = "展开", AutoSize = true };
        readonly Button viewReminders = new Button { Text = "查看提醒", AutoSize = true };
        readonly Button currentMap = new Button { Text = "当前地图", AutoSize = true };
        readonly Button ignoreReminder = new Button { Text = "本次忽略", AutoSize = true, Visible = false };
        readonly Button restoreReminder = new Button { Text = "恢复提醒", AutoSize = true, Visible = false };
        readonly HashSet<string> ignoredReminders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly Label status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Muted, AutoEllipsis = true };
        readonly Label title = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Ink };
        readonly Button refresh = new Button { Text = "重新读取存档", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(9, 1, 9, 1) };
        readonly Button chooseSave = new Button { Text = "选择存档文件", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(9, 1, 9, 1) };
        readonly Button findSaves = new Button { Text = "自动查找", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(9, 1, 9, 1) };
        readonly Button fullDetails = new Button { Text = "查看完整攻略", AutoSize = true, Enabled = false };
        readonly Button relatedChecks = new Button { Text = "关联检查", AutoSize = true, Visible = false };
        readonly Button relatedTask = new Button { Text = "查看关联任务", AutoSize = true, Visible = false };
        readonly SplitContainer split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 5, BackColor = Color.FromArgb(224, 231, 235) };
        readonly string[] rememberedRegion = new string[Pages.Length], rememberedCategory = new string[Pages.Length], rememberedFilter = new string[Pages.Length], rememberedSearch = new string[Pages.Length];
        readonly string[] rememberedDocument = new string[Pages.Length];
        readonly int[] sortColumns = Enumerable.Repeat(-1, Pages.Length).ToArray();
        readonly bool[] sortDescending = new bool[Pages.Length];
        Catalog catalog;
        string game;
        string manualSave;
        string[] files;
        Account[] saves;
        Progress progress;
        Collections collections;
        QuestNavigation navigation;
        Row[] achievementPlans;
        TaskDetails taskDetails = new TaskDetails();
        MapHelp mapHelp;
        DetailsWindow detailsWindow;
        bool updating, busy, taskViewChosen, relatedReader, remindersCollapsed = true;
        int page, taskPage = 3, currentTaskPage = 7;
        string selectedNode = "", nodeQuest = "";

        sealed class Choice
        {
            public readonly string Value, Text;
            public Choice(string value, string text) { Value = value; Text = text; }
            public override string ToString() { return Text; }
        }
        sealed class ViewRow
        {
            public string[] Cells;
            public string Text, Group, Gate;
            public bool Warning;
            public Row Document;
            public ViewRow(string[] cells, string text, string group = null) { Cells = cells; Text = text; Group = group; }
        }
        static ComboBox Combo(int width) { return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = width, IntegralHeight = true, DropDownHeight = 320 }; }
        static string Selected(ComboBox combo) { return combo.SelectedItem == null ? "" : ((Choice)combo.SelectedItem).Value; }
        static void Set(ComboBox combo, IEnumerable<Choice> choices, string chosen, bool fitText = false)
        {
            var options = choices.ToArray();
            combo.BeginUpdate();
            try
            {
                combo.Items.Clear();
                combo.Items.AddRange(options);
                int index = Array.FindIndex(options, option => option.Value == chosen);
                combo.SelectedIndex = options.Length == 0 ? -1 : Math.Max(0, index);
                if (fitText && options.Length > 1)
                {
                    int textWidth = options.Max(option => TextRenderer.MeasureText(option.Text, combo.Font).Width);
                    // Leave room for the drop button or scrollbar and the text inset.
                    combo.Width = Math.Max(combo.Width, textWidth + SystemInformation.VerticalScrollBarWidth + 12);
                    combo.DropDownWidth = combo.Width;
                }
                combo.Visible = options.Length > 1;
            }
            finally { combo.EndUpdate(); }
        }
        static Choice Option(string value, string text) { return new Choice(value, text); }
        static Choice[] Options(params string[] choices) { return choices.Select((text, index) => Option(index.ToString(), text)).ToArray(); }
        static Label Label(string text, Color color) { return new Label { Text = text, AutoSize = true, ForeColor = color, Margin = new Padding(4, 7, 3, 0) }; }

        public Window()
        {
            mapHelp = new MapHelp(taskDetails);
            Text = "影之刃零 · 任务助手";
            Font = new Font("Microsoft YaHei UI", 10);
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1200, 850);
            MinimumSize = new Size(850, 590);
            BackColor = Color.FromArgb(242, 245, 247);
            var frame = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            frame.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 156));
            frame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var sidebar = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Background, RowCount = 3, ColumnCount = 1, Margin = new Padding(0) };
            sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
            sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            var brand = new Label { Text = "影之刃零\r\n任务助手", Dock = DockStyle.Fill, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(Font.FontFamily, 14, FontStyle.Bold) };
            nav.BackColor = Background;
            nav.ForeColor = Color.White;
            nav.Items.AddRange(PageOrder.Select(i => Pages[i]).ToArray());
            nav.DrawItem += DrawNav;
            sidebar.Controls.Add(brand, 0, 0);
            sidebar.Controls.Add(nav, 0, 1);
            sidebar.Controls.Add(new Label { Text = "本机 Steam · 只读", Dock = DockStyle.Fill, ForeColor = Color.LightSteelBlue, TextAlign = ContentAlignment.MiddleCenter }, 0, 2);

            var body = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 8, ColumnCount = 1, Margin = new Padding(0), Padding = new Padding(16, 12, 16, 8) };
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            body.RowStyles.Add(reminderRow);
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
            body.RowStyles.Add(taskViewRow);
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 41));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            var savesBar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true, Margin = new Padding(0) };
            savesBar.Controls.Add(Label("账号", Ink));
            savesBar.Controls.Add(account);
            savesBar.Controls.Add(Label("槽位", Ink));
            savesBar.Controls.Add(slot);
            savesBar.Controls.Add(refresh);
            savesBar.Controls.Add(chooseSave);
            savesBar.Controls.Add(findSaves);
            var banner = new Panel { Dock = DockStyle.Fill, BackColor = Background, Padding = new Padding(12, 0, 12, 0), Margin = new Padding(0, 0, 0, 4) };
            banner.Controls.Add(overview);
            title.Font = new Font(Font.FontFamily, 18, FontStyle.Bold);
            var filterBar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true, Margin = new Padding(0) };
            filterBar.Controls.AddRange(new Control[] { region, node, category, filter, Label("搜索", Ink), search, currentMap });
            taskViews.Controls.AddRange(new Control[] { progressView, allTasksView });
            split.Panel1.Controls.Add(list);
            var detailFrame = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(13, 7, 8, 5), RowCount = 2, ColumnCount = 1 };
            detailFrame.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            detailFrame.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var detailTools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            detailTools.Controls.Add(new Label { Text = "当前摘要", AutoSize = true, Margin = new Padding(0, 7, 10, 0) });
            detailTools.Controls.Add(fullDetails);
            detailTools.Controls.Add(relatedChecks);
            detailTools.Controls.Add(relatedTask);
            detailTools.Controls.Add(ignoreReminder);
            detailTools.Controls.Add(restoreReminder);
            detailFrame.Controls.Add(detailTools, 0, 0);
            detailFrame.Controls.Add(detail, 0, 1);
            split.Panel2.Controls.Add(detailFrame);
            split.SizeChanged += delegate
            {
                if (split.Height > 300 && split.SplitterDistance < 110)
                    split.SplitterDistance = split.Height * 58 / 100;
            };
            body.Controls.Add(savesBar, 0, 0);
            body.Controls.Add(banner, 0, 1);
            var reminderTools = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, Padding = new Padding(3, 0, 3, 0) };
            reminderTools.Controls.AddRange(new Control[] { reminderToggle, viewReminders });
            reminders.Controls.Add(reminder);
            reminders.Controls.Add(reminderTools);
            body.Controls.Add(reminders, 0, 2);
            body.Controls.Add(title, 0, 3);
            body.Controls.Add(taskViews, 0, 4);
            body.Controls.Add(filterBar, 0, 5);
            body.Controls.Add(split, 0, 6);
            body.Controls.Add(status, 0, 7);
            frame.Controls.Add(sidebar, 0, 0);
            frame.Controls.Add(body, 1, 0);
            Controls.Add(frame);
            nav.SelectedIndexChanged += delegate
            {
                if (updating || nav.SelectedIndex < 0) return;
                int selectedPage = PageOrder[nav.SelectedIndex];
                SelectPage(selectedPage == 7 ? taskPage : selectedPage);
            };
            account.SelectedIndexChanged += delegate { if (!updating) SelectAccount(); };
            slot.SelectedIndexChanged += async delegate { if (!updating) await Analyze(); };
            region.SelectedIndexChanged += delegate { if (!updating) { rememberedRegion[page] = Selected(region); Populate(); } };
            category.SelectedIndexChanged += delegate { if (!updating) { rememberedCategory[page] = Selected(category); Populate(); } };
            filter.SelectedIndexChanged += delegate { if (!updating) { rememberedFilter[page] = Selected(filter); Populate(); } };
            search.TextChanged += delegate { if (!updating) { rememberedSearch[page] = search.Text; Populate(); } };
            list.SelectedIndexChanged += delegate { ShowDetails(); };
            list.ColumnClick += delegate(object sender, ColumnClickEventArgs e) { SortColumn(e.Column); };
            list.DoubleClick += delegate { FollowSelection(); };
            detail.LinkClicked += delegate(object sender, LinkClickedEventArgs e) { OpenLink(e.LinkText); };
            refresh.Click += async delegate { await Reload(manualSave); };
            chooseSave.Click += async delegate { await ChooseSave(); };
            findSaves.Click += async delegate { await Reload(null); };
            fullDetails.Click += delegate { OpenDetails(); };
            progressView.CheckedChanged += delegate { if (!updating && progressView.Checked) { taskViewChosen = true; SelectPage(currentTaskPage); } };
            allTasksView.CheckedChanged += delegate { if (!updating && allTasksView.Checked) { taskViewChosen = true; SelectPage(3); } };
            node.SelectedIndexChanged += delegate
            {
                if (updating) return;
                selectedNode = Selected(node); nodeQuest = "";
                rememberedFilter[0] = "3";
                SelectPage(selectedNode == "" ? 7 : 0);
            };
            relatedChecks.Click += delegate { ShowRelatedChecks(); };
            relatedTask.Click += delegate { OpenRelatedTask(); };
            currentMap.Text = "返回当前进度";
            currentMap.Click += delegate { rememberedRegion[7] = null; selectedNode = nodeQuest = ""; SelectPage(7); };
            reminderToggle.Click += delegate { remindersCollapsed = !remindersCollapsed; UpdateReminders(); };
            viewReminders.Click += delegate
            {
                rememberedRegion[7] = null;
                rememberedFilter[7] = mapHelp.Entries.Any(e => e.Flag("notice") && (e.Flag("stageRelated") || MapHelp.Related(e, mapHelp.CurrentMap)) && !IsIgnored(e)) ? "4" : "5";
                rememberedSearch[7] = "";
                selectedNode = nodeQuest = "";
                SelectPage(7);
            };
            ignoreReminder.Click += delegate { SetIgnored(true); };
            restoreReminder.Click += delegate { SetIgnored(false); };
            KeyPreview = true;
            KeyDown += async delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.F5) { e.Handled = true; await Reload(manualSave); } };
            nav.SelectedIndex = 0;
            Shown += async delegate { split.SplitterDistance = split.Height * 58 / 100; await Reload(null); };
        }

        void DrawNav(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (var brush = new SolidBrush(selected ? Color.FromArgb(43, 66, 78) : Background)) e.Graphics.FillRectangle(brush, e.Bounds);
            if (selected) using (var brush = new SolidBrush(Color.FromArgb(122, 211, 188))) e.Graphics.FillRectangle(brush, e.Bounds.Left, e.Bounds.Top, 4, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, nav.Items[e.Index].ToString(), Font, new Rectangle(e.Bounds.X + 18, e.Bounds.Y, e.Bounds.Width - 18, e.Bounds.Height),
                Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
        void SetBusy(bool value, string message)
        {
            busy = value;
            account.Enabled = slot.Enabled = refresh.Enabled = chooseSave.Enabled = findSaves.Enabled = !value;
            if (message != null) status.Text = message;
        }
        async Task ChooseSave()
        {
            if (busy) return;
            using (var dialog = new OpenFileDialog { Title = "选择影之刃零存档", Filter = "存档文件 (*.sav)|*.sav|所有文件 (*.*)|*.*", CheckFileExists = true, Multiselect = false })
            {
                int selected = account.SelectedIndex;
                if (files != null && selected >= 0 && selected < files.Length)
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(files[selected]);
                    dialog.FileName = Path.GetFileName(files[selected]);
                }
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    bool loaded = await Reload(dialog.FileName);
                    if (!loaded && !IsDisposed) MessageBox.Show(this, status.Text, "存档读取失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
        async Task<bool> Reload(string saveFile)
        {
            if (busy) return false;
            SetBusy(true, "正在查找游戏并读取存档…");
            int selected = account.SelectedIndex;
            string previousFile = files != null && selected >= 0 && selected < files.Length ? files[selected] : "";
            string previousSlot = Selected(slot);
            try
            {
                var loaded = await Task.Run(() =>
                {
                    string path = GameDiscovery.FindGame();
                    string[] found = saveFile == null ? GameDiscovery.FindSaves(path) : new[] { saveFile };
                    var metadata = catalog != null && path == game ? catalog : new Catalog(path);
                    return new { Path = path, Files = found, Catalog = metadata, Accounts = found.Select(file => SaveReader.Read(path, file)).ToArray() };
                });
                if (IsDisposed) return false;
                game = loaded.Path;
                catalog = loaded.Catalog;
                files = loaded.Files;
                saves = loaded.Accounts;
                manualSave = saveFile;
                updating = true;
                Set(account, files.Select((file, index) => Option(index.ToString(), manualSave == null ? "本地存档 " + (index + 1) : "手动选择的存档")),
                    Array.FindIndex(files, file => string.Equals(file, previousFile, StringComparison.OrdinalIgnoreCase)).ToString());
                account.Visible = true;
                updating = false;
                SetSlots(string.Equals(files[account.SelectedIndex], previousFile, StringComparison.OrdinalIgnoreCase) ? previousSlot : "");
                return await Analyze(true);
            }
            catch (Exception error)
            {
                if (!IsDisposed) SetBusy(false, "读取失败：" + error.Message);
                return false;
            }
        }
        void SetSlots(string preferred)
        {
            if (saves == null || account.SelectedIndex < 0) return;
            Account selected = saves[account.SelectedIndex];
            updating = true;
            Set(slot, selected.Slots.Select(name => Option(name, (name == selected.Preferred ? "继续游戏 · " : "") + name + " · " + selected.Time(name))),
                selected.Slots.Contains(preferred) ? preferred : selected.Preferred);
            slot.Visible = true;
            updating = false;
        }
        async void SelectAccount()
        {
            SetSlots("");
            await Analyze();
        }
        async Task<bool> Analyze(bool fromReload = false)
        {
            if ((!fromReload && busy) || saves == null || account.SelectedIndex < 0 || slot.SelectedIndex < 0) return false;
            SetBusy(true, "正在核对任务、成就和收集记录…");
            Account selected = saves[account.SelectedIndex];
            string selectedSlot = Selected(slot);
            try
            {
                var data = await Task.Run(() =>
                {
                    var p = new Progress(catalog, selected, selectedSlot);
                    var c = new Collections(p);
                    return new { Progress = p, Collections = c, Navigation = new QuestNavigation(p, c) };
                });
                if (IsDisposed) return false;
                ShowData(data.Progress, data.Collections, data.Navigation);
                status.Text = "存档保存于 " + selected.Time(selectedSlot) + "  ·  读取于 " + DateTime.Now.ToString("HH:mm:ss") +
                    (catalog.Warnings.Count == 0 ? "" : "  ·  " + string.Join("  ", catalog.Warnings)) + "  ·  " + files[account.SelectedIndex];
                return true;
            }
            catch (Exception error) { if (!IsDisposed) status.Text = "分析失败：" + error.Message; return false; }
            finally { if (!IsDisposed) SetBusy(false, null); }
        }
        internal void ShowData(Progress p, Collections c, QuestNavigation n)
        {
            if (progress == null && !taskViewChosen)
            {
                taskPage = currentTaskPage = 7;
                if (page == 0 || page == 3 || page == 7) page = 7;
            }
            progress = p;
            collections = c;
            navigation = n;
            achievementPlans = new AchievementPlan(p, c, n).Rows;
            taskDetails = new TaskDetails(p, c, n, achievementPlans);
            mapHelp = new MapHelp(taskDetails, p, n);
            if (detailsWindow != null && !detailsWindow.IsDisposed)
            {
                string id = detailsWindow.DocumentId;
                Row updated = mapHelp.Entries.Concat(taskDetails.Quests.Values).Concat(taskDetails.Nodes.Values).Concat(taskDetails.Checks.Values).Concat(taskDetails.NodeTasks.Values).FirstOrDefault(d => d.S("id") == id);
                detailsWindow.ShowDocument(updated ?? Data.Make("id", id, "title", "当前任务", "stateLabel", "存档未记录",
                    "summary", "所选槽位没有这条任务或检查的可读取记录。", "next", "请查看所选槽位的任务列表。"), DetailContext());
            }
            string location = c.Regions.FirstOrDefault(r => r.Flag("current")).S("name").Or(p.Location);
            overview.Text = p.StageInfo.S("name") + "  ·  " + location + "  ·  最近铃铛 " + p.Bell.Or("未记录") +
                "  ·  第 " + (p.Cycle < 0 ? "?" : (p.Cycle + 1).ToString()) + " 周目";
            UpdateReminders();
            Configure();
        }
        string ReminderKey(Row document)
        {
            return DetailContext() + "|" + (progress == null ? "资料浏览" : progress.Cycle.ToString()) + "|" + document.S("id");
        }
        bool IsIgnored(Row document) { return ignoredReminders.Contains(ReminderKey(document)); }
        void SetIgnored(bool ignored)
        {
            if (list.SelectedItems.Count == 0) return;
            Row document = ((ViewRow)list.SelectedItems[0].Tag).Document;
            if (!document.Flag("dismissible")) return;
            foreach (string id in document.Strings("noticeIds"))
            {
                string key = ReminderKey(Data.Make("id", id));
                if (ignored) ignoredReminders.Add(key); else ignoredReminders.Remove(key);
            }
            UpdateReminders();
            Populate();
        }
        void UpdateReminders()
        {
            Row[] related = mapHelp.CurrentMap == "" ? new Row[0] : mapHelp.Entries.Where(e => e.Flag("notice") && (e.Flag("stageRelated") || MapHelp.Related(e, mapHelp.CurrentMap))).ToArray();
            Row[] active = related.Where(e => !IsIgnored(e)).ToArray();
            int ignored = related.Length - active.Length;
            reminders.Visible = reminder.Visible = related.Length > 0;
            reminderToggle.Enabled = active.Length > 0;
            reminderToggle.Text = remindersCollapsed ? "展开" : "收起";
            viewReminders.Text = active.Length > 0 ? "查看提醒" : "查看已忽略";
            reminder.Text = "当前进度 " + active.Length + " 项提醒" + (ignored > 0 ? " · 已忽略 " + ignored + " 项" : "") + " · 按最近读取的存档核对";
            if (!remindersCollapsed) reminder.Text += "\r\n" + string.Join("\r\n", active.Take(3).Select(e => e.S("title") + " · " + e.S("helpLabel")));
            reminderRow.Height = related.Length == 0 ? 0 : reminder.Font.Height * (1 + (remindersCollapsed ? 0 : Math.Min(3, active.Length))) + 16;
        }
        void Configure()
        {
            title.Text = Pages[page];
            updating = true;
            search.Text = rememberedSearch[page] ?? "";
            IEnumerable<Choice> regions = new Choice[0], categories = new Choice[0];
            Choice[] filters;
            string defaultRegion = "";
            switch (page)
            {
                case 0:
                    filters = Options("未满足", "需现场核对", "关联成就", "全部检查", "已满足", FailedQuestsLabel);
                    break;
                case 1:
                    filters = Options("当前可查", "暂不可取", "已收集", "全部点位");
                    if (collections != null)
                    {
                        regions = collections.Regions.Select(r => Option(r.S("id"), r.S("name") + " · " + r.N("done") + "/" + r.N("total")));
                        categories = new[] { Option("", "全部类别") }.Concat(Progress.Guides.Rows("collectionCategories").Select(c => Option(c.S("id"), c.S("name"))));
                        defaultRegion = collections.SelectedMap;
                    }
                    break;
                case 2:
                    filters = Options("当前及已开放地区", "暂不可取", "已收集", "全部目录", "已持有待铸魂");
                    if (collections != null)
                    {
                        regions = new[] { Option("", "全部地区") }.Concat(collections.Regions.Select(r => Option(r.S("id"), r.S("name"))));
                        categories = new[] { Option("", "全部类别") }.Concat(collections.Categories.Select(c => Option(c.S("id"), c.S("name") + " · " + c.N("done") + "/" + c.N("total"))));
                    }
                    break;
                case 3:
                    filters = progress == null ? new[] { Option("4", "全部任务"), Option("6", "资源留档") }
                        : Options("当前目标", "风险优先", "暂不可触发", "已完成", "全部任务", "可接取但未接", "资源留档", FailedQuestsLabel);
                    if (navigation != null)
                    {
                        regions = new[] { Option("", "全部地区") }.Concat(taskDetails.Quests.Values.SelectMany(q => q.Rows("steps"))
                            .GroupBy(s => s.S("map")).Select(g => Option(g.Key, g.First().S("mapName"))));
                        categories = new[] { Option("", "全部任务类型"), Option("Main", "主线"), Option("Branch", "迷局"), Option("Simple", "异闻"), Option("Collect", "收集") };
                    }
                    else regions = new[] { Option("", "全部地区") }.Concat(taskDetails.Quests.Values.SelectMany(q => q.Rows("steps"))
                        .GroupBy(s => s.S("map")).Select(g => Option(g.Key, g.First().S("mapName"))));
                    break;
                case 4:
                    filters = Options("前置已满足", "暂不可取", "已解锁", "全部成就");
                    if (progress != null) categories = new[] { Option("", "全部分类") }.Concat(progress.Achievements.Select(a => a.S("category")).Distinct().Select(c => Option(c, c)));
                    break;
                case 5:
                    filters = Options("当前待收集或交付", "已交付", "全部地区");
                    break;
                case 7:
                    filters = Options("相关任务与检查", "任务卡点", "风险提示", "奖励核对", "当前提醒", "已忽略提醒");
                    regions = new[] { Option("", "当前阶段与地图") }.Concat(Progress.Guides.Get("navigationMaps").Dict().Keys.Select(m => Option(m, TaskDetails.MapName(m))))
                        .Concat(new[] { Option("Dream", TaskDetails.MapName("Dream")) });
                    break;
                default: filters = new Choice[0]; break;
            }
            Set(region, regions, rememberedRegion[page] ?? defaultRegion);
            Set(category, categories, rememberedCategory[page] ?? "", fitText: true);
            Set(filter, filters, rememberedFilter[page] ?? (page == 3 ? "4" : "0"));
            if (progress == null && page == 3) rememberedFilter[page] = Selected(filter);
            bool task = page == 0 || page == 3 || page == 7;
            taskViews.Visible = task; taskViewRow.Height = task ? 36 : 0;
            progressView.Checked = page == 0 || page == 7; allTasksView.Checked = page == 3;
            Set(node, new[] { Option("", "选择检查节点…"), Option("*", "全部检查节点") }.Concat(Progress.Guides.Rows("checkpoints").Select(g => Option(g.S("id"), g.S("name")))), selectedNode);
            node.Visible = page == 0 || page == 7;
            currentMap.Visible = page == 0 || page == 7;
            currentMap.Enabled = mapHelp.CurrentMap != "";
            updating = false;
            Populate();
        }
        void SelectPage(int value)
        {
            if (list.SelectedItems.Count > 0) rememberedDocument[page] = ((ViewRow)list.SelectedItems[0].Tag).Document.S("id");
            page = value;
            if (value == 0 || value == 3 || value == 7) taskPage = value;
            if (value == 0 || value == 7) currentTaskPage = value;
            updating = true;
            nav.SelectedIndex = Array.IndexOf(PageOrder, value == 0 || value == 3 ? 7 : value);
            updating = false;
            Configure();
        }

        static string Lines(params string[] lines) { return string.Join("\r\n", lines.Where(line => !string.IsNullOrWhiteSpace(line))); }
        static string Labelled(string label, string value) { return value == "" ? "" : label + "  " + value; }
        static string ObjectiveText(Row objective)
        {
            return objective.S("name") + " " + (objective.Get("value") == null ? "?" : objective.N("value").ToString()) + "/1";
        }
        static string ObjectiveSummary(Row[] objectives)
        {
            return objectives.Length <= 4 ? string.Join(" · ", objectives.Select(ObjectiveText))
                : objectives.Count(o => o.N("value") == 1) + "/" + objectives.Length + " 项";
        }
        static string ObjectiveDetails(Row[] objectives)
        {
            return objectives.Length == 0 ? "" : Lines("进度明细（所选槽位）", Lines(objectives.Select(ObjectiveText).ToArray()),
                objectives.Any(o => o.Get("value") == null) ? "? 表示当前存档没有足够的记录来判断。" : "");
        }
        static string Steps(IEnumerable<string> steps) { return Lines(steps.Where(step => step != "").Select((step, i) => (i + 1) + ". " + step).ToArray()); }
        static string Sources(params string[] sources) { return Lines(sources.Where(s => s != "").Distinct().ToArray()); }
        IEnumerable<ViewRow> NodeRows(string mode)
        {
            foreach (Row gate in Progress.Guides.Rows("checkpoints"))
            {
                if (selectedNode != "*" && selectedNode != gate.S("id")) continue;
                Row[] checks = taskDetails.NodeTasks.Values.Where(d => d.S("nodeId") == gate.S("id") &&
                    (nodeQuest == "" || d.S("questId") == nodeQuest)).ToArray();
                if (checks.Length == 0) continue;
                Row summary = taskDetails.Nodes[gate.S("id")];
                if (mode == "3") yield return new ViewRow(new[] { gate.S("name") + " · 总览", "", summary.S("stateLabel") + " · " + summary.S("phase"), summary.S("progress") },
                    DocumentText(summary), gate.S("name")) { Document = summary };
                foreach (Row document in checks)
                {
                    bool done = document.S("state") == "done";
                    if (mode == "0" && done || mode == "4" && !done || mode == "1" && document.S("state") != "unknown" ||
                        mode == "2" && document.Strings("achievements").Length == 0 || mode == "5" && document.S("reason") == "") continue;
                    yield return new ViewRow(new[] { document.S("title"), document.S("taskStateLabel"), document.S("stateLabel"), document.S("reason").Or(document.S("next")) },
                        DocumentText(document), gate.S("name")) { Document = document, Warning = document.S("reason") != "" };
                }
            }
        }
        IEnumerable<ViewRow> CollectionRows(string mode, bool global)
        {
            string map = Selected(region), kind = Selected(category);
            Row mapRow = collections.Regions.FirstOrDefault(r => r.S("id") == map);
            IEnumerable<Row> rows = global ? collections.Items : mapRow.Rows("points");
            if (global && map != "") rows = rows.Where(r => (mode == "4" ? r.Get("temper") : r).Strings("regionIds").Contains(map));
            var labels = (global ? collections.Categories : Progress.Guides.Rows("collectionCategories")).ToDictionary(r => r.S("id"), r => r.S("name"));
            foreach (Row item in rows)
            {
                if (kind != "" && item.S("category") != kind) continue;
                Row access = item.Get("access").Dict(), guide = item.Get("guide").Dict();
                var temper = item.Get("temper");
                if (mode == "4" && temper.S("state") != "pending") continue;
                bool done = item.S("state") == "done", locked = access.S("state") == "locked";
                if (mode == "0" && (done || locked || global && !item.Flag("available"))) continue;
                if (mode == "1" && (done || !locked) || mode == "2" && !done) continue;
                string name = item.S("name"), bell = item.S("bell").Or(guide.S("bell")).Or(item.Rows("nearest").FirstOrDefault().S("name"));
                string place = global ? string.Join("、", item.Strings("regionNames")) : item.S("regionName");
                string group;
                string groupId = kind.Or(item.S("category"));
                if (!labels.TryGetValue(groupId, out group)) group = groupId;
                string state = Status.S(item.S("state"));
                string temperState = TemperStatus.S(temper.S("state")), temperPlace = string.Join("、", temper.Strings("regionNames"));
                string temperText = Lines(Labelled("铸魂", temperState), Labelled("铸魂形态", temper.S("form")),
                    Labelled("所需铸魂材料", temper.S("material")), Labelled("材料地区", temperPlace),
                    Labelled("材料附近铃铛", temper.S("bell")), Steps(temper.Strings("steps")));
                if (mode == "4")
                {
                    yield return new ViewRow(new[] { name, temperState, temper.S("material"), temper.S("bell"), temperPlace },
                        Lines(name, temperText, temper.S("source")), group);
                    continue;
                }
                string detailText = Lines(name, item.S("subtitle"),
                    item.S("description"), Labelled("适用装备", item.S("soulWeapon")), Labelled("合成产物", item.S("soulProduct")), temperText,
                    Labelled("地区", place), Labelled("附近铃铛", bell),
                    access.S("reason"), Steps(global ? item.Strings("steps") : guide.Strings("steps")),
                    Lines(item.Rows("nearest").Select(b => b.S("name") + " 约 " + b.N("distance") + " 米" + (b.Flag("activated") ? "（已激活）" : "")).ToArray()),
                    Labelled("游戏标识", item.S("id").Or(item.S("key"))), Sources(guide.S("source"), guide.S("mapUrl"), access.S("source"), temper.S("source")));
                yield return new ViewRow(new[] { name, state, access.S("label"), bell, place }, detailText, group);
            }
        }
        IEnumerable<ViewRow> QuestRows(string mode)
        {
            string map = Selected(region), kind = Selected(category);
            foreach (Row quest in navigation.Quests.OrderBy(q => q.S("riskLevel") == "urgent" ? 0 : q.S("riskLevel") == "warning" ? 1 : 2))
            {
                Row document = taskDetails.Quests[quest.S("id")];
                if (kind != "" && quest.S("type") != kind) continue;
                bool archive = quest.S("catalogNote") != "";
                if (archive != (mode == "6")) continue;
                Row[] targets = quest.Rows("targets");
                if (!archive && map != "" && !document.Rows("steps").Any(step => step.S("map") == map || step.S("map") == "Any") && !targets.Any(t => t.S("mapId") == map)) continue;
                bool done = (quest.S("state") == "done" || quest.Flag("settled")) && !quest.Flag("actionable");
                bool failed = quest.S("state") == "failed" || quest.S("state") == "interrupted";
                if (mode == "7" && !failed || mode == "5" && !quest.Flag("canAccept") || mode == "3" && !done ||
                    mode != "3" && mode != "4" && mode != "6" && mode != "7" && done) continue;
                bool blocked = targets.Length > 0 && targets.All(t => t.Get("access").S("state") == "locked" || t.Get("access").S("state") == "unknown");
                if (mode == "0" && (!(quest.Flag("recorded") || quest.Flag("actionable") || quest.Flag("canAccept")) || blocked) ||
                    mode == "1" && quest.S("riskLevel") != "urgent" && quest.S("riskLevel") != "warning" || mode == "2" && !blocked) continue;
                string checks = string.Join("、", taskDetails.NodeTasks.Values.Where(d => d.S("questId") == quest.S("id")).Select(d => d.S("nodeName")).Distinct());
                document = document.With("questId", quest.S("id"), "summary", Lines(document.S("summary"), checks == "" ? "" : "关联检查节点：" + checks));
                yield return new ViewRow(new[] { quest.S("name"), document.S("stateLabel"),
                    string.Join("、", targets.Select(t => t.S("person")).Where(t => t != "").Distinct()),
                    string.Join("、", targets.Select(t => t.Get("access").S("label")).Where(t => t != "").Distinct()),
                    document.S("next"), document.S("reason").Or(quest.S("catalogNote")) }, DocumentText(document), archive ? "资源留档" : null) {
                    Document = document, Warning = document.S("reason") != ""
                };
            }
        }

        static string DocumentText(Row document)
        {
            return Lines(document.S("summary"), Lines(document.Strings("notes")),
                Lines(document.Rows("steps").Select(s => Lines(s.S("mapName"), s.S("place"), s.S("title"), s.S("text"))).ToArray()));
        }
        IEnumerable<ViewRow> ReferenceRows(string mode)
        {
            string map = Selected(region);
            foreach (Row document in taskDetails.Quests.Values)
            {
                if (document.Flag("archive") != (mode == "6")) continue;
                Row[] steps = document.Rows("steps");
                if (mode != "6" && map != "" && !steps.Any(step => step.S("map") == map || step.S("map") == "Any")) continue;
                yield return new ViewRow(new[] { document.S("title"), document.S("stateLabel"),
                    string.Join("、", steps.Select(step => step.S("mapName")).Distinct()), steps.Length == 0 ? "见资料说明" : steps.Length + " 步" },
                    DocumentText(document), document.Flag("archive") ? "资源留档" : "任务攻略") { Document = document };
            }
        }
        IEnumerable<ViewRow> HelpRows(string mode)
        {
            string map = Selected(region);
            foreach (var group in mapHelp.Entries.GroupBy(e => e.S("questId") == "" ? e.S("id") : "quest:" + e.S("questId")))
            {
                Row[] scope = group.Where(e => map == "" ? e.Flag("stageRelated") || MapHelp.Related(e, mapHelp.CurrentMap) : MapHelp.Related(e, map)).ToArray();
                if (scope.Length == 0) continue;
                Row[] shown = scope.Where(e => mode == "5" ? IsIgnored(e) : !IsIgnored(e)).ToArray();
                if (mode == "1") shown = shown.Where(e => e.S("helpKind") == "quest").ToArray();
                if (mode == "2") shown = shown.Where(e => e.S("helpKind") == "risk").ToArray();
                if (mode == "3") shown = shown.Where(e => e.S("helpKind") == "reward").ToArray();
                if (mode == "4") shown = shown.Where(e => e.Flag("notice")).ToArray();
                if (shown.Length == 0) continue;
                Row basis = group.FirstOrDefault(e => e.S("helpKind") == "quest") ?? shown[0];
                if (basis.S("helpKind") != "quest" && basis.S("questId") != "")
                    basis = basis.With("title", Progress.Guides.Get("questGuides").Get(basis.S("questId")).S("name").Or(basis.S("title")),
                        "state", "unknown", "stateLabel", progress == null ? "未读取存档" : "待核实");
                string notices = Lines(shown.Where(e => e.S("helpKind") != "quest").Select(e => e.S("title") + " · " + e.S("helpLabel")).ToArray());
                string[] noticeIds = shown.Where(e => e.Flag("dismissible")).Select(e => e.S("id")).ToArray();
                string text = Lines(basis.S("summary"), notices == "" ? "" : "相关风险与奖励：\r\n" + notices,
                    noticeIds.Length == 0 ? "" : "可忽略或恢复本任务的相关提醒，仅对本次运行的当前存档、槽位和周目生效。");
                Row document = basis.With("id", group.Key, "summary", text, "dismissible", noticeIds.Length > 0,
                    "noticeIds", noticeIds, "ignored", mode == "5");
                string hint = notices.Replace("\r\n", "；");
                yield return new ViewRow(new[] { document.S("title"), document.S("stateLabel"), hint, basis.S("helpText").Replace("\r\n", " ") },
                    DocumentText(document), basis.S("questId") == "" ? "其他检查" : "相关任务") {
                    Document = document, Warning = basis.S("reason") != "" || shown.Any(e => e.S("reason") != "")
                };
            }
        }
        IEnumerable<ViewRow> AchievementRows(string mode)
        {
            string kind = Selected(category);
            string state = mode == "1" ? "locked" : mode == "2" ? "done" : mode == "3" ? "" : "ready";
            foreach (Row a in achievementPlans)
            {
                if (state != "" && a.S("availability") != state || kind != "" && a.S("category") != kind) continue;
                Row counter = a.Get("progress") as Row;
                string progressText = counter == null ? "" : (counter.Get("value") == null ? "?" : counter.N("value").ToString()) + "/" + counter.N("target") + " " + counter.S("unit");
                Row[] objectives = a.Rows("objectives");
                string id = a.S("id");
                var gates = progress.Gates.Where(g => g.Rows("items").Any(i => i.Strings("achievements").Contains(id) || i.S("achievement") == id)).ToArray();
                string text = Lines(a.S("name") + " · " + a.S("availabilityLabel"), Labelled("达成条件", a.S("description")),
                    Labelled("当前判断", a.S("availabilityReason")), Labelled("进度", progressText), counter.S("scope"), ObjectiveDetails(objectives),
                    Lines(a.Rows("risks").Where(r => r.Flag("available") && !r.Flag("optional") && a.S("state") != "done")
                        .Select(r => "留意：" + r.S("gateName") + " · " + r.S("title")).Distinct().ToArray()),
                    Labelled("完成步骤", string.Join("\r\n", a.Strings("steps").Select((step, index) => (index + 1) + ". " + step))),
                    Lines(a.Strings("notes")), Labelled("相关行前节点", string.Join("、", gates.Select(g => g.S("name")))), a.S("planSource"));
                string group = (mode == "3" ? a.S("availabilityLabel") + " · " : "") + a.S("category");
                yield return new ViewRow(new[] { a.S("name"), a.S("availabilityLabel"), a.S("currentRequirement"), progressText.Or(ObjectiveSummary(objectives)) }, text, group) {
                    Warning = a.Flag("warning"),
                    Gate = gates.FirstOrDefault(g => g.Flag("available")).S("id").Or(gates.FirstOrDefault().S("id"))
                };
            }
        }
        IEnumerable<ViewRow> TreasureRows(string mode)
        {
            foreach (Row group in progress.Treasures)
            {
                bool handedIn = group.Flag("handedIn"), complete = group.Flag("collected");
                if (mode == "0" && (handedIn || progress.Rank < progress.TreasureRank(group.S("branch"))) || mode == "1" && !handedIn) continue;
                Row[] items = group.Rows("items");
                string count = items.Count(i => i.Flag("owned")) + "/" + items.Length;
                string state = handedIn ? "已交付" : complete ? "已收齐，待交付" : "尚有缺项";
                string text = Lines(group.S("name") + " · " + state, "本周目 " + count,
                    Lines(items.Select(i => i.S("name") + " · " + (i.Flag("owned") ? "已取得" : i.Flag("known") ? "未取得" : "待核实")).ToArray()),
                    Progress.Guides.Get("sources").Get("treasures").S("url"));
                if (mode == "0" && !complete)
                    foreach (Row item in items.Where(i => !i.Flag("owned")))
                        yield return new ViewRow(new[] { item.S("name"), item.Flag("known") ? "未取得" : "待核实", count }, text, group.S("name"));
                else yield return new ViewRow(new[] { group.S("name"), state, count }, text, group.S("name"));
            }
        }

        void SortColumn(int column)
        {
            sortDescending[page] = sortColumns[page] == column && !sortDescending[page];
            sortColumns[page] = column;
            Populate();
        }

        void Populate()
        {
            string mode = Selected(filter).Or("0"), query = search.Text.Trim();
            var columns = new[] { "名称", "状态" };
            IEnumerable<ViewRow> rows = new ViewRow[0];
            if (progress != null)
            {
                switch (page)
                {
                    case 1: columns = new[] { "收集品", "收集状态", "路线状态", "最近铃铛", "地区" }; rows = CollectionRows(mode, false); break;
                    case 2:
                        columns = mode == "4" ? new[] { "装备", "铸魂状态", "铸魂材料", "材料附近铃铛", "材料地区" }
                            : new[] { "收集品", "收集状态", "路线状态", "最近铃铛", "地区" };
                        rows = CollectionRows(mode, true);
                        break;
                    case 3: columns = new[] { "任务", "完成状态", "人物或触发点", "可用情况", "进度", "失败原因或风险" }; rows = QuestRows(mode); break;
                    case 4: columns = new[] { "成就", "可用情况", "达成要求", "进度" }; rows = AchievementRows(mode); break;
                    case 5: columns = new[] { "珍宝或地区", "状态", "进度" }; rows = TreasureRows(mode); break;
                }
            }
            else if (page == 3)
            {
                columns = new[] { "任务", "个人进度", "涉及地图", "流程步骤" };
                rows = ReferenceRows(mode);
            }
            if (page == 0)
            {
                columns = new[] { "任务或检查", "任务状态", "节点条件", "条件与原因" };
                rows = NodeRows(mode);
            }
            if (page == 6)
            {
                columns = new[] { "资料", "来源" };
                rows = Progress.Guides.Get("sources").Dict().Values.Select(source =>
                    new ViewRow(new[] { source.S("title"), source.S("url") }, Lines(source.S("title"), source.S("url"))));
            }
            if (page == 7)
            {
                columns = new[] { "任务或检查", "任务状态", "相关风险与奖励", "进度或卡点" };
                rows = HelpRows(mode);
            }
            int sort = sortColumns[page];
            bool descending = sortDescending[page];
            if (sort >= columns.Length) sort = -1;
            if (sort >= 0)
            {
                var nonempty = rows.OrderBy(row => string.IsNullOrEmpty(row.Cells[sort]));
                rows = descending ? nonempty.ThenByDescending(row => row.Cells[sort], ColumnTextOrder)
                    : nonempty.ThenBy(row => row.Cells[sort], ColumnTextOrder);
            }
            var previous = list.SelectedItems.Count == 0 ? null : list.SelectedItems[0].Tag as ViewRow;
            string previousId = rememberedDocument[page] ?? (previous == null ? "" : previous.Document.S("id"));
            list.BeginUpdate();
            try
            {
                list.Items.Clear();
                list.Groups.Clear();
                list.Columns.Clear();
                int[] widths = page == 0 ? NodeWidths : page == 7 ? HelpWidths : page == 2 ? mode == "4" ? TemperWidths : CollectionWidths : page == 3 && progress != null ? QuestWidths : page == 4 ? AchievementWidths : null;
                for (int i = 0; i < columns.Length; i++)
                {
                    int width = widths != null ? widths[i]
                        : i == 0 ? 245 : columns[i] == "人物或触发点" || columns[i] == "关联成就" ? 250
                        : columns[i] == "来源" ? 490 : columns[i] == "进度或原因" || columns[i] == "达成要求" || columns[i] == "涉及地图" || columns[i] == "检查项目" ? 320 : columns[i] == "最近铃铛" ? 168 : 136;
                    list.Columns.Add(columns[i] + (sort == i ? descending ? " ↓" : " ↑" : ""), width);
                }
                var groups = new Dictionary<string, ListViewGroup>();
                foreach (ViewRow row in rows)
                {
                    if (query != "" && !row.Text.Has(query) && !row.Cells.Any(cell => cell.Has(query)) && !(row.Group ?? "").Has(query)) continue;
                    var item = new ListViewItem(row.Cells) { Tag = row, ForeColor = row.Document.Flag("ignored") ? Muted : row.Warning ? Danger : Ink };
                    if (sort < 0 && !string.IsNullOrEmpty(row.Group))
                    {
                        ListViewGroup group;
                        if (!groups.TryGetValue(row.Group, out group))
                        {
                            group = new ListViewGroup(row.Group);
                            groups.Add(row.Group, group);
                            list.Groups.Add(group);
                        }
                        item.Group = group;
                    }
                    list.Items.Add(item);
                }
                if (list.Items.Count > 0)
                {
                    var remembered = previousId == "" ? null : list.Items.Cast<ListViewItem>().FirstOrDefault(i => ((ViewRow)i.Tag).Document.S("id") == previousId);
                    (remembered ?? list.Items[0]).Selected = true;
                }
                string subtitle = "";
                if (progress == null && (page == 0 || page == 3)) subtitle = list.Items.Count + " 项 · 未读取存档";
                if (page == 0) subtitle = selectedNode == "*" ? "全部检查节点" : Progress.Guides.Rows("checkpoints").First(g => g.S("id") == selectedNode).S("name");
                if (page == 1 && collections != null)
                {
                    Row map = collections.Regions.FirstOrDefault(r => r.S("id") == Selected(region));
                    if (map != null) subtitle = map.S("name") + " · " + map.N("done") + "/" + map.N("total") + " 已收集";
                }
                if (page == 2 && collections != null) subtitle = mode == "4" ? list.Items.Count + " 件待铸魂"
                    : collections.Items.Count(i => i.S("state") == "done") + "/" + collections.Items.Length + " 已收集";
                if (page == 3 && navigation != null && mode == "5") subtitle = list.Items.Count + " 项可接取";
                if (page == 4 && achievementPlans != null) subtitle = achievementPlans.Count(a => a.S("availability") == "done") + "/" + achievementPlans.Length + " 已解锁  ·  " +
                    achievementPlans.Count(a => a.S("availability") == "ready") + " 前置已满足  ·  " + achievementPlans.Count(a => a.S("availability") == "locked") + " 暂不可取";
                if (page == 7) subtitle = Selected(region) == "" ? "当前阶段" + (mapHelp.CurrentMap == "" ? "" : " · " + TaskDetails.MapName(mapHelp.CurrentMap)) : TaskDetails.MapName(Selected(region));
                if (page == 7) subtitle += " · " + (progress == null ? "未读取存档" : "按所选存档") + " · " + list.Items.Count + " 项";
                title.Text = (page == 7 ? "当前进度" : Pages[page]) + (subtitle == "" ? "" : "   /   " + subtitle);
            }
            finally { list.EndUpdate(); }
            ShowDetails();
        }
        void ShowDetails()
        {
            var selected = list.SelectedItems.Count == 0 ? null : list.SelectedItems[0].Tag as ViewRow;
            fullDetails.Enabled = selected != null && selected.Document != null;
            fullDetails.Text = page == 0 ? "查看检查详情" : "查看完整攻略";
            string quest = selected == null ? "" : selected.Document.S("questId");
            relatedChecks.Visible = page != 0 && quest != "" && taskDetails.NodeTasks.Values.Any(d => d.S("questId") == quest);
            relatedTask.Visible = page == 0 && quest != "" && taskDetails.Quests.ContainsKey(quest);
            bool dismissible = page == 7 && selected != null && selected.Document.Flag("dismissible");
            ignoreReminder.Visible = dismissible && !selected.Document.Flag("ignored");
            restoreReminder.Visible = dismissible && selected.Document.Flag("ignored");
            ignoreReminder.Text = "忽略所示提醒";
            if (selected != null) rememberedDocument[page] = selected.Document.S("id");
            string empty = page == 3 && Selected(filter) == "5" && list.Items.Count == 0 ? "当前筛选没有已核对可接的未接任务。" : "当前筛选没有条目。";
            if (page == 7) empty = Selected(filter) == "4" ? "所选地区没有待显示的提醒。已忽略的内容可在“已忽略提醒”中恢复。"
                : Selected(filter) == "5" ? "所选地区没有已忽略的提醒。" : "当前地区和筛选没有匹配的事项，可切换地图或搜索条件。";
            detail.Text = selected == null ? progress == null && page != 0 && page != 3 && page != 6 && page != 7
                ? "本页需要读取游戏资源和存档。可先在“任务助手”浏览全部任务与节点检查。" : empty : selected.Document != null ? selected.Document.S("summary") : selected.Text +
                (page == 4 && !string.IsNullOrEmpty(selected.Gate) ? "\r\n\r\n双击列表条目查看关联检查节点。" : "");
            if (selected != null && selected.Document != null && detailsWindow != null && !detailsWindow.IsDisposed)
            {
                Row displayed;
                detailsWindow.ShowDocument(relatedReader && taskDetails.Quests.TryGetValue(quest, out displayed) ? displayed : selected.Document, DetailContext());
            }
        }
        string DetailContext()
        {
            if (progress == null) return "资料浏览 · 未读取存档";
            string file = files != null && account.SelectedIndex >= 0 ? files[account.SelectedIndex] : "诊断数据";
            return file + " · " + Selected(slot);
        }
        void OpenDetails()
        {
            var selected = list.SelectedItems.Count == 0 ? null : list.SelectedItems[0].Tag as ViewRow;
            if (selected == null || selected.Document == null) return;
            relatedReader = false;
            ShowDocument(selected.Document);
        }
        void ShowDocument(Row document)
        {
            if (detailsWindow == null || detailsWindow.IsDisposed)
            {
                detailsWindow = new DetailsWindow();
                detailsWindow.RefreshRequested += async delegate { await Reload(manualSave); };
            }
            detailsWindow.ShowDocument(document, DetailContext());
            if (!detailsWindow.Visible) detailsWindow.Show(this);
            detailsWindow.BringToFront();
        }
        void ShowRelatedChecks()
        {
            if (list.SelectedItems.Count == 0) return;
            nodeQuest = ((ViewRow)list.SelectedItems[0].Tag).Document.S("questId");
            if (nodeQuest == "") return;
            selectedNode = "*"; rememberedFilter[0] = "3"; rememberedSearch[0] = "";
            SelectPage(0);
        }
        void OpenRelatedTask()
        {
            if (list.SelectedItems.Count == 0) return;
            string quest = ((ViewRow)list.SelectedItems[0].Tag).Document.S("questId");
            Row document;
            if (!taskDetails.Quests.TryGetValue(quest, out document)) return;
            relatedReader = true;
            ShowDocument(document);
        }
        void FollowSelection()
        {
            var item = list.SelectedItems.Count == 0 ? null : list.SelectedItems[0].Tag as ViewRow;
            if (item == null) return;
            if (page == 4 && !string.IsNullOrEmpty(item.Gate))
            {
                rememberedFilter[0] = "3";
                selectedNode = item.Gate; nodeQuest = "";
                rememberedSearch[0] = "";
                SelectPage(0);
            }
            else if (page == 6) OpenLink(item.Cells[1]);
        }
        internal static void OpenLink(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) || uri.Scheme != "https" && uri.Scheme != "http") return;
            try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
            catch (System.ComponentModel.Win32Exception error) { MessageBox.Show(error.Message, "打开攻略失败"); }
        }

        internal int[] CheckPages()
        {
            var result = new int[PageOrder.Length];
            for (int i = 0; i < PageOrder.Length; i++)
            {
                SelectPage(PageOrder[i]);
                result[i] = list.Items.Count;
            }
            SelectPage(7);
            return result;
        }
        internal int CheckPageFilter(int pageIndex, int filterIndex)
        {
            if (pageIndex == 0 || pageIndex == 3 || pageIndex == 7) taskViewChosen = true;
            if (pageIndex == 0 && selectedNode == "") selectedNode = "*";
            SelectPage(pageIndex);
            filter.SelectedIndex = filterIndex;
            return list.Items.Count;
        }
    }
}
