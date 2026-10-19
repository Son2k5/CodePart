import { useEffect, useRef, useState } from 'react';
import Markdown from 'react-markdown';
import rehypeSanitize from 'rehype-sanitize';
import { toast } from 'sonner';
import axios from 'axios';
import { axiosInstance as api } from '../../lib/axios';
import type { TestResult } from '../code-judge/api';
import './assistant.css';
import { consumeHintStream } from './stream';
import { RagSources, type RagReference } from './RagSources';
import { HintStepper } from './HintStepper';

type Stage = 'Start' | 'InProgress' | 'LogicError' | 'CannotRun' | 'PartialPass';
type Hint = { stage: Stage; message: string; steps: string[]; suspectedLines: number[]; guidingQuestion?: string; level: number; guardStatus: string; references?: RagReference[] };
type Result = { interactionId: string; hint: Hint; remaining: number; unlockedLevel: number; code?: string };
const titles: Record<Stage, string> = { Start: 'Cùng hiểu đề nhé', InProgress: 'Bước tiếp theo gợi ý', LogicError: 'Cùng tìm điểm chưa đúng', CannotRun: 'Giải thích lỗi', PartialPass: 'Khám phá trường hợp còn thiếu' };
const errors: Record<string, string> = { AI_UNAVAILABLE: 'AI tạm thời chưa sẵn sàng. Em có thể dùng gợi ý cơ bản bên dưới.', QUOTA: 'Em đã dùng hết lượt gợi ý hôm nay.', RATE_LIMIT: 'Hãy đợi một chút trước khi hỏi tiếp.', LEVEL_LOCKED: 'Hãy thử sửa bài: cấp tiếp theo mở sau 2 lần sai hoặc 3 phút.', GUARD_FALLBACK: 'Trợ giảng đã chuyển sang gợi ý an toàn.', FORBIDDEN: 'Em cần tham gia lớp học để dùng trợ giảng.' };

