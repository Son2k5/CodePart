import { useEffect, useRef, useState } from 'react';
import Markdown from 'react-markdown';
import rehypeSanitize from 'rehype-sanitize';
import { toast } from 'sonner';
import axios from 'axios';
import { axiosInstance as api } from '../../lib/axios';
import type { TestResult } from '../code-judge/api';
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
  return <aside className="mt-[20px] p-6 border border-[#e4def5] rounded-[20px] bg-gradient-to-br from-[#fcfaff] to-[#f0f9ff] text-[#334155] shadow-[0_5px_24px_#64748b08]" aria-label="Trợ giảng AI" aria-busy={busy}>
    <header className="flex items-center gap-[12px] flex-wrap"><span className="text-[28px] text-[#8b7ac0] select-none">✦</span><div><small className="tracking-[0.12em] text-[#7c6bb0] text-[10px] font-bold block">TRỢ GIẢNG AI</small><h2 className="text-[19px] my-[3px] font-semibold">{titles[stage]}</h2></div>{result && <span className="ml-auto text-xs text-[#64748b]">Còn {result.remaining} lượt</span>}</header>
    <p className="text-[14px] text-[#64748b] mt-1 mb-3">Mỗi gợi ý là một bước để em tự tìm ra lời giải.</p>
    <HintStepper unlockedLevel={result?.unlockedLevel ?? 0} currentLevel={result?.hint.level} busy={busy} remaining={result?.remaining} onSelect={level => void ask('Hint', level)} />
    {stage === 'PartialPass' && <div className="flex flex-wrap gap-[6px] mt-[14px]" aria-label="Kết quả test">{results.map((r, i) => <span key={i} title={r.verdict} className={`px-[10px] py-[5px] rounded-[7px] text-xs font-medium ${r.verdict === 'Accepted' ? 'bg-[#dcf5e7] text-[#276749]' : 'bg-[#ffedd5] text-[#9a571c]'}`}>{i + 1} {r.verdict === 'Accepted' ? '✓' : '×'}</span>)}</div>}
    <div aria-live="polite" className="my-4 leading-[1.75] [overflow-wrap:anywhere] [&_pre]:overflow-auto [&_blockquote]:border-l-[3px] [&_blockquote]:border-l-[#baa4e1] [&_blockquote]:pl-[14px]">{busy ? <p>{streamed || 'Trợ giảng đang đọc bài và kiểm tra gợi ý…'}</p> : currentResult && result && <>
      <Markdown skipHtml rehypePlugins={[rehypeSanitize]}>{result.hint.message}</Markdown>
      <ol className="list-decimal pl-5 space-y-1">{result.hint.steps.map((step, i) => <li key={i}><Markdown skipHtml rehypePlugins={[rehypeSanitize]}>{step}</Markdown></li>)}</ol>
      {result.hint.guidingQuestion && <blockquote className="my-2 italic text-[#5b507a]">{result.hint.guidingQuestion}</blockquote>}
      <RagSources sources={result.hint.references ?? []} label="Học liệu tham khảo cho gợi ý" />
      {result.hint.suspectedLines.length > 0 && <button onClick={() => onLines(result.hint.suspectedLines)} className="mt-2 px-[14px] py-[9px] rounded-[10px] bg-white border border-[#ddd6ee] text-[#66538c] text-sm cursor-pointer hover:bg-[#f6f2fc]">Xem dòng {result.hint.suspectedLines.join(', ')}</button>}
    </>}</div>
    {(stage === 'LogicError' || stage === 'PartialPass') && <button disabled={busy} onClick={() => void ask('TraceTestCase')} className="mt-2 px-[14px] py-[9px] rounded-[10px] bg-white border border-[#ddd6ee] text-[#66538c] text-sm cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed hover:bg-[#f6f2fc]">Hướng dẫn từng bước với sample sai</button>}
    {stage === 'CannotRun' && <button disabled={busy} onClick={() => void ask('ExplainError')} className="mt-2 px-[14px] py-[9px] rounded-[10px] bg-white border border-[#ddd6ee] text-[#66538c] text-sm cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed hover:bg-[#f6f2fc]">Giải thích lỗi này</button>}
    <form onSubmit={e => { e.preventDefault(); void ask('Ask'); }} className="grid gap-2 mt-[18px]"><label htmlFor="ai-question" className="text-[13px] font-medium text-[#475569]">Em đang vướng ở đâu?</label><textarea id="ai-question" maxLength={2000} value={question} onChange={e => setQuestion(e.target.value)} placeholder="Ví dụ: Vì sao vòng lặp này không dừng?" className="border border-[#ddd6ee] rounded-[12px] p-[12px] bg-white/70 text-sm focus:outline-none focus:border-[#8b7ac0] resize-y min-h-[75px]" /><button disabled={busy || !question.trim() || result?.remaining === 0} className="justify-self-end px-[14px] py-[9px] rounded-[10px] bg-white border border-[#ddd6ee] text-[#66538c] text-sm cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed hover:bg-[#f6f2fc]">Hỏi trợ giảng</button></form>
    {result && <footer className="flex items-center gap-[12px] flex-wrap mt-4 text-xs text-[#64748b]"><span>Gợi ý này có hữu ích không?</span><button disabled={feedbackBusy} aria-label="Hữu ích" aria-pressed={feedback === true} onClick={() => void rate(true)} className={`px-[14px] py-[9px] rounded-[10px] border cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed transition-colors ${feedback === true ? 'bg-[#e9e2fa] border-[#b4a2d3]' : 'bg-white border-[#ddd6ee] hover:bg-[#f6f2fc]'}`}>👍</button><button disabled={feedbackBusy} aria-label="Chưa hữu ích" aria-pressed={feedback === false} onClick={() => void rate(false)} className={`px-[14px] py-[9px] rounded-[10px] border cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed transition-colors ${feedback === false ? 'bg-[#e9e2fa] border-[#b4a2d3]' : 'bg-white border-[#ddd6ee] hover:bg-[#f6f2fc]'}`}>👎</button></footer>}
  </aside>;
}
