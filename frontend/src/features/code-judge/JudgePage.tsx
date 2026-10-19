import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { judgeApi, problemScope } from './api';
import { ScopePicker } from './ScopePicker';
import './judge.css';
export { WorkspacePage as JudgePage } from '../workspace/WorkspacePage';

export function ProblemListPage() {
  const [search, setSearch] = useSearchParams();
  const scope = search.get('scope') || '';
  const scopes = useQuery({ queryKey: ['judge-scopes'], queryFn: () => judgeApi.scopes() });
  const [page, setPage] = useState(1);
  const q = useQuery({ queryKey: ['judge-list', page, scope], queryFn: () => judgeApi.list(false, page, scope), refetchInterval: 30000 });
  return <main className="judge-shell"><Link to="/courses">← Lớp của tôi</Link><h1>Bài tập lập trình của lớp</h1><p className="judge-muted">Chọn bài giáo viên đã đăng để mở khung coding, chạy thử và nộp bài.</p>
    <ScopePicker scopes={scopes.data || []} value={scope} all onChange={v => { setSearch(v ? { scope: v } : {}); setPage(1); }} />
    {scopes.isError && <p>Không tải được danh sách lớp.</p>}{q.isError && <p>Không thể mở bài tập của lớp này.</p>}{q.isPending && <p>Đang tải bài tập…</p>}{q.data?.items.length === 0 && <p>Chưa có bài tập được xuất bản trong lớp.</p>}
    {q.data?.items.map(p => <Link className="judge-card judge-list-item" key={p.id} to={`/problems/${p.id}`}><div><strong>{p.title}</strong><p className="judge-muted">{scopes.data?.find(s => `${s.kind}:${s.id}` === problemScope(p))?.name}</p></div><span>{p.difficulty} · {p.points} điểm →</span></Link>)}<div className="judge-actions"><button disabled={page === 1} onClick={() => setPage(page - 1)}>Trước</button><span>{page}</span><button disabled={page * 20 >= (q.data?.total || 0)} onClick={() => setPage(page + 1)}>Sau</button></div></main>;
}
