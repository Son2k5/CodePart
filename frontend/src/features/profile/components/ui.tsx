import React from 'react';
import { AlertCircle, Info, Lock } from 'lucide-react';

export const roleBadge: Record<string, { label: string; className: string }> = {
  Student: { label: 'Sinh viên', className: 'bg-sky-100 text-sky-700' },
  Teacher: { label: 'Giảng viên', className: 'bg-violet-100 text-violet-700' },
  Admin: { label: 'Quản trị', className: 'bg-amber-100 text-amber-700' },
};

export const statusBadge: Record<string, { label: string; className: string }> = {
  Active: { label: 'Đang hoạt động', className: 'bg-emerald-100 text-emerald-700' },
  Pending: { label: 'Chờ phê duyệt', className: 'bg-amber-100 text-amber-700' },
  Disabled: { label: 'Đã khóa', className: 'bg-rose-100 text-rose-700' },
  Rejected: { label: 'Đã từ chối', className: 'bg-slate-200 text-slate-600' },
};

export function Badge({ className, children }: { className: string; children: React.ReactNode }) {
  return (
    <span className={`inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-[11px] font-semibold ${className}`}>
      {children}
    </span>
  );
}

interface FieldProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label: string;
  error?: string;
  locked?: boolean;
  hint?: string;
}

/** Ô nhập có nhãn, lỗi inline (pastel + icon), và trạng thái khoá + tooltip. */
export const Field = React.forwardRef<HTMLInputElement, FieldProps>(
  ({ label, error, locked, hint, className = '', ...props }, ref) => (
    <div className="space-y-1.5">
      <label className="flex items-center gap-1.5 text-xs font-semibold text-[#3e656b]">
        {label}
        {locked && (
          <span className="group relative inline-flex items-center">
            <Lock className="h-3.5 w-3.5 text-slate-400" />
            <span className="pointer-events-none absolute left-1/2 top-6 z-10 hidden w-max max-w-[220px] -translate-x-1/2 rounded-lg bg-[#3e656b] px-2.5 py-1.5 text-[11px] font-medium text-white shadow-lg group-hover:block">
              Liên hệ admin để thay đổi
            </span>
          </span>
        )}
      </label>
      <input
        ref={ref}
        disabled={locked || props.disabled}
        aria-invalid={!!error}
        className={`w-full rounded-xl border bg-white px-3.5 py-2.5 text-sm text-slate-800 outline-none transition-colors placeholder:text-slate-300 focus:ring-2 disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-500 ${
          error
            ? 'border-rose-300 focus:border-rose-400 focus:ring-rose-100'
            : 'border-[#cddfe3] focus:border-[#397c8c] focus:ring-[#edf9fa]'
        } ${className}`}
        {...props}
      />
      {error ? (
        <p role="alert" className="flex items-center gap-1 text-xs text-rose-600">
          <AlertCircle className="h-3.5 w-3.5 shrink-0" />
          {error}
        </p>
      ) : hint ? (
        <p className="flex items-center gap-1 text-xs text-slate-400">
          <Info className="h-3.5 w-3.5 shrink-0" />
          {hint}
        </p>
      ) : null}
    </div>
  ),
);
Field.displayName = 'Field';

interface SegmentedProps {
  value: string;
  onChange: (value: string) => void;
  options: { value: string; label: string }[];
  disabled?: boolean;
}

export function SegmentedControl({ value, onChange, options, disabled }: SegmentedProps) {
  return (
    <div className={`inline-flex rounded-xl border border-[#cddfe3] bg-[#edf9fa]/60 p-1 ${disabled ? 'opacity-60' : ''}`}>
      {options.map((opt) => (
        <button
          key={opt.value}
          type="button"
          disabled={disabled}
          onClick={() => onChange(opt.value)}
          className={`rounded-lg px-4 py-1.5 text-sm font-medium transition-all ${
            value === opt.value
              ? 'bg-white text-[#397c8c] shadow-sm'
              : 'text-[#5b7c82] hover:text-[#397c8c]'
          }`}
        >
          {opt.label}
        </button>
      ))}
    </div>
  );
}

export function Spinner({ className = 'h-4 w-4' }: { className?: string }) {
  return (
    <span
      className={`inline-block animate-spin rounded-full border-2 border-current border-t-transparent ${className}`}
      aria-hidden="true"
    />
  );
}

export function ShimmerBlock({ className = '' }: { className?: string }) {
  return <div className={`cp-shimmer rounded-xl ${className}`} />;
}

export function EmptyState({
  icon,
  title,
  description,
  onRetry,
}: {
  icon: React.ReactNode;
  title: string;
  description: string;
  onRetry?: () => void;
}) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 rounded-3xl border border-[#dfecee] bg-white/70 px-6 py-14 text-center">
      <div className="grid h-16 w-16 place-items-center rounded-2xl bg-[#edf9fa] text-[#397c8c]">{icon}</div>
      <div>
        <p className="text-base font-semibold text-[#3e656b]">{title}</p>
        <p className="mt-1 text-sm text-slate-500">{description}</p>
      </div>
      {onRetry && (
        <button
          type="button"
          onClick={onRetry}
          className="mt-1 rounded-full border border-[#cddfe3] bg-white px-5 py-2 text-sm font-medium text-[#397c8c] transition-colors hover:bg-[#edf9fa]"
        >
          Thử lại
        </button>
      )}
    </div>
  );
}
