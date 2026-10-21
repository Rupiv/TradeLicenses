// Builds GBA ward master + legacy->GBA mapping draft from:
//   ward_369_master_utf8.csv (new 369 wards), legacy_wards.csv, legacy_ac.csv
// Usage: node build_mapping.mjs <dir>
import fs from 'node:fs';
import path from 'node:path';

const dir = process.argv[2];

function parseCsv(text) {
  text = text.replace(/^﻿/, '');
  const rows = []; let row = []; let cur = ''; let q = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (q) {
      if (c === '"' && text[i + 1] === '"') { cur += '"'; i++; }
      else if (c === '"') q = false;
      else cur += c;
    } else if (c === '"') q = true;
    else if (c === ',') { row.push(cur); cur = ''; }
    else if (c === '\n' || c === '\r') {
      if (c === '\r' && text[i + 1] === '\n') i++;
      row.push(cur); cur = '';
      if (row.some(v => v !== '')) rows.push(row);
      row = [];
    } else cur += c;
  }
  if (cur !== '' || row.length) { row.push(cur); rows.push(row); }
  const [h, ...data] = rows;
  return data.map(r => Object.fromEntries(h.map((k, i) => [k.trim(), (r[i] ?? '').trim()])));
}
const read = f => parseCsv(fs.readFileSync(path.join(dir, f), 'utf8'));

const gba = read('ward_369_master_utf8.csv');
const legacyAll = read('legacy_wards.csv');
const acs = read('legacy_ac.csv');

// Legacy wards 199+ are test rows created from the UI (0 licences) - not real wards
const legacy = legacyAll.filter(w => Number(w.wardID) <= 198);

const norm = s => s.toLowerCase()
  .replace(/\bward\b/g, '')
  .replace(/nagara\b/g, 'nagar')
  .replace(/[^a-z]/g, '');
function dice(a, b) {
  if (!a || !b) return 0;
  if (a === b) return 1;
  const bi = s => { const m = new Map(); for (let i = 0; i < s.length - 1; i++) { const k = s.slice(i, i + 2); m.set(k, (m.get(k) || 0) + 1); } return m; };
  const A = bi(a), B = bi(b); let inter = 0;
  for (const [k, v] of A) inter += Math.min(v, B.get(k) || 0);
  return (2 * inter) / (a.length - 1 + b.length - 1);
}

const corpIds = { East: 1, South: 2, West: 3, North: 4, Central: 5 }; // same IDs as Payment_Gateway_Config
const acByCode = new Map(acs.map(a => [a.constituencyCode, a]));

// ---- 1. GBA ward -> primary legacy ward ----------------------------------
const gbaOut = gba.map(g => {
  const n = norm(g.ward_name);
  const sameAc = legacy.filter(l => l.constituencyCode === g.ac_no);
  const score = l => dice(n, norm(l.wardName));
  let best = null, bestScore = -1, method;
  for (const l of sameAc) { const s = score(l); if (s > bestScore) { best = l; bestScore = s; } }

  if (bestScore >= 0.8) method = bestScore === 1 ? 'NAME_EXACT' : 'NAME_FUZZY';
  else {
    // Ward may have moved constituency: accept a near-exact name match anywhere
    let gBest = null, gScore = -1;
    for (const l of legacy) { const s = score(l); if (s > gScore) { gBest = l; gScore = s; } }
    if (gScore >= 0.9) { best = gBest; bestScore = gScore; method = 'NAME_OTHER_AC'; }
    else if (sameAc.length) {
      // No name match: fall back to the busiest legacy ward of the same AC (same MOH area) - needs review
      best = [...sameAc].sort((a, b) => Number(b.licences) - Number(a.licences))[0];
      method = 'AC_DEFAULT';
    } else { best = null; method = 'NO_MATCH'; }
  }
  return {
    corporationId: corpIds[g.Corporation],
    corporation: g.Corporation, corporation_kn: g.corporation_kn,
    zone_name: g.zone_name,
    wardNo: g.ward_id, ward_name: g.ward_name, ward_name_kn: g.ward_name_kn,
    ac_no: g.ac_no, ac: g.ac, ac_kn: g.ac_kn,
    constituencyID: acByCode.get(g.ac_no)?.constituencyID ?? '',
    legacyWardID: best?.wardID ?? '', legacyWardName: best?.wardName ?? '',
    mohID: best?.topMohID ?? '',
    matchMethod: method, matchScore: bestScore < 0 ? '' : bestScore.toFixed(2),
    needsReview: ['AC_DEFAULT', 'NO_MATCH', 'NAME_OTHER_AC'].includes(method) || !best?.topMohID ? 'Y' : 'N',
  };
});

