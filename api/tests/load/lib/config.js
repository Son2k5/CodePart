import http from 'k6/http';

export const BASE_URL = __ENV.BASE_URL || 'https://localhost:5001';
export const PASSWORD = __ENV.LOGIN_PASSWORD || 'ChangeThisForLoadTests1!';
const EMAIL_PREFIX = __ENV.LOGIN_EMAIL_PREFIX || 'load-user-';
const EMAIL_DOMAIN = __ENV.LOGIN_EMAIL_DOMAIN || 'example.edu';
const START_INDEX = Number.parseInt(__ENV.LOGIN_START_INDEX || '1', 10);

export function emailFor(index) {
  return `${EMAIL_PREFIX}${String(START_INDEX + index).padStart(5, '0')}@${EMAIL_DOMAIN}`;
}

export function login(index, ipAddress, tags = {}) {
  return http.post(
    `${BASE_URL}/api/auth/login`,
    JSON.stringify({ email: emailFor(index), password: PASSWORD }),
    {
      headers: {
        'Content-Type': 'application/json',
        'X-Forwarded-For': ipAddress,
      },
      tags,
    },
  );
}

export function generatedAttackEmail(index) {
  return `attack-${__VU}-${index}-${Date.now()}@invalid.example`;
}

export const commonOptions = {
  insecureSkipTLSVerify: true,
  discardResponseBodies: false,
};
