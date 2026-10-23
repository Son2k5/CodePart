import type { JudgeScope } from './api';
export function ScopePicker({ scopes, value, onChange, disabled = false, all = false }: { scopes: JudgeScope[]; value: string; onChange: (v: string) => void; disabled?: boolean; all?: boolean }) {
  return <select aria-label="Chọn lớp" value={value} disabled={disabled} onChange={e => onChange(e.target.value)}>
    <option value="">{all ? 'Tất cả bài tập' : 'Thư viện luyện tập chung'}</option>
    {(['class', 'course'] as const).map(kind => <optgroup key={kind} label={kind === 'class' ? 'Lớp' : 'Lớp học phần'}>{scopes.filter(s => s.kind === kind).map(s => <option key={s.id} value={`${kind}:${s.id}`}>{s.name}</option>)}</optgroup>)}
  </select>;
}
