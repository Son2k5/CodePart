import { z } from 'zod';

// Đồng bộ validator phía backend (Users/UserValidators.cs).
const GENDERS = ['Male', 'Female', 'Other'] as const;

const notFutureDate = (value: string): boolean => {
  if (!value) return true;
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return false;
  const today = new Date();
  today.setHours(23, 59, 59, 999);
  return parsed.getTime() <= today.getTime();
};

export const profileFormSchema = z.object({
  fullName: z
    .string()
    .trim()
    .min(2, 'Họ tên phải có 2–100 ký tự.')
    .max(100, 'Họ tên phải có 2–100 ký tự.'),
  phoneNumber: z
    .string()
    .trim()
    .refine((v) => v === '' || /^(0|\+84)\d{9}$/.test(v), 'Số điện thoại không hợp lệ (VD: 0912345678 hoặc +84912345678).'),
  dateOfBirth: z
    .string()
    .refine(notFutureDate, 'Ngày sinh không được ở tương lai.'),
  gender: z.enum([...GENDERS, ''] as [string, ...string[]]).optional(),
  academicTitle: z.string().trim().max(100, 'Học hàm/học vị tối đa 100 ký tự.').optional(),
  // Không chỉnh sửa qua form thông tin — giữ nguyên khi gửi.
  avatarUrl: z.string().nullable().optional(),
  administrativeClassId: z.string().nullable().optional(),
  facultyId: z.string().nullable().optional(),
});

export type ProfileFormValues = z.infer<typeof profileFormSchema>;

export const changePasswordSchema = z
  .object({
    oldPassword: z.string().min(1, 'Nhập mật khẩu hiện tại.').max(128),
    newPassword: z
      .string()
      .min(8, 'Mật khẩu mới tối thiểu 8 ký tự.')
      .regex(/[A-Z]/, 'Mật khẩu mới cần ít nhất một chữ in hoa.')
      .regex(/[0-9]/, 'Mật khẩu mới cần ít nhất một chữ số.')
      .refine((v) => new TextEncoder().encode(v).length <= 72, 'Mật khẩu vượt giới hạn 72 byte.'),
    confirmPassword: z.string().min(1, 'Xác nhận mật khẩu.'),
  })
  .refine((data) => data.newPassword !== data.oldPassword, {
    message: 'Mật khẩu mới phải khác mật khẩu cũ.',
    path: ['newPassword'],
  })
  .refine((data) => data.newPassword === data.confirmPassword, {
    message: 'Mật khẩu xác nhận không khớp.',
    path: ['confirmPassword'],
  });

export type ChangePasswordFormValues = z.infer<typeof changePasswordSchema>;

export const AVATAR_ACCEPT = ['image/jpeg', 'image/png', 'image/webp'];
export const AVATAR_MAX_BYTES = 2 * 1024 * 1024;

/** Validate loại file + kích thước theo Magic-byte phía backend. */
export function validateAvatarFile(file: File): string | null {
  const okType = AVATAR_ACCEPT.includes(file.type);
  if (!okType) return 'Ảnh phải định dạng JPG, PNG hoặc WebP.';
  if (file.size === 0) return 'Tệp ảnh đang trống.';
  if (file.size > AVATAR_MAX_BYTES) return 'Ảnh vượt quá 2MB.';
  return null;
}
