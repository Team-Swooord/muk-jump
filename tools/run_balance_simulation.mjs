#!/usr/bin/env node

/**
 * 먹점프 코드 수치 기반 밸런스 몬테카를로.
 *
 * 이 도구는 Unity 물리를 흉내 내는 자동 플레이 봇이 아니다.
 * 1) 실제 GameplayRandom과 같은 xorshift32로 콘텐츠 배치/먹떼 경제를 반복하고,
 * 2) 10m 구간을 통과할 조건부 확률을 입력한 민감도 모델로 목표 퍼널을 비교한다.
 *
 * 2번은 게임 플레이를 관찰해 학습한 모델이 아니며, 실제 도달 판수를 예측하지 않는다.
 *
 * 실행:
 *   node tools/run_balance_simulation.mjs
 *   node tools/run_balance_simulation.mjs --content-runs=10000 --skill-runs=10000 --scenarios=5000
 */

const COURSE_HEIGHT = 1000;
const ENDURANCE_HEIGHT = 3000;
const DEFAULT_CONTENT_RUNS = 100_000;
const DEFAULT_SKILL_RUNS = 100_000;
const DEFAULT_SCENARIOS = 50_000;
const MAX_ATTEMPTS = 50;
const MAX_LIVING_PLAYERS = 24;
const CLONES_PER_PICKUP = 1;
const STREAM_COUNT = 6;
const UINT_RANGE = 0x1_0000_0000;
const FLOAT_UNIT = 1 / 16_777_216;

const stream = Object.freeze({
  items: 0,
  obstacles: 1,
  fallingRocks: 2,
  weather: 3,
  platforms: 4,
  player: 5,
});

const pickupProfiles = Object.freeze([
  { name: "55%", chance: 0.55 },
  { name: "70%", chance: 0.70 },
  { name: "84%", chance: 0.84 },
  { name: "93%", chance: 0.93 },
  { name: "100%", chance: 1.0 },
]);

const skillProfiles = Object.freeze([
  { name: "입문", early: 0.88, post: 0.932 },
  { name: "보통", early: 0.94, post: 0.962 },
  { name: "숙련", early: 0.975, post: 0.978 },
  { name: "전문", early: 0.99, post: 0.986 },
]);

function parsePositiveInt(name, fallback) {
  const prefix = `--${name}=`;
  const entry = process.argv.find((value) => value.startsWith(prefix));
  if (!entry) return fallback;
  const parsed = Number.parseInt(entry.slice(prefix.length), 10);
  if (!Number.isFinite(parsed) || parsed <= 0) {
    throw new Error(`${name}은 1 이상의 정수여야 합니다.`);
  }
  return parsed;
}

function mix(value) {
  value >>>= 0;
  value ^= value >>> 16;
  value = Math.imul(value, 0x7feb352d) >>> 0;
  value ^= value >>> 15;
  value = Math.imul(value, 0x846ca68b) >>> 0;
  value ^= value >>> 16;
  return value >>> 0;
}

class GameplayRng {
  constructor(seed) {
    this.states = new Uint32Array(STREAM_COUNT);
    const root = seed >>> 0;
    for (let index = 0; index < STREAM_COUNT; index += 1) {
      const domainSeed =
        (root + Math.imul(0x9e3779b9, index + 1)) >>> 0;
      const mixed = mix(domainSeed);
      this.states[index] =
        mixed !== 0 ? mixed : (0x6d2b79f5 ^ index) >>> 0;
    }
  }

  nextUInt(domain) {
    let state = this.states[domain] >>> 0;
    state ^= state << 13;
    state ^= state >>> 17;
    state ^= state << 5;
    state >>>= 0;
    this.states[domain] = state;
    return state;
  }

  value(domain) {
    return (this.nextUInt(domain) >>> 8) * FLOAT_UNIT;
  }

  float(domain, minimum, maximum) {
    if (maximum <= minimum) return minimum;
    return minimum + (maximum - minimum) * this.value(domain);
  }

  int(domain, minimum, maximum) {
    if (maximum <= minimum) return minimum;
    const range = maximum - minimum;
    const threshold = (UINT_RANGE - range) % range;
    let sample;
    do {
      sample = this.nextUInt(domain);
    } while (sample < threshold);
    return minimum + (sample % range);
  }
}

class BehaviorRng {
  constructor(seed, domain) {
    const initial = mix((seed ^ Math.imul(domain, 0x9e3779b9)) >>> 0);
    this.state = initial || 0x6d2b79f5;
  }

