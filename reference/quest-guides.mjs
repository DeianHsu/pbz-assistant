import { questOffers } from './quest-offers.mjs';
import { questArchive } from './quest-catalog.mjs';
import { baiXueSteps, chivalryEncounters, sources, treasureGroups } from './risk-data.mjs';
import { questObjectives } from './objective-progress.mjs';

// 地图、操作和判定证据分开维护。没有证据映射的操作必须显示待核实，不能按相邻步骤猜测完成。
const step = (map, place, title, text, evidence = null, extra = {}) => ({ map, place, title, text, evidence, ...extra });
const branch = id => ({ kind: 'branch', step: id });
const objective = index => ({ kind: 'objective', index });
const item = (id, category = 'goods') => ({ kind: 'item', id, category });
const tag = id => ({ kind: 'tag', id });
const done = { kind: 'done' };
export const questGuides = {};
function guide(id, name, steps, notes = [], extraSources = []) {
  const offers = questOffers[id] || [];
  const offer = offers.at(-1);
  questGuides[id] = {
    name, archive: false,
    steps: [...(offer ? [step(offer.map, offer.place, '接取' + name, offer.action, { kind: 'started' })] : []), ...steps],
    notes: [...new Set([...offers.map(o => o.warning).filter(Boolean), ...notes])],
    sources: [...new Set([...offers.map(o => o.source), ...extraSources].filter(Boolean))]
  };
}
const finish = (map, place, text) => step(map, place, '交付与结算', text, done);

