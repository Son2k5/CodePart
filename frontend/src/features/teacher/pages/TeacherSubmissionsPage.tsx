import { useState, type FormEvent } from 'react';
import { toast } from 'sonner';
import { usePortalQuery, usePortalMutation, type SubmissionPage, type SubmissionDetail } from '../api/teacherApi';
import { Empty, PortalError } from '../components/PortalUi';

function SubmissionReview({ courseId, id, close }: { courseId: string; id: string; close: () => void }) {
  const detail = usePortalQuery<SubmissionDetail>(`/teacher/courses/${courseId}/submissions/${id}`);
  const grade = usePortalMutation();
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const data = new FormData(e.currentTarget);
    try { await grade.mutateAsync({ path: `/teacher/courses/${courseId}/submissions/${id}/grade`, method: 'put', data: { score: Number(data.get('score')), feedback: data.get('feedback') } }); toast.success('Đã lưu điểm và nhận xét'); } catch { /* Keep form on failure. */ }
  }
  return <section className="portal-card"><div className="portal-heading"><h2>Chi tiết bài nộp</h2><button onClick={close}>Đóng chi tiết</button></div><PortalError error={detail.error} retry={() => void detail.refetch()} />
    {detail.isPending ? <p>Đang tải bài nộp…</p> : detail.data && <>
      <h3>{detail.data.summary.studentName} · {detail.data.summary.exerciseTitle}</h3><p>{detail.data.summary.status} · {detail.data.summary.passedTests}/{detail.data.summary.totalTests} test đạt · {detail.data.summary.language}</p>
      <pre className="portal-code">{detail.data.sourceCode}</pre><h3>Kết quả từng test</h3>
      {detail.data.testResults.map(t => <details key={t.order}><summary>Test {t.order}: {t.status} · {t.runtimeMs ?? '—'} ms · {t.memoryKb ?? '—'} KB</summary><div className="portal-test"><div><h4>Đầu vào</h4><pre>{t.input}</pre></div><div><h4>Mong đợi</h4><pre>{t.expectedOutput}</pre></div><div><h4>Thực tế</h4><pre>{t.actualOutput ?? '—'}</pre></div></div>{t.errorMessage && <pre className="portal-code">{t.errorMessage}</pre>}</details>)}
      <form className="portal-form" onSubmit={submit}><h3>Điểm của giảng viên</h3><p>Điểm thủ công được lưu riêng, giữ nguyên kết quả chấm tự động.</p>
        <label>Điểm (0–10)<input name="score" type="number" min={0} max={10} step="0.01" required defaultValue={detail.data.summary.teacherScore ?? ''} /></label>
        <label>Nhận xét<textarea name="feedback" maxLength={2000} defaultValue={detail.data.summary.teacherFeedback ?? ''} /></label>
        {detail.data.summary.gradedAt && <p>Lần chấm gần nhất: {new Date(detail.data.summary.gradedAt).toLocaleString('vi-VN')}</p>}
        <PortalError error={grade.error} /><button className="primary" disabled={grade.isPending || detail.data.summary.status === 'Pending'}>{grade.isPending ? 'Đang lưu…' : 'Lưu điểm'}</button>
      </form>
    </>}
  </section>;
}
export function TeacherSubmissionsPage({ courseId }: { courseId: string }) {
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<string | null>(null);
  const query = usePortalQuery<SubmissionPage>(`/teacher/courses/${courseId}/submissions?page=${page}&pageSize=20`);
  return <>
    <section className="portal-card"><h2>Bài nộp của lớp</h2><PortalError error={query.error} retry={() => void query.refetch()} />
      {query.isPending ? <p role="status">Đang tải bài nộp…</p> : !query.error && !query.data?.items.length ? <Empty>Chưa có bài nộp.</Empty> : <div className="portal-table"><table><thead><tr><th>Sinh viên</th><th>Bài tập</th><th>Kết quả tự động</th><th>Điểm GV</th><th>Nộp lúc</th><th /></tr></thead><tbody>{query.data?.items.map(s => <tr key={s.id}><td>{s.studentName}</td><td>{s.exerciseTitle}</td><td>{s.status} ({s.passedTests}/{s.totalTests})</td><td>{s.teacherScore ?? 'Chưa chấm'}</td><td>{new Date(s.createdAt).toLocaleString('vi-VN')}</td><td><button onClick={() => setSelected(s.id)}>Xem & chấm</button></td></tr>)}</tbody></table></div>}
      <div className="portal-pagination"><button disabled={page === 1 || query.isFetching} onClick={() => setPage(p => p - 1)}>Trước</button><span>Trang {page}/{Math.max(1, query.data?.totalPages ?? 1)} · {query.data?.totalCount ?? 0} bài nộp</span><button disabled={query.isFetching || page >= (query.data?.totalPages ?? 1)} onClick={() => setPage(p => p + 1)}>Sau</button></div>
    </section>
    {selected && <SubmissionReview key={selected} courseId={courseId} id={selected} close={() => setSelected(null)} />}
  </>;
}
