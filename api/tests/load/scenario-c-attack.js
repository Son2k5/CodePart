import http from 'k6/http';
import exec from 'k6/execution';
import { check } from 'k6';
import { Counter } from 'k6/metrics';
import { BASE_URL, PASSWORD, commonOptions, generatedAttackEmail, login } from './lib/config.js';

const serverErrors = new Counter('server_errors');
const realLoginFailures = new Counter('real_login_failures');

export const options = {
  ...commonOptions,
  scenarios: {
    rotating_email_attack: {
      executor: 'constant-arrival-rate',
      exec: 'attack',
      rate: 20,
      timeUnit: '1s',
      duration: '60s',
      preAllocatedVUs: 100,
      maxVUs: 400,
    },
    real_users: {
      executor: 'shared-iterations',
      exec: 'realUsers',
      vus: 100,
      iterations: 100,
      maxDuration: '60s',
    },
  },
  thresholds: {
    server_errors: ['count==0'],
    real_login_failures: ['count==0'],
  },
};

export function attack() {
  const response = http.post(
    `${BASE_URL}/api/auth/login`,
    JSON.stringify({
      email: generatedAttackEmail(exec.scenario.iterationInTest),
      password: PASSWORD,
    }),
    {
      headers: {
        'Content-Type': 'application/json',
        'X-Forwarded-For': '203.0.113.250',
      },
      tags: { workload: 'attack' },
    },
  );
  serverErrors.add(response.status >= 500 ? 1 : 0);
  check(response, { 'attack is controlled': (result) => result.status === 401 || result.status === 429 });
}

export function realUsers() {
  const index = exec.scenario.iterationInTest;
  const response = login(index, `198.51.100.${20 + (index % 3)}`, { workload: 'real_during_attack' });
  serverErrors.add(response.status >= 500 ? 1 : 0);
  realLoginFailures.add(response.status === 200 ? 0 : 1);
  check(response, { 'real user succeeded': (result) => result.status === 200 });
}
