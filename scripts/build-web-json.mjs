// 从源数据 bg_cards.json 生成网页版精简数据 cards.json
// 由 GitHub Actions 在构建 DLL 后调用，产物随 Release 发布
// 用法: node scripts/build-web-json.mjs <bg_cards.json 路径> <输出路径>
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { dirname } from 'node:path';

const [input, output] = process.argv.slice(2);
if (!input || !output) {
  console.error('用法: node build-web-json.mjs <input bg_cards.json> <output cards.json>');
  process.exit(1);
}

// 从 HearthstoneJSON 获取 dbfId → cardId 映射，用于解析金色版本 cardId
console.log('📡 获取 HearthstoneJSON 数据...');
const hsRes = await fetch('https://api.hearthstonejson.com/v1/latest/zhCN/cards.json');
const hsCards = await hsRes.json();
const hsByDbfId = new Map(hsCards.map(c => [c.dbfId, c.id]));
console.log(`✓ ${hsByDbfId.size} 条 dbfId→cardId 映射`);

const raw = JSON.parse(readFileSync(input, 'utf8'));

const cards = raw.cards
  .filter(c => c.cardType === 'minion' && !c.isToken && !c.isDuosOnly)
  .map(c => ({
    cardId: c.cardId,
    goldenCardId: c.dbfIdGold ? hsByDbfId.get(c.dbfIdGold) ?? `${c.cardId}_G` : `${c.cardId}_G`,
    name: (c.name || '').trim(),
    nameZh: (c.nameZh || '').trim(),
    textZh: c.textZh || '',
    tier: c.tier ?? 0,
    minionType: c.minionType ?? '',
    attack: c.attack ?? 0,
    health: c.health ?? 0,
    keywords: c.keywords ?? [],
    isBuddy: !!c.isBuddy,
    isTimewarped: !!c.isTimewarped,
    dbfIdGold: c.dbfIdGold ?? null,
  }))
  .sort((a, b) => (a.tier - b.tier) || a.nameZh.localeCompare(b.nameZh, 'zh'));

const out = {
  version: raw.meta?.version ?? 'unknown',
  generatedAt: new Date().toISOString().slice(0, 10),
  count: cards.length,
  cards,
};

mkdirSync(dirname(output), { recursive: true });
writeFileSync(output, JSON.stringify(out));
console.log(`cards.json 生成完毕: v${out.version}, ${out.cards.length} 个随从`);
