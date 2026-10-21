import { lazy, Suspense, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useStore } from 'zustand';
import * as Tabs from '@radix-ui/react-tabs';
import { Group, Panel, Separator, useDefaultLayout, usePanelRef } from 'react-resizable-panels';
import { ArrowLeft, ArrowRight, BookOpen, Check, ChevronDown, ChevronLeft, ChevronRight, Code2, History, Keyboard, Maximize2, Minimize2, Play, RotateCcw, Send, Sparkles, Terminal, Trophy } from 'lucide-react';
import Markdown from 'react-markdown';
import rehypeSanitize from 'rehype-sanitize';
import { toast } from 'sonner';
import { active, judgeApi, judgeError, problemScope, useSubmissionStatus, type Problem, type TestResult } from '../code-judge/api';
import { useAuthStore } from '../auth/store/authStore';
import { axiosInstance } from '../../lib/axios';
import { createWorkspaceStore, draftKey, type WorkspaceStore } from './store';
import { Empty, Loading, Mascot, Modal, PanelBoundary } from './ui';
import { ResultCard, TestGrid, VerdictBanner, verdictLabel } from './Results';
import type { Diagnostic, EditorHandle } from './CodeEditor';
import './workspace.css';
const AiAssistantPanel = lazy(() => import('../ai-assistant/AiAssistantPanel').then(m => ({ default: m.AiAssistantPanel })));
const CodeEditor = lazy(() => import('./CodeEditor'));
const MockControls = lazy(() => import('./mocks/MockControls'));
const noLines: number[] = [];
const safeLayoutStorage = { getItem: (key: string) => { try { return localStorage.getItem(key); } catch { return null; } }, setItem: (key: string, value: string) => { try { localStorage.setItem(key, value); } catch { /* Layout preference is optional. */ } } };
type TopicContext = { id: string; title: string; dueAt?: string; exercises: { problemId: string; status: string }[] };
type Snapshot = { code: string; language: string; custom: boolean };

export function WorkspacePage() {
  const params = useParams(); const id = params.problemId ?? params.id ?? '';
  const query = useQuery({ queryKey: ['judge-problem', id], queryFn: () => judgeApi.problem(id) });
  if (query.isPending) return <main className="workspace ws-page"><Loading /></main>;
  if (query.isError) return <main className="workspace ws-page"><Empty title="Chưa mở được bài tập">Kiểm tra kết nối hoặc quyền truy cập lớp học.</Empty><div className="ws-center"><button onClick={() => void query.refetch()}>Thử lại</button><Link to="/problems">Về danh sách bài</Link></div></main>;
  return <PanelBoundary key={id}><Workspace problem={query.data} /></PanelBoundary>;
}

function SaveStatus({ store, draftId }: { store: WorkspaceStore; draftId: string }) {
  const draft = useStore(store, s => s.drafts[draftId]);
  return <span className="ws-save" role="status">{draft?.error ?? (draft?.savedAt ? <><Check size={12} /> Đã lưu {new Date(draft.savedAt).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })}</> : 'Tự động lưu nháp')}</span>;
}
function Assistant({ store, draftId, problem, language, snapshot, results, onLines }: { store: WorkspaceStore; draftId: string; problem: Problem; language: string; snapshot?: Snapshot; results?: TestResult[]; onLines: (lines: number[]) => void }) {
  const code = useStore(store, s => s.drafts[draftId]?.code ?? '');
  return <><div className="ws-assistant-intro"><Mascot /><div><span className="ws-eyebrow">BẠN ĐỒNG HÀNH NHỎ</span><h2>Gỡ rối cùng Mầm</h2><p>Một gợi ý nhỏ, một bước tiến mới.</p></div></div><Suspense fallback={<Loading text="Đang chuẩn bị trợ giảng AI…" />}><AiAssistantPanel key={draftId} problemId={problem.id} language={language} sourceCode={code} starterCode={problem.starterCodes.find(s => s.language === language)?.template ?? ''} results={snapshot?.code === code && snapshot.language === language ? results : undefined} onLines={onLines} /></Suspense></>;
}

