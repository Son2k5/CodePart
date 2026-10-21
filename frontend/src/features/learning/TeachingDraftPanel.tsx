import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { axiosInstance as api } from '../../lib/axios';
import { getErrorMessage } from '../../lib/errorService';
import { RagSources, type RagReference } from '../ai-assistant/RagSources';

type Test = { input: string; expectedOutput: string; isSample: boolean };
type Quiz = { question: string; choices: string[]; correctIndex: number; explanation: string };
type Content = { title: string; description: string; referenceSolution: string; tests: Test[]; quiz: Quiz[]; sourceIds: string[] };
type Draft = { id: string; kind: string; language: string; content: string; sources: string; status: string; validation: string; rowVersion: string };
function errorText(e: unknown) { return getErrorMessage(e, 'Không thực hiện được. Kiểm tra dịch vụ và thử lại.'); }

export function TeachingDraftPanel({ topicId }: { topicId: string }) {
  const [kind, setKind] = useState('exercise'); const [language, setLanguage] = useState('python');
  const [question, setQuestion] = useState(''); const [busy, setBusy] = useState(false); const [error, setError] = useState('');
  const [selected, setSelected] = useState<Draft>();
  const path = `/topics/${topicId}/rag/drafts`;
  const list = useQuery({ queryKey: ['teaching-drafts', topicId], queryFn: async () => (await api.get<Draft[]>(path)).data });
  async function generate() {
    setBusy(true); setError('');
    try { const { data } = await api.post<Draft>(path, { question, kind, language }, { timeout: 65000 }); setSelected(data); await list.refetch(); }
    catch (e) { setError(errorText(e)); } finally { setBusy(false); }
  }
  return <section className="learning-assistant"><h3>Hỗ trợ biên soạn cho giảng viên</h3><p>Tạo bản nháp từ bài học, chỉnh sửa và kiểm chứng trước khi duyệt. Nội dung này chỉ dành cho người quản lý lớp.</p>
    <form onSubmit={e => { e.preventDefault(); void generate(); }}><label>Loại nội dung<select value={kind} disabled={busy} onChange={e => setKind(e.target.value)}><option value="exercise">Bài tập lập trình</option><option value="quiz">Trắc nghiệm</option></select></label>
      {kind === 'exercise' && <label>Ngôn ngữ<select value={language} disabled={busy} onChange={e => setLanguage(e.target.value)}>{['python', 'cpp', 'java', 'csharp'].map(x => <option key={x}>{x}</option>)}</select></label>}
      <label>Yêu cầu<textarea required maxLength={2000} value={question} onChange={e => setQuestion(e.target.value)} placeholder="Ví dụ: Bài tập mảng một chiều ở mức cơ bản, có trường hợp phần tử trùng." /></label><button disabled={busy || !question.trim()}>{busy ? 'Đang tạo bản nháp…' : 'Tạo bản nháp'}</button>
    </form>{error && <p role="alert">{error}</p>}
    {list.isError && <p role="alert">Chưa tải được bản nháp. <button onClick={() => void list.refetch()}>Thử lại</button></p>}
    <ul>{list.data?.map(d => <li key={d.id}><button disabled={busy} onClick={() => setSelected(d)}>{(JSON.parse(d.content) as Content).title} · {d.status === 'Reviewed' ? 'Đã duyệt' : 'Bản nháp'}</button></li>)}</ul>
    {selected && <DraftEditor key={`${selected.id}:${selected.rowVersion}`} draft={selected} path={path} onChange={async d => { setSelected(d); await list.refetch(); }} />}
  </section>;
}

