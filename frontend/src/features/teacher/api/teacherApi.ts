import { useQuery } from '@tanstack/react-query';
import { axiosInstance } from '../../../lib/axios';
import { getErrorMessage } from '../../../lib/errorService';
import { useAuthStore } from '../../auth/store/authStore';

export interface Student { id: string; fullName: string; email: string; studentId: string | null; status: string }
export interface TeachingClass { id: string; name: string; academicYear: string; facultyName: string; studentCount: number }

export function usePortalQuery<T>(path: string | null) {
  const userId = useAuthStore(s => s.user?.id);
  return useQuery({ queryKey: ['teaching', userId, path], enabled: !!path && !!userId,
    queryFn: async ({ signal }) => (await axiosInstance.get<T>(path!, { signal })).data });
}
export function portalError(error: unknown) {
  return getErrorMessage(error, 'Thao tác thất bại. Vui lòng thử lại sau.');
}
