import { Link } from 'react-router-dom';
import { ArrowLeft, ArrowRight, BookOpen, X } from 'lucide-react';
import type { Problem } from '../api';
import { problemCategories, difficultyLabels } from '../problemCategories';
type Props = { items: Problem[]; page: number; total: number; fetching: boolean; pending: boolean; error: boolean; success: boolean; hasFilters: boolean; resetFilters: () => void; onRetry: () => void; onPageChange: (page: number) => void };
const categoryLabel = (id: string) => problemCategories.find(c => c.id === id)?.label || 'Tổng hợp';
export function PracticeProblemList({ items, page, total, fetching, pending, error, success, hasFilters, resetFilters, onRetry, onPageChange }: Props) {
  const categories = [...new Set(items.map(p => p.category))];
  const totalPages = Math.max(1, Math.ceil(total / 20));
  return <section aria-labelledby="problem-list-title" aria-busy={fetching}>
          <div className="practice-list-heading"><h2 id="problem-list-title">Danh sách bài tập</h2>{hasFilters ? <button type="button" onClick={resetFilters}><X size={14} /> Xóa bộ lọc</button> : <span>Theo chủ đề</span>}</div>
          {pending ? <div className="practice-empty" role="status"><span className="practice-loading" />Đang tải bài tập…</div> : error ? <div className="practice-empty" role="alert"><h3>Chưa tải được danh sách bài tập</h3><p>Vui lòng thử kết nối lại.</p><button type="button" onClick={() => onRetry()}>Thử lại</button></div> : items.length === 0 ? <div className="practice-empty"><BookOpen size={28} /><h3>{hasFilters ? 'Chưa tìm thấy bài phù hợp' : 'Chưa có bài tập trên trang này'}</h3><p>{hasFilters ? 'Thử chủ đề khác hoặc bỏ bớt bộ lọc nhé.' : 'Bài tập sẽ xuất hiện sau khi được xuất bản.'}</p>{hasFilters && <button type="button" onClick={resetFilters}>Xem tất cả bài tập</button>}</div> : categories.map(category => <section className="practice-group" key={category} aria-label={categoryLabel(category)}>
            <div className="practice-group-heading"><h3>{categoryLabel(category)}</h3><span>{items.filter(p => p.category === category).length} bài trên trang</span></div>
            <div className="practice-table-wrap"><table className="practice-table"><thead><tr><th scope="col" className="practice-index">STT</th><th scope="col">Bài tập</th><th scope="col">Độ khó</th><th scope="col" className="practice-points">Điểm</th><th scope="col"><span className="practice-sr-only">Mở bài tập</span></th></tr></thead><tbody>
              {items.filter(p => p.category === category).map(p => <tr key={p.id}><td className="practice-index">{String((page - 1) * 20 + items.indexOf(p) + 1).padStart(2, '0')}</td><td><Link className="practice-problem-title" to={`/practice/${p.id}`}>{p.title}</Link></td><td><span className={`practice-difficulty ${p.difficulty.toLowerCase()}`}>{difficultyLabels[p.difficulty] || p.difficulty}</span></td><td className="practice-points">{p.points}</td><td><Link className="practice-open" to={`/practice/${p.id}`} aria-label={`Làm bài ${p.title}`}><ArrowRight size={16} /></Link></td></tr>)}
            </tbody></table></div>
          </section>)}
          {success && (total > 20 || page > 1) && <nav className="practice-pagination" aria-label="Phân trang bài tập"><button type="button" disabled={page <= 1} onClick={() => onPageChange(page - 1)}><ArrowLeft size={14} /> Trước</button><span>Trang {page} / {totalPages}</span><button type="button" disabled={page >= totalPages} onClick={() => onPageChange(page + 1)}>Sau <ArrowRight size={14} /></button></nav>}
        </section>;
}
