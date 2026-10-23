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
  return <section className="mt-4 [&_ul]:list-none [&_ul]:p-0 [&_li]:my-2"><h3 className="text-sm font-semibold my-2">{label}</h3><ul className="list-none p-0">{sources.map(s => <li key={s.chunkId} className="my-2">
    <button type="button" disabled={busy} onClick={() => void open(s)} className="border border-[#c7d3df] rounded-lg px-3 py-2 bg-[#f3f7fb] text-sm cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed hover:bg-[#e9f0f8]">{s.title}{s.page ? ` · trang ${s.page}` : s.heading ? ` · ${s.heading}` : ''}</button>
  </li>)}</ul>{error && <p role="alert" className="text-[#a32222] text-sm my-2">{error}</p>}
    {source && <div className="p-[18px] border border-[#d3deeb] rounded-xl mt-4 max-h-[500px] overflow-auto bg-white"><button type="button" onClick={() => setSource(undefined)} className="border border-[#c7d3df] rounded-lg px-3 py-2 bg-[#f3f7fb] text-sm cursor-pointer hover:bg-[#e9f0f8] mb-3">Đóng nguồn</button><h4 className="font-semibold text-base mb-2">{source.title}</h4>
      {source.url ? <a href={source.url} target="_blank" rel="noopener noreferrer" className="text-[#175cb8] underline text-sm">Mở tài liệu PDF</a> : <Markdown skipHtml rehypePlugins={[rehypeSanitize]}>{source.text}</Markdown>}
    </div>}
  </section>;
}
