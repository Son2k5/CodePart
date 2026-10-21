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
      <form className="auth-form" aria-busy={busy} onSubmit={(event) => {
        event.preventDefault();
        if (/^\d{6}$/.test(otp) && !busy) verifyOtp({ email, otp });
      }}>
        <OtpInput value={otp} onChange={setOtp} length={6} disabled={busy} />
        <p className="auth-hint">Mã có hiệu lực trong 10 phút. Nếu chưa nhận được email, hãy kiểm tra cả thư mục thư rác.</p>
        <button type="submit" disabled={busy || !/^\d{6}$/.test(otp)} className="auth-submit">
          {isVerifying ? 'Đang xác thực…' : <>Xác thực tài khoản <ArrowRight size={17} /></>}
        </button>
      </form>
      <p className="auth-switch">Chưa nhận được mã?{' '}
        <button type="button" className="auth-link" disabled={countdown > 0 || busy} onClick={() => {
          if (countdown === 0 && !busy) resendOtp(email, { onSuccess: () => { setCountdown(60); setOtp(''); } });
        }}>{isResending ? 'Đang gửi…' : countdown > 0 ? `Gửi lại sau ${countdown}s` : 'Gửi lại mã'}</button>
      </p>
      <Link to="/login" className="auth-link auth-back"><ArrowLeft size={16} /> Quay lại đăng nhập</Link>
    </BrightAuthLayout>
  );
};
