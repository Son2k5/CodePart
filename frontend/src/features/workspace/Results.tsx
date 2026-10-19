import { useEffect } from 'react';
import { Check, Clock3, CircleAlert, LockKeyhole } from 'lucide-react';
import type { Submission, TestResult } from '../code-judge/api';
import { Mascot } from './ui';

export const verdictLabel: Record<string, string> = { Accepted: 'Chính xác rồi!', WrongAnswer: 'Cần thêm một chút nữa', CompileError: 'Lỗi biên dịch', RuntimeError: 'Lỗi khi chạy', TimeLimitExceeded: 'Vượt thời gian', MemoryLimitExceeded: 'Vượt bộ nhớ', InternalError: 'Máy chấm gặp trục trặc', Pending: 'Đang xếp hàng', Queued: 'Đang xếp hàng', Running: 'Đang chấm bài', Judging: 'Đang chấm bài', PartialPass: 'Đúng một phần' };
export function VerdictBanner({ submission: s }: { submission: Submission }) {
  const pending = ['Pending', 'Queued', 'Running', 'Judging'].includes(s.status);
  const pass = s.status === 'Accepted';
  useEffect(() => {
    if (!pass || matchMedia('(prefers-reduced-motion: reduce)').matches) return;
    let cancelled = false;
    void import('canvas-confetti').then(({ default: confetti }) => { if (!cancelled) void confetti({ particleCount: 45, spread: 55, origin: { y: .6 }, colors: ['#A8D787', '#A8DCEC', '#F3CE68'], disableForReducedMotion: true }); });
    return () => { cancelled = true; };
  }, [pass, s.id]);
  return <div className={`ws-verdict ${pass ? 'is-pass' : pending || s.status.includes('Limit') ? 'is-wait' : 'is-fail'}`} role="status"><Mascot size={66} mood={pass ? 'celebrate' : pending || s.status === 'WrongAnswer' ? 'thinking' : 'sad'} /><div><strong>{verdictLabel[s.status] ?? s.status}</strong><p>{pending ? 'Mầm đang đợi kết quả cùng bạn…' : pass ? `Bạn nhận được ${s.awardedPoints} điểm. Giỏi lắm, tiếp tục nhé!` : 'Đọc kết quả bên dưới, thử lại từng bước nhé.'}</p>{s.queuePosition != null && pending && <small>Vị trí hàng đợi: {s.queuePosition}</small>}</div></div>;
}
export function TestGrid({ total, results }: { total: number; results: TestResult[] }) {
  return <div className="ws-test-grid" aria-label="Trạng thái từng kiểm thử">{Array.from({ length: total }, (_, i) => {
    const result = results.find(r => r.index === i + 1); const pass = result?.verdict === 'Accepted';
    return <span key={i} className={result ? pass ? 'is-pass' : 'is-fail' : 'is-pending'} title={`Test ${i + 1}: ${result ? verdictLabel[result.verdict] ?? result.verdict : 'Đang chờ'}`} aria-label={`Test ${i + 1}: ${result?.verdict ?? 'Đang chờ'}`}>{result ? pass ? <Check size={12} /> : <CircleAlert size={12} /> : <Clock3 size={12} />}{i + 1}</span>;
  })}</div>;
}
export function ResultCard({ result: r, custom = false, onLine }: { result: TestResult; custom?: boolean; onLine?: (line: number) => void }) {
  // Hidden results never render payloads, diagnostics or timing, even if a server accidentally sends them.
  if (!r.isSample && !custom) return <div className="ws-hidden-test"><LockKeyhole size={14} /><span>Test ẩn #{r.index}</span><strong>{verdictLabel[r.verdict] ?? r.verdict}</strong></div>;
  const mismatch = r.verdict !== 'Accepted' && r.expectedOutput != null && r.actualOutput !== r.expectedOutput;
  return <section className="ws-result-card"><header><strong>{custom ? 'Input của bạn' : `Ví dụ #${r.index}`}</strong><span className={`ws-badge ${r.verdict === 'Accepted' ? 'is-pass' : 'is-fail'}`}>{custom && r.verdict === 'Accepted' ? 'Đã thực thi' : verdictLabel[r.verdict] ?? r.verdict}</span><small>{r.runtimeMs} ms</small></header>
    <label>Đầu vào</label><pre>{r.input ?? '—'}</pre>{!custom && r.expectedOutput != null && <><label>Kết quả mong đợi</label><pre>{r.expectedOutput}</pre></>}<label>Kết quả của bạn</label><pre className={mismatch && !custom ? 'ws-diff' : ''}>{mismatch && !custom && r.actualOutput ? r.actualOutput.split('\n').map((line, i) => <span key={i}>{line === r.expectedOutput?.split('\n')[i] ? line : <mark>{line || '↵'}</mark>}{i < r.actualOutput!.split('\n').length - 1 ? '\n' : ''}</span>) : r.actualOutput ?? '—'}</pre>
    {r.errorMessage && <pre className="ws-error">{r.errorMessage}</pre>}{r.line != null && <button onClick={() => onLine?.(r.line!)}>Đến dòng {r.line}</button>}</section>;
}
