import { axiosInstance } from '../../../lib/axios';

export interface TestCase {
  id: string;
  input: string;
  expectedOutput: string;
  isHidden: boolean;
}

export interface Exercise {
  id: string;
  slug: string;
  title: string;
  description: string;
  difficulty: 'Easy' | 'Medium' | 'Hard';
  testCases: TestCase[];
  starterCode: string;
  constraints: string;
  supportedLanguages: string[];
}

export interface RunResult {
  stdout: string;
  stderr: string;
  compileOutput: string;
  message: string;
  status: { id: number; description: string };
  time: string;
  memory: number;
}

export interface SubmitResult {
  submissionId: string;
  status: string;
  testResults: {
    testCaseId: string;
    passed: boolean;
    actualOutput: string;
    runResult: RunResult;
  }[];
}

interface ApiExecution {
  submissionId: string | null; status: string; runtimeMs: number | null; memoryKb: number | null;
  testCases: { testCaseId: string | null; status: string; actualOutput: string | null; errorMessage: string | null; runtimeMs: number | null; memoryKb: number | null }[];
}
const runResult = (status: string, output: string | null, error: string | null, ms: number | null, memory: number | null): RunResult => ({
  stdout: output ?? '', stderr: status === 'CompilationError' ? '' : error ?? '', compileOutput: status === 'CompilationError' ? error ?? '' : '',
  message: status, status: { id: status === 'Accepted' ? 3 : 4, description: status }, time: ms == null ? '0' : String(ms / 1000), memory: memory ?? 0,
});

export const judgeApi = {
  getExerciseBySlug: async (slug: string): Promise<Exercise> => {
    const response = await axiosInstance.get<Omit<Exercise, 'testCases'> & { sampleTestCases: { id: string; input: string; expectedOutput: string }[] }>(`/exercises/${slug}`);
    return { ...response.data, testCases: response.data.sampleTestCases.map(t => ({ ...t, isHidden: false })) };
  },

  runCode: async (
    exerciseId: string,
    language: string,
    sourceCode: string,
    customInput?: string
  ): Promise<RunResult> => {
    const response = await axiosInstance.post<ApiExecution>(
      `/exercises/${exerciseId}/run`,
      { language, sourceCode, customInput },
    );
    const d = response.data;
    return runResult(d.status, d.testCases.map(t => t.actualOutput ?? '').join('\n'), d.testCases.find(t => t.errorMessage)?.errorMessage ?? null, d.runtimeMs, d.memoryKb);
  },

  submitCode: async (
    exerciseId: string,
    language: string,
    sourceCode: string
  ): Promise<SubmitResult> => {
    const response = await axiosInstance.post<ApiExecution>(
      `/exercises/${exerciseId}/submit`,
      { language, sourceCode },
    );
    const d = response.data;
    return { submissionId: d.submissionId ?? '', status: d.status, testResults: d.testCases.map(t => ({
      testCaseId: t.testCaseId ?? '', passed: t.status === 'Accepted', actualOutput: t.actualOutput ?? '',
      runResult: runResult(t.status, t.actualOutput, t.errorMessage, t.runtimeMs, t.memoryKb),
    })) };
  },
};
