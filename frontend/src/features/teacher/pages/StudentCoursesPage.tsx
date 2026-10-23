import { useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { usePortalQuery, usePortalMutation, type Course, type CourseExercise } from '../api/teacherApi';
import { Empty, PortalError } from '../components/PortalUi';
import { useLogout } from '../../auth/hooks/useAuth';
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
  return <main className="p-4 sm:p-8 max-w-[1150px] mx-auto text-sm text-[#202d42] [&_button]:inline-flex [&_button]:items-center [&_button]:justify-center [&_button]:gap-2 [&_button]:bg-white [&_button]:border [&_button]:border-[#dbe3f0] [&_button]:rounded-lg [&_button]:px-[14px] [&_button]:py-[9px] [&_button]:cursor-pointer [&_button]:font-medium [&_button:disabled]:opacity-50 [&_button:disabled]:cursor-not-allowed [&_button.primary]:bg-[#285ec8] [&_button.primary]:text-white [&_button.primary]:border-[#285ec8] [&_.portal-heading]:flex [&_.portal-heading]:justify-between [&_.portal-heading]:items-center [&_.portal-heading]:max-[800px]:items-start [&_.portal-heading]:max-[800px]:flex-wrap [&_.portal-heading]:gap-5 [&_.portal-heading]:mb-[30px] [&_.portal-heading_p]:text-[10px] [&_.portal-heading_p]:tracking-[0.15em] [&_.portal-heading_p]:font-bold [&_.portal-heading_p]:text-[#6983b3] [&_.portal-heading_p]:mb-2 [&_.portal-heading_h1]:text-[24px] min-[801px]:[&_.portal-heading_h1]:text-[28px] [&_.portal-heading_h1]:font-bold [&_.portal-heading_h1]:tracking-[-0.7px] [&_.portal-heading_h1]:my-0 [&_.portal-heading_h1]:mb-2 [&_.portal-heading_span]:text-[#6e7e95] [&_.portal-card]:bg-white [&_.portal-card]:border [&_.portal-card]:border-[#e1e7f0] [&_.portal-card]:rounded-[14px] [&_.portal-card]:p-[18px] min-[801px]:[&_.portal-card]:p-6 [&_.portal-card]:mb-5 [&_.portal-card_h2]:text-[18px] [&_.portal-card_h2]:font-semibold [&_.portal-card_h2]:mb-3.5 [&_.portal-card_p]:text-[#6e7e95] [&_.portal-courses]:grid [&_.portal-courses]:grid-cols-[repeat(auto-fill,minmax(270px,1fr))] [&_.portal-courses]:gap-5 [&_.portal-form]:max-w-[800px] [&_.portal-form]:grid [&_.portal-form]:gap-[17px] [&_.portal-form_label]:grid [&_.portal-form_label]:gap-[7px] [&_.portal-form_label]:text-xs [&_.portal-form_label]:font-semibold [&_.portal-form_label]:text-[#566780] [&_.portal-form_input]:border [&_.portal-form_input]:border-[#d8e1ef] [&_.portal-form_input]:rounded-lg [&_.portal-form_input]:bg-white [&_.portal-form_input]:px-3 [&_.portal-form_input]:py-2.5 [&_.portal-form_input]:text-[#202d42] [&_.portal-form_input]:w-full [&_.portal-exercises]:grid [&_.portal-exercises]:gap-3 [&_.portal-exercises_a]:flex [&_.portal-exercises_a]:justify-between [&_.portal-exercises_a]:gap-3.5 [&_.portal-exercises_a]:p-4 [&_.portal-exercises_a]:border [&_.portal-exercises_a]:border-[#e1e7f0] [&_.portal-exercises_a]:rounded-[9px] [&_.portal-exercises_a]:no-underline [&_.portal-exercises_a]:text-[#285ec8] [&_.portal-exercises_small]:text-[#7a879b]"><header className="portal-heading"><div><p>CODEPART · HỌC TẬP</p><h1>Lớp học phần của tôi</h1><span>Tham gia lớp bằng mã giảng viên cung cấp và bắt đầu làm bài.</span></div><button disabled={logout.isPending} onClick={() => logout.mutate()}>Đăng xuất</button></header>
    <form className="portal-card portal-form" onSubmit={submit}><label>Mã tham gia lớp<input maxLength={20} required value={code} onChange={e => setCode(e.target.value)} autoComplete="off" /></label><PortalError error={join.error} /><button className="primary" disabled={join.isPending}>Tham gia lớp</button></form>
    <div className="flex gap-4 my-3 text-sm font-semibold text-[#285ec8] [&>a]:hover:underline"><Link to="/classes">Khám phá lớp public / Tham gia bằng mã lớp</Link><Link to="/problems">Luyện tập Code Judge →</Link></div>
    <PortalError error={courses.error} retry={() => void courses.refetch()} />
    {courses.isPending ? <p role="status">Đang tải lớp…</p> : courses.data?.length === 0 ? <Empty>Bạn chưa tham gia lớp học phần nào.</Empty> : <div className="portal-courses">{courses.data?.map(c => <section className="portal-card" key={c.id}><h2>{c.subjectName}</h2><p>{c.sectionCode} · {c.semester}</p><p>{c.exerciseCount} bài tập · {c.status === 'Closed' ? 'Đã đóng lớp' : 'Đang mở'}</p><button onClick={() => setSelected(c.id)}>Xem bài tập</button></section>)}</div>}
    {selected && <ClassProblemsPanel id={selected} kind="course" />}
    {selected && !!exercises.data?.length && <section className="portal-card"><h2>Bài tập trước đây · {courses.data?.find(c => c.id === selected)?.subjectName}</h2><PortalError error={exercises.error} retry={() => void exercises.refetch()} />
      {exercises.isPending ? <p>Đang tải bài tập…</p> : exercises.data?.length === 0 ? <Empty>Giảng viên chưa giao bài tập.</Empty> : <div className="portal-exercises">{exercises.data?.map(e => <Link to={`/exercise/${e.slug}`} key={e.id}>{e.title}<small>{e.difficulty} →</small></Link>)}</div>}
    </section>}
  </main>;
}
