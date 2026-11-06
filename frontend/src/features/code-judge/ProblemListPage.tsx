import { useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { judgeApi } from './api';
import { MemberHeader } from '../home/MemberHeader';
import { useAuthStore } from '../auth/store/authStore';
import { problemCategories } from './problemCategories';
import { PracticeSidebar } from './components/PracticeSidebar';
import { PracticeIntro } from './components/PracticeIntro';
import { PracticeFilters } from './components/PracticeFilters';
import { PracticeProblemList } from './components/PracticeProblemList';
import { PracticeOverview } from './components/PracticeOverview';
import './practice-library.css';

const categoryLabel = (id: string) => problemCategories.find(c => c.id === id)?.label || 'Tổng hợp';

export function ProblemListPage() {
  const [params, setParams] = useSearchParams();
  const userId = useAuthStore(s => s.user?.id);
  const scope = params.get('scope') || '';
  const classView = /^class:[^:]+$/.test(scope);
  const rawPage = Number(params.get('page'));
  const page = Number.isFinite(rawPage) ? Math.max(1, Math.min(100000, Math.floor(rawPage))) : 1;
  const filters = { page, search: params.get('search') || '', difficulty: params.get('difficulty') || '', category: params.get('category') || '', language: params.get('language') || '' };
  const query = useQuery({ queryKey: ['judge-list', userId, classView ? scope : 'library', filters], queryFn: () => classView ? judgeApi.list(false, page, scope) : judgeApi.library(filters) });
  const update = (key: string, value: string) => setParams(previous => {
    const next = new URLSearchParams(previous);
    if (value) next.set(key, value); else next.delete(key);
    if (key !== 'page') next.delete('page');
    return next;
  });
  const resetFilters = () => setParams(previous => {
    const next = new URLSearchParams(previous);
    ['search', 'difficulty', 'category', 'language', 'page'].forEach(key => next.delete(key));
    return next;
  });
  const items = query.data?.items || [];
  const hasFilters = !classView && !!(filters.search || filters.difficulty || filters.category || filters.language);
  const title = classView ? 'Bài tập của lớp' : filters.category ? categoryLabel(filters.category) : 'Luyện tập lập trình';

  return <div className="practice-library">
    <MemberHeader />
    <div className={`practice-layout${classView ? ' practice-layout-class' : ''}`}>
      <PracticeSidebar classView={classView} category={filters.category} onCategoryChange={value => update('category', value)} />

      <main className="practice-main">
        <PracticeIntro classView={classView} category={filters.category} title={title} onCategoryChange={value => update('category', value)} />

        {!classView && <PracticeFilters filters={filters} onChange={update} />}
        <PracticeProblemList items={items} page={page} total={query.data?.total || 0} fetching={query.isFetching} pending={query.isPending} error={query.isError} success={query.isSuccess} hasFilters={hasFilters} resetFilters={resetFilters} onRetry={() => void query.refetch()} onPageChange={value => update('page', String(value))} />
      </main>

      <PracticeOverview progress={query.isSuccess ? query.data.progress : undefined} title={classView ? "Tiến độ của lớp" : filters.category ? categoryLabel(filters.category) : "Tiến độ luyện tập"} hasFilters={hasFilters} />
    </div>
  </div>;
}