  value() {
    let state = this.state >>> 0;
    state ^= state << 13;
    state ^= state >>> 17;
    state ^= state << 5;
    this.state = state >>> 0;
    return (this.state >>> 8) * FLOAT_UNIT;
  }
}

function lerp(a, b, t) {
  return a + (b - a) * Math.max(0, Math.min(1, t));
}

function simulateItems(seed, pickupChance, heightLimit = COURSE_HEIGHT) {
  const gameplay = new GameplayRng(seed);
  const pickup = new BehaviorRng(seed, 17);
  let nextHeight = 12;
  let introClonePending = true;
  let livingPlayers = 1;
  let itemCount = 0;
  let clonePickupCount = 0;
  let capHeight = Number.POSITIVE_INFINITY;
  const typeCounts = {
    clone: 0,
    inkDrop: 0,
    goldenBrush: 0,
    shield: 0,
    reserve: 0,
  };
  const milestones = new Map();
  const milestoneHeights = [30, 60, 100, 250, 500, 750, 1000];

  while (nextHeight <= heightLimit) {
    for (const milestone of milestoneHeights) {
      if (!milestones.has(milestone) && milestone < nextHeight) {
        milestones.set(milestone, {
          items: itemCount,
          clones: clonePickupCount,
          livingPlayers,
          capped: livingPlayers === MAX_LIVING_PLAYERS ? 1 : 0,
        });
      }
    }

    const canCreateClone = livingPlayers < MAX_LIVING_PLAYERS;
    let type;
    if (introClonePending && canCreateClone) {
      type = "clone";
      introClonePending = false;
    } else {
      const cloneChance = lerp(
        0.35,
        0.50,
        (nextHeight - 30) / (250 - 30),
      );
      if (
        canCreateClone &&
        gameplay.value(stream.items) < cloneChance
      ) {
        type = "clone";
      } else {
        type = ["inkDrop", "goldenBrush", "shield", "reserve"][
          gameplay.int(stream.items, 0, 4)
        ];
      }
    }

    typeCounts[type] += 1;
    itemCount += 1;

    // 실제 Spawn에서 아이템 유형 결정 뒤 X, 회전값을 소비한다.
    gameplay.float(stream.items, -4, 4);
    gameplay.float(stream.items, 0, Math.PI * 2);

    if (pickup.value() < pickupChance && type === "clone") {
      clonePickupCount += 1;
      livingPlayers = Math.min(
        MAX_LIVING_PLAYERS,
        livingPlayers + CLONES_PER_PICKUP,
      );
      if (
        livingPlayers === MAX_LIVING_PLAYERS &&
        !Number.isFinite(capHeight)
      ) {
        capHeight = nextHeight;
      }
    }

    nextHeight += gameplay.float(stream.items, 10, 16);
  }

  for (const milestone of milestoneHeights) {
    if (!milestones.has(milestone)) {
      milestones.set(milestone, {
        items: itemCount,
        clones: clonePickupCount,
        livingPlayers,
        capped: livingPlayers === MAX_LIVING_PLAYERS ? 1 : 0,
      });
    }
  }

  return {
    itemCount,
    clonePickupCount,
    livingPlayers,
    capHeight,
    typeCounts,
    milestones,
  };
}

