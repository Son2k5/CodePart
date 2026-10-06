import exec from 'k6/execution';
import { check } from 'k6';
import { Counter } from 'k6/metrics';
import { commonOptions, login } from './lib/config.js';

const serverErrors = new Counter('server_errors');

export const options = {
  ...commonOptions,
  scenarios: {
    nat_login: {
      executor: 'shared-iterations',
      vus: 300,
      iterations: 1000,
      maxDuration: '60s',
    },
  },
  thresholds: {
    server_errors: ['count==0'],
    'http_req_duration{scenario:nat_login}': ['p(95)<5000'],
    'checks{scenario:nat_login}': ['rate>0.99'],
  },
};

export default function () {
  const index = exec.scenario.iterationInTest;
  const ip = `198.51.100.${10 + (index % 3)}`;
  const response = login(index, ip, { workload: 'real_nat' });
  serverErrors.add(response.status >= 500 ? 1 : 0);
  check(response, { 'login succeeded': (result) => result.status === 200 });
}
