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
      {isSuccess && <div className="p-[15px] rounded-xl bg-[#f2f9ed] text-[#48693b] text-[13px] leading-[1.8] mb-[22px]" role="status">Nếu email này đã được đăng ký, bạn sẽ nhận được liên kết đặt lại mật khẩu. Hãy kiểm tra hộp thư đến và thư mục thư rác.</div>}
      <form onSubmit={handleSubmit((data) => forgotPassword(data))} className="grid gap-5" noValidate aria-busy={isPending}>
        <AuthField label="Email đã đăng ký" type="email" autoComplete="email" placeholder="ban@hanu.edu.vn" {...register('email', { onChange: () => { if (isSuccess) reset(); } })} error={errors.email?.message} />
        <button type="submit" disabled={isPending} className="flex items-center justify-center gap-2.5 w-full min-h-[48px] px-5 py-3 rounded-[24px] bg-[#7bd14e] text-[#244517] text-sm font-[650] shadow-[0_5px_16px_#74b65024] hover:enabled:bg-[#8ddd63] hover:enabled:-translate-y-0.5 transition-all disabled:opacity-65 disabled:cursor-wait">{isPending ? 'Đang gửi yêu cầu…' : <><Mail size={17} /> {isSuccess ? 'Gửi lại liên kết' : 'Gửi liên kết khôi phục'}</>}</button>
      </form>
      <Link to="/login" className="flex justify-center items-center gap-2 mt-6 text-[13px] text-[#397581] font-semibold hover:underline hover:underline-offset-4"><ArrowLeft size={16} /> Quay lại đăng nhập</Link>
    </BrightAuthLayout>
  );
};
