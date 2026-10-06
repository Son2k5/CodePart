import exec from 'k6/execution';
import { check } from 'k6';
import { Counter } from 'k6/metrics';
import { commonOptions, login } from './lib/config.js';

const serverErrors = new Counter('server_errors');

export const options = {
  ...commonOptions,
  scenarios: {
    distributed_login: {
      executor: 'shared-iterations',
      vus: 300,
      iterations: 1000,
      maxDuration: '60s',
    },
  },
  thresholds: {
    server_errors: ['count==0'],
    'checks{scenario:distributed_login}': ['rate>0.99'],
  },
};

export default function () {
  const index = exec.scenario.iterationInTest;
  const thirdOctet = Math.floor(index / 250);
  const fourthOctet = (index % 250) + 1;
  const response = login(index, `198.51.${thirdOctet}.${fourthOctet}`, { workload: 'real_distributed' });
  serverErrors.add(response.status >= 500 ? 1 : 0);
  check(response, { 'login succeeded': (result) => result.status === 200 });
}
