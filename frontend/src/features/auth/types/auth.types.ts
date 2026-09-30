export type Role = 'Admin' | 'Teacher' | 'Student';
export type UserStatus = 'Pending' | 'Active' | 'Rejected' | 'Disabled';

export interface User {
  id: string;
  email: string;
  fullName: string;
  role: Role;
  status: UserStatus;
  emailVerifiedAt?: string | null;
}

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
}

export interface LoginResponse extends AuthTokens {
  user: User;
}
