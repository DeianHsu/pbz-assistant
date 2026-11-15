using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;
using Row = System.Collections.Generic.Dictionary<string, object>;

namespace PBZ.Native
{
    // 隔离游戏发现入口；窗口、存档解析和槽位分析均编译自生产代码。
    internal static class GameDiscovery
    {
        public static string Game;
        public static string[] Saves;
        public static int Searches;
        public static bool MissingGame;
        public static string FindGame()
        {
            if (MissingGame) throw new DirectoryNotFoundException("没有找到 Steam 版影之刃零。当前读取器需要游戏程序和资源。");
            return Game;
        }
        public static string[] FindSaves(string game)
        {
            Searches++;
            if (Saves.Length == 0) throw new FileNotFoundException("没有找到本地存档。");
            return Saves;
        }
    }

    internal static class SaveSelectionTest
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static T Get<T>(object value, string name) { return (T)value.GetType().GetField(name, Fields).GetValue(value); }
        static void Set(object value, string name, object field) { value.GetType().GetField(name, Fields).SetValue(value, field); }
        static Task<bool> Reload(Window window, string path) { return (Task<bool>)typeof(Window).GetMethod("Reload", Fields).Invoke(window, new object[] { path }); }
        static void Check(bool result, string message) { if (!result) throw new Exception(message); }
        static string Slot(Window window) { return Get<Progress>(window, "progress").Slot.Get("PBZGameSystem").S("DateTimeString"); }
        static async Task Idle(Window window) { while (Get<bool>(window, "busy")) await Task.Delay(10); }
        static async Task Select(Window window, string field, int index)
        {
            Get<ComboBox>(window, field).SelectedIndex = index;
            await Idle(window);
        }
        internal static Catalog EmptyCatalog()
        {
            var catalog = (Catalog)FormatterServices.GetUninitializedObject(typeof(Catalog));
            Set(catalog, "Quests", new List<Row>());
            Set(catalog, "Items", new List<Row>());
            Set(catalog, "Achievements", new Row[0]);
            Set(catalog, "Maps", new Row[0]);
            Set(catalog, "ScenePickups", new Row[0]);
            Set(catalog, "Warnings", new List<string>());
            Set(catalog, "Localization", new Dictionary<string, string>());
            Set(catalog, "bells", new Dictionary<string, string>());
            return catalog;
        }
        static async Task ReferenceBrowser(string[] args)
        {
            GameDiscovery.MissingGame = true;
            using (var window = new Window { Opacity = 0, ShowInTaskbar = false })
            {
                window.Show();
                await Reload(window, null);
                await Idle(window);
                Check(Get<Progress>(window, "progress") == null && Get<Label>(window, "status").Text.StartsWith("读取失败："), "未安装游戏的启动须保留资料浏览模式");
                int[] counts = window.CheckPages();
                Check(counts[0] == 105 && counts[3] == 62 && counts[6] > 0, "没有游戏和存档时，全部任务、21 个节点、84 个检查和资料来源须直接显示");
                var documents = Get<TaskDetails>(window, "taskDetails");
                Check(documents.Quests.Count == 71, "内置任务与资源留档须完整加载");
                foreach (Row document in documents.Quests.Values.Concat(documents.Nodes.Values).Concat(documents.Checks.Values))
                {
                    Check(document.Flag("reference") && document.S("state") == "unknown" && document.S("stateLabel") == "未读取存档" && document.S("reason") == "",
                        "资料浏览不得推断个人进度或失败原因：" + document.S("id"));
                    Check(document.Rows("steps").All(s => s.S("mapName") != "" && s.S("state") == "unknown" && s.S("stateLabel") == "未读取存档" && !s.Flag("current")),
                        "未读取存档时，每一步应保留地图且明确进度未读取：" + document.S("id"));
                }
                var list = Get<ListView>(window, "list");
                Check(window.CheckPageFilter(3, 1) == 9 && list.Items.Cast<ListViewItem>().All(i => Get<Row>(i.Tag, "Document").Flag("archive")), "资料浏览须能单独查看资源留档");
                Check(window.CheckPageFilter(3, 0) == 62 && Get<ComboBox>(window, "filter").Text == "全部任务", "无存档任务页须默认提供全部任务");
                var search = Get<TextBox>(window, "search");
                search.Text = "采集止血藤";
                Check(list.Items.Count == 1 && list.Items[0].Text == "见习医师", "未读取存档也应能搜索攻略步骤全文");
                search.Text = "归途";
                var region = Get<ComboBox>(window, "region");
                region.SelectedIndex = Enumerable.Range(0, region.Items.Count).Single(i => region.Items[i].ToString() == "血色森林");
                Check(list.Items.Count == 0 && Get<RichTextBox>(window, "detail").Text == "当前筛选没有条目。", "无存档时应按攻略地图筛选并显示空结果说明");
                region.SelectedIndex = Enumerable.Range(0, region.Items.Count).Single(i => region.Items[i].ToString() == "陆家村地区");
                Application.DoEvents();
                Check(list.Items.Count == 1 && Get<Button>(window, "fullDetails").Enabled, "找到任务后须可直接打开完整攻略");
                var reader = new DetailsWindow { Opacity = 0, ShowInTaskbar = false };
                Set(window, "detailsWindow", reader);
                Get<Button>(window, "fullDetails").PerformClick();
                Application.DoEvents();
                var flow = Get<RichTextBox>(reader, "flow");
                Check(reader.Visible && reader.DocumentId == "quest:GuiTu" && flow.Text.Contains("地图：陆家村") && flow.Text.Contains("[未读取存档]"), "无游戏、无存档时须能阅读按地图分组的独立详情");
                Check(!flow.Text.Contains("[已完成]") && !flow.Text.Contains("[正在进行]") && !flow.Text.Contains("[未完成]"), "攻略浏览不得显示未经存档核实的步骤进度");
                Check(Get<Label>(reader, "current").Text.StartsWith("资料说明：") && Get<RichTextBox>(reader, "overview").Text.Contains("浏览模式"), "详情应说明当前处于资料浏览模式");
                string preview = Environment.GetEnvironmentVariable("PBZ_REFERENCE_PREVIEW");
                if (!string.IsNullOrEmpty(preview))
                    using (var bitmap = new System.Drawing.Bitmap(window.Width, window.Height)) { window.DrawToBitmap(bitmap, window.ClientRectangle); bitmap.Save(preview); }

                GameDiscovery.MissingGame = false;
                GameDiscovery.Saves = new string[0];
                Check(!await Reload(window, null), "已安装游戏但无存档时也应明确读取失败");
                Check(Get<Progress>(window, "progress") == null && list.Items.Count == 1 && reader.DocumentId == "quest:GuiTu", "找不到存档不得清空内置任务与已打开攻略");
                GameDiscovery.Saves = new[] { args[1], args[2] };
                Catalog catalog = EmptyCatalog();
                catalog.Quests.Add(Data.Make("id", "GuiTu", "name", "归途", "type", "Other", "branches", new Row()));
                Set(window, "game", args[0]);
                Set(window, "catalog", catalog);
                region.SelectedIndex = 0;
                bool loaded = await Reload(window, args[1]);
                Check(loaded, "资料浏览后须能正常读取存档：" + Get<Label>(window, "status").Text);
                Application.DoEvents();
                Check(Get<Progress>(window, "progress") != null && Get<ComboBox>(window, "filter").Text == "全部任务", "读取成功后须保留全部任务筛选并提供实际进度");
                Check(!Get<Row>(reader, "document").Flag("reference") && Get<Label>(reader, "current").Text.StartsWith("当前进度：") && !flow.Text.Contains("[未读取存档]"),
                    "从资料浏览切到真实槽位后，已打开详情须同步实际进度");
                reader.Close();
                window.Close();
            }
            Console.WriteLine("无存档资料浏览验证通过");
        }
        static async Task Run(string[] args)
        {
            GameDiscovery.Game = args[0];
            GameDiscovery.Saves = new[] { args[1], args[2] };
            await ReferenceBrowser(args);
            using (var window = new Window())
            {
                Set(window, "game", args[0]);
                Set(window, "catalog", EmptyCatalog());
                // 创建真实控件和消息循环，窗口透明且不进入任务栏，测试不发声。
                window.ShowInTaskbar = false;
                window.Opacity = 0;
                window.Show();
                await Reload(window, null);
                await Idle(window);
                Check(Get<ComboBox>(window, "account").Items.Count == 2, "自动查找须保留两个账号：" + Get<Label>(window, "status").Text);
                Check(Get<Progress>(window, "progress").Cycle == 1, "首次读取应打开继续游戏槽位");
                var autoButton = Get<Button>(window, "findSaves");
                Check(autoButton.Right <= autoButton.Parent.ClientSize.Width, "默认窗口须完整显示自动查找按钮");

                await Select(window, "slot", 0);
                Check(Get<Progress>(window, "progress").Cycle == 0, "自动存档须支持槽位切换");
                int searches = GameDiscovery.Searches;
                Check(await Reload(window, args[3]), "成功读取手动文件应返回成功结果");
                Check(GameDiscovery.Searches == searches, "手动读取不应扫描默认存档目录");
                Check(Get<Progress>(window, "progress").Cycle == 3, "打开新文件须使用该文件的继续游戏槽位");
                Check(Get<Label>(window, "status").Text.Contains(args[3]), "状态栏须显示当前文件路径");
                Check(window.CheckPages().Length == 7, "手动读取后全部页面须能正常填充");
                await Select(window, "slot", 0);
                Check(Get<Progress>(window, "progress").Cycle == 2, "手动存档须支持槽位切换");
                File.Copy(args[4], args[3], true);
                Get<Button>(window, "refresh").PerformClick();
                await Idle(window);
                Check(Get<Progress>(window, "progress").Cycle == 6, "刷新须重新读取手动文件并保留槽位");
                typeof(Form).GetMethod("OnKeyDown", Fields).Invoke(window, new object[] { new KeyEventArgs(Keys.F5) });
                await Idle(window);
                Check(Get<Progress>(window, "progress").Cycle == 6 && GameDiscovery.Searches == searches, "F5 须沿用手动文件");

                var previous = Get<Progress>(window, "progress");
                Check(!await Reload(window, args[5]), "读取无效文件应返回失败结果，供选档入口显示错误");
                Check(object.ReferenceEquals(previous, Get<Progress>(window, "progress")), "无效文件不得清空当前进度");
                Check(Get<string>(window, "manualSave") == args[3], "读取失败不得切换当前文件");
                File.Delete(args[3]);
                Get<Button>(window, "refresh").PerformClick();
                await Idle(window);
                Check(Get<Label>(window, "status").Text.StartsWith("读取失败："), "文件被移走须明确报错");
                Check(object.ReferenceEquals(previous, Get<Progress>(window, "progress")), "文件被移走须保留当前进度");
                File.Copy(args[4], args[3]);

                GameDiscovery.MissingGame = true;
                Check(!await Reload(window, args[3]), "只有存档而缺少游戏时应明确返回失败");
                Check(Get<Label>(window, "status").Text.Contains("游戏程序和资源"), "缺少游戏时应说明解析依赖");
                Check(object.ReferenceEquals(previous, Get<Progress>(window, "progress")), "缺少游戏的读取失败应保留当前进度");
                Check(Get<Button>(window, "chooseSave").Enabled, "读取失败后应恢复选档入口");
                GameDiscovery.MissingGame = false;

                GameDiscovery.Saves = new string[0];
                autoButton.PerformClick();
                await Idle(window);
                Check(Get<string>(window, "manualSave") == args[3], "自动查找失败须保留手动文件");
                Get<Button>(window, "refresh").PerformClick();
                await Idle(window);
                Check(Get<Progress>(window, "progress").Cycle == 6, "没有默认存档时仍须能手动刷新");
                GameDiscovery.Saves = new[] { args[1], args[2] };
                autoButton.PerformClick();
                await Idle(window);
                Check(Get<string>(window, "manualSave") == null && Get<ComboBox>(window, "account").Items.Count == 2, "须能返回自动查找");
                Check(Get<Progress>(window, "progress").Cycle == 1, "返回自动查找须选择对应文件的继续游戏槽位");
                await Select(window, "account", 1);
                Check(Get<Progress>(window, "progress").Cycle == 4, "返回自动查找后须仍能切换账号");
                await Select(window, "slot", 1);
                GameDiscovery.Saves = new[] { args[2], args[1] };
                Get<Button>(window, "refresh").PerformClick();
                await Idle(window);
                Check(Get<ComboBox>(window, "account").SelectedIndex == 0 && Get<Progress>(window, "progress").Cycle == 5,
                    "默认存档顺序改变后，刷新须保留所选账号和槽位");
                Check(Slot(window).EndsWith("05"), "显示进度须与所选槽位一致");
                Check(Get<Button>(window, "chooseSave").Enabled && autoButton.Enabled, "读取结束须恢复选择入口");
                DetailsTests.Run(window);
                window.Close();
            }
            Console.WriteLine("存档选择验证通过");
        }
        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                int exitCode = 0;
                // 使用真实消息循环，保证多个测试窗口关闭、重开时仍在界面线程继续执行。
                using (var host = new Form { Opacity = 0, ShowInTaskbar = false })
                {
                    host.Shown += async delegate
                    {
                        try { await Run(args); }
                        catch (Exception error) { Console.Error.WriteLine(error); exitCode = 1; }
                        finally { host.Close(); }
                    };
                    Application.Run(host);
                }
                return exitCode;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
    }
}
