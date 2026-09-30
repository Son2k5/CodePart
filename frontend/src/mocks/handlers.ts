import { http, HttpResponse, delay } from 'msw';
import type { User } from '../features/auth/types/auth.types';

const BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api';

export const handlers = [
  http.post(`${BASE_URL}/auth/login`, async ({ request }) => {
    await delay(1000);
    const body = await request.json() as any;
    
    if (body.email === 'pending@hanu.edu.vn') {
      return HttpResponse.json({ code: 'ACCOUNT_PENDING', message: 'Tài khoản đang chờ duyệt' }, { status: 403 });
    }
    if (body.email === 'unverified@hanu.edu.vn') {
      return HttpResponse.json({ code: 'UNVERIFIED_EMAIL', message: 'Email chưa xác thực' }, { status: 403 });
    }
    
    if (body.email === 'admin@hanu.edu.vn') {
      return HttpResponse.json({
        accessToken: 'mock-access-token',
        refreshToken: 'mock-refresh-token',
        user: { id: '1', email: body.email, fullName: 'Admin User', role: 'Admin', status: 'Active' }
      });
    }

    if (body.email.includes('teacher')) {
      return HttpResponse.json({
        accessToken: 'mock-access-token',
        refreshToken: 'mock-refresh-token',
        user: { id: '2', email: body.email, fullName: 'Teacher User', role: 'Teacher', status: 'Active' }
      });
    }

    // Default to student
    return HttpResponse.json({
      accessToken: 'mock-access-token',
      refreshToken: 'mock-refresh-token',
      user: { id: '3', email: body.email, fullName: 'Student User', role: 'Student', status: 'Active' }
    });
  }),

  http.post(`${BASE_URL}/auth/register/student`, async () => {
    await delay(1000);
    return HttpResponse.json({ message: 'Success' });
  }),

  http.post(`${BASE_URL}/auth/register/teacher`, async () => {
    await delay(1000);
    return HttpResponse.json({ message: 'Success' });
  }),

  http.post(`${BASE_URL}/auth/verify-otp`, async ({ request }) => {
    await delay(1000);
    const body = await request.json() as any;
    if (body.otp !== '123456') {
      return HttpResponse.json({ message: 'OTP không hợp lệ' }, { status: 400 });
    }
    return HttpResponse.json({ message: 'Success' });
  }),

  http.post(`${BASE_URL}/auth/resend-otp`, async () => {
    await delay(1000);
    return HttpResponse.json({ message: 'Success' });
  }),

  http.post(`${BASE_URL}/auth/forgot-password`, async () => {
    await delay(1000);
    return HttpResponse.json({ message: 'Success' });
  }),

  http.post(`${BASE_URL}/auth/reset-password`, async () => {
    await delay(1000);
    return HttpResponse.json({ message: 'Success' });
  }),

  http.post(`${BASE_URL}/auth/refresh`, async () => {
    await delay(500);
    return HttpResponse.json({
      accessToken: 'new-mock-access-token'
    });
  }),

  http.post(`${BASE_URL}/auth/logout`, async () => {
    await delay(500);
    return HttpResponse.json({ message: 'Success' });
  }),
];
