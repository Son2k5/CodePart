import { z } from 'zod';
import { AUTH_MESSAGES } from '../../../constants/messages';

const hanuEmailRegex = /@hanu\.edu\.vn$/;
const studentEmailRegex = /^[0-9]+@hanu\.edu\.vn$/;
const teacherEmailRegex = /^(?![0-9]+@)[a-zA-Z0-9_.-]+@hanu\.edu\.vn$/;
const passwordRegex = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$/;

export const loginSchema = z.object({
  email: z.string().email(AUTH_MESSAGES.INVALID_EMAIL),
  password: z.string().min(1, AUTH_MESSAGES.REQUIRED_FIELD),
});
export type LoginInput = z.infer<typeof loginSchema>;

export const registerStudentSchema = z.object({
  fullName: z.string().trim().min(1, AUTH_MESSAGES.REQUIRED_FIELD),
  email: z.string()
    .email(AUTH_MESSAGES.INVALID_EMAIL)
    .regex(hanuEmailRegex, AUTH_MESSAGES.MUST_BE_HANU_EMAIL)
    .regex(studentEmailRegex, AUTH_MESSAGES.MUST_BE_STUDENT_EMAIL),
  password: z.string()
    .min(8, AUTH_MESSAGES.PASSWORD_MIN_LENGTH)
    .regex(passwordRegex, AUTH_MESSAGES.PASSWORD_PATTERN),
  confirmPassword: z.string()
}).refine((data) => data.password === data.confirmPassword, {
  message: AUTH_MESSAGES.PASSWORD_NOT_MATCH,
  path: ['confirmPassword'],
});
export type RegisterStudentInput = z.infer<typeof registerStudentSchema>;

export const registerTeacherSchema = z.object({
  fullName: z.string().trim().min(1, AUTH_MESSAGES.REQUIRED_FIELD),
  email: z.string()
    .email(AUTH_MESSAGES.INVALID_EMAIL)
    .regex(hanuEmailRegex, AUTH_MESSAGES.MUST_BE_HANU_EMAIL)
    .regex(teacherEmailRegex, AUTH_MESSAGES.MUST_BE_TEACHER_EMAIL),
  password: z.string()
    .min(8, AUTH_MESSAGES.PASSWORD_MIN_LENGTH)
    .regex(passwordRegex, AUTH_MESSAGES.PASSWORD_PATTERN),
  confirmPassword: z.string()
}).refine((data) => data.password === data.confirmPassword, {
  message: AUTH_MESSAGES.PASSWORD_NOT_MATCH,
  path: ['confirmPassword'],
});
export type RegisterTeacherInput = z.infer<typeof registerTeacherSchema>;

export const verifyOtpSchema = z.object({
  email: z.string().email(AUTH_MESSAGES.INVALID_EMAIL),
  otp: z.string().length(6, AUTH_MESSAGES.INVALID_OTP).regex(/^\d+$/, AUTH_MESSAGES.INVALID_OTP),
});
export type VerifyOtpInput = z.infer<typeof verifyOtpSchema>;

export const forgotPasswordSchema = z.object({
  email: z.string().email(AUTH_MESSAGES.INVALID_EMAIL),
});
export type ForgotPasswordInput = z.infer<typeof forgotPasswordSchema>;

export const resetPasswordSchema = z.object({
  email: z.string().email(AUTH_MESSAGES.INVALID_EMAIL),
  otp: z.string().length(6, AUTH_MESSAGES.INVALID_OTP).regex(/^\d+$/, AUTH_MESSAGES.INVALID_OTP),
  newPassword: z.string()
    .min(8, AUTH_MESSAGES.PASSWORD_MIN_LENGTH)
    .regex(passwordRegex, AUTH_MESSAGES.PASSWORD_PATTERN),
  confirmPassword: z.string()
}).refine((data) => data.newPassword === data.confirmPassword, {
  message: AUTH_MESSAGES.PASSWORD_NOT_MATCH,
  path: ['confirmPassword'],
});
export type ResetPasswordInput = z.infer<typeof resetPasswordSchema>;
