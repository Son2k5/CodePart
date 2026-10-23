import { axiosInstance as api } from '../../lib/axios';
import { getErrorMessage } from '../../lib/errorService';
import { useQuery } from '@tanstack/react-query';
import { toast } from 'sonner';
export type Starter = { language: string; template: string };
export type Test = { id?: string; input: string; expectedOutput: string; isSample: boolean; order: number; weight: number };
export type Problem = { id: string; title: string; description: string; category: string; difficulty: string; timeLimitMs: number; memoryLimitMb: number; points: number; courseId: string | null; classId: string | null; status: string; comparisonMode: number; starterCodes: Starter[]; testCases: Test[] };
export type JudgeScope = { id: string; name: string; kind: 'class' | 'course' };
export function scopeParams(value: string): Record<string, string> { const [kind, id] = value.split(':'); return id ? kind === 'class' ? { classId: id } : { courseId: id } : {}; }
export function problemScope(p: Partial<Problem>) { return p.classId ? `class:${p.classId}` : p.courseId ? `course:${p.courseId}` : ''; }
export type TestResult = { testCaseId?: string; index: number; isSample: boolean; verdict: string; runtimeMs: number; input?: string; expectedOutput?: string; actualOutput?: string; errorMessage?: string; line?: number; column?: number };
export type Submission = { id: string; problemId: string; status: string; language: string; passedCount: number; totalCount: number; score: number; awardedPoints: number; compileOutput?: string; createdAt: string; results: TestResult[]; sourceCode?: string; queuePosition?: number; memoryKb?: number; runtimeMs?: number; latePenalty?: number; maxRuntimeMs?: number; maxMemoryKb?: number };
export type Page<T> = { items: T[]; total: number };
export const judgeApi = {
  problem: async (id: string, manage = false): Promise<Problem> => (await api.get(`${manage ? '/admin' : ''}/problems/${id}`)).data,
  scopes: async (manage = false): Promise<JudgeScope[]> => (await api.get(`${manage ? '/admin' : ''}/problems/scopes`)).data,
  list: async (manage = false, page = 1, scope = ''): Promise<Page<Problem>> => (await api.get(`${manage ? '/admin' : ''}/problems/`, { params: { page, ...scopeParams(scope) } })).data,
  library: async (filters: { page: number; search: string; difficulty: string; category: string; language: string }): Promise<Page<Problem>> => (await api.get('/problems/', { params: { ...filters, library: true } })).data,
  run: async (id: string, language: string, sourceCode: string, customInput?: string): Promise<{ results: TestResult[] }> => (await api.post(`/problems/${id}/run`, { language, sourceCode, customInput })).data,
  submit: async (id: string, language: string, sourceCode: string, topicId?: string): Promise<{ submissionId: string; queuePosition?: number }> => (await api.post(`/problems/${id}/submit`, { language, sourceCode, ...(topicId ? { topicId } : {}) })).data,
  submission: async (id: string, manage = false): Promise<Submission> => (await api.get(`${manage ? '/admin' : ''}/submissions/${id}`)).data,
  history: async (id: string, page = 1): Promise<Page<Submission>> => (await api.get(`/problems/${id}/submissions`, { params: { page } })).data,
  save: async (p: Partial<Problem>) => p.id ? (await api.put(`/admin/problems/${p.id}`, p)).data : (await api.post('/admin/problems/', p)).data,
  tests: async (id: string, tests: Test[]) => api.put(`/admin/problems/${id}/tests`, tests),
  action: async (id: string, action: string) => action === 'delete' ? api.delete(`/admin/problems/${id}`) : api.post(`/admin/problems/${id}/${action}`),
  submissions: async (filters: Record<string, string | number>): Promise<Page<Submission>> => (await api.get('/admin/submissions', { params: filters })).data,
};
export const active = (status?: string) => !!status && ['Pending', 'Running', 'Queued', 'Judging'].includes(status);
export function useSubmissionStatus(id?: string) {
  return useQuery({ queryKey: ['judge-submission', id], queryFn: () => judgeApi.submission(id!), enabled: !!id,
    refetchInterval: q => q.state.status === 'error' ? false : !q.state.data || active(q.state.data.status) ? 1500 : false,
    retry: 2, refetchIntervalInBackground: false });
}
export function judgeError(error: unknown) {
  toast.error(getErrorMessage(error, 'Không thể hoàn thành yêu cầu. Vui lòng thử lại.'));
}
