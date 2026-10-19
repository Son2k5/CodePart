"""Validate Compose contracts without starting containers or reading real .env secrets."""
import json
import os
from pathlib import Path
import subprocess
import unittest

API = Path(__file__).resolve().parents[2]


def config(production=False, profile=None):
    filename = 'docker-compose.production.yml' if production else 'docker-compose.yml'
    envfile = '.env.production.example' if production else '.env.example'
    # Empty profile prevents a developer's COMPOSE_PROFILES selecting extra services.
    env = dict(os.environ, COMPOSE_PROFILES='')
    command = ['docker', 'compose', '--env-file', envfile, '-f', filename]
    if profile:
        command += ['--profile', profile]
    command += ['config', '--format', 'json']
    result = subprocess.run(command, cwd=API, env=env, capture_output=True, text=True, encoding='utf-8')
    if result.returncode:
        # Do not print rendered config/environment, which could contain shell-provided keys.
        raise AssertionError(f'Compose validation failed: {filename}, profile={profile}, exit={result.returncode}')
    return json.loads(result.stdout)


class ComposeContractTests(unittest.TestCase):
    def test_exactly_two_compose_files(self):
        self.assertEqual({p.name for p in API.glob('*compose*.yml')},
                         {'docker-compose.yml', 'docker-compose.production.yml'})

    def test_all_six_service_sets(self):
        for production in (False, True):
            for profile in (None, 'ai', 'rag'):
                with self.subTest(production=production, profile=profile):
                    model = config(production, profile)
                    expected = {'redis', 'api', 'web'} if production else {'redis'}
                    if profile:
                        expected.add('ai-service')
                    if profile == 'rag':
                        expected.update({'qdrant', 'ai-indexer'})
                    self.assertEqual(set(model['services']), expected)

    def test_dev_ports_are_local_only_and_cache_uses_container_dns(self):
        services = config(False, 'rag')['services']
        for name, service in services.items():
            with self.subTest(service=name):
                self.assertTrue(service.get('ports'))
                for port in service['ports']:
                    self.assertEqual(port['host_ip'], '127.0.0.1')
        self.assertEqual(services['ai-service']['environment']['REDIS_URL'], 'redis://redis:6379/0')
        self.assertEqual(services['ai-service']['environment']['QDRANT_URL'], 'http://qdrant:6333')

    def test_production_has_no_internal_service_ports(self):
        model = config(True, 'rag')
        for name, service in model['services'].items():
            with self.subTest(service=name):
                if name == 'web':
                    self.assertEqual({p['target'] for p in service['ports']}, {80, 443})
                else:
                    self.assertFalse(service.get('ports'))
                self.assertIn('backend', service['networks'])
        redis = model['services']['redis']
        self.assertEqual(redis['image'], 'redis:7-alpine')
        self.assertTrue(redis['secrets'])
        self.assertIn('--aclfile', redis['command'][-1])

    def test_python_hardening_and_shared_embedding_config(self):
        for production in (False, True):
            services = config(production, 'rag')['services']
            for name in ('ai-service', 'ai-indexer'):
                with self.subTest(production=production, service=name):
                    service = services[name]
                    self.assertTrue(service['read_only'])
                    self.assertIn('ALL', service['cap_drop'])
                    self.assertIn('no-new-privileges:true', service['security_opt'])
                    self.assertGreater(int(service['mem_limit']), 0)
                    self.assertTrue(service['healthcheck'])
            for key in ('QDRANT_URL', 'QDRANT_API_KEY', 'EMBEDDING_BASE_URL', 'EMBEDDING_MODEL', 'EMBEDDING_DIMENSIONS'):
                self.assertEqual(services['ai-service']['environment'][key], services['ai-indexer']['environment'][key])

    def test_existing_volume_targets_are_preserved(self):
        for production in (False, True):
            model = config(production, 'rag')
            self.assertEqual(model['services']['redis']['volumes'][0]['source'], 'codepath-redis-data')
            self.assertEqual(model['services']['qdrant']['volumes'][0]['source'], 'qdrant-data' if production else 'qdrant-local-data')

    def test_production_profiles_validate_secrets_at_service_start(self):
        services = config(True, 'rag')['services']
        self.assertIn('SERVICE_API_KEY:?', services['ai-service']['command'][-1])
        self.assertIn('EMBEDDING_MODEL:?', services['ai-indexer']['command'][-1])
        self.assertIn('QDRANT__SERVICE__API_KEY:?', services['qdrant']['command'][-1])


if __name__ == '__main__':
    unittest.main(verbosity=2)
