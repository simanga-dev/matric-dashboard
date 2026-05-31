#!/usr/bin/env node
// SessionStart hook: checks development environment prerequisites

import { execSync } from 'child_process';

const projectDir = process.env.CLAUDE_PROJECT_DIR || process.cwd();

function run(cmd, opts = {}) {
  return execSync(cmd, {
    encoding: 'utf8',
    stdio: ['pipe', 'pipe', 'pipe'],
    ...opts,
  }).trim();
}

console.log('=== MatricDasbhoard Environment Check ===');

// .NET SDK
try {
  console.log(run('dotnet --version'));
  console.log('OK .NET SDK');
} catch {
  console.log('MISSING .NET SDK');
}

// dotnet-ef
try {
  const tools = run('dotnet tool list -g');
  if (tools.includes('dotnet-ef')) {
    console.log('OK dotnet-ef tool');
  } else {
    console.log('MISSING dotnet-ef (run: dotnet tool restore)');
  }
} catch {
  console.log('MISSING dotnet-ef (run: dotnet tool restore)');
}

// Docker
try {
  run('docker info');
  console.log('OK Docker');
} catch {
  console.log('MISSING Docker');
}

console.log('=== Ready ===');
