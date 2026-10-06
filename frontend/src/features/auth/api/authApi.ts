import { axiosInstance } from '../../../lib/axios';
import type {
  LoginInput,
  RegisterStudentInput,
  RegisterTeacherInput,
  VerifyOtpInput,
  ForgotPasswordInput
} from '../schemas/authSchemas';
import type { LoginResponse } from '../types/auth.types';

export const authApi = {
  login: async (data: LoginInput): Promise<LoginResponse> => {
    const response = await axiosInstance.post<LoginResponse>('/auth/login', data);
    return response.data;
  },

  registerStudent: async (data: Omit<RegisterStudentInput, 'confirmPassword'>) => {
    const response = await axiosInstance.post('/auth/register', data);
    return response.data;
  },

  registerTeacher: async (data: Omit<RegisterTeacherInput, 'confirmPassword'>) => {
    const response = await axiosInstance.post('/auth/register', data);
    return response.data;
  },

  verifyOtp: async (data: VerifyOtpInput) => {
    const response = await axiosInstance.post('/auth/verify-otp', data);
    return response.data;
  },

  resendOtp: async (email: string) => {
    const response = await axiosInstance.post('/auth/resend-otp', { email });
    return response.data;
  },

  logout: async () => {
    const response = await axiosInstance.post('/auth/logout');
    return response.data;
  },

  forgotPassword: async (data: ForgotPasswordInput) => {
    const response = await axiosInstance.post('/auth/forgot-password', data);
    return response.data;
  },

  resetPassword: async (data: { token: string; newPassword: string }) => {
    const response = await axiosInstance.post('/auth/reset-password', {
      token: data.token,
      newPassword: data.newPassword,
    });
    return response.data;
  },
};
