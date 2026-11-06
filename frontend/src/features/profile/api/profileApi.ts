import { axiosInstance } from '../../../lib/axios';
import type {
  UserProfile,
  ProfileOptions,
  UpdateProfilePayload,
  ChangePasswordPayload,
} from '../types';

/** Chuyển dữ liệu form/role thành body PUT /users/me (backend thay thế toàn bộ field). */
export function buildProfilePayload(
  profile: UserProfile,
  form: {
    fullName: string;
    phoneNumber: string;
    dateOfBirth: string;
    gender?: string;
    academicTitle?: string;
    administrativeClassId?: string | null;
    facultyId?: string | null;
  },
): UpdateProfilePayload {
  const nullIfBlank = (v?: string | null) => (v && v.trim() ? v.trim() : null);
  const isStudent = profile.role === 'Student';
  const isTeacher = profile.role === 'Teacher';
  return {
    fullName: form.fullName.trim(),
    phoneNumber: nullIfBlank(form.phoneNumber),
    dateOfBirth: nullIfBlank(form.dateOfBirth),
    gender: nullIfBlank(form.gender) as UpdateProfilePayload['gender'],
    // Giữ nguyên avatar — avatar chỉ đổi qua POST /users/me/avatar.
    avatarUrl: profile.avatarUrl,
    administrativeClassId: isStudent ? form.administrativeClassId ?? null : null,
    facultyId: isTeacher ? form.facultyId ?? null : null,
    academicTitle: isTeacher ? nullIfBlank(form.academicTitle) : null,
    version: profile.version,
  };
}

export const profileApi = {
  getMe: async (signal?: AbortSignal): Promise<UserProfile> =>
    (await axiosInstance.get<UserProfile>('/users/me', { signal })).data,

  getOptions: async (signal?: AbortSignal): Promise<ProfileOptions> =>
    (await axiosInstance.get<ProfileOptions>('/users/me/options', { signal })).data,

  updateMe: async (payload: UpdateProfilePayload): Promise<UserProfile> =>
    (await axiosInstance.put<UserProfile>('/users/me', payload)).data,

  changePassword: async (payload: ChangePasswordPayload): Promise<void> => {
    await axiosInstance.put('/users/me/password', payload);
  },

  uploadAvatar: async (
    file: Blob,
    version: number,
    onProgress?: (fraction: number) => void,
  ): Promise<UserProfile> => {
    const form = new FormData();
    form.append('file', file, 'avatar');
    form.append('version', String(version));
    const { data } = await axiosInstance.post<UserProfile>('/users/me/avatar', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
      onUploadProgress: (event) => {
        if (onProgress && event.total) onProgress(event.loaded / event.total);
      },
    });
    return data;
  },
};
