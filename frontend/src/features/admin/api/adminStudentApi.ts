import { axiosInstance } from '../../../lib/axios';
import { getErrorMessage } from '../../../lib/errorService';
import type { AdminUserItem } from '../types/admin.types';
import type { Faculty } from './adminTeacherApi';

export interface StudentFilter {
  page?: number;
  pageSize?: number;
  search?: string;
  facultyId?: string;
  status?: AdminUserItem['status'];
  sortBy?: 'name_asc' | 'name_desc' | 'created_asc' | 'created_desc';
}

export interface StudentStats {
  totalStudents: number;
  activeStudents: number;
  pendingApprovals: number;
  disabledStudents: number;
  rejectedStudents: number;
}

export type StudentFormData = Partial<AdminUserItem> & { temporaryPassword?: string };
export type StudentAction = 'approve' | 'reject' | 'disable' | 'enable';

type StudentResponse = Omit<AdminUserItem, 'role' | 'department' | 'phone' | 'bio'> & {
  facultyName?: string | null;
  phone?: string | null;
  bio?: string | null;
  enrolledClasses?: { id: string; name: string; academicYear: string }[];
  enrolledCourses?: { id: string; subjectName: string; sectionCode: string; semester: string; teacherName?: string }[];
};

interface StudentPage {
  items: StudentResponse[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

const mapStudent = (s: StudentResponse): AdminUserItem => ({
  ...s,
  role: 'Student',
  department: s.facultyName ?? '',
  phone: s.phone ?? '',
  bio: s.bio ?? '',
  enrolledClasses: s.enrolledClasses,
  enrolledCourses: s.enrolledCourses,
});

const studentProfile = (d: StudentFormData) => ({
  fullName: d.fullName,
  studentId: d.studentId || '',
  facultyId: d.facultyId || null,
  phone: d.phone || null,
  bio: d.bio || null,
});

export const adminStudentApi = {
  async list(params: StudentFilter, signal?: AbortSignal) {
    const { data } = await axiosInstance.get<StudentPage>('/admin/students', { params, signal });
    return { ...data, items: data.items.map(mapStudent) };
  },
  async stats() {
    return (await axiosInstance.get<StudentStats>('/admin/students/stats')).data;
  },
  async faculties() {
    return (await axiosInstance.get<Faculty[]>('/admin/faculties')).data;
  },
  async detail(id: string, signal?: AbortSignal) {
    const response = (await axiosInstance.get<StudentResponse>(`/admin/students/${id}`, { signal })).data;
    return mapStudent(response);
  },
  async create(d: StudentFormData) {
    return (await axiosInstance.post('/admin/students', {
      ...studentProfile(d),
      email: d.email,
      temporaryPassword: d.temporaryPassword,
      status: d.status ?? 'Active',
    })).data;
  },
  async update(id: string, d: StudentFormData) {
    return (await axiosInstance.put(`/admin/students/${id}`, studentProfile(d))).data;
  },
  async action(id: string, action: StudentAction) {
    return (await axiosInstance.post(`/admin/users/${id}/${action}`)).data;
  },
  async delete(id: string) {
    return (await axiosInstance.delete(`/admin/students/${id}`)).data;
  },
  async bulk(userIds: string[], action: 'approve' | 'disable') {
    return (await axiosInstance.post<number>(`/admin/students/bulk-${action}`, { userIds })).data;
  },
  async bulkDelete(userIds: string[]) {
    return (await axiosInstance.post<number>('/admin/students/bulk-delete', { userIds })).data;
  },
  async export(params: StudentFilter) {
    const { page: _page, pageSize: _pageSize, ...filters } = params;
    const { data } = await axiosInstance.get<Blob>('/admin/students/export', { params: filters, responseType: 'blob' });
    const url = URL.createObjectURL(data);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'sinh-vien.csv';
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  },
};

export function studentError(error: unknown): string {
  return getErrorMessage(error, 'Thao tác sinh viên thất bại. Vui lòng thử lại.');
}
