import React, { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { resetPasswordSchema, ResetPasswordInput } from '../schemas/authSchemas';
import { useResetPassword } from '../hooks/useAuth';
import { AuthLayout } from '../components/AuthLayout';
import { PasswordInput } from '../components/PasswordInput';
import { OtpInput } from '../components/OtpInput';

export const ResetPasswordPage: React.FC = () => {
  const [searchParams] = useSearchParams();
  const email = searchParams.get('email');
  const navigate = useNavigate();

  const { register, handleSubmit, setValue, watch, formState: { errors } } = useForm<ResetPasswordInput>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { email: email || '', otp: '' }
  });
  
  const { mutate: resetPassword, isPending } = useResetPassword();

  useEffect(() => {
    if (!email) {
      navigate('/forgot-password');
    }
  }, [email, navigate]);

  const onSubmit = (data: ResetPasswordInput) => {
    const { confirmPassword, ...payload } = data;
    resetPassword(payload);
  };

  if (!email) return null;

  return (
    <AuthLayout 
      title="Create New Password" 
      subtitle={`Enter the OTP sent to ${email} and your new password.`}
    >
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-6 mt-4">
        
        <div className="space-y-2">
          <label className="block text-center text-[11px] font-semibold text-gray-500 uppercase tracking-wider">
            6-Digit OTP
          </label>
          <OtpInput 
            value={watch('otp')} 
            onChange={(val) => setValue('otp', val, { shouldValidate: true })} 
            length={6} 
            error={errors.otp?.message}
          />
        </div>

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
