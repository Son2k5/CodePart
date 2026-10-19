import { useState } from 'react';
import Markdown from 'react-markdown';
import rehypeSanitize from 'rehype-sanitize';
import { axiosInstance as api } from '../../lib/axios';

export type RagReference = { chunkId: string; documentId: string; blockId: string; topicId: string;
  classId: string; version: number; title: string; page?: number; heading: string };
type Source = { kind: string; title: string; text?: string; url?: string };

export function RagSources({ sources, label = 'Nguồn tham khảo' }: { sources: RagReference[]; label?: string }) {
  const [source, setSource] = useState<Source>();
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  async function open(ref: RagReference) {
    setBusy(true); setError(''); setSource(undefined);
    try {
      const { data } = await api.get<Source>(`/topics/${ref.topicId}/rag/sources/${ref.blockId}`, { params: { version: ref.version } });
      if (data.url) {
        const url = new URL(data.url);
        if (url.protocol !== 'https:') throw new Error('Invalid source URL');
        data.url = `${url.href}${ref.page ? `#page=${ref.page}` : ''}`;
      }
      setSource(data);
    } catch { setError('Nguồn đã thay đổi hoặc bạn không còn quyền xem. Hãy hỏi lại để cập nhật.'); }
    finally { setBusy(false); }
  }
  if (!sources.length) return null;
  return <section className="rag-sources"><h3>{label}</h3><ul>{sources.map(s => <li key={s.chunkId}>
    <button type="button" disabled={busy} onClick={() => void open(s)}>{s.title}{s.page ? ` · trang ${s.page}` : s.heading ? ` · ${s.heading}` : ''}</button>
  </li>)}</ul>{error && <p role="alert">{error}</p>}
    {source && <div className="rag-source"><button type="button" onClick={() => setSource(undefined)}>Đóng nguồn</button><h4>{source.title}</h4>
      {source.url ? <a href={source.url} target="_blank" rel="noopener noreferrer">Mở tài liệu PDF</a> : <Markdown skipHtml rehypePlugins={[rehypeSanitize]}>{source.text}</Markdown>}
    </div>}
  </section>;
}
