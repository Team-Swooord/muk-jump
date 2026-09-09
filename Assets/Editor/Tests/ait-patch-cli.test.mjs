import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, realpathSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import {
  RUNTIME_VERSION,
  injectRuntimeVersion,
  resolveCliEntry,
  verifyRuntimeVersion,
} from '../ait-patch-cli.mjs';

test('pnpm strict형 nested CLI를 web-framework 기준으로 찾는다', () => {
  const root = mkdtempSync(join(tmpdir(), 'mukjump-ait-resolve-'));
  const framework = join(
    root,
    'node_modules',
    '@apps-in-toss',
    'web-framework',
  );
  const cli = join(
    framework,
    'node_modules',
    '@apps-in-toss',
    'cli',
  );
  mkdirSync(join(cli, 'dist'), { recursive: true });
  writeFileSync(join(root, 'package.json'), '{"type":"module"}');
  writeFileSync(
    join(framework, 'package.json'),
    JSON.stringify({
      name: '@apps-in-toss/web-framework',
      version: '3.0.1',
      exports: { './package.json': './package.json' },
    }),
  );
  writeFileSync(
    join(cli, 'package.json'),
    JSON.stringify({
      name: '@apps-in-toss/cli',
      version: '3.0.1',
      bin: { ait: 'dist/index.js' },
      exports: { './package.json': './package.json' },
    }),
  );
  const entry = join(cli, 'dist', 'index.js');
  writeFileSync(entry, 'writer.setMetadata({ platform: 2 });');

  assert.equal(resolveCliEntry(root), realpathSync(entry));
});

test('runtimeVersion을 정확히 한 번 넣고 두 번째 변환은 byte-identical이다', () => {
  const source = `
    const marker = "setMetadata({ runtimeVersion: fake })";
    writer.setMetadata({
      platform: WEB,
      extra: { nested: '}' }, // nested/comment 보존
    });
  `;
  const once = injectRuntimeVersion(source);
  assert.equal(once.changed, true);
  assert.equal(once.valid, true);
  assert.equal(verifyRuntimeVersion(once.source), true);
  assert.equal(
    once.source.match(/runtimeVersion\s*:/g)?.length,
    2,
    '문자열 안 가짜 토큰 하나와 실제 속성 하나만 있어야 합니다.',
  );

  const twice = injectRuntimeVersion(once.source);
  assert.equal(twice.changed, false);
  assert.equal(twice.valid, true);
  assert.equal(twice.source, once.source);
});

test('upstream exact 값은 no-op, 다른 값과 구조 변경은 실패한다', () => {
  const upstream = injectRuntimeVersion(
    `writer.setMetadata({ runtimeVersion: '${RUNTIME_VERSION}', platform: WEB });`,
  );
  assert.equal(upstream.changed, false);
  assert.equal(upstream.valid, true);

  const conflict = injectRuntimeVersion(
    "writer.setMetadata({ runtimeVersion: 'other', platform: WEB });",
  );
  assert.equal(conflict.valid, false);

  const missing = injectRuntimeVersion('writer.setMetadata(metadata);');
  assert.equal(missing.valid, false);
});
