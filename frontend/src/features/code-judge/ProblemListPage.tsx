import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { judgeApi, problemScope } from './api';
import { ScopePicker } from './ScopePicker';

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
    <main className="min-h-screen bg-[#fafaf7] p-6 text-[#334155] font-sans">
      <div className="flex justify-between items-center mb-6">
        <Link to="/courses" className="text-[#6366b5] hover:underline font-medium">← Lớp của tôi</Link>
        <button
          onClick={() => { void q.refetch(); void scopes.refetch(); }}
          className="px-[10px] py-1 border border-[#d1d5db] rounded text-[13px] bg-transparent cursor-pointer hover:bg-black/5"
        >
          Làm mới
        </button>
      </div>
      <h1 className="text-[26px] font-bold my-4">Bài tập lập trình của lớp</h1>
      <p className="text-[#64748b] text-[13px] mb-4">Chọn bài giáo viên đã đăng để mở khung coding, chạy thử và nộp bài.</p>
      <ScopePicker scopes={scopes.data || []} value={scope} all onChange={v => { setSearch(v ? { scope: v } : {}); setPage(1); }} />
      {scopes.isError && <p className="text-[#b75d57] my-2">Không tải được danh sách lớp.</p>}
      {q.isError && <p className="text-[#b75d57] my-2">Không thể mở bài tập của lớp này.</p>}
      {q.isPending && <p className="my-2">Đang tải bài tập…</p>}
      {q.data?.items.length === 0 && <p className="my-2">Chưa có bài tập được xuất bản trong lớp.</p>}
      {q.data?.items.map(p => (
        <Link className="bg-white border border-[#e2e8f0] rounded-[20px] shadow-[0_3px_14px_#33415506] flex justify-between items-center p-[22px] my-3 hover:border-[#cbd5e1] transition-colors" key={p.id} to={`/problems/${p.id}`}>
          <div>
            <strong>{p.title}</strong>
            <p className="text-[#64748b] text-[13px] mt-1">{scopes.data?.find(s => `${s.kind}:${s.id}` === problemScope(p))?.name}</p>
          </div>
          <span className="text-sm font-medium">{p.difficulty} · {p.points} điểm →</span>
        </Link>
      ))}
      <div className="flex items-center gap-[14px] justify-end p-[14px]">
        <button disabled={page === 1} onClick={() => setPage(page - 1)} className="px-[14px] py-[9px] border border-[#e2e8f0] rounded-[10px] bg-white cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed hover:bg-slate-50">Trước</button>
        <span className="text-sm font-semibold">{page}</span>
        <button disabled={page * 20 >= (q.data?.total || 0)} onClick={() => setPage(page + 1)} className="px-[14px] py-[9px] border border-[#e2e8f0] rounded-[10px] bg-white cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed hover:bg-slate-50">Sau</button>
      </div>
    </main>
  );
}
