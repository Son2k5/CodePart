import { useCallback, useEffect, useState, type ReactNode } from 'react';
import { useBlocker } from 'react-router-dom';
import * as Tabs from '@radix-ui/react-tabs';
import { useQueryClient } from '@tanstack/react-query';
import { CloudOff, RefreshCw, ShieldAlert } from 'lucide-react';
import { toast } from 'sonner';
import '../profile.css';
import { useMyProfile, useProfileOptions, useUpdateProfile, profileKeys } from '../hooks/useProfile';
import { profileApi } from '../api/profileApi';
import { extractFieldErrors, isConflict, isForbidden, type FieldErrors } from '../lib/fieldErrors';
import { getErrorMessage } from '../../../lib/errorService';
import type { UpdateProfilePayload } from '../types';
import { ProfileCard } from '../components/ProfileCard';
import { PersonalInfoTab } from '../components/PersonalInfoTab';
import { SecurityTab } from '../components/SecurityTab';
import { EmptyState, ShimmerBlock } from '../components/ui';

function PageShell({ children }: { children: ReactNode }) {
  return (
    <div className="mx-auto min-h-[60vh] w-full max-w-6xl px-4 py-8 sm:px-6">
      <header className="mb-6">
        <h1 className="text-2xl font-bold text-[#3e656b]">Hồ sơ của tôi</h1>
        <p className="mt-1 text-sm text-slate-500">Quản lý thông tin cá nhân, ảnh đại diện và bảo mật tài khoản.</p>
      </header>
      {children}
    </div>
  );
}

function LoadingSkeleton() {
  return (
    <div className="grid gap-6 lg:grid-cols-[340px_1fr]">
      <div className="space-y-4 rounded-3xl border border-[#dfecee] bg-white p-6">
        <ShimmerBlock className="mx-auto h-32 w-32 rounded-full" />
        <ShimmerBlock className="mx-auto h-5 w-40" />
        <ShimmerBlock className="mx-auto h-4 w-56" />
        <ShimmerBlock className="h-24 w-full" />
      </div>
      <div className="space-y-5 rounded-3xl border border-[#dfecee] bg-white p-6">
        <ShimmerBlock className="h-9 w-64" />
        {Array.from({ length: 5 }).map((_, i) => (
          <ShimmerBlock key={i} className="h-12 w-full" />
        ))}
      </div>
    </div>
  );
}

