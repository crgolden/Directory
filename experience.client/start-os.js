'use strict';
const { spawnSync } = require('node:child_process');
const script = process.platform === 'win32' ? 'start:windows' : 'start:default';
const result = process.platform === 'win32'
  ? spawnSync('cmd', ['/c', 'npm', 'run', script], { stdio: 'inherit' })
  : spawnSync('npm', ['run', script], { stdio: 'inherit' });
process.exit(result.status ?? 1);
