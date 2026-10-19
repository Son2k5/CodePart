import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { createWorkspaceStore, draftKey } from '../src/features/workspace/store';
import { ResultCard, TestGrid, VerdictBanner } from '../src/features/workspace/Results';
import { HintStepper } from '../src/features/ai-assistant/HintStepper';
import { demoSubmission } from '../src/features/workspace/mocks/fixtures';
afterEach(() => vi.useRealTimers());
describe('workspace drafts and execution', () => {
  it('debounces writes and preserves independent language drafts', () => {
    vi.useFakeTimers(); const values = new Map<string, string>();
    const setItem = vi.fn((key: string, value: string) => values.set(key, value));
    const store = createWorkspaceStore({ getItem: key => values.get(key) ?? null, setItem });
    const py = draftKey('student-a', 'sum', 'python'), cpp = draftKey('student-a', 'sum', 'cpp');
    store.getState().open(py, 'starter'); store.getState().edit(py, 'print(1)');
    vi.advanceTimersByTime(500); store.getState().edit(py, 'print(2)');
    vi.advanceTimersByTime(799); expect(setItem).not.toHaveBeenCalled();
    vi.advanceTimersByTime(1); expect(values.get(py)).toBe('print(2)');
    store.getState().open(cpp, 'int main() {}'); store.getState().edit(cpp, 'int main() { return 0; }'); store.getState().save(cpp);
    expect(store.getState().open(py, 'starter')).toBe('print(2)');
    expect(values.get(cpp)).toBe('int main() { return 0; }');
    expect(draftKey('student-b', 'sum', 'python')).not.toBe(py);
  });
  it('flushes the latest edit and reports storage failure without losing code', () => {
    const store = createWorkspaceStore({ getItem: () => null, setItem: () => { throw Error('Quota'); } });
    store.getState().open('draft', ''); store.getState().edit('draft', 'important code'); store.getState().save('draft');
    expect(store.getState().drafts.draft.code).toBe('important code');
    expect(store.getState().drafts.draft.error).toBeTruthy();
    expect(store.getState().drafts.draft.savedAt).toBeUndefined();
  });
  it('prevents a second submission before the first request resolves', () => {
    const store = createWorkspaceStore({ getItem: () => null, setItem: () => {} });
    expect(store.getState().begin('submitting')).toBe(true);
    expect(store.getState().begin('submitting')).toBe(false);
    store.getState().transition('queued'); expect(store.getState().begin('running')).toBe(false);
    store.getState().transition('judging'); expect(store.getState().begin('submitting')).toBe(false);
    store.getState().transition('done'); expect(store.getState().begin('running')).toBe(true);
    store.getState().transition('error'); expect(store.getState().begin('submitting')).toBe(true);
  });
});
describe('result rendering', () => {
  it('never exposes hidden payloads, even when provided by the server', () => {
    render(<ResultCard result={{ index: 3, isSample: false, verdict: 'WrongAnswer', runtimeMs: 12345, input: 'secret-input', expectedOutput: 'secret-expected', actualOutput: 'secret-actual', errorMessage: 'secret-error', line: 99 }} />);
    expect(screen.getByText('Test ẩn #3')).toBeInTheDocument();
    expect(document.body.textContent).not.toMatch(/secret-|12345|99/);
  });
  it('shows custom execution without claiming it passed an expected output', () => {
    render(<ResultCard custom result={{ index: 1, isSample: true, verdict: 'Accepted', runtimeMs: 10, actualOutput: '42' }} />);
    expect(screen.getByText('Đã thực thi')).toBeInTheDocument();
    expect(screen.queryByText('Kết quả mong đợi')).not.toBeInTheDocument();
  });
  it('labels passed, failed and pending tests independently of color', () => {
    render(<TestGrid total={3} results={[{ index: 1, isSample: true, verdict: 'Accepted', runtimeMs: 1 }, { index: 2, isSample: true, verdict: 'WrongAnswer', runtimeMs: 1 }]} />);
    expect(screen.getByLabelText('Test 1: Accepted')).toBeInTheDocument();
    expect(screen.getByLabelText('Test 2: WrongAnswer')).toBeInTheDocument();
    expect(screen.getByLabelText('Test 3: Đang chờ')).toBeInTheDocument();
  });
  it.each(['Accepted', 'WrongAnswer', 'CompileError', 'RuntimeError', 'TimeLimitExceeded', 'MemoryLimitExceeded', 'Pending'])('announces %s', status => {
    render(<VerdictBanner submission={{ ...demoSubmission, status }} />);
    expect(screen.getByRole('status')).toHaveTextContent(/.+/);
  });
});

describe('AI levels', () => {
  it('locks future levels and disables all actions when quota is exhausted', () => {
    const { rerender } = render(<HintStepper unlockedLevel={0} busy={false} onSelect={() => {}} />);
    expect(screen.getByRole('button', { name: 'Gợi ý 1' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Gợi ý 2' })).toBeDisabled();
    rerender(<HintStepper unlockedLevel={2} busy={false} remaining={2} onSelect={() => {}} />);
    expect(screen.getByRole('button', { name: 'Gợi ý 3' })).toBeEnabled();
    rerender(<HintStepper unlockedLevel={2} busy={false} remaining={0} onSelect={() => {}} />);
    expect(screen.getByRole('button', { name: 'Gợi ý 1' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Gợi ý 3' })).toBeDisabled();
  });
});
