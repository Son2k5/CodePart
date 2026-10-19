import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { axiosInstance } from '../../lib/axios';
import { useAuthStore } from '../auth/store/authStore';
import { teacherError } from '../admin/api/adminTeacherApi';

export interface Teacher { id: string; fullName: string; email: string }
export interface ClassItem {
  id: string; name: string; description: string; thumbnail: string | null;
  status: 'Draft' | 'Active' | 'Archived'; accessType: 'Public' | 'CodeRequired';
  joinCode: string | null; joinCodeExpiresAt: string | null; maxStudents: number | null;
  startDate: string | null; endDate: string | null; createdAt: string; rowVersion: string;
  studentCount: number; teachers: Teacher[];
}
export interface PublicClass { id: string; name: string; description: string; thumbnail: string | null; maxStudents: number | null; studentCount: number; isEnrolled: boolean }
export interface Student { id: string; fullName: string; email: string; joinedAt: string; joinMethod: string }
export interface Page<T> { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number }
export function useDebounced<T>(value: T) {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => { const timer = setTimeout(() => setDebounced(value), 350); return () => clearTimeout(timer); }, [value]);
  return debounced;
}
export function useClassesQuery<T>(path: string, params?: Record<string, unknown>, enabled = true) {
  const user = useAuthStore(s => s.user?.id);
  return useQuery({ queryKey: ['classes', user, path, params], enabled: enabled && !!user,
    queryFn: async ({ signal }) => (await axiosInstance.get<T>(path, { params, signal })).data });
}
export function useClassMutation() {
  const client = useQueryClient();
  return useMutation({ mutationFn: async ({ path, method = 'post', data }: { path: string; method?: 'post' | 'put' | 'patch' | 'delete'; data?: unknown }) =>
    (await axiosInstance.request({ url: path, method, data })).data,
    onSuccess: async () => {
      await Promise.all(['classes', 'admin-teachers', 'teaching'].map(key => client.invalidateQueries({ queryKey: [key] })));
      toast.success('Thao tác thành công');
    }, onError: error => toast.error(teacherError(error)),
  });
}
