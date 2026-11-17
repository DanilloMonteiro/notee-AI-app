// Texto de custo dos fluxos de IA (organizar e criar nota): valor em USD ou aviso de simulação local.
export const usd = value =>
  new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'USD', minimumFractionDigits: 2, maximumFractionDigits: 4 }).format(value);

export function costText(cost, tokens, external) {
  if (!external) return 'sem custo (simulação local)';
  if (cost === null || cost === undefined) return `preço não configurado · ~${tokens.toLocaleString('pt-BR')} tokens`;
  return `${usd(cost)} · ~${tokens.toLocaleString('pt-BR')} tokens`;
}
