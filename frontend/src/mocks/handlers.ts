import { http, HttpResponse, delay } from 'msw';

const BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api';

type MockRole = 'Admin' | 'Teacher' | 'Student';
type MockUser = { id: string; email: string; fullName: string; role: MockRole; status: 'Active' };
let currentUser: MockUser | null = null;

const tokenResponse = (user: MockUser, accessToken = 'mock-access-token') => ({
  accessToken,
  expiresIn: 900,
  tokenType: 'Bearer' as const,
  user,
});

export const handlers = [
  http.post(`${BASE_URL}/auth/login`, async ({ request }) => {
    await delay(1000);
    const body = await request.json() as any;
    
    if (body.email === 'pending@hanu.edu.vn') {
      return HttpResponse.json({ code: 'ACCOUNT_PENDING', detail: 'Tài khoản đang chờ duyệt' }, { status: 403 });
    }
    if (body.email === 'unverified@hanu.edu.vn') {
      return HttpResponse.json({ code: 'UNVERIFIED_EMAIL', detail: 'Email chưa xác thực' }, { status: 403 });
    }

    if (body.email === 'admin@hanu.edu.vn') {
      currentUser = { id: '1', email: body.email, fullName: 'Admin User', role: 'Admin', status: 'Active' };
      return HttpResponse.json(tokenResponse(currentUser));
    }

    if (body.email.includes('teacher')) {
      currentUser = { id: '2', email: body.email, fullName: 'Teacher User', role: 'Teacher', status: 'Active' };
      return HttpResponse.json(tokenResponse(currentUser));
    }

    // Default to student
    currentUser = { id: '3', email: body.email, fullName: 'Student User', role: 'Student', status: 'Active' };
    return HttpResponse.json(tokenResponse(currentUser));
  }),

  http.post(`${BASE_URL}/auth/register`, async () => {
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
    return currentUser
      ? HttpResponse.json(tokenResponse(currentUser, 'new-mock-access-token'))
      : HttpResponse.json({ code: 'UNAUTHORIZED', detail: 'No active session.' }, { status: 401 });
  }),

  http.post(`${BASE_URL}/auth/logout`, async () => {
    await delay(500);
    currentUser = null;
    return HttpResponse.json({ message: 'Success' });
  }),
];
