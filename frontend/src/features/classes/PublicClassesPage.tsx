import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useClassesQuery, useClassMutation, useDebounced, type Page, type PublicClass } from './api';
import { Pagination, QueryState } from './ClassUi';

export function PublicClassesPage() {
  const [search, setSearch] = useState(''); const [page, setPage] = useState(1); const [code, setCode] = useState('');
  const debounced = useDebounced(search); const mutation = useClassMutation();
  const query = useClassesQuery<Page<PublicClass>>('/classes/public', { page, pageSize: 12, search: debounced });
  const joined = useClassesQuery<{ id: string; name: string }[]>('/learning/classes');
  return <main className="max-w-[1250px] mx-auto p-6 text-[#24242c] font-sans [&_h1]:text-[26px] [&_h1]:font-bold [&_h1]:mb-[18px] [&_h2]:text-[20px] [&_h2]:font-semibold [&_h2]:my-3.5 [&_p]:my-3 [&_button]:border [&_button]:border-[#d4d4db] [&_button]:rounded-lg [&_button]:px-3.5 [&_button]:py-2 [&_button]:bg-white [&_button]:cursor-pointer hover:[&_button]:bg-[#f0f0f7] [&_button:disabled]:opacity-50 [&_button:disabled]:cursor-not-allowed [&_button.primary]:bg-[#4338ca] [&_button.primary]:text-white [&_button.primary]:border-[#4338ca] [&_input:not([type=checkbox])]:border [&_input:not([type=checkbox])]:border-[#c8c8d0] [&_input:not([type=checkbox])]:rounded-[7px] [&_input:not([type=checkbox])]:p-[9px] [&_input:not([type=checkbox])]:bg-white [&_a]:text-[#4338ca] hover:[&_a]:underline"><Link to="/courses">← Khóa học của tôi</Link><h1>Khám phá lớp public</h1><p>Tham gia miễn phí các lớp đang mở hoặc nhập mã được chia sẻ.</p><Link to="/problems">Mở bài tập các lớp đã tham gia →</Link>
    {!!joined.data?.length && <section><h2>Học liệu các lớp đã tham gia</h2>{joined.data.map(c => <p key={c.id}><Link to={`/classes/${c.id}/learning`}>{c.name} →</Link></p>)}</section>}
    <form className="flex gap-3 items-center flex-wrap my-4" onSubmit={e => { e.preventDefault(); mutation.mutate({ path: '/classes/join-by-code', data: { code: code.trim().toUpperCase() } }, { onSuccess: () => setCode('') }); }}>
      <input aria-label="Mã lớp" required minLength={8} maxLength={8} pattern="[A-HJ-NP-Za-hj-np-z2-9]{8}" placeholder="Nhập mã lớp (8 ký tự)" value={code} onChange={e => setCode(e.target.value)} /><button className="primary" disabled={mutation.isPending}>Tham gia bằng mã</button>
    </form><input aria-label="Tìm lớp public" placeholder="Tìm tên lớp" value={search} onChange={e => { setSearch(e.target.value); setPage(1); }} className="w-full max-w-md my-3" />
    <QueryState loading={query.isLoading} error={query.error} empty={query.data?.items.length === 0} retry={() => query.refetch()} />
    {query.data && <><div className="grid grid-cols-[repeat(auto-fit,minmax(240px,1fr))] gap-4 my-6">{query.data.items.map(c => <article className="border border-[#dedee5] bg-white p-5 rounded-xl flex flex-col justify-between shadow-sm [&_img]:w-full [&_img]:h-40 [&_img]:object-cover [&_img]:rounded-lg" key={c.id}>{c.thumbnail && <img src={c.thumbnail} alt="" loading="lazy" />}<span className="inline-block bg-[#dcfce7] text-[#166534] rounded-full px-2.5 py-1 text-xs font-medium w-fit my-2">Public · Miễn phí</span><h2>{c.name}</h2><p>{c.description}</p><p>{c.studentCount}/{c.maxStudents ?? '∞'} học sinh</p>
      <>{c.isEnrolled && <Link to={`/classes/${c.id}/learning`}>Học liệu và trợ giảng →</Link>}{c.isEnrolled && <Link to={`/problems?scope=class:${c.id}`}>Bài tập lập trình →</Link>}</><button disabled={mutation.isPending || !c.isEnrolled && c.maxStudents !== null && c.studentCount >= c.maxStudents} onClick={() => mutation.mutate({ path: `/classes/${c.id}/${c.isEnrolled ? 'leave' : 'join'}` })}>{c.isEnrolled ? 'Rời lớp' : 'Tham gia lớp'}</button></article>)}</div>
      <Pagination page={page} totalPages={query.data.totalPages} totalCount={query.data.totalCount} onChange={setPage} /></>}
  </main>;
}
