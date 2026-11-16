import { startFakeSourceProviders } from './fake-source-providers.mjs';

if (process.env.NOTEE_SOURCE_PROVIDER_MODE !== 'fake') {
  throw new Error('Configure NOTEE_SOURCE_PROVIDER_MODE=fake e os endpoints Sources:Providers para executar os testes de fontes sem provedores reais.');
}

export const fakeSourceProviders = await startFakeSourceProviders();