function Workspace({ problem: p }: { problem: Problem }) {
  const { classId, slug } = useParams(); const navigate = useNavigate();
  const user = useAuthStore(s => s.user);
  const [store] = useState(() => createWorkspaceStore());
  const [isMobile, setIsMobile] = useState(() => matchMedia('(max-width: 1023px)').matches);
  useEffect(() => { const media = matchMedia('(max-width: 1023px)'); const update = () => setIsMobile(media.matches); media.addEventListener('change', update); return () => media.removeEventListener('change', update); }, []);
  const phase = useStore(store, s => s.phase);
  const busy = ['running', 'submitting', 'queued', 'judging'].includes(phase);
  const [language, setLanguage] = useState(p.starterCodes[0]?.language ?? 'python');
  const draftId = draftKey(user?.id ?? 'guest', p.id, language);
  const starter = p.starterCodes.find(s => s.language === language)?.template ?? '';
  const [leftTab, setLeftTab] = useState('description');
  const [mobileTab, setMobileTab] = useState('description');
  const [consoleTab, setConsoleTab] = useState('testcase');
  const [custom, setCustom] = useState(false); const [input, setInput] = useState('');
  const [selectedSample, setSelectedSample] = useState(0);
  const [fontSize, setFontSize] = useState(14);
  const [fullscreen, setFullscreen] = useState(false);
  const [resetOpen, setResetOpen] = useState(false); const [shortcutsOpen, setShortcutsOpen] = useState(false);
  const [collapsed, setCollapsed] = useState(false);
  const [submissionId, setSubmissionId] = useState<string>();
  const [snapshot, setSnapshot] = useState<Snapshot>();
  const [runResults, setRunResults] = useState<TestResult[]>();
  const [suspected, setSuspected] = useState<number[]>(noLines);
  const [hintCode, setHintCode] = useState<string>();
  const [historyPage, setHistoryPage] = useState(1);
  const editor = useRef<EditorHandle | null>(null);
  const onEditorReady = useCallback((value: EditorHandle | null) => { editor.current = value; }, []);
  const consolePanel = usePanelRef();
  const horizontal = useDefaultLayout({ id: 'codepath:workspace:horizontal', storage: safeLayoutStorage, onlySaveAfterUserInteractions: true });
  const vertical = useDefaultLayout({ id: 'codepath:workspace:vertical', storage: safeLayoutStorage, onlySaveAfterUserInteractions: true });
  const cache = useQueryClient(); const scope = problemScope(p);
  const scopes = useQuery({ queryKey: ['judge-scopes'], queryFn: () => judgeApi.scopes() });
  const peers = useQuery({ queryKey: ['judge-list', 1, scope], queryFn: () => judgeApi.list(false, 1, scope) });
  const topic = useQuery({ queryKey: ['workspace-topic', classId, slug], enabled: !!classId && !!slug, queryFn: async () => (await axiosInstance.get<TopicContext>(`/classes/${classId}/topics/${slug}`)).data });
  const history = useQuery({ queryKey: ['judge-history', p.id, historyPage], queryFn: () => judgeApi.history(p.id, historyPage) });
  const status = useSubmissionStatus(submissionId); const s = status.data;
  const className = scopes.data?.find(value => `${value.kind}:${value.id}` === scope)?.name ?? 'Góc luyện tập';
  const siblings = classId && slug ? topic.data?.exercises.map(e => e.problemId) ?? [] : peers.data?.items.map(item => item.id) ?? [];
  const position = siblings.indexOf(p.id);
  const problemPath = (id: string) => classId && slug ? `/classes/${classId}/topics/${slug}/problems/${id}` : `/problems/${id}`;
  const samples = p.testCases.filter(t => t.isSample);
  const sample = samples[selectedSample];
  const revealResults = () => { setConsoleTab('result'); setMobileTab('result'); setCollapsed(false); consolePanel.current?.expand(); };
  useEffect(() => { store.getState().open(draftId, starter); }, [store, draftId, starter]);
  useEffect(() => {
    const running = history.data?.items.find(item => active(item.status));
    if (running && !submissionId) { setSubmissionId(running.id); store.getState().transition('queued'); }
  }, [history.data, submissionId, store]);
  useEffect(() => {
    if (!s) return;
    store.getState().transition(active(s.status) ? ['Pending', 'Queued'].includes(s.status) ? 'queued' : 'judging' : 'done');
    if (!active(s.status)) void cache.invalidateQueries({ queryKey: ['judge-history', p.id] });
  }, [s?.status, s?.id, store, cache, p.id]);
  // A disconnected status request is not proof that the judge stopped. Keep submit locked until retry succeeds.
  const onError = (error: unknown) => { store.getState().transition('error'); judgeError(error); };
  const run = useMutation({ mutationFn: (request: Snapshot) => judgeApi.run(p.id, request.language, request.code, request.custom ? input : undefined), onError,
    onSuccess: data => { setRunResults(data.results); store.getState().transition('done'); revealResults(); } });
  const submit = useMutation({ mutationFn: (request: Snapshot) => judgeApi.submit(p.id, request.language, request.code, topic.data?.id), onError,
    onSuccess: data => { setSubmissionId(data.submissionId); store.getState().transition('queued'); revealResults(); void cache.invalidateQueries({ queryKey: ['judge-history', p.id] }); } });
  const execute = (kind: 'run' | 'submit') => {
    const code = store.getState().drafts[draftId]?.code ?? starter;
    if (!code.trim()) { toast.info('Viết một chút code trước khi chạy nhé.'); return; }
    if (classId && slug && !topic.data) { toast.info('Cần tải xong bài học trước khi chạy hoặc nộp.'); return; }
    if (!store.getState().begin(kind === 'run' ? 'running' : 'submitting')) return;
    store.getState().save(draftId); setSubmissionId(undefined); setRunResults(undefined); setSuspected(noLines);
    const request = { code, language, custom: kind === 'run' && custom }; setSnapshot(request); revealResults();
    if (kind === 'run') run.mutate(request); else submit.mutate(request);
  };
  const actions = useRef({ execute, draftId }); actions.current = { execute, draftId };
  useEffect(() => {
    const handler = (event: KeyboardEvent) => {
      if (document.querySelector('[role="dialog"]')) return;
      if (event.metaKey || event.ctrlKey) {
        if (event.key === "'") { event.preventDefault(); actions.current.execute('run'); }
        if (event.key === 'Enter') { event.preventDefault(); actions.current.execute('submit'); }
        if (event.key.toLowerCase() === 's') { event.preventDefault(); store.getState().save(actions.current.draftId); }
      } else if (event.key === '?' && !(event.target as HTMLElement).closest('input,textarea,[contenteditable="true"],.monaco-editor')) { event.preventDefault(); setShortcutsOpen(true); }
      if (event.key === 'Escape') setFullscreen(false);
    };
    window.addEventListener('keydown', handler, true); return () => window.removeEventListener('keydown', handler, true);
  }, [store]);
  const currentDiagnostics = useMemo<Diagnostic[]>(() => (runResults ?? s?.results ?? []).filter(r => r.isSample && r.line && r.errorMessage).map(r => ({ line: r.line!, column: r.column, message: r.errorMessage! })), [runResults, s?.results]);
  const jumpToLine = (line: number) => { setMobileTab('code'); requestAnimationFrame(() => { editor.current?.revealLineInCenter(line); editor.current?.setPosition({ lineNumber: line, column: 1 }); editor.current?.focus(); }); };
  const highlightLines = (lines: number[]) => { setHintCode(store.getState().drafts[draftId]?.code); setSuspected(lines); if (lines[0]) jumpToLine(lines[0]); };
  const executionActions = <><button className="ws-run" disabled={busy} onClick={() => execute('run')} title="Ctrl/Cmd + '"><Play size={16} />{phase === 'running' ? 'Đang chạy…' : 'Chạy thử'}</button><button className="ws-submit" disabled={busy} onClick={() => execute('submit')} title="Ctrl/Cmd + Enter"><Send size={15} />{busy && phase !== 'running' ? 'Đang chấm…' : 'Nộp bài'}</button></>;
  const testcaseContent = <><div className="ws-sample-tabs">{samples.map((_, index) => <button key={index} aria-pressed={!custom && selectedSample === index} onClick={() => { setCustom(false); setSelectedSample(index); }}>Ví dụ {index + 1}</button>)}<button aria-pressed={custom} onClick={() => setCustom(true)}>+ Input của bạn</button></div>{custom ? <><label htmlFor="workspace-custom">Đầu vào tùy chỉnh</label><textarea id="workspace-custom" value={input} onChange={e => setInput(e.target.value)} placeholder="Nhập dữ liệu theo định dạng của đề bài…" /><p className="ws-muted">Chạy với input của bạn để xem đầu ra thực tế.</p></> : sample ? <><label>Đầu vào</label><pre>{sample.input}</pre><label>Kết quả mong đợi</label><pre>{sample.expectedOutput}</pre></> : <Empty title="Chưa có ví dụ">Bạn có thể thêm input để chạy thử.</Empty>}</>;
  const resultContent = <div className="ws-results">
    {status.isError && <div className="ws-inline-error" role="alert">Chưa nhận được trạng thái. Bài vẫn có thể đang được chấm.<button onClick={() => void status.refetch()}>Kết nối lại</button></div>}
    {phase === 'error' && <div className="ws-inline-error" role="alert">Chưa thực hiện được. Kiểm tra thông báo và thử lại nhé.</div>}
    {s ? <><VerdictBanner submission={s} /><div className="ws-result-meta"><span>{s.passedCount}/{s.totalCount} test đúng</span><span>{(s.maxRuntimeMs ?? s.runtimeMs) != null ? `${s.maxRuntimeMs ?? s.runtimeMs} ms` : ''}{s.memoryKb != null ? ` · ${s.memoryKb} KB` : ''}</span></div><progress aria-label="Tiến độ chấm bài" max={s.totalCount || 1} value={s.results.length} /><TestGrid total={s.totalCount} results={s.results} />{s.compileOutput && <pre className="ws-error">{s.compileOutput}</pre>}{s.results.map(result => <ResultCard key={result.index} result={result} onLine={jumpToLine} />)}
      {s.status === 'Accepted' && <div className="ws-next"><strong>Một bước tiến thật xinh! 🌱</strong>{s.latePenalty != null && <p>Điểm trừ do nộp muộn: {s.latePenalty}</p>}<div>{position >= 0 && siblings[position + 1] && <Link to={problemPath(siblings[position + 1])}>Bài tiếp theo <ArrowRight size={15} /></Link>}{classId && <Link to={`/classes/${classId}/learning`}>Về bài giảng</Link>}</div></div>}</>
      : busy ? <Loading text={phase === 'running' ? 'Code của bạn đang chạy…' : 'Đang gửi bài của bạn…'} />
        : runResults ? <><div className="ws-console-heading"><Check size={16} /><strong>Đã chạy xong</strong><span>{snapshot?.custom ? 'Input tùy chỉnh' : `${runResults.filter(r => r.verdict === 'Accepted').length}/${runResults.length} ví dụ đúng`}</span></div>{runResults.map(result => <ResultCard key={result.index} result={result} custom={snapshot?.custom} onLine={jumpToLine} />)}</>
          : <Empty title="Thử một ý tưởng nhỏ nào">Chạy code để xem kết quả. Mầm ở đây cùng bạn!</Empty>}
  </div>;
  return <main className={`workspace ws-page ${fullscreen ? 'ws-focus-mode' : ''}`}>
    {import.meta.env.DEV && import.meta.env.VITE_USE_MOCK === 'true' && <Suspense><MockControls /></Suspense>}
    <header className="ws-topbar"><Link to="/" className="ws-brand"><span><Code2 size={23} /></span>CodePath<span className="ws-brand-dot">.</span></Link><div className="ws-breadcrumb"><Link to="/problems">{className}</Link><ChevronRight size={13} />{topic.data && <><span>{topic.data.title}</span><ChevronRight size={13} /></>}<strong>{p.title}</strong></div><div className="ws-top-actions">{executionActions}</div><span className="ws-avatar" title={user?.fullName}>{user?.fullName?.split(' ').at(-1)?.[0] ?? 'B'}</span></header>
    <div className="ws-welcome"><div><Link to="/problems" className="ws-back"><ArrowLeft size={14} /> Danh sách bài tập</Link><h1>Mỗi dòng code, một bước tiến<span> 🌱</span></h1><p>Cứ thử, cứ sai, rồi bạn sẽ làm được.</p></div><div className="ws-welcome-right"><span className="ws-points"><Trophy size={16} /> {p.points} điểm</span>{topic.data?.dueAt && <span className={`ws-badge ${Date.parse(topic.data.dueAt) < Date.now() ? 'is-fail' : 'is-wait'}`}>Hạn: {new Date(topic.data.dueAt).toLocaleString('vi-VN')}</span>}<Mascot size={76} /></div></div>
    {topic.isError && <div className="ws-inline-error">Chưa tải được ngữ cảnh bài học.<button onClick={() => void topic.refetch()}>Thử lại</button></div>}
    <Group orientation="horizontal" className="ws-layout" {...horizontal}>
      <Panel id="problem" defaultSize="42%" minSize="28%" className={`ws-card ws-problem ${mobileTab === 'description' ? 'ws-mobile-active' : ''}`}>
        <Tabs.Root value={leftTab} onValueChange={setLeftTab} className="ws-tab-root"><Tabs.List className="ws-tabs" aria-label="Nội dung bài tập"><Tabs.Trigger value="description"><BookOpen size={15} />Đề bài</Tabs.Trigger><Tabs.Trigger value="assistant"><Sparkles size={15} />Trợ giảng AI</Tabs.Trigger><Tabs.Trigger value="history"><History size={15} />Bài đã nộp</Tabs.Trigger></Tabs.List>
          <Tabs.Content value="description" className="ws-panel-scroll"><div className="ws-problem-heading"><span className="ws-eyebrow">THỬ THÁCH HÔM NAY</span><div className="ws-problem-pager"><button aria-label="Bài trước" disabled={busy || position <= 0} onClick={() => navigate(problemPath(siblings[position - 1]))}><ChevronLeft size={16} /></button><button aria-label="Bài tiếp theo" disabled={busy || position < 0 || position >= siblings.length - 1} onClick={() => navigate(problemPath(siblings[position + 1]))}><ChevronRight size={16} /></button></div></div><h2>{p.title}</h2><div className="ws-problem-meta"><span className="ws-badge is-pass">{({ Easy: 'Dễ', Medium: 'Vừa sức', Hard: 'Thử thách' } as Record<string, string>)[p.difficulty] ?? p.difficulty}</span><span>◷ {p.timeLimitMs} ms</span><span>▤ {p.memoryLimitMb} MB</span></div><div className="ws-markdown"><Markdown skipHtml rehypePlugins={[rehypeSanitize]}>{p.description}</Markdown></div>{samples.map((test, index) => <section className="ws-example" key={test.id ?? index}><h3><span>{index + 1}</span>Ví dụ {index + 1}</h3><div><label>Đầu vào</label><pre>{test.input}</pre><label>Đầu ra</label><pre>{test.expectedOutput}</pre></div></section>)}<div className="ws-note"><Sparkles size={18} /><p>Bí ý tưởng? Ghé tab <strong>Trợ giảng AI</strong> để nhận một gợi ý nhỏ nhé.</p></div></Tabs.Content>
          <Tabs.Content value="assistant" className="ws-panel-scroll"><PanelBoundary>{leftTab === 'assistant' && <Assistant store={store} draftId={draftId} problem={p} language={language} snapshot={snapshot} results={runResults ?? s?.results} onLines={highlightLines} />}</PanelBoundary></Tabs.Content>
          <Tabs.Content value="history" className="ws-panel-scroll"><h2>Những lần bạn đã cố gắng</h2><p className="ws-muted">Mỗi lần thử đều giúp bạn tiến bộ hơn.</p>{history.isPending ? <Loading text="Đang tải bài đã nộp…" /> : history.isError ? <button onClick={() => void history.refetch()}>Tải lại lịch sử</button> : !history.data.items.length ? <Empty title="Hành trình bắt đầu từ đây">Bài nộp đầu tiên của bạn sẽ xuất hiện ở đây.</Empty> : history.data.items.map(item => <button className="ws-history" disabled={busy} key={item.id} onClick={() => { setSnapshot(undefined); setRunResults(undefined); setSubmissionId(item.id); revealResults(); }}><span><strong>{verdictLabel[item.status] ?? item.status}</strong><small>{item.language} · {new Date(item.createdAt).toLocaleString('vi-VN')}</small></span><span>{item.passedCount}/{item.totalCount}<ChevronRight size={15} /></span></button>)}<div className="ws-pagination"><button disabled={historyPage === 1} onClick={() => setHistoryPage(v => v - 1)}>Trước</button><span>Trang {historyPage}</span><button disabled={historyPage * 20 >= (history.data?.total ?? 0)} onClick={() => setHistoryPage(v => v + 1)}>Sau</button></div></Tabs.Content>
        </Tabs.Root>
      </Panel>
      <Separator className="ws-separator" aria-label="Đổi độ rộng đề bài và code" />
      <Panel id="work" minSize="35%" className={`ws-work ${mobileTab === 'code' ? 'ws-mobile-active' : ''}`}>
        <Group orientation="vertical" className="ws-editor-group" {...vertical}>
          <Panel id="editor" defaultSize="65%" minSize="30%" className="ws-card ws-editor-card"><div className="ws-editor-toolbar"><span className="ws-editor-label"><Code2 size={16} />Code của bạn</span><select aria-label="Ngôn ngữ lập trình" value={language} disabled={busy} onChange={e => { store.getState().save(draftId); setLanguage(e.target.value); setSuspected(noLines); }}>{p.starterCodes.map(value => <option key={value.language} value={value.language}>{({ python: 'Python', cpp: 'C++', java: 'Java', javascript: 'JavaScript' } as Record<string, string>)[value.language] ?? value.language}</option>)}</select><div className="ws-editor-tools"><select aria-label="Cỡ chữ code" value={fontSize} onChange={e => setFontSize(Number(e.target.value))}>{[12, 14, 16, 18, 20].map(size => <option key={size} value={size}>{size}px</option>)}</select><button className="ws-icon" title="Khôi phục code mẫu" aria-label="Khôi phục code mẫu" onClick={() => setResetOpen(true)}><RotateCcw size={15} /></button><button className="ws-icon" aria-label={fullscreen ? 'Thoát chế độ tập trung' : 'Chế độ tập trung'} onClick={() => setFullscreen(v => !v)}>{fullscreen ? <Minimize2 size={15} /> : <Maximize2 size={15} />}</button></div></div>
            <div className="ws-editor-body"><PanelBoundary key={draftId}><Suspense fallback={<Loading text="Đang mở trình soạn thảo…" />}><CodeEditor key={draftId} store={store} draftId={draftId} starter={starter} language={language} fontSize={fontSize} onReady={onEditorReady} judgedCode={snapshot?.code} hintCode={hintCode} diagnostics={snapshot?.language === language ? currentDiagnostics : []} suspectedLines={suspected} /></Suspense></PanelBoundary></div><div className="ws-editor-status"><SaveStatus store={store} draftId={draftId} /><button onClick={() => setShortcutsOpen(true)}><Keyboard size={13} />Phím tắt</button></div></Panel>
          <Separator className="ws-separator ws-separator-horizontal" aria-label="Đổi chiều cao code và kết quả" />
          <Panel id="console" panelRef={consolePanel} defaultSize="35%" minSize="18%" collapsible collapsedSize="48px" className="ws-card ws-console-card"><Tabs.Root value={consoleTab} onValueChange={value => { setConsoleTab(value); setCollapsed(false); consolePanel.current?.expand(); }} className="ws-tab-root"><div className="ws-console-bar"><Tabs.List className="ws-tabs" aria-label="Bảng chạy code"><Tabs.Trigger value="testcase"><Terminal size={14} />Testcase</Tabs.Trigger><Tabs.Trigger value="result"><Check size={14} />Kết quả</Tabs.Trigger></Tabs.List><button className="ws-icon" aria-label={collapsed ? 'Mở console' : 'Thu gọn console'} aria-expanded={!collapsed} onClick={() => { if (collapsed) consolePanel.current?.expand(); else consolePanel.current?.collapse(); setCollapsed(v => !v); }}><ChevronDown size={16} style={{ transform: collapsed ? 'rotate(180deg)' : undefined }} /></button></div><Tabs.Content value="testcase" className="ws-console-scroll">{!isMobile && testcaseContent}</Tabs.Content><Tabs.Content value="result" className="ws-console-scroll">{!isMobile && resultContent}</Tabs.Content></Tabs.Root></Panel>
        </Group>
      </Panel>
    </Group>
    {isMobile && <section className={`ws-card ws-mobile-results ${mobileTab === 'result' ? 'ws-mobile-active' : ''}`} aria-label="Kết quả chạy code"><Tabs.Root value={consoleTab} onValueChange={setConsoleTab} className="ws-tab-root"><Tabs.List className="ws-tabs" aria-label="Chạy thử trên di động"><Tabs.Trigger value="testcase">Testcase</Tabs.Trigger><Tabs.Trigger value="result">Kết quả</Tabs.Trigger></Tabs.List><Tabs.Content value="testcase" className="ws-console-scroll">{testcaseContent}</Tabs.Content><Tabs.Content value="result" className="ws-console-scroll">{resultContent}</Tabs.Content></Tabs.Root></section>}
    <footer className="ws-footer"><span><span className="ws-online-dot" /> Một chút kiên nhẫn, một chút tiến bộ.</span><button onClick={() => setShortcutsOpen(true)}><Keyboard size={13} />Phím tắt</button></footer>
    <div className="ws-mobile-dock"><div className="ws-mobile-actions">{executionActions}</div><nav aria-label="Không gian làm bài trên di động">{[{ id: 'description', title: 'Đề bài', Icon: BookOpen }, { id: 'code', title: 'Code', Icon: Code2 }, { id: 'result', title: 'Kết quả', Icon: Terminal }].map(({ id, title, Icon }) => <button key={id} aria-current={mobileTab === id ? 'page' : undefined} onClick={() => setMobileTab(id)}><Icon size={18} />{title}</button>)}</nav></div>
    <Modal open={resetOpen} onOpenChange={setResetOpen} title="Bắt đầu lại từ code mẫu?" description="Bản nháp của ngôn ngữ đang chọn sẽ được thay thế. Các ngôn ngữ khác vẫn được giữ lại."><div className="ws-modal-actions"><button onClick={() => setResetOpen(false)}>Giữ bản nháp</button><button className="ws-submit" onClick={() => { store.getState().edit(draftId, starter); store.getState().save(draftId); editor.current?.setValue(starter); setSuspected(noLines); setResetOpen(false); }}>Khôi phục code mẫu</button></div></Modal>
    <Modal open={shortcutsOpen} onOpenChange={setShortcutsOpen} title="Những phím tắt nhỏ" description="Giữ nhịp suy nghĩ, để bàn phím giúp bạn."><dl className="ws-shortcuts"><dt>Chạy thử</dt><dd><kbd>Ctrl / ⌘ + '</kbd></dd><dt>Nộp bài</dt><dd><kbd>Ctrl / ⌘ + Enter</kbd></dd><dt>Lưu nháp</dt><dd><kbd>Ctrl / ⌘ + S</kbd></dd><dt>Mở hướng dẫn</dt><dd><kbd>?</kbd></dd><dt>Thoát tập trung</dt><dd><kbd>Esc</kbd></dd></dl></Modal>
  </main>;
}
