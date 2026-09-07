import { ApiError, isRecord, readBoolean, readString, requestJson } from './client';
import type { Permissions, Session, StaffUser } from '@/types/auth';

function parseUser(value: unknown): StaffUser {
  const id = readString(value, 'id');
  const username = readString(value, 'username');
  const role = readString(value, 'role');
  if (!id || !username || !role) throw new ApiError('Invalid user payload', null, 'invalid-response');

  return {
    id,
    username,
    role,
    branchId: readString(value, 'branchId'),
    doctorName: readString(value, 'doctorName'),
    doctorId: readString(value, 'doctorId'),
    doctorColor: readString(value, 'doctorColor'),
    doctorInitials: readString(value, 'doctorInitials'),
    mustChangePassword: readBoolean(value, 'mustChangePassword'),
    email: readString(value, 'email'),
    isActive: readBoolean(value, 'isActive', true),
  };
}

function parseSession(value: unknown): Session {
  if (!isRecord(value)) throw new ApiError('Invalid login payload', null, 'invalid-response');
  const accessToken = readString(value, 'accessToken');
  const refreshToken = readString(value, 'refreshToken');
  if (!accessToken || !refreshToken) throw new ApiError('Missing session token', null, 'invalid-response');
  return { accessToken, refreshToken, user: parseUser(value.user) };
}

export const authApi = {
  async login(username: string, password: string) {
    const payload = await requestJson('/api/auth/mobile/login', {
      method: 'POST',
      body: JSON.stringify({ username, password }),
    });
    return parseSession(payload);
  },

  async refresh(refreshToken: string) {
    const payload = await requestJson('/api/auth/mobile/refresh-token', {
      method: 'POST',
      refreshToken,
    });
    if (!isRecord(payload)) throw new ApiError('Invalid refresh payload', null, 'invalid-response');
    const accessToken = readString(payload, 'accessToken');
    const nextRefreshToken = readString(payload, 'refreshToken');
    if (!accessToken || !nextRefreshToken) throw new ApiError('Missing refreshed token', null, 'invalid-response');
    return { accessToken, refreshToken: nextRefreshToken };
  },

  async me(accessToken: string) {
    return parseUser(await requestJson('/api/auth/me', { accessToken }));
  },

  async permissions(accessToken: string): Promise<Permissions> {
    const payload = await requestJson('/api/auth/me/permissions', { accessToken });
    if (!isRecord(payload)) throw new ApiError('Invalid permissions payload', null, 'invalid-response');
    const role = readString(payload, 'role');
    const rawPermissions = payload.permissions;
    if (!role || !Array.isArray(rawPermissions) || !rawPermissions.every((item) => typeof item === 'string')) {
      throw new ApiError('Invalid permissions payload', null, 'invalid-response');
    }
    return { role, permissions: rawPermissions };
  },

  async logout(accessToken: string, refreshToken: string) {
    await requestJson('/api/auth/mobile/logout', {
      method: 'POST',
      accessToken,
      refreshToken,
    });
  },
};