function simulateContent(seed, heightLimit = COURSE_HEIGHT) {
  const gameplay = new GameplayRng(seed);
  let movingObstacles = 0;
  let dragons = 0;
  let haetaes = 0;
  let nextObstacle = 30;
  let firstDragonPending = true;
  let firstHaetaePending = true;
  let activeLargeAnimalUntil = Number.NEGATIVE_INFINITY;

  // 카메라 상하 despawn 폭을 고도 길이로 환산한 근사치다.
  const dragonActiveHeightSpan = 45.2;
  // 해태는 카메라 진입 대기 + 1회 경고/돌진 뒤 반납된다. 시간 기반 실제 길이는
  // 플레이 속도에 따라 달라지므로 이 값은 대형 동물 상호배타를 위한 보수적 근사다.
  const haetaeActiveHeightSpan = 24;
  while (nextObstacle <= heightLimit) {
    movingObstacles += 1;
    const hasActiveLargeAnimal = nextObstacle < activeLargeAnimalUntil;
    let isDragon = false;
    let isHaetae = false;

    if (nextObstacle >= 320 && firstHaetaePending) {
      if (!hasActiveLargeAnimal) {
        firstHaetaePending = false;
        isHaetae = true;
      }
    } else if (nextObstacle < 320) {
      if (nextObstacle >= 60 && !hasActiveLargeAnimal) {
        if (firstDragonPending) {
          firstDragonPending = false;
          isDragon = true;
        } else {
          // 먹해태가 해금되기 전에는 기존 어린 용 28% 체감을 유지한다.
          isDragon = gameplay.value(stream.obstacles) < 0.28;
        }
      }
    } else if (!hasActiveLargeAnimal) {
      if (firstDragonPending) {
        firstDragonPending = false;
        isDragon = true;
      } else {
        const roll = gameplay.value(stream.obstacles);
        isHaetae = roll < 0.12;
        isDragon = !isHaetae && roll < 0.30;
      }
    }

    if (isDragon) {
      dragons += 1;
      activeLargeAnimalUntil = nextObstacle + dragonActiveHeightSpan;
    } else if (isHaetae) {
      haetaes += 1;
      activeLargeAnimalUntil = nextObstacle + haetaeActiveHeightSpan;
    }

    if (isHaetae) {
      // SpawnHaetae의 진입 방향과 세로 오프셋 소비를 재현한다.
      gameplay.value(stream.obstacles);
      gameplay.value(stream.obstacles);
    } else {
      // 일반/용 Spawn의 amplitude, X, speed, phase 소비를 재현한다.
      gameplay.value(stream.obstacles);
      gameplay.value(stream.obstacles);
      gameplay.value(stream.obstacles);
      gameplay.value(stream.obstacles);
    }
    nextObstacle += gameplay.float(stream.obstacles, 8, 12);
  }

  let windPlatforms = 0;
  let nextWind = 25 + gameplay.float(stream.platforms, 82, 128);
  while (nextWind <= heightLimit) {
    windPlatforms += 1;
    gameplay.float(stream.platforms, 2.8, 3.8);
    gameplay.value(stream.platforms);
    nextWind += gameplay.float(stream.platforms, 82, 128);
  }

  let updrafts = 0;
  let nextUpdraft = gameplay.int(stream.weather, 180, 261);
  while (nextUpdraft <= heightLimit) {
    updrafts += 1;
    nextUpdraft += gameplay.int(stream.weather, 220, 341);
  }

  return { movingObstacles, dragons, haetaes, windPlatforms, updrafts };
}

function zoneNetPenalty(height) {
  const band = Math.floor(Math.max(0, height) / 250) % 4;
  // 실제 구간 페널티는 분신·방패·풍맥·상승기류가 일부 상쇄한다고 보고
  // 민감도 모델에는 절반 수준의 순 페널티만 반영한다.
  return [0, 0.001, 0.002, 0.003][band];
}

function simulateAttempt(seed, early, post, heightLimit = ENDURANCE_HEIGHT) {
  // 콘텐츠 시뮬레이션과 의도적으로 분리된 목표 퍼널 민감도 계산이다.
  // 발판·피격·아이템 이벤트를 직접 소비하지 않으며, 그 순효과가 이미 포함됐다고
  // 가정한 10m 조건부 통과율을 입력으로 받는다.
  const rng = new BehaviorRng(seed, 31);
  const runJitter = (rng.value() + rng.value() - 1) * 0.0045;
  let height = 0;
  while (height < heightLimit) {
    const nextHeight = height + 10;
    const base = nextHeight <= 30 ? early : post;
    const introSafety = nextHeight <= 30 ? 0.006 : 0;
    const probability = Math.max(
      0,
      Math.min(
        0.9995,
        base + introSafety + runJitter - zoneNetPenalty(nextHeight),
      ),
    );
    if (rng.value() >= probability) break;
    height = nextHeight;
  }
  return height;
}

function percentile(sorted, probability) {
  if (sorted.length === 0) return 0;
  const index = Math.max(
    0,
    Math.min(sorted.length - 1, Math.ceil(sorted.length * probability) - 1),
  );
  return sorted[index];
}

function fixedSkillSimulation(profile, count) {
  const scores = new Array(count);
  const milestones = [30, 60, 100, 250, 500, 750, 1000];
  const reached = Object.fromEntries(milestones.map((height) => [height, 0]));
  for (let index = 0; index < count; index += 1) {
    const score = simulateAttempt(
      (index + 1 + Math.imul(profile.name.charCodeAt(0), 100_003)) >>> 0,
      profile.early,
      profile.post,
    );
    scores[index] = score;
    for (const milestone of milestones) {
      if (score >= milestone) reached[milestone] += 1;
    }
  }
  scores.sort((a, b) => a - b);
  return {
    scores,
    reached,
    percentiles: {
      p50: percentile(scores, 0.50),
      p90: percentile(scores, 0.90),
      p95: percentile(scores, 0.95),
      p99: percentile(scores, 0.99),
    },
  };
}