function DraftEditor({ draft, path, onChange }: { draft: Draft; path: string; onChange: (d: Draft) => Promise<void> }) {
  const [content, setContent] = useState<Content>(() => JSON.parse(draft.content));
  const [dirty, setDirty] = useState(false); const [busy, setBusy] = useState(false); const [error, setError] = useState('');
  function change(c: Content) { setContent(c); setDirty(true); }
  async function action(name: 'save' | 'verify' | 'review') {
    setBusy(true); setError('');
    try {
      const response = name === 'save' ? await api.put<Draft>(`${path}/${draft.id}`, { rowVersion: draft.rowVersion, content })
        : await api.post<Draft>(`${path}/${draft.id}/${name}`, { rowVersion: draft.rowVersion }, { timeout: name === 'verify' ? 190000 : 35000 });
      await onChange(response.data);
    } catch (e) { setError(errorText(e)); } finally { setBusy(false); }
  }
  function download() {
    const file = draft.kind === 'exercise' ? content.tests.map((t, i) => ({ ...t, order: i + 1, weight: 1 })) : content;
    const url = URL.createObjectURL(new Blob([JSON.stringify(file, null, 2)], { type: 'application/json' }));
    const a = document.createElement('a'); a.href = url; a.download = `${draft.kind}-${draft.id}.json`; a.click(); URL.revokeObjectURL(url);
  }
  return <fieldset disabled={busy}><legend>Rà soát bản nháp</legend><label>Tiêu đề<input value={content.title} maxLength={250} onChange={e => change({ ...content, title: e.target.value })} /></label>
    <label>Mô tả<textarea rows={6} value={content.description} maxLength={12000} onChange={e => change({ ...content, description: e.target.value })} /></label>
    {draft.kind === 'exercise' ? <><label>Lời giải tham chiếu · {draft.language}<textarea rows={12} maxLength={20000} value={content.referenceSolution} onChange={e => change({ ...content, referenceSolution: e.target.value })} /></label>
      {content.tests.map((test, i) => <details key={i}><summary>Test {i + 1}{test.isSample ? ' · Sample' : ''}</summary><label>Input<textarea value={test.input} onChange={e => change({ ...content, tests: content.tests.map((t, j) => j === i ? { ...t, input: e.target.value } : t) })} /></label><label>Output<textarea value={test.expectedOutput} onChange={e => change({ ...content, tests: content.tests.map((t, j) => j === i ? { ...t, expectedOutput: e.target.value } : t) })} /></label>
        <label><input type="checkbox" checked={test.isSample} onChange={e => change({ ...content, tests: content.tests.map((t, j) => j === i ? { ...t, isSample: e.target.checked } : t) })} /> Hiển thị làm ví dụ</label></details>)}
    </> : content.quiz.map((q, i) => <fieldset key={i}><legend>Câu {i + 1}</legend><label>Câu hỏi<textarea value={q.question} onChange={e => change({ ...content, quiz: content.quiz.map((x, j) => j === i ? { ...x, question: e.target.value } : x) })} /></label>
      {q.choices.map((choice, k) => <label key={k}>Lựa chọn {k + 1}<input value={choice} onChange={e => change({ ...content, quiz: content.quiz.map((x, j) => j === i ? { ...x, choices: x.choices.map((v, n) => n === k ? e.target.value : v) } : x) })} /></label>)}
      <label>Đáp án đúng<select value={q.correctIndex} onChange={e => change({ ...content, quiz: content.quiz.map((x, j) => j === i ? { ...x, correctIndex: Number(e.target.value) } : x) })}>{[0, 1, 2, 3].map(k => <option key={k} value={k}>{k + 1}</option>)}</select></label>
      <label>Giải thích<textarea value={q.explanation} onChange={e => change({ ...content, quiz: content.quiz.map((x, j) => j === i ? { ...x, explanation: e.target.value } : x) })} /></label></fieldset>)}
    <RagSources sources={JSON.parse(draft.sources) as RagReference[]} />
    <p>Trạng thái: {draft.status === 'Reviewed' ? 'Đã duyệt' : 'Bản nháp'}{draft.kind === 'exercise' && ` · Kiểm chứng: ${draft.validation === 'Passed' ? 'Đã qua toàn bộ test' : draft.validation === 'NotRun' ? 'Chưa chạy' : draft.validation}`}</p>
    {error && <p role="alert">{error}</p>}<div className="learning-actions"><button disabled={!dirty} onClick={() => void action('save')}>Lưu chỉnh sửa</button>
      {draft.kind === 'exercise' && <button disabled={dirty} onClick={() => void action('verify')}>Chạy lời giải qua bộ test</button>}
      <button disabled={dirty || draft.status === 'Reviewed' || draft.kind === 'exercise' && draft.validation !== 'Passed'} onClick={() => void action('review')}>Xác nhận đã rà soát</button>
      <button disabled={dirty || draft.status !== 'Reviewed'} onClick={download}>{draft.kind === 'exercise' ? 'Tải bộ test để nhập vào ngân hàng bài tập' : 'Tải bộ câu hỏi đã duyệt'}</button>
    </div><p>{busy ? 'Đang xử lý…' : 'Bản nháp chưa tự công bố cho sinh viên. Với bài lập trình, sao chép đề và nhập bộ test vào ngân hàng bài tập để xuất bản.'}</p>
  </fieldset>;
}
