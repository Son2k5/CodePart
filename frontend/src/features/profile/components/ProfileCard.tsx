import { Mail } from 'lucide-react';
import type { ProfileOptions, UserProfile } from '../types';
import { AvatarUploader } from './AvatarUploader';
import { Badge, roleBadge, statusBadge } from './ui';

interface ProfileCardProps {
  profile: UserProfile;
  options: ProfileOptions | undefined;
  busy: boolean;
  progress: number | null;
  onUpload: (blob: Blob) => void;
}

const findName = (list: { id: string; name: string }[] | undefined, id: string | null) =>
  id ? list?.find((o) => o.id === id)?.name ?? '—' : '—';

export function ProfileCard({ profile, options, busy, progress, onUpload }: ProfileCardProps) {
  const role = roleBadge[profile.role] ?? { label: profile.role, className: 'bg-slate-100 text-slate-600' };
  const status = statusBadge[profile.status] ?? { label: profile.status, className: 'bg-slate-100 text-slate-600' };

  return (
    <div className="rounded-3xl border border-[#dfecee] bg-white p-6 shadow-sm">
      <AvatarUploader avatarUrl={profile.avatarUrl} fullName={profile.fullName} busy={busy} progress={progress} onUpload={onUpload} />

      <div className="mt-5 text-center">
        <h2 className="break-words text-xl font-bold text-[#3e656b]">{profile.fullName}</h2>
        <p className="mt-1 flex items-center justify-center gap-1.5 text-sm text-slate-500">
          <Mail className="h-4 w-4" />
          <span className="break-all">{profile.email}</span>
        </p>
        <div className="mt-3 flex flex-wrap items-center justify-center gap-2">
          <Badge className={role.className}>{role.label}</Badge>
          <Badge className={status.className}>{status.label}</Badge>
        </div>
      </div>

      <dl className="mt-6 space-y-3 border-t border-[#eef6f7] pt-5 text-sm">
        {profile.role === 'Student' && (
          <>
            <Row label="Mã sinh viên" value={profile.studentCode ?? '—'} mono />
            <Row label="Lớp hành chính" value={findName(options?.administrativeClasses, profile.administrativeClassId)} />
          </>
        )}
        {profile.role === 'Teacher' && (
          <>
            <Row label="Khoa" value={findName(options?.faculties, profile.facultyId)} />
            <Row label="Học hàm / học vị" value={profile.academicTitle ?? '—'} />
          </>
        )}
        {profile.role === 'Admin' && <Row label="Vai trò" value="Quản trị hệ thống" />}
      </dl>
    </div>
  );
}

function Row({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="flex items-start justify-between gap-4">
      <dt className="shrink-0 text-slate-500">{label}</dt>
      <dd className={`text-right font-medium text-[#3e656b] ${mono ? 'font-mono' : ''}`}>{value}</dd>
    </div>
  );
}
