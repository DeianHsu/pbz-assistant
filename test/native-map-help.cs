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
            Check(Get<ComboBox>(window, "region").Text == "悲泣溪谷" && list.Items.Cast<ListViewItem>().All(i => MapHelp.Related(Document(i), "XiGu")), "地图助手默认关联存档中的当前地图");
            Check(!list.Items.Cast<ListViewItem>().Any(i => Document(i).S("id") == "quest:GuiTu"), "其他地图的任务不得混作当前地图相关任务");
            Check(window.OwnedForms.All(f => f.IsDisposed), "切换地图信息不应自动打开完整攻略或弹窗");
            var region = Get<ComboBox>(window, "region");
            region.SelectedIndex = Enumerable.Range(0, region.Items.Count).Single(i => region.Items[i].ToString() == "陆家村地区");
            Check(list.Items.Cast<ListViewItem>().Any(i => Document(i).S("id") == "quest:GuiTu"), "可主动查阅其他地图关联的任务");
            Get<Button>(window, "currentMap").PerformClick();
            Check(region.Text == "悲泣溪谷", "当前地图按钮须返回存档位置");
            int count = help.Entries.Count(e => e.Flag("notice") && MapHelp.Related(e, "XiGu"));
            Check(count > 1 && Get<Label>(window, "reminder").Text.Contains(count + " 项提醒"), "提醒摘要须统计全部相关风险与奖励，不能只显示第一项");
            float compact = Get<RowStyle>(window, "reminderRow").Height;
            Get<Button>(window, "reminderToggle").PerformClick();
            Check(Get<RowStyle>(window, "reminderRow").Height > compact && Get<Button>(window, "reminderToggle").Text == "收起", "提醒可展开查看并保留收起入口");
            Get<Button>(window, "reminderToggle").PerformClick();
            Check(Get<RowStyle>(window, "reminderRow").Height == compact, "收起提醒应恢复紧凑摘要");
            Get<Button>(window, "viewReminders").PerformClick();
            Check(Get<ComboBox>(window, "filter").Text == "当前提醒" && list.Items.Count == count, "查看提醒应集中展示当前地图未忽略的全部提醒");
            Pick(list, medicineReward);
            Check(Get<RichTextBox>(window, "detail").Text.Contains("关联任务：见习医师") && Get<Button>(window, "fullDetails").Enabled, "底部保留奖励摘要和主动展开攻略的入口");
            Get<Button>(window, "ignoreReminder").PerformClick();
            Check(list.Items.Count == count - 1 && !list.Items.Cast<ListViewItem>().Any(i => Document(i).S("id") == medicineReward), "忽略应移除当前提醒，同时保留事实记录");
            Show(window, catalog, slot);
            Check(window.CheckPageFilter(7, 5) == 1, "刷新同一存档后应保留本次运行的忽略选择");
            Check(Get<Button>(window, "restoreReminder").Visible && Get<TaskDetails>(window, "taskDetails").Quests["JianXiYiShi"].S("state") == "done", "忽略不应改写任务状态，且应提供恢复入口");
            Get<Button>(window, "restoreReminder").PerformClick();
            Check(list.Items.Count == 0 && window.CheckPageFilter(7, 4) == count, "恢复提醒后应重新出现在提醒清单");
            Pick(list, medicineReward); Get<Button>(window, "ignoreReminder").PerformClick();
            Show(window, catalog, Slot("XiGu", 1));
            Check(window.CheckPageFilter(7, 5) == 0, "不同周目不得沿用忽略状态");
            Show(window, catalog, slot);
            string[] files = Get<string[]>(window, "files"), otherFiles = files.Select(f => f + ".another").ToArray();
            Set(window, "files", otherFiles); Show(window, catalog, slot);
            Check(window.CheckPageFilter(7, 5) == 0, "不同存档文件不得沿用忽略状态");
            Set(window, "files", files); Show(window, catalog, slot);
            var slots = Get<ComboBox>(window, "slot"); int previousSlot = slots.SelectedIndex;
            Set(window, "updating", true); slots.SelectedIndex = previousSlot == 0 ? 1 : 0; Set(window, "updating", false);
            Show(window, catalog, slot);
            Check(window.CheckPageFilter(7, 5) == 0, "不同槽位不得沿用忽略状态");
            Set(window, "updating", true); slots.SelectedIndex = previousSlot; Set(window, "updating", false);
            Show(window, catalog, slot);
            Check(window.CheckPageFilter(7, 5) == 1, "切回原文件、槽位和周目应恢复相应的忽略选择");
            Get<Button>(window, "restoreReminder").PerformClick();
            window.CheckPageFilter(7, 0);
            string preview = Environment.GetEnvironmentVariable("PBZ_MAP_HELP_PREVIEW");
            if (!string.IsNullOrEmpty(preview))
                using (var bitmap = new Bitmap(window.Width, window.Height)) { window.DrawToBitmap(bitmap, window.ClientRectangle); bitmap.Save(preview); }
            Check(original == Data.Serializer().Serialize(slot), "地图帮助和提醒操作不得改写存档内容");
            Console.WriteLine("地图助手与可忽略提醒验证通过");
        }
    }
}
