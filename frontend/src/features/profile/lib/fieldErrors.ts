import axios from 'axios';

/**
 * Map lỗi ProblemDetails/validation từ backend về lỗi theo từng field (camelCase).
 * Backend trả: { errors: { PropertyName: ["msg", ...] }, code, title, status }.
 * PropertyName là PascalCase (FullName, PhoneNumber...) → đổi ký tự đầu về thường
 * để khớp tên field trong react-hook-form.
 */
export type FieldErrors = Record<string, string>;

function toCamel(key: string): string {
  if (!key) return '';
  return key.charAt(0).toLowerCase() + key.slice(1);
}

/** Lấy lỗi theo field. Key '' dùng cho lỗi chung của form. */
export function extractFieldErrors(error: unknown): FieldErrors {
  const result: FieldErrors = {};
  if (!axios.isAxiosError(error)) return result;
  const data = error.response?.data as Record<string, any> | undefined;
  const errors = data?.errors;
  if (errors && typeof errors === 'object') {
    for (const [key, value] of Object.entries<string[]>(errors)) {
      const message = Array.isArray(value) ? value[0] : String(value);
      if (message) result[toCamel(key)] = message;
    }
  }
  return result;
}

export function isConflict(error: unknown): boolean {
  return axios.isAxiosError(error) && error.response?.status === 409;
}

export function isForbidden(error: unknown): boolean {
  return axios.isAxiosError(error) && error.response?.status === 403;
}

export function isUnauthenticated(error: unknown): boolean {
  return axios.isAxiosError(error) && error.response?.status === 401;
}
