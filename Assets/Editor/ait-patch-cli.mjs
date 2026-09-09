#!/usr/bin/env node
// Apps-in-Toss 3.x가 WEB .ait 메타데이터에 runtimeVersion을 생략하는
// upstream 공백을 빌드 직전에 보완한다. 패치 실패는 반드시 non-zero로 끝난다.

import {
  readFileSync,
  realpathSync,
  unlinkSync,
  writeFileSync,
} from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

export const RUNTIME_VERSION = '0.84.0';
const TAG = '[mukjump-ait-runtime]';

export function injectRuntimeVersion(src, runtimeVersion = RUNTIME_VERSION) {
  const object = findSetMetadataObject(src);
  if (!object) {
    return {
      changed: false,
      valid: false,
      source: src,
      reason: 'setMetadata 객체 리터럴을 찾지 못했습니다',
    };
  }

  const objectText = src.slice(object.start, object.end + 1);
  const property = /\bruntimeVersion\s*:\s*([^,}\n]+)/.exec(objectText);
  if (property) {
    const exactLiteral = new RegExp(
      `\\bruntimeVersion\\s*:\\s*["']${escapeRegExp(runtimeVersion)}["']`,
    );
    return {
      changed: false,
      valid: exactLiteral.test(objectText),
      source: src,
      reason: exactLiteral.test(objectText)
        ? `runtimeVersion=${JSON.stringify(runtimeVersion)} 이미 존재합니다`
        : `다른 runtimeVersion 표현식이 이미 존재합니다: ${property[1].trim()}`,
    };
  }

  const insertAt = object.start + 1;
  const injected = `\n    runtimeVersion: ${JSON.stringify(runtimeVersion)},`;
  return {
    changed: true,
    valid: true,
    source: src.slice(0, insertAt) + injected + src.slice(insertAt),
    reason: `runtimeVersion=${JSON.stringify(runtimeVersion)} 삽입`,
  };
}

export function resolveCliEntry(cwd = process.cwd()) {
  const projectRequire = createRequire(resolve(cwd, 'package.json'));
  const frameworkPackageJsonPath = projectRequire.resolve(
    '@apps-in-toss/web-framework/package.json',
  );
  const frameworkPackageJson = JSON.parse(
    readFileSync(frameworkPackageJsonPath, 'utf8'),
  );
  const frameworkMajor = Number.parseInt(
    String(frameworkPackageJson.version ?? '').split('.')[0],
    10,
  );
  if (!Number.isFinite(frameworkMajor) || frameworkMajor < 3) {
    throw new Error(
      `지원하지 않는 @apps-in-toss/web-framework 버전입니다: ` +
      `${frameworkPackageJson.version ?? '(없음)'}`,
    );
  }

  // pnpm strict layout에서는 cli가 루트 direct dependency가 아니라
  // web-framework의 dependency이므로 프레임워크 위치를 기준으로 해석한다.
  const frameworkRequire = createRequire(frameworkPackageJsonPath);
  const packageJsonPath = frameworkRequire.resolve(
    '@apps-in-toss/cli/package.json',
  );
  const packageJson = JSON.parse(readFileSync(packageJsonPath, 'utf8'));
  const bin = typeof packageJson.bin === 'string'
    ? packageJson.bin
    : packageJson.bin?.ait;
  if (typeof bin !== 'string' || bin.trim() === '') {
    throw new Error('@apps-in-toss/cli package.json에 ait bin이 없습니다.');
  }
  return realpathSync(resolve(dirname(packageJsonPath), bin));
}

export function verifyRuntimeVersion(src, runtimeVersion = RUNTIME_VERSION) {
  const object = findSetMetadataObject(src);
  if (!object) return false;
  const objectText = src.slice(object.start, object.end + 1);
  return new RegExp(
    `\\bruntimeVersion\\s*:\\s*["']${escapeRegExp(runtimeVersion)}["']`,
  ).test(objectText);
}

function findSetMetadataObject(src) {
  const method = 'setMetadata';
  let index = -1;
  for (let cursor = 0; cursor < src.length; cursor++) {
    const skipped = skipStringOrComment(src, cursor);
    if (skipped > cursor) {
      cursor = skipped - 1;
      continue;
    }
    if (!src.startsWith(method, cursor)) continue;
    const before = cursor > 0 ? src[cursor - 1] : '';
    const after = src[cursor + method.length] ?? '';
    if (/[$\w]/.test(before) || /[$\w]/.test(after)) continue;
    const openParen = skipTrivia(src, cursor + method.length);
    if (src[openParen] !== '(') continue;
    const objectStart = skipTrivia(src, openParen + 1);
    if (src[objectStart] !== '{') continue;
    index = objectStart;
    break;
  }
  if (index < 0) return null;

  const start = index;
  let depth = 0;
  for (let cursor = start; cursor < src.length; cursor++) {
    const skipped = skipStringOrComment(src, cursor);
    if (skipped > cursor) {
      cursor = skipped - 1;
      continue;
    }
    if (src[cursor] === '{') depth++;
    if (src[cursor] === '}' && --depth === 0) {
      return { start, end: cursor };
    }
  }
  return null;
}

function skipTrivia(src, index) {
  for (;;) {
    while (index < src.length && /\s/.test(src[index])) index++;
    const next = skipComment(src, index);
    if (next === index) return index;
    index = next;
  }
}

function skipStringOrComment(src, index) {
  const character = src[index];
  if (character === '"' || character === "'" || character === '`') {
    return skipString(src, index);
  }
  return skipComment(src, index);
}

function skipString(src, index) {
  const quote = src[index];
  let cursor = index + 1;
  while (cursor < src.length) {
    if (src[cursor] === '\\') {
      cursor += 2;
      continue;
    }
    if (src[cursor] === quote) return cursor + 1;
    cursor++;
  }
  return cursor;
}

function skipComment(src, index) {
  if (src[index] === '/' && src[index + 1] === '/') {
    let cursor = index + 2;
    while (cursor < src.length && src[cursor] !== '\n') cursor++;
    return cursor;
  }
  if (src[index] === '/' && src[index + 1] === '*') {
    let cursor = index + 2;
    while (
      cursor < src.length &&
      !(src[cursor] === '*' && src[cursor + 1] === '/')
    ) cursor++;
    return Math.min(src.length, cursor + 2);
  }
  return index;
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function main() {
  const cliPath = resolveCliEntry();
  const source = readFileSync(cliPath, 'utf8');
  const result = injectRuntimeVersion(source);
  if (!result.valid) throw new Error(result.reason);

  if (result.changed) {
    // pnpm의 content-addressable store와 연결된 hardlink를 끊고 새 inode에 쓴다.
    try { unlinkSync(cliPath); } catch { /* 일반 파일이면 그대로 생성한다. */ }
    writeFileSync(cliPath, result.source, 'utf8');
  }

  const written = readFileSync(cliPath, 'utf8');
  if (!verifyRuntimeVersion(written)) {
    throw new Error('패치 후 CLI에서 runtimeVersion을 재확인하지 못했습니다.');
  }
  console.log(`${TAG} ${result.reason}; 재검증 완료.`);
}

const invokedPath = process.argv[1];
if (invokedPath && import.meta.url === pathToFileURL(invokedPath).href) {
  try {
    main();
  } catch (error) {
    console.error(`${TAG} 실패: ${error?.message ?? error}`);
    process.exitCode = 1;
  }
}
