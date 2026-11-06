import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { NotificationBell } from '../src/features/notifications/NotificationBell';
import { useAuthStore } from '../src/features/auth/store/authStore';

const api = vi.hoisted(() => ({ get: vi.fn(), patch: vi.fn(), delete: vi.fn() }));
vi.mock('../src/lib/axios', () => ({ axiosInstance: api }));

let items: { id: string; type: string; title: string; body: string; isRead: boolean; createdAt: string }[];
beforeEach(() => {
  vi.resetAllMocks();
  useAuthStore.setState({ user: { id: 'student-1', fullName: 'Student', email: 'test@example.com', role: 'Student', status: 'Active' } });
  items = [1, 2].map(id => ({ id: String(id), type: 'SystemBroadcast', title: `Thông báo ${id}`, body: `Nội dung ${id}`, isRead: false, createdAt: '2026-10-24T08:00:00Z' }));
  api.get.mockImplementation(async (path, config) => ({ data: path.endsWith('unread-count') ? { count: items.filter(item => !item.isRead).length } : {
    items: items.filter(item => config.params.isRead !== false || !item.isRead), nextCursor: null, hasMore: false,
  } }));
  api.patch.mockImplementation(async (path: string) => { items = items.map(item => path.endsWith('read-all') || path.includes(`/${item.id}/`) ? { ...item, isRead: true } : item); });
  api.delete.mockImplementation(async (path: string) => { items = items.filter(item => !path.endsWith(`/${item.id}`)); });
});
function mount() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(<QueryClientProvider client={client}><NotificationBell /></QueryClientProvider>);
}

describe('notification inbox', () => {
  it('loads only the count until opened, filters unread, and synchronizes read/delete actions', async () => {
    mount();
    fireEvent.click(await screen.findByRole('button', { name: 'Thông báo, 2 chưa đọc' }));
    await screen.findByText('Nội dung 1');
    fireEvent.click(screen.getByRole('button', { name: 'Chưa đọc' }));
    await waitFor(() => expect(api.get).toHaveBeenCalledWith('/notifications/', expect.objectContaining({ params: expect.objectContaining({ isRead: false, take: 20 }) })));
    fireEvent.click((await screen.findAllByRole('button', { name: 'Đánh dấu đã đọc' }))[0]);
    await waitFor(() => expect(screen.queryByText('Nội dung 1')).not.toBeInTheDocument());
    expect(screen.getByRole('button', { name: 'Thông báo, 1 chưa đọc' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Xóa thông báo: Thông báo 2' }));
    await screen.findByText('Bạn đã đọc hết thông báo.');
    expect(api.delete).toHaveBeenCalledWith('/notifications/2');
  });

  it('marks all as read and closes with Escape, restoring focus', async () => {
    mount();
    const bell = await screen.findByRole('button', { name: 'Thông báo, 2 chưa đọc' });
    expect(api.get).toHaveBeenCalledTimes(1);
    fireEvent.click(bell);
    await screen.findByText('Nội dung 1');
    fireEvent.click(screen.getByRole('button', { name: 'Đọc tất cả' }));
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Đánh dấu đã đọc' })).not.toBeInTheDocument());
    expect(api.patch).toHaveBeenCalledWith('/notifications/read-all');
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(screen.queryByRole('region', { name: 'Danh sách thông báo' })).not.toBeInTheDocument();
    expect(bell).toHaveFocus();
  });

  it('keeps unread data when a write fails and displays an error', async () => {
    api.patch.mockRejectedValue(new Error('offline'));
    mount();
    fireEvent.click(await screen.findByRole('button', { name: 'Thông báo, 2 chưa đọc' }));
    await screen.findByText('Nội dung 1');
    fireEvent.click(screen.getByRole('button', { name: 'Đọc tất cả' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Không thể cập nhật');
    expect(screen.getByRole('button', { name: 'Thông báo, 2 chưa đọc' })).toBeInTheDocument();
  });

  it('requests the next cursor and displays both pages', async () => {
    api.get.mockImplementation(async (path, config) => ({ data: path.endsWith('unread-count') ? { count: 2 } : {
      items: [items[config.params.cursor ? 1 : 0]], nextCursor: config.params.cursor ? null : 'next-page', hasMore: !config.params.cursor,
    } }));
    mount();
    fireEvent.click(await screen.findByRole('button', { name: 'Thông báo, 2 chưa đọc' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Xem thêm' }));
    await screen.findByText('Nội dung 2');
    expect(screen.getByText('Nội dung 1')).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith('/notifications/', expect.objectContaining({ params: expect.objectContaining({ cursor: 'next-page' }) }));
  });

  it('does not fetch notifications for an inactive account', () => {
    useAuthStore.setState({ user: null });
    mount();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(api.get).not.toHaveBeenCalled();
  });
});
