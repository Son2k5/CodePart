import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { profileApi } from '../api/profileApi';
import type { UpdateProfilePayload, ChangePasswordPayload } from '../types';
import { useAuthStore } from '../../auth/store/authStore';

export const profileKeys = {
  me: ['profile', 'me'] as const,
  options: ['profile', 'options'] as const,
};

export function useMyProfile() {
  return useQuery({
    queryKey: profileKeys.me,
    queryFn: ({ signal }) => profileApi.getMe(signal),
  });
}

export function useProfileOptions(enabled = true) {
  return useQuery({
    queryKey: profileKeys.options,
    queryFn: ({ signal }) => profileApi.getOptions(signal),
    enabled,
    staleTime: 5 * 60 * 1000,
  });
}

/** Đồng bộ họ tên/avatar vào auth store để header hiển thị ngay. */
function syncAuthStore(profile: { id: string; fullName: string; avatarUrl: string | null }) {
  const session = useAuthStore.getState();
  if (session.user && session.user.id === profile.id && session.accessToken) {
    session.setAuth({ ...session.user, fullName: profile.fullName }, session.accessToken);
  }
}

export function useUpdateProfile() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: UpdateProfilePayload) => profileApi.updateMe(payload),
    onSuccess: (profile) => {
      client.setQueryData(profileKeys.me, profile);
      syncAuthStore(profile);
    },
  });
}

export function useUploadAvatar() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ file, version }: { file: Blob; version: number }) => profileApi.uploadAvatar(file, version),
    onSuccess: (profile) => {
      client.setQueryData(profileKeys.me, profile);
      syncAuthStore(profile);
    },
  });
}

export function useChangePassword() {
  return useMutation({
    mutationFn: (payload: ChangePasswordPayload) => profileApi.changePassword(payload),
  });
}
