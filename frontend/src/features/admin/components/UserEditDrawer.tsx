import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import * as Dialog from '@radix-ui/react-dialog';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { History, Loader2, Save, X } from 'lucide-react';
import { toast } from 'sonner';
import { adminUserApi, type AdminUpdateUserPayload } from '../api/adminUserApi';
import { profileApi } from '../../profile/api/profileApi';
import { extractFieldErrors, isConflict, type FieldErrors } from '../../profile/lib/fieldErrors';
import { getErrorMessage } from '../../../lib/errorService';
import { Field, SegmentedControl, ShimmerBlock, Spinner } from '../../profile/components/ui';
import { SearchableSelect } from '../../profile/components/SearchableSelect';
import { roleBadge, statusBadge } from '../../profile/components/ui';

type Role = 'Admin' | 'Teacher' | 'Student';
type UserStatus = 'Active' | 'Pending' | 'Rejected' | 'Disabled';

const schema = z.object({
  fullName: z.string().trim().min(2, 'Họ tên phải có 2–100 ký tự.').max(100, 'Họ tên phải có 2–100 ký tự.'),
  phoneNumber: z.string().trim().refine((v) => v === '' || /^(0|\+84)\d{9}$/.test(v), 'Số điện thoại không hợp lệ.'),
  dateOfBirth: z.string().refine((v) => !v || new Date(v).getTime() <= Date.now(), 'Ngày sinh không được ở tương lai.'),
  gender: z.enum(['Male', 'Female', 'Other', '']),
  academicTitle: z.string().trim().max(100).optional(),
  administrativeClassId: z.string().nullable().optional(),
  facultyId: z.string().nullable().optional(),
  role: z.enum(['Admin', 'Teacher', 'Student']),
  status: z.enum(['Active', 'Pending', 'Rejected', 'Disabled']),
});
type FormValues = z.infer<typeof schema>;

interface UserEditDrawerProps {
  userId: string | null;
  open: boolean;
  onClose: () => void;
}

const actionLabel: Record<string, string> = {
  UserUpdated: 'Cập nhật hồ sơ',
  UserDisabled: 'Vô hiệu hóa',
  UserEnabled: 'Mở khóa',
  UserDeleted: 'Xóa tài khoản',
  TeacherApproved: 'Duyệt giảng viên',
  TeacherRejected: 'Từ chối giảng viên',
  TeacherCreated: 'Tạo giảng viên',
  TeacherUpdated: 'Cập nhật giảng viên',
  StudentCreated: 'Tạo sinh viên',
  StudentUpdated: 'Cập nhật sinh viên',
};

