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

export const judgeApi = {
  getExerciseBySlug: async (slug: string): Promise<Exercise> => {
    const response = await axiosInstance.get<Exercise>(`/exercises/${slug}`);
    return response.data;
  },

  runCode: async (
    exerciseId: string,
    language: string,
    sourceCode: string,
    customInput?: string
  ): Promise<RunResult> => {
    const response = await axiosInstance.post<RunResult>(
      `/exercises/${exerciseId}/run`,
      { language, sourceCode, customInput },
    );
    return response.data;
  },

  submitCode: async (
    exerciseId: string,
    language: string,
    sourceCode: string
  ): Promise<SubmitResult> => {
    const response = await axiosInstance.post<SubmitResult>(
      `/exercises/${exerciseId}/submit`,
      { language, sourceCode },
    );
    return response.data;
  },
};
