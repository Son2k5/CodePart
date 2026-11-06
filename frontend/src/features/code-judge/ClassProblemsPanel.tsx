import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { judgeApi } from './api';

export function ClassProblemsPanel({ id, kind = 'class', manage = false }: { id: string; kind?: 'class'; manage?: boolean }) {
  const scope = `${kind}:${id}`;
  const q = useQuery({ queryKey: [manage ? 'judge-admin-list' : 'judge-list', 1, scope], queryFn: () => judgeApi.list(manage, 1, scope) });

  const difficultyBadge = (diff: string) => {
    const d = diff?.toLowerCase();
    if (d === 'easy' || d === 'dễ') {
      return <span className="px-2.5 py-0.5 text-[11px] font-bold border border-emerald-200 bg-emerald-50 text-emerald-700 rounded">Dễ</span>;
    }
    if (d === 'hard' || d === 'khó') {
      return <span className="px-2.5 py-0.5 text-[11px] font-bold border border-rose-200 bg-rose-50 text-rose-700 rounded">Khó</span>;
    }
    return <span className="px-2.5 py-0.5 text-[11px] font-bold border border-amber-200 bg-amber-50 text-amber-700 rounded">Trung bình</span>;
  };

  return (
    <div className="bg-white border border-blue-200/90 rounded-md overflow-hidden shadow-none border-t-3 border-t-blue-500 text-slate-800">
      <div className="px-5 py-4 bg-blue-50/40 border-b border-blue-100 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-base font-bold text-slate-900">Bài tập lập trình</h2>
          <p className="text-xs text-slate-500 mt-0.5">
            {manage
              ? 'Tạo và xuất bản bài tập cho lớp. Học sinh làm bài trên khung coding chung.'
              : 'Bài tập giáo viên đã đăng cho lớp. Chọn bài để viết code, chạy thử và nộp.'}
          </p>
        </div>
        <div className="flex items-center gap-2.5 flex-wrap">
          <Link
            to={`${manage ? '/manage/problems' : '/practice'}?scope=${encodeURIComponent(scope)}`}
            className="px-3.5 py-1.5 text-xs font-semibold rounded-md bg-blue-600 hover:bg-blue-700 text-white transition-colors inline-flex items-center shadow-none"
          >
            {manage ? 'Quản lý / Đăng bài' : 'Xem danh sách bài tập'}
          </Link>
          {manage && (
            <Link
              to={`/manage/problems?scope=${encodeURIComponent(scope)}&tab=submissions`}
              className="px-3.5 py-1.5 text-xs font-semibold rounded-md border border-blue-300 bg-white text-blue-700 hover:bg-blue-50 transition-colors inline-flex items-center"
            >
              Xem bài nộp của lớp
            </Link>
          )}
        </div>
      </div>

      <div className="p-5">
        {q.isPending && (
          <div className="py-8 flex items-center justify-center gap-2 text-slate-500 text-xs">
            <span className="inline-block w-4 h-4 border-2 border-blue-500 border-t-transparent rounded-full animate-spin" />
            <span>Đang tải bài tập…</span>
          </div>
        )}

        {q.isError && (
          <div className="p-3.5 border border-rose-200 bg-rose-50 text-rose-800 rounded-md flex items-center justify-between text-xs my-2">
            <span>Chưa tải được danh sách bài tập.</span>
            <button
              type="button"
              onClick={() => q.refetch()}
              className="px-3 py-1 bg-white border border-rose-300 text-rose-700 rounded text-xs font-semibold hover:bg-rose-50 transition-colors cursor-pointer"
            >
              Thử lại
            </button>
          </div>
        )}

        {!q.isPending && !q.isError && q.data?.items.length === 0 && (
          <div className="py-10 text-center border border-dashed border-blue-200 rounded-md bg-blue-50/20">
            <p className="text-xs text-slate-500 mb-3">
              {manage ? 'Lớp chưa có bài tập lập trình nào.' : 'Chưa có bài tập nào được xuất bản.'}
            </p>
            {manage && (
              <Link
                to={`/manage/problems?scope=${encodeURIComponent(scope)}`}
                className="px-3.5 py-1.5 text-xs font-semibold rounded-md border border-blue-300 bg-white text-blue-700 hover:bg-blue-50 transition-colors inline-block"
              >
                Tạo bài tập đầu tiên
              </Link>
            )}
          </div>
        )}

        {!q.isPending && !q.isError && (q.data?.items?.length ?? 0) > 0 && (
          <div className="overflow-x-auto border border-slate-200 rounded-md">
            <table className="w-full text-left text-xs border-collapse">
              <thead>
                <tr className="bg-blue-50/30 border-b border-blue-100 text-slate-700 font-semibold">
                  <th className="p-3 w-12 text-center text-slate-400">STT</th>
                  <th className="p-3">Tên bài tập</th>
                  <th className="p-3 w-28 text-center">Độ khó</th>
                  <th className="p-3 w-24 text-center">Điểm số</th>
                  {manage && <th className="p-3 w-28 text-center">Trạng thái</th>}
                  <th className="p-3 w-28 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {q.data?.items.map((p, index) => (
                  <tr key={p.id} className="hover:bg-blue-50/20 transition-colors">
                    <td className="p-3 text-center text-slate-400">{index + 1}</td>
                    <td className="p-3 font-semibold text-slate-900">
                      <Link
                        to={manage ? `/manage/problems?scope=${encodeURIComponent(scope)}` : `/practice/${p.id}`}
                        className="hover:underline text-slate-900 hover:text-blue-600"
                      >
                        {p.title}
                      </Link>
                    </td>
                    <td className="p-3 text-center">{difficultyBadge(p.difficulty)}</td>
                    <td className="p-3 text-center font-bold text-slate-700">{p.points} đ</td>
                    {manage && (
                      <td className="p-3 text-center">
                        <span className={`inline-block px-2.5 py-0.5 rounded text-[11px] font-semibold border ${p.status === 'Published' ? 'border-emerald-200 text-emerald-700 bg-emerald-50' : 'border-amber-200 text-amber-700 bg-amber-50'}`}>
                          {p.status === 'Published' ? 'Đã xuất bản' : 'Bản nháp'}
                        </span>
                      </td>
                    )}
                    <td className="p-3 text-right">
                      <Link
                        to={manage ? `/manage/problems?scope=${encodeURIComponent(scope)}` : `/practice/${p.id}`}
                        className="text-blue-600 hover:text-blue-700 font-bold hover:underline"
                      >
                        {manage ? 'Cấu hình' : 'Làm bài'} →
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
