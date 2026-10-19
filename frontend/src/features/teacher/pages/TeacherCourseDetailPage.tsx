import { useState, type FormEvent } from 'react';
import { Link, NavLink, useLocation, useParams } from 'react-router-dom';
import { toast } from 'sonner';
import { usePortalQuery, usePortalMutation, type Course, type Student, type CourseExercise } from '../api/teacherApi';
import { Empty, PortalError, StudentTable } from '../components/PortalUi';
import { ClassProblemsPanel } from '../../code-judge/ClassProblemsPanel';
import { TeacherSubmissionsPage } from './TeacherSubmissionsPage';

function ExercisePreview({ slug }: { slug: string }) {
  const query = usePortalQuery<{ title: string; description: string; constraints: string; starterCode: string; sampleTestCases: { id: string; input: string; expectedOutput: string }[] }>(`/exercises/${slug}`);
  return <div className="portal-card"><PortalError error={query.error} />{query.isPending && <p>Đang tải đề bài…</p>}{query.data && <><h3>{query.data.title}</h3><p style={{ whiteSpace: 'pre-wrap' }}>{query.data.description}</p><h3>Ràng buộc</h3><p>{query.data.constraints || 'Không có'}</p><h3>Mã khởi đầu</h3><pre className="portal-code">{query.data.starterCode}</pre>{query.data.sampleTestCases.map(t => <div className="portal-test" key={t.id}><pre>{t.input}</pre><pre>{t.expectedOutput}</pre></div>)}</>}</div>;
}
export function TeacherCourseDetailPage() {
  const { id = '' } = useParams();
  const location = useLocation();
  const submissionsTab = location.pathname.endsWith('/submissions');
  const course = usePortalQuery<Course>(`/teacher/courses/${id}`);
  const students = usePortalQuery<Student[]>(submissionsTab ? null : `/teacher/courses/${id}/students`);
  const exercises = usePortalQuery<CourseExercise[]>(submissionsTab ? null : `/teacher/courses/${id}/exercises`);
  const mutation = usePortalMutation();
  const [preview, setPreview] = useState<string | null>(null);
  const [email, setEmail] = useState('');
  async function addStudent(e: FormEvent) {
    e.preventDefault();
    try { await mutation.mutateAsync({ path: `/teacher/courses/${id}/students`, data: { email: email.trim() } }); setEmail(''); toast.success('Đã thêm sinh viên'); } catch { /* Error shown below. */ }
  }
  if (course.isPending) return <p role="status">Đang tải lớp…</p>;
  if (!course.data) return <PortalError error={course.error} retry={() => void course.refetch()} />;
  const c = course.data;
  return <>
    <Link to="/teacher/courses" className="portal-link">← Danh sách lớp học phần</Link>
    <header className="portal-heading" style={{ marginTop: 20 }}><div><p>{c.sectionCode} · {c.semester}</p><h1>{c.subjectName}</h1><span>{c.enrolledCount}/{c.capacity} sinh viên · {c.language} · {c.status === 'Active' ? 'Đang mở' : c.status === 'Draft' ? 'Bản nháp' : 'Đã đóng'}</span></div>
      <button disabled={mutation.isPending} onClick={() => mutation.mutate({ path: `/teacher/courses/${id}/status`, method: 'put', data: { status: c.status === 'Active' ? 'Closed' : 'Active' } })}>{c.status === 'Active' ? 'Đóng lớp' : 'Mở lớp'}</button></header>
    <div className="portal-card portal-actions"><span>Mã tham gia: <strong>{c.joinCode}</strong></span><button onClick={async () => { try { await navigator.clipboard.writeText(c.joinCode ?? ''); toast.success('Đã sao chép mã lớp'); } catch { toast.error('Không thể sao chép. Hãy chọn và sao chép mã lớp.'); } }}>Sao chép</button><span>Sinh viên nhập mã tại trang Lớp học phần để tham gia.</span></div>
    <PortalError error={mutation.error} />
    <nav className="portal-tabs"><NavLink end to={`/teacher/courses/${id}`}>Sinh viên & bài tập</NavLink><NavLink to={`/teacher/courses/${id}/submissions`}>Bài nộp & chấm điểm</NavLink></nav>
    {submissionsTab ? <TeacherSubmissionsPage courseId={id} /> : <>
      <section className="portal-card"><h2>Sinh viên trong lớp</h2>
        {c.status === 'Active' && <form className="portal-form" onSubmit={addStudent}><label>Thêm bằng email sinh viên<input type="email" required maxLength={256} value={email} onChange={e => setEmail(e.target.value)} placeholder="sinhvien@hanu.edu.vn" /></label><button disabled={mutation.isPending}>Thêm vào lớp</button></form>}
        <PortalError error={students.error} retry={() => void students.refetch()} />
        {students.isPending ? <p>Đang tải sinh viên…</p> : students.data && <StudentTable students={students.data} action={s => <button disabled={mutation.isPending || s.status !== 'Active'} onClick={() => mutation.mutate({ path: `/teacher/courses/${id}/students/${s.id}`, method: 'delete' })}>Cho rời lớp</button>} />}
      </section>
      <ClassProblemsPanel id={id} kind="course" manage />
      <section className="portal-card"><div className="portal-heading"><h2>Bài tập trước đây</h2></div>
        <PortalError error={exercises.error} retry={() => void exercises.refetch()} />
        {exercises.isPending ? <p>Đang tải bài tập…</p> : exercises.data?.length === 0 ? <Empty>Chưa có bài tập. Mở lớp và giao bài khi sẵn sàng.</Empty> : <div className="portal-exercises">{exercises.data?.map(e => <div key={e.id}><div className="portal-actions"><button onClick={() => setPreview(preview === e.slug ? null : e.slug)}>{e.title}</button><span>{e.difficulty}</span><small>{new Date(e.createdAt).toLocaleDateString('vi-VN')}</small></div>{preview === e.slug && <ExercisePreview slug={e.slug} />}</div>)}</div>}
      </section>
    </>}
  </>;
}
