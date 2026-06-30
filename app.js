/* World Cup 2026 bracket — renders from data.json and re-polls it on an interval,
   so updating that file (or pointing DATA_URL at a real live-scores feed) is all
   it takes to bring the page up to date without touching the markup. */

const DATA_URL = "data.json";
const POLL_MS = 20000;

let lastData = null;
let lastFetchOk = null;

function esc(s) {
  return String(s).replace(/[&<>"']/g, (c) => ({
    "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;",
  }[c]));
}

function groupRowTemplate(row) {
  const favCls = row.fav ? ` fav-${row.fav}` : "";
  const star = row.fav ? '<span class="star">⭐</span>' : "";
  return `<div class="row ${row.cls}${favCls}">
    <span class="ps">${row.pos}</span>
    <span class="fl">${row.flag}</span>
    <span class="nm">${esc(row.name)}${star}</span>
    <span class="wdl">${row.record}</span>
    <span class="pt">${row.pts}</span>
  </div>`;
}

function groupTemplate(group) {
  return `<div class="grp">
    <div class="grp__top"><span class="grp__name">${esc(group.name)}</span><span class="grp__sub">Full time</span></div>
    ${group.rows.map(groupRowTemplate).join("")}
  </div>`;
}

function renderGroups(groups) {
  document.getElementById("groups").innerHTML = groups.map(groupTemplate).join("");
}

function ktTemplate(kt) {
  if (kt.tbd) {
    const labelCls = kt.feeder === "TBD" ? "cd" : "feeder";
    return `<div class="kt tbd"><span class="fl">▫️</span><span class="${labelCls}">${esc(kt.feeder)}</span><span class="sc"></span></div>`;
  }
  const roleCls = kt.role ? ` ${kt.role}` : "";
  const score = kt.score === null || kt.score === undefined ? "" : kt.score;
  return `<div class="kt${roleCls}"><span class="fl">${kt.flag}</span><span class="cd">${esc(kt.name)}</span><span class="sc">${score}</span></div>`;
}

function matchTemplate(m, tbdRound) {
  let cardCls = "card";
  if (m.status === "done") cardCls += " done";
  if (m.status === "live") cardCls += " live";
  if (tbdRound) cardCls += " tbdcard";
  if (m.fav) cardCls += ` fav-${m.fav}`;
  if (m.route) cardCls += ` route-${m.route}`;
  const tag = m.tagpin
    ? `<span class="tagpin" style="color:var(--${m.tagpin.colorVar})">${esc(m.tagpin.text)}</span>`
    : "";
  const pens = m.pens ? `<div class="pens">${esc(m.pens)}</div>` : "";
  return `<div class="match" data-match-id="${m.id}"><div class="${cardCls}">
    <div class="when">${esc(m.when)}${tag}</div>
    ${m.teams.map(ktTemplate).join("")}
    ${pens}
  </div></div>`;
}

function roundTemplate(roundId, label, sublabel, pairs, tbdRound) {
  const pairsHtml = pairs.map(
    (pair) => `<div class="pair">${pair.map((m) => matchTemplate(m, tbdRound)).join("")}</div>`
  ).join("");
  return `<div class="round ${roundId} feed">
    <div class="rhead"><b>${label}</b><span>${sublabel}</span></div>
    ${pairsHtml}
  </div>`;
}

function finalTemplate(final) {
  return `<div class="round final">
    <div class="rhead"><b>Final</b><span>Jul 19</span></div>
    <div class="champ"><div class="box">
      <div class="cup">🏆</div>
      <div class="lab">Champion</div>
      <div class="sub">${esc(final.when)}</div>
    </div></div>
  </div>`;
}

function renderBracket(knockout) {
  const html = [
    roundTemplate("r32", "Round of 32", "Jun 28 – Jul 3", knockout.r32, false),
    roundTemplate("r16", "Round of 16", "Jul 4 – 6", knockout.r16, true),
    roundTemplate("qf", "Quarter-finals", "Jul 9 – 11", knockout.qf, true),
    roundTemplate("sf", "Semi-finals", "Jul 14 – 15", knockout.sf, true),
    finalTemplate(knockout.final),
  ].join("");
  document.getElementById("bracket").innerHTML = html;
}

function renderMeta(meta) {
  document.getElementById("stat-groups").textContent = meta.groupsCount;
  document.getElementById("stat-knockout").textContent = meta.knockoutTeams;
  document.getElementById("stat-last32").textContent = meta.last32Decided;
  document.getElementById("stat-final").textContent = meta.finalDateShort;
  document.getElementById("foot-follow").innerHTML = `<b>Next for ${esc(meta.follow.team)}:</b> ${esc(meta.follow.next)}`;
  document.getElementById("foot-home").innerHTML = `<b>${esc(meta.home.team)}:</b> ${esc(meta.home.next)}`;
  document.getElementById("foot-results").textContent = `Results through ${meta.resultsThrough} · ${meta.tiesRemaining} last-32 ties still to play.`;
}

function allMatches(data) {
  const out = [];
  for (const key of ["r32", "r16", "qf", "sf"]) {
    for (const pair of data.knockout[key]) for (const m of pair) out.push(m);
  }
  return out;
}

function flashChangedMatches(oldData, newData) {
  if (!oldData) return;
  const oldMap = new Map(allMatches(oldData).map((m) => [m.id, JSON.stringify(m)]));
  for (const m of allMatches(newData)) {
    if (oldMap.has(m.id) && oldMap.get(m.id) !== JSON.stringify(m)) {
      const el = document.querySelector(`[data-match-id="${m.id}"] .card`);
      if (el) {
        el.classList.add("flash");
        setTimeout(() => el.classList.remove("flash"), 1500);
      }
    }
  }
}

function setPollStatus(ok) {
  const el = document.getElementById("poll-status");
  const ts = document.getElementById("last-updated");
  const now = new Date();
  ts.textContent = `Checked ${now.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit" })}`;
  el.textContent = ok ? `Auto-updating every ${POLL_MS / 1000}s` : "Offline — showing last known data";
  el.classList.toggle("stale", !ok);
}

function render(data) {
  renderMeta(data.meta);
  renderGroups(data.groups);
  renderBracket(data.knockout);
}

async function poll() {
  try {
    const res = await fetch(`${DATA_URL}?_=${Date.now()}`, { cache: "no-store" });
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const data = await res.json();
    if (lastData && JSON.stringify(data) !== JSON.stringify(lastData)) {
      render(data);
      flashChangedMatches(lastData, data);
    } else if (!lastData) {
      render(data);
    }
    lastData = data;
    lastFetchOk = true;
  } catch (e) {
    lastFetchOk = false;
  }
  setPollStatus(lastFetchOk);
}

function init() {
  const seed = document.getElementById("wc-data");
  if (seed) {
    try {
      lastData = JSON.parse(seed.textContent);
      render(lastData);
    } catch (e) {
      /* fall through to fetch */
    }
  }
  document.getElementById("refresh-btn").addEventListener("click", poll);
  poll();
  setInterval(poll, POLL_MS);
}

document.addEventListener("DOMContentLoaded", init);
