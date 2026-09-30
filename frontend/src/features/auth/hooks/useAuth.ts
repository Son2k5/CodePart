import { useMutation } from '@tanstack/react-query';
import { authApi } from '../api/authApi';
import { useAuthStore } from '../store/authStore';
import { useNavigate } from 'react-router-dom';
import { AUTH_MESSAGES } from '../../../constants/messages';
import { toast } from 'sonner';

const getErrorMessage = (error: any) => {
  return error.response?.data?.message || error.message || 'Đã có lỗi xảy ra';
};

export const useLogin = () => {
  const setAuth = useAuthStore((state) => state.setAuth);
  const navigate = useNavigate();

  return useMutation({
    mutationFn: authApi.login,
    onSuccess: (data) => {
      setAuth(data.user, data.accessToken);
      toast.success(AUTH_MESSAGES.LOGIN_SUCCESS);
      
      if (data.user.role === 'Admin') {
        navigate('/admin');
      } else if (data.user.role === 'Teacher') {
        navigate('/teacher');
      } else {
        navigate('/courses');
      }
    },
    onError: (error: any) => {
      // Handle specific errors like Not Verified, Pending, etc.
      const code = error.response?.data?.code;
      if (code === 'UNVERIFIED_EMAIL') {
        toast.error('Email chưa xác thực. Chuyển hướng...');
        // need email state passed, let's assume login returns the email in error or we can get it from mutation args
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
    onSuccess: () => {
      toast.success(AUTH_MESSAGES.REGISTER_SUCCESS);
      navigate('/pending-approval');
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
    onSuccess: () => {
      toast.success(AUTH_MESSAGES.OTP_VERIFIED);
      navigate('/login');
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
  const navigate = useNavigate();
  return useMutation({
    mutationFn: authApi.forgotPassword,
    onSuccess: (_, variables) => {
      toast.success(AUTH_MESSAGES.OTP_SENT);
      navigate(`/reset-password?email=${encodeURIComponent(variables.email)}`);
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
      toast.success(AUTH_MESSAGES.LOGOUT_SUCCESS);
      navigate('/login');
    },
    onError: () => {
      // Even if API fails, clear local state
      clearAuth();
      navigate('/login');
    },
  });
};
