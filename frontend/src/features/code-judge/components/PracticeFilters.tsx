import { ArrowRight, Search } from 'lucide-react';
import { difficultyLabels } from '../problemCategories';
type Props = { filters: { search: string; difficulty: string; language: string }; onChange: (key: string, value: string) => void };
export function PracticeFilters({ filters, onChange }: Props) {
  return <form className="practice-filters" onSubmit={e => { e.preventDefault(); const data = new FormData(e.currentTarget); onChange('search', String(data.get('search') || '').trim()); }}>
          <label className="practice-search"><Search size={17} aria-hidden="true" /><input key={filters.search} name="search" defaultValue={filters.search} placeholder="Tìm bài tập…" aria-label="Tên bài tập" /><button type="submit" aria-label="Tìm kiếm"><ArrowRight size={17} /></button></label>
          <label><span className="practice-sr-only">Độ khó</span><select aria-label="Độ khó" value={filters.difficulty} onChange={e => onChange('difficulty', e.target.value)}><option value="">Mọi độ khó</option>{Object.entries(difficultyLabels).map(([id, label]) => <option key={id} value={id}>{label}</option>)}</select></label>
          <label><span className="practice-sr-only">Ngôn ngữ</span><select aria-label="Ngôn ngữ" value={filters.language} onChange={e => onChange('language', e.target.value)}><option value="">Mọi ngôn ngữ</option>{[['python', 'Python'], ['cpp', 'C++'], ['java', 'Java'], ['csharp', 'C#']].map(([id, label]) => <option key={id} value={id}>{label}</option>)}</select></label>
        </form>;
}
