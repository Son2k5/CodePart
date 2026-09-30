import React from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useNavigate, Link } from 'react-router-dom';
import { forgotPasswordSchema, ForgotPasswordInput } from '../schemas/authSchemas';
import { useForgotPassword } from '../hooks/useAuth';
import { AuthLayout } from '../components/AuthLayout';
import { ArrowLeft } from 'lucide-react';

export const ForgotPasswordPage: React.FC = () => {
  const { register, handleSubmit, formState: { errors } } = useForm<ForgotPasswordInput>({
    resolver: zodResolver(forgotPasswordSchema),
  });
  const { mutate: forgotPassword, isPending } = useForgotPassword();
  const navigate = useNavigate();

  const onSubmit = (data: ForgotPasswordInput) => {
    forgotPassword(data);
  };

  return (
    <AuthLayout 
      title="Reset Password" 
      subtitle="Enter your email to receive a reset link/OTP"
      backButton={
        <Link to="/login" className="flex items-center gap-2 text-sm font-medium text-gray-500 hover:text-slate-900">
          <ArrowLeft className="w-4 h-4" /> Back to Login
        </Link>
      }
    >
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-6 mt-4">
        <div className="space-y-1">
          <label className="block text-[11px] font-semibold text-gray-500 uppercase tracking-wider">
            Email
          </label>
          <input
            {...register('email')}
            type="email"
            placeholder="hello@hanu.edu.vn"
            className={`w-full border-b py-2.5 text-sm text-slate-900 placeholder-gray-300 focus:outline-none bg-transparent font-medium transition-colors ${
              errors.email ? 'border-red-500 focus:border-red-600' : 'border-gray-300 focus:border-slate-900'
            }`}
          />
          {errors.email && <p className="text-xs text-red-500 mt-1">{errors.email.message}</p>}
        </div>

        <div className="pt-4">
          <button
            type="submit"
            disabled={isPending}
            className="w-full flex items-center justify-center py-3.5 px-4 bg-[#1a1a1a] hover:bg-black disabled:bg-gray-400 text-white font-semibold text-sm rounded-full shadow-md transition-all duration-200"
          >
            {isPending ? (
              <div className="w-5 h-5 border-2 border-white/20 border-t-white rounded-full animate-spin"></div>
            ) : (
              <span>Send Reset OTP</span>
            )}
          </button>
        </div>
      </form>
    </AuthLayout>
  );
};
