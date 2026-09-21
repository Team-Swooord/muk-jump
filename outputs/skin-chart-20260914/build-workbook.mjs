import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { Workbook, SpreadsheetFile, FileBlob } from '@oai/artifact-tool';

const dir = path.dirname(fileURLToPath(import.meta.url));
const rows = JSON.parse(await fs.readFile(path.join(dir, 'skin-catalog.json'), 'utf8'));
const headers = Object.keys(rows[0]);
const numeric = new Set(['schemaVersion', 'skinId', 'unlockGoal', 'cooldownSeconds', 'unlockBalanceVersion']);
if (process.argv.includes('--sync-unlock-curve')) {
  const file = path.join(dir, '먹점프_스킨_뒤끝차트_v1.xlsx');
  const current = await SpreadsheetFile.importXlsx(await FileBlob.load(file));
  const sheet = current.worksheets.getItem('Skins');
  const before = sheet.getRange('A1:Y63').values;
  const fields = before[0];
  const source = new Map(rows.map(row => [Number(row.skinId), row]));
  const allowed = new Set(['unlockGoal', 'unlockRuleKo', 'unlockRuleEn', 'unlockRuleJa']);
  const hintIds = new Set([5, 33, ...Array.from({length: 15}, (_, i) => i + 46)]);
  let changed = 0;
  for (let r = 1; r < before.length; r++) {
    const id = Number(before[r][fields.indexOf('skinId')]);
    const row = source.get(id);
    if (!row || Number(row.unlockBalanceVersion) !== 2) throw Error(`Missing balance-v2 skin ${id}`);
    for (let c = 0; c < fields.length; c++) {
      const field = fields[c];
      if (!allowed.has(field) && !(hintIds.has(id) && /^lore(Ko|En|Ja)$/.test(field))) continue;
      const value = numeric.has(field) ? Number(row[field]) : row[field];
      sheet.getCell(r, c).values = [[value]];
      if (value !== before[r][c]) changed++;
    }
  }
  sheet.getRange('Z1:Z63').values = [['unlockBalanceVersion'], ...rows.map(() => [2])];
  sheet.getRange('Z1:Z63').format.columnWidth = 24;
  sheet.getRange('Z1:Z63').format.font = {name: 'Apple SD Gothic Neo', size: 11, color: '#333333'};
  sheet.getRange('Z1:Z63').format.fill = '#F3F2EE';
  sheet.getRange('Z1:Z63').format.wrapText = true;
  sheet.getRange('Z2:Z63').setNumberFormat('0');
  const oldTable = sheet.tables.items.find(t => t.name === 'MukJumpSkinConfig');
  if (!oldTable) throw Error('Skin table missing');
  const style = oldTable.style;
  oldTable.delete();
  const table = sheet.tables.add('A1:Z63', true, 'MukJumpSkinConfig');
  table.style = style;
  table.showFilterButton = true;
  sheet.getRange('Z1').format.fill = '#282623';
  sheet.getRange('Z1').format.font = {name: 'Apple SD Gothic Neo', size: 11, bold: true, color: '#FFFFFF'};
  current.comments.setSelf({displayName: '먹점프'});
  current.comments.addThread({cell: sheet.getRange('Z1')}, '고정: 해금 곡선 버전 2. 이 차트는 새 앱 버전에서 사용합니다. 구버전 앱에 전역 배포하지 마세요.');
  current.recalculate();
  const after = sheet.getRange('A1:Y63').values;
  for (let r = 0; r < before.length; r++) for (let c = 0; c < fields.length; c++) {
    const id = r > 0 ? Number(before[r][fields.indexOf('skinId')]) : -1;
    const editable = r > 0 && (allowed.has(fields[c]) || hintIds.has(id) && /^lore(Ko|En|Ja)$/.test(fields[c]));
    if (!editable && before[r][c] !== after[r][c]) throw Error(`Unrelated cell changed ${r}:${c}`);
  }
  if (sheet.getRange('A1:Z63').values.flat().some(v => typeof v === 'string' && /^#(REF!|VALUE!|DIV\/0!|N\/A|NAME\?|NUM!|SPILL!)/.test(v))) throw Error('Spreadsheet error');
  await (await SpreadsheetFile.exportXlsx(current)).save(file);
  const preview = await current.render({sheetName: 'Skins', range: 'J2:N8', scale: 1.2, format: 'png'});
  const previewPath = path.resolve(dir, '../../output/qa/skin-skills/unlock-curve-sheet.png');
  await fs.writeFile(previewPath, new Uint8Array(await preview.arrayBuffer()));
  console.log(JSON.stringify({changedCells: changed, preservedUnrelatedCells: true, skins: source.size, columns: 26, file, previewPath}));
  process.exit(0);
}
if (process.argv.includes('--preview-current') || process.argv.includes('--clean-descriptions')) {
  const file = path.join(dir, '먹점프_스킨_뒤끝차트_v1.xlsx');
  const current = await SpreadsheetFile.importXlsx(await FileBlob.load(file));
  const skinSheet = current.worksheets.getItem('Skins');
  const before = skinSheet.getRange('A1:Y63').values;
  const clean = text => text.replace(/。(?=\S)/g, ' ').replace(/。/g, '').replace(/(?<!\.)\.(?!\.)(?=\s|$)/g, '');
  if (process.argv.includes('--clean-descriptions')) {
    for (const area of ['G2:I63', 'U2:W63']) {
      const cells = skinSheet.getRange(area);
      cells.values = cells.values.map(row => row.map(value => typeof value === 'string' ? clean(value) : value));
    }
    current.recalculate();
    const after = skinSheet.getRange('A1:Y63').values;
    let changed = 0;
    for (let r = 0; r < before.length; r++) for (let c = 0; c < before[r].length; c++) {
      const expected = r > 0 && [6, 7, 8, 20, 21, 22].includes(c) && typeof before[r][c] === 'string'
        ? clean(before[r][c]) : before[r][c];
      if (after[r][c] !== expected) throw Error(`Unexpected edit at ${r}:${c}`);
      if (after[r][c] !== before[r][c]) changed++;
    }
    await (await SpreadsheetFile.exportXlsx(current)).save(file);
    console.log(JSON.stringify({ changedDescriptionCells: changed, preservedOtherCells: true }));
  }
  const preview = await current.render({ sheetName: 'Skins', range: 'D2:I4', scale: 1.4, format: 'png' });
  const previewPath = path.resolve(dir, '../../output/qa/skin-skills/description-sheet.png');
  await fs.writeFile(previewPath, new Uint8Array(await preview.arrayBuffer()));
  console.log(previewPath);
  process.exit(0);
}
if (rows.length !== 62 || new Set(rows.map(x => x.skinId)).size !== 62 || rows.some(x => x.skinId === '20')) throw Error('Invalid skin inventory');
for (const row of rows) for (const [key, value] of Object.entries(row)) {
  if (!/^[A-Za-z][A-Za-z0-9]*$/.test(key) || key === 'id' || value.includes('\\')) throw Error('BACKND format violation');
}
const wb = Workbook.create();
const sheet = wb.worksheets.add('Skins');
const values = [headers, ...rows.map(row => headers.map(key => numeric.has(key) ? Number(row[key]) : row[key]))];
const grid = sheet.getRange('A1:Z63');
grid.values = values;
grid.format.font = { name: 'Apple SD Gothic Neo', size: 11, color: '#333333' };
grid.format.fill = '#F3F2EE';
grid.format.wrapText = true;
grid.format.verticalAlignment = 'center';
grid.format.rowHeight = 76;
sheet.showGridLines = false;
sheet.freezePanes.freezeRows(1);
sheet.freezePanes.freezeColumns(3);
sheet.tabColor = '#AE1C3C';
const widths = [18,10,22,25,27,25,40,44,40,15,21,14,17,42,46,44,21,22,23,23,52,58,54,21,33,24];
headers.forEach((key, i) => {
  const column = String.fromCharCode(65 + i);
  sheet.getRange(`${column}1:${column}63`).format.columnWidth = widths[i];
  if (numeric.has(key)) sheet.getRange(`${column}2:${column}63`).setNumberFormat('0');
});
for (const area of ['D2:I63', 'R2:W63']) {
  sheet.getRange(area).format.fill = '#FFF6DF';
  sheet.getRange(area).format.font.color = '#71461A';
}
rows.forEach((row, i) => {
  const r = i + 2;
  if (row.goalEditable === 'YES') {
    sheet.getRange(`L${r}`).format.fill = '#FFF0C8';
    sheet.getRange(`L${r}`).format.font = { bold: true, color: '#91531F' };
    sheet.dataValidations.add({ range: `L${r}`, rule: { type: 'whole', operator: 'between', formula1: 1, formula2: 100000000 } });
  }
  if (Number(row.cooldownSeconds) > 0) {
    sheet.getRange(`X${r}`).format.fill = '#FFF0C8';
    sheet.getRange(`X${r}`).format.font = { bold: true, color: '#91531F' };
    sheet.dataValidations.add({ range: `X${r}`, rule: { type: 'whole', operator: 'between', formula1: 1, formula2: 600 } });
  }
  if (row.abilityKey === 'None') sheet.getRange(`R${r}:W${r}`).format.fill = '#F3F2EE';
});
const table = sheet.tables.add('A1:Z63', true, 'MukJumpSkinConfig');
table.style = 'TableStyleLight1';
table.showFilterButton = true;
sheet.getRange('A1:Z1').format.fill = '#282623';
sheet.getRange('A1:Z1').format.font = { bold: true, color: '#FFFFFF', size: 11 };
sheet.getRange('A1:Z1').format.rowHeight = 38;
sheet.getRange('D2:F63').format.font.bold = true;
sheet.getRange('A2:B63').format.horizontalAlignment = 'center';
sheet.getRange('L2:M63').format.horizontalAlignment = 'center';
wb.comments.setSelf({ displayName: '먹점프' });
const notes = {
  schemaVersion: '고정: 스키마 버전 1. 시트를 추가하지 마세요. 노란색 셀만 편집합니다.',
  skinId: '고정: 저장·해금에 연결된 ID. 삭제·재배열·재사용 금지. 은퇴 ID 20은 제외됨.',
  skinKey: '고정: 게임 코드와 연결되는 스킨 키.',
  nameKo: '편집 가능: 한국어 이름. 80자 이하.', nameEn: '편집 가능: 영어 이름.', nameJa: '편집 가능: 일본어 이름.',
  loreKo: '편집 가능: 한국어 배경 이야기.', loreEn: '편집 가능: 영어 배경 이야기.', loreJa: '편집 가능: 일본어 배경 이야기.',
  tier: '고정: 현재 의상 등급. Hidden / UltraHidden은 발견 전 숨김.',
  unlockMetric: '고정: 조건 종류. 목표 숫자는 unlockGoal에서 편집합니다.',
  unlockGoal: '노란색만 편집: 일반 해금 목표. goalEditable=NO인 복합 히든·연동·이벤트는 고정.',
  goalEditable: '안내용: YES이면 목표 숫자 편집 가능. NO이면 복합 규칙을 코드에서 관리.',
  unlockRuleKo: '안내용: 추출 시점 조건. 수정해도 게임 규칙은 바뀌지 않습니다. 일반 조건 문구는 게임에서 목표 숫자로 자동 작성됩니다.',
  unlockRuleEn: '안내용: 영어 조건.', unlockRuleJa: '안내용: 일본어 조건.',
  abilityKey: '고정: 기존 능력 구현과 연결. None이면 장식 전용.',
  abilityNameKo: '편집 가능: 한국어 능력 이름.', abilityNameEn: '편집 가능: 영어 능력 이름.', abilityNameJa: '편집 가능: 일본어 능력 이름.',
  abilityDescriptionKo: '편집 가능: 한국어 능력 설명. {cooldown}은 숫자로 자동 치환됩니다. 삭제하지 마세요. 문구만으로 새 능력이 생기지는 않습니다.',
  abilityDescriptionEn: '편집 가능: 영어 능력 설명. {cooldown} 유지.', abilityDescriptionJa: '편집 가능: 일본어 능력 설명. {cooldown} 유지.',
  cooldownSeconds: '노란색만 편집: 바람부채·각시탈·거미탈 쿨타임, 1~600초. 게임 타이머와 HUD에 적용. 나머지 0은 고정.',
  assetKey: '참고: 앱에 포함된 스프라이트. 빈 값은 기본 먹 또는 애니메이션 전용. 엑셀로 새 이미지를 추가하지 않습니다.',
  unlockBalanceVersion: '고정: 해금 곡선 버전 2. 새 앱용 차트이며 구버전 앱에 전역 배포하지 않습니다.'
};
headers.forEach((h, i) => wb.comments.addThread({ cell: sheet.getCell(0, i) }, notes[h]));
wb.recalculate();
if (grid.values.length !== 63 || grid.values[62][1] !== 62) throw Error('Final row mismatch');
const errors = grid.values.flat().filter(v => typeof v === 'string' && /^#(REF!|VALUE!|DIV\/0!|N\/A|NAME\?|NUM!|SPILL!)/.test(v));
if (errors.length || grid.formulas.flat().some(Boolean)) throw Error('Unexpected formula/error');
console.log((await wb.inspect({ kind: 'region', sheetId: sheet.id, range: 'A1:F4', maxChars: 2100, tableMaxRows: 4, tableMaxCols: 6 })).ndjson);
await (await SpreadsheetFile.exportXlsx(wb)).save(path.join(dir, '먹점프_스킨_뒤끝차트_v1.xlsx'));
if (process.argv.includes('--render')) for (const [label, range] of [['names','A1:F6'], ['goals','J1:N6'], ['skills','Q2:X4']]) {
  const preview = await wb.render({ sheetName: 'Skins', range, scale: 1.4, format: 'png' });
  await fs.writeFile(path.join(dir, `preview-${label}.png`), new Uint8Array(await preview.arrayBuffer()));
}
console.log(JSON.stringify({ rows: rows.length, columns: headers.length, formulas: 0, errors: 0, output: path.join(dir, '먹점프_스킨_뒤끝차트_v1.xlsx') }));
