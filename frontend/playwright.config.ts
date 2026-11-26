import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './e2e',
  workers: 1,
  use: {
    ...devices['Desktop Chrome'],
    locale: 'vi-VN',
    timezoneId: 'Asia/Ho_Chi_Minh',
    baseURL: 'http://localhost:5186',
    channel: process.env.CI ? undefined : 'chrome',
    trace: 'off',
  },
})