function improvementParameters(attempt) {
  // 실측 학습식이 아니라 반복 시도가 좋아지는 경우를 비교하기 위한 가정 곡선이다.
  return {
    early: 0.91 + 0.065 * (1 - Math.exp(-(attempt - 1) / 5)),
    post: 0.945 + 0.034 * (1 - Math.exp(-(attempt - 1) / 7)),
  };
}

function improvementSimulation(scenarioCount) {
  const snapshots = [1, 3, 5, 10, 15, 20, 30, 50];
  const milestones = [250, 500, 750, 1000, 2000, 3000];
  const bestAt = Object.fromEntries(
    snapshots.map((attempt) => [attempt, new Array(scenarioCount)]),
  );
  const firstAt = Object.fromEntries(
    milestones.map((height) => [height, new Array(scenarioCount).fill(0)]),
  );

  for (let scenario = 0; scenario < scenarioCount; scenario += 1) {
    let best = 0;
    for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt += 1) {
      const parameters = improvementParameters(attempt);
      const seed =
        (Math.imul(scenario + 1, 0x45d9f3b) ^
          Math.imul(attempt, 0x27d4eb2d)) >>>
        0;
      const score = simulateAttempt(
        seed,
        parameters.early,
        parameters.post,
      );
      best = Math.max(best, score);
      for (const milestone of milestones) {
        if (firstAt[milestone][scenario] === 0 && score >= milestone) {
          firstAt[milestone][scenario] = attempt;
        }
      }
      if (bestAt[attempt]) bestAt[attempt][scenario] = best;
    }
  }

  const bestSummary = {};
  for (const attempt of snapshots) {
    const values = bestAt[attempt].sort((a, b) => a - b);
    bestSummary[attempt] = {
      p25: percentile(values, 0.25),
      p50: percentile(values, 0.50),
      p75: percentile(values, 0.75),
      p90: percentile(values, 0.90),
    };
  }

  const firstSummary = {};
  for (const milestone of milestones) {
    const all = firstAt[milestone];
    const reachedCount = all.reduce(
      (sum, attempt) => sum + (attempt > 0 ? 1 : 0),
      0,
    );
    const attempts = all
      .map((attempt) => (attempt > 0 ? attempt : MAX_ATTEMPTS + 1))
      .sort((a, b) => a - b);
    firstSummary[milestone] = {
      p25: percentile(attempts, 0.25),
      p50: percentile(attempts, 0.50),
      p75: percentile(attempts, 0.75),
      p90: percentile(attempts, 0.90),
      notReachedRate: 1 - reachedCount / scenarioCount,
    };
  }

  return { bestSummary, firstSummary };
}

function aggregateContent(count) {
  const contentTotals = {
    itemCount: 0,
    movingObstacles: 0,
    dragons: 0,
    haetaes: 0,
    windPlatforms: 0,
    updrafts: 0,
  };
  const pickupResults = {};

  for (const profile of pickupProfiles) {
    pickupResults[profile.name] = {
      clonePickups: 0,
      capHeights: [],
      milestones: new Map(),
      typeCounts: {
        clone: 0,
        inkDrop: 0,
        goldenBrush: 0,
        shield: 0,
        reserve: 0,
      },
    };
  }

  for (let index = 0; index < count; index += 1) {
    const seed = index + 1;
    const content = simulateContent(seed);
    const baselineItems = simulateItems(seed, 0.70);
    contentTotals.itemCount += baselineItems.itemCount;
    for (const key of [
      "movingObstacles",
      "dragons",
      "haetaes",
      "windPlatforms",
      "updrafts",
    ]) {
      contentTotals[key] += content[key];
    }

    for (const profile of pickupProfiles) {
      const result = simulateItems(seed, profile.chance);
      const aggregate = pickupResults[profile.name];
      aggregate.clonePickups += result.clonePickupCount;
      if (Number.isFinite(result.capHeight)) {
        aggregate.capHeights.push(result.capHeight);
      }
      for (const [type, value] of Object.entries(result.typeCounts)) {
        aggregate.typeCounts[type] += value;
      }
      for (const [height, values] of result.milestones) {
        const target = aggregate.milestones.get(height) ?? {
          items: 0,
          clones: 0,
          livingPlayers: 0,
          capped: 0,
        };
        target.items += values.items;
        target.clones += values.clones;
        target.livingPlayers += values.livingPlayers;
        target.capped += values.capped;
        aggregate.milestones.set(height, target);
      }
    }
  }

  for (const aggregate of Object.values(pickupResults)) {
    aggregate.capHeights.sort((a, b) => a - b);
  }

  return { contentTotals, pickupResults };
}

