/** Decode SSE across arbitrary UTF-8/network chunk boundaries. */
export async function consumeHintStream<T>(stream: ReadableStream<Uint8Array>, onDelta: (text: string) => void): Promise<T> {
  const reader = stream.getReader();
  const decoder = new TextDecoder();
  let buffer = '';
  let result: T | undefined;
  try {
    while (true) {
      const { value, done } = await reader.read();
      buffer += decoder.decode(value, { stream: !done });
      let boundary: number;
      while ((boundary = buffer.indexOf('\n\n')) >= 0) {
        const event = buffer.slice(0, boundary); buffer = buffer.slice(boundary + 2);
        const name = event.split('\n').find(l => l.startsWith('event:'))?.slice(6).trim();
        const data = event.split('\n').filter(l => l.startsWith('data:')).map(l => l.slice(5).trimStart()).join('\n');
        if (name === 'delta') onDelta(JSON.parse(data));
        if (name === 'result') result = JSON.parse(data) as T;
      }
      if (done) break;
    }
  } finally { reader.releaseLock(); }
  if (!result) throw new Error('Incomplete hint stream');
  return result;
}