export function ProfilePage() {
  const client = useQueryClient();
  const { data: profile, isPending, error, refetch } = useMyProfile();
  const options = useProfileOptions();
  const updateProfile = useUpdateProfile();

  const [dirty, setDirty] = useState(false);
  const [infoServerErrors, setInfoServerErrors] = useState<FieldErrors>({});
  const [conflictOpen, setConflictOpen] = useState(false);
  const [avatarBusy, setAvatarBusy] = useState(false);
  const [progress, setProgress] = useState<number | null>(null);

  const blocker = useBlocker(
    ({ currentLocation, nextLocation }) => dirty && currentLocation.pathname !== nextLocation.pathname,
  );

  useEffect(() => {
    const handler = (e: BeforeUnloadEvent) => {
      if (dirty) {
        e.preventDefault();
        e.returnValue = '';
      }
    };
    window.addEventListener('beforeunload', handler);
    return () => window.removeEventListener('beforeunload', handler);
  }, [dirty]);

  const reload = useCallback(() => {
    setConflictOpen(false);
    setInfoServerErrors({});
    setDirty(false);
    void refetch();
    void options.refetch();
  }, [refetch, options]);

  const handleSave = (payload: UpdateProfilePayload) => {
    setInfoServerErrors({});
    updateProfile.mutate(payload, {
      onSuccess: () => {
        toast.success('Đã cập nhật thông tin cá nhân.');
        setDirty(false);
      },
      onError: (err) => {
        if (isConflict(err)) {
          setConflictOpen(true);
          return;
        }
        const fieldErrors = extractFieldErrors(err);
        if (Object.keys(fieldErrors).length) setInfoServerErrors(fieldErrors);
        else setInfoServerErrors({ '': getErrorMessage(err) });
        toast.error(getErrorMessage(err));
      },
    });
  };

  const handleUpload = async (blob: Blob) => {
    if (!profile) return;
    setAvatarBusy(true);
    setProgress(0);
    try {
      const updated = await profileApi.uploadAvatar(blob, profile.version, setProgress);
      client.setQueryData(profileKeys.me, updated);
      toast.success('Đã cập nhật ảnh đại diện.');
    } catch (err) {
      if (isConflict(err)) setConflictOpen(true);
      else toast.error(getErrorMessage(err, 'Không tải được ảnh lên.'));
    } finally {
      setAvatarBusy(false);
      setProgress(null);
    }
  };

  if (isPending && !profile) return <PageShell><LoadingSkeleton /></PageShell>;

  if (error && !profile) {
    if (isForbidden(error)) {
      return (
        <PageShell>
          <EmptyState
            icon={<ShieldAlert className="h-7 w-7" />}
            title="Không có quyền truy cập"
            description="Bạn không có quyền xem trang này."
          />
        </PageShell>
      );
    }
    return (
      <PageShell>
        <EmptyState
          icon={<CloudOff className="h-7 w-7" />}
          title="Không tải được hồ sơ"
          description={getErrorMessage(error)}
          onRetry={() => void refetch()}
        />
      </PageShell>
    );
  }

  if (!profile) return <PageShell><LoadingSkeleton /></PageShell>;

  return (
    <PageShell>
      <div className="grid gap-6 lg:grid-cols-[340px_1fr]">
        <div className="lg:sticky lg:top-6 lg:self-start">
          <ProfileCard
            profile={profile}
            options={options.data}
            busy={avatarBusy}
            progress={progress}
            onUpload={handleUpload}
          />
        </div>

        <div className="rounded-3xl border border-[#dfecee] bg-white p-6 shadow-sm">
          <Tabs.Root defaultValue="info">
            <Tabs.List className="flex gap-1 border-b border-[#eef6f7]" aria-label="Khu vực hồ sơ">
              {[
                { value: 'info', label: 'Thông tin cá nhân' },
                { value: 'security', label: 'Bảo mật' },
              ].map((tab) => (
                <Tabs.Trigger
                  key={tab.value}
                  value={tab.value}
                  className="group relative px-4 py-2.5 text-sm font-medium text-slate-500 outline-none transition-colors data-[state=active]:text-[#397c8c] hover:text-[#397c8c]"
                >
                  {tab.label}
                  <span className="cp-tabs-underline absolute inset-x-3 -bottom-px h-0.5 origin-left scale-x-0 rounded-full bg-[#397c8c] transition-transform group-data-[state=active]:scale-x-100" />
                </Tabs.Trigger>
              ))}
            </Tabs.List>

            <Tabs.Content value="info" className="cp-fade-in pt-6 outline-none">
              <PersonalInfoTab
                profile={profile}
                options={options.data}
                saving={updateProfile.isPending}
                serverErrors={infoServerErrors}
                onSave={handleSave}
                onDirtyChange={setDirty}
              />
            </Tabs.Content>

            <Tabs.Content value="security" className="cp-fade-in pt-6 outline-none">
              <SecurityTab />
            </Tabs.Content>
          </Tabs.Root>
        </div>
      </div>

      {/* Xung đột phiên bản (409) */}
      {conflictOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4 backdrop-blur-sm">
          <div className="w-full max-w-sm rounded-3xl bg-white p-6 text-center shadow-2xl">
            <div className="mx-auto grid h-14 w-14 place-items-center rounded-2xl bg-amber-100 text-amber-600">
              <RefreshCw className="h-6 w-6" />
            </div>
            <h3 className="mt-4 text-lg font-semibold text-[#3e656b]">Thông tin đã được thay đổi ở nơi khác</h3>
            <p className="mt-2 text-sm text-slate-500">Hồ sơ của bạn vừa được cập nhật bởi một thao tác khác. Hãy tải lại dữ liệu mới nhất rồi thử lại.</p>
            <button type="button" onClick={reload} className="mt-5 w-full rounded-full bg-[#397c8c] px-5 py-2.5 text-sm font-semibold text-white hover:bg-[#327080]">
              Tải lại dữ liệu
            </button>
          </div>
        </div>
      )}

      {/* Xác nhận rời trang khi chưa lưu */}
      {blocker.state === 'blocked' && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4 backdrop-blur-sm">
          <div className="w-full max-w-sm rounded-3xl bg-white p-6 text-center shadow-2xl">
            <h3 className="text-lg font-semibold text-[#3e656b]">Bạn chưa lưu thay đổi</h3>
            <p className="mt-2 text-sm text-slate-500">Rời trang bây giờ sẽ làm mất các thay đổi chưa lưu.</p>
            <div className="mt-5 flex justify-center gap-2">
              <button type="button" onClick={() => blocker.reset?.()} className="rounded-full border border-[#cddfe3] px-5 py-2 text-sm font-medium text-[#3e656b] hover:bg-slate-50">
                Ở lại
              </button>
              <button
                type="button"
                onClick={() => {
                  setDirty(false);
                  blocker.proceed?.();
                }}
                className="rounded-full bg-[#397955] px-5 py-2 text-sm font-semibold text-white hover:bg-[#2f6547]"
              >
                Rời đi
              </button>
            </div>
          </div>
        </div>
      )}
    </PageShell>
  );
}
