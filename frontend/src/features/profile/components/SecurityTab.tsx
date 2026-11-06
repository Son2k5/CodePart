import { useState, type InputHTMLAttributes } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { AlertCircle, Check, Eye, EyeOff, ShieldCheck } from 'lucide-react';
import { toast } from 'sonner';
import { useNavigate } from 'react-router-dom';
import { changePasswordSchema, type ChangePasswordFormValues } from '../schemas/profileSchemas';
import { evaluatePassword } from '../lib/passwordStrength';
import { useChangePassword } from '../hooks/useProfile';
import { authApi } from '../../auth/api/authApi';
import { getErrorMessage } from '../../../lib/errorService';
import { extractFieldErrors, type FieldErrors } from '../lib/fieldErrors';

const strengthMeta = {
  weak: { label: 'Yếu', bar: 'w-1/3 bg-rose-400', text: 'text-rose-600' },
  medium: { label: 'Vừa', bar: 'w-2/3 bg-amber-400', text: 'text-amber-600' },
  strong: { label: 'Mạnh', bar: 'w-full bg-emerald-500', text: 'text-emerald-600' },
} as const;

function PasswordField({
  label,
  error,
  show,
  onToggle,
  ...props
}: InputHTMLAttributes<HTMLInputElement> & { label: string; error?: string; show: boolean; onToggle: () => void }) {
  return (
    <div className="space-y-1.5">
      <label className="text-xs font-semibold text-[#3e656b]">{label}</label>
      <div className="relative">
        <input
          {...props}
          type={show ? 'text' : 'password'}
          aria-invalid={!!error}
          className={`w-full rounded-xl border bg-white px-3.5 py-2.5 pr-11 text-sm text-slate-800 outline-none transition-colors focus:ring-2 ${
            error ? 'border-rose-300 focus:border-rose-400 focus:ring-rose-100' : 'border-[#cddfe3] focus:border-[#397c8c] focus:ring-[#edf9fa]'
          }`}
        />
        <button type="button" onClick={onToggle} tabIndex={-1} aria-label={show ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'} className="absolute right-2 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600">
          {show ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
        </button>
      </div>
      {error && (
        <p role="alert" className="flex items-center gap-1 text-xs text-rose-600">
          <AlertCircle className="h-3.5 w-3.5 shrink-0" />
          {error}
        </p>
      )}
    </div>
  );
}

export function SecurityTab() {
  const navigate = useNavigate();
  const changePassword = useChangePassword();
  const [show, setShow] = useState({ old: false, neu: false, confirm: false });
  const [serverErrors, setServerErrors] = useState<FieldErrors>({});

  const {
    register,
    handleSubmit,
    watch,
    reset,
    formState: { errors },
  } = useForm<ChangePasswordFormValues>({ resolver: zodResolver(changePasswordSchema), mode: 'onTouched' });

  const oldPassword = watch('oldPassword') ?? '';
  const newPassword = watch('newPassword') ?? '';
  const strength = evaluatePassword(newPassword, oldPassword);
  const meta = strengthMeta[strength.level];

  const onSubmit = handleSubmit(async (values) => {
    setServerErrors({});
    try {
      await changePassword.mutateAsync(values);
      toast.success('Đổi mật khẩu thành công. Vui lòng đăng nhập lại.');
      reset();
      await authApi.logout();
      navigate('/login', { replace: true });
    } catch (err) {
      const fe = extractFieldErrors(err);
      const general = getErrorMessage(err, 'Không thể đổi mật khẩu. Kiểm tra lại thông tin.');
      if (Object.keys(fe).length) setServerErrors(fe);
      else setServerErrors({ '': general });
      toast.error(general, { id: 'change-password' });
    }
  });

  const errorFor = (key: keyof ChangePasswordFormValues) => (errors[key]?.message as string) || serverErrors[key];

  return (
    <form onSubmit={onSubmit} className="space-y-5">
      {serverErrors[''] && (
        <div className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">{serverErrors['']}</div>
      )}
      <div className="flex items-start gap-3 rounded-2xl bg-[#edf9fa] p-4 text-sm text-[#3e656b]">
        <ShieldCheck className="mt-0.5 h-5 w-5 shrink-0 text-[#397c8c]" />
        <p>Sau khi đổi mật khẩu, tất cả phiên đăng nhập hiện tại bị thu hồi và bạn sẽ phải đăng nhập lại.</p>
      </div>

      <PasswordField label="Mật khẩu hiện tại" autoComplete="current-password" show={show.old} onToggle={() => setShow((s) => ({ ...s, old: !s.old }))} error={errorFor('oldPassword')} {...register('oldPassword')} />

      <div className="space-y-3">
        <PasswordField label="Mật khẩu mới" autoComplete="new-password" show={show.neu} onToggle={() => setShow((s) => ({ ...s, neu: !s.neu }))} error={errorFor('newPassword')} {...register('newPassword')} />
        {newPassword && (
          <div className="space-y-2">
            <div className="flex items-center gap-2">
              <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-slate-100">
                <div className={`h-full rounded-full transition-all ${meta.bar}`} />
              </div>
              <span className={`w-10 text-right text-xs font-semibold ${meta.text}`}>{meta.label}</span>
            </div>
            <ul className="grid grid-cols-1 gap-1 sm:grid-cols-2">
              {strength.rules.map((rule) => (
                <li key={rule.key} className={`flex items-center gap-1.5 text-xs ${rule.met ? 'text-emerald-600' : 'text-slate-400'}`}>
                  {rule.met ? <Check className="h-3.5 w-3.5" /> : <AlertCircle className="h-3.5 w-3.5" />}
                  {rule.label}
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>

      <PasswordField label="Xác nhận mật khẩu mới" autoComplete="new-password" show={show.confirm} onToggle={() => setShow((s) => ({ ...s, confirm: !s.confirm }))} error={errorFor('confirmPassword')} {...register('confirmPassword')} />

      <div className="flex justify-end">
        <button
          type="submit"
          disabled={changePassword.isPending}
          className="inline-flex items-center gap-1.5 rounded-full bg-[#397955] px-5 py-2 text-sm font-semibold text-white transition-colors hover:bg-[#2f6547] disabled:cursor-not-allowed disabled:opacity-60"
        >
          {changePassword.isPending ? 'Đang đổi…' : 'Đổi mật khẩu'}
        </button>
      </div>
    </form>
  );
}
