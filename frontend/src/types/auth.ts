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
