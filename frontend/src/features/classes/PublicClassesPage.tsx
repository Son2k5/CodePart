import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useClassesQuery, useClassMutation, useDebounced, type Page, type PublicClass } from './api';
import { Pagination, QueryState } from './ClassUi';
import './classes.css';

export function PublicClassesPage() {
  const [search, setSearch] = useState(''); const [page, setPage] = useState(1); const [code, setCode] = useState('');
  const debounced = useDebounced(search); const mutation = useClassMutation();
  const query = useClassesQuery<Page<PublicClass>>('/classes/public', { page, pageSize: 12, search: debounced });
  const joined = useClassesQuery<{ id: string; name: string }[]>('/learning/classes');
  return <main className="classes-page"><Link to="/courses">← Khóa học của tôi</Link><h1>Khám phá lớp public</h1><p>Tham gia miễn phí các lớp đang mở hoặc nhập mã được chia sẻ.</p><Link to="/problems">Mở bài tập các lớp đã tham gia →</Link>
    {!!joined.data?.length && <section><h2>Học liệu các lớp đã tham gia</h2>{joined.data.map(c => <p key={c.id}><Link to={`/classes/${c.id}/learning`}>{c.name} →</Link></p>)}</section>}
    <form className="class-toolbar" onSubmit={e => { e.preventDefault(); mutation.mutate({ path: '/classes/join-by-code', data: { code: code.trim().toUpperCase() } }, { onSuccess: () => setCode('') }); }}>
      <input aria-label="Mã lớp" required minLength={8} maxLength={8} pattern="[A-HJ-NP-Za-hj-np-z2-9]{8}" placeholder="Nhập mã lớp (8 ký tự)" value={code} onChange={e => setCode(e.target.value)} /><button className="primary" disabled={mutation.isPending}>Tham gia bằng mã</button>
    </form><input aria-label="Tìm lớp public" placeholder="Tìm tên lớp" value={search} onChange={e => { setSearch(e.target.value); setPage(1); }} />
    <QueryState loading={query.isLoading} error={query.error} empty={query.data?.items.length === 0} retry={() => query.refetch()} />
    {query.data && <><div className="class-grid">{query.data.items.map(c => <article className="class-card" key={c.id}>{c.thumbnail && <img src={c.thumbnail} alt="" loading="lazy" />}<span className="class-badge public">Public · Miễn phí</span><h2>{c.name}</h2><p>{c.description}</p><p>{c.studentCount}/{c.maxStudents ?? '∞'} học sinh</p>
      <>{c.isEnrolled && <Link to={`/classes/${c.id}/learning`}>Học liệu và trợ giảng →</Link>}{c.isEnrolled && <Link to={`/problems?scope=class:${c.id}`}>Bài tập lập trình →</Link>}</><button disabled={mutation.isPending || !c.isEnrolled && c.maxStudents !== null && c.studentCount >= c.maxStudents} onClick={() => mutation.mutate({ path: `/classes/${c.id}/${c.isEnrolled ? 'leave' : 'join'}` })}>{c.isEnrolled ? 'Rời lớp' : 'Tham gia lớp'}</button></article>)}</div>
      <Pagination page={page} totalPages={query.data.totalPages} totalCount={query.data.totalCount} onChange={setPage} /></>}
  </main>;
}