function fixed(value, digits = 2) {
  return Number(value).toFixed(digits);
}

function percent(value, digits = 2) {
  return `${fixed(value * 100, digits)}%`;
}

function printContent(aggregate, count) {
  const totals = aggregate.contentTotals;
  console.log("\n## 1,000m 콘텐츠 배치");
  console.log("| 항목 | 판당 평균 |");
  console.log("|---|---:|");
  console.log(`| 아이템 | ${fixed(totals.itemCount / count)} |`);
  console.log(`| 이동 장애물 | ${fixed(totals.movingObstacles / count)} |`);
  console.log(`| 어린 용(활성 폭 근사) | ${fixed(totals.dragons / count)} |`);
  console.log(`| 먹해태(활성 폭 근사·위험 중첩 제외 전 상한) | ${fixed(totals.haetaes / count)} |`);
  console.log(`| 풍맥 발판 | ${fixed(totals.windPlatforms / count)} |`);
  console.log(
    `| 상승기류(고도 간격만 적용한 이론치) | ` +
      `${fixed(totals.updrafts / count)} |`,
  );

  console.log("\n## 먹분신 포화");
  console.log(
    "| 아이템 획득률 | 분신 획득 | 24마리 도달률 | 도달자 고도 P50 | P90 |",
  );
  console.log("|---:|---:|---:|---:|---:|");
  for (const profile of pickupProfiles) {
    const result = aggregate.pickupResults[profile.name];
    console.log(
      `| ${profile.name} | ${fixed(result.clonePickups / count)} | ` +
        `${percent(result.capHeights.length / count)} | ` +
        `${fixed(percentile(result.capHeights, 0.50), 1)}m | ` +
        `${fixed(percentile(result.capHeights, 0.90), 1)}m |`,
    );
  }

  const standardPickup = aggregate.pickupResults["70%"];
  const totalTypes = Object.values(standardPickup.typeCounts).reduce(
    (sum, value) => sum + value,
    0,
  );
  console.log("\n### 획득률 70%의 생성 아이템 비중");
  console.log("| 먹분신 | 먹물방울 | 황금 붓 | 방어막 | 붓 여유 |");
  console.log("|---:|---:|---:|---:|---:|");
  console.log(
    `| ${percent(standardPickup.typeCounts.clone / totalTypes)} | ` +
      `${percent(standardPickup.typeCounts.inkDrop / totalTypes)} | ` +
      `${percent(standardPickup.typeCounts.goldenBrush / totalTypes)} | ` +
      `${percent(standardPickup.typeCounts.shield / totalTypes)} | ` +
      `${percent(standardPickup.typeCounts.reserve / totalTypes)} |`,
  );

  for (const pickupName of ["55%", "70%", "84%"]) {
    const result = aggregate.pickupResults[pickupName];
    console.log(`\n### 획득률 ${pickupName} 고도별 먹떼`);
    console.log("| 고도 | 누적 아이템 | 분신 획득 | 생존 수(피격 전) | 상한 도달 |");
    console.log("|---:|---:|---:|---:|---:|");
    for (const [height, values] of result.milestones) {
      console.log(
        `| ${height}m | ${fixed(values.items / count)} | ` +
          `${fixed(values.clones / count)} | ` +
          `${fixed(values.livingPlayers / count)} | ` +
          `${percent(values.capped / count)} |`,
      );
    }
  }
}

