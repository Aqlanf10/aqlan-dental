export type StaffUser = {
  id: string;
  username: string;
  role: string;
  branchId: string | null;
  doctorName: string | null;
  doctorId: string | null;
  doctorColor: string | null;
  doctorInitials: string | null;
  mustChangePassword: boolean;
  email: string | null;
  isActive: boolean;
};

export type Session = {
  accessToken: string;
  refreshToken: string;
  user: StaffUser;
};

export type Permissions = {
  role: string;
  permissions: string[];
};
