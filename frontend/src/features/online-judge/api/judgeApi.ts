import { authenticatedFetch } from '../../../lib/apiClient';

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
    const response = await fetch(`/api/exercises/${slug}`);
    if (!response.ok) throw new Error('Failed to fetch exercise');
    return response.json();
  },

  runCode: async (
    exerciseId: string,
    languageId: number,
    sourceCode: string,
    customInput?: string
  ): Promise<RunResult> => {
    const response = await authenticatedFetch(`/api/exercises/${exerciseId}/run`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ languageId, sourceCode, customInput }),
    });
    if (!response.ok) throw new Error('Failed to run code');
    return response.json();
  },

  submitCode: async (
    exerciseId: string,
    languageId: number,
    sourceCode: string
  ): Promise<SubmitResult> => {
    const response = await authenticatedFetch(`/api/exercises/${exerciseId}/submit`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ languageId, sourceCode }),
    });
    if (!response.ok) throw new Error('Failed to submit code');
    return response.json();
  },
};
