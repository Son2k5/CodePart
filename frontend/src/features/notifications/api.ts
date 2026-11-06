import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { axiosInstance } from '../../lib/axios';

export interface Notification {
  id: string;
  type: string;
  title: string;
  body: string;
  isRead: boolean;
  createdAt: string;
}
interface NotificationPage { items: Notification[]; nextCursor: string | null; hasMore: boolean }

export function useNotifications(userId: string, open: boolean, unreadOnly: boolean) {
  const client = useQueryClient();
  const key = ['notifications', userId];
  const count = useQuery({
    queryKey: [...key, 'count'],
    queryFn: async ({ signal }) => (await axiosInstance.get<{ count: number }>('/notifications/unread-count', { signal })).data.count,
    refetchInterval: 30_000,
    refetchOnWindowFocus: true,
    staleTime: 0,
  });
  const list = useInfiniteQuery({
    queryKey: [...key, 'list', unreadOnly],
    initialPageParam: undefined as string | undefined,
    queryFn: async ({ pageParam, signal }) => (await axiosInstance.get<NotificationPage>('/notifications/', {
      signal, params: { take: 20, cursor: pageParam, isRead: unreadOnly ? false : undefined },
    })).data,
    getNextPageParam: page => page.hasMore ? page.nextCursor ?? undefined : undefined,
    enabled: open,
    refetchInterval: open ? 30_000 : false,
    refetchOnWindowFocus: true,
    staleTime: 0,
  });
  const update = useMutation({
    mutationFn: async (action: { type: 'read' | 'delete'; id: string } | { type: 'read-all' }) => {
      if (action.type === 'delete') await axiosInstance.delete(`/notifications/${action.id}`);
      else await axiosInstance.patch(action.type === 'read-all' ? '/notifications/read-all' : `/notifications/${action.id}/read`);
    },
    onSuccess: () => client.invalidateQueries({ queryKey: key }),
  });
  return { count, list, update };
}
