import { lazy, Suspense, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import Markdown from 'react-markdown';
import rehypeSanitize from 'rehype-sanitize';
import { axiosInstance as api } from '../../lib/axios';
import { getErrorMessage } from '../../lib/errorService';
import { useAuthStore } from '../auth/store/authStore';
import { RagSources, type RagReference } from '../ai-assistant/RagSources';
import './learning.css';

const TeachingDraftPanel = lazy(() => import('./TeachingDraftPanel').then(m => ({ default: m.TeachingDraftPanel })));

type Block = { blockId: string; type: string; data: { markdown?: string; title?: string; assetId?: string; pageCount?: number; url?: string } };
type Topic = { id: string; chapterId: string; title: string; slug: string; rowVersion: string; publishedVersion: number;
  draftContent: string; status: string; lockedReason?: string | null };
type Chapter = { id: string; title: string; topics?: Topic[] };
type ReadTopic = { id: string; title: string; publishedVersion: number; content: Block[]; exercises: { problemId: string; status: string }[] };
type IndexStatus = { enabled: boolean; status: string; chunks: number; errorCode?: string };
const statusNames: Record<string, string> = { Pending: 'Đang chờ xử lý', Processing: 'Đang xử lý', Ready: 'Sẵn sàng',
  Failed: 'Xử lý chưa thành công', NotIndexed: 'Chưa xử lý', Superseded: 'Phiên bản đã thay đổi' };
function message(e: unknown) {
  return getErrorMessage(e, 'Không thực hiện được. Kiểm tra quyền truy cập và thử lại.');
}

export function LearningPage() {
  const { classId = '' } = useParams();
  const role = useAuthStore(s => s.user?.role);
  const manage = role === 'Admin' || role === 'Teacher';
  const [selected, setSelected] = useState('');
  const [chapterTitle, setChapterTitle] = useState('');
  const [topicTitle, setTopicTitle] = useState('');
  const [chapterId, setChapterId] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const chapters = useQuery({ queryKey: ['learning-chapters', classId, manage], queryFn: async () => manage
    ? (await api.get<Chapter[]>(`/classes/${classId}/chapters`)).data
    : (await api.get<{ chapters: Chapter[] }>(`/classes/${classId}/outline`)).data.chapters });
  const topics = useQuery({ queryKey: ['learning-topics', classId], enabled: manage,
    queryFn: async () => (await api.get<Topic[]>(`/classes/${classId}/topics`)).data });
  const allTopics = manage ? topics.data ?? [] : chapters.data?.flatMap(c => c.topics ?? []) ?? [];
  const current = allTopics.find(t => t.id === selected);
  async function create(kind: 'chapter' | 'topic') {
    setBusy(true); setError('');
    try {
      if (kind === 'chapter') {
        await api.post(`/classes/${classId}/chapters`, { title: chapterTitle, order: chapters.data?.length ?? 0 }); setChapterTitle('');
      } else {
        const { data } = await api.post<{ id: string }>(`/classes/${classId}/topics`, { chapterId: chapterId || chapters.data?.[0]?.id,
          title: topicTitle, slug: `bai-${crypto.randomUUID()}`, summary: '', order: allTopics.length, estimatedMinutes: 0 });
        setTopicTitle(''); setSelected(data.id);
      }
      await Promise.all([chapters.refetch(), topics.refetch()]);
    } catch (e) { setError(message(e)); } finally { setBusy(false); }
  }
  return <main className="learning-page"><Link to={manage ? role === 'Admin' ? `/admin/classes/${classId}` : '/teacher/classes' : '/classes'}>← Danh sách lớp</Link>
    <h1>Học liệu và trợ giảng</h1><p>Đọc bài học, tham khảo tài liệu và đặt câu hỏi theo nội dung của lớp.</p>
    {(chapters.isError || topics.isError) && <p role="alert">Không tải được học liệu. <button onClick={() => { void chapters.refetch(); if (manage) void topics.refetch(); }}>Thử lại</button></p>}
    {error && <p role="alert">{error}</p>}
    {manage && <div className="learning-create"><form onSubmit={e => { e.preventDefault(); void create('chapter'); }}><label>Tên chương<input required maxLength={250} value={chapterTitle} onChange={e => setChapterTitle(e.target.value)} /></label><button disabled={busy}>Thêm chương</button></form>
      <form onSubmit={e => { e.preventDefault(); void create('topic'); }}><label>Chương<select value={chapterId || chapters.data?.[0]?.id || ''} onChange={e => setChapterId(e.target.value)}>{chapters.data?.map(c => <option key={c.id} value={c.id}>{c.title}</option>)}</select></label><label>Tên bài học<input required maxLength={250} value={topicTitle} onChange={e => setTopicTitle(e.target.value)} /></label><button disabled={busy || !chapters.data?.length}>Thêm bài học</button></form></div>}
    <div className="learning-layout"><nav aria-label="Danh sách bài học">{chapters.isPending ? <p>Đang tải…</p> : !chapters.data?.length ? <p>Chưa có học liệu.</p> : chapters.data.map(c => <section key={c.id}><h2>{c.title}</h2>{allTopics.filter(t => t.chapterId === c.id).map(t =>
      <button key={t.id} disabled={!manage && !!t.lockedReason} aria-pressed={selected === t.id} onClick={() => setSelected(t.id)}>{t.title}{!manage && t.lockedReason ? ' · Chưa mở' : ''}</button>)}</section>)}</nav>
      <div>{current ? manage ? <TopicEditor key={current.id} topic={current} refresh={() => topics.refetch()} /> : <TopicReader key={current.id} classId={classId} topic={current} /> : <p>Chọn một bài học để bắt đầu.</p>}</div>
    </div>
  </main>;
}

function TopicEditor({ topic, refresh }: { topic: Topic; refresh: () => Promise<unknown> }) {
  const [blocks, setBlocks] = useState<Block[]>(() => JSON.parse(topic.draftContent));
  const [rowVersion, setRowVersion] = useState(topic.rowVersion);
  const [busy, setBusy] = useState(false); const [error, setError] = useState(''); const [notice, setNotice] = useState('');
  const status = useQuery({ queryKey: ['rag-status', topic.id], queryFn: async () => (await api.get<IndexStatus>(`/topics/${topic.id}/rag/status`)).data,
    refetchInterval: q => ['Pending', 'Processing'].includes(q.state.data?.status ?? '') ? 5000 : false });
  async function save(publish: boolean) {
    setBusy(true); setError(''); setNotice('');
    try {
      const { data } = await api.put<{ rowVersion: string }>(`/topics/${topic.id}/draft`, { rowVersion, blocks }); setRowVersion(data.rowVersion);
      if (publish) {
        const result = await api.post<{ rowVersion: string }>(`/topics/${topic.id}/publish`); setRowVersion(result.data.rowVersion);
      }
      await refresh(); await status.refetch(); setNotice(publish ? 'Đã công bố. Học liệu sẽ được xử lý cho trợ giảng khi dịch vụ được bật.' : 'Đã lưu bản nháp.');
    } catch (e) { setError(message(e)); } finally { setBusy(false); }
  }
  async function upload(file?: File) {
    if (!file) return;
    if (file.size > 20 * 1024 * 1024) { setError('PDF dùng cho trợ giảng cần nhỏ hơn hoặc bằng 20 MB.'); return; }
    setBusy(true); setError('');
    try {
      const signed = (await api.post<{ id: string; upload: { url: string; parameters: Record<string, string> } }>('/assets/sign-upload',
        { kind: 'Pdf', mimeType: 'application/pdf', sizeBytes: file.size })).data;
      const form = new FormData(); Object.entries(signed.upload.parameters).forEach(([k, v]) => form.append(k, v)); form.append('file', file);
      const uploaded = await fetch(signed.upload.url, { method: 'POST', body: form, credentials: 'omit' });
      if (!uploaded.ok) throw new Error('Upload failed');
      const asset = (await api.post<{ pageCount: number }>(`/assets/${signed.id}/complete`)).data;
      setBlocks(old => [...old, { blockId: crypto.randomUUID(), type: 'Pdf', data: { assetId: signed.id, title: file.name, pageCount: asset.pageCount } }]);
      setNotice('Đã tải PDF. Lưu hoặc công bố bài học để sử dụng.');
    } catch (e) { setError(message(e)); } finally { setBusy(false); }
  }
  async function reindex() {
    setBusy(true); setError('');
    try { await api.post(`/topics/${topic.id}/rag/reindex`); await status.refetch(); }
    catch (e) { setError(message(e)); } finally { setBusy(false); }
  }
  return <section><h2>{topic.title}</h2><p>{topic.status === 'Published' ? 'Đã công bố' : 'Bản nháp'} · Phiên bản {topic.publishedVersion}</p>
    {error && <p role="alert">{error}</p>}{notice && <p role="status">{notice}</p>}
    {blocks.map((b, i) => <fieldset key={b.blockId}><legend>{b.type === 'Markdown' ? 'Nội dung bài học' : b.data.title ?? b.type}</legend>
      {b.type === 'Markdown' ? <textarea aria-label={`Nội dung ${i + 1}`} rows={12} maxLength={200000} disabled={busy} value={b.data.markdown ?? ''} onChange={e => setBlocks(old => old.map(x => x.blockId === b.blockId ? { ...x, data: { ...x.data, markdown: e.target.value } } : x))} /> : <p>{b.type === 'Pdf' ? `${b.data.pageCount} trang` : 'Nội dung đã có trong bài học'}</p>}
      <button disabled={busy} onClick={() => setBlocks(old => old.filter(x => x.blockId !== b.blockId))}>Gỡ khỏi bản nháp</button>
    </fieldset>)}
    <div className="learning-actions"><button disabled={busy} onClick={() => setBlocks(old => [...old, { blockId: crypto.randomUUID(), type: 'Markdown', data: { markdown: '' } }])}>Thêm văn bản Markdown</button>
      <label>Thêm PDF<input type="file" accept="application/pdf,.pdf" disabled={busy} onChange={e => { void upload(e.target.files?.[0]); e.target.value = ''; }} /></label>
      <button disabled={busy} onClick={() => void save(false)}>Lưu bản nháp</button><button disabled={busy || !blocks.length} onClick={() => void save(true)}>Lưu và công bố</button></div>
    <section className="rag-status"><h3>Trợ giảng học liệu</h3>{status.isError ? <p>Chưa đọc được trạng thái. <button onClick={() => void status.refetch()}>Thử lại</button></p> : <>
      <p>{status.data?.enabled === false ? 'Dịch vụ chưa được bật · ' : ''}{statusNames[status.data?.status ?? ''] ?? 'Đang tải…'}{status.data?.status === 'Ready' ? ` · ${status.data.chunks} đoạn học liệu` : ''}</p>
      {status.data?.errorCode && <p role="alert">{status.data.errorCode === 'PDF_NEEDS_OCR' ? 'PDF không có văn bản đọc được. Hãy dùng bản PDF có chữ hoặc chuyển OCR trước.' : `Mã xử lý: ${status.data.errorCode}`}</p>}
      <button disabled={busy || !status.data?.enabled || ['Pending', 'Processing'].includes(status.data?.status ?? '') || !topic.publishedVersion} onClick={() => void reindex()}>Xử lý lại học liệu</button>
    </>}</section>
    {topic.status === 'Published' && <TopicAssistant topicId={topic.id} />}
    {topic.status === 'Published' && (
      <Suspense fallback={<p>Đang tải trợ giảng biên soạn…</p>}>
        <TeachingDraftPanel topicId={topic.id} />
      </Suspense>
    )}
  </section>;
}

function TopicReader({ classId, topic }: { classId: string; topic: Topic }) {
  const query = useQuery({ queryKey: ['learning-read', classId, topic.slug], queryFn: async () => (await api.get<ReadTopic>(`/classes/${classId}/topics/${topic.slug}`)).data });
  if (query.isPending) return <p>Đang tải bài học…</p>;
  if (query.isError) return <p role="alert">Không thể mở bài học này.</p>;
  const data = query.data;
  return <article><h2>{data.title}</h2>{data.content.map(b => <section key={b.blockId}>
    {b.type === 'Markdown' ? <Markdown skipHtml rehypePlugins={[rehypeSanitize]}>{b.data.markdown}</Markdown> : b.type === 'Pdf' ? <RagSources sources={[{ chunkId: b.blockId, documentId: b.data.assetId!, blockId: b.blockId, topicId: data.id, classId, version: data.publishedVersion, title: b.data.title ?? 'Tài liệu', heading: '' }]} /> : b.type === 'Video' && b.data.url ? <a href={b.data.url} target="_blank" rel="noopener noreferrer">Xem video bài học</a> : null}
  </section>)}{data.exercises.length > 0 && <section><h3>Bài tập</h3>{data.exercises.map((e, i) => <p key={e.problemId}><Link to={`/problems/${e.problemId}`}>Bài tập {i + 1}</Link> · {e.status}</p>)}</section>}<TopicAssistant topicId={data.id} /></article>;
}

function TopicAssistant({ topicId }: { topicId: string }) {
  const [question, setQuestion] = useState(''); const [busy, setBusy] = useState(false); const [error, setError] = useState('');
  const [result, setResult] = useState<{ answer: string; citations: RagReference[]; groundingStatus: string }>();
  const [matches, setMatches] = useState<(RagReference & { content: string })[]>([]);
  async function ask() {
    setBusy(true); setError(''); setResult(undefined); setMatches([]);
    try { setResult((await api.post(`/topics/${topicId}/rag/ask`, { question }, { timeout: 35000 })).data); }
    catch (e) { setError(message(e)); } finally { setBusy(false); }
  }
  async function search() {
    setBusy(true); setError(''); setResult(undefined); setMatches([]);
    try { const { data } = await api.post<(RagReference & { content: string })[]>(`/topics/${topicId}/rag/search`, { question }, { timeout: 15000 });
      setMatches(data); if (!data.length) setResult({ answer: 'Chưa tìm thấy đoạn học liệu phù hợp.', citations: [], groundingStatus: 'insufficient_context' }); }
    catch (e) { setError(message(e)); } finally { setBusy(false); }
  }
  return <section className="learning-assistant" aria-busy={busy}><h3>Hỏi trợ giảng về bài học</h3><p>Câu trả lời dựa trên học liệu được công bố mà bạn có quyền xem.</p>
    <form onSubmit={e => { e.preventDefault(); void ask(); }}><label>Câu hỏi<textarea required maxLength={2000} value={question} onChange={e => setQuestion(e.target.value)} placeholder="Ví dụ: Điều kiện dừng vòng lặp được giải thích như thế nào?" /></label><button disabled={busy || !question.trim()}>{busy ? 'Đang tìm học liệu và kiểm tra câu trả lời…' : 'Gửi câu hỏi'}</button><button type="button" disabled={busy || !question.trim()} onClick={() => void search()}>Tìm trong bài học</button></form>
    {error && <p role="alert">{error}</p>}{result && <div aria-live="polite"><Markdown skipHtml rehypePlugins={[rehypeSanitize]}>{result.answer}</Markdown><RagSources sources={result.citations} /></div>}
    {matches.map(p => <section key={p.chunkId}><Markdown skipHtml rehypePlugins={[rehypeSanitize]}>{p.content}</Markdown><RagSources sources={[p]} /></section>)}
  </section>;
}