guide('JianXiYiShi', '见习医师', [
  step('XiGu', '斧老大关押处外·河底', '采集止血藤', '从关押处出口往左下到河底，调查地面药材。', objective(0)),
  step('XiGu', '远眺龙柱处·木桥对面', '采集凝脂叶', '过铃铛左侧木桥，贴左侧墙跳上对面平台，再往左找药材。', objective(1)),
  step('XiGu', '溪谷龙柱·龙头雕像前的道路', '采集金创草', '由凝脂叶附近右侧洞穴穿到龙柱一侧，在龙头正对的路上调查光点。', objective(2)),
  finish('XiGu', '驿站铁匠铺旁·玉儿', '击败荆野前回到玉儿处，交齐三味药材并结束对话。'),
  step('XiGu', '驿站·玉儿', '核对香薰药盒', '交付后核对饰品奖励，任务完成与奖励入包分别检查。', item('AC_Drink', 'accessory'))
], [], ['https://www.gamersky.com/handbook/202610/2236093_6.shtml']);
guide('QingLiMenHu', '清理门户', [
  step('XiGu', '恶弓手营地', '处理老弓手的委托', '按任务目标前往恶弓手营地，击败相关弟子并调查任务物品。', objective(0)),
  step('XiGu', '恶弓手营地', '取得羽弓', '处理营地目标后拾取羽弓，不要只击败敌人而漏捡。', objective(1)),
  finish('XiGu', '驿站屋顶·老弓手', '返回屋顶找老弓手，交还羽弓并完成对话。应在白赤练战及营地变异前处理。'),
  step('XiGu', '驿站屋顶', '核对影之武装奖励', '在装备中确认羽弓已领取。', item('UnnamedBow', 'phantom'))
]);
guide('NanYanZhiYin', '难言之隐', [
  step('XiGu', '驿站主路·彭猛战场', '调查彭猛留下的信', '击败斧老大彭猛，调查战后留下的绝笔信。'),
  step('XiGu', '驿站铃铛旁封锁院落', '清理院落并回守卫处', '进入铁链封锁的院落清理无脸怪物，出来再找入口守卫交谈。'),
  step('XiGu', '驿站入口·队长', '领取给副官的手令', '向队长汇报院落情况，领取交给副官的手令。', objective(0)),
  step('XiGu', '杀气塔顶·鬼差旁高台', '取得塔底外门钥匙', '击败追命后返回塔顶，用锁链到鬼差右上方的高处取钥匙。'),
  step('XiGu', '溪谷平原入口至塔底营地·副官', '进入营地找副官', '从平原入口向下沿左侧路线走，在中段转入右侧房屋，穿过钢管通路找到副官交手令。'),
  finish('XiGu', '驿站入口·队长', '返回队长处完成对话，结束难言之隐。该路线也影响凶斧案卷。')
], [], ['https://www.gamersky.com/handbook/202610/2236093_3.shtml']);
guide('DaoYiYouDao', '盗亦有道', [
  step('FuJiZhiLin', '亡命徒陷阱旁·滑溜手', '拾取飞贼血书', '击败滑溜手后调查掉落的血书。', objective(0)),
  step('FuJiZhiLin', '飞贼营地·梁上君', '交还血书并完成问答', '找到梁上君，交出血书，逐项完成相关问答；具体回答以当前游戏对话为准。', branch('1')),
  finish('FuJiZhiLin', '飞贼营地', '结束交付对话，应在白赤练及营地变异前完成。'),
  step('FuJiZhiLin', '飞贼营地', '检查屏息绑带', '问答结算后核对饰品奖励。', item('AC_Stealth', 'accessory'))
]);
guide('JiuSiFuShang', '救死扶伤', [
  ...[0, 1, 2, 3].map(i => step('FuJiZhiLin', '飞贼营地及周边伤患处', '救治第' + (i + 1) + '名飞贼', '在营地及相邻路线找到受伤飞贼，完成救治对话。四名伤患分别读取存档事件；位置由地图收集页的本地点位辅助核对。', objective(i))),
  finish('FuJiZhiLin', '飞贼营地·玉儿', '四名伤患处理后回玉儿处报告，完成结算。不要拖到白赤练战后营地变异。')
]);
guide('XueRouHua', '血肉花', [
  step('XiGu', '新娘阿花战场', '取得血肉花', '击败新娘阿花，调查战场取得血肉花。', objective(0)),
  step('XiGu', '驿站·玉儿', '先把血肉花交给玉儿', '在早期路线结束前返回玉儿处交付。此处只完成早期步骤，不代表整条任务结束。', branch('S2')),
  step('FuJiZhiLin', '飞贼营地·玉儿', '继续玉儿的后续对话', '玉儿转移到营地后，继续她的对话与相关救治任务；接取地点会随她的移动改变。', branch('S1')),
  finish('HouJie', '常虹战后·玉儿家', '沿庞镇主线推进到常虹战后，进入玉儿家与她父亲对话，完成血肉花后续。'),
  step('HouJie', '玉儿家', '核对奖励与结算记录', '保存后查看任务和饰品。若早期交付已错过，不要把抵达后街视为任务完成。')
]);
guide('GeJueXieZi', '隔绝楔子', [
  step('XiGu', '溪谷杀气塔', '取得溪谷浑源石', '按塔顶鬼差的请求探索杀气塔，取得第一块浑源石。', objective(0)),
  step('XiGu', '溪谷杀气塔顶', '交回第一块浑源石', '离开悲泣溪谷前回塔顶找友善鬼差交付。', branch('1-1')),
  step('MeiGuHu', '庞镇杀气塔', '取得第二块浑源石', '后续到达梅谷湖一带的庞镇杀气塔，探索塔内并取得第二块浑源石。', objective(1)),
  step('MeiGuHu', '庞镇杀气塔顶', '完成第二次交付', '回到对应塔顶与鬼差对话并交付。', branch('2-1')),
  step('FuJiZhiLin', '冰封后的血色森林杀气塔', '取得最后一块浑源石', '戏梦山庄后冰封路线开放，再回森林杀气塔探索。', objective(2)),
  step('FuJiZhiLin', '冰封杀气塔顶', '交付并完成路线', '交回最后一块浑源石，继续塔顶鬼差后续，核对最终任务记录。', branch('3-1'))
]);
guide('GuiTu', '归途', [
  step('LuJiaCun', '水井底部与飞贼密道', '打通通往庞镇的密道', '先推进陆家村主线，打通井底密道。打通后先返回村口。', branch('1')),
  step('LuJiaCun', '村口·老根头藏身处', '告知老根头密道已通', '进入庞镇前与老根头对话，明确完成告知目标。', branch('2')),
  step('LuJiaCun', '村口·老根头', '取得故屋钥匙', '回报密道已通后继续对话，领取故屋钥匙。后街开门与烧枝分别需要该钥匙和烈焰狮头。', item('K_05004')),
  step('HouJie', '老根头家·黄灯笼房子', '调查家中遗物', '用钥匙进入故屋，按屋内人物的对话取得旧木梳。两种故屋分支以日志实际目标为准。', objective(1), { activeBranches:['3-A','3-B'] }),
  finish('LuJiaCun', '村口·老根头', '返回陆家村把遗物交给老根头，完成归途。')
]);
guide('LanJiaGongZi', '兰家大少', [
  step('LuJiaCun', '水井与飞贼密道', '推进打通密道', '接受兰少狄的请求后推进井底主线，先打通密道。', branch('1')),
  step('LuJiaCun', '兰公子藏身处', '回报密道已通', '去庞镇前返回兰少狄处，完成告知，不要以密道已开代替对话。', branch('2')),
  step('PangZhen', '兰氏大宅', '跟进兰少狄后续', '进入庞镇后按日志调查兰氏大宅，取得进入后续房间所需的钥匙。', objective(0)),
  finish('PangZhen', '兰氏大宅', '依当前目标继续兰家人物的会面和调查，直至任务结算。已失败时先核对失败原因。')
]);
guide('YiGeBuLiu', '一个不留', [
  step('XiGu', '驿站·慧觉', '回访悲泣溪谷驿站', '接取后返回驿站调查慧觉及人物变化，按日志处理遗物。', branch('2')),
  step('LuJiaCun', '幸存者小院', '回访苦叶寺同门', '带着已取得的线索返回小院，与相关同门完成交谈或交付。', branch('2-1')),
  step('FuJiZhiLin', '飞贼营地', '调查营地后续', '回到营地处理变异后的目标；这会影响仍未结清的早期任务，因此先清理早期交付。', branch('3')),
  finish('LuJiaCun', '飞贼密道入口·受伤飞贼', '返回密道入口跟进受伤飞贼的最后线索，调查遗留物并确认结算。')
]);
guide('BaiShanMiWen', '白山秘闻', [
  step('ZhengDaoDaYing', '山舍与白山派线索处', '追查毕桃桃', '从飞贼营地接取后前往正道大营，依日志调查山舍和白山派人物。', branch('1')),
  step('ZhengDaoDaYing', '山道尽头·毕桃桃的肉身', '先调查肉身', '击败乌冷霜后，先对肉身互动查看，再继续后面的首领。', branch('2')),
  step('ZhengDaoDaYing', '剑偶玉璧战场', '完成带任务的剑偶路线', '带着已推进的白山秘闻继续剑偶玉璧战，战后检查日志和奖励。', objective(0)),
  finish('ZhengDaoDaYing', '白山派相关人物处', '按剩余目标完成会面和交付，核对任务完成记录。')
]);
guide('QiShengHeLai', '泣声何来', [
  step('XiGu', '沿溪谷的哭声线索', '沿哭声调查', '从开端之地沿溪水跟随女子哭声，调查沿途触发点。', branch('1')),
  step('FuJiZhiLin', '森林深处', '追查森林里的哭声', '进入血色森林后继续调查哭声与弦者线索。', branch('2')),
  step('DengDingShanDao', '山顶宗祠', '登上宗祠寻找哭声', '进入青蛇屋外围前，先完成登上宗祠寻找哭声的目标。', branch('3')),
  step('DengDingShanDao', '宗祠地下', '进入地下继续弦者路线', '从宗祠进入地下，完成对应调查和弦者相关目标。', branch('4')),
  finish('DengDingShanDao', '宗祠地下', '按剩余日志处理最后的交互。精确中断事件尚未实机复现，不能仅因进入后期地图就猜测失败。')
]);
guide('XiaoCui', '小翠', [
  step('LuJiaCun', '人偶试验场入口', '保留小翠红头绳', '接取时领取红头绳，后续找爷爷和父母均需保留相关信物。', objective(0)),
  step('DengDingShanDao', '铁杀营地后的囚笼与山门', '找到陆三迁并绕路开门', '找到求救的陆三迁，按他的请求绕到门后打开通路。先推进救人，再去宗祠。', branch('1')),
  step('DengDingShanDao', '横刀毕峰战场附近', '拾取无根仙露', '击败横刀毕峰后仔细检查战场附近的无根仙露。', objective(1)),
  step('DengDingShanDao', '囚笼道的升降吊笼', '降下吊笼救人', '走通往宗祠的独木桥之前，用脱落的牢笼拉杆降下吊笼，打开笼门救陆三迁。', branch('3')),
  step('DengDingShanDao', '囚笼道·陆三迁', '交无根仙露', '救出后与陆三迁对话并交仙露。只取得仙露不能代替交付。', tag('ddsd.陆三迁2.有喝仙露')),
  step('DengDingShanDao', '圣母娘娘宗祠铃铛旁', '回访陆三迁', '进入宗祠后回访老人，完成后续对话。', branch('4-A')),
  step('DengDingShanDao', '宗祠·老人最后停留处', '领取给家人的信', '调查最后的对话和遗留物，保留给家人的绝笔信与红头绳。', item('R_070001')),
  step('DuanHunYa', '最后的正道营地·陆解元', '先交付并完成安葬', '挑战力者和锯者前交还红头绳与书信，同意安葬家人并完成墓前对话。', branch('5')),
  step('DuanHunYa', '力者和锯者战场', '再挑战首领', '确认战前交付已满足后再开战。进入战斗才补交信物会错过这条奖励路线。', tag('dhy.力者战斗.2阶段击杀')),
  step('DuanHunYa', '首领战后·陆解元', '回访领取白山派头带', '战后找小翠父亲完成最后对话，检查白山派头带入包。', item('TU_11007'))
], [], [sources.cui.url]);
guide('WuXiangWang', '勿相忘', [
  step('DengDingShanDao', '周平子所在的推桥与牢笼处', '先推桥开笼救周平子', '挑战疯婆子奎艳艳前，帮助推桥并打开牢笼，与周平子完成救人对话。', branch('S1')),
  step('DengDingShanDao', '疯婆子战后回访处', '取得遗书', '战后回访周平子的后续位置，调查遗留书信。早期线索不能代替开笼记录。', objective(0)),
  finish('PangZhen', '市集酒楼顶层·雨玲', '用幻术琵琶驱散酒楼入口的雾，上楼把遗书交给雨玲。')
]);
guide('LiuYi', '英雄悲歌', [
  step('DengDingShanDao', '柳义吊笼', '先开笼并听取遗物请求', '降下吊笼后打开门，与柳义谈墨玑子的遗物，避免让他死在笼里。'),
  step('DengDingShanDao', '墨玑子遗物所在路线', '找到残剑遗物', '按日志追查墨玑子遗物，取得对应残剑后返回柳义处。', objective(0)),
  step('DengDingShanDao', '柳义处', '交付遗物并处理拔剑', '在柳义仍存活时交付，继续拔剑相关目标。', branch('2')),
  finish('DengDingShanDao', '柳义最后停留处', '完成剩余对话并调查奖励；精确离场触发点尚待实机核对。')
], [], ['https://www.gamersky.com/handbook/202611/2240851_7.shtml']);
guide('YongShengDaoZhu', '赎罪名单', [
  step('DengDingShanDao', '疯婆子战场下方洞窟·悬崖岔路', '找回赎罪名单', baiXueSteps.ledger.action, objective(0), { routeStep:'ledger' }),
  step('DengDingShanDao', '宗祠外山门东侧·陈柱', '交名单并选择分支', baiXueSteps.deliver.action, branch('2'), { routeStep:'deliver' }),
  step('DengDingShanDao', '陈柱战场', '交战分支：击败铁面杀手', baiXueSteps.fight.action, tag('ddsd.杀气怪面.死亡'), { unlessTag:'ddsd.杀气怪面.放走', routeStep:'fight' }),
  step('DengDingShanDao', '陈柱原先坐着的位置', '离开分支：核对钥匙和项链', baiXueSteps.spared.action, tag('ddsd.杀气怪面.放走'), { unlessTag:'ddsd.杀气怪面.死亡', routeStep:'spared' }),
  finish('DengDingShanDao', '陈柱与女性人偶处', '完成当前选择的结算，拿到后街小院钥匙和沾血的项链后衔接白雪儿。')
], [], [sources.ledger.url, sources.baiXueChoices.url]);
guide('BaiXue', '白雪儿', [
  step('DengDingShanDao', '陈柱身旁的女性人偶', '取得小院钥匙', baiXueSteps.puppet.action, objective(0), { routeStep:'puppet' }),
  step('DengDingShanDao', '陈柱与女性人偶的对话结算处', '取得沾血的项链', '核对当前分支的对话奖励，拿到项链后再到后街小院。', objective(1)),
  step('HouJie', '青蛇屋铃铛至后街小院', '进入卧房并交项链', baiXueSteps.home.action, branch('2'), { routeStep:'home' }),
  step('HouJie', '小院卧房·床边', '拾取第一片花瓣', baiXueSteps.petal1.action, item('F_06004'), { routeStep:'petal1' }),
  step('HouJie', '小院水井附近', '拾取第二片花瓣', baiXueSteps.petal2.action, item('F_06022'), { routeStep:'petal2' }),
  step('HouJie', '水井旁老槐树', '调查树下取得雪与血', baiXueSteps.tree.action, item('AC_Bleed', 'accessory'), { routeStep:'tree' })
], ['陈柱交战与离开分支分别核对，不能只按是否击杀推断后续奖励。'], [sources.baiXue.url, sources.baiXueChoices.url]);
guide('MoJiaoZhiNv', '魔教之女', [
  step('ZhengDaoDaYing', '鸽舍屋顶', '对话后沿锁链脱战', '与红衣女剑客完成相关对话，从锁链荡绳离开，避免在单独遭遇时击杀。', tag('zddy.红衣女剑客.荡绳离开')),
  step('ZhengDaoDaYing', '七星剑阵大师兄战场', '推进七星双人战', '继续剑阵路线，保持女剑客存活到对应共同战斗。', tag('zddy.剑阵.完成任务')),
  step('ZhengDaoDaYing', '大师兄尸体', '调查花瓣', '战后调查大师兄尸体，核对花瓣“自己的决定”。', item('F_03001')),
  step('ZhengDaoDaYing', '红衣女剑客尸体', '调查同心双鱼佩', '再调查红衣女剑客的尸体，核对饰品。两份奖励分开记录。', item('AC_LowShachi', 'accessory'))
], [], [sources.qixing.url]);
guide('BuMieXiaDan', '不灭侠胆', [
  step('ZhengDaoDaYing', '七星剑阵路线', '先推进剑阵领头人相关事件', '先见过并推进剑阵领头人的路线，再返回庞镇找狼头领。', branch('S1')),
  step('PangZhen', '“组织”地下酒馆·狼头领', '领取破碎的侠胆', '与紫衣狼头领对话，确认获得破碎的侠胆后再处理各地高手。', item('I_06006')),
  ...chivalryEncounters.map((entry, i) => step(entry.map, entry.bell + '·' + entry.name, '第' + (i + 1) + '处：帮助解脱并拾取花瓣', entry.route + '战斗中在跪地挣扎窗口帮助解脱，再调查原地花瓣。', item(entry.id))),
  finish('PangZhen', '“组织”地下酒馆·狼头领', '按任务日志回报并完成侠胆结算。任务完成或最终奖励已经取得时，无需为了计数强行重做六处。')
], ['已解脱但未拾取花瓣时可以回访原处；游戏只记录失败而未指出哪一处时，失败原因也应保留这一限制。'], [sources.chivalry.url]);
guide('HuanShuPiPa', '幻术琵琶', [
  step('PangZhen', '“组织”地下酒馆·幻鬼', '询问琵琶修复', '携白赤练手记找到幻鬼，了解三个部件与梅谷湖路线。', branch('S1')),
  step('LuJiaCun', '陆家村可探索区域', '取得琴头', '按收集点路线寻找幻术琵琶琴头。', objective(0)),
  step('ZhengDaoDaYing', '正道大营可探索区域', '取得背板', '按收集点路线寻找幻术琵琶背板；进入大营需要相应入口钥匙。', objective(2)),
  step('MeiGuHu', '机关船与湖以北路线', '取得琴弦', '推进机关船并进入湖以北的路线，取得幻术琵琶琴弦。', objective(1)),
  step('PangZhen', '“组织”地下酒馆·幻鬼', '带齐三件修复', '琴头、背板、琴弦齐备后交给幻鬼修复。', branch('2')),
  finish('MeiGuHu', '通往枷笼山的雾门', '在雾门前使用修好的琵琶，进入枷笼山并衔接后续任务。')
]);
guide('JiGuanChuan', '机关船', [
  step('MeiGuHu', '火刀与双锏高手所在路线', '取得祝原的核心', '探索梅谷湖路线，击败对应火刀高手后调查杀气改造核心。', objective(0)),
  step('MeiGuHu', '双锏高手所在路线', '取得毕峰的核心', '处理双锏高手并取得另一枚杀气改造核心。', objective(1)),
  step('PangZhen', '庞镇船坞·船老大', '回船坞交付核心', '回到船老大处处理机关船的修复目标。相关路线还需烈焰狮头及引魂能力。', branch('2')),
  finish('PangZhen', '庞镇船坞', '完成修复与登船目标，保存后核对日志，再乘船探索梅谷湖后续。')
]);
guide('KunLunSanJie', '昆仑三杰', [
  step('HouJie', '武轩战后·魏三船与井底线索', '取得大营外门钥匙', '多个线索同时出现时先找魏三船；完成相应线索并取得外门钥匙。', objective(0)),
  step('ZhengDaoDaYing', '大营外门与校场', '进入大营追查三杰', '持钥匙返回外门开门，按日志调查校场及三杰线索。', branch('2')),
  step('DengDingShanDao', '荒行子路线', '追至枷笼山', '继续三杰相关人物与荒行子的线索；先处理沿途需要救出的囚笼人物。', objective(1)),
  finish('DengDingShanDao', '山顶宗祠', '到宗祠继续剩余目标与对话，核对任务结算。')
]);
guide('HuXinMiMi', '湖心秘密', [
  step('LuJiaCun', '幻术师萤火的记忆路线', '调查萤火记忆', '从村口桥边取得第一朵记忆之花，沿陆家村探索补齐相关线索。', branch('1')),
  step('MeiGuHu', '峡谷深处·荡秽', '调查湖区深处', '进入梅谷湖后，沿峡谷深入并处理荡秽相关目标。', branch('2')),
  step('LuJiaCun', '梦差幻境入口', '完成幻境取得人偶舌头', '进入陆家村梦差密室及人偶中枢路线，取得红房子所需的人偶舌头。', item('K_04902')),
  step('XiGu', '红房子', '取得水晶人偶眼珠', '持人偶舌头回到悲泣溪谷打开红房子，调查水晶人偶眼珠【阳】。', item('K_12002')),
  step('MeiGuHu', '九十九号人偶', '镶入水晶眼珠', '返回湖区找到九十九号人偶，按当前目标镶入眼珠。', branch('S3')),
  ...['LuJiaCun', 'HouJie', 'MeiGuHu', 'DuanHunYa'].map((map, i) => step(map, ['炎茹路线', '雷婷路线', '蛊鹤路线', '岁寒路线'][i], '取得第' + (i + 1) + '张护法面具', '推进对应护法路线并取得面具；完整点位可在全收集品中按物品查看。', objective(i))),
  step('MeiGu_YiJi', '护法四像镜', '集齐四张面具后进入遗迹', '返回四像镜，完成四面具条件后进入幻术师遗迹。', branch('3')),
  finish('MeiGu_YiJi', '湖心仪式与遗迹深处', '按日志完成湖心仪式及深处目标。结局前分别复核本任务与血脉觉醒。')
]);
guide('HongFangZi', '红房子', [
  step('LuJiaCun', '梦差幻境·人偶中枢路线', '取得人偶舌头', '完成陆家村梦差密室的探索，取得对应的开门道具。', objective(0)),
  step('XiGu', '杀气塔下方红房子内门', '返回并打开内门', '回红房子使用人偶舌头开门；已消耗的本周目道具仍可作为开门证据。', branch('1')),
  finish('XiGu', '红房子内部', '调查内部的目标与眼珠线索，继续湖心秘密所需的后续。')
]);
guide('RenOuZhongShu', '人偶中枢', [
  step('LuJiaCun', '梦差密室传送门', '进入陆家村梦差幻境', '从人偶试验场东侧岔路进入密室，穿过传送门。', tag('rozs.人偶中枢.从陆家村梦差密室到达人偶中枢')),
  step('ZhengDaoDaYing', '绝命坑道梦差密室', '调查大营的梦差入口', '在正道大营找到对应密室并穿过传送门，调查与中枢相连的路线。', tag('rozs.人偶中枢.从绝命坑道梦差密室到达人偶中枢')),
  step('DengDingShanDao', '弦者人偶囚室', '调查枷笼山入口', '推进宗祠地下与弦者线索，找到囚室中的传送门。', tag('rozs.人偶中枢.从弦者人偶囚室到达人偶中枢')),
  finish('Dream', '人偶中枢', '按中枢内的机关及任务目标继续调查；三个入口分别保留到达记录，不要求重复进入已完成入口。')
]);
guide('XiGuShenChu', '溪谷深处', [
  step('XiGu', '溪谷杀气塔相关路线', '取得外门钥匙', '按门边弟子的线索探索杀气塔及周边，取得对应钥匙。', objective(0)),
  step('XiGu', '塔底营地外门', '返回外门继续调查', '带钥匙回到外门，完成对话与开门后的目标。', branch('S1')),
  finish('XiGu', '溪谷深处', '沿新开放路线完成余下调查，核对紫色任务的完成记录。')
]);
guide('XuKongHuanXiang', '虚空幻象', [
  step('FuJiZhiLin', '森林入口至森林深处', '追踪虚空幻象', '与入口虚空互动后，继续调查森林深处遇到的幻象，逐次完成对话和当前目标。', branch('1')),
  finish('FuJiZhiLin', '森林深处·虚空', '完成这条幻象路线的结算；榕树挑战及弦音花瓣有独立清单，不以本任务结束代替全收集。')
]);
guide('DaoJianFoXin', '道剑佛心', [
  step('HouJie', '废弃铁匠铺', '取得青铜精华', '到后街废弃铁匠铺调查，拾取青铜精华。', objective(0)),
  step('PangZhen', '任意已激活铃铛', '为天玄青铜剑铸魂', '携带青铜精华，在铃铛的铸魂界面按天玄青铜剑的配方完成铸魂。', branch('2')),
  finish('PangZhen', '“组织”地下酒馆·毕狄', '返回毕狄道长处完成后续对话并核对任务结算。')
]);
guide('RuMengQiZi', '入梦祈子', [
  step('LuJiaCun', '三人偶水井附近的人偶路线', '找到装有女织意识的人偶', '先在院内与昏睡的女织接任务，再调查装有她意识的人偶；避免先击败人偶才回头接取。', branch('1')),
  finish('LuJiaCun', '女织所在院落与水井周边', '按任务提示处理人偶和后续对话，回院内核对人物状态及任务结算。')
], [], ['https://www.gamersky.com/handbook/202610/2238190.shtml']);
guide('RenOuMuXin', '人偶木心', [
  step('LuJiaCun', '幸存者小院·沛郎', '把歌女人偶的信交给沛郎', '从幽蓝萤火之庙接取后，带信去幸存者小院交付。', branch('1')),
  step('LuJiaCun', '机擎巨人遗骸附近·田野营火', '调查沛郎相关肉身', '继续前往巨人遗骸附近，调查田野营火旁的肉身。', branch('2')),
  finish('LuJiaCun', '幽蓝萤火之庙·歌女人偶', '带着调查结果回歌女人偶处交付，领取人偶木心。')
]);
guide('DaHuang', '大黄', [
  step('HouJie', '狭窄民居二楼附近院落', '调查大黄尸体', '推进庞镇后街与武轩路线后到该院落，携猎户的犬铃调查大黄尸体。', branch('1')),
  finish('LuJiaCun', '幸存者小院北面·猎户', '按日志回猎户处报告大黄的下落，完成交付与结算。')
]);
guide('JiangHuRenShi', '江湖人士', [
  step('DengDingShanDao', '囚笼洞口', '开笼并完成对话', '先清理笼外敌人，打开吊笼和清醒的江湖人士交谈，再推进山道。'),
  step('DengDingShanDao', '横刀毕峰战场', '推进横刀毕峰相关目标', '沿山道击败横刀毕峰，并按日志处理江湖人士后续。', objective(0)),
  finish('DengDingShanDao', '囚笼人物后续位置', '回访当前日志所指的人物位置，完成剩余交谈。')
]);
guide('XiangJianHeJi', '相煎何急', [
  step('MeiGuHu', '梅谷村东岛船坞·农户弟弟', '跟进兄弟后续', '从青蛇屋附近的记忆花瓣开启路线后，到东岛船坞找农户的弟弟，推进相关对话。', branch('1')),
  finish('MeiGuHu', '农户兄弟相关路线', '依人物对话和剩余记忆线索完成结算；缺少可读取的细分证据时，逐步状态保留待核实。')
]);
guide('ZaiHuiXiongZhang', '再会兄长', [
  step('MeiGuHu', '陆家村浅滩船坞·阿瑾', '与阿瑾继续兄长线索', '在黄家大宅取得花瓣后，前往梅谷湖的陆家村浅滩船坞与阿瑾对话。', branch('1')),
  finish('MeiGuHu', '陆家村浅滩船坞', '调查人物后续和记忆线索，完成相关交付与结算。')
]);
guide('TouPaiHuaDan', '头牌花旦', [
  step('PangZhen', '地下酒馆入口旁的台子', '引出童子并前往天香阁', '拿到脂粉后回酒馆入口，与左侧台子互动，再到酒馆对面戏台左侧进入天香阁。', branch('1')),
  step('PangZhen', '天香阁二楼至三楼', '找到跳舞的花旦', '上二楼走到另一侧黄色房间；附近宝箱有虎型镇纸。再沿该房间左侧梯子上三楼找花旦。'),
  step('PangZhen', '天香阁三楼·花旦', '取得残破的舞扇', '与跳舞的花旦互动，领取残破的舞扇。', objective(0)),
  finish('PangZhen', '“组织”地下酒馆·头牌花旦人偶', '带舞扇返回酒馆交付，完成最后的对话。')
], [], ['https://www.gamersky.com/handbook/202610/2236093_22.shtml']);
guide('FangKuai', '方块', [
  step('MeiGuHu', '梅谷村东岛船坞·铃铛附近房屋外', '取得暗源方块·卯', '从铃铛朝左前房屋走，沿外侧楼梯上三楼，穿过左边缝隙后转右，下到宝箱处取得碎片。', objective(0)),
  step('MeiGuHu', '水行帮船坞·铃铛右侧路线', '取得暗源方块·榫', '从区域铃铛沿右侧路线清理敌人，核对另一块碎片是否发放。', objective(1)),
  finish('PangZhen', '“组织”地下酒馆·渔夫与幻鬼', '返回酒馆完成交付和后续对话。')
], [], ['https://www.gamersky.com/handbook/202610/2236093_23.shtml']);
guide('QuanXin', '拳心', [
  step('FuJiZhiLin', '冰封森林杀气塔顶·铁鬼', '谈铁拳门主往事', '冰封后上塔顶与铁鬼交谈，接下调查墓碑的目标。'),
  step('FuJiZhiLin', '遗冢地·铁拳门主墓碑', '调查墓碑并领取碎片', '回遗冢地调查墓碑，核对拳心碎片·壳；另一部件与核配对用于铁拳套铸魂。', branch('1')),
  finish('FuJiZhiLin', '冰封杀气塔顶', '按剩余日志回访对话并确认任务结算。')
]);
guide('YiBiZhiDao', '以彼之道', [
  step('FuJiZhiLin', '冰封杀气塔顶·玉妖与铁鬼', '按约定准备装备', '回塔顶与玉妖、铁鬼对话，按约定限制只装备妖锯和铁拳套。'),
  step('FuJiZhiLin', '冰封杀气塔顶', '核对妖锯', '确认指定武器已持有，替换其他主武器不会满足这项装备条件。', objective(0)),
  step('FuJiZhiLin', '冰封杀气塔顶', '核对铁拳套', '确认指定影之武装已持有，再进行相应切磋。', objective(1)),
  finish('FuJiZhiLin', '冰封杀气塔顶', '使用约定装备完成切磋，战后继续对话并检查奖励。')
]);
guide('SanMianHunDun', '三面混沌', [
  step('XiGu', '圣母像裂缝', '首次挑战三面魔偶', '进入裂缝试炼，完成首次挑战。早期强度较高，可以稍后回来。', objective(0)),
  step('XiMeng', '三面魔偶身份与记忆线索', '调查三面相关记忆', '进入戏梦山庄后，继续身份与记忆目标；具体花瓣会在下面按物品分别核对。', branch('S2')),
  ...[2, 3, 4].map((index, i) => step('FuJiZhiLin', '镜林·三面魔偶记忆点', '取得第' + (i + 1) + '片三面记忆', '沿镜林相关记忆点调查，核对这一片花瓣的本周目记录。', objective(index))),
  step('FuJiZhiLin', '冰封镜林·最终战前', '准备炽刃', '按任务目标取得并装备炽刃，再开始对应最终战。', item('XieHuo', 'weapon')),
  step('FuJiZhiLin', '镜林最终战', '按指定武器完成最终战', '以炽刃击败对应魔偶，核对特殊条件分支。普通击败不能代替指定武器条件。', branch('1')),
  finish('FuJiZhiLin', '最终战后的奖励处', '调查战后奖励，核对铸魂材料及三面混沌任务结算。')
], [], [sources.missables.url]);

