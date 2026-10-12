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
      <form onSubmit={handleSubmit((data) => login(data))} className="auth-form" noValidate aria-busy={isPending}>
        <AuthField label="Email" type="email" autoComplete="username" placeholder="ban@hanu.edu.vn" {...register('email')} error={errors.email?.message} />
        <AuthField label="Mật khẩu" type="password" autoComplete="current-password" placeholder="Nhập mật khẩu của bạn" {...register('password')} error={errors.password?.message} />
        <Link to="/forgot-password" className="auth-link auth-forgot-link">Quên mật khẩu?</Link>
        <button type="submit" disabled={isPending} className="auth-submit">{isPending ? 'Đang đăng nhập…' : <>Đăng nhập <ArrowRight size={17} /></>}</button>
      </form>
      <p className="auth-switch">Chưa có tài khoản? <Link to="/register" className="auth-link">Đăng ký ngay</Link></p>
    </BrightAuthLayout>
  );
};
