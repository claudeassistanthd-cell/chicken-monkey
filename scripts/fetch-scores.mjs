#!/usr/bin/env node
/**
 * Fetches 2026 World Cup standings and match results from football-data.org
 * and maps them into data.json, then regenerates the inline seed in index.html.
 *
 * Usage: SCORES_API_KEY=<key> node scripts/fetch-scores.mjs
 * Free tier: https://www.football-data.org/coverage (10 req/min, covers WC)
 *
 * Exits 1 without writing any files if the API call fails.
 */

import { readFileSync, writeFileSync } from "fs";
import { fileURLToPath } from "url";
import { dirname, join } from "path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = join(__dirname, "..");

const API_KEY = process.env.SCORES_API_KEY;
if (!API_KEY) {
  console.error("SCORES_API_KEY not set — aborting without changes");
  process.exit(1);
}

const BASE = "https://api.football-data.org/v4";
const COMP = "WC"; // FIFA World Cup competition code (same code used for 2022)

/* ───────── team metadata ───────── */

const FLAGS = {
  "Mexico": "🇲🇽", "South Africa": "🇿🇦", "South Korea": "🇰🇷", "Czechia": "🇨🇿",
  "Switzerland": "🇨🇭", "Canada": "🇨🇦", "Bosnia": "🇧🇦", "Qatar": "🇶🇦",
  "Brazil": "🇧🇷", "Morocco": "🇲🇦", "Scotland": "🏴󠁧󠁢󠁳󠁣󠁴󠁿", "Haiti": "🇭🇹",
  "USA": "🇺🇸", "Australia": "🇦🇺", "Paraguay": "🇵🇾", "Türkiye": "🇹🇷",
  "Germany": "🇩🇪", "Ivory Coast": "🇨🇮", "Ecuador": "🇪🇨", "Curaçao": "🇨🇼",
  "Netherlands": "🇳🇱", "Japan": "🇯🇵", "Sweden": "🇸🇪", "Tunisia": "🇹🇳",
  "Belgium": "🇧🇪", "Egypt": "🇪🇬", "Iran": "🇮🇷", "New Zealand": "🇳🇿",
  "Spain": "🇪🇸", "Cape Verde": "🇨🇻", "Uruguay": "🇺🇾", "Saudi Arabia": "🇸🇦",
  "France": "🇫🇷", "Norway": "🇳🇴", "Senegal": "🇸🇳", "Iraq": "🇮🇶",
  "Argentina": "🇦🇷", "Austria": "🇦🇹", "Algeria": "🇩🇿", "Jordan": "🇯🇴",
  "Colombia": "🇨🇴", "Portugal": "🇵🇹", "Congo DR": "🇨🇩", "Uzbekistan": "🇺🇿",
  "England": "🏴󠁧󠁢󠁥󠁮󠁧󠁿", "Croatia": "🇭🇷", "Ghana": "🇬🇭", "Panama": "🇵🇦",
};

// API team name → our canonical name
const NAME_MAP = {
  "United States": "USA",
  "Côte d'Ivoire": "Ivory Coast",
  "Cote d'Ivoire": "Ivory Coast",
  "Turkey": "Türkiye",
  "Bosnia and Herzegovina": "Bosnia",
  "Bosnia & Herzegovina": "Bosnia",
  "Czech Republic": "Czechia",
  "DR Congo": "Congo DR",
  "Congo, DR": "Congo DR",
  "Democratic Republic of Congo": "Congo DR",
};

function normalize(apiName) {
  return NAME_MAP[apiName] || apiName;
}

function flag(name) {
  return FLAGS[name] || "🏳️";
}

