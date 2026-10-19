import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useSearchParams } from 'react-router-dom';
import Editor from '@monaco-editor/react';
import { toast } from 'sonner';
import { judgeApi, judgeError, problemScope, scopeParams, type Problem, type Test, type Submission } from './api';
import { ScopePicker } from './ScopePicker';
import './JudgePage';
import './judge.css';

const empty: Partial<Problem> = { title: '', description: '', difficulty: 'Easy', timeLimitMs: 2000, memoryLimitMb: 256, points: 100, comparisonMode: 0, courseId: null,
  starterCodes: [{ language: 'python', template: '# Read from stdin and print the answer\n' }, { language: 'cpp', template: '#include <iostream>\nint main() {\n    return 0;\n}\n' }, { language: 'java', template: 'public class Main {\n    public static void main(String[] args) {\n    }\n}\n' }, { language: 'csharp', template: 'using System;\nclass Program { static void Main() { } }\n' }] };
export function JudgeAdminPage() {
  const [search, setSearch] = useSearchParams();
  const scope = search.get('scope') || '';
  const scopes = useQuery({ queryKey: ['judge-admin-scopes'], queryFn: () => judgeApi.scopes(true) });
  const [tab, setTab] = useState(search.get('tab') === 'submissions' ? 'submissions' : 'problems');
  const [page, setPage] = useState(1);
  const [form, setForm] = useState<Partial<Problem>>();
  const [tests, setTests] = useState<Test[]>([]);
  const [busy, setBusy] = useState(false);
  const [drag, setDrag] = useState<number>();
  const [selected, setSelected] = useState<Submission>();
  const [filters, setFilters] = useState({ problemId: '', courseId: '', userId: '', verdict: '' });
  const cache = useQueryClient();
  const problems = useQuery({ queryKey: ['judge-admin-list', page, scope], queryFn: () => judgeApi.list(true, page, scope) });
  const submissions = useQuery({ queryKey: ['judge-admin-submissions', page, filters, scope], queryFn: () => judgeApi.submissions({ ...Object.fromEntries(Object.entries(filters).filter(([, v]) => v)), ...scopeParams(scope), page }), enabled: tab === 'submissions' });
  const perform = async (fn: () => Promise<unknown>) => { setBusy(true); try { await fn(); await cache.invalidateQueries({ queryKey: ['judge-admin-list'] }); } catch (e) { judgeError(e); } finally { setBusy(false); } };
  const edit = async (id: string) => perform(async () => { const p = await judgeApi.problem(id, true); setForm(p); setTests(p.testCases); });
  const reorder = (from: number, to: number) => { const next = [...tests]; const [moved] = next.splice(from, 1); next.splice(to, 0, moved); setTests(next.map((t, i) => ({ ...t, order: i + 1 }))); };
  const save = () => perform(async () => {
    if (!form) return;
    const saved = await judgeApi.save(form);
    setForm({ ...form, id: saved.id, status: 'Draft' });
    await judgeApi.tests(saved.id, tests.map((t, i) => ({ ...t, order: i + 1 })));
    toast.success('Đã lưu đề bài và bộ test.');
  });
  const readImport = async (file?: File) => {
    if (!file) return;
    try {
      if (file.size > 30 * 1024 * 1024) throw new Error();
      const value: unknown = JSON.parse(await file.text());
      if (!Array.isArray(value) || value.length > 15 || !value.every(t => typeof t.input === 'string' && typeof t.expectedOutput === 'string' && typeof t.isSample === 'boolean' && Number.isInteger(t.weight) && t.weight > 0)) throw new Error();
      setTests(value.map((t, i) => ({ ...t, order: i + 1 })));
      toast.success('Đã đọc JSON. Bấm Lưu để cập nhật.');
    } catch { toast.error('JSON phải là mảng tối đa 15 test: input, expectedOutput, isSample, weight.'); }
  };
  const total = tab === 'problems' ? problems.data?.total : submissions.data?.total;
  return <main className="judge-shell"><Link to="/dashboard">← Trang quản lý</Link><h1>Bài tập lập trình theo lớp</h1>
    <p className="judge-muted">Chọn lớp, tạo đề và bộ test, sau đó xuất bản để học sinh làm bài trên khung coding chung.</p>
    <ScopePicker scopes={scopes.data || []} value={scope} all disabled={busy} onChange={v => { setSearch(v ? { scope: v } : {}); setPage(1); setForm(undefined); setSelected(undefined); }} />
    {scopes.isError && <p>Không tải được danh sách lớp. <button onClick={() => scopes.refetch()}>Thử lại</button></p>}
    <nav className="judge-tabs"><button aria-pressed={tab === 'problems'} onClick={() => { setTab('problems'); setPage(1); }}>Đề bài</button><button aria-pressed={tab === 'submissions'} onClick={() => { setTab('submissions'); setPage(1); }}>Bài nộp học sinh</button></nav>
    {tab === 'problems' ? <><div className="judge-actions"><button disabled={!scopes.data?.length} onClick={() => { setForm({ ...structuredClone(empty), classId: null, ...scopeParams(scope) }); setTests([]); }}>+ Tạo bài tập cho lớp</button></div>
      {problems.isError && <p>Không tải được danh sách đề bài.</p>}{problems.data?.items.map(p => <div className="judge-card judge-list-item" key={p.id}><div><strong>{p.title}</strong><p>{scopes.data?.find(s => `${s.kind}:${s.id}` === problemScope(p))?.name} · {p.status === 'Published' ? 'Đã đăng' : 'Bản nháp'} · {p.difficulty}</p></div><div className="judge-actions"><button disabled={busy} onClick={() => edit(p.id)}>Mở</button><button disabled={busy} onClick={() => perform(async () => { await judgeApi.action(p.id, p.status === 'Published' ? 'unpublish' : 'publish'); if (form?.id === p.id) setForm(undefined); })}>{p.status === 'Published' ? 'Về nháp' : 'Xuất bản'}</button><button disabled={busy} onClick={() => { if (confirm('Ẩn bài tập này?')) void perform(async () => { await judgeApi.action(p.id, 'delete'); if (form?.id === p.id) setForm(undefined); }); }}>Xóa</button></div></div>)}
      {form && <section className="judge-card"><div className="judge-actions"><strong>{form.id ? 'Chỉnh sửa bài tập' : 'Bài tập mới'}</strong><button onClick={() => setForm(undefined)}>Đóng</button></div>{form.status === 'Published' && <p className="judge-error">Chuyển về nháp trước khi chỉnh sửa.</p>}
        <fieldset disabled={busy || form.status === 'Published'}><div className="judge-form">
          <label>Tiêu đề<input value={form.title} onChange={e => setForm({ ...form, title: e.target.value })} /></label>
          <label>Lớp nhận bài tập<ScopePicker scopes={scopes.data || []} value={problemScope(form)} disabled={!!form.id} onChange={v => setForm({ ...form, courseId: null, classId: null, ...scopeParams(v) })} /></label>
          <label>Độ khó<select value={form.difficulty} onChange={e => setForm({ ...form, difficulty: e.target.value })}>{['Easy', 'Medium', 'Hard'].map(d => <option key={d}>{d}</option>)}</select></label>
          <label>So sánh output<select value={form.comparisonMode} onChange={e => setForm({ ...form, comparisonMode: Number(e.target.value) })}><option value={0}>Theo dòng</option><option value={1}>Theo token</option></select></label>
          {(['timeLimitMs', 'memoryLimitMb', 'points'] as const).map((field, i) => <label key={field}>{['Thời gian (ms)', 'Bộ nhớ (MB)', 'Điểm'][i]}<input type="number" value={form[field]} onChange={e => setForm({ ...form, [field]: Number(e.target.value) })} /></label>)}
          <label className="judge-wide">Mô tả (Markdown)<textarea value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} /></label>
          {form.starterCodes?.map((starter, i) => <label key={starter.language}>Code mẫu · {starter.language}<textarea value={starter.template} onChange={e => setForm({ ...form, starterCodes: form.starterCodes?.map((s, j) => i === j ? { ...s, template: e.target.value } : s) })} /></label>)}
          <div className="judge-wide"><h3>Test cases · {tests.length}/15</h3><p className="judge-muted">Xuất bản cần 10–15 test, trong đó có 1–2 sample. Kéo thả để đổi thứ tự.</p><label>Import JSON<input type="file" accept="application/json,.json" onChange={e => { void readImport(e.target.files?.[0]); e.target.value = ''; }} /></label>
            {tests.map((t, i) => <section key={i} className="judge-card judge-test-editor" draggable onDragStart={() => setDrag(i)} onDragOver={e => e.preventDefault()} onDrop={() => { if (drag !== undefined) reorder(drag, i); setDrag(undefined); }}>
              <div className="judge-summary"><strong>#{i + 1}</strong><label><input type="checkbox" checked={t.isSample} onChange={e => setTests(tests.map((v, j) => i === j ? { ...v, isSample: e.target.checked } : v))} /> Sample (hiển thị cho học sinh)</label><button onClick={() => setTests(tests.filter((_, j) => i !== j))}>Xóa</button></div>
              <div className="judge-test-fields">{(['input', 'expectedOutput'] as const).map(field => <label key={field}>{field}<textarea value={t[field]} onChange={e => setTests(tests.map((v, j) => i === j ? { ...v, [field]: e.target.value } : v))} /></label>)}</div>
              <label>Trọng số<input type="number" min="1" max="1000" value={t.weight} onChange={e => setTests(tests.map((v, j) => i === j ? { ...v, weight: Number(e.target.value) } : v))} /></label><div className="judge-actions"><button disabled={i === 0} onClick={() => reorder(i, i - 1)}>↑</button><button disabled={i === tests.length - 1} onClick={() => reorder(i, i + 1)}>↓</button></div>
            </section>)}
            <button disabled={tests.length >= 15} onClick={() => setTests([...tests, { input: '', expectedOutput: '', isSample: tests.length === 0, order: tests.length + 1, weight: 1 }])}>+ Thêm test</button>
          </div>
        </div><div className="judge-actions"><button className="judge-primary" disabled={!problemScope(form)} onClick={save}>{busy ? 'Đang lưu…' : 'Lưu bài tập và test'}</button></div></fieldset>
      </section>}</> : <>
        <div className="judge-form">{(['problemId', 'userId'] as const).map(key => <label key={key}>{key}<input value={filters[key]} onChange={e => { setFilters({ ...filters, [key]: e.target.value }); setPage(1); }} /></label>)}<label>Verdict<select value={filters.verdict} onChange={e => { setFilters({ ...filters, verdict: e.target.value }); setPage(1); }}><option value="">Tất cả</option>{['Accepted', 'WrongAnswer', 'TimeLimitExceeded', 'MemoryLimitExceeded', 'RuntimeError', 'CompileError', 'InternalError', 'Pending', 'Running'].map(v => <option key={v}>{v}</option>)}</select></label></div>
        {submissions.isError && <p>Không thể tải bài nộp. Kiểm tra bộ lọc UUID.</p>}<div className="judge-card"><table className="judge-table"><thead><tr><th>Thời gian</th><th>Kết quả</th><th>Điểm</th><th>Code</th></tr></thead><tbody>{submissions.data?.items.map(s => <tr key={s.id}><td>{new Date(s.createdAt).toLocaleString()}</td><td>{s.status} · {s.passedCount}/{s.totalCount}</td><td>{s.score}</td><td><button onClick={() => perform(async () => setSelected(await judgeApi.submission(s.id, true)))}>Xem {s.language}</button></td></tr>)}</tbody></table></div>
        {selected && <section className="judge-card"><div className="judge-actions"><strong>{selected.status} · {selected.id}</strong><button onClick={() => setSelected(undefined)}>Đóng</button></div><Editor height="450px" language={selected.language} value={selected.sourceCode} theme="vs" options={{ readOnly: true, minimap: { enabled: false } }} /></section>}
      </>}
    <div className="judge-actions"><button disabled={page === 1} onClick={() => setPage(page - 1)}>Trước</button><span>{page}</span><button disabled={page * 20 >= (total || 0)} onClick={() => setPage(page + 1)}>Sau</button></div>
  </main>;
}
