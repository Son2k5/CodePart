import { useState, type FormEvent } from 'react';
import { usePortalMutation, type ExerciseInput, type TestInput } from '../api/teacherApi';
import { PortalError } from './PortalUi';

const blankTest = (): TestInput => ({ input: '', expectedOutput: '', isSample: true, explanation: '' });
export function ExerciseForm({ courseId, onSaved }: { courseId: string; onSaved: () => void }) {
  const mutation = usePortalMutation();
  const [tests, setTests] = useState<TestInput[]>([blankTest()]);
  function update(index: number, patch: Partial<TestInput>) { setTests(current => current.map((t, i) => i === index ? { ...t, ...patch } : t)); }
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const fields = Object.fromEntries(new FormData(e.currentTarget));
    const data: ExerciseInput = { title: String(fields.title), difficulty: String(fields.difficulty), description: String(fields.description),
      constraints: String(fields.constraints), starterCode: String(fields.starterCode), timeLimitMs: Number(fields.timeLimitMs), memoryLimitKb: Number(fields.memoryLimitKb), testCases: tests };
    try { await mutation.mutateAsync({ path: `/teacher/courses/${courseId}/exercises`, data }); onSaved(); } catch { /* Keep the draft on error. */ }
  }
  return <form className="portal-form" onSubmit={submit}>
    <label>Tên bài tập<input name="title" required maxLength={250} /></label>
    <label>Độ khó<select name="difficulty"><option>Easy</option><option>Medium</option><option>Hard</option></select></label>
    <label>Đề bài (văn bản thuần)<textarea name="description" required maxLength={10000} rows={6} /></label>
    <label>Ràng buộc dữ liệu<textarea name="constraints" maxLength={10000} /></label>
    <label>Mã khởi đầu<textarea name="starterCode" maxLength={10000} spellCheck={false} /></label>
    <div className="portal-two"><label>Giới hạn thời gian (ms)<input name="timeLimitMs" type="number" min={100} max={15000} defaultValue={1000} required /></label><label>Bộ nhớ (KB)<input name="memoryLimitKb" type="number" min={16384} max={1048576} defaultValue={65536} required /></label></div>
    <h3>Bộ kiểm thử</h3><p>Cần ít nhất một test mẫu. Test ẩn chỉ hiển thị kết quả đạt/trượt cho sinh viên.</p>
    {tests.map((test, i) => <fieldset key={i}><legend>Test {i + 1}</legend>
      <div className="portal-two"><label>Đầu vào<textarea required maxLength={10000} value={test.input} onChange={e => update(i, { input: e.target.value })} /></label><label>Kết quả mong đợi<textarea required maxLength={10000} value={test.expectedOutput} onChange={e => update(i, { expectedOutput: e.target.value })} /></label></div>
      <label className="check"><input type="checkbox" checked={test.isSample} onChange={e => update(i, { isSample: e.target.checked })} />Test mẫu công khai</label>
      <label>Giải thích<input maxLength={2000} value={test.explanation} onChange={e => update(i, { explanation: e.target.value })} /></label>
      {tests.length > 1 && <button type="button" onClick={() => setTests(current => current.filter((_, n) => n !== i))}>Xóa test này</button>}
    </fieldset>)}
    <button type="button" disabled={tests.length >= 50} onClick={() => setTests(current => [...current, { ...blankTest(), isSample: false }])}>Thêm test ẩn</button>
    <PortalError error={mutation.error} /><div className="portal-actions"><button className="primary" disabled={mutation.isPending}>{mutation.isPending ? 'Đang giao bài…' : 'Giao bài cho lớp'}</button><button type="button" onClick={onSaved} disabled={mutation.isPending}>Hủy</button></div>
  </form>;
}