// 独立收集任务：逐个目标都有地图和对应存档证据；运行时补入物品名称及已核对点位的文字路线。
const collectionRoutes = {
  RongShuZhiXin: ['榕树之心', ['FuJiZhiLin','ZhengDaoDaYing','HouJie','MeiGuHu','DengDingShanDao'], '人面榕树挑战', '与榕树互动，完成对应挑战。到访记录不能代替挑战完成事件。'],
  XueMaiJueXing: ['血脉觉醒', ['XiGu','FuJiZhiLin','XiMeng','MeiGuHu','MeiGu_YiJi'], '龙柱试炼', '在龙柱处互动并完成试炼；五处结算分别核对。'],
  ShaoZhu: ['少主', ['LuJiaCun'], '沐小葵沿途的红丝带', '调查红丝带并取得记忆线索。乘升降机挑战沐小葵前核对五条全部收齐。'],
  XianYin: ['弦音', ['FuJiZhiLin'], '镜林·虚空记忆之花', '调查对应记忆之花。六朵分别记录，首领战支援与花瓣收集分别核对。'],
  QiXingJianZhen: ['七星弟子', ['ZhengDaoDaYing'], '七星剑阵沿途·弟子遗体', '沿剑阵路线调查对应弟子遗体和记忆之花。不要把大师兄战奖励当作这六朵全部完成。'],
  WuShiZunZhu: ['舞狮尊主', ['PangZhen','HouJie','HouJie','HouJie','HouJie','HouJie'], '舞狮尊主记忆路线', '调查对应的舞狮尊主记忆花瓣。首领击败记录不能代替花瓣实际拾取。'],
  ZhengYiMengChen: ['正义蒙尘', ['DengDingShanDao'], '荒行子记忆之花', '沿枷笼山调查对应荒行子记忆之花；先处理滑墙途中可落脚的平台，避免漏掉支路。'],
  BaiYing: ['白影', ['MeiGu_YiJi'], '幻术师遗迹·白影记忆', '进入遗迹需要四张护法面具，沿遗迹调查这片白影记忆。'],
  XiongFu: ['凶斧', ['XiGu'], '四凶斧案卷', '调查对应案卷。需要手令的交付应先处理；后续密信须推进一个不留后返回圣母像山洞路线。'],
  XiaoXue: ['小雪', ['ZhengDaoDaYing'], '小雪要求的大侠信物', '按物品清单寻找对应信物，取得后再推进八卦阁顶层的交付。']
};
for (const [id, [name, maps, place, text]] of Object.entries(collectionRoutes)) {
  const objectives = questObjectives[id];
  const steps = objectives.map((entry, i) => step(maps[i] || maps[0], place,
    entry.name || '收集目标 ' + (i + 1), text, objective(i), { targetItem:entry.item || '' }));
  if (id === 'XiongFu') steps.push(finish('PangZhen', '八卦阁顶层·八卦童子', '案卷与彭猛相关目标处理后，把收齐的七卷交给八卦童子，核对奖励。'));
  if (id === 'XiaoXue') steps.push(finish('PangZhen', '八卦阁顶层·八卦童子', '取得所需信物并持八卦阁钥匙进入顶层，完成清单交付。'));
  guide(id, name, steps);
}
guide('CiMuXin', '慈母心', [
  step('HouJie', '后山凉亭后的屋顶宝箱', '取得秘密地窖开关', '沿后山凉亭方向经过房顶，调查宝箱取得后街秘密地窖开关。'),
  finish('HouJie', '贫民窟·后街秘密地窖', '第一片在屋顶梯子下的契据旁。由贫民窟铃铛往上进入秘密地窖，穿过后拾取第二片，核对慈花甘露及任务结算。')
]);
guide('XiLouFeiXu', '浮华引', [
  ...[0, 1, 2, 3, 4, 5].map(i => step('MeiGuHu', '梅谷乐坊岛周围水面', '寻找烟儿第' + (i + 1) + '朵记忆', '乘船探索乐坊岛周围水面，逐朵调查烟儿记忆之花。', objective(i))),
  step('MeiGuHu', '湖面相关战场·烟千岁', '完成烟千岁路线', '收集线索后按日志继续烟千岁相关目标并完成战斗。', objective(6)),
  finish('MeiGuHu', '乐坊岛与烟千岁后续', '检查战后对话、记忆与任务结算。')
]);
guide('JiaoTuSanKu', '狡兔三窟', [
  step('FuJiZhiLin', '血色森林小张藏身处', '与获救的小张交谈', '先放下挂笼小道的囚笼并开门救人，再到藏身处谈各地分身。', tag('fjzl.小张.打开笼子对话后')),
  ...[['LuJiaCun','陆家村'],['HouJie','庞镇后街'],['MeiGuHu','梅谷湖'],['XiMeng','戏梦山庄']].map(([map, name]) =>
    step(map, name + '小张藏身处', '找到' + name + '的小张', '到该地区的小张藏身处完成对话，保存后核对到访与交付记录；已见过的分身不必反复寻找。')),
  finish('XiMeng', '各地小张最后的对话', '按日志完成剩余分身会面；所有分身已见而任务仍活动时，以实际对话和日志为准。')
]);
const treasureMaps = { '1':'FuJiZhiLin', '3':'LuJiaCun', '4':'HouJie', '5':'MeiGuHu', '6':'XiMeng' };
guide('MiaoShouKongKong', '妙手拾遗', treasureGroups.flatMap(group => [
  ...group.ids.map(id => step(treasureMaps[group.branch[0]], group.name + '珍宝点', '取得本区珍宝',
    '按地图或全收集品中的对应点位寻找这件珍宝。已交付时背包数量为零仍可证明曾取得。', item(id), { targetItem:id })),
  step(treasureMaps[group.branch[0]], group.name + '小张藏身处', '交付' + group.name + '珍宝',
    '本地区珍宝齐备后，回小张处交付并领取对应奖励。收齐和交付分别记录。', branch(group.branch))
]), ['五个地区分别交付。梅谷湖以本机任务条件和物品表的三件为准，不跟随资料中存在争议的数量。'], [sources.treasures.url]);

