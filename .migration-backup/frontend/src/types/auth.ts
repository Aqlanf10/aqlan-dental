export interface UserDto {
  id: string;
  username: string;
  role: string;
  branchId?: string;
  doctorName?: string;
  doctorId?: string;
  doctorColor?: string;
  doctorInitials?: string;
  mustChangePassword?: boolean;
  email?: string;
  isActive?: boolean;
  deletedAt?: string | null;
  permissions?: string[]; // permission keys from /api/auth/me/permissions
}

export interface UserPermissionsDto {
  role: string;
  permissions: string[];
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  user: UserDto;
  mustChangePassword?: boolean;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface ForgotPasswordRequest {
  usernameOrEmail: string;
}

export interface ResetPasswordRequest {
  token: string;
  newPassword: string;
  confirmPassword: string;
}

export interface ImpersonateRequest {
  reason: string;
}

export interface ImpersonateResponse {
  accessToken: string;
  user: UserDto;
}
