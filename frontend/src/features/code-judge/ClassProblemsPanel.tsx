import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { judgeApi } from './api';

export function ClassProblemsPanel({ id, kind, manage = false }: { id: string; kind: 'class' | 'course'; manage?: boolean }) {
  const scope = `${kind}:${id}`;
  const q = useQuery({ queryKey: [manage ? 'judge-admin-list' : 'judge-list', 1, scope], queryFn: () => judgeApi.list(manage, 1, scope), refetchInterval: manage ? false : 30000 });
  return <section className="portal-card" style={{ margin: '20px 0', padding: 20 }}>
    <h2>Bài tập lập trình</h2><p>{manage ? 'Tạo và xuất bản bài tập cho lớp. Học sinh làm bài trên khung coding chung.' : 'Bài tập giáo viên đã đăng cho lớp. Chọn bài để viết code, chạy thử và nộp.'}</p>
    <Link to={`${manage ? '/manage/problems' : '/problems'}?scope=${encodeURIComponent(scope)}`}>{manage ? 'Quản lý / đăng bài cho lớp →' : 'Mở danh sách bài tập →'}</Link>
    {manage && <p><Link to={`/manage/problems?scope=${encodeURIComponent(scope)}&tab=submissions`}>Xem bài nộp lập trình của lớp →</Link></p>}
    {q.isPending && <p>Đang tải bài tập…</p>}
    {q.isError && <p>Chưa tải được bài tập. <button onClick={() => q.refetch()}>Thử lại</button></p>}
    {q.data?.items.length === 0 && <p>{manage ? 'Lớp chưa có bài tập lập trình.' : 'Chưa có bài được xuất bản.'}</p>}
    <div className="portal-exercises">{q.data?.items.map(p => <Link key={p.id} to={manage ? `/manage/problems?scope=${encodeURIComponent(scope)}` : `/problems/${p.id}`}>
      {p.title}<small>{p.difficulty} · {p.points} điểm{manage ? ` · ${p.status === 'Published' ? 'Đã đăng' : 'Bản nháp'}` : ''} →</small>
    </Link>)}</div>
  </section>;
}
