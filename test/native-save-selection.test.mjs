import { test } from 'node:test';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

test('原生窗口支持手动选档、刷新、槽位切换及返回自动查找', { skip: process.platform !== 'win32', timeout: 60000 }, () => {
  const root = fileURLToPath(new URL('../', import.meta.url));
  const work = path.join(root, '.local');
  fs.mkdirSync(work, { recursive: true });
  const temp = fs.mkdtempSync(path.join(work, 'save-selection-test-'));
  const run = (command, args) => {
    const result = spawnSync(command, args, { encoding: 'utf8', windowsHide: true, timeout: 30000 });
    if (result.error) throw result.error;
    assert.equal(result.status, 0, result.stdout + result.stderr);
    return result.stdout;
  };
  try {
    const key = crypto.randomBytes(32);
    const seal = raw => {
      const nonce = crypto.randomBytes(12), cipher = crypto.createCipheriv('aes-256-gcm', key, nonce);
      const encrypted = Buffer.concat([cipher.update(raw), cipher.final()]);
      return Buffer.concat([Buffer.from('50425a5301010000', 'hex'), nonce, cipher.getAuthTag(), encrypted]);
    };
    const game = path.join(temp, 'game');
    fs.mkdirSync(path.join(game, 'platform/steam'), { recursive: true });
    const image = Buffer.alloc(256), anchor = Buffer.from('sealed but block is PLAINTEXT\0', 'utf16le');
    anchor.copy(image, 32);
    const share = crypto.randomBytes(32), offset = 32 + anchor.length;
    share.copy(image, offset);
    Buffer.from(share.map((value, index) => value ^ key[index])).copy(image, offset + 32);
    fs.writeFileSync(path.join(game, 'platform/steam/PBZ.exe'), image);

    // 独立构造真实加密容器；测试仍使用生产代码解密和分析槽位。
    const save = (name, cycle, preferred) => {
      const slot = value => ({
        PBZGameSystem: { CycleCount: value, DateTimeString: `2026-11-15 20:00:0${value}`, CurrentLevelName: 'XiGu' },
        Narrative: { NarrativeSavedQuests: [] }, EventPipeline: { GlobalTags: [] }
      });
      const entries = { AutoSave0: slot(cycle), ManualSave0: slot(cycle + 1), __Advanced__: { LastContinueSlot: preferred } };
      const rows = [], blocks = [];
      let blockOffset = 0;
      for (const [slotName, json] of Object.entries(entries)) {
        const label = Buffer.from(slotName), raw = Buffer.from(JSON.stringify(json)), block = seal(raw);
        const row = Buffer.alloc(2 + label.length + 14);
        row.writeUInt16LE(label.length); label.copy(row, 2);
        const at = 2 + label.length;
        row[at + 1] = 1; row.writeUInt32LE(raw.length, at + 2);
        row.writeUInt32LE(blockOffset, at + 6); row.writeUInt32LE(block.length, at + 10);
        rows.push(row); blocks.push(block); blockOffset += block.length;
      }
      const header = Buffer.alloc(12);
      header.write('PBZS'); header[4] = 3; header[5] = 1; header.writeUInt16LE(rows.length, 6);
      header.writeUInt32LE(12 + rows.reduce((sum, row) => sum + row.length, 0) + 36, 8);
      const file = path.join(temp, name);
      fs.writeFileSync(file, Buffer.concat([header, seal(Buffer.concat([header, ...rows])), ...blocks]));
      return file;
    };
    const automatic = save('account-a.sav', 0, 'ManualSave0');
    const secondAccount = save('account-b.sav', 4, 'AutoSave0');
    const manual = save('中文 手动备份.sav', 2, 'ManualSave0');
    const updated = save('updated.sav', 6, 'ManualSave0');
    const invalid = path.join(temp, 'invalid.sav');
    fs.writeFileSync(invalid, Buffer.alloc(64));

    const guides = path.join(temp, 'guides.json');
    run(process.execPath, [path.join(root, 'native/build-data.mjs'), guides]);
    const compiler = path.join(process.env.WINDIR, 'Microsoft.NET/Framework64/v4.0.30319/csc.exe');
    const exe = path.join(temp, 'SaveSelectionTest.exe');
    const sources = fs.readdirSync(path.join(root, 'native'))
      .filter(name => name.endsWith('.cs') && !['Entry.cs', 'GameDiscovery.cs'].includes(name))
      .map(name => path.join(root, 'native', name));
    run(compiler, ['/nologo', '/target:exe', '/platform:x64', '/codepage:65001', `/out:${exe}`, `/resource:${guides},PBZ.Guides`,
      '/reference:System.Web.Extensions.dll', '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll',
      ...sources, path.join(root, 'test/native-save-selection.cs')]);
    const output = run(exe, [game, automatic, secondAccount, manual, updated, invalid]);
    assert.match(output, /存档选择验证通过/);
  } finally {
    assert.equal(path.dirname(path.resolve(temp)), path.resolve(work));
    fs.rmSync(temp, { recursive: true, force: true });
  }
});
