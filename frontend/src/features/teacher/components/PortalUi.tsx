import type { ReactNode } from 'react';
import { portalError, type Student } from '../api/teacherApi';

export function PortalError({ error, retry }: { error: unknown; retry?: () => void }) {
  return error ? <div className="portal-error" role="alert">{portalError(error)} {retry && <button onClick={retry}>Thử lại</button>}</div> : null;
}
export function Empty({ children }: { children: ReactNode }) { return <p className="portal-empty">{children}</p>; }
export function StudentTable({ students, action }: { students: Student[]; action?: (s: Student) => ReactNode }) {
  return students.length ? <div className="portal-table"><table><thead><tr><th>Mã sinh viên</th><th>Họ và tên</th><th>Email</th><th>Trạng thái</th>{action && <th>Thao tác</th>}</tr></thead>
    <tbody>{students.map(s => <tr key={s.id}><td>{s.studentId ?? '—'}</td><td>{s.fullName}</td><td>{s.email}</td><td>{s.status}</td>{action && <td>{action(s)}</td>}</tr>)}</tbody></table></div> : <Empty>Chưa có sinh viên trong lớp.</Empty>;
}
