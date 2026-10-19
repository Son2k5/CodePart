import { Link } from 'react-router-dom';
import { useState } from 'react';
import { usePortalQuery, type TeachingClass, type Student } from '../api/teacherApi';
import { Empty, PortalError, StudentTable } from '../components/PortalUi';
import { ClassProblemsPanel } from '../../code-judge/ClassProblemsPanel';

export function TeacherClassesPage() {
  const classes = usePortalQuery<TeachingClass[]>('/teacher/classes');
  const [selected, setSelected] = useState<string | null>(null);
  const students = usePortalQuery<Student[]>(selected ? `/teacher/classes/${selected}/students` : null);
  return <><header className="portal-heading"><div><p>CỐ VẤN HỌC TẬP</p><h1>Lớp niên khóa</h1><span>Các lớp được phân công cho bạn trong hệ thống.</span></div></header>
    <PortalError error={classes.error} retry={() => void classes.refetch()} />
    {classes.isPending ? <p>Đang tải lớp…</p> : classes.data?.length === 0 ? <Empty>Chưa được phân công lớp niên khóa.</Empty> : <div className="portal-courses">{classes.data?.map(c => <section className="portal-card" key={c.id}><h2>{c.name}</h2><p>{c.academicYear} · {c.facultyName}</p><p>{c.studentCount} sinh viên</p><Link to={`/classes/${c.id}/learning`}>Học liệu và trợ giảng</Link><button onClick={() => setSelected(c.id)}>Xem sinh viên</button></section>)}</div>}
    {selected && <ClassProblemsPanel id={selected} kind="class" manage />}
    {selected && <section className="portal-card"><h2>Sinh viên lớp {classes.data?.find(c => c.id === selected)?.name}</h2><PortalError error={students.error} retry={() => void students.refetch()} />{students.isPending ? <p>Đang tải sinh viên…</p> : students.data && <StudentTable students={students.data} />}</section>}
  </>;
}
