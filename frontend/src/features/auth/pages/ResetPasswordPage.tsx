import React from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link } from 'react-router-dom';
import { resetPasswordSchema } from '../schemas/authSchemas';
import type { ResetPasswordInput } from '../schemas/authSchemas';
import { useResetPassword } from '../hooks/useAuth';
import { AuthLayout } from '../components/AuthLayout';
import { PasswordInput } from '../components/PasswordInput';

function extractTokenFromLocation(): string {
  // Backend sends link as /reset-password#token=<64hex> (see EmailSender).
  // Token is in URL fragment so it never hits the server on link open.
  const hash = window.location.hash || '';
  const hashMatch = hash.match(/token=([A-Fa-f0-9]{64})/);
  if (hashMatch) return hashMatch[1];
  const params = new URLSearchParams(window.location.search);
  return params.get('token') ?? '';
}

export const ResetPasswordPage: React.FC = () => {
  const token = extractTokenFromLocation();

  const { register, handleSubmit, formState: { errors } } = useForm<ResetPasswordInput>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { token, newPassword: '', confirmPassword: '' }
  });

  const { mutate: resetPassword, isPending } = useResetPassword();

  const onSubmit = (data: ResetPasswordInput) => {
    const { confirmPassword, ...rest } = data;
    void confirmPassword;
    resetPassword({ token: rest.token, newPassword: rest.newPassword });
  };

  if (!token) {
    return (
      <AuthLayout
        title="Invalid Reset Link"
        subtitle="Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn."
      >
        <div className="space-y-6 mt-4 text-center">
          <p className="text-sm text-gray-600 leading-relaxed">
            Vui lòng mở lại liên kết trong email (dạng <span className="font-mono">/reset-password#token=...</span>),
            hoặc yêu cầu gửi lại email đặt lại mật khẩu.
          </p>
          <div className="pt-4 flex flex-col gap-3">
            <Link
              to="/forgot-password"
              className="w-full flex items-center justify-center py-3.5 px-4 bg-[#1a1a1a] hover:bg-black text-white font-semibold text-sm rounded-full shadow-md transition-all duration-200"
            >
              Request New Link
            </Link>
            <Link to="/login" className="text-[13px] font-medium text-gray-500 hover:text-slate-900">
              Back to Login
            </Link>
          </div>
        </div>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout
      title="Create New Password"
      subtitle="Enter your new password below."
    >
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-6 mt-4">
        <input type="hidden" {...register('token')} />
        {errors.token && (
          <p className="text-xs text-red-500 text-center" role="alert">{errors.token.message}</p>
        )}

        <PasswordInput
          label="New Password"
          placeholder="••••••••"
          {...register('newPassword')}
          error={errors.newPassword?.message}
        />

        <PasswordInput
          label="Confirm Password"
          placeholder="••••••••"
          {...register('confirmPassword')}
          error={errors.confirmPassword?.message}
        />

        <div className="pt-4">
          <button
            type="submit"
            disabled={isPending}
            className="w-full flex items-center justify-center py-3.5 px-4 bg-[#1a1a1a] hover:bg-black disabled:bg-gray-400 text-white font-semibold text-sm rounded-full shadow-md transition-all duration-200"
          >
            {isPending ? (
              <div className="w-5 h-5 border-2 border-white/20 border-t-white rounded-full animate-spin"></div>
            ) : (
              <span>Reset Password</span>
            )}
          </button>
        </div>
      </form>
    </AuthLayout>
  );
};
