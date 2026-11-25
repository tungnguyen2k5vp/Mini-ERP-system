import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './e2e',
  workers: 1,
  use: {
    ...devices['Desktop Chrome'],
    baseURL: 'http://localhost:5186',
    channel: process.env.CI ? undefined : 'chrome',
    trace: 'off',
  },
})
