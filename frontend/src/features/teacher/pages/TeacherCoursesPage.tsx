import { useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowUpRight, Users, BookOpen } from 'lucide-react';
import { usePortalQuery, type Course } from '../api/teacherApi';
import { Empty, PortalError } from '../components/PortalUi';

export function TeacherCoursesPage() {
  const courses = usePortalQuery<Course[]>('/teacher/courses');
  const [search, setSearch] = useState('');
  const items = (courses.data ?? []).filter(c => `${c.subjectName} ${c.sectionCode} ${c.semester}`.toLocaleLowerCase().includes(search.toLocaleLowerCase()));
  return <>
    <header className="portal-heading"><div><p>GIẢNG DẠY</p><h1>Lớp học phần</h1><span>Các lớp học phần được phân công cho bạn. Việc tạo lớp do quản trị viên phụ trách.</span></div></header>
    <div className="portal-stats"><div><span>Lớp phụ trách</span><strong>{courses.data?.length ?? '—'}</strong></div><div><span>Đang mở</span><strong>{courses.data?.filter(c => c.status === 'Active').length ?? '—'}</strong></div><div><span>Lượt ghi danh</span><strong>{courses.data?.reduce((n, c) => n + c.enrolledCount, 0) ?? '—'}</strong></div></div>
    <label className="portal-search">Tìm lớp<input value={search} onChange={e => setSearch(e.target.value)} placeholder="Tên môn, mã lớp hoặc học kỳ" /></label>
    <PortalError error={courses.error} retry={() => void courses.refetch()} />
    {courses.isPending ? <p role="status">Đang tải lớp học phần…</p> : !courses.error && !items.length ? <Empty>Chưa có lớp phù hợp. Liên hệ quản trị viên để được phân công lớp.</Empty> : null}
    <div className="portal-courses">{items.map(c => <Link className="portal-card course-card" key={c.id} to={`/teacher/courses/${c.id}`}><div><span className={`portal-badge ${c.status}`}>{c.status === 'Draft' ? 'Bản nháp' : c.status === 'Active' ? 'Đang mở' : 'Đã đóng'}</span><ArrowUpRight size={18} /></div><h2>{c.subjectName}</h2><p>{c.sectionCode} · {c.semester}</p><footer><span><Users size={15} />{c.enrolledCount}/{c.capacity}</span><span><BookOpen size={15} />{c.exerciseCount} bài tập</span><span>{c.language}</span></footer></Link>)}</div>
  </>;
}
