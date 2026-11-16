import { test } from 'node:test';
import assert from 'node:assert/strict';
import { questGuides, checkpointMaps } from '../reference/quest-guides.mjs';
import { navigationDefinitions, navigationMaps } from '../reference/quest-navigation.mjs';
import { questOffers } from '../reference/quest-offers.mjs';
import { questArchive } from '../reference/quest-catalog.mjs';
import { checkpoints } from '../reference/rules.mjs';
import { questObjectives } from '../reference/objective-progress.mjs';

test('项目已知任务及全部行前节点均有文字流程或明确的资源留档说明', () => {
  const tasks = new Set([...navigationDefinitions.keys()].map(key => key.split(':')[0]).concat(
    Object.keys(questOffers), Object.keys(questArchive), checkpoints.flatMap(g => g.requirements.map(r => r.quest).filter(Boolean))));
  assert.ok(tasks.size >= 71);
  for (const id of tasks) {
    const guide = questGuides[id];
    assert.ok(guide, id);
    if (questArchive[id]) {
      assert.equal(guide.archive, true, id);
      assert.deepEqual(guide.steps, [], id + '不能为未确认可玩的资源虚构流程');
      assert.ok(guide.notes.length > 0, id);
    } else {
      assert.ok(guide.steps.length >= 2, id);
      assert.ok(guide.sources.length > 0, id);
    }
  }
  for (const checkpoint of checkpoints) assert.ok(navigationMaps[checkpointMaps[checkpoint.id]] || checkpointMaps[checkpoint.id] === 'Any', checkpoint.id);
});

test('每个攻略步骤都明确地图、地点和操作，目标证据不越界', () => {
  for (const [id, guide] of Object.entries(questGuides)) for (const step of guide.steps) {
    assert.ok(navigationMaps[step.map] || ['Any', 'Dream'].includes(step.map), id + ':' + step.map);
    for (const key of ['place', 'title', 'text']) assert.ok(step[key].trim(), id + ':' + key);
    if (step.evidence?.kind === 'objective') assert.ok(questObjectives[id]?.[step.evidence.index], id + ':' + step.evidence.index);
    assert.notEqual(step.state, 'done', '攻略文本不能预置玩家完成状态');
  }
});

test('小翠的跨地图流程、战前交付与战后奖励分开，白雪儿保留两种陈柱分支', () => {
  const cui = questGuides.XiaoCui.steps;
  assert.ok(cui.some(step => step.map === 'LuJiaCun'));
  assert.ok(cui.some(step => step.map === 'DengDingShanDao'));
  const delivery = cui.findIndex(step => step.evidence?.step === '5');
  const reward = cui.findIndex(step => step.evidence?.id === 'TU_11007');
  assert.ok(delivery >= 0 && reward > delivery);
  const choices = questGuides.YongShengDaoZhu.steps.filter(step => step.unlessTag);
  assert.equal(choices.length, 2);
  assert.notEqual(choices[0].unlessTag, choices[1].unlessTag);
});

test('奖励核对显式标注领取条件，准备物品和珍宝收集不会被当作结算漏领', () => {
  for (const [id, guide] of Object.entries(questGuides)) for (const step of guide.steps.filter(s => s.reward)) {
    assert.equal(step.evidence?.kind, 'item', id + ':' + step.title);
    assert.ok(['done', 'branch', 'crusherVictory'].includes(step.reward.kind), id + ':' + step.title);
    if (step.reward.kind === 'branch') assert.ok(step.reward.step, id);
  }
  assert.equal(questGuides.XiaoCui.steps.find(s => s.evidence?.id === 'TU_11007').reward.kind, 'crusherVictory');
  assert.equal(questGuides.GuiTu.steps.find(s => s.evidence?.id === 'K_05004').reward.step, '2');
  for (const id of ['HuXinMiMi', 'SanMianHunDun', 'MiaoShouKongKong']) assert.ok(questGuides[id].steps.every(s => !s.reward), id);
  const checks = checkpoints.flatMap(g => g.requirements);
  assert.ok(checks.filter(c => c.reward).every(c => c.check === 'item'));
  for (const item of ['XieHuo', 'R_070001', 'T_12002']) assert.ok(checks.filter(c => c.item === item).every(c => !c.reward), item + '是任务前置或普通收集');
});
