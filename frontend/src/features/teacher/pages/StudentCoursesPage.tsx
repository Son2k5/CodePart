import { useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { usePortalQuery, usePortalMutation, type Course, type CourseExercise } from '../api/teacherApi';
import { Empty, PortalError } from '../components/PortalUi';
import { useLogout } from '../../auth/hooks/useAuth';
import '../styles/teacher.css';
import { ClassProblemsPanel } from '../../code-judge/ClassProblemsPanel';

export function StudentCoursesPage() {
  const courses = usePortalQuery<Course[]>('/student/courses');
  const [selected, setSelected] = useState<string | null>(null);
  const [code, setCode] = useState('');
  const exercises = usePortalQuery<CourseExercise[]>(selected ? `/student/courses/${selected}/exercises` : null);
  const join = usePortalMutation();
  const logout = useLogout();
  async function submit(e: FormEvent) {
    e.preventDefault();
    try { const result = await join.mutateAsync({ path: '/student/courses/join', data: { joinCode: code.trim() } }); setCode(''); setSelected(result); } catch { /* Show server error and retain code. */ }
  }
  return <main className="student-portal"><header className="portal-heading"><div><p>CODEPART · HỌC TẬP</p><h1>Lớp học phần của tôi</h1><span>Tham gia lớp bằng mã giảng viên cung cấp và bắt đầu làm bài.</span></div><button disabled={logout.isPending} onClick={() => logout.mutate()}>Đăng xuất</button></header>
    <form className="portal-card portal-form" onSubmit={submit}><label>Mã tham gia lớp<input maxLength={20} required value={code} onChange={e => setCode(e.target.value)} autoComplete="off" /></label><PortalError error={join.error} /><button className="primary" disabled={join.isPending}>Tham gia lớp</button></form>
    <Link to="/classes">Khám phá lớp public / Tham gia bằng mã lớp</Link>
    <Link to="/problems">Luyện tập Code Judge →</Link>
    <PortalError error={courses.error} retry={() => void courses.refetch()} />
    {courses.isPending ? <p role="status">Đang tải lớp…</p> : courses.data?.length === 0 ? <Empty>Bạn chưa tham gia lớp học phần nào.</Empty> : <div className="portal-courses">{courses.data?.map(c => <section className="portal-card" key={c.id}><h2>{c.subjectName}</h2><p>{c.sectionCode} · {c.semester}</p><p>{c.exerciseCount} bài tập · {c.status === 'Closed' ? 'Đã đóng lớp' : 'Đang mở'}</p><button onClick={() => setSelected(c.id)}>Xem bài tập</button></section>)}</div>}
    {selected && <ClassProblemsPanel id={selected} kind="course" />}
    {selected && !!exercises.data?.length && <section className="portal-card"><h2>Bài tập trước đây · {courses.data?.find(c => c.id === selected)?.subjectName}</h2><PortalError error={exercises.error} retry={() => void exercises.refetch()} />
      {exercises.isPending ? <p>Đang tải bài tập…</p> : exercises.data?.length === 0 ? <Empty>Giảng viên chưa giao bài tập.</Empty> : <div className="portal-exercises">{exercises.data?.map(e => <Link to={`/exercise/${e.slug}`} key={e.id}>{e.title}<small>{e.difficulty} →</small></Link>)}</div>}
    </section>}
  </main>;
}