export function UserEditDrawer({ userId, open, onClose }: UserEditDrawerProps) {
  const client = useQueryClient();
  const enabled = open && !!userId;

  const userQuery = useQuery({
    queryKey: ['admin-user', userId],
    queryFn: ({ signal }) => adminUserApi.getUser(userId!, signal),
    enabled,
  });
  const auditQuery = useQuery({
    queryKey: ['admin-user-audit', userId],
    queryFn: ({ signal }) => adminUserApi.auditLogs(userId!, 8, signal),
    enabled,
  });
  const optionsQuery = useQuery({
    queryKey: ['profile', 'options'],
    queryFn: ({ signal }) => profileApi.getOptions(signal),
    staleTime: 5 * 60 * 1000,
  });

  const {
    register,
    handleSubmit,
    reset,
    watch,
    setValue,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(schema) });

  const [serverErrors, setServerErrors] = useState<FieldErrors>({});

  useEffect(() => {
    if (userQuery.data) {
      const u = userQuery.data;
      reset({
        fullName: u.fullName,
        phoneNumber: u.phoneNumber ?? '',
        dateOfBirth: u.dateOfBirth ?? '',
        gender: u.gender ?? '',
        academicTitle: u.academicTitle ?? '',
        administrativeClassId: u.administrativeClassId,
        facultyId: u.facultyId,
        role: u.role as Role,
        status: u.status as UserStatus,
      });
      setServerErrors({});
    }
  }, [userQuery.data, reset]);

  const update = useMutation({
    mutationFn: (payload: AdminUpdateUserPayload) => adminUserApi.updateUser(userId!, payload),
    onSuccess: (u) => {
      toast.success('Đã cập nhật người dùng.');
      client.invalidateQueries({ queryKey: ['admin-user', userId] });
      client.invalidateQueries({ queryKey: ['admin-user-audit', userId] });
      client.invalidateQueries({ queryKey: ['admin-students'] });
      client.invalidateQueries({ queryKey: ['admin-teachers'] });
      reset({
        fullName: u.fullName,
        phoneNumber: u.phoneNumber ?? '',
        dateOfBirth: u.dateOfBirth ?? '',
        gender: u.gender ?? '',
        academicTitle: u.academicTitle ?? '',
        administrativeClassId: u.administrativeClassId,
        facultyId: u.facultyId,
        role: u.role as Role,
        status: u.status as UserStatus,
      });
    },
    onError: (err) => {
      if (isConflict(err)) {
        toast.error('Hồ sơ vừa bị thay đổi ở nơi khác. Đang tải lại dữ liệu mới nhất…');
        userQuery.refetch();
        auditQuery.refetch();
        return;
      }
      const fe = extractFieldErrors(err);
      setServerErrors(Object.keys(fe).length ? fe : { '': getErrorMessage(err) });
      toast.error(getErrorMessage(err));
    },
  });

  const role = watch('role') as Role;
  const gender = watch('gender') ?? '';
  const administrativeClassId = watch('administrativeClassId') ?? null;
  const facultyId = watch('facultyId') ?? null;

  const onSubmit = handleSubmit((values) => {
    setServerErrors({});
    const nb = (v?: string | null) => (v && v.trim() ? v.trim() : null);
    update.mutate({
      fullName: values.fullName.trim(),
      phoneNumber: nb(values.phoneNumber),
      dateOfBirth: nb(values.dateOfBirth),
      gender: nb(values.gender),
      avatarUrl: userQuery.data?.avatarUrl ?? null,
      administrativeClassId: values.role === 'Student' ? values.administrativeClassId ?? null : null,
      facultyId: values.role === 'Teacher' ? values.facultyId ?? null : null,
      academicTitle: values.role === 'Teacher' ? nb(values.academicTitle) : null,
      version: userQuery.data?.version ?? 0,
      role: values.role,
      status: values.status,
    });
  });

  const errorFor = (key: keyof FormValues) => (errors[key]?.message as string) || serverErrors[key];
  const u = userQuery.data;

  return (
    <Dialog.Root open={open} onOpenChange={(v) => !v && onClose()}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-40 bg-slate-900/40 backdrop-blur-sm data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0" />
        <Dialog.Content className="fixed right-0 top-0 z-50 flex h-full w-full max-w-md flex-col border-l border-[#dfecee] bg-white shadow-2xl transition-transform data-[state=open]:translate-x-0 data-[state=closed]:translate-x-full data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:duration-200 data-[state=open]:duration-300">
          <div className="flex items-center justify-between border-b border-[#eef6f7] px-6 py-4">
            <div>
              <Dialog.Title className="text-base font-bold text-[#3e656b]">Chỉnh sửa người dùng</Dialog.Title>
              <Dialog.Description className="mt-0.5 text-xs text-slate-500">{u?.email ?? 'Đang tải…'}</Dialog.Description>
            </div>
            <Dialog.Close className="grid h-9 w-9 place-items-center rounded-lg text-slate-400 hover:bg-slate-100">
              <X className="h-5 w-5" />
            </Dialog.Close>
          </div>

          <div className="flex-1 overflow-y-auto px-6 py-5">
            {userQuery.isPending ? (
              <div className="space-y-4">
                {Array.from({ length: 6 }).map((_, i) => (
                  <ShimmerBlock key={i} className="h-11 w-full" />
                ))}
              </div>
            ) : userQuery.isError ? (
              <div className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700">
                {getErrorMessage(userQuery.error)}{' '}
                <button className="font-semibold underline" onClick={() => userQuery.refetch()}>
                  Thử lại
                </button>
              </div>
            ) : u ? (
              <form onSubmit={onSubmit} className="space-y-5">
                {serverErrors[''] && <div className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">{serverErrors['']}</div>}

                <Field label="Họ và tên *" error={errorFor('fullName')} {...register('fullName')} />
                <div className="grid grid-cols-2 gap-3">
                  <Field label="Email" value={u.email} locked readOnly />
                  <Field label={u.role === 'Student' ? 'Mã sinh viên' : 'Mã GV'} value={u.studentCode ?? '—'} locked readOnly />
                </div>
                <div className="grid grid-cols-2 gap-3">
                  <Field label="Số điện thoại" error={errorFor('phoneNumber')} {...register('phoneNumber')} />
                  <Field label="Ngày sinh" type="date" max={new Date().toISOString().slice(0, 10)} error={errorFor('dateOfBirth')} {...register('dateOfBirth')} />
                </div>

                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-1.5">
                    <label className="text-xs font-semibold text-[#3e656b]">Vai trò</label>
                    <select value={role} onChange={(e) => setValue('role', e.target.value as Role, { shouldDirty: true })} className="w-full rounded-xl border border-[#cddfe3] bg-white px-3 py-2.5 text-sm outline-none focus:border-[#397c8c]">
                      {(['Student', 'Teacher', 'Admin'] as Role[]).map((r) => (
                        <option key={r} value={r}>
                          {roleBadge[r]?.label ?? r}
                        </option>
                      ))}
                    </select>
                    {errorFor('role') && <p className="text-xs text-rose-600">{errorFor('role')}</p>}
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-xs font-semibold text-[#3e656b]">Trạng thái</label>
                    <select value={watch('status')} onChange={(e) => setValue('status', e.target.value as UserStatus, { shouldDirty: true })} className="w-full rounded-xl border border-[#cddfe3] bg-white px-3 py-2.5 text-sm outline-none focus:border-[#397c8c]">
                      {(['Active', 'Pending', 'Disabled', 'Rejected'] as UserStatus[]).map((s) => (
                        <option key={s} value={s}>
                          {statusBadge[s]?.label ?? s}
                        </option>
                      ))}
                    </select>
                    {errorFor('status') && <p className="text-xs text-rose-600">{errorFor('status')}</p>}
                  </div>
                </div>

                <div className="space-y-1.5">
                  <label className="text-xs font-semibold text-[#3e656b]">Giới tính</label>
                  <SegmentedControl value={gender} onChange={(v) => setValue('gender', v as FormValues['gender'], { shouldDirty: true })} options={[{ value: 'Male', label: 'Nam' }, { value: 'Female', label: 'Nữ' }, { value: 'Other', label: 'Khác' }]} />
                </div>

                {role === 'Student' && (
                  <SearchableSelect label="Lớp hành chính" value={administrativeClassId} options={optionsQuery.data?.administrativeClasses ?? []} error={errorFor('administrativeClassId')} onChange={(v) => setValue('administrativeClassId', v, { shouldDirty: true })} />
                )}
                {role === 'Teacher' && (
                  <div className="grid grid-cols-2 gap-3">
                    <SearchableSelect label="Khoa" value={facultyId} options={optionsQuery.data?.faculties ?? []} error={errorFor('facultyId')} onChange={(v) => setValue('facultyId', v, { shouldDirty: true })} />
                    <Field label="Học hàm / học vị" error={errorFor('academicTitle')} {...register('academicTitle')} />
                  </div>
                )}

                <div className="flex justify-end gap-2 pt-1">
                  <button type="button" onClick={onClose} className="rounded-full border border-[#cddfe3] px-4 py-2 text-sm font-medium text-[#3e656b] hover:bg-slate-50">
                    Hủy
                  </button>
                  <button type="submit" disabled={update.isPending} className="inline-flex items-center gap-1.5 rounded-full bg-[#397955] px-5 py-2 text-sm font-semibold text-white hover:bg-[#2f6547] disabled:opacity-60">
                    {update.isPending ? <Spinner /> : <Save className="h-4 w-4" />}
                    Lưu
                  </button>
                </div>
              </form>
            ) : null}

            <section className="mt-8 border-t border-[#eef6f7] pt-5">
              <h3 className="mb-3 flex items-center gap-2 text-sm font-semibold text-[#3e656b]">
                <History className="h-4 w-4" /> Lịch sử thay đổi gần đây
              </h3>
              {auditQuery.isPending ? (
                <div className="flex items-center gap-2 text-sm text-slate-400">
                  <Loader2 className="h-4 w-4 animate-spin" /> Đang tải…
                </div>
              ) : !auditQuery.data?.length ? (
                <p className="text-sm text-slate-400">Chưa có bản ghi nào.</p>
              ) : (
                <ol className="relative space-y-4 pl-4">
                  <span className="absolute left-1 top-1 bottom-1 w-px bg-[#dfecee]" aria-hidden />
                  {auditQuery.data.map((log) => (
                    <li key={log.id} className="relative">
                      <span className="absolute -left-[13px] top-1.5 h-2.5 w-2.5 rounded-full border-2 border-white bg-[#397c8c]" aria-hidden />
                      <p className="text-sm font-medium text-[#3e656b]">{actionLabel[log.action] ?? log.action}</p>
                      <p className="text-xs text-slate-400">{new Date(log.occurredAt).toLocaleString('vi-VN')}</p>
                    </li>
                  ))}
                </ol>
              )}
            </section>
          </div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}

