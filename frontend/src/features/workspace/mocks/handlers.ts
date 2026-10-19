import { delay, http, HttpResponse } from 'msw';
import { demoProblem, makeResults } from './fixtures';
import type { Submission } from '../../code-judge/api';
const base = import.meta.env.VITE_API_BASE_URL || '/api';
const user = { id: 'workspace-demo', email: 'demo@codepath.local', fullName: 'Bạn nhỏ', role: 'Student', status: 'Active' };
const submissions = new Map<string, { value: Submission; started: number; scenario: string }>();
const scenario = () => sessionStorage.getItem('codepath:mock:verdict') ?? 'Accepted';
const aiScenario = () => sessionStorage.getItem('codepath:mock:ai') ?? 'Start';
const errorResponse = (code: string, status = 429) => HttpResponse.json({ code, detail: ({ JUDGE_BUSY: 'Máy chấm đang bận, thử lại sau một chút nhé.', RATE_LIMITED: 'Bạn chạy hơi nhanh. Đợi một chút rồi thử lại nhé.', SUBMISSION_IN_PROGRESS: 'Bài trước đang được chấm.', AI_UNAVAILABLE: 'Trợ giảng tạm thời chưa sẵn sàng.', AI_QUOTA_EXCEEDED: 'Bạn đã dùng hết lượt gợi ý hôm nay.' } as Record<string, string>)[code] }, { status });
function readSubmission(id: string) {
  const item = submissions.get(id); if (!item) return;
  const elapsed = Date.now() - item.started;
  const queueTime = item.scenario === 'LongQueue' ? 14000 : 1600;
  if (elapsed < queueTime) return { ...item.value, status: 'Pending', queuePosition: Math.max(1, Math.ceil((queueTime - elapsed) / 1000)), results: [], passedCount: 0 };
  if (elapsed < queueTime + 5000) {
    const results = item.value.results.slice(0, Math.floor((elapsed - queueTime) / 5000 * 15));
    return { ...item.value, status: 'Running', results, passedCount: results.filter(r => r.verdict === 'Accepted').length };
  }
  return item.value;
}
export const workspaceHandlers = [
  http.post(`${base}/auth/refresh-token`, () => HttpResponse.json({ accessToken: 'workspace-demo-token', expiresIn: 3600, tokenType: 'Bearer', user })),
  http.get(`${base}/auth/me`, () => HttpResponse.json(user)),
  http.post(`${base}/auth/refresh-token/logout`, () => HttpResponse.json({ message: 'OK' })),
  http.get(`${base}/problems/scopes`, () => HttpResponse.json([{ id: 'demo-class', name: 'Lập trình căn bản', kind: 'class' }])),
  http.get(`${base}/problems/`, () => HttpResponse.json({ items: [demoProblem, { ...demoProblem, id: 'demo-next', title: 'Luyện tập: Tổng hai số' }], total: 2 })),
  http.get(`${base}/problems/:id/submissions`, ({ params, request }) => {
    const items = [...submissions.keys()].map(id => readSubmission(id)!).filter(s => s.problemId === params.id).reverse();
    const page = Number(new URL(request.url).searchParams.get('page') || 1);
    return HttpResponse.json({ items: items.slice((page - 1) * 20, page * 20), total: items.length });
  }),
  http.get(`${base}/problems/:id`, async ({ params }) => { await delay(250); return HttpResponse.json({ ...demoProblem, id: params.id, title: params.id === 'demo-next' ? 'Luyện tập: Tổng hai số' : demoProblem.title }); }),
  http.post(`${base}/problems/:id/run`, async ({ request }) => {
    await delay(800); const selected = scenario();
    if (['JUDGE_BUSY', 'RATE_LIMITED', 'SUBMISSION_IN_PROGRESS'].includes(selected)) return errorResponse(selected);
    const body = await request.json() as { customInput?: string };
    const results = makeResults(selected === 'LongQueue' ? 'Accepted' : selected, 2);
    if (body.customInput !== undefined) {
      const numbers = body.customInput.trim().split(/\s+/).map(Number);
      return HttpResponse.json({ results: [{ index: 1, isSample: true, verdict: selected === 'Accepted' ? 'Accepted' : selected, runtimeMs: 10, input: body.customInput, actualOutput: numbers.length === 2 && numbers.every(Number.isFinite) ? String(numbers[0] + numbers[1]) : 'Mock: nhập hai số nguyên.' }] });
    }
    return HttpResponse.json({ results });
  }),
  http.post(`${base}/problems/:id/submit`, async ({ params, request }) => {
    await delay(450); const selected = scenario();
    if (['JUDGE_BUSY', 'RATE_LIMITED', 'SUBMISSION_IN_PROGRESS'].includes(selected)) return errorResponse(selected);
    const body = await request.json() as { sourceCode: string; language: string };
    const id = crypto.randomUUID(); const verdict = selected === 'LongQueue' ? 'Accepted' : selected;
    const results = makeResults(verdict); const passedCount = results.filter(r => r.verdict === 'Accepted').length;
    submissions.set(id, { started: Date.now(), scenario: selected, value: { id, problemId: String(params.id), status: verdict === 'PartialPass' ? 'WrongAnswer' : verdict, language: body.language, sourceCode: body.sourceCode, createdAt: new Date().toISOString(), passedCount, totalCount: 15, score: Math.round(passedCount / 15 * 100), awardedPoints: passedCount === 15 ? 100 : 0, results, runtimeMs: 24, memoryKb: 10240, ...(verdict === 'CompileError' ? { compileOutput: 'Dòng 5: biến chưa được khai báo. Kiểm tra tên biến nhé.' } : {}) } });
    return HttpResponse.json({ submissionId: id, queuePosition: selected === 'LongQueue' ? 14 : 2 }, { status: 202 });
  }),
  http.get(`${base}/submissions/:id`, ({ params }) => { const value = readSubmission(String(params.id)); return value ? HttpResponse.json(value) : HttpResponse.json({ code: 'NOT_FOUND' }, { status: 404 }); }),
  http.get(`${base}/classes/:id/topics/:slug`, () => HttpResponse.json({ id: 'demo-topic', title: 'Làm quen với nhập xuất', exercises: [{ problemId: 'demo-sum', status: 'Published' }, { problemId: 'demo-next', status: 'Published' }] })),
  http.post(`${base}/ai/assist`, async ({ request }) => {
    const selected = aiScenario();
    if (selected.startsWith('AI_')) return errorResponse(selected, selected === 'AI_UNAVAILABLE' ? 503 : 429);
    const body = await request.json() as { level?: number };
    const level = body.level ?? 1;
    if (level > 1 && selected === 'Locked') return errorResponse('LEVEL_LOCKED', 403);
    const stage = selected === 'Locked' ? 'Start' : selected;
    const message = ({ Start: 'Mình bắt đầu từ đề bài nhé! 🌱\n\nĐề cho **hai số nguyên**, và yêu cầu tìm **tổng** của chúng. Bạn sẽ dùng phép toán nào?', InProgress: 'Bạn đã có bước đọc dữ liệu rồi! Hãy kiểm tra xem đã xuất kết quả ra màn hình chưa nhé.', LogicError: 'Thử tự tính với ví dụ `2 3`. Bạn mong nhận được kết quả gì? So sánh phép toán trong code với yêu cầu đề nhé.', CannotRun: 'Mình cùng đọc thông báo lỗi nhé. Kiểm tra tên biến ở dòng 5: biến đó đã được khai báo trước khi sử dụng chưa?', PartialPass: 'Bạn đã làm đúng nhiều test rồi! Thử kiểm tra với **số âm**, **số 0** và số gần giới hạn của đề nhé.' } as Record<string, string>)[stage] ?? 'Hãy kiểm tra từng bước đọc, xử lý và xuất dữ liệu nhé.';
    const result = { interactionId: crypto.randomUUID(), remaining: 5, unlockedLevel: selected === 'Locked' ? 0 : 3, hint: { stage, message, steps: ['Đọc hai giá trị từ đầu vào.', 'Xác định phép tính cần thực hiện.', 'Thử lại với cả hai ví dụ.'].slice(0, level), suspectedLines: ['CannotRun', 'LogicError'].includes(stage) ? [5] : [], level, guardStatus: 'Safe', guidingQuestion: 'Bạn muốn thử sửa bước nào trước?' } };
    const encoder = new TextEncoder();
    const stream = new ReadableStream({ async start(controller) {
      for (const part of message.match(/.{1,24}/gs) ?? []) { await delay(65); controller.enqueue(encoder.encode(`event: delta\ndata: ${JSON.stringify(part)}\n\n`)); }
      controller.enqueue(encoder.encode(`event: result\ndata: ${JSON.stringify(result)}\n\n`)); controller.close();
    } });
    return new HttpResponse(stream, { headers: { 'Content-Type': 'text/event-stream' } });
  }),
  http.post(`${base}/ai/:id/feedback`, () => HttpResponse.json({ success: true })),
];
