import http from 'k6/http';
import { check } from 'k6';
import { Counter } from 'k6/metrics';
import { BASE_URL, commonOptions, login } from './lib/config.js';

const serverErrors = new Counter('server_errors');
const refreshFailures = new Counter('refresh_failures');
let authenticated = false;

export const options = {
  ...commonOptions,
  scenarios: {
    refresh_5000_per_minute: {
      executor: 'constant-arrival-rate',
      rate: 5000,
      timeUnit: '60s',
      duration: '60s',
      preAllocatedVUs: 300,
      maxVUs: 1000,
    },
  },
  thresholds: {
    server_errors: ['count==0'],
    refresh_failures: ['count==0'],
  },
};

export default function () {
  if (!authenticated) {
    const loginResponse = login(__VU - 1, `198.51.100.${1 + (__VU % 250)}`, { workload: 'refresh_seed' });
    authenticated = loginResponse.status === 200;
    if (!authenticated) {
      refreshFailures.add(1);
      return;
    }
  }

  const response = http.post(`${BASE_URL}/api/auth/refresh`, null, {
    headers: { 'X-Forwarded-For': `198.51.100.${1 + (__VU % 250)}` },
    tags: { workload: 'refresh' },
  });
  serverErrors.add(response.status >= 500 ? 1 : 0);
  refreshFailures.add(response.status === 200 ? 0 : 1);
  check(response, { 'refresh succeeded': (result) => result.status === 200 });
}
