import { useState } from 'react';
import { Link } from 'react-router-dom';
import { problemCategories } from '../problemCategories';

type Props = { classView: boolean; category: string; onCategoryChange: (value: string) => void };
const normalize = (value: string) => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/gi, 'd').toLocaleLowerCase('vi');

export function PracticeSidebar({ classView, category, onCategoryChange }: Props) {
  const [search, setSearch] = useState('');
  const categories = problemCategories.filter(c => normalize(c.label).includes(normalize(search.trim())));
  return <aside className="practice-sidebar">
    <Link to="/practice" className="practice-library-link">Thư viện bài tập</Link>
    {!classView && <>
      <input className="practice-topic-search" type="search" value={search} onChange={event => setSearch(event.target.value)} placeholder="Tìm chủ đề…" aria-label="Tìm chủ đề" />
      <nav className="practice-categories" aria-label="Chủ đề bài tập">
        <button type="button" aria-pressed={!category} onClick={() => onCategoryChange('')}>Tất cả bài tập</button>
        {categories.map(c => <button type="button" key={c.id} aria-pressed={category === c.id} onClick={() => onCategoryChange(c.id)}>{c.label}</button>)}
      </nav>
      {categories.length === 0 && <p className="practice-topic-empty" role="status">Không tìm thấy chủ đề.</p>}
    </>}
  </aside>;
}

