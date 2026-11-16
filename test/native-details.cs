using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Row = System.Collections.Generic.Dictionary<string, object>;

namespace PBZ.Native
{
    internal static class DetailsTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        [StructLayout(LayoutKind.Sequential)]
        struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        struct FormatRange { public IntPtr Hdc, Target; public Rect Area, Page; public int Start, End; }
        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        static extern IntPtr FormatText(IntPtr window, uint message, IntPtr render, ref FormatRange range);
        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        static extern IntPtr FreeFormat(IntPtr window, uint message, IntPtr render, IntPtr range);
        static T Get<T>(object target, string name) { return (T)target.GetType().GetField(name, Fields).GetValue(target); }
        static void Check(bool passed, string message) { if (!passed) throw new Exception(message); }
        static Row Record(string id, string state, string[] completed, string active)
        {
            return Data.Make("QuestClass", "/Game/" + id + "." + id, "QuestCompletion", state, "CompletedBranches", completed,
                "QuestBranches", active == "" ? new Row[0] : new[] { Data.Make("BranchID", active, "BranchType", "EBranchType::BranchActivating") });
        }
        static Progress Progress(Catalog catalog, string guiState)
        {
            Row slot = Data.Make("PBZGameSystem", Data.Make("CycleCount", 0, "CurrentLevelName", "LuJiaCun", "DateTimeString", "2026-11-15 20:00:00"),
                "Narrative", Data.Make("NarrativeSavedQuests", new[] {
                    Record("GuiTu", guiState, new[] { "1" }, "2"),
                    Record("XiaoCui", "EQuestCompletion::QC_Succeeded", new[] { "1", "3", "4-A", "5" }, ""),
                    Record("LiuYi", "EQuestCompletion::QC_Failed", new[] { "1" }, "2") }),
                "EventPipeline", Data.Make("GlobalTags", new string[0]),
                "Inventory", Data.Make("InventoryInfos", new Row(), "MainWeaponInfos", new Row(), "ShaChiWeaponInfo", new Row(), "AccessoryInfos", new Row()),
                "BossRecord", Data.Make("BP_Monster_HookMan_P2", Data.Make("Records", Data.Make("0_1", Data.Make("KillCount", 1)))));
            var account = new Account(Data.Make("AutoSave0", slot), DateTime.UtcNow);
            return new Progress(catalog, account, "AutoSave0");
        }
        public static void Run(Window window)
        {
            Catalog catalog = SaveSelectionTest.EmptyCatalog();
            foreach (string id in new[] { "GuiTu", "XiaoCui", "LiuYi" })
                catalog.Quests.Add(Data.Make("id", id, "name", PBZ.Native.Progress.Guides.Get("questGuides").Get(id).S("name"), "type", "Other", "branches", new Row()));
            Progress p = Progress(catalog, "EQuestCompletion::QC_Started");
            var collected = new Collections(p);
            var navigation = new QuestNavigation(p, collected);
            string before = Data.Serializer().Serialize(p.Slot);
            var documents = new TaskDetails(p, collected, navigation, new AchievementPlan(p, collected, navigation).Rows);
            Row gui = documents.Quests["GuiTu"];
            Check(gui.S("stateLabel") == "进行中", "任务必须使用统一的完成状态");
            Check(gui.Rows("steps").First(s => s.Get("evidence").S("step") == "1").S("state") == "done", "已完成分支应标记对应攻略步骤");
            Row current = gui.Rows("steps").Single(s => s.Flag("current"));
            Check(current.S("mapName").Contains("陆家村") && current.Get("evidence").S("step") == "2", "当前步骤应按活动分支定位并显示地图");
            Check(gui.S("next").Contains("第 "), "摘要应指明当前进行到哪一步");
            Row cui = documents.Quests["XiaoCui"];
            Check(cui.S("state") == "done", "任务结算与奖励应分别判断");
            Check(cui.Rows("steps").Single(s => s.Get("evidence").S("id") == "TU_11007").S("state") != "done", "任务完成不能自动勾选战后奖励");
            Row failed = documents.Quests["LiuYi"];
            Check(failed.S("reason").Contains("具体原因"), "只能确认失败时必须明确原因未知");
            Check(failed.Rows("steps").All(s => !s.Flag("current")), "失败任务不能仍然标记当前进行");
            Check(failed.Rows("steps").Any(s => s.S("state") == "done"), "失败前有证据完成的历史步骤仍须保留");
            Check(documents.Nodes["valley"].S("phase") == "已越过节点" && documents.Nodes["valley"].S("state") != "failed", "越过节点不等于任务失败");
            Check(documents.Nodes["jingye"].Rows("steps").All(s => !s.Flag("current")), "仅可检查的未接任务不能标记为当前进行");
            Check(documents.Nodes.Count == 21 && documents.Checks.Count == p.Gates.Sum(g => g.Rows("items").Length), "所有节点与检查项都必须有详情");
            Check(documents.Nodes["pangzhen"].Rows("steps").Length == 2 && documents.Nodes["pangzhen"].Rows("steps").All(s => s.S("section") == "本节点检查"), "节点详情只保留本节点条件，关联任务完整流程单独查阅");
            foreach (Row document in documents.Quests.Values.Concat(documents.Nodes.Values).Concat(documents.Checks.Values))
                foreach (Row step in document.Rows("steps")) Check(step.S("mapName") != "" && step.S("stateLabel") != "", "每一步都必须显示地图及完成状态");
            Check(TaskDetails.CheckState(Data.Make("state", "failed", "stateLabel", "提前击杀风险")) == "unknown", "风险证据不足时不能当作确认失败");
            Check(before == Data.Serializer().Serialize(p.Slot), "详情分析不能改写存档");
            var future = Progress(catalog, "EQuestCompletion::QC_Future");
            var futureCollections = new Collections(future);
            var futureNavigation = new QuestNavigation(future, futureCollections);
            var unknown = new TaskDetails(future, futureCollections, futureNavigation, new Row[0]);
            Check(unknown.Quests["GuiTu"].Rows("steps").All(s => !s.Flag("current")), "未知状态不能依靠残留活动分支标记当前步骤");

            window.ShowData(p, collected, navigation);
            window.CheckPageFilter(3, 4);
            var list = Get<ListView>(window, "list");
            ListViewItem selected = list.Items.Cast<ListViewItem>().First(i => Get<Row>(i.Tag, "Document").S("id") == "quest:GuiTu");
            selected.Selected = true;
            Application.DoEvents();
            var reader = new DetailsWindow { Opacity = 0, ShowInTaskbar = false };
            typeof(Window).GetField("detailsWindow", Fields).SetValue(window, reader);
            Get<Button>(window, "fullDetails").PerformClick();
            Application.DoEvents();
            Check(reader.Visible && window.OwnedForms.Contains(reader) && reader.MaximizeBox, "详情必须是可与主界面同时使用的独立可最大化窗口");
            Check(Get<RichTextBox>(window, "detail").Text.Contains("当前进度") && !Get<RichTextBox>(window, "detail").Text.Contains("资料来源"), "底部只保留摘要");
            var flow = Get<RichTextBox>(reader, "flow");
            var outline = Get<TreeView>(reader, "outline");
            Check(flow.Text.Contains("地图：") && flow.Text.Contains("正在进行"), "全文必须显示地图与逐步进度");
            Check(outline.Nodes.Count == 3 && outline.Nodes[0].Nodes.Count == 4, "连续同地图步骤应合并到同一地图区块");
            Check(outline.Nodes[2].Text.Contains("回访") && outline.Nodes[2].Nodes[0].Text.StartsWith("6. "), "跨地图后的回访应保留原始执行顺序及步骤编号");
            Check(flow.Text.Split(new[] { "地图：" }, StringSplitOptions.None).Length - 1 == 3, "地图只在区块标题显示一次");
            Check(flow.Text.Contains("告知老根头密道已通  [正在进行]") && !flow.Text.Contains("状态："), "状态应紧跟步骤标题并移除单独的状态行");
            foreach (var status in new[] {
                new { Label = "[已完成]", Color = Color.FromArgb(39, 125, 58) },
                new { Label = "[正在进行]", Color = Color.FromArgb(153, 107, 0) },
                new { Label = "[未完成]", Color = Color.FromArgb(107, 114, 128) } })
            {
                Check(flow.Find(status.Label, 0, RichTextBoxFinds.None) >= 0, "应显示步骤状态：" + status.Label);
                Check(flow.SelectionColor.ToArgb() == status.Color.ToArgb(), "步骤状态颜色应符合完成、进行和未完成的约定");
            }
            outline.SelectedNode = outline.Nodes[1].Nodes[0];
            Check(flow.SelectionStart == (int)outline.SelectedNode.Tag, "地图目录中的步骤应跳转到对应正文");
            outline.SelectedNode = outline.Nodes[0];
            Check(flow.SelectionStart == (int)outline.SelectedNode.Tag, "地图目录应支持跳转到区块标题");
            Check(Get<RichTextBox>(reader, "flow").Visible && Get<RichTextBox>(reader, "flow").ClientSize.Width > 260, "正文必须拥有可见的独立阅读区域");
            Get<TextBox>(reader, "search").Text = "告知";
            Check(reader.FindNext(), "正文搜索必须能定位攻略内容");
            Get<NumericUpDown>(reader, "fontSize").Value = 18;
            Check(Get<RichTextBox>(reader, "flow").ZoomFactor > 1, "字号调整必须影响正文");
            reader.ShowDocument(gui, "阅读位置测试");
            var reading = Get<RichTextBox>(reader, "flow");
            reading.Select(reading.TextLength - 1, 0); reading.ScrollToCaret();
            int firstVisible = reading.GetCharIndexFromPosition(new Point(1, 1));
            reader.ShowDocument(cui, "阅读位置测试");
            reader.ShowDocument(gui, "阅读位置测试");
            Check(reading.GetCharIndexFromPosition(new Point(1, 1)) == firstVisible, "切换条目后应恢复该篇攻略的阅读位置");
            reader.WindowState = FormWindowState.Maximized;
            Application.DoEvents();
            Check(reader.WindowState == FormWindowState.Maximized, "详情窗口应支持最大化");
            int refreshRequests = 0;
            reader.RefreshRequested += delegate { refreshRequests++; };
            typeof(Form).GetMethod("OnKeyDown", Fields).Invoke(reader, new object[] { new KeyEventArgs(Keys.F5) });
            Check(refreshRequests == 1, "详情窗口中的 F5 应发出重新读取请求");
            string preview = Environment.GetEnvironmentVariable("PBZ_DETAIL_PREVIEW");
            if (!string.IsNullOrEmpty(preview))
            {
                using (var bitmap = new Bitmap(reader.Width, reader.Height)) { reader.DrawToBitmap(bitmap, reader.ClientRectangle); bitmap.Save(preview); }
                // 富文本控件不支持 DrawToBitmap，使用其原生排版接口检查正文。
                var text = Get<RichTextBox>(reader, "flow");
                using (var bitmap = new Bitmap(text.ClientSize.Width, text.ClientSize.Height))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.White);
                    var range = new FormatRange { Start = 0, End = text.TextLength,
                        Area = new Rect { Right = (int)(bitmap.Width * 1440 / graphics.DpiX), Bottom = (int)(bitmap.Height * 1440 / graphics.DpiY) } };
                    range.Page = range.Area;
                    IntPtr dc = graphics.GetHdc();
                    try { range.Hdc = range.Target = dc; FormatText(text.Handle, 0x439, new IntPtr(1), ref range); }
                    finally { FreeFormat(text.Handle, 0x439, IntPtr.Zero, IntPtr.Zero); graphics.ReleaseHdc(dc); }
                    bitmap.Save(preview + ".flow.png");
                }
            }
            window.CheckPageFilter(1, 0);
            window.ShowData(future, futureCollections, futureNavigation);
            Check(Get<Label>(reader, "heading").Text.Contains("待核实"), "在其他页面切换存档时，已打开的任务详情也必须同步");
            reader.Close();
            Console.WriteLine("详情窗口与逐步进度验证通过");
        }
    }
}
