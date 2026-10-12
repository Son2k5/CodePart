import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link } from 'react-router-dom';
import { ArrowLeft, Mail } from 'lucide-react';
import { forgotPasswordSchema } from '../schemas/authSchemas';
import type { ForgotPasswordInput } from '../schemas/authSchemas';
import { useForgotPassword } from '../hooks/useAuth';
import { BrightAuthLayout } from '../components/BrightAuthLayout';
import { AuthField } from '../components/AuthField';

export const ForgotPasswordPage = () => {
  const { register, handleSubmit, formState: { errors } } = useForm<ForgotPasswordInput>({ resolver: zodResolver(forgotPasswordSchema) });
  const { mutate: forgotPassword, isPending, isSuccess, reset } = useForgotPassword();
  return (
    <BrightAuthLayout variant="forgot" title="Quên mật khẩu?" subtitle="Nhập email đã đăng ký. Chúng tôi sẽ gửi hướng dẫn để bạn tạo mật khẩu mới.">
      {isSuccess && <div className="auth-notice" role="status">Nếu email này đã được đăng ký, bạn sẽ nhận được liên kết đặt lại mật khẩu. Hãy kiểm tra hộp thư đến và thư mục thư rác.</div>}
      <form onSubmit={handleSubmit((data) => forgotPassword(data))} className="auth-form" noValidate aria-busy={isPending}>
        <AuthField label="Email đã đăng ký" type="email" autoComplete="email" placeholder="ban@hanu.edu.vn" {...register('email', { onChange: () => { if (isSuccess) reset(); } })} error={errors.email?.message} />
        <button type="submit" disabled={isPending} className="auth-submit">{isPending ? 'Đang gửi yêu cầu…' : <><Mail size={17} /> {isSuccess ? 'Gửi lại liên kết' : 'Gửi liên kết khôi phục'}</>}</button>
      </form>
      <Link to="/login" className="auth-link auth-back"><ArrowLeft size={16} /> Quay lại đăng nhập</Link>
    </BrightAuthLayout>
  );
};
