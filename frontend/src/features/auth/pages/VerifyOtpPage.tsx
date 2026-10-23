import { useState, useEffect } from 'react';
import { Link, useSearchParams, useNavigate } from 'react-router-dom';
import { ArrowLeft, ArrowRight } from 'lucide-react';
import { useVerifyOtp, useResendOtp } from '../hooks/useAuth';
import { BrightAuthLayout } from '../components/BrightAuthLayout';
import { OtpInput } from '../components/OtpInput';

export const VerifyOtpPage = () => {
  const [searchParams] = useSearchParams();
  const email = searchParams.get('email');
  const navigate = useNavigate();
  const [otp, setOtp] = useState('');
  const [countdown, setCountdown] = useState(60);
  const { mutate: verifyOtp, isPending: isVerifying } = useVerifyOtp();
  const { mutate: resendOtp, isPending: isResending } = useResendOtp();

  useEffect(() => {
    if (!email) navigate('/login', { replace: true });
  }, [email, navigate]);

  useEffect(() => {
    if (countdown <= 0) return;
    const timer = setTimeout(() => setCountdown(c => c - 1), 1000);
    return () => clearTimeout(timer);
  }, [countdown]);

  if (!email) return null;
  const busy = isVerifying || isResending;

  return (
    <BrightAuthLayout variant="verify" title="Xác thực email" subtitle={`Nhập mã xác thực gồm 6 chữ số được gửi đến ${email}.`}>
      <form className="grid gap-5" aria-busy={busy} onSubmit={(event) => {
        event.preventDefault();
        if (/^\d{6}$/.test(otp) && !busy) verifyOtp({ email, otp });
      }}>
        <OtpInput value={otp} onChange={setOtp} length={6} disabled={busy} />
        <p className="text-[#6b7f83] text-[11px] leading-[1.7]">Mã có hiệu lực trong 10 phút. Nếu chưa nhận được email, hãy kiểm tra cả thư mục thư rác.</p>
        <button type="submit" disabled={busy || !/^\d{6}$/.test(otp)} className="flex items-center justify-center gap-2.5 w-full min-h-[48px] px-5 py-3 rounded-[24px] bg-[#7bd14e] text-[#244517] text-sm font-[650] shadow-[0_5px_16px_#74b65024] hover:enabled:bg-[#8ddd63] hover:enabled:-translate-y-0.5 transition-all disabled:opacity-65 disabled:cursor-wait">
          {isVerifying ? 'Đang xác thực…' : <>Xác thực tài khoản <ArrowRight size={17} /></>}
        </button>
      </form>
      <p className="mt-[26px] pt-[23px] border-t border-[#edf2f2] text-center text-xs leading-[1.8] text-[#6b7f83]">Chưa nhận được mã?{' '}
        <button type="button" className="text-[#397581] font-semibold hover:underline hover:underline-offset-4 disabled:opacity-60 disabled:cursor-not-allowed disabled:no-underline cursor-pointer" disabled={countdown > 0 || busy} onClick={() => {
          if (countdown === 0 && !busy) resendOtp(email, { onSuccess: () => { setCountdown(60); setOtp(''); } });
        }}>{isResending ? 'Đang gửi…' : countdown > 0 ? `Gửi lại sau ${countdown}s` : 'Gửi lại mã'}</button>
      </p>
      <Link to="/login" className="flex justify-center items-center gap-2 mt-6 text-[13px] text-[#397581] font-semibold hover:underline hover:underline-offset-4"><ArrowLeft size={16} /> Quay lại đăng nhập</Link>
    </BrightAuthLayout>
  );
};
