import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Save, Undo2 } from 'lucide-react';
import { profileFormSchema, type ProfileFormValues } from '../schemas/profileSchemas';
import { buildProfilePayload } from '../api/profileApi';
import type { FieldErrors } from '../lib/fieldErrors';
import type { ProfileOptions, UpdateProfilePayload, UserProfile } from '../types';
import { Field, SegmentedControl, Spinner } from './ui';
import { SearchableSelect } from './SearchableSelect';

interface PersonalInfoTabProps {
  profile: UserProfile;
  options: ProfileOptions | undefined;
  saving: boolean;
  serverErrors: FieldErrors;
  onSave: (payload: UpdateProfilePayload) => void;
  onDirtyChange: (dirty: boolean) => void;
}

const toFormValues = (p: UserProfile): ProfileFormValues => ({
  fullName: p.fullName,
  phoneNumber: p.phoneNumber ?? '',
  dateOfBirth: p.dateOfBirth ?? '',
  gender: p.gender ?? '',
  academicTitle: p.academicTitle ?? '',
  avatarUrl: p.avatarUrl,
  administrativeClassId: p.administrativeClassId,
  facultyId: p.facultyId,
});

export function PersonalInfoTab({ profile, options, saving, serverErrors, onSave, onDirtyChange }: PersonalInfoTabProps) {
  const {
    register,
    handleSubmit,
    reset,
    watch,
    setValue,
    formState: { errors, isDirty },
  } = useForm<ProfileFormValues>({
    resolver: zodResolver(profileFormSchema),
    defaultValues: toFormValues(profile),
    mode: 'onBlur',
  });

  // Reset khi dữ liệu gốc thay đổi (sau khi lưu / tải lại).
  useEffect(() => {
    reset(toFormValues(profile));
  }, [profile.version, profile.id, reset]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    onDirtyChange(isDirty);
  }, [isDirty, onDirtyChange]);

  const gender = watch('gender') ?? '';
  const administrativeClassId = watch('administrativeClassId') ?? null;
  const facultyId = watch('facultyId') ?? null;

  const isStudent = profile.role === 'Student';
  const isTeacher = profile.role === 'Teacher';

  const submit = handleSubmit((values) => onSave(buildProfilePayload(profile, values)));

  const errorFor = (key: keyof ProfileFormValues) =>
    (errors[key]?.message as string) || serverErrors[key];

  return (
    <form onSubmit={submit} className="space-y-5">
      {serverErrors[''] && (
        <div className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">{serverErrors['']}</div>
      )}

      <Field label="Họ và tên *" error={errorFor('fullName')} placeholder="Nguyễn Văn An" {...register('fullName')} />

      <div className="grid gap-5 sm:grid-cols-2">
        <Field label="Email" value={profile.email} locked readOnly />
        <Field
          label={isStudent ? 'Mã sinh viên' : 'Mã đăng nhập'}
          value={profile.studentCode ?? profile.email}
          locked
          readOnly
        />
      </div>

      <div className="grid gap-5 sm:grid-cols-2">
        <Field label="Số điện thoại" placeholder="0912345678" error={errorFor('phoneNumber')} {...register('phoneNumber')} />
        <Field label="Ngày sinh" type="date" max={new Date().toISOString().slice(0, 10)} error={errorFor('dateOfBirth')} {...register('dateOfBirth')} />
      </div>

      <div className="space-y-1.5">
        <label className="text-xs font-semibold text-[#3e656b]">Giới tính</label>
        <SegmentedControl
          value={gender}
          onChange={(v) => setValue('gender', v, { shouldDirty: true, shouldValidate: true })}
          options={[
            { value: 'Male', label: 'Nam' },
            { value: 'Female', label: 'Nữ' },
            { value: 'Other', label: 'Khác' },
          ]}
        />
        {errorFor('gender') && <p className="text-xs text-rose-600">{errorFor('gender')}</p>}
      </div>

      {isStudent && (
        <SearchableSelect
          label="Lớp hành chính"
          value={administrativeClassId}
          options={options?.administrativeClasses ?? []}
          error={serverErrors['administrativeClassId'] || (errors.administrativeClassId?.message as string)}
          onChange={(v) => setValue('administrativeClassId', v, { shouldDirty: true })}
        />
      )}

      {isTeacher && (
        <div className="grid gap-5 sm:grid-cols-2">
          <SearchableSelect
            label="Khoa"
            value={facultyId}
            options={options?.faculties ?? []}
            error={serverErrors['facultyId'] || (errors.facultyId?.message as string)}
            onChange={(v) => setValue('facultyId', v, { shouldDirty: true })}
          />
          <Field label="Học hàm / học vị" placeholder="TS., PGS., ThS.…" error={errorFor('academicTitle')} {...register('academicTitle')} />
        </div>
      )}

      <div className="space-y-1.5">
        <label className="flex items-center gap-1.5 text-xs font-semibold text-[#3e656b]">
          Vai trò
          <span className="text-slate-400">(khoá)</span>
        </label>
        <input
          value={profile.role}
          disabled
          className="w-full cursor-not-allowed rounded-xl border border-[#cddfe3] bg-slate-50 px-3.5 py-2.5 text-sm text-slate-500"
        />
      </div>

      <div className="flex items-center justify-end gap-2 pt-1">
        <button
          type="button"
          onClick={() => reset(toFormValues(profile))}
          disabled={!isDirty || saving}
          className="inline-flex items-center gap-1.5 rounded-full border border-[#cddfe3] px-4 py-2 text-sm font-medium text-[#3e656b] transition-colors hover:bg-slate-50 disabled:opacity-50"
        >
          <Undo2 className="h-4 w-4" />
          Hủy
        </button>
        <button
          type="submit"
          disabled={!isDirty || saving}
          className="inline-flex items-center gap-1.5 rounded-full bg-[#397955] px-5 py-2 text-sm font-semibold text-white transition-colors hover:bg-[#2f6547] disabled:cursor-not-allowed disabled:opacity-50"
        >
          {saving ? <Spinner /> : <Save className="h-4 w-4" />}
          {saving ? 'Đang lưu…' : 'Lưu thay đổi'}
        </button>
      </div>
    </form>
  );
}
