import { useMutation } from '@tanstack/react-query';
import { authApi } from '../api/authApi';
import { useAuthStore } from '../store/authStore';
import { useNavigate } from 'react-router-dom';
import { AUTH_MESSAGES } from '../../../constants/messages';
import { toast } from 'sonner';
import { recordActivity, markSessionEnded } from '../sessionActivity';

import { getErrorMessage } from '../../../lib/errorService';

export const useLogin = () => {
  const setAuth = useAuthStore((state) => state.setAuth);
  const navigate = useNavigate();

  return useMutation({
    mutationFn: authApi.login,
    onSuccess: (data) => {
      recordActivity();
      setAuth(data.user, data.accessToken);
      toast.success(AUTH_MESSAGES.LOGIN_SUCCESS);
      
      if (data.user.role === 'Admin') {
        navigate('/admin');
      } else if (data.user.role === 'Teacher') {
        navigate('/teacher');
      } else {
        navigate('/home');
      }
    },
    onError: (error: any, variables) => {
      const code = error.response?.data?.code;
      if (code === 'UNVERIFIED_EMAIL') {
        toast.error('Email chưa xác thực. Vui lòng nhập mã OTP.');
        navigate(`/verify-otp?email=${encodeURIComponent(variables.email)}`);
      } else if (code === 'ACCOUNT_PENDING') {
        navigate('/pending-approval');
      } else {
        toast.error(getErrorMessage(error));
      }
    },
  });
};

export const useRegisterStudent = () => {
  const navigate = useNavigate();
  return useMutation({
    mutationFn: authApi.registerStudent,
    onSuccess: (_, variables) => {
      toast.success(AUTH_MESSAGES.REGISTER_SUCCESS);
      navigate(`/verify-otp?email=${encodeURIComponent(variables.email)}`);
    },
    onError: (error: any) => {
      toast.error(getErrorMessage(error));
    },
  });
};

export const useRegisterTeacher = () => {
  const navigate = useNavigate();
  return useMutation({
    mutationFn: authApi.registerTeacher,
    onSuccess: (_, variables) => {
      toast.success(AUTH_MESSAGES.REGISTER_SUCCESS);
      navigate(`/verify-otp?email=${encodeURIComponent(variables.email)}`);
    },
    onError: (error: any) => {
      toast.error(getErrorMessage(error));
    },
  });
};

export const useVerifyOtp = () => {
  const navigate = useNavigate();
  return useMutation({
    mutationFn: authApi.verifyOtp,
    onSuccess: (_, variables) => {
      toast.success(AUTH_MESSAGES.OTP_VERIFIED);
      const isStudent = /^\d+@hanu\.edu\.vn$/i.test(variables.email);
      navigate(isStudent ? '/login' : '/pending-approval');
    },
    onError: (error: any) => {
      toast.error(getErrorMessage(error));
    },
  });
};

export const useResendOtp = () => {
  return useMutation({
    mutationFn: authApi.resendOtp,
    onSuccess: () => {
      toast.success(AUTH_MESSAGES.OTP_SENT);
    },
    onError: (error: any) => {
      if (error.response?.status === 429) {
        toast.error(AUTH_MESSAGES.RATE_LIMIT);
      } else {
        toast.error(getErrorMessage(error));
      }
    },
  });
};

export const useForgotPassword = () => {
  return useMutation({
    mutationFn: authApi.forgotPassword,
    onSuccess: () => {
      // Backend sends reset link via email (/reset-password#token=...).
      // Do not navigate to /reset-password directly (token comes from email).
      toast.success('Nếu email đã đăng ký, hướng dẫn đặt lại mật khẩu sẽ được gửi đến hộp thư của bạn.');
    },
    onError: (error: any) => {
      toast.error(getErrorMessage(error));
    },
  });
};

export const useResetPassword = () => {
  const navigate = useNavigate();
  return useMutation({
    mutationFn: authApi.resetPassword,
    onSuccess: () => {
      toast.success(AUTH_MESSAGES.PASSWORD_RESET_SUCCESS);
      navigate('/login');
    },
    onError: (error: any) => {
      toast.error(getErrorMessage(error));
    },
  });
};

export const useLogout = () => {
  const clearAuth = useAuthStore((state) => state.clearAuth);
  const navigate = useNavigate();
  return useMutation({
    mutationFn: authApi.logout,
    onSuccess: () => {
      clearAuth();
      markSessionEnded();
      toast.success(AUTH_MESSAGES.LOGOUT_SUCCESS);
      navigate('/login');
    },
    onError: () => {
      // Even if API fails, clear local state
      clearAuth();
      markSessionEnded();
      navigate('/login');
    },
  });
};
