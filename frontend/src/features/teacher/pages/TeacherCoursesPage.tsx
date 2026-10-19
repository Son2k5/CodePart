import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Plus, ArrowUpRight, Users, BookOpen } from 'lucide-react';
import { usePortalQuery, usePortalMutation, type Course } from '../api/teacherApi';
import { Empty, PortalError } from '../components/PortalUi';

export function TeacherCoursesPage() {
  const courses = usePortalQuery<Course[]>('/teacher/courses');
  const create = usePortalMutation();
  const navigate = useNavigate();
  const [showForm, setShowForm] = useState(false);
  const [search, setSearch] = useState('');
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const data = Object.fromEntries(new FormData(e.currentTarget));
    try {
      const result = await create.mutateAsync({ path: '/teacher/courses', data: { ...data, capacity: Number(data.capacity) } });
      navigate(`/teacher/courses/${result.id}`);
    } catch { /* Mutation error is shown in the form. */ }
  }
  const items = (courses.data ?? []).filter(c => `${c.subjectName} ${c.sectionCode} ${c.semester}`.toLocaleLowerCase().includes(search.toLocaleLowerCase()));
  return <>
    <header className="portal-heading"><div><p>GIẢNG DẠY</p><h1>Lớp học phần</h1><span>Tổ chức lớp, giao bài tập và theo dõi kết quả học tập.</span></div><button className="primary" onClick={() => setShowForm(!showForm)}><Plus size={17} />Tạo lớp học phần</button></header>
    {showForm && <form className="portal-card portal-form" onSubmit={submit}><h2>Lớp học phần mới</h2>
      <label>Tên môn học<input name="subjectName" required maxLength={200} /></label>
      <div className="portal-two"><label>Mã lớp học phần<input name="sectionCode" required maxLength={50} /></label><label>Học kỳ<input name="semester" required maxLength={50} placeholder="HK1-2026" /></label></div>
      <div className="portal-two"><label>Ngôn ngữ<select name="language"><option value="python">Python</option><option value="cpp">C++</option><option value="java">Java</option><option value="javascript">JavaScript</option><option value="csharp">C#</option></select></label><label>Sĩ số tối đa<input name="capacity" type="number" min={1} max={1000} defaultValue={60} required /></label></div>
      <p>Lớp mới ở trạng thái bản nháp. Mở lớp khi sẵn sàng nhận sinh viên.</p><PortalError error={create.error} /><button className="primary" disabled={create.isPending}>{create.isPending ? 'Đang tạo…' : 'Tạo lớp'}</button>
    </form>}
    <div className="portal-stats"><div><span>Lớp phụ trách</span><strong>{courses.data?.length ?? '—'}</strong></div><div><span>Đang mở</span><strong>{courses.data?.filter(c => c.status === 'Active').length ?? '—'}</strong></div><div><span>Lượt ghi danh</span><strong>{courses.data?.reduce((n, c) => n + c.enrolledCount, 0) ?? '—'}</strong></div></div>
    <label className="portal-search">Tìm lớp<input value={search} onChange={e => setSearch(e.target.value)} placeholder="Tên môn, mã lớp hoặc học kỳ" /></label>
    <PortalError error={courses.error} retry={() => void courses.refetch()} />
    {courses.isPending ? <p role="status">Đang tải lớp học phần…</p> : !courses.error && !items.length ? <Empty>Chưa có lớp phù hợp. Tạo lớp mới để bắt đầu.</Empty> : null}
    <div className="portal-courses">{items.map(c => <Link className="portal-card course-card" key={c.id} to={`/teacher/courses/${c.id}`}><div><span className={`portal-badge ${c.status}`}>{c.status === 'Draft' ? 'Bản nháp' : c.status === 'Active' ? 'Đang mở' : 'Đã đóng'}</span><ArrowUpRight size={18} /></div><h2>{c.subjectName}</h2><p>{c.sectionCode} · {c.semester}</p><footer><span><Users size={15} />{c.enrolledCount}/{c.capacity}</span><span><BookOpen size={15} />{c.exerciseCount} bài tập</span><span>{c.language}</span></footer></Link>)}</div>
  </>;
}