// Stable key for fuzzy team matching
function key(name) {
  return normalize(name).toLowerCase().replace(/[\s\-'.]/g, "");
}

/* ───────── API helpers ───────── */

async function apiFetch(path) {
  const res = await fetch(`${BASE}${path}`, {
    headers: { "X-Auth-Token": API_KEY },
  });
  if (!res.ok) {
    const body = await res.text().catch(() => "");
    throw new Error(`API ${path} → HTTP ${res.status}: ${body.slice(0, 240)}`);
  }
  return res.json();
}

/* ───────── date / status helpers ───────── */

const MADRID_TZ = "Europe/Madrid";

function formatWhen(utcDate, apiStatus) {
  const d = new Date(utcDate);
  const day = d.toLocaleDateString("en-GB", {
    weekday: "short", day: "numeric", month: "short", timeZone: MADRID_TZ,
  });
  if (["FINISHED", "AWARDED"].includes(apiStatus)) return `${day} · FT`;
  const time = d.toLocaleTimeString("en-GB", {
    hour: "2-digit", minute: "2-digit", timeZone: MADRID_TZ,
  });
  return `${day} · ${time}`;
}

function apiStatusToSchema(apiStatus) {
  if (["FINISHED", "AWARDED"].includes(apiStatus)) return "done";
  if (["IN_PLAY", "PAUSED", "LIVE", "HALFTIME"].includes(apiStatus)) return "live";
  return "scheduled";
}

/* ───────── knockout match patching ───────── */

function findApiMatch(apiMatches, nameA, nameB) {
  const ka = key(nameA), kb = key(nameB);
  return apiMatches.find(m => {
    const h = key(normalize(m.homeTeam?.name || ""));
    const a = key(normalize(m.awayTeam?.name || ""));
    return (h === ka && a === kb) || (h === kb && a === ka);
  }) ?? null;
}

function patchKnockoutMatch(schemaMatch, apiMatches) {
  const teams = schemaMatch.teams;
  const known = teams.filter(t => !t.tbd && t.name);
  if (known.length < 2) return; // can't identify TBD slots yet

  const apiM = findApiMatch(apiMatches, known[0].name, known[1].name);
  if (!apiM) return;

  schemaMatch.status = apiStatusToSchema(apiM.status);
  schemaMatch.when = formatWhen(apiM.utcDate, apiM.status);
  schemaMatch.kickoff = apiM.utcDate; // ISO 8601 — used by app.js for local-tz display

  if (schemaMatch.status === "scheduled") return;

  const hNorm = normalize(apiM.homeTeam.name);
  const aNorm = normalize(apiM.awayTeam.name);
  const hScore = apiM.score?.fullTime?.home;
  const aScore = apiM.score?.fullTime?.away;

  for (const t of teams) {
    if (!t.name) continue;
    const isHome = key(t.name) === key(hNorm);
    if (hScore !== null && hScore !== undefined) {
      t.score = isHome ? hScore : aScore;
    }
    if (schemaMatch.status === "done" && apiM.score?.winner) {
      const won =
        (isHome && apiM.score.winner === "HOME_TEAM") ||
        (!isHome && apiM.score.winner === "AWAY_TEAM");
      t.role = won ? "w" : "l";
    }
  }

  if (
    schemaMatch.status === "done" &&
    apiM.score?.duration === "PENALTY_SHOOTOUT" &&
    apiM.score?.penalties
  ) {
    const hp = apiM.score.penalties.home;
    const ap = apiM.score.penalties.away;
    const winnerNorm = apiM.score.winner === "HOME_TEAM" ? hNorm : aNorm;
    schemaMatch.pens = `✓ ${winnerNorm} win ${Math.max(hp, ap)}–${Math.min(hp, ap)} on pens`;
  }
}

/* ───────── meta derivation ───────── */

function deriveMeta(schema, apiMatches) {
  const meta = schema.meta;

  const r32 = schema.knockout.r32.flat();
  const r32Done = r32.filter(m => m.status === "done").length;
  meta.last32Decided = `${r32Done} / 16`;
  meta.tiesRemaining = 16 - r32Done;

  // Date of most-recently-finished match
  const finished = apiMatches
    .filter(m => ["FINISHED", "AWARDED"].includes(m.status))
    .sort((a, b) => new Date(b.utcDate) - new Date(a.utcDate));
  if (finished.length > 0) {
    const d = new Date(finished[0].utcDate);
    meta.resultsThrough = d.toLocaleDateString("en-GB", {
      day: "numeric", month: "short", timeZone: MADRID_TZ,
    });
  }

  // Next scheduled match for Spain and Australia
  const now = Date.now();
  function nextFor(teamName) {
    return apiMatches
      .filter(m => {
        const names = [
          normalize(m.homeTeam?.name || ""),
          normalize(m.awayTeam?.name || ""),
        ];
        return (
          names.includes(teamName) &&
          ["SCHEDULED", "TIMED"].includes(m.status) &&
          new Date(m.utcDate) > now
        );
      })
      .sort((a, b) => new Date(a.utcDate) - new Date(b.utcDate))[0] ?? null;
  }

  function fmtNext(apiM, forTeam) {
    if (!apiM) return "No upcoming matches scheduled.";
    const hNorm = normalize(apiM.homeTeam.name);
    const aNorm = normalize(apiM.awayTeam.name);
    const opp = normalize(forTeam) === hNorm ? aNorm : hNorm;
    const d = new Date(apiM.utcDate);
    const dayStr = d.toLocaleDateString("en-GB", {
      weekday: "short", day: "numeric", month: "short", timeZone: MADRID_TZ,
    });
    const timeStr = d.toLocaleTimeString("en-GB", {
      hour: "2-digit", minute: "2-digit", timeZone: MADRID_TZ,
    });
    return `vs ${opp} — ${dayStr}, ${timeStr} (Madrid).`;
  }

  const spainNext = nextFor("Spain");
  if (spainNext) meta.follow.next = fmtNext(spainNext, "Spain");

  const ausNext = nextFor("Australia");
  if (ausNext) meta.home.next = fmtNext(ausNext, "Australia");
}

/* ───────── main ───────── */

async function main() {
  const dataPath = join(ROOT, "data.json");
  const indexPath = join(ROOT, "index.html");

  const originalJson = readFileSync(dataPath, "utf8");
  const schema = JSON.parse(originalJson);

  let standingsResp, matchesResp;
  try {
    [standingsResp, matchesResp] = await Promise.all([
      apiFetch(`/competitions/${COMP}/standings`),
      apiFetch(`/competitions/${COMP}/matches`),
    ]);
  } catch (err) {
    console.error("API fetch failed — keeping existing data:", err.message);
    process.exit(1);
  }

  const apiMatches = matchesResp.matches ?? [];

  /* ── group standings ── */
  const standingsByGroup = {};
  for (const s of standingsResp.standings ?? []) {
    if (s.type !== "TOTAL") continue;
    const letter = (s.group ?? "").replace("GROUP_", "");
    standingsByGroup[letter] = s.table;
  }

  for (const group of schema.groups) {
    const letter = group.name.replace("Group ", "");
    const table = standingsByGroup[letter];
    if (!table?.length) continue;

    // Preserve fav + cls from current data (determined by complex FIFA rules, not recomputed)
    const metaByKey = {};
    for (const row of group.rows) metaByKey[key(row.name)] = { cls: row.cls, fav: row.fav };

    group.rows = table.map(entry => {
      const name = normalize(entry.team.name);
      const prev = metaByKey[key(name)] ?? {};
      const row = {
        pos: entry.position,
        flag: flag(name),
        name,
        record: `${entry.won}-${entry.draw}-${entry.lost}`,
        pts: entry.points,
        cls: prev.cls ?? "o",
      };
      if (prev.fav) row.fav = prev.fav;
      return row;
    });
  }

  /* ── knockout matches ── */
  const matchesByStage = {};
  for (const m of apiMatches) {
    (matchesByStage[m.stage] ??= []).push(m);
  }

  const stageKeys = {
    r32: "LAST_32",
    r16: "LAST_16",
    qf: "QUARTER_FINALS",
    sf: "SEMI_FINALS",
  };

  for (const [schemaKey, apiStage] of Object.entries(stageKeys)) {
    const apiMs = matchesByStage[apiStage] ?? [];
    for (const pair of schema.knockout[schemaKey] ?? []) {
      for (const m of pair) patchKnockoutMatch(m, apiMs);
    }
  }

  /* ── meta ── */
  deriveMeta(schema, apiMatches);
  schema.generatedAt = new Date().toISOString();

  /* ── write only if changed ── */
  const newJson = JSON.stringify(schema, null, 2);
  if (newJson === originalJson.trimEnd()) {
    console.log("No changes — skipping commit.");
    process.exit(0);
  }

  writeFileSync(dataPath, newJson + "\n");
  console.log("data.json updated.");

  // Keep inline seed in sync so the page renders instantly on first load
  const indexHtml = readFileSync(indexPath, "utf8");
  const SEED_OPEN = '<script type="application/json" id="wc-data">';
  const SEED_CLOSE = "</script>";
  const si = indexHtml.indexOf(SEED_OPEN);
  const ei = indexHtml.indexOf(SEED_CLOSE, si + SEED_OPEN.length);
  if (si !== -1 && ei !== -1) {
    const newIndex =
      indexHtml.slice(0, si + SEED_OPEN.length) +
      newJson +
      indexHtml.slice(ei);
    writeFileSync(indexPath, newIndex);
    console.log("index.html seed updated.");
  }
}

main();
