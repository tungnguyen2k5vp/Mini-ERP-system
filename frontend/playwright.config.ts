import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './e2e',
  workers: 1,
  use: {
    locale: 'vi-VN',
    timezoneId: 'Asia/Ho_Chi_Minh',
    baseURL: 'http://localhost:5186',
    trace: 'off',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'], channel: process.env.CI ? undefined : 'chrome' } },
    {
      name: 'firefox', testMatch: ['**/p0-reconciliation.spec.ts', '**/document-workflow.spec.ts', '**/credit-finance.spec.ts'],
      use: {
        ...devices['Desktop Firefox'],
        // Native GPU composition crashes this Firefox build during axe contrast checks on Windows.
        launchOptions: process.platform === 'win32' ? { firefoxUserPrefs: {
          'gfx.webrender.software': true, 'layers.acceleration.disabled': true, 'gfx.webrender.all': false,
        } } : {},
      },
    },
    { name: 'webkit', testMatch: ['**/p0-reconciliation.spec.ts', '**/document-workflow.spec.ts', '**/credit-finance.spec.ts'], use: devices['Desktop Safari'] },
  ],
})
