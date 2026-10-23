import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link } from 'react-router-dom';
import { ArrowLeft, ArrowRight } from 'lucide-react';
import { resetPasswordSchema } from '../schemas/authSchemas';
import type { ResetPasswordInput } from '../schemas/authSchemas';
import { useResetPassword } from '../hooks/useAuth';
import { BrightAuthLayout } from '../components/BrightAuthLayout';
import { AuthField } from '../components/AuthField';

function extractTokenFromLocation(): string {
  const fragment = new URLSearchParams(window.location.hash.slice(1));
  const query = new URLSearchParams(window.location.search);
  return fragment.get('token') ?? query.get('token') ?? '';
}

export const ResetPasswordPage = () => {
  const token = extractTokenFromLocation();
  const { register, handleSubmit, formState: { errors } } = useForm<ResetPasswordInput>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { token, newPassword: '', confirmPassword: '' },
  });
  const { mutate: resetPassword, isPending } = useResetPassword();
  const validToken = /^[a-fA-F0-9]{64}$/.test(token);

  return (
    <BrightAuthLayout variant="reset" title={validToken ? 'Tạo mật khẩu mới' : 'Liên kết không hợp lệ'}
      subtitle={validToken ? 'Đặt mật khẩu mới để bảo vệ tài khoản của bạn.' : 'Vui lòng mở liên kết trong email hoặc yêu cầu một liên kết khôi phục mới.'}>
      {validToken ? (
        <form onSubmit={handleSubmit(({ token, newPassword }) => resetPassword({ token, newPassword }))} className="grid gap-5" noValidate aria-busy={isPending}>
          <input type="hidden" {...register('token')} />
          {errors.token && <p className="text-[#b43f3f] text-xs leading-[1.6]" role="alert">{errors.token.message}</p>}
          <AuthField label="Mật khẩu mới" type="password" autoComplete="new-password" placeholder="Nhập mật khẩu mới" {...register('newPassword')} error={errors.newPassword?.message} />
          <p className="text-[#6b7f83] text-[11px] leading-[1.7]">Mật khẩu từ 12 đến 128 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.</p>
          <AuthField label="Xác nhận mật khẩu" type="password" autoComplete="new-password" placeholder="Nhập lại mật khẩu mới" {...register('confirmPassword')} error={errors.confirmPassword?.message} />
          <button type="submit" disabled={isPending} className="flex items-center justify-center gap-2.5 w-full min-h-[48px] px-5 py-3 rounded-[24px] bg-[#7bd14e] text-[#244517] text-sm font-[650] shadow-[0_5px_16px_#74b65024] hover:enabled:bg-[#8ddd63] hover:enabled:-translate-y-0.5 transition-all disabled:opacity-65 disabled:cursor-wait">{isPending ? 'Đang lưu mật khẩu…' : <>Đặt lại mật khẩu <ArrowRight size={17} /></>}</button>
          <Link to="/forgot-password" className="text-center text-xs text-[#397581] font-semibold hover:underline hover:underline-offset-4">Liên kết hết hạn? Yêu cầu liên kết mới</Link>
        </form>
      ) : <Link to="/forgot-password" className="flex items-center justify-center gap-2.5 w-full min-h-[48px] px-5 py-3 rounded-[24px] bg-[#7bd14e] text-[#244517] text-sm font-[650] shadow-[0_5px_16px_#74b65024] hover:bg-[#8ddd63] transition-all">Yêu cầu liên kết mới <ArrowRight size={17} /></Link>}
      <Link to="/login" className="flex justify-center items-center gap-2 mt-6 text-[13px] text-[#397581] font-semibold hover:underline hover:underline-offset-4"><ArrowLeft size={16} /> Quay lại đăng nhập</Link>
    </BrightAuthLayout>
  );
};
