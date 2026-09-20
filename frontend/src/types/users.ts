import type { Role } from './api';

export interface UserSummary { userId: number; username: string; email: string; fullName: string; role: Role; isActive: boolean; createdAt: string }
export type UserDetails = UserSummary;
export interface UserPage { items: UserSummary[]; totalCount: number; page: number; pageSize: number }
export interface UserQuery { search?: string; role?: Role; isActive?: boolean; page?: number; pageSize?: number }
export interface CreateUserRequest { username: string; email: string; password: string; fullName: string; role: Role; isActive: boolean }
export interface UpdateUserRequest { username: string; email: string; fullName: string; role: Role; isActive: boolean }
export interface RoleOption { roleId: number; roleName: Role; description: string | null }
