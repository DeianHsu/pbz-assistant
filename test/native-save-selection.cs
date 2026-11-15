using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
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
        static async Task Run(string[] args)
        {
            GameDiscovery.Game = args[0];
            GameDiscovery.Saves = new[] { args[1], args[2] };
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
                Task run = Run(args);
                while (!run.IsCompleted) { Application.DoEvents(); Thread.Sleep(5); }
                run.GetAwaiter().GetResult();
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
    }
}
