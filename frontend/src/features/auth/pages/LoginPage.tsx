import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link } from 'react-router-dom';
import { ArrowRight } from 'lucide-react';
import { loginSchema } from '../schemas/authSchemas';
import type { LoginInput } from '../schemas/authSchemas';
import { useLogin } from '../hooks/useAuth';
import { BrightAuthLayout } from '../components/BrightAuthLayout';
import { AuthField } from '../components/AuthField';

export const LoginPage = () => {
  const { register, handleSubmit, formState: { errors } } = useForm<LoginInput>({ resolver: zodResolver(loginSchema) });
  const { mutate: login, isPending } = useLogin();
  return (
    <BrightAuthLayout variant="login" title="Chào mừng trở lại!" subtitle="Đăng nhập để tiếp tục hành trình học tập của bạn.">
      <form onSubmit={handleSubmit((data) => login(data))} className="grid gap-5" noValidate aria-busy={isPending}>
        <AuthField label="Email" type="email" autoComplete="username" placeholder="ban@hanu.edu.vn" {...register('email')} error={errors.email?.message} />
        <AuthField label="Mật khẩu" type="password" autoComplete="current-password" placeholder="Nhập mật khẩu của bạn" {...register('password')} error={errors.password?.message} />
        <Link to="/forgot-password" className="justify-self-end -mt-1.5 text-xs text-[#397581] font-semibold hover:underline hover:underline-offset-4">Quên mật khẩu?</Link>
        <button type="submit" disabled={isPending} className="flex items-center justify-center gap-2.5 w-full min-h-[48px] px-5 py-3 rounded-[24px] bg-[#7bd14e] text-[#244517] text-sm font-[650] shadow-[0_5px_16px_#74b65024] hover:enabled:bg-[#8ddd63] hover:enabled:-translate-y-0.5 transition-all disabled:opacity-65 disabled:cursor-wait">{isPending ? 'Đang đăng nhập…' : <>Đăng nhập <ArrowRight size={17} /></>}</button>
      </form>
      <p className="mt-[26px] pt-[23px] border-t border-[#edf2f2] text-center text-xs leading-[1.8] text-[#6b7f83]">Chưa có tài khoản? <Link to="/register" className="text-[#397581] font-semibold hover:underline hover:underline-offset-4">Đăng ký ngay</Link></p>
    </BrightAuthLayout>
  );
};
