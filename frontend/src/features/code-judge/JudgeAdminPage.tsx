import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useSearchParams } from 'react-router-dom';
import Editor from '@monaco-editor/react';
import { toast } from 'sonner';
import { judgeApi, judgeError, problemScope, scopeParams, type Problem, type Test, type Submission } from './api';
import { problemCategories } from './problemCategories';
import { ScopePicker } from './ScopePicker';

const empty: Partial<Problem> = { title: '', description: '', category: 'general', difficulty: 'Easy', timeLimitMs: 2000, memoryLimitMb: 256, points: 100, comparisonMode: 0, courseId: null,
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
  return <main className="min-h-screen bg-[#fafaf7] p-4 md:p-6 text-[#334155] font-sans [&_h1]:text-[26px] [&_h1]:font-bold [&_h1]:my-4 [&_h3]:text-[17px] [&_h3]:font-semibold [&_h3]:mt-[18px] [&_h3]:mb-[10px] [&_button]:px-[14px] [&_button]:py-[9px] [&_button]:border [&_button]:border-[#e2e8f0] [&_button]:rounded-[10px] [&_button]:bg-white [&_button]:cursor-pointer [&_button]:inline-flex [&_button]:items-center [&_button]:justify-center [&_button]:gap-2 [&_button[aria-pressed=true]]:bg-[#eef2ff] [&_button[aria-pressed=true]]:text-[#6366b5] [&_button[aria-pressed=true]]:border-[#c7d2fe] [&_button:disabled]:opacity-50 [&_button:disabled]:cursor-not-allowed [&_select]:px-[14px] [&_select]:py-[9px] [&_select]:border [&_select]:border-[#e2e8f0] [&_select]:rounded-[10px] [&_select]:bg-white [&_select]:cursor-pointer [&_label]:text-[#64748b] [&_label]:text-[13px] [&_textarea]:border [&_textarea]:border-[#e2e8f0] [&_textarea]:rounded-[10px] [&_textarea]:p-[10px] [&_textarea]:w-full [&_textarea]:bg-white [&_textarea]:min-h-[100px] [&_textarea]:font-mono [&_input:not([type=checkbox])]:border [&_input:not([type=checkbox])]:border-[#e2e8f0] [&_input:not([type=checkbox])]:rounded-[10px] [&_input:not([type=checkbox])]:p-[10px] [&_input:not([type=checkbox])]:w-full [&_input:not([type=checkbox])]:bg-white"><Link to="/dashboard" className="text-[#6366b5] hover:underline font-medium">← Trang quản lý</Link><h1>Quản lý bài tập lập trình</h1>
    <p className="text-[#64748b] text-[13px] mb-4">Tạo bài cho thư viện luyện tập chung hoặc giao riêng cho lớp. Thêm đề, phân loại và bộ test trước khi xuất bản.</p>
    <ScopePicker scopes={scopes.data || []} value={scope} all disabled={busy} onChange={v => { setSearch(v ? { scope: v } : {}); setPage(1); setForm(undefined); setSelected(undefined); }} />
    {scopes.isError && <p className="text-[#b75d57] my-2">Không tải được danh sách lớp. <button onClick={() => scopes.refetch()}>Thử lại</button></p>}
    <nav className="flex items-center gap-[14px] px-[18px] py-3 border-b border-[#edf0f5] my-4"><button aria-pressed={tab === 'problems'} onClick={() => { setTab('problems'); setPage(1); }}>Đề bài</button><button aria-pressed={tab === 'submissions'} onClick={() => { setTab('submissions'); setPage(1); }}>Bài nộp học sinh</button></nav>
    {tab === 'problems' ? <><div className="flex items-center gap-[14px] justify-end p-[14px]"><button disabled={busy} onClick={() => { setForm({ ...structuredClone(empty), classId: null, ...scopeParams(scope) }); setTests([]); }}>+ Tạo bài tập</button></div>
      {problems.isError && <p className="text-[#b75d57] my-2">Không tải được danh sách đề bài.</p>}{problems.data?.items.map(p => <div className="bg-white border border-[#e2e8f0] rounded-[20px] shadow-[0_3px_14px_#33415506] flex justify-between items-center p-[22px] my-3" key={p.id}><div><strong>{p.title}</strong><p className="text-[#64748b] text-[13px] mt-1">{scopes.data?.find(s => `${s.kind}:${s.id}` === problemScope(p))?.name || 'Thư viện luyện tập chung'} · {p.status === 'Published' ? 'Đã đăng' : 'Bản nháp'} · {p.difficulty}</p></div><div className="flex items-center gap-[14px] justify-end"><button disabled={busy} onClick={() => edit(p.id)}>Mở</button><button disabled={busy} onClick={() => perform(async () => { await judgeApi.action(p.id, p.status === 'Published' ? 'unpublish' : 'publish'); if (form?.id === p.id) setForm(undefined); })}>{p.status === 'Published' ? 'Về nháp' : 'Xuất bản'}</button><button disabled={busy} onClick={() => { if (confirm('Ẩn bài tập này?')) void perform(async () => { await judgeApi.action(p.id, 'delete'); if (form?.id === p.id) setForm(undefined); }); }}>Xóa</button></div></div>)}
      {form && <section className="bg-white border border-[#e2e8f0] rounded-[20px] shadow-[0_3px_14px_#33415506] p-6 my-4"><div className="flex items-center justify-between gap-[14px] mb-4"><strong>{form.id ? 'Chỉnh sửa bài tập' : 'Bài tập mới'}</strong><button onClick={() => setForm(undefined)}>Đóng</button></div>{form.status === 'Published' && <p className="text-[#b75d57] font-medium my-2">Chuyển về nháp trước khi chỉnh sửa.</p>}
        <fieldset disabled={busy || form.status === 'Published'}><div className="grid grid-cols-1 md:grid-cols-2 gap-4 my-4 [&>label]:grid [&>label]:gap-2">
          <label>Tiêu đề<input value={form.title} onChange={e => setForm({ ...form, title: e.target.value })} /></label>
          <label>Nơi đăng bài tập<ScopePicker scopes={scopes.data || []} value={problemScope(form)} disabled={!!form.id} onChange={v => setForm({ ...form, courseId: null, classId: null, ...scopeParams(v) })} /></label>
          <label>Dạng bài<select value={form.category || 'general'} onChange={e => setForm({ ...form, category: e.target.value })}>{problemCategories.map(c => <option key={c.id} value={c.id}>{c.label}</option>)}</select></label>
          <label>Độ khó<select value={form.difficulty} onChange={e => setForm({ ...form, difficulty: e.target.value })}>{['Easy', 'Medium', 'Hard'].map(d => <option key={d}>{d}</option>)}</select></label>
          <label>So sánh output<select value={form.comparisonMode} onChange={e => setForm({ ...form, comparisonMode: Number(e.target.value) })}><option value={0}>Theo dòng</option><option value={1}>Theo token</option></select></label>
          {(['timeLimitMs', 'memoryLimitMb', 'points'] as const).map((field, i) => <label key={field}>{['Thời gian (ms)', 'Bộ nhớ (MB)', 'Điểm'][i]}<input type="number" value={form[field]} onChange={e => setForm({ ...form, [field]: Number(e.target.value) })} /></label>)}
          <label className="col-span-full">Mô tả (Markdown)<textarea value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} /></label>
          {form.starterCodes?.map((starter, i) => <label key={starter.language}>Code mẫu · {starter.language}<textarea value={starter.template} onChange={e => setForm({ ...form, starterCodes: form.starterCodes?.map((s, j) => i === j ? { ...s, template: e.target.value } : s) })} /></label>)}
          <div className="col-span-full"><h3>Test cases · {tests.length}/15</h3><p className="text-[#64748b] text-[13px] mb-2">Xuất bản cần 10–15 test, trong đó có 1–2 sample. Kéo thả để đổi thứ tự.</p><label className="mb-3">Import JSON<input type="file" accept="application/json,.json" onChange={e => { void readImport(e.target.files?.[0]); e.target.value = ''; }} /></label>
            {tests.map((t, i) => <section key={i} className="bg-white border border-[#e2e8f0] rounded-[14px] p-[18px] my-3" draggable onDragStart={() => setDrag(i)} onDragOver={e => e.preventDefault()} onDrop={() => { if (drag !== undefined) reorder(drag, i); setDrag(undefined); }}>
              <div className="flex items-center justify-between gap-[14px] mb-3"><strong>#{i + 1}</strong><label className="inline-flex items-center gap-2"><input type="checkbox" checked={t.isSample} onChange={e => setTests(tests.map((v, j) => i === j ? { ...v, isSample: e.target.checked } : v))} /> Sample (hiển thị cho học sinh)</label><button onClick={() => setTests(tests.filter((_, j) => i !== j))}>Xóa</button></div>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-[14px] my-2">{(['input', 'expectedOutput'] as const).map(field => <label key={field} className="grid gap-1">{field}<textarea value={t[field]} onChange={e => setTests(tests.map((v, j) => i === j ? { ...v, [field]: e.target.value } : v))} /></label>)}</div>
              <label className="grid gap-1 mt-2">Trọng số<input type="number" min="1" max="1000" value={t.weight} onChange={e => setTests(tests.map((v, j) => i === j ? { ...v, weight: Number(e.target.value) } : v))} /></label><div className="flex items-center gap-2 justify-end mt-2"><button disabled={i === 0} onClick={() => reorder(i, i - 1)}>↑</button><button disabled={i === tests.length - 1} onClick={() => reorder(i, i + 1)}>↓</button></div>
            </section>)}
            <button disabled={tests.length >= 15} onClick={() => setTests([...tests, { input: '', expectedOutput: '', isSample: tests.length === 0, order: tests.length + 1, weight: 1 }])} className="mt-2">+ Thêm test</button>
          </div>
        </div><div className="flex items-center gap-[14px] justify-end p-[14px]"><button className="bg-[#818cf8] text-white border-[#818cf8] hover:bg-[#6366f1]" disabled={busy} onClick={save}>{busy ? 'Đang lưu…' : 'Lưu bài tập và test'}</button></div></fieldset>
      </section>}</> : <>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 my-4 [&>label]:grid [&>label]:gap-2">{(['problemId', 'userId'] as const).map(key => <label key={key}>{key}<input value={filters[key]} onChange={e => { setFilters({ ...filters, [key]: e.target.value }); setPage(1); }} /></label>)}<label>Verdict<select value={filters.verdict} onChange={e => { setFilters({ ...filters, verdict: e.target.value }); setPage(1); }}><option value="">Tất cả</option>{['Accepted', 'WrongAnswer', 'TimeLimitExceeded', 'MemoryLimitExceeded', 'RuntimeError', 'CompileError', 'InternalError', 'Pending', 'Running'].map(v => <option key={v}>{v}</option>)}</select></label></div>
        {submissions.isError && <p className="text-[#b75d57] my-2">Không thể tải bài nộp. Kiểm tra bộ lọc UUID.</p>}<div className="bg-white border border-[#e2e8f0] rounded-[20px] shadow-[0_3px_14px_#33415506] overflow-x-auto my-4"><table className="w-full border-collapse [&_th]:text-left [&_th]:p-[14px] [&_th]:border-b [&_th]:border-[#e2e8f0] [&_td]:text-left [&_td]:p-[14px] [&_td]:border-b [&_td]:border-[#e2e8f0]"><thead><tr><th>Thời gian</th><th>Kết quả</th><th>Điểm</th><th>Code</th></tr></thead><tbody>{submissions.data?.items.map(s => <tr key={s.id}><td>{new Date(s.createdAt).toLocaleString()}</td><td>{s.status} · {s.passedCount}/{s.totalCount}</td><td>{s.score}</td><td><button onClick={() => perform(async () => setSelected(await judgeApi.submission(s.id, true)))}>Xem {s.language}</button></td></tr>)}</tbody></table></div>
        {selected && <section className="bg-white border border-[#e2e8f0] rounded-[20px] shadow-[0_3px_14px_#33415506] p-6 my-4"><div className="flex items-center justify-between gap-[14px] mb-4"><strong>{selected.status} · {selected.id}</strong><button onClick={() => setSelected(undefined)}>Đóng</button></div><Editor height="450px" language={selected.language} value={selected.sourceCode} theme="vs" options={{ readOnly: true, minimap: { enabled: false } }} /></section>}
      </>}
    <div className="flex items-center gap-[14px] justify-end p-[14px]"><button disabled={page === 1} onClick={() => setPage(page - 1)}>Trước</button><span className="text-sm font-semibold">{page}</span><button disabled={page * 20 >= (total || 0)} onClick={() => setPage(page + 1)}>Sau</button></div>
  </main>;
}
