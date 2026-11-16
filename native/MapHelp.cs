using System;
using System.Collections.Generic;
using System.Linq;
using Row = System.Collections.Generic.Dictionary<string, object>;

namespace PBZ.Native
{
    // 按地图关联已有证据和攻略，不为玩家编排探索顺序。
    internal sealed class MapHelp
    {
        public readonly Row[] Entries;
        public readonly string CurrentMap;
        static readonly Row Maps = Progress.Guides.Get("navigationMaps").Dict();

        public static string MapId(string location)
        {
            string direct = Maps.Keys.FirstOrDefault(k => k.Equals(location, StringComparison.OrdinalIgnoreCase));
            if (direct != null) return direct;
            return Maps.Keys.OrderByDescending(k => k.Length).FirstOrDefault(k => location.Has(k) ||
                Maps.Get(k).List().ElementAtOrDefault(2).Str().Equals(location, StringComparison.OrdinalIgnoreCase)) ?? "";
        }
        public static bool Related(Row entry, string map)
        {
            return map == "" || entry.Strings("maps").Contains(map) || entry.Strings("maps").Contains("Any");
        }
        static Row Entry(Row document, string kind, string label, string text, IEnumerable<string> maps, string place, bool notice)
        {
            return document.With("helpKind", kind, "helpLabel", label, "helpText", text, "maps", maps.Where(m => m != "").Distinct().ToArray(),
                "helpPlace", place, "notice", notice, "dismissible", kind != "quest");
        }
        public MapHelp(TaskDetails details, Progress progress = null, QuestNavigation navigation = null)
        {
            bool reference = progress == null;
            CurrentMap = reference ? "" : MapId(progress.Location);
            var entries = new List<Row>();
            var trackedRewards = new HashSet<string>();
            foreach (var pair in details.Quests)
            {
                Row document = pair.Value;
                if (document.Flag("archive")) continue;
                Row quest = reference ? null : navigation.Quests.First(q => q.S("id") == pair.Key);
                Row[] steps = document.Rows("steps");
                string[] maps = steps.Select(s => s.S("map")).Concat(quest.Rows("targets").Select(t => MapId(t.S("mapId")))).Distinct().ToArray();
                string text = reference ? "未读取存档，可按本地图的文字步骤查阅相关人物和线索。" : document.S("blocker");
                entries.Add(Entry(document, "quest", document.S("stateLabel"), text, maps,
                    string.Join("、", quest.Rows("targets").Select(t => t.S("person")).Where(s => s != "").Distinct()), false));
                for (int i = 0; i < steps.Length; i++)
                {
                    Row step = steps[i];
                    if (step.Get("reward") == null) continue;
                    string item = step.Get("evidence").S("id"), category = step.Get("evidence").S("category");
                    trackedRewards.Add(category + ":" + item);
                    if (!reference && !step.Flag("rewardReady") && step.S("state") != "done") continue;
                    string label = reference ? "未读取存档" : step.S("state") == "done" ? "本周目已领取" : step.S("state") == "pending" ? "未记录本周目领取" : "领取待核实";
                    string explanation = reference ? "领取条件和个人进度在读取存档后核对。"
                        : step.S("state") == "done" ? "本周目的物品获取记录已满足。"
                        : step.S("state") == "pending" ? "已记录对应任务或交付条件，尚无本周目的领取记录；可核对互动与奖励结算。"
                        : "相关任务条件已满足，物品字段不足以确认领取情况，可在游戏内核对。";
                    Row reward = document.With("id", "reward:" + pair.Key + ":" + i, "title", step.S("title"), "state", step.S("state"), "stateLabel", label,
                        "next", step.S("text"), "evidence", step.S("proof"),
                        "summary", TaskDetails.Join(step.S("title") + " · " + label, "关联任务：" + document.S("title"), explanation,
                            "地图与地点：" + step.S("mapName") + " · " + step.S("place"), step.S("text")));
                    entries.Add(Entry(reward, "reward", label, explanation, new[] { step.S("map") }, step.S("place"), !reference && step.Flag("rewardReady") && step.S("state") == "pending"));
                }
            }
            foreach (Row gate in reference ? Progress.Guides.Rows("checkpoints") : progress.Gates)
            {
                Row[] items = gate.Rows(reference ? "requirements" : "items");
                for (int i = 0; i < items.Length; i++)
                {
                    Row item = items[i], document = details.Checks[gate.S("id") + ":" + i];
                    bool reward = item.Flag("reward");
                    if (reward && trackedRewards.Contains(item.S("category") + ":" + item.S("item"))) continue;
                    if (!reward && (gate.Flag("planning") || gate.Flag("advisory"))) continue;
                    if (!reference && (!item.Flag("available") || !reward && item.Flag("satisfied"))) continue;
                    Row step = document.Rows("steps").Last();
                    string label = reference ? "未读取存档" : reward ? item.Flag("satisfied") ? "已有获取或交付记录" : "领取待核对"
                        : document.S("reason") != "" ? document.S("stateLabel") : gate.Flag("passed") ? "已过节点，待复核" : "提前核对";
                    string text = reward ? document.S("next") : TaskDetails.Join("关联节点：" + gate.S("name"),
                        document.S("reason").Or(gate.Flag("passed") ? "已越过检查节点，当前证据尚不能确认此项满足；可核对原记录与补救说明。" : item.S("action")),
                        gate.S("note"), item.S("impact"));
                    Row help = document.With("stateLabel", label,
                        "summary", TaskDetails.Join(document.S("title") + " · " + label, text, reference ? "个人进度与风险是否发生，需读取存档后判断。" : "依据最近一次读取的存档；提前核对提示与已确认失败分别判断。"));
                    entries.Add(Entry(help, reward ? "reward" : "risk", label, text,
                        new[] { step.S("map"), Progress.Guides.Get("checkpointMaps").S(gate.S("id")) }, step.S("place"),
                        !reference && !item.Flag("satisfied") && (!reward || gate.Flag("passed"))));
                }
            }
            Entries = entries.ToArray();
        }
    }
}
