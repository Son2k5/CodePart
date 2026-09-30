import React from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useNavigate } from 'react-router-dom';
import { loginSchema, LoginInput } from '../schemas/authSchemas';
import { useLogin } from '../hooks/useAuth';
import { AuthLayout } from '../components/AuthLayout';
import { PasswordInput } from '../components/PasswordInput';

export const LoginPage: React.FC = () => {
  const { register, handleSubmit, formState: { errors } } = useForm<LoginInput>({
    resolver: zodResolver(loginSchema),
  });
  const { mutate: login, isPending } = useLogin();
  const navigate = useNavigate();

  const onSubmit = (data: LoginInput) => {
    login(data);
  };

  return (
    <AuthLayout title="Welcome Back!" subtitle="Enter Your Details Below">
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
        <div className="space-y-1">
          <label className="block text-[11px] font-semibold text-gray-500 uppercase tracking-wider">
            Email
          </label>
          <input
            {...register('email')}
            type="email"
            placeholder="hello@student.edu.vn"
            className={`w-full border-b py-2.5 text-sm text-slate-900 placeholder-gray-300 focus:outline-none bg-transparent font-medium transition-colors ${
              errors.email ? 'border-red-500 focus:border-red-600' : 'border-gray-300 focus:border-slate-900'
            }`}
            aria-invalid={!!errors.email}
          />
          {errors.email && (
            <p className="text-xs text-red-500 mt-1" role="alert">{errors.email.message}</p>
          )}
        </div>

        <PasswordInput
          label="Password"
          placeholder="••••••••"
          {...register('password')}
          error={errors.password?.message}
        />

        <div className="flex items-center justify-between pt-2">
          <label className="flex items-center gap-2 cursor-pointer group">
            <input
              type="checkbox"
              className="w-3.5 h-3.5 rounded border-gray-300 text-slate-900 focus:ring-slate-900 cursor-pointer"
            />
            <span className="text-[12px] font-medium text-gray-500 group-hover:text-gray-700 transition-colors">
              Remember me
            </span>
          </label>
          <Link to="/forgot-password" className="text-[12px] font-medium text-gray-400 hover:text-slate-900 transition-colors">
            Forgot password?
          </Link>
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
              <span>Log in</span>
            )}
          </button>
        </div>
      </form>

      <div className="mt-8 text-center text-[13px] font-medium text-gray-500">
        <p>
          Don't have an account?{' '}
          <Link to="/register" className="text-slate-900 font-bold hover:underline">
            Sign Up
          </Link>
        </p>
      </div>
    </AuthLayout>
  );
};
