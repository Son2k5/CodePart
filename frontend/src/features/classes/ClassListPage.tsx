import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useClassesQuery, useClassMutation, useDebounced, type ClassItem, type Page } from './api';
import { ConfirmDialog, Pagination, QueryState } from './ClassUi';
import './classes.css';

export function ClassListPage() {
  const [search, setSearch] = useState(''); const [page, setPage] = useState(1);
  const [status, setStatus] = useState(''); const [accessType, setAccess] = useState(''); const [hasTeacher, setHasTeacher] = useState('');
  const [sort, setSort] = useState('createdAt'); const [desc, setDesc] = useState(true); const [deleting, setDeleting] = useState<ClassItem | null>(null);
  const debounced = useDebounced(search); const mutation = useClassMutation();
  const query = useClassesQuery<Page<ClassItem>>('/admin/classes', { page, pageSize: 20, search: debounced, status: status || undefined, accessType: accessType || undefined, hasTeacher: hasTeacher || undefined, sort, desc });
  return <main className="classes-page"><div className="class-toolbar"><h1>Quản lý lớp học</h1><Link className="class-button" to="/admin/classes/new">+ Tạo lớp</Link></div>
    <div className="class-toolbar"><input aria-label="Tìm lớp" placeholder="Tìm theo tên hoặc mã lớp" value={search} onChange={e => { setSearch(e.target.value); setPage(1); }} />
      <select aria-label="Trạng thái" value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}><option value="">Mọi trạng thái</option><option>Draft</option><option>Active</option><option>Archived</option></select>
      <select aria-label="Quyền tham gia" value={accessType} onChange={e => { setAccess(e.target.value); setPage(1); }}><option value="">Mọi quyền tham gia</option><option>Public</option><option>CodeRequired</option></select>
      <select aria-label="Phân công giáo viên" value={hasTeacher} onChange={e => { setHasTeacher(e.target.value); setPage(1); }}><option value="">Mọi phân công</option><option value="true">Có giáo viên</option><option value="false">Chưa có giáo viên</option></select>
      <select aria-label="Sắp xếp" value={sort} onChange={e => { setSort(e.target.value); setPage(1); }}><option value="createdAt">Ngày tạo</option><option value="name">Tên lớp</option><option value="studentCount">Số học sinh</option></select>
      <button onClick={() => { setDesc(!desc); setPage(1); }}>{desc ? 'Giảm dần ↓' : 'Tăng dần ↑'}</button>
    </div><QueryState loading={query.isLoading} error={query.error} empty={query.data?.items.length === 0} retry={() => query.refetch()} />
    {query.data && <><div className="class-table-wrap"><table><thead><tr><th>Lớp học</th><th>Quyền tham gia</th><th>Trạng thái</th><th>Học sinh</th><th>Giáo viên</th><th>Thao tác</th></tr></thead>
      <tbody>{query.data.items.map(c => <tr key={c.id}><td><Link to={`/admin/classes/${c.id}`}>{c.name}</Link></td><td><span className={`class-badge ${c.accessType === 'Public' ? 'public' : ''}`}>{c.accessType === 'Public' ? 'Public' : 'Code'}</span></td><td>{c.status}</td><td>{c.studentCount}/{c.maxStudents ?? '∞'}</td><td>{c.teachers.map(t => t.fullName).join(', ') || 'Chưa phân công'}</td><td><Link to={`/admin/classes/${c.id}/edit`}>Sửa</Link> <button className="danger" onClick={() => setDeleting(c)}>Xóa</button></td></tr>)}</tbody></table></div>
      <Pagination page={page} totalPages={query.data.totalPages} totalCount={query.data.totalCount} onChange={setPage} /></>}
    {deleting && <ConfirmDialog title="Xóa lớp học" message={deleting.studentCount > 0 ? `Lớp ${deleting.name} còn ${deleting.studentCount} học sinh. Xác nhận sẽ xóa mềm lớp và gỡ toàn bộ học sinh đang tham gia.` : `Xác nhận xóa lớp ${deleting.name}?`} pending={mutation.isPending} onClose={() => setDeleting(null)} onConfirm={() => mutation.mutate({ path: `/admin/classes/${deleting.id}?force=${deleting.studentCount > 0}&confirm=true`, method: 'delete' }, { onSuccess: () => setDeleting(null) })} />}
  </main>;
}
