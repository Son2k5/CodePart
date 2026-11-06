export type StrengthLevel = 'weak' | 'medium' | 'strong';

export interface PasswordRule {
  key: string;
  label: string;
  met: boolean;
}

export interface PasswordStrength {
  score: number; // 0..4
  level: StrengthLevel;
  rules: PasswordRule[];
}

/**
 * Quy tắc đồng bộ backend: ≥ 8 ký tự, có chữ hoa, có số, khác mật khẩu cũ.
 * Bổ sung chữ thường + ký tự đặc biệt + ≥ 12 để tính độ mạnh (0..4).
 */
export function evaluatePassword(password: string, oldPassword?: string): PasswordStrength {
  const hasLength = password.length >= 8;
  const hasUpper = /[A-Z]/.test(password);
  const hasDigit = /[0-9]/.test(password);
  const differsFromOld = !!password && password !== oldPassword;

  const rules: PasswordRule[] = [
    { key: 'length', label: 'Ít nhất 8 ký tự', met: hasLength },
    { key: 'upper', label: 'Có chữ in hoa', met: hasUpper },
    { key: 'digit', label: 'Có chữ số', met: hasDigit },
    { key: 'differs', label: 'Khác mật khẩu cũ', met: differsFromOld },
  ];

  const coreScore = [hasLength, hasUpper, hasDigit, differsFromOld].filter(Boolean).length;
  const bonus = (password.length >= 12 ? 1 : 0) + (/[a-z]/.test(password) ? 1 : 0) + (/[^a-zA-Z0-9]/.test(password) ? 1 : 0);
  const score = Math.max(0, Math.min(4, Math.round((coreScore / 4) * 2 + bonus / 3 * 2)));

  const level: StrengthLevel = score <= 1 ? 'weak' : score <= 3 ? 'medium' : 'strong';
  return { score, level, rules };
}