export function AiAssistantPanel({ problemId, language, sourceCode, starterCode, results = [], onLines }: {
  problemId: string; language: string; sourceCode: string; starterCode: string; results?: TestResult[]; onLines: (lines: number[]) => void;
}) {
  const [result, setResult] = useState<Result>();
  const [resultCode, setResultCode] = useState('');
  const [streamed, setStreamed] = useState('');
  const [question, setQuestion] = useState('');
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState<boolean>();
  const [feedbackBusy, setFeedbackBusy] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const latestCode = useRef(sourceCode); latestCode.current = sourceCode;
  useEffect(() => () => controller.current?.abort(), []);
  const verdicts = results.map(r => r.verdict);
  const passed = verdicts.filter(v => v === 'Accepted').length;
  const inferred: Stage = verdicts.some(v => ['CompileError', 'RuntimeError'].includes(v)) ? 'CannotRun'
    : (passed > 0 && passed < results.length) || verdicts.some(v => ['TimeLimitExceeded', 'MemoryLimitExceeded'].includes(v)) ? 'PartialPass'
    : verdicts.includes('WrongAnswer') ? 'LogicError' : sourceCode.trim() === starterCode.trim() || sourceCode.trim().length < 8 ? 'Start' : 'InProgress';
  const currentResult = resultCode === sourceCode ? result : undefined;
  const stage = currentResult?.hint.stage ?? inferred;
  async function ask(type: string, level = result?.hint.level ?? 1) {
    setBusy(true); setFeedback(undefined); setStreamed('');
    controller.current?.abort(); controller.current = new AbortController();
    const requestedCode = sourceCode;
    try {
      const response = await api.post<ReadableStream<Uint8Array>>('/ai/assist', { problemId, language, sourceCode, type, level, question: question || null },
        { adapter: 'fetch', responseType: 'stream', headers: { Accept: 'text/event-stream' }, signal: controller.current.signal, timeout: 35000 });
      const data = await consumeHintStream<Result>(response.data, text => { if (latestCode.current === requestedCode) setStreamed(old => old + text); });
      if (latestCode.current !== requestedCode) { toast.info('Code đã thay đổi. Hãy hỏi lại để nhận gợi ý cho bản mới.'); return; }
      setResult(data); setResultCode(requestedCode); onLines(data.hint.suspectedLines);
      if (data.code) toast.info(errors[data.code] ?? data.code);
    } catch (e) {
      if (!axios.isCancel(e)) {
        let code: string | undefined;
        if (axios.isAxiosError(e)) {
          const body = e.response?.data;
          if (body instanceof ReadableStream) {
            try { code = (await new Response(body).json()).code; } catch { /* Empty proxy error response. */ }
          } else code = body?.code;
        }
        if (code === 'AI_QUOTA_EXCEEDED' || code === 'QUOTA') toast.error('Em đã dùng hết lượt gợi ý hôm nay.');
        else toast.error(errors[code ?? ''] ?? 'Chưa nhận được gợi ý. Vui lòng thử lại.');
      }
    } finally { setBusy(false); }
  }
  async function rate(helpful: boolean) {
    if (!result) return;
    setFeedbackBusy(true);
    try { await api.post(`/ai/${result.interactionId}/feedback`, { helpful }); setFeedback(helpful); }
    catch { toast.error('Chưa lưu được đánh giá.'); }
    finally { setFeedbackBusy(false); }
  }
  return <aside className="ai-assistant" aria-label="Trợ giảng AI" aria-busy={busy}>
    <header><span className="ai-spark">✦</span><div><small>TRỢ GIẢNG AI</small><h2>{titles[stage]}</h2></div>{result && <span className="ai-quota">Còn {result.remaining} lượt</span>}</header>
    <p className="ai-intro">Mỗi gợi ý là một bước để em tự tìm ra lời giải.</p>
    <HintStepper unlockedLevel={result?.unlockedLevel ?? 0} currentLevel={result?.hint.level} busy={busy} remaining={result?.remaining} onSelect={level => void ask('Hint', level)} />
    {stage === 'PartialPass' && <div className="ai-tests" aria-label="Kết quả test">{results.map((r, i) => <span key={i} title={r.verdict} className={r.verdict === 'Accepted' ? 'pass' : 'fail'}>{i + 1} {r.verdict === 'Accepted' ? '✓' : '×'}</span>)}</div>}
    <div aria-live="polite" className="ai-answer">{busy ? <p>{streamed || 'Trợ giảng đang đọc bài và kiểm tra gợi ý…'}</p> : currentResult && result && <>
      <Markdown skipHtml rehypePlugins={[rehypeSanitize]}>{result.hint.message}</Markdown>
      <ol>{result.hint.steps.map((step, i) => <li key={i}><Markdown skipHtml rehypePlugins={[rehypeSanitize]}>{step}</Markdown></li>)}</ol>
      {result.hint.guidingQuestion && <blockquote>{result.hint.guidingQuestion}</blockquote>}
      <RagSources sources={result.hint.references ?? []} label="Học liệu tham khảo cho gợi ý" />
      {result.hint.suspectedLines.length > 0 && <button onClick={() => onLines(result.hint.suspectedLines)}>Xem dòng {result.hint.suspectedLines.join(', ')}</button>}
    </>}</div>
    {(stage === 'LogicError' || stage === 'PartialPass') && <button disabled={busy} onClick={() => void ask('TraceTestCase')}>Hướng dẫn từng bước với sample sai</button>}
    {stage === 'CannotRun' && <button disabled={busy} onClick={() => void ask('ExplainError')}>Giải thích lỗi này</button>}
    <form onSubmit={e => { e.preventDefault(); void ask('Ask'); }}><label htmlFor="ai-question">Em đang vướng ở đâu?</label><textarea id="ai-question" maxLength={2000} value={question} onChange={e => setQuestion(e.target.value)} placeholder="Ví dụ: Vì sao vòng lặp này không dừng?" /><button disabled={busy || !question.trim() || result?.remaining === 0}>Hỏi trợ giảng</button></form>
    {result && <footer><span>Gợi ý này có hữu ích không?</span><button disabled={feedbackBusy} aria-label="Hữu ích" aria-pressed={feedback === true} onClick={() => void rate(true)}>👍</button><button disabled={feedbackBusy} aria-label="Chưa hữu ích" aria-pressed={feedback === false} onClick={() => void rate(false)}>👎</button></footer>}
  </aside>;
}
