export interface UserDto {
  id: string;
  username: string;
  role: string;
  branchId?: string;
  doctorName?: string;
  doctorColor?: string;
  doctorInitials?: string;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  user: UserDto;
}
