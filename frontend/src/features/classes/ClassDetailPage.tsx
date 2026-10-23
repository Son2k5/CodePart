import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { toast } from 'sonner';
import { useClassesQuery, useClassMutation, useDebounced, type ClassItem, type Page, type Student, type Teacher } from './api';
import { ConfirmDialog, Pagination, QueryState, TeacherPicker } from './ClassUi';
import { ClassProblemsPanel } from '../code-judge/ClassProblemsPanel';

export function ClassDetailPage() {
  const { id } = useParams(); const root = `/admin/classes/${id}`;
  const query = useClassesQuery<ClassItem>(root); const mutation = useClassMutation();
  const [tab, setTab] = useState('info'); const [teachers, setTeachers] = useState<Teacher[]>([]);
  const [search, setSearch] = useState(''); const [page, setPage] = useState(1); const debounced = useDebounced(search);
  const [expiry, setExpiry] = useState('');
  const [confirm, setConfirm] = useState<{ title: string; message: string; path: string; method: 'post' | 'delete'; data?: unknown } | null>(null);
  const students = useClassesQuery<Page<Student>>(`${root}/students`, { search: debounced, page, pageSize: 20 }, tab === 'students');
  const c = query.data;
  return <main className="max-w-[1250px] mx-auto p-6 text-[#24242c] font-sans [&_h1]:text-[26px] [&_h1]:font-bold [&_h1]:mb-[18px] [&_h2]:text-[20px] [&_h2]:font-semibold [&_h2]:my-3.5 [&_p]:my-3 [&_button]:border [&_button]:border-[#d4d4db] [&_button]:rounded-lg [&_button]:px-3.5 [&_button]:py-2 [&_button]:bg-white [&_button]:cursor-pointer hover:[&_button]:bg-[#f0f0f7] [&_button:disabled]:opacity-50 [&_button:disabled]:cursor-not-allowed [&_button.primary]:bg-[#4338ca] [&_button.primary]:text-white [&_button.primary]:border-[#4338ca] [&_button.danger]:text-[#b91c1c] [&_button.danger]:border-[#fca5a5] [&_input:not([type=checkbox])]:border [&_input:not([type=checkbox])]:border-[#c8c8d0] [&_input:not([type=checkbox])]:rounded-[7px] [&_input:not([type=checkbox])]:p-[9px] [&_input:not([type=checkbox])]:bg-white [&_select]:border [&_select]:border-[#c8c8d0] [&_select]:rounded-[7px] [&_select]:p-[9px] [&_select]:bg-white [&_fieldset]:border [&_fieldset]:border-[#dedee5] [&_fieldset]:rounded-[9px] [&_fieldset]:p-4 [&_fieldset]:my-4 [&_a]:text-[#4338ca] hover:[&_a]:underline [&_a.class-button]:inline-flex [&_a.class-button]:items-center [&_a.class-button]:gap-2 [&_a.class-button]:border [&_a.class-button]:border-[#d4d4db] [&_a.class-button]:rounded-lg [&_a.class-button]:px-3.5 [&_a.class-button]:py-2 [&_a.class-button]:bg-white [&_a.class-button]:no-underline hover:[&_a.class-button]:bg-[#f0f0f7]"><Link to="/admin/classes">← Danh sách lớp</Link>
    <QueryState loading={query.isLoading} error={query.error} retry={() => query.refetch()} />
    {c && <><div className="flex gap-3 items-center flex-wrap my-4"><h1>{c.name}</h1><Link className="class-button" to={`/admin/classes/${id}/edit`}>Sửa thông tin</Link></div>
      <div className="flex gap-3 items-center flex-wrap my-4 [&_button[aria-selected=true]]:bg-[#4338ca] [&_button[aria-selected=true]]:text-white" role="tablist" aria-label="Chi tiết lớp">{[['info', 'Thông tin'], ['teachers', 'Giáo viên'], ['students', 'Học sinh']].map(([key, label]) => <button id={`tab-${key}`} aria-controls="class-panel" role="tab" aria-selected={tab === key} key={key} onClick={() => setTab(key)}>{label}</button>)}</div>
      <Link className="class-button" to={`/classes/${id}/learning`}>Học liệu và trợ giảng</Link>
      <ClassProblemsPanel id={id!} kind="class" manage />
      <section id="class-panel" role="tabpanel" aria-labelledby={`tab-${tab}`}>
      {tab === 'info' && <><p>{c.description || 'Chưa có mô tả.'}</p><p><span className={`inline-block rounded-full px-2.5 py-1 text-xs font-medium ${c.accessType === 'Public' ? 'bg-[#dcfce7] text-[#166534]' : 'bg-[#e0e7ff] text-[#3730a3]'}`}>{c.accessType}</span> · {c.studentCount}/{c.maxStudents ?? '∞'} học sinh</p>
        <label className="flex gap-1.5 flex-col my-3 max-w-xs">Trạng thái<select value={c.status} disabled={mutation.isPending} onChange={e => mutation.mutate({ path: `${root}/status`, method: 'patch', data: { status: e.target.value } })}><option>Draft</option><option>Active</option><option>Archived</option></select></label>
        <p>Bắt đầu: {c.startDate ? new Date(c.startDate).toLocaleString('vi-VN') : 'Chưa đặt'} · Kết thúc: {c.endDate ? new Date(c.endDate).toLocaleString('vi-VN') : 'Chưa đặt'}</p>
        {c.accessType === 'CodeRequired' && <fieldset><legend>Mã tham gia</legend><p>{c.joinCode ?? 'Mã đã được thu hồi'}</p><p>Hết hạn: {c.joinCodeExpiresAt ? new Date(c.joinCodeExpiresAt).toLocaleString('vi-VN') : 'Không hết hạn'}</p>
          <button disabled={!c.joinCode} onClick={() => { void navigator.clipboard.writeText(c.joinCode!).then(() => toast.success('Đã sao chép mã'), () => toast.error('Không thể sao chép mã')); }}>Sao chép mã</button>
          <label className="flex gap-1.5 flex-col my-3 max-w-xs">Hạn dùng mã mới<input type="datetime-local" value={expiry} onChange={e => setExpiry(e.target.value)} /></label>
          <div className="flex gap-3 items-center flex-wrap my-4"><button disabled={mutation.isPending} onClick={() => setConfirm({ title: 'Tạo lại mã lớp', message: 'Mã cũ sẽ không còn sử dụng được. Tiếp tục?', path: `${root}/join-code/regenerate`, method: 'post', data: { expiresAt: expiry ? new Date(expiry).toISOString() : null } })}>Tạo lại mã</button>
            <button disabled={mutation.isPending || !c.joinCode} onClick={() => setConfirm({ title: 'Thu hồi mã lớp', message: 'Học sinh mới sẽ không thể tham gia bằng mã hiện tại.', path: `${root}/join-code`, method: 'delete' })}>Thu hồi mã</button></div></fieldset>}
      </>}
      {tab === 'teachers' && <><QueryState loading={false} error={null} empty={!c.teachers.length} retry={() => query.refetch()} />{c.teachers.map(t => <div className="flex gap-3 items-center flex-wrap my-4" key={t.id}><span>{t.fullName} — {t.email}</span><button disabled={mutation.isPending} onClick={() => setConfirm({ title: 'Gỡ giáo viên', message: `Gỡ ${t.fullName} khỏi lớp?`, path: `${root}/teachers/${t.id}`, method: 'delete' })}>Gỡ</button></div>)}
        <TeacherPicker value={teachers} onChange={setTeachers} /><button disabled={!teachers.length || mutation.isPending} onClick={() => mutation.mutate({ path: `${root}/teachers`, data: { teacherIds: teachers.map(t => t.id) } }, { onSuccess: () => setTeachers([]) })}>Thêm giáo viên</button></>}
      {tab === 'students' && <><input aria-label="Tìm học sinh" placeholder="Tên hoặc email học sinh" value={search} onChange={e => { setSearch(e.target.value); setPage(1); }} className="w-full max-w-md my-3" />
        <QueryState loading={students.isLoading} error={students.error} empty={students.data?.items.length === 0} retry={() => students.refetch()} />
        {students.data && <><div className="overflow-auto bg-white border border-[#e4e4e7] rounded-[10px] my-4 [&_table]:w-full [&_table]:border-collapse [&_th]:text-left [&_th]:p-3.5 [&_th]:border-b [&_th]:border-[#eee] [&_th]:whitespace-nowrap [&_td]:text-left [&_td]:p-3.5 [&_td]:border-b [&_td]:border-[#eee] [&_td]:whitespace-nowrap"><table><thead><tr><th>Học sinh</th><th>Email</th><th>Ngày tham gia</th><th>Thao tác</th></tr></thead><tbody>{students.data.items.map(s => <tr key={s.id}><td>{s.fullName}</td><td>{s.email}</td><td>{new Date(s.joinedAt).toLocaleString('vi-VN')}</td><td><button disabled={mutation.isPending} onClick={() => setConfirm({ title: 'Xóa học sinh khỏi lớp', message: `Xóa ${s.fullName} khỏi lớp?`, path: `${root}/students/${s.id}`, method: 'delete' })}>Xóa khỏi lớp</button></td></tr>)}</tbody></table></div><Pagination page={page} totalPages={students.data.totalPages} totalCount={students.data.totalCount} onChange={setPage} /></>}
      </>}
      </section></>}
    {confirm && <ConfirmDialog {...confirm} pending={mutation.isPending} onClose={() => setConfirm(null)} onConfirm={() => mutation.mutate(confirm, { onSuccess: () => setConfirm(null) })} />}
  </main>;
}