function printSkills(results, count) {
  console.log("\n## 가정된 고정 구간 통과율의 민감도");
  console.log("| 숙련도 | P50 | P90 | P95 | P99 | 1,000m 도달률 | 중앙 도전 수 |");
  console.log("|---|---:|---:|---:|---:|---:|---:|");
  for (const profile of skillProfiles) {
    const result = results[profile.name];
    const probability = result.reached[1000] / count;
    const medianAttempts =
      probability > 0 ? Math.ceil(Math.log(0.5) / Math.log(1 - probability)) : Infinity;
    console.log(
      `| ${profile.name} | ${result.percentiles.p50}m | ` +
        `${result.percentiles.p90}m | ${result.percentiles.p95}m | ` +
        `${result.percentiles.p99}m | ${percent(probability)} | ` +
        `${Number.isFinite(medianAttempts) ? medianAttempts : "∞"}판 |`,
    );
  }

  console.log("\n### 목표 고도 도달률");
  console.log("| 숙련도 | 30m | 60m | 100m | 250m | 500m | 750m | 1,000m |");
  console.log("|---|---:|---:|---:|---:|---:|---:|---:|");
  for (const profile of skillProfiles) {
    const result = results[profile.name];
    const cells = [30, 60, 100, 250, 500, 750, 1000].map((height) =>
      percent(result.reached[height] / count),
    );
    console.log(`| ${profile.name} | ${cells.join(" | ")} |`);
  }
}

function printImprovement(result) {
  console.log("\n## 가정된 반복 향상 시나리오의 최고 진행고도");
  console.log("| 누적 판수 | 진행고도 P25 | P50 | P75 | P90 |");
  console.log("|---:|---:|---:|---:|---:|");
  for (const [attempt, values] of Object.entries(result.bestSummary)) {
    console.log(
      `| ${attempt}판 | ${values.p25}m | ${values.p50}m | ` +
        `${values.p75}m | ${values.p90}m |`,
    );
  }

  console.log("\n### 전체 시나리오 기준 첫 목표 도달 판수");
  console.log("| 목표 | P25 | P50 | P75 | P90 | 50판 내 미도달 |");
  console.log("|---:|---:|---:|---:|---:|---:|");
  const formatAttempt = (attempt) =>
    attempt > MAX_ATTEMPTS ? `>${MAX_ATTEMPTS}판` : `${attempt}판`;
  for (const [height, values] of Object.entries(result.firstSummary)) {
    console.log(
      `| ${height}m | ${formatAttempt(values.p25)} | ` +
        `${formatAttempt(values.p50)} | ${formatAttempt(values.p75)} | ` +
        `${formatAttempt(values.p90)} | ` +
        `${percent(values.notReachedRate)} |`,
    );
  }
}

function main() {
  const contentRuns = parsePositiveInt("content-runs", DEFAULT_CONTENT_RUNS);
  const skillRuns = parsePositiveInt("skill-runs", DEFAULT_SKILL_RUNS);
  const scenarioCount = parsePositiveInt("scenarios", DEFAULT_SCENARIOS);

  console.log("# 먹점프 콘텐츠 확률·목표 퍼널 민감도");
  console.log(
    `콘텐츠 ${contentRuns.toLocaleString()}시드 · ` +
      `숙련도별 ${skillRuns.toLocaleString()}판 · ` +
      `반복 향상 ${scenarioCount.toLocaleString()}시나리오×${MAX_ATTEMPTS}판`,
  );

  const content = aggregateContent(contentRuns);
  printContent(content, contentRuns);

  const fixedResults = {};
  for (const profile of skillProfiles) {
    fixedResults[profile.name] = fixedSkillSimulation(profile, skillRuns);
  }
  printSkills(fixedResults, skillRuns);

  const improvement = improvementSimulation(scenarioCount);
  printImprovement(improvement);

  console.log("\n## 해석 주의");
  console.log(
    "- 도달 판수는 게임을 자동 플레이하거나 실제 로그로 학습한 결과가 아니다. " +
      "도구에 입력한 10m 조건부 통과율에 따른 목표 퍼널 민감도다.",
  );
  console.log(
    "- 목표 고도는 먹떼의 코스 진행 고도를 뜻한다. 가장 높은 한 마리의 최고 점수와 " +
      "무한맵을 여는 SwarmProgressHeight는 실제 게임에서 다를 수 있다.",
  );
  console.log(
    "- 콘텐츠/먹떼 표는 코드 상수와 난수 규칙을 반영하지만, 어린 용 활성 폭·" +
      "상승기류 시점·아이템 실제 획득 고도는 근사한다.",
  );
  console.log(
    "- 통과율/반복 향상 표는 실제 플레이 로그가 없는 상태의 민감도 모델이다. " +
      "10m 조건부 통과율 안에 발판 실수, 장애물, 분신·방패·바람의 순효과를 묶었다.",
  );
  console.log(
    "- 따라서 목표 판수는 출시 판정값이 아니다. 여러 테스터의 반복 플레이 로그로 " +
      "별도 검증해야 한다.",
  );
}

main();
