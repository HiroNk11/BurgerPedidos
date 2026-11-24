export const commerceUrl = (process.env.EXPO_PUBLIC_COMMERCE_URL ?? 'http://localhost:5180').replace(/\/+$/, '');
export const tenantId = process.env.EXPO_PUBLIC_TENANT_ID ?? '';
