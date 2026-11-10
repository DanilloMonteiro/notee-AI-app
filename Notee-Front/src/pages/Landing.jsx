// src/pages/Landing.jsx
// Landing pública em "/", portada de referencias/Notee_Landing.html (v1.3).
// A demonstração é só animação de interface: não chama API, IA nem pesquisa.
import React, { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { Icon } from '../components/Icon';
import './landing.css';

const STAGES = ['write', 'question', 'connect'];
const STAGE_TABS = [
  { id: 'write', no: '01', label: 'Escreva' },
  { id: 'question', no: '02', label: 'Explore' },
  { id: 'connect', no: '03', label: 'Conecte' },
];
const CYCLE_MS = 6500;

const prefersReducedMotion = () => window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;

function Logo({ to = '/' }) {
  return (
    <Link className="logo" to={to} aria-label="Notee, início">
      <span className="logo-mark">n</span>notee<span style={{ color: '#a7c17e' }}>.</span>
    </Link>
  );
}

function scrollToSection(id) {
  document.getElementById(id)?.scrollIntoView({ behavior: prefersReducedMotion() ? 'auto' : 'smooth', block: 'start' });
}

export function Landing() {
  const [stage, setStage] = useState('write');
  const [cycleKey, setCycleKey] = useState(0); // muda para reiniciar o ciclo após interação
  const frameRef = useRef(null);
  const tabRefs = useRef({});
  // Mesmo critério do PrivateRoute: há sessão se existe access token salvo.
  const [loggedIn] = useState(() => {
    try {
      return Boolean(localStorage.getItem('notee_token'));
    } catch {
      return false;
    }
  });
  const startTo = loggedIn ? '/home' : '/register';

  // O app interno tem min-width de desktop; a landing é responsiva.
  // useLayoutEffect: aplica antes da primeira pintura, sem "pulo" de 1120px no celular.
  useLayoutEffect(() => {
    document.documentElement.classList.add('is-landing');
    document.body.classList.add('is-landing');
    return () => {
      document.documentElement.classList.remove('is-landing');
      document.body.classList.remove('is-landing');
    };
  }, []);

  // Troca automática de etapa, pausada com a aba oculta ou com hover/foco na demonstração.
  useEffect(() => {
    if (prefersReducedMotion()) return undefined;
    const id = setInterval(() => {
      const frame = frameRef.current;
      if (!frame || document.hidden || frame.matches(':hover') || frame.matches(':focus-within')) return;
      setStage(current => STAGES[(STAGES.indexOf(current) + 1) % STAGES.length]);
    }, CYCLE_MS);
    return () => clearInterval(id);
  }, [cycleKey]);

  const selectStage = useCallback((next, focus = false) => {
    setStage(next);
    setCycleKey(k => k + 1);
    if (focus) tabRefs.current[next]?.focus();
  }, []);

  const onTabKeyDown = event => {
    if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
    event.preventDefault();
    let index = STAGES.indexOf(stage);
    if (event.key === 'Home') index = 0;
    else if (event.key === 'End') index = STAGES.length - 1;
    else index = (index + (event.key === 'ArrowRight' ? 1 : -1) + STAGES.length) % STAGES.length;
    selectStage(STAGES[index], true);
  };

  const sceneProps = id => ({
    className: `nl-demo-scene nl-scene-${id}${stage === id ? ' is-active' : ''}`,
    'data-scene': id,
    role: 'tabpanel',
    id: `nl-panel-${id}`,
    'aria-labelledby': `nl-tab-${id}`,
    'aria-hidden': stage !== id,
  });

  return (
    <div className="notee-landing" id="notee-landing">
      <div className="nl-nav-wrap">
        <nav className="nl-container nl-nav" aria-label="Navegação principal">
          <Logo />
          <div className="nl-nav-links">
            <button className="nl-nav-link" type="button" onClick={() => scrollToSection('como-funciona')}>
              Como funciona
            </button>
            <button className="nl-nav-link" type="button" onClick={() => scrollToSection('recursos')}>
              Recursos
            </button>
            <button className="nl-nav-link" type="button" onClick={() => scrollToSection('experiencia')}>
              Experiência
            </button>
          </div>
          <div className="nl-nav-actions">
            {!loggedIn && (
              <Link to="/login" className="nl-btn nl-btn-ghost nl-btn-small">
                Entrar
              </Link>
            )}
            <Link to={startTo} className="nl-btn nl-btn-primary nl-btn-small">
              {loggedIn ? 'Ir para meu espaço' : 'Criar minha conta'} <Icon name="arrow" className="sm" />
            </Link>
          </div>
        </nav>
      </div>

      <main>
        <section className="nl-hero" aria-labelledby="nl-hero-heading">
          <span className="nl-hero-particle p1" aria-hidden="true">
            <Icon name="spark" className="lg" />
          </span>
          <span className="nl-hero-particle p2" aria-hidden="true">
            <Icon name="link" />
          </span>
          <span className="nl-hero-particle p3" aria-hidden="true">
            <Icon name="plus" className="sm" />
          </span>
          <div className="nl-container nl-hero-inner">
            <span className="nl-eyebrow">
              <span className="nl-live-dot" aria-hidden="true" />
              Suas ideias merecem ir além das anotações
            </span>
            <h1 id="nl-hero-heading">
              Tudo começa com uma nota.
              <br />
              <span className="nl-accent">O resto se conecta.</span>
            </h1>
            <p>
              Escreva sem perder o ritmo, transforme dúvidas em descobertas e encontre as conexões entre tudo o que
              você aprende.
            </p>
            <div className="nl-hero-actions">
              <Link to={startTo} className="nl-btn nl-btn-primary">
                {loggedIn ? 'Ir para meu espaço' : 'Criar minha conta'} <Icon name="arrow" />
              </Link>
              <button className="nl-btn" type="button" onClick={() => scrollToSection('experiencia')}>
                Conhecer a experiência <Icon name="down" className="sm" />
              </button>
            </div>
            <div className="nl-hero-note">
              <Icon name="shield" className="sm" /> Demonstração ilustrativa · dados de exemplo
            </div>
          </div>
        </section>

        <section className="nl-experience nl-container" id="experiencia" aria-labelledby="nl-experience-heading">
          <div className="nl-experience-heading">
            <div>
              <div className="nl-kicker">O Notee em movimento</div>
              <h2 id="nl-experience-heading">Veja uma ideia ganhar novas conexões.</h2>
            </div>
            <div className="nl-stage-buttons" role="tablist" aria-label="Etapas da experiência">
              {STAGE_TABS.map(tab => (
                <button
                  key={tab.id}
                  ref={el => {
                    tabRefs.current[tab.id] = el;
                  }}
                  type="button"
                  className="nl-stage-btn"
                  id={`nl-tab-${tab.id}`}
                  role="tab"
                  aria-controls={`nl-panel-${tab.id}`}
                  aria-selected={stage === tab.id}
                  tabIndex={stage === tab.id ? 0 : -1}
                  onClick={() => selectStage(tab.id)}
                  onKeyDown={onTabKeyDown}
                >
                  <strong>{tab.no}</strong> {tab.label}
                </button>
              ))}
            </div>
          </div>

          <div
            className="nl-demo-frame"
            id="nl-demo-frame"
            ref={frameRef}
            data-stage={stage}
            aria-label="Demonstração animada das funcionalidades do Notee"
          >
            <div className="nl-demo-window">
              <div className="nl-window-dots" aria-hidden="true">
                <i />
                <i />
                <i />
              </div>
              <div className="nl-window-title">
                <Icon name="lock" className="sm" /> notee / meu espaço / descobertas
              </div>
              <span className="nl-demo-state">
                <span className="nl-live-dot" aria-hidden="true" />
                Explorando ideias
              </span>
            </div>
            <div className="nl-demo-body">
              <aside className="nl-demo-sidebar" aria-hidden="true">
                <div className="nl-demo-brand">
                  <b>n</b> notee<span style={{ color: '#a7c17e' }}>.</span>
                </div>
                <div className="nl-sidebar-group">MEU ESPAÇO</div>
                <div className="nl-sidebar-item" data-side="write">
                  <Icon name="note" />
                  <span>Minhas notas</span>
                </div>
                <div className="nl-sidebar-item" data-side="question">
                  <Icon name="spark" />
                  <span>Dúvidas e pesquisas</span>
                </div>
                <div className="nl-sidebar-item" data-side="connect">
                  <Icon name="link" />
                  <span>Mapas de ideias</span>
                </div>
                <div className="nl-sidebar-item">
                  <Icon name="folder" />
                  <span>Categorias</span>
                </div>
                <div className="nl-demo-sidefooter">
                  <i>n</i> Meu espaço
                </div>
              </aside>

              <div className="nl-demo-canvas">
                <div {...sceneProps('write')}>
                  <div className="nl-scene-content">
                    <div className="nl-scene-label">MINHAS NOTAS / DESENVOLVIMENTO</div>
                    <h3>O que torna um sistema confiável?</h3>
                    <div className="nl-scene-badges">
                      <span># arquitetura</span>
                      <span># aprendizado</span>
                      <span>
                        <Icon name="lock" className="sm" /> Privada
                      </span>
                    </div>
                    <div className="nl-paper">
                      <div className="nl-paper-caption">
                        <Icon name="note" className="sm" /> Meu aprendizado, do meu jeito
                      </div>
                      <p>Hoje estudei como uma aplicação pode continuar funcionando mesmo depois de uma falha.</p>
                      <p>
                        <strong>Uma operação, dois destinos.</strong> Salvar informações no banco e enviar uma mensagem
                        para uma fila são ações diferentes.
                      </p>
                      <div className="nl-mini-rule" />
                      <p>
                        <span className="nl-typing">E se uma etapa falhar no meio do caminho?</span>
                      </p>
                      <div className="nl-muted-line" />
                      <div className="nl-muted-line short" />
                    </div>
                    <div className="nl-scene-foot">
                      <Icon name="check" className="sm" /> Escreva sem interromper o raciocínio
                    </div>
                  </div>
                  <aside className="nl-insight-panel">
                    <div className="nl-insight-heading">
                      <Icon name="spark" /> Assistente de curiosidade
                    </div>
                    <p className="nl-insight-subtitle">Aqui, o seu texto é sempre o ponto de partida.</p>
                    <div className="nl-side-card ghost">
                      <Icon name="question" />
                      <h4>Sua próxima pergunta está aqui.</h4>
                      <p>Escreva livremente. Se surgir uma dúvida, marque o trecho quando quiser.</p>
                    </div>
                    <div className="nl-side-foot">
                      <Icon name="shield" className="sm" /> O conteúdo original é preservado
                    </div>
                  </aside>
                </div>

                <div {...sceneProps('question')}>
                  <div className="nl-scene-content">
                    <div className="nl-scene-label">MINHAS NOTAS / DÚVIDA IDENTIFICADA</div>
                    <h3>O que torna um sistema confiável?</h3>
                    <div className="nl-scene-badges">
                      <span># arquitetura</span>
                      <span># aprendizado</span>
                      <span>Uma dúvida marcada</span>
                    </div>
                    <div className="nl-paper">
                      <div className="nl-paper-caption">
                        <Icon name="note" className="sm" /> Nota original
                      </div>
                      <p>Hoje estudei como uma aplicação pode continuar funcionando mesmo depois de uma falha.</p>
                      <p>Salvar informações no banco e enviar mensagens para uma fila são operações diferentes.</p>
                      <p>
                        <span className="nl-highlight-question">
                          Por que uma tarefa pode se perder entre o banco e a fila?
                        </span>
                      </p>
                      <p>Quero entender as escolhas, não só decorar seus nomes.</p>
                      <div className="nl-capture">
                        <Icon name="question" className="sm" /> Dúvida salva sem alterar a nota
                      </div>
                    </div>
                    <span className="nl-question-connector" aria-hidden="true">
                      <Icon name="arrow" />
                    </span>
                    <div className="nl-scene-foot">
                      <Icon name="check" className="sm" /> Pergunta e resposta ficam em registros separados
                    </div>
                  </div>
                  <aside className="nl-insight-panel">
                    <div className="nl-insight-heading">
                      <Icon name="spark" /> Pesquisa assistida
                    </div>
                    <p className="nl-insight-subtitle">O contexto da sua pergunta acompanha a pesquisa.</p>
                    <div className="nl-side-card">
                      <div className="nl-side-badge">
                        <Icon name="check" className="sm" /> Exemplo com fontes
                      </div>
                      <h4>Por que o padrão Outbox ajuda a evitar tarefas perdidas?</h4>
                      <p>
                        A alteração de dados e o evento podem ser registrados na mesma transação. A publicação acontece
                        depois.
                      </p>
                      <div className="nl-answer-bar">
                        <span>2 referências no exemplo</span>
                        <span className="nl-source">
                          <Icon name="external" className="sm" /> Ver fontes
                        </span>
                      </div>
                    </div>
                    <div className="nl-side-foot">
                      <Icon name="info" className="sm" /> Pesquisa simulada, não é consulta ao vivo
                    </div>
                  </aside>
                </div>

                <div {...sceneProps('connect')}>
                  <div className="nl-map-panel">
                    <div className="nl-map-label">
                      <Icon name="link" className="sm" /> Mapa de ideias · Aprendizados
                    </div>
                    <svg className="nl-map-svg" viewBox="0 0 700 478" preserveAspectRatio="none" aria-hidden="true">
                      <path d="M287 210 C228 185 198 136 120 124" />
                      <path d="M287 210 C385 127 435 119 512 105" />
                      <path d="M287 210 C210 298 196 323 161 367" />
                      <path d="M287 210 C365 302 460 313 515 347" />
                      <circle cx="287" cy="210" r="5" />
                      <circle cx="120" cy="124" r="5" />
                      <circle cx="512" cy="105" r="5" />
                      <circle cx="161" cy="367" r="5" />
                      <circle cx="515" cy="347" r="5" />
                    </svg>
                    <div className="nl-map-node is-root">
                      <i>
                        <Icon name="link" className="sm" />
                      </i>
                      <strong>Confiabilidade</strong>
                      <small>Ideia central</small>
                    </div>
                    <div className="nl-map-node is-one">
                      <i>
                        <Icon name="note" className="sm" />
                      </i>
                      <strong>Outbox</strong>
                      <small>Conceito</small>
                    </div>
                    <div className="nl-map-node is-two">
                      <i>
                        <Icon name="note" className="sm" />
                      </i>
                      <strong>Mensageria</strong>
                      <small>Aprendizado</small>
                    </div>
                    <div className="nl-map-node is-three">
                      <i>
                        <Icon name="question" className="sm" />
                      </i>
                      <strong>Falhas</strong>
                      <small>Dúvida</small>
                    </div>
                    <div className="nl-map-node is-four">
                      <i>
                        <Icon name="check" className="sm" />
                      </i>
                      <strong>Idempotência</strong>
                      <small>Conexão</small>
                    </div>
                  </div>
                  <aside className="nl-insight-panel">
                    <div className="nl-insight-heading">
                      <Icon name="spark" /> Contexto conectado
                    </div>
                    <p className="nl-insight-subtitle">Visualize os assuntos e relações que você construiu.</p>
                    <div className="nl-side-card">
                      <div className="nl-side-badge">
                        <Icon name="link" className="sm" /> Mapa de exemplo
                      </div>
                      <h4>O aprendizado não acontece em linha reta.</h4>
                      <p>Reúna conceitos relacionados, dúvidas e referências em um só espaço visual.</p>
                    </div>
                    <div className="nl-map-notice">
                      <Icon name="info" className="sm" /> Você escolhe o que entra no contexto de uma nova pesquisa.
                    </div>
                    <div className="nl-side-foot">
                      <Icon name="shield" className="sm" /> Relações sob seu controle
                    </div>
                  </aside>
                </div>
              </div>
            </div>
            <div className="nl-demo-bottom">
              <div className="nl-bottom-left">
                <Icon name="spark" className="sm" /> Animação de interface — nenhum vídeo ou IA executada
              </div>
              <Link to="/boards">
                Abrir mapas de ideias <Icon name="arrow" className="sm" />
              </Link>
            </div>
          </div>
          <div className="nl-demo-caption">
            Clique nas etapas para explorar · A apresentação alterna automaticamente quando está em repouso.
          </div>
        </section>

        <section className="nl-how" id="como-funciona" aria-labelledby="nl-how-title">
          <div className="nl-container">
            <div className="nl-divider-head">
              <span>Menos abas. Mais clareza.</span>
            </div>
            <div className="nl-section-centered">
              <div className="nl-kicker">Uma jornada natural</div>
              <h2 id="nl-how-title">Da primeira anotação à próxima descoberta.</h2>
              <p>O Notee acompanha a sua curiosidade, sem tentar pensar por você.</p>
            </div>
            <div className="nl-steps">
              <article className="nl-step-card">
                <div className="nl-step-no">
                  PASSO 01 <span>ESCREVA</span>
                </div>
                <div className="nl-step-icon">
                  <Icon name="note" className="lg" />
                </div>
                <h3>Capture o pensamento.</h3>
                <p>
                  Escreva em blocos, organize por categorias e mantenha o fluxo da ideia sem trocar de ferramenta.
                </p>
              </article>
              <article className="nl-step-card">
                <div className="nl-step-no">
                  PASSO 02 <span>PERGUNTE</span>
                </div>
                <div className="nl-step-icon">
                  <Icon name="question" className="lg" />
                </div>
                <h3>Guarde o que falta entender.</h3>
                <p>
                  Transforme trechos em dúvidas, revise respostas e consulte as referências sem apagar seu raciocínio
                  original.
                </p>
              </article>
              <article className="nl-step-card">
                <div className="nl-step-no">
                  PASSO 03 <span>CONECTE</span>
                </div>
                <div className="nl-step-icon">
                  <Icon name="link" className="lg" />
                </div>
                <h3>Enxergue o todo.</h3>
                <p>Relacione notas em mapas de ideias e descubra como um assunto pode iluminar outro.</p>
              </article>
            </div>
          </div>
        </section>

        <section className="nl-features" id="recursos" aria-labelledby="nl-features-title">
          <div className="nl-container">
            <div className="nl-feature-top">
              <div>
                <div className="nl-kicker">Pensado para quem é curioso</div>
                <h2 className="nl-section-title" id="nl-features-title">
                  Mais espaço para o que realmente importa.
                </h2>
              </div>
              <p>
                Um ambiente tranquilo para escrever, questionar, aprender e voltar às suas ideias quando elas fizerem
                sentido.
              </p>
            </div>
            <div className="nl-feature-grid">
              <article className="nl-feature-card nl-feature-card-dark">
                <div className="nl-kicker">MAPAS DE IDEIAS</div>
                <h3>Um pensamento puxa outro.</h3>
                <p>
                  Construa suas conexões e enxergue o conhecimento como uma rede, não como uma pilha de arquivos.
                </p>
                <div className="nl-feature-graph" aria-hidden="true">
                  <svg viewBox="0 0 290 200">
                    <path d="M145 98L39 42M145 98L244 29M145 98L245 158M145 98L67 179M39 42L67 179" />
                    <rect x="106" y="76" width="76" height="45" rx="10" />
                    <rect x="9" y="24" width="60" height="35" rx="8" />
                    <rect x="214" y="13" width="65" height="35" rx="8" />
                    <rect x="210" y="141" width="69" height="35" rx="8" />
                    <rect x="37" y="161" width="62" height="34" rx="8" />
                    <circle cx="145" cy="98" r="5" />
                  </svg>
                </div>
              </article>
              <article className="nl-feature-card">
                <div className="nl-kicker">PESQUISA COM CONTEXTO</div>
                <h3>Uma resposta não precisa ser um ponto final.</h3>
                <p>Guarde perguntas, respostas e fontes separadamente do texto que deu origem à curiosidade.</p>
                <div className="nl-feature-research">
                  <strong>
                    <Icon name="spark" className="sm" /> Pesquisa vinculada
                  </strong>
                  <small>Como o padrão Outbox reduz inconsistências?</small>
                  <span>
                    <Icon name="check" className="sm" /> Referências disponíveis no exemplo
                  </span>
                </div>
              </article>
              <article className="nl-feature-card nl-feature-small nl-feature-card-pale">
                <div className="nl-kicker">ESCRITA SEM DISTRAÇÕES</div>
                <h3>O pensamento continua sendo seu.</h3>
                <p>
                  Blocos editáveis, histórico de versões e um espaço que respeita o seu jeito de organizar o
                  aprendizado.
                </p>
                <div className="nl-feature-decoration" aria-hidden="true" />
              </article>
              <article className="nl-feature-card nl-feature-small">
                <div className="nl-kicker">CONTROLE E PRIVACIDADE</div>
                <h3>Compartilhe apenas quando decidir.</h3>
                <p>
                  Suas notas começam privadas. A publicação de um conteúdo é uma escolha explícita, com versão
                  independente do rascunho.
                </p>
                <div className="nl-privacy-icon" aria-hidden="true">
                  <Icon name="shield" className="lg" />
                </div>
              </article>
            </div>
          </div>
        </section>

        <section className="nl-cta-section">
          <div className="nl-container">
            <div className="nl-cta">
              <div className="nl-cta-glow" aria-hidden="true" />
              <div className="nl-kicker">Seu próximo capítulo começa aqui</div>
              <h2>Boas ideias merecem um lugar para crescer.</h2>
              <p>Comece com uma nota. Deixe que as perguntas abram novos caminhos.</p>
              <Link to={startTo} className="nl-btn">
                {loggedIn ? 'Ir para meu espaço' : 'Começar agora'} <Icon name="arrow" />
              </Link>
            </div>
          </div>
        </section>
      </main>

      <footer className="nl-footer">
        <div className="nl-container nl-footer-inner">
          <Logo />
          <span className="nl-footer-text">Notee · Anote agora. Entenda depois.</span>
          <div className="nl-footer-links">
            <button type="button" onClick={() => scrollToSection('experiencia')}>
              Experiência
            </button>
            <Link to="/library">Biblioteca</Link>
          </div>
        </div>
      </footer>
    </div>
  );
}
