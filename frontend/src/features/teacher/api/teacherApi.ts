import axios from 'axios';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { axiosInstance } from '../../../lib/axios';
import { useAuthStore } from '../../auth/store/authStore';

export interface Course { id: string; subjectName: string; sectionCode: string; language: string; semester: string; joinCode: string | null; capacity: number; enrolledCount: number; status: 'Draft' | 'Active' | 'Closed'; exerciseCount: number }
export interface Student { id: string; fullName: string; email: string; studentId: string | null; status: string }
export interface TeachingClass { id: string; name: string; academicYear: string; facultyName: string; studentCount: number }
export interface CourseExercise { id: string; slug: string; title: string; difficulty: string; createdAt: string }
export interface Submission { id: string; exerciseId: string; exerciseTitle: string; studentId: string; studentName: string; language: string; status: string; passedTests: number; totalTests: number; createdAt: string; teacherScore: number | null; teacherFeedback: string | null; gradedAt: string | null }
export interface SubmissionPage { items: Submission[]; page: number; pageSize: number; totalCount: number; totalPages: number }
export interface SubmissionDetail { summary: Submission; sourceCode: string; testResults: { order: number; status: string; input: string; expectedOutput: string; actualOutput: string | null; errorMessage: string | null; runtimeMs: number | null; memoryKb: number | null }[] }
export interface TestInput { input: string; expectedOutput: string; isSample: boolean; explanation: string }
export interface ExerciseInput { title: string; difficulty: string; description: string; constraints: string; starterCode: string; timeLimitMs: number; memoryLimitKb: number; testCases: TestInput[] }

export function usePortalQuery<T>(path: string | null) {
  const userId = useAuthStore(s => s.user?.id);
  return useQuery({ queryKey: ['teaching', userId, path], enabled: !!path && !!userId,
    queryFn: async ({ signal }) => (await axiosInstance.get<T>(path!, { signal })).data });
}
export function usePortalMutation() {
  const client = useQueryClient();
  return useMutation({ mutationFn: async ({ path, method = 'post', data }: { path: string; method?: 'post' | 'put' | 'delete'; data?: unknown }) =>
    (await axiosInstance.request({ url: path, method, data })).data,
    onSuccess: async () => { await Promise.all([
      client.invalidateQueries({ queryKey: ['teaching'] }), client.invalidateQueries({ queryKey: ['admin-teachers'] }),
    ]); } });
}
export function portalError(error: unknown) {
  if (axios.isAxiosError(error)) {
    const status = error.response?.status;
    const data = error.response?.data;
    if (data?.errors) return Object.values(data.errors).flat().join(' ');
    if (status && status >= 500) {
      return data?.title || 'Đã có lỗi xảy ra từ máy chủ. Vui lòng thử lại sau.';
    }
    return data?.detail || data?.title || 'Không thể kết nối máy chủ.';
  }
  return error instanceof Error ? error.message : 'Thao tác thất bại.';
}
