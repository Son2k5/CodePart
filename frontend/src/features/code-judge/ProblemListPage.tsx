import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { judgeApi, problemScope } from './api';
import { ScopePicker } from './ScopePicker';
import './judge.css';

export function ProblemListPage() {
  const [search, setSearch] = useSearchParams();
  const scope = search.get('scope') || '';
  const scopes = useQuery({ queryKey: ['judge-scopes'], queryFn: () => judgeApi.scopes() });
  const [page, setPage] = useState(1);
  const q = useQuery({
    queryKey: ['judge-list', page, scope],
    queryFn: () => judgeApi.list(false, page, scope),
  });

  return (
    <main className="judge-shell">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Link to="/courses">← Lớp của tôi</Link>
        <button
          onClick={() => { void q.refetch(); void scopes.refetch(); }}
          style={{ background: 'none', border: '1px solid #d1d5db', borderRadius: 4, padding: '4px 10px', cursor: 'pointer', fontSize: 13 }}
        >
          Làm mới
        </button>
      </div>
      <h1>Bài tập lập trình của lớp</h1>
      <p className="judge-muted">Chọn bài giáo viên đã đăng để mở khung coding, chạy thử và nộp bài.</p>
      <ScopePicker scopes={scopes.data || []} value={scope} all onChange={v => { setSearch(v ? { scope: v } : {}); setPage(1); }} />
      {scopes.isError && <p>Không tải được danh sách lớp.</p>}
      {q.isError && <p>Không thể mở bài tập của lớp này.</p>}
      {q.isPending && <p>Đang tải bài tập…</p>}
      {q.data?.items.length === 0 && <p>Chưa có bài tập được xuất bản trong lớp.</p>}
      {q.data?.items.map(p => (
        <Link className="judge-card judge-list-item" key={p.id} to={`/problems/${p.id}`}>
          <div>
            <strong>{p.title}</strong>
            <p className="judge-muted">{scopes.data?.find(s => `${s.kind}:${s.id}` === problemScope(p))?.name}</p>
          </div>
          <span>{p.difficulty} · {p.points} điểm →</span>
        </Link>
      ))}
      <div className="judge-actions">
        <button disabled={page === 1} onClick={() => setPage(page - 1)}>Trước</button>
        <span>{page}</span>
        <button disabled={page * 20 >= (q.data?.total || 0)} onClick={() => setPage(page + 1)}>Sau</button>
      </div>
    </main>
  );
}
