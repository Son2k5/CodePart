import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import ts from 'typescript';

const source = await readFile(new URL('../src/features/ai-assistant/stream.ts', import.meta.url), 'utf8');
const { outputText } = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 } });
const { consumeHintStream } = await import(`data:text/javascript;base64,${Buffer.from(outputText).toString('base64')}`);

test('SSE preserves Vietnamese across network and UTF-8 boundaries', async () => {
  const bytes = new TextEncoder().encode('event: delta\ndata: "Gợi ý tiếng Việt"\n\nevent: result\ndata: {"remaining":29}\n\n');
  const stream = new ReadableStream({ start(controller) { for (let i = 0; i < bytes.length; i += 7) controller.enqueue(bytes.slice(i, i + 7)); controller.close(); } });
  let text = '';
  const result = await consumeHintStream(stream, delta => { text += delta; });
  assert.equal(text, 'Gợi ý tiếng Việt'); assert.equal(result.remaining, 29);
});

test('a truncated SSE response does not become a completed hint', async () => {
  const stream = new ReadableStream({ start(controller) { controller.enqueue(new TextEncoder().encode('event: delta\ndata: "hello"\n\n')); controller.close(); } });
  await assert.rejects(() => consumeHintStream(stream, () => {}), /Incomplete/);
});
