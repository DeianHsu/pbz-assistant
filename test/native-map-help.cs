using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Row = System.Collections.Generic.Dictionary<string, object>;

namespace PBZ.Native
{
    internal static class MapHelpTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static T Get<T>(object value, string name) { return (T)value.GetType().GetField(name, Fields).GetValue(value); }
        static void Set(object value, string name, object field) { value.GetType().GetField(name, Fields).SetValue(value, field); }
        static void Check(bool value, string text) { if (!value) throw new Exception(text); }
        static Row Record(string id, string state, string[] completed, string active)
        {
            return Data.Make("QuestClass", "/Game/" + id + "." + id, "QuestCompletion", "EQuestCompletion::QC_" + state, "CompletedBranches", completed,
                "QuestBranches", active == "" ? new Row[0] : new[] { Data.Make("BranchID", active, "BranchType", "EBranchType::BranchActivating") });
        }
        static Row Slot(string map = "XiGu", int cycle = 0)
        {
            return Data.Make("PBZGameSystem", Data.Make("CycleCount", cycle, "CurrentLevelName", map),
                "Narrative", Data.Make("NarrativeSavedQuests", new[] {
                    Record("JianXiYiShi", "Succeeded", new string[0], ""),
                    Record("GuiTu", "Started", new[] { "1", "2" }, "3-A"),
                    Record("XiaoCui", "Succeeded", new[] { "1", "3", "4-A", "5" }, ""),
                    Record("LiuYi", "Failed", new[] { "1" }, "2"),
                    Record("MainCh1-ShiXinCun", "Started", new string[0], "1") }),
                "EventPipeline", Data.Make("GlobalTags", new string[0]),
                "Inventory", Data.Make("InventoryInfos", new Row(), "MainWeaponInfos", new Row(), "ShaChiWeaponInfo", new Row(), "AccessoryInfos", new Row()));
        }
        static MapHelp Show(Window window, Catalog catalog, Row slot)
        {
            var progress = new Progress(catalog, new Account(Data.Make("AutoSave0", slot), DateTime.UtcNow), "AutoSave0");
            var collections = new Collections(progress);
            window.ShowData(progress, collections, new QuestNavigation(progress, collections));
            return Get<MapHelp>(window, "mapHelp");
        }
        static Row Document(ListViewItem item) { return Get<Row>(item.Tag, "Document"); }
        static void Pick(ListView list, string id)
        {
            foreach (ListViewItem item in list.Items) item.Selected = false;
            list.Items.Cast<ListViewItem>().Single(i => Document(i).S("id") == id).Selected = true;
            Application.DoEvents();
        }
        static void Preview(Window window, string suffix)
        {
            string path = Environment.GetEnvironmentVariable("PBZ_MAP_HELP_PREVIEW");
            if (!string.IsNullOrEmpty(path))
                using (var bitmap = new Bitmap(window.Width, window.Height)) { window.DrawToBitmap(bitmap, window.ClientRectangle); bitmap.Save(path + suffix); }
        }
        static void BackstreetProgress(Window window)
        {
            Catalog catalog = SaveSelectionTest.EmptyCatalog();
            foreach (string id in new[] { "MainCh2-SiZhen", "MainCh2-XinZhiZongJi", "MainCh2-SiDaSheWu", "XueRouHua", "WuShiZunZhu", "BaiShanMiWen", "JianXiYiShi", "DaoJianFoXin", "Unmapped" })
                catalog.Quests.Add(Data.Make("id", id, "name", Progress.Guides.Get("questGuides").Get(id).S("name").Or(id), "type", "Other", "branches", new Row()));
            Set(catalog, "Maps", new[] {
                Data.Make("id", "PangZhen", "name", "庞镇地区", "members", new[] { "PangZhen", "HouJie" }, "points", new[] {
                    Data.Make("key", "HouJie:flower:F_06033", "rawMap", "HouJie", "kind", "flower", "id", "F_06033", "name", "舞狮尊主的记忆·后街", "category", "petal"),
                    Data.Make("key", "HouJie:flower:Unmapped", "rawMap", "HouJie", "kind", "flower", "id", "F_Unmapped", "name", "舞狮尊主的记忆·证据不足", "category", "petal") }),
                Data.Make("id", "XiMeng", "name", "戏梦山庄", "members", new[] { "XiMeng" }, "points", new[] {
                    Data.Make("key", "XiMeng:flower:Future", "rawMap", "XiMeng", "kind", "flower", "id", "F_Future", "name", "舞狮尊主的记忆·后续地区", "category", "petal") }) });
            Row slot = Slot("PBZ_HouJie_WP");
            slot.Get("Narrative").Dict()["NarrativeSavedQuests"] = new[] {
                Record("MainCh2-SiZhen", "Succeeded", new string[0], ""),
                Record("MainCh2-XinZhiZongJi", "Started", new string[0], "3"),
                Record("XueRouHua", "Started", new[] { "1", "S2" }, "2"),
                Record("WuShiZunZhu", "Started", new string[0], "1"),
                Record("BaiShanMiWen", "Started", new string[0], "1"),
                Record("JianXiYiShi", "Succeeded", new string[0], ""),
                Record("DaoJianFoXin", "Started", new string[0], "1"),
                Record("Unmapped", "Started", new string[0], "1") };
            slot.Get("EventPipeline").Dict()["GlobalTags"] = new[] { "pz.击杀.堕落将军" };
            slot.Get("Inventory").Get("AccessoryInfos").Dict()["AC_Drink"] = Data.Make("AcquiredCycle", 0);
            string original = Data.Serializer().Serialize(slot);
            Show(window, catalog, slot);
            window.CheckPageFilter(7, 0);
            Get<Button>(window, "currentMap").PerformClick();
            var list = Get<ListView>(window, "list");
            var sword = list.Items.Cast<ListViewItem>().Single(i => Document(i).S("questId") == "DaoJianFoXin");
            Check(sword.SubItems[4].Text.Contains("青铜精华"), "精简目标时仍须保留要取得的物品，不能只留下地点");
            foreach (string id in new[] { "MainCh2-SiZhen", "MainCh2-SiDaSheWu", "MainCh2-XinZhiZongJi", "XueRouHua", "JianXiYiShi", "Unmapped" })
                Check(!list.Items.Cast<ListViewItem>().Any(i => Document(i).S("questId") == id), "当前可做不得混入已完成、未接、其他目的地、前置不满足或证据不足的任务：" + id + "\n" +
                    list.Items.Cast<ListViewItem>().Select(Document).FirstOrDefault(d => d.S("questId") == id).S("summary"));
            Row lion = list.Items.Cast<ListViewItem>().Select(Document).Single(d => d.S("questId") == "WuShiZunZhu");
            Check(lion.Rows("targets").Length == 1 && lion.Rows("targets")[0].S("rawMap") == "HouJie", "同一收集任务只能列当前地图可处理的点位，后续地区及未知通路应分开");
            Check(lion.S("helpPhase") == "庞镇阶段" && lion.S("summary").Contains("庞镇后街") && !lion.S("summary").Contains("后续地区") && !lion.S("summary").Contains("映射待核实"), "当前任务须写明地图、阶段、可执行目标，不能夹带后续点位");
            Check(list.Items.Cast<ListViewItem>().All(i => MapHelp.Related(Document(i), "HouJie")), "默认可做列表只显示本图，跨地图事项通过提醒单独查阅");
            Check(list.Items.Cast<ListViewItem>().All(i => Document(i).S("stateLabel") != "待核实"), "未记录接取的任务应直接注明无接取记录，不能把已知状态写成待核实");
            Pick(list, "quest:WuShiZunZhu");
            Preview(window, ".current.png");

            window.CheckPageFilter(7, 1);
            Row lily = list.Items.Cast<ListViewItem>().Select(Document).Single(d => d.S("questId") == "XueRouHua");
            Check(lily.S("helpText").Contains("长鸿") && !lily.S("helpText").Contains("核实") && !lily.S("helpText").Contains("核对"), "存档能确定缺少的前置应直接说明，不能写成人工核对");
            lion = list.Items.Cast<ListViewItem>().Select(Document).Single(d => d.S("questId") == "WuShiZunZhu");
            Check(lion.S("helpPhase") == "戏梦山庄阶段" && lion.Strings("maps").SequenceEqual(new[] { "XiMeng" }), "后续地区应在前置未满足中单列实际阶段，不能沿用整条任务的接取阶段");
            Pick(list, "quest:XueRouHua");
            Preview(window, ".locked.png");

            window.CheckPageFilter(7, 6);
            Check(list.Items.Cast<ListViewItem>().Any(i => Document(i).S("questId") == "Unmapped"), "真正缺少定位证据的活动任务应保留在证据不足中");
            Check(!list.Items.Cast<ListViewItem>().Any(i => Document(i).S("questId") == "XueRouHua"), "已知前置不满足和证据不足必须分开");
            lion = list.Items.Cast<ListViewItem>().Select(Document).Single(d => d.S("questId") == "WuShiZunZhu");
            Check(lion.Rows("targets").Length == 1 && lion.Rows("targets")[0].Get("access").S("state") == "unknown", "未知通路不能因相邻点位可达而被标为可做");
            Preview(window, ".unknown.png");

            window.CheckPageFilter(3, 4);
            Check(list.Columns.Cast<ColumnHeader>().Any(c => c.Text == "目标地图") && list.Columns.Cast<ColumnHeader>().Any(c => c.Text == "阶段"), "全部任务也必须直接显示地图和阶段");
            var trace = list.Items.Cast<ListViewItem>().Single(i => Document(i).S("questId") == "MainCh2-XinZhiZongJi");
            Check(trace.SubItems[2].Text == "庞镇地区" && !trace.SubItems[2].Text.Contains("后街"), "整条任务曾涉及后街，不能覆盖当前活动分支实际要去的景炀轩");
            var memories = list.Items.Cast<ListViewItem>().Single(i => Document(i).S("questId") == "WuShiZunZhu");
            Check(memories.SubItems[4].Text.StartsWith("舞狮尊主的记忆") && memories.SubItems[4].Text.EndsWith("（另 2 项）") && !memories.SubItems[4].Text.Contains("后续地区"), "多个目标在列表中只保留一个名称和剩余数量");
            Check(Document(memories).S("next").Contains("后续地区"), "其他目标的完整位置和路线仍保留在详情中");
            var unaccepted = list.Items.Cast<ListViewItem>().Single(i => Document(i).S("questId") == "MainCh2-SiDaSheWu");
            Check(unaccepted.SubItems[4].Text == "未接取", "没有接取记录时直接使用短状态");
            Check(original == Data.Serializer().Serialize(slot), "当前目标筛选不得修改原始存档");
            Console.WriteLine("庞镇后街当前目标、阶段分流与前置判定验证通过");
        }
        public static void Run(Window window)
        {
            var offline = new MapHelp(new TaskDetails());
            Check(offline.CurrentMap == "" && offline.Entries.All(e => !e.Flag("notice") && e.Flag("reference")), "未读取存档时只展示地图资料，不发布个人风险提醒");
            Check(MapHelp.MapId("DuanHunShan") == "DuanHunYa" && MapHelp.MapId("MeiGu_YiJi") == "MeiGu_YiJi" && MapHelp.MapId("Unknown") == "", "当前地图须识别别名，未知地图应保持未知");
            Catalog catalog = SaveSelectionTest.EmptyCatalog();
            foreach (string id in new[] { "JianXiYiShi", "GuiTu", "XiaoCui", "LiuYi", "FengChe" })
                catalog.Quests.Add(Data.Make("id", id, "name", Progress.Guides.Get("questGuides").Get(id).S("name"), "type", "Other", "branches", new Row()));
            Row slot = Slot();
            string original = Data.Serializer().Serialize(slot);
            MapHelp help = Show(window, catalog, slot);
            string medicineReward = help.Entries.Single(e => e.S("id").StartsWith("reward:JianXiYiShi:")).S("id");
            Check(!help.Entries.Any(e => e.S("id") == "quest:FengChe"), "地图助手应排除资源留档任务");
            foreach (string id in new[] { "check:sanmian:0", "check:mountain:2", "check:opera:0" })
                Check(offline.Entries.Single(e => e.S("id") == id).S("helpKind") == "risk", "任务前置物品和普通珍宝应按节点风险核对：" + id);
            Check(help.Entries.Single(e => e.S("id") == medicineReward).Flag("notice"), "任务结算但未领取的已知奖励应独立提醒");
            Check(help.Entries.Any(e => e.S("id").StartsWith("reward:GuiTu:")), "领取条件已满足的阶段奖励须在任务结算前单独核对");
            Check(!help.Entries.Any(e => e.S("id").StartsWith("reward:XiaoCui:")), "小翠任务完成但尚未击败力者和锯者时，不应把战后奖励当作漏领");
            string blocker = help.Entries.Single(e => e.S("id") == "quest:GuiTu").S("blocker");
            Check(blocker.Contains("当前尚未推进"), "任务卡点须解释目标地区尚未开放的条件：" + blocker);
            Check(help.Entries.Single(e => e.S("id") == "quest:LiuYi").S("blocker").Contains("具体原因"), "失败原因未知时须保留证据范围");

            Row won = Slot("DuanHunShan");
            won.Get("EventPipeline").Dict()["GlobalTags"] = new[] { "dhy.力者战斗.2阶段击杀" };
            Check(Show(window, catalog, won).Entries.Single(e => e.S("id").StartsWith("reward:XiaoCui:")).Flag("notice"), "战后应提示核对尚无领取记录的白山派头带");
            Row unknown = Slot(); unknown.Remove("Inventory");
            Row uncertainReward = Show(window, catalog, unknown).Entries.Single(e => e.S("id") == medicineReward);
            Check(!uncertainReward.Flag("notice") && uncertainReward.S("helpLabel") == "领取待核实", "缺少物品字段只能待核实，不应当作漏领提醒");
            Row received = Slot();
            received.Get("Inventory").Get("AccessoryInfos").Dict()["AC_Drink"] = Data.Make("AcquiredCycle", 0);
            Row got = Show(window, catalog, received).Entries.Single(e => e.S("id") == medicineReward);
            Check(!got.Flag("notice") && got.S("helpLabel") == "本周目已领取", "已领取奖励应消除提醒");
            Row started = Slot();
            started.Get("Narrative").Rows("NarrativeSavedQuests")[0]["QuestCompletion"] = "EQuestCompletion::QC_Started";
            Check(!Show(window, catalog, started).Entries.Any(e => e.S("id") == medicineReward), "尚未完成奖励前置的任务不应误报漏领");

            help = Show(window, catalog, slot);
            window.CheckPageFilter(7, 0);
            var list = Get<ListView>(window, "list");
            var sidebar = Get<ListBox>(window, "nav");
            Check(list.Columns.Cast<ColumnHeader>().Any(c => c.Text == "地图") && list.Columns.Cast<ColumnHeader>().Any(c => c.Text == "阶段"), "当前进度必须直接显示目标地图和处理阶段");
            Check(sidebar.Items.Count == 6 && sidebar.Items[0].ToString() == "任务助手" && !sidebar.Items.Contains("地图助手") && !sidebar.Items.Contains("行前检查") && !sidebar.Items.Contains("任务路线"), "三栏应合并为一个任务助手入口");
            Check(Get<RadioButton>(window, "progressView").Checked && Get<ComboBox>(window, "region").Text == "当前阶段与地图", "当前进度应结合阶段和地图展示相关信息");
            Check(list.Items.Cast<ListViewItem>().Select(i => Document(i).S("id")).Distinct().Count() == list.Items.Count, "同一任务在当前进度中只能出现一次");
            Check(list.Items.Cast<ListViewItem>().All(i => MapHelp.Related(Document(i), "XiGu")), "默认可做列表不应混入其他地图的提醒");
            window.CheckPageFilter(7, 4);
            Check(list.Items.Cast<ListViewItem>().Any(i => i.Group.Header.StartsWith("跨地图提醒")), "当前阶段的跨地图事项应保留在提醒视图并单独分组");
            window.CheckPageFilter(7, 0);
            Check(window.OwnedForms.All(f => f.IsDisposed), "查看进度不应自动打开完整攻略或弹窗");
            var region = Get<ComboBox>(window, "region");
            region.SelectedIndex = Enumerable.Range(0, region.Items.Count).Single(i => region.Items[i].ToString() == "陆家村地区");
            Check(list.Items.Cast<ListViewItem>().Count(i => Document(i).S("questId") == "GuiTu") == 1, "任务与阶段奖励应合并到同一行");
            Get<Button>(window, "currentMap").PerformClick();
            Check(region.Text == "当前阶段与地图", "返回当前进度应清除手动查阅地图和节点");

            var nodes = Get<ComboBox>(window, "node");
            nodes.SelectedIndex = Enumerable.Range(0, nodes.Items.Count).Single(i => nodes.Items[i].ToString() == "进入庞镇前");
            Check(Get<RadioButton>(window, "progressView").Checked && sidebar.SelectedIndex == 0, "节点检查应位于当前进度内部");
            Row nodeGui = list.Items.Cast<ListViewItem>().Select(Document).Single(d => d.S("questId") == "GuiTu");
            Check(nodeGui.S("taskStateLabel") == "进行中" && nodeGui.S("stateLabel") == "已满足", "整体任务仍在进行，也应单独确认本节点条件已满足");
            Check(nodeGui.Rows("steps").Length == 1 && nodeGui.Rows("steps").All(step => step.S("section") == "本节点检查"), "节点条件不重复嵌入整条任务攻略");
            Pick(list, nodeGui.S("id"));
            Preview(window, ".node.png");
            var reader = new DetailsWindow { Opacity = 0, ShowInTaskbar = false }; Set(window, "detailsWindow", reader);
            Get<Button>(window, "relatedTask").PerformClick();
            Check(reader.Visible && reader.DocumentId == "quest:GuiTu" && Get<RichTextBox>(reader, "flow").Text.Contains("地图：庞镇后街"), "可从节点条件按需打开关联任务的完整流程");
            Show(window, catalog, slot);
            Check(reader.DocumentId == "quest:GuiTu", "刷新节点检查时，已经打开的关联任务全文应保持当前阅读对象");
            reader.Close();

            Row parallel = Slot();
            parallel.Get("Narrative").Rows("NarrativeSavedQuests")[1]["QuestBranches"] = new[] {
                Data.Make("BranchID", "3-A", "BranchType", "EBranchType::BranchActivating"),
                Data.Make("BranchID", "4", "BranchType", "EBranchType::BranchActivating") };
            Show(window, catalog, parallel);
            Get<RadioButton>(window, "allTasksView").PerformClick();
            Check(Get<RadioButton>(window, "allTasksView").Checked && !nodes.Visible, "全部任务应提供独立查阅视图");
            Check(list.Items.Cast<ListViewItem>().Count(i => Document(i).S("questId") == "GuiTu") == 1, "多个活动目标也应只展示一条任务");
            Preview(window, ".tasks.png");
            Pick(list, "quest:GuiTu");
            Check(Get<Button>(window, "relatedChecks").Visible, "全部任务应提供关联检查入口");
            Get<Button>(window, "relatedChecks").PerformClick();
            Check(list.Items.Cast<ListViewItem>().Where(i => Document(i).S("questId") != "").All(i => Document(i).S("questId") == "GuiTu"), "关联检查应聚焦所选任务的节点条件");
            Get<RadioButton>(window, "allTasksView").PerformClick();
            Check(Document(list.SelectedItems[0]).S("id") == "quest:GuiTu", "切回全部任务应保留之前选中的任务");

            help = Show(window, catalog, slot);
            window.CheckPageFilter(7, 0); Get<Button>(window, "currentMap").PerformClick();
            Row[] notices = help.Entries.Where(e => e.Flag("notice") && (e.Flag("stageRelated") || MapHelp.Related(e, "XiGu"))).ToArray();
            int count = notices.Select(e => e.S("questId") == "" ? e.S("id") : "quest:" + e.S("questId")).Distinct().Count();
            Check(notices.Length > 1 && Get<Label>(window, "reminder").Text.Contains(notices.Length + " 项提醒"), "摘要应统计全部阶段提醒，列表按任务合并");
            float compact = Get<RowStyle>(window, "reminderRow").Height;
            Get<Button>(window, "reminderToggle").PerformClick();
            Check(Get<RowStyle>(window, "reminderRow").Height > compact, "提醒可展开");
            Get<Button>(window, "reminderToggle").PerformClick();
            Check(Get<RowStyle>(window, "reminderRow").Height == compact, "提醒可收起");
            Get<Button>(window, "viewReminders").PerformClick();
            Check(Get<ComboBox>(window, "filter").Text == "当前提醒" && list.Items.Count == count, "查看提醒应展示合并后的任务清单");
            const string medicineTask = "quest:JianXiYiShi";
            Pick(list, medicineTask);
            Check(Get<RichTextBox>(window, "detail").Text.Contains("核对香薰药盒") && Get<Button>(window, "fullDetails").Enabled, "任务摘要应包含独立奖励核对并保留全文入口");
            Get<Button>(window, "ignoreReminder").PerformClick();
            Check(list.Items.Count == count - 1, "忽略应移除该任务当前显示的提醒");
            Show(window, catalog, slot);
            Check(window.CheckPageFilter(7, 5) == 1, "同一存档刷新后应保留本次忽略选择");
            Check(Get<Button>(window, "restoreReminder").Visible && Get<TaskDetails>(window, "taskDetails").Quests["JianXiYiShi"].S("state") == "done", "忽略不应改写任务状态，且应支持恢复");
            Get<Button>(window, "restoreReminder").PerformClick();
            Check(list.Items.Count == 0 && window.CheckPageFilter(7, 4) == count, "恢复后应重新出现在当前提醒中");
            Pick(list, medicineTask); Get<Button>(window, "ignoreReminder").PerformClick();
            Show(window, catalog, Slot("XiGu", 1));
            Check(window.CheckPageFilter(7, 5) == 0, "不同周目不得沿用忽略状态");
            Show(window, catalog, slot);
            string[] files = Get<string[]>(window, "files"), otherFiles = files.Select(f => f + ".another").ToArray();
            Set(window, "files", otherFiles); Show(window, catalog, slot);
            Check(window.CheckPageFilter(7, 5) == 0, "不同文件不得沿用忽略状态");
            Set(window, "files", files); Show(window, catalog, slot);
            var slots = Get<ComboBox>(window, "slot"); int previousSlot = slots.SelectedIndex;
            Set(window, "updating", true); slots.SelectedIndex = previousSlot == 0 ? 1 : 0; Set(window, "updating", false);
            Show(window, catalog, slot);
            Check(window.CheckPageFilter(7, 5) == 0, "不同槽位不得沿用忽略状态");
            Set(window, "updating", true); slots.SelectedIndex = previousSlot; Set(window, "updating", false);
            Show(window, catalog, slot);
            Check(window.CheckPageFilter(7, 5) == 1, "切回原文件、槽位和周目应恢复相应选择");
            Get<Button>(window, "restoreReminder").PerformClick();
            window.CheckPageFilter(7, 0);
            Preview(window, "");
            Check(original == Data.Serializer().Serialize(slot), "任务帮助和提醒操作不得改写存档");
            BackstreetProgress(window);
            Console.WriteLine("地图助手与可忽略提醒验证通过");
        }
    }
}