// ---- 2. legacy ward -> candidate GBA wards (for existing licences at renewal) ----
const mapOut = [];
for (const l of legacy) {
  const ln = norm(l.wardName);
  const cands = new Map();
  for (const g of gbaOut) {
    const s = dice(ln, norm(g.ward_name));
    if (g.legacyWardID === l.wardID) cands.set(g, Math.max(s, cands.get(g) ?? 0));
    else if (s >= 0.8 && g.ac_no === l.constituencyCode) cands.set(g, s);
  }
  let method = 'PRIMARY_OF_GBA';
  if (!cands.size) {
    method = 'AC_ONLY';
    for (const g of gbaOut) if (g.ac_no === l.constituencyCode) cands.set(g, 0);
  }
  const sorted = [...cands.entries()].sort((a, b) => b[1] - a[1]);
  sorted.forEach(([g, s], i) => mapOut.push({
    legacyWardID: l.wardID, legacyWardName: l.wardName, legacyLicences: l.licences,
    corporationId: g.corporationId, wardNo: g.wardNo, gbaWardName: g.ward_name,
    isPrimary: i === 0 ? 1 : 0, matchMethod: method, matchScore: s.toFixed(2),
  }));
}

const toCsv = rows => {
  const h = Object.keys(rows[0]);
  const esc = v => { v = String(v ?? ''); return /[",\n]/.test(v) ? `"${v.replace(/"/g, '""')}"` : v; };
  return '﻿' + [h.join(','), ...rows.map(r => h.map(k => esc(r[k])).join(','))].join('\r\n') + '\r\n';
};
fs.writeFileSync(path.join(dir, 'gba_ward_master.csv'), toCsv(gbaOut), 'utf8');
fs.writeFileSync(path.join(dir, 'legacy_to_gba_ward_mapping.csv'), toCsv(mapOut), 'utf8');

// ---- summary ----
const count = (arr, k) => arr.reduce((m, r) => (m[r[k]] = (m[r[k]] || 0) + 1, m), {});
console.log('GBA wards:', gbaOut.length, count(gbaOut, 'matchMethod'), 'needsReview:', gbaOut.filter(g => g.needsReview === 'Y').length);
console.log('GBA per corporation:', count(gbaOut, 'corporation'));
const zones = [...new Set(gbaOut.map(g => g.corporation + ' / ' + g.zone_name))];
console.log('GBA zones:', zones.length, zones);
const legacyMethods = {}; for (const l of legacy) { const r = mapOut.find(m => m.legacyWardID === l.wardID && m.isPrimary); legacyMethods[r?.matchMethod ?? 'NONE'] = (legacyMethods[r?.matchMethod ?? 'NONE'] || 0) + 1; }
console.log('Legacy wards mapped:', legacy.length, legacyMethods, 'mapping rows:', mapOut.length);
const corpSplit = legacy.filter(l => new Set(mapOut.filter(m => m.legacyWardID === l.wardID).map(m => m.corporationId)).size > 1);
console.log('Legacy wards whose candidates span >1 corporation:', corpSplit.length, corpSplit.map(l => l.wardName).join('; '));
const maxLen = Math.max(...gbaOut.map(g => g.ward_name.length));
console.log('max ward_name length:', maxLen);
