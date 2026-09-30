import React, { useState, useEffect } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { useVerifyOtp, useResendOtp } from '../hooks/useAuth';
import { AuthLayout } from '../components/AuthLayout';
import { OtpInput } from '../components/OtpInput';
import { ArrowLeft } from 'lucide-react';

export const VerifyOtpPage: React.FC = () => {
  const [searchParams] = useSearchParams();
  const email = searchParams.get('email');
  const navigate = useNavigate();
  
  const [otp, setOtp] = useState('');
  const [countdown, setCountdown] = useState(60);

  const { mutate: verifyOtp, isPending: isVerifying } = useVerifyOtp();
  const { mutate: resendOtp, isPending: isResending } = useResendOtp();

  useEffect(() => {
    if (!email) {
      navigate('/login');
    }
  }, [email, navigate]);

  useEffect(() => {
    let timer: NodeJS.Timeout;
    if (countdown > 0) {
      timer = setTimeout(() => setCountdown(c => c - 1), 1000);
    }
    return () => clearTimeout(timer);
  }, [countdown]);

  const handleVerify = () => {
    if (otp.length === 6 && email) {
      verifyOtp({ email, otp });
    }
  };

  const handleResend = () => {
    if (email && countdown === 0) {
      resendOtp(email, {
        onSuccess: () => setCountdown(60)
      });
    }
  };

  if (!email) return null;

  return (
    <AuthLayout 
      title="Verify Email" 
      subtitle={`We sent a 6-digit code to ${email}`}
      backButton={
        <button onClick={() => navigate('/login')} className="flex items-center gap-2 text-sm font-medium text-gray-500 hover:text-slate-900">
          <ArrowLeft className="w-4 h-4" /> Back to Login
        </button>
      }
    >
      <div className="space-y-8 mt-8">
        <OtpInput value={otp} onChange={setOtp} length={6} />
        
        <div className="pt-4">
          <button
            onClick={handleVerify}
            disabled={isVerifying || otp.length !== 6}
            className="w-full flex items-center justify-center py-3.5 px-4 bg-[#1a1a1a] hover:bg-black disabled:bg-gray-400 text-white font-semibold text-sm rounded-full shadow-md transition-all duration-200"
          >
            {isVerifying ? (
              <div className="w-5 h-5 border-2 border-white/20 border-t-white rounded-full animate-spin"></div>
            ) : (
              <span>Verify Account</span>
            )}
          </button>
        </div>

        <div className="text-center text-sm font-medium text-gray-500">
          Didn't receive the code?{' '}
          <button 
            onClick={handleResend} 
            disabled={countdown > 0 || isResending}
            className={`font-bold ${countdown > 0 ? 'text-gray-400' : 'text-slate-900 hover:underline'}`}
          >
            {countdown > 0 ? `Resend in ${countdown}s` : 'Resend Code'}
          </button>
        </div>
      </div>
    </AuthLayout>
  );
};