const mainRoutes = {
  'MainCh1-NiLiuErShang': ['逆流而上', [
    step('XiGu','开端之地至驿站','沿溪谷抵达驿站','沿主线穿过溪谷，处理挡路敌人与红丝线，激活沿途铃铛。', branch('1')),
    step('XiGu','驿站与杀气塔方向','推进驿站主线','调查主线人物与牢门，处理彭猛等沿途首领；离开前复核玉儿与老弓手的任务。', branch('2')),
    finish('XiGu','溪谷主线通路','按当前日志继续离开溪谷的目标，核对任务完成记录。')]],
  'MainCh1-2-GangTieJuTa': ['钢铁巨塔', [
    step('XiGu','溪谷杀气塔','逐层推进巨塔','沿塔内主线处理机关与敌人，注意塔顶鬼差及第一块浑源石的交付。', branch('1')),
    step('XiGu','追命战场','击败追命并取得武装','完成主线首领战，调查并取得后续探索需要的追命锁镰。', branch('2')),
    finish('XiGu','巨塔后的主线出口','沿日志打开后续通路。')]],
  'MainCh1-LinZhongShaJi': ['林中杀机', [
    step('FuJiZhiLin','森林入口至飞贼营地','穿过森林并处理营地事务','沿主线推进；先接白山秘闻、救小张并结清玉儿和营地交付。', branch('1')),
    step('FuJiZhiLin','镜林与白赤练战场','推进森林首领路线','探索镜林，战前复核可错过内容，再挑战白赤练。', branch('2')),
    finish('FuJiZhiLin','白赤练后的通道','调查战后通路，前往陆家村。')]],
  'MainCh1-ShiXinCun': ['失心村', [
    step('LuJiaCun','三人偶水井与村中藏身处','调查陆家村','先与老根头和兰少狄接任务，再推进水井、人偶与三位大师的目标。', branch('1')),
    ...[0,1,2].map(i => step('LuJiaCun','三位大师相关路线','让第' + (i + 1) + '位大师安息','分别处理各位大师的解脱目标。', objective(i))),
    step('LuJiaCun','水井与飞贼密道','打通井底密道','取得主线所需道具并打通密道，先回村口向两人报告。', branch('4')),
    finish('PangZhen','飞贼密道出口','完成回报后从密道进入庞镇，继续镇内主线。')]],
  'MainCh2-SiZhen': ['死镇', [
    step('PangZhen','镇中心与地下酒馆','调查镇内人物','按主线进入“组织”地下酒馆，与幻鬼等人物交谈，开启船与琵琶的后续。', branch('1')),
    step('HouJie','后街入口与镇内主线','探索后街','处理进入后街的首领与入口条件，再按日志调查后街。', branch('3')),
    finish('PangZhen','庞镇主线人物处','完成本段调查，继续心之踪迹及四大蛇屋。')]],
  'MainCh2-XinZhiZongJi': ['心之踪迹', [
    step('HouJie','常虹战后·玉儿家','调查玉儿父亲','完成常虹相关路线后进入玉儿家。血肉花交付有独立状态，需要一并核对。', branch('1')),
    step('PangZhen','景炀轩·雷当','推进雷当线索','沿主线调查景炀轩，处理雷当相关目标。', branch('3')),
    step('HouJie','舞狮馆大门与深处','进入舞狮馆','取得所需钥匙后进入舞狮馆，处理尊主及后续能力。', branch('6')),
    finish('HouJie','青蛇屋通往戏梦山庄的密道','进入山庄前复核十个紫色任务及枷笼山救人，确认后通过密道。')]],
  'MainCh2-SiDaSheWu': ['四大蛇屋', [
    step('HouJie','舞狮尊主与雷当路线','取得并修复烈焰狮头','推进相关主线与首领，取得烧枝能力后再探索对应入口。', branch('S1')),
    ...['HouJie','FuJiZhiLin','LuJiaCun'].map((map, i) => step(map,['青蛇屋','赤蛇屋','黑蛇屋'][i],'取得并调查第' + (i + 1) + '件机关物品','进入对应蛇屋，按蛇形机关及当前日志核对蛇眼、蛇信或蛇牙。', objective(i))),
    finish('MeiGuHu','白蛇屋大门','取得游龙信物并满足开门条件后，继续白蛇屋目标。')]],
  'MainCh3-PoJu': ['破局', [
    step('XiMeng','演武场','取得戏票','推进演武场相关目标，取得进入戏楼所需的戏票。', objective(0)),
    step('XiMeng','戏楼入口左侧房间','先拿戏楼珍宝','用票进入后先拿金银香囊，再触发演出与后续主线。', item('T_12002')),
    step('XiMeng','山庄与可轰开的墙壁','完成山庄调查','按日志调查戏楼与山庄，使用已取得的武装打开对应通路。', branch('2')),
    finish('DuanHunYa','断魂山入口','完成山庄本段主线后前往断魂山；冰封区域也会开放后期回访。')]],
  'MainCh3-LongYuZuo': ['龙与座', [
    step('DuanHunYa','最后的正道营地','先处理小翠战前交付','在开启力者和锯者战之前先交信物并安葬；战后领取奖励另行核对。', {kind:'externalBranch', id:'XiaoCui', step:'5'}),
    step('DuanHunYa','断魂山与断魂崖','沿最终主线推进','继续本章首领和断魂崖目标。决定结局前复核湖心秘密与血脉觉醒。', branch('1')),
    finish('DuanHunYa','终局路线','按日志完成终局。开启新周目前再核对成就和未领取奖励。')]]
};
for (const [id, [name, steps]] of Object.entries(mainRoutes)) guide(id, name, steps, [], [sources.walkthrough.url]);

for (const [id, reason] of Object.entries(questArchive)) {
  questGuides[id] = { name:id, archive:true, steps:[], notes:[reason], sources:[] };
}

// 节点的完整流程复用每个检查项及其关联任务；每个检查项至少有一个明确的地图入口。
export const checkpointMaps = {
  valley:'XiGu', jingye:'XiGu', baichilian:'FuJiZhiLin', luxiaokui:'LuJiaCun', 'mu-rewards':'LuJiaCun',
  'lu-well':'LuJiaCun', pangzhen:'LuJiaCun', qixing:'ZhengDaoDaYing', baishan:'ZhengDaoDaYing',
  chivalry:'ZhengDaoDaYing', mountain:'DengDingShanDao', qingshe:'DengDingShanDao', mirage:'HouJie',
  opera:'XiMeng', sanmian:'FuJiZhiLin', snow:'FuJiZhiLin', crusher:'DuanHunYa', 'crusher-reward':'DuanHunYa',
  ending:'DuanHunYa', treasures:'Any', cleanup:'Any'
};
