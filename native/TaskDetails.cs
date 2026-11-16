using System;
using System.Collections.Generic;
using System.Linq;
using Row = System.Collections.Generic.Dictionary<string, object>;

namespace PBZ.Native
{
    // 任务和检查共用的展示快照；不改写游戏状态，也不把推进过节点当作任务失败。
    internal sealed class TaskDetails
    {
        readonly Progress p;
        readonly Collections collections;
        readonly Row[] achievementPlans;
        const string Unread = "未读取存档";
        const string ReferenceNote = "未读取存档，仅展示内置攻略；完成情况、失败原因和当前步骤在读取存档后判断。";
        public readonly Dictionary<string, Row> Quests = new Dictionary<string, Row>();
        public readonly Dictionary<string, Row> Nodes = new Dictionary<string, Row>();
        public readonly Dictionary<string, Row> Checks = new Dictionary<string, Row>();
        public readonly Dictionary<string, Row> NodeTasks = new Dictionary<string, Row>();
        public TaskDetails()
        {
            achievementPlans = Progress.Guides.Get("achievementPlans").Dict().Select(pair => pair.Value.Dict().With("id", pair.Key, "name", pair.Key)).ToArray();
            foreach (var pair in Progress.Guides.Get("questGuides").Dict()) Quests.Add(pair.Key, ReferenceQuest(pair.Key, pair.Value.Dict()));
            AddGates(Progress.Guides.Rows("checkpoints").Select(gate => gate.With("items", gate.Rows("requirements").Select(item =>
                item.With("title", item.S("title") == "完成紫色任务" ? "完成" + Quests[item.S("quest")].S("title")
                    : item.S("title") == "核对成就" ? "核对成就 " + item.S("achievement") : item.S("title"))).ToArray())));
        }
        public TaskDetails(Progress progress, Collections collected, QuestNavigation navigation, Row[] plans)
        {
            p = progress; collections = collected; achievementPlans = plans;
            foreach (Row quest in navigation.Quests) Quests.Add(quest.S("id"), Quest(quest));
            AddGates(p.Gates);
        }
        void AddGates(IEnumerable<Row> gates)
        {
            foreach (Row gate in gates)
            {
                Row[] items = gate.Rows("items");
                for (int i = 0; i < items.Length; i++) Checks.Add(gate.S("id") + ":" + i, Check(gate, items[i]));
                Nodes.Add(gate.S("id"), Node(gate));
                foreach (var group in items.Select((item, i) => new { Item = item, Index = i, Quest = QuestFor(item) })
                    .GroupBy(row => row.Quest == "" ? "check:" + row.Index : row.Quest))
                {
                    string questId = group.First().Quest;
                    Row quest; Quests.TryGetValue(questId, out quest);
                    Row[] checks = group.Select(row => Checks[gate.S("id") + ":" + row.Index]).ToArray();
                    Row[] required = checks.Where(c => !c.Flag("optional")).ToArray();
                    if (required.Length == 0) required = checks;
                    string state = p == null ? "unknown" : required.All(c => c.S("state") == "done") ? "done"
                        : required.Any(c => c.S("state") == "failed") ? "failed" : required.Any(c => c.S("state") == "interrupted") ? "interrupted"
                        : required.Any(c => c.S("state") == "unknown") ? "unknown" : "pending";
                    string title = quest.S("title").Or(Progress.Guides.Get("questGuides").Get(questId).S("name")).Or(checks[0].S("title"));
                    string label = p == null ? Unread : CheckLabel(state), taskState = quest.S("stateLabel").Or(p == null ? Unread : "待核实");
                    string reason = Join(checks.Select(c => c.S("reason")).Where(s => s != "").Distinct().ToArray());
                    string conditions = Join(checks.Select(c => c.S("title") + " · " + c.S("stateLabel") + (c.Flag("optional") ? "（可选）" : "")).ToArray());
                    Row document = Data.Make("id", "node-task:" + gate.S("id") + ":" + group.Key, "title", title, "state", state, "stateLabel", label,
                        "questId", questId, "taskStateLabel", questId == "" ? "" : taskState, "nodeId", gate.S("id"), "nodeName", gate.S("name"),
                        "reference", p == null, "reason", reason, "next", conditions, "steps", checks.Select(c => c.Rows("steps").Last()).ToArray(),
                        "phase", Nodes[gate.S("id")].S("phase"), "recovery", Join(checks.Select(c => c.S("recovery")).Where(s => s != "").Distinct().ToArray()),
                        "notes", new[] { gate.S("note"), "这里只核对本节点的具体条件。关联任务的整体状态单独显示，完整流程可通过“查看关联任务”打开。" },
                        "evidence", Join(checks.Select(c => c.S("evidence")).Distinct().ToArray()), "sources", checks.SelectMany(c => c.Strings("sources")).Concat(new[] { gate.S("source") }).Where(s => s != "").Distinct().ToArray(),
                        "achievements", group.SelectMany(row => row.Item.Strings("achievements")).Distinct().ToArray(),
                        "summary", Join(title, questId == "" ? "" : "任务状态：" + taskState, "检查节点：" + gate.S("name"), "本节点条件：" + label,
                            conditions, reason == "" ? "" : "失败／中断原因：" + reason));
                    NodeTasks.Add(document.S("id"), document);
                }
            }
        }
        public string QuestFor(Row item)
        {
            string id = item.S("quest").Or(item.S("evidenceQuest"));
            if (id != "" || item.S("item") == "") return id;
            return Quests.FirstOrDefault(pair => pair.Value.Rows("steps").Any(step => step.Get("reward") != null &&
                step.Get("evidence").S("id").Equals(item.S("item"), StringComparison.OrdinalIgnoreCase) &&
                step.Get("evidence").S("category") == item.S("category"))).Key ?? "";
        }
        static Row ReferenceQuest(string id, Row guide)
        {
            Row[] steps = guide.Rows("steps").Select(step => step.With("mapName", MapName(step.S("map")),
                "state", "unknown", "stateLabel", Unread, "current", false, "proof", ReferenceNote)).ToArray();
            return Data.Make("id", "quest:" + id, "questId", id, "title", guide.S("name"), "state", "unknown", "stateLabel", Unread,
                "reference", true, "archive", guide.Flag("archive"), "steps", steps, "notes", guide.Strings("notes"), "sources", guide.Strings("sources"),
                "evidence", ReferenceNote, "next", "可先阅读文字攻略，读取存档后显示个人进度和当前步骤。",
                "summary", Join(guide.S("name") + " · " + Unread, ReferenceNote));
        }
        public static string Join(params string[] text) { return string.Join("\r\n", text.Where(s => !string.IsNullOrWhiteSpace(s))); }
        static string CurrentSteps(IEnumerable<Row> steps)
        {
            return string.Join("、", steps.Select((s, i) => s.Flag("current") ? "第 " + (i + 1) + " 步 · " + s.S("mapName") + " · " + s.S("place") + " · " + s.S("title") : "").Where(s => s != ""));
        }
        public static string MapName(string map)
        {
            if (map == "Any") return "全地图（各地区分别处理）";
            if (map == "Dream") return "梦差幻境·人偶中枢";
            return Progress.Guides.Get("navigationMaps").Get(map).List().FirstOrDefault().Str().Or("地图待核实");
        }
        bool? Evidence(string id, Row evidence)
        {
            string kind = evidence.S("kind");
            string questId = evidence.S("id").Or(id);
            Row record = p.Quest(questId);
            switch (kind)
            {
                case "started": return p.State(id) == "unknown" || !p.KnownQuests ? (bool?)null : record != null;
                case "done": return !p.KnownQuests || p.State(id) == "unknown" || p.State(id) == "missing" ? (bool?)null : p.State(id) == "done";
                case "branch": case "externalBranch":
                    if (!p.KnownQuests || p.State(questId) == "unknown") return null;
                    if (record == null) return false;
                    if (!(record.Get("CompletedBranches") is object[]) && !(record.Get("QuestBranches") is object[])) return null;
                    return Progress.BranchCompleted(record, evidence.S("step"));
                case "tag": return p.Tag(evidence.S("id"));
                case "item": return p.Acquired(evidence.S("id"), evidence.S("category"));
                case "crusherVictory": return p.KnownTags ? (bool?)(p.State(id) == "done" && p.StageInfo.Flag("afterCrusher")) : null;
                case "objective":
                    Row[] definitions = Progress.Guides.Get("questObjectives").Rows(id);
                    int index = evidence.N("index", -1);
                    if (index < 0 || index >= definitions.Length) return null;
                    object value = p.ReadObjectives(new[] { definitions[index] })[0].Get("value");
                    return value == null ? (bool?)null : value.Int() == 1;
                default: return null;
            }
        }
        Row Step(string id, Row quest, Row definition)
        {
            Row evidence = definition.Get("evidence").Dict();
            bool? completed = Evidence(id, evidence);
            bool stopped = quest.S("state") == "failed" || quest.S("state") == "interrupted";
            string evidenceQuest = evidence.S("kind") == "externalBranch" ? evidence.S("id") : id;
            bool branchActive = (evidence.S("kind") == "branch" || evidence.S("kind") == "externalBranch") && Progress.BranchActive(p.Quest(evidenceQuest), evidence.S("step"));
            bool routeActive = definition.S("routeStep") != "" && p.BaiXueStep.S("quest") == id && p.BaiXueStep.S("id") == definition.S("routeStep");
            bool active = !stopped && quest.S("state") != "unknown" && quest.S("state") != "done" && completed != true && (branchActive || routeActive ||
                definition.Strings("activeBranches").Any(b => Progress.BranchActive(p.Quest(id), b)));
            bool skipped = definition.S("unlessTag") != "" && p.Tags.Contains(definition.S("unlessTag"));
            string state = skipped ? "skipped" : completed == true ? "done" : active ? "current" : completed == false ? "pending" : "unknown";
            string text = definition.S("text"), title = definition.S("title"), place = definition.S("place"), map = definition.S("map");
            string targetItem = definition.S("targetItem");
            if (evidence.S("kind") == "item") targetItem = targetItem.Or(evidence.S("id"));
            if (evidence.S("kind") == "objective")
            {
                Row objective = Progress.Guides.Get("questObjectives").Rows(id).ElementAtOrDefault(evidence.N("index"));
                targetItem = targetItem.Or(objective.S("item"));
            }
            Row item = collections.Items.FirstOrDefault(i => i.S("id").Equals(targetItem, StringComparison.OrdinalIgnoreCase));
            if (targetItem != "" && item != null)
            {
                if (title.StartsWith("收集目标 ", StringComparison.Ordinal) || title == "取得本区珍宝") title = item.S("name").Or(title);
                string[] routes = item.Strings("steps");
                text = Join(text, string.Join("\r\n", routes));
                place = item.S("bell").Or(place);
                // 只采用原始点位的明确地图，不用当前角色所在地图填补未知位置。
                Row origin = item.Rows("origins").FirstOrDefault(o => o.S("rawMap").Equals(map, StringComparison.OrdinalIgnoreCase)) ?? item.Rows("origins").FirstOrDefault();
                string known = Progress.Guides.Get("navigationMaps").Dict().Keys.FirstOrDefault(k => k.Equals(origin.S("rawMap"), StringComparison.OrdinalIgnoreCase));
                if (known != null) map = known;
            }
            string label = state == "done" ? "已完成" : state == "current" ? "当前进行" : state == "pending" ? "未完成" : state == "skipped" ? "本分支不适用" : "待核实";
            string proof = evidence.Count == 0 ? "此操作尚无可逐步核对的存档字段，需在游戏内确认。"
                : evidence.S("kind") == "branch" || evidence.S("kind") == "externalBranch" ? "按任务分支 " + evidence.S("step") + " 的完成及活动记录判断。"
                : "按所选槽位的" + (evidence.S("kind") == "item" || evidence.S("kind") == "objective" ? "物品或目标记录" : "任务或事件记录") + "判断。";
            if (branchActive) proof = Join(proof, "当前游戏目标：" + quest.Rows("branches").FirstOrDefault(b => b.S("id") == evidence.S("step")).S("objective"));
            return definition.With("map", map, "mapName", MapName(map), "place", place, "title", title, "text", text,
                "rewardReady", definition.Get("reward") != null && Evidence(id, definition.Get("reward").Dict()) == true,
                "state", state, "stateLabel", label, "current", active && !skipped, "proof", proof);
        }
        static string Blocker(Row quest, string reason)
        {
            if (reason != "") return reason;
            if (quest.S("state") == "done") return "任务已结算，奖励领取情况按物品记录分别核对。";
            string[] conditions = quest.Rows("targets").Where(t => t.Get("access").S("state") == "locked")
                .Select(t => Join(t.S("person"), t.Get("access").S("reason"))).Where(s => s != "").Distinct().ToArray();
            string[] unknown = quest.Rows("targets").Where(t => t.Get("access").S("state") == "unknown")
                .Select(t => Join(t.S("person"), t.Get("access").S("reason"))).Where(s => s != "").Distinct().ToArray();
            if (conditions.Length > 0 || unknown.Length > 0) return Join(conditions.Length == 0 ? "" : "前置未满足：\r\n" + Join(conditions),
                unknown.Length == 0 ? "" : "存档证据不足：\r\n" + Join(unknown));
            if (quest.Flag("canAccept")) return "当前已知接取条件满足；可按人物或触发点信息查找入口。";
            if (quest.S("state") == "unknown") return "当前任务状态无法识别，尚不能判断卡点；可对照游戏日志与完整攻略。";
            if (quest.S("state") == "missing") return "所选槽位尚无接取记录，接取位置与流程见完整攻略。";
            return "已知前置满足，可按当前目标推进。";
        }
        Row Quest(Row quest)
        {
            string id = quest.S("id"), state = quest.S("state");
            Row guide = Progress.Guides.Get("questGuides").Get(id).Dict();
            string title = quest.S("name");
            if (title == id || title == "") title = guide.S("name").Or(id);
            Row failure = quest.Get("failure").Dict();
            string reason = failure.S("reason");
            if (state == "failed" && reason == "") reason = "已确认任务失败，存档未记录具体原因。";
            string recovery = failure.S("action");
            if (reason != "" && recovery == "") recovery = "核对失败前的旧存档；若该路线本周目已不可恢复，需在下一周目重新处理。";
            string label = quest.S("catalogNote") != "" ? "资源留档" : quest.S("stateLabel").Or(Progress.Guides.Get("STATE_LABELS").S(state)).Or("存档证据不足");
            var definitions = guide.Rows("steps").ToList();
            if (guide.Count == 0 || guide.Flag("archive") && quest.Flag("recorded"))
            {
                foreach (Row branch in quest.Rows("branches"))
                {
                    Row target = Progress.Guides.Get("navigationDefinitions").Rows(id + ":" + branch.S("id")).FirstOrDefault();
                    definitions.Add(Data.Make("map", target.S("map"), "place", target.S("person"), "title", branch.S("objective").Or("游戏目标 " + branch.S("id")),
                        "text", branch.S("objective").Or("当前游戏资源没有可显示的目标文字，路线待核实。"), "evidence", Data.Make("kind", "branch", "step", branch.S("id"))));
                }
            }
            Row[] steps = definitions.Select(s => Step(id, quest, s)).ToArray();
            string current = CurrentSteps(steps);
            string activeTargets = string.Join("、", quest.Rows("branches").Where(b => b.Flag("active")).Select(b => b.S("objective")).Where(s => s != ""));
            string destinations = Join(quest.Rows("targets").Where(t => t.S("rawMap") != "").Select(t =>
                t.S("area") + " · " + t.S("person") + (t.S("next") == "" ? "" : "：" + t.S("next"))).Distinct().ToArray());
            string next = reason != "" ? recovery : current != "" ? current : destinations != "" ? destinations : activeTargets != "" ? activeTargets
                : state == "done" ? "任务已完成；独立奖励和收集步骤仍按各自证据核对。" : quest.Flag("canAccept") ? "先完成接取步骤。" : "当前进行到哪一步尚不能从存档确定。";
            string evidenceText = Join("任务状态：" + label, "当前日志目标：" + activeTargets.Or("无可读取的活动目标"), failure.S("evidence"));
            string blocker = Blocker(quest, reason);
            string[] notes = guide.Strings("notes").Concat(new[] { quest.S("catalogNote"),
                quest.S("stateLabel") != label ? "路线提示：" + quest.S("stateLabel") : "",
                guide.Count == 0 ? "此任务尚未核对人工文字路线，下面展示游戏原生目标。" : "" }).Where(s => s != "").ToArray();
            return Data.Make("id", "quest:" + id, "questId", id, "title", title, "state", state, "stateLabel", label, "archive", quest.S("catalogNote") != "", "reason", reason, "recovery", recovery,
                "evidence", evidenceText, "next", next, "blocker", blocker, "steps", steps, "notes", notes,
                "sources", guide.Strings("sources").Concat(quest.Rows("targets").Select(t => t.S("source"))).Concat(new[] { failure.S("source") }).Where(s => s != "").Distinct().ToArray(),
                "summary", Join(title + " · " + label, "当前进度：" + next, reason == "" ? "推进条件与卡点：" + blocker : "失败／中断原因：" + reason));
        }
        public static string CheckState(Row item)
        {
            if (item.Flag("satisfied")) return "done";
            if (item.Get("failure") != null) return item.Get("failure").S("state").Or("interrupted");
            if (item.S("state") == "failed") return item.S("stateLabel") == "提前击杀风险" ? "unknown" : "failed";
            if (item.S("state") == "interrupted") return "interrupted";
            if (item.S("state") == "unknown" || item.S("mode") == "manual") return "unknown";
            return "pending";
        }
        static string CheckLabel(string state) { return state == "done" ? "已满足" : state == "failed" ? "已失败" : state == "interrupted" ? "已中断" : state == "unknown" ? "待核实" : "未满足"; }
        Row Check(Row gate, Row item)
        {
            bool reference = p == null;
            string state = reference ? "unknown" : CheckState(item), id = item.S("quest"), label = reference ? Unread : CheckLabel(state);
            Row quest;
            Quests.TryGetValue(id, out quest);
            string map = Progress.Guides.Get("checkpointMaps").S(gate.S("id"));
            Row corresponding = quest.Rows("steps").FirstOrDefault(s => s.Get("evidence").S("step") == item.S("branch") && item.S("branch") != "");
            if (corresponding != null) map = corresponding.S("map");
            else if (item.Flag("accepted")) map = quest.Rows("steps").FirstOrDefault().S("map").Or(map);
            string reason = item.Get("failure").S("reason");
            if ((state == "failed" || state == "interrupted") && reason == "") reason = item.S("evidence").Or("已记录检查条件失败或中断，具体原因未知。");
            string recovery = item.Get("failure").S("action");
            if (reason != "" && recovery == "") recovery = "核对节点前的旧存档和当前奖励记录；不能仅根据已越过节点判断整个任务失败。";
            bool current = state == "pending" && id != "" && !quest.Rows("steps").Any(s => s.Flag("current")) &&
                ((item.S("branch") != "" && Progress.BranchActive(p.Quest(id), item.S("branch"))) ||
                item.Strings("branches").Any(b => Progress.BranchActive(p.Quest(id), b)));
            Row check = Data.Make("title", item.S("title"), "map", map, "mapName", MapName(map), "place", item.S("bell").Or(gate.S("name")),
                "text", item.S("action").Or(reference ? "按照关联任务的完整流程逐项核对。" : quest.S("next")), "state", state == "done" ? "done" : current ? "current" : state,
                "stateLabel", current ? "当前进行" : label, "current", current, "proof", reference ? ReferenceNote : item.S("evidence"), "section", "本节点检查");
            var steps = quest.Rows("steps").Select(s => s.With("section", quest.S("title"))).ToList();
            if (item.S("check") == "achievement")
            {
                Row plan = achievementPlans.FirstOrDefault(a => a.S("id") == item.S("achievement"));
                string planMap = reference ? plan.S("map").Or("Any") : "Any";
                foreach (string text in plan.Strings("steps")) steps.Add(Data.Make("map", planMap, "mapName", MapName(planMap), "place", "按本条成就的地区与目标分别处理",
                    "title", plan.S("name"), "text", text, "state", "unknown", "stateLabel", reference ? Unread : "待核实", "proof", reference ? ReferenceNote : "成就的整体解锁不能证明某一个操作步骤已完成。"));
                if (reference) check["text"] = "按本项完整流程核对成就条件；解锁进度在读取存档后显示。";
            }
            steps.Add(check);
            string phase = reference ? "资料浏览" : gate.Flag("passed") ? "已越过节点" : item.Flag("available") ? "当前可检查" : "尚未开放";
            string currentSteps = CurrentSteps(steps);
            string next = reason != "" ? recovery : state == "done" ? "本项检查已满足。" : currentSteps.Or(item.S("action").Or(quest.S("next")));
            return Data.Make("id", "check:" + gate.S("id") + ":" + Array.IndexOf(gate.Rows("items"), item), "title", item.S("title"), "state", state,
                "stateLabel", label, "phase", phase, "reference", reference, "reason", reason, "recovery", recovery, "next", next, "optional", item.Flag("optional"),
                "evidence", reference ? ReferenceNote : Join(item.S("evidence"), item.Get("failure").S("evidence")), "impact", item.S("impact"), "steps", steps.ToArray(),
                "notes", new[] { gate.S("note"), "本节点的满足条件列在“本节点检查”步骤；关联任务的完整流程同时保留后续地区与战后奖励。",
                    item.S("verification"), item.S("riskNote"), item.S("stateLabel") == "提前击杀风险" ? item.S("evidence") : "" }.Where(s => s != "").ToArray(),
                "sources", quest.Strings("sources").Concat(new[] { item.S("source"), item.Get("failure").S("source") }).Where(s => s != "").Distinct().ToArray(),
                "summary", Join(item.S("title") + " · " + label + " · " + phase, reference ? ReferenceNote : "下一步：" + next, reason == "" ? "" : "失败／中断原因：" + reason));
        }
        Row Node(Row gate)
        {
            bool reference = p == null;
            Row[] items = gate.Rows("items"), required = items.Where(i => !i.Flag("optional")).ToArray();
            string[] states = required.Select(i => reference ? "unknown" : CheckState(i)).ToArray();
            int done = states.Count(s => s == "done"), unknown = states.Count(s => s == "unknown");
            string state = reference ? "unknown" : states.All(s => s == "done") ? "done" : states.Contains("failed") ? "failed" : states.Contains("interrupted") ? "interrupted" : unknown > 0 ? "unknown" : "pending";
            string phase = reference ? "资料浏览" : gate.Flag("passed") ? "已越过节点" : gate.Flag("available") ? "当前可检查" : "尚未到达";
            Row[] checks = items.Select((item, i) => Checks[gate.S("id") + ":" + i]).ToArray();
            string progress = reference ? required.Length + " 项必需检查，" + (items.Length - required.Length) + " 项可选检查"
                : "已满足 " + done + "/" + required.Length + (unknown > 0 ? "，另有 " + unknown + " 项待核实" : "");
            string reason = string.Join("\r\n", checks.Where(c => c.S("reason") != "").Select(c => (c.Flag("optional") ? "（可选项）" : "") + c.S("title") + "：" + c.S("reason")).Distinct());
            string next = string.Join("；", checks.Where(c => c.S("state") != "done").Select(c => c.S("title")));
            Row[] steps = checks.Select(c => c.Rows("steps").Last()).ToArray();
            string currentSteps = CurrentSteps(steps);
            if (currentSteps != "") next = Join("当前进行：" + currentSteps, "待检查：" + next);
            return Data.Make("id", "node:" + gate.S("id"), "title", gate.S("name"), "state", state, "stateLabel", reference ? Unread : CheckLabel(state), "phase", phase, "reference", reference,
                "reason", reason, "progress", progress, "next", next.Or("本节点所有必需检查项均已满足。"), "steps", steps.ToArray(),
                "evidence", string.Join("\r\n", checks.Select(c => c.S("title") + "：" + c.S("stateLabel") + "；" + c.S("evidence"))),
                "recovery", string.Join("\r\n", checks.Select(c => c.S("recovery")).Where(s => s != "").Distinct()),
                "notes", new[] { gate.S("note"), "这里只展示本节点需要核对的条件，关联任务的完整流程可单独查阅。",
                    "节点推进情况与检查结果独立显示。可选项不计入必需检查的分母；已越过节点不自动代表任务失败。" },
                "sources", new[] { gate.S("source") }.Concat(checks.SelectMany(c => c.Strings("sources"))).Where(s => s != "").Distinct().ToArray(),
                "summary", Join(gate.S("name") + " · " + phase, progress, reference ? ReferenceNote : "", reason == "" ? "" : "失败／中断原因：" + reason,
                    (reference ? "检查项目：" : "待处理：") + next.Or("无")));
        }
    }
}
