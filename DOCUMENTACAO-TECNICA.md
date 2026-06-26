# Documentação Técnica — Bendita Coxinha

**Versão:** 1.0.0  
**Data:** Junho 2026  
**Stack:** ASP.NET Core 8 · Next.js 14 · PostgreSQL 16 · Docker

---

## 1. Visão Geral

O sistema Bendita Coxinha é uma plataforma de gestão completa para restaurante derivada do projeto [SoftNerd](https://github.com/Taino-Edu/softNerd). A principal decisão de arquitetura na adaptação foi **simplificar o stack de banco de dados**: o MongoDB (usado no SoftNerd como event store para vendas avulsas e cache de cartas TCG) foi removido. Todo o estado do sistema reside em um único banco PostgreSQL, reduzindo complexidade operacional e custos de infraestrutura.

### O que mudou em relação ao SoftNerd

| SoftNerd | Bendita Coxinha |
|---|---|
| MongoDB (event store + cache TCG) | Removido — VendaAvulsa em PostgreSQL |
| Módulo de Cartas TCG | Removido |
| APIs Pokémon / Scryfall / YuGiOh | Removidas |
| AuditLog imutável (SHA-256 IPs) | Removido — LGPD simplificado |
| Campeonatos | Renomeado para Eventos |
| `/admin/cartas` | Removido |

---

## 2. Arquitetura

### 2.1 Visão de alto nível

```
┌─────────────────────────────────────────────────────────────────┐
│                        Internet                                  │
└─────────────────────────┬───────────────────────────────────────┘
                          │ HTTPS
                ┌─────────▼─────────┐
                │    Cloudflare      │  DNS + SSL/TLS (termina HTTPS)
                └─────────┬─────────┘
                          │ HTTP :80
                ┌─────────▼─────────┐
                │   Nginx 1.27      │  Proxy reverso + uploads estáticos
                └──────┬──────┬─────┘
                       │      │
          ┌────────────▼┐    ┌▼────────────────┐
          │  Next.js 14 │    │  ASP.NET Core 8  │
          │  :3000      │    │  :5000           │
          └─────────────┘    └────────┬─────────┘
                                      │
                             ┌────────▼─────────┐
                             │  PostgreSQL 16    │
                             │  :5432            │
                             └──────────────────┘
```

### 2.2 Comunicação em tempo real

Comandas usam **SignalR** (WebSocket com fallback para Long Polling):

```
Cliente (celular/mesa) ──── WS /hubs/comanda ────► ComandaHub
Admin (dashboard)      ────                    ◄─── notificações push
```

O Nginx roteia `/hubs/` com headers `Upgrade: websocket` para manter a conexão persistente.

---

## 3. Backend — ASP.NET Core 8

### 3.1 Organização de pastas

```
BenditaCoxinha/
├── Controllers/          # Camada HTTP — validação, autorização, resposta
│   ├── AuthController        /api/auth/*
│   ├── ProductController     /api/product/*
│   ├── ComandaController     /api/comanda/*
│   ├── VendaAvulsaController /api/venda-avulsa/*
│   ├── CrediariosController  /api/crediario/*
│   ├── ChampionshipController /api/championship/*   ← (Eventos)
│   ├── UserController        /api/user/*
│   ├── AnnouncementController /api/announcement/*
│   ├── CategoryController    /api/category/*
│   ├── UploadController      /api/upload/*
│   ├── AiChatController      /api/ai/chat
│   ├── LgpdController        /api/lgpd/*
│   ├── RelatoriosController  /api/relatorios/*
│   ├── PerfisController      /api/perfis/*
│   └── AnalyticsController   /api/analytics/*
│
├── Services/
│   ├── Interfaces/       # Contratos (IAuthService, IProductService…)
│   └── Implementations/  # Lógica de negócio
│       ├── AuthService
│       ├── ProductService
│       ├── ComandaService
│       ├── VendaAvulsaService    ← lê/grava em PostgreSQL
│       ├── CreditarioService
│       ├── ChampionshipService   ← (Eventos)
│       ├── UserService
│       ├── AnnouncementService
│       ├── EmailService
│       ├── GeminiChatService
│       └── CategoryService
│
├── Models/
│   └── PostgreSQL/       # Entidades EF Core (sem MongoDB)
│       ├── User
│       ├── Product / ProductCategory
│       ├── Comanda / ComandaItem
│       ├── Championship              ← (Eventos)
│       ├── VendaAvulsa               ← novo (era MongoDB)
│       ├── Crediario / PagamentoCrediario
│       ├── Announcement
│       ├── LgpdRequest / CookieConsent
│       └── Perfil
│
├── Data/
│   ├── AppDbContext.cs   # Único DbContext — todos os DbSets
│   └── Migrations/       # EF Core migrations (PostgreSQL + SQLite)
│
├── Hubs/
│   └── ComandaHub.cs     # SignalR — notifica dashboard em tempo real
│
├── Middleware/
│   └── OperatorPermissionMiddleware.cs  # RBAC granular por rota
│
├── Configuration/
│   └── Settings.cs       # JwtSettings, EmailSettings, GeminiSettings
│
└── HealthChecks/
    └── DbHealthCheck.cs  # Probe do PostgreSQL em /health
```

### 3.2 Banco de dados

**Um único banco: PostgreSQL 16**

Em desenvolvimento a aplicação usa **SQLite** automaticamente (sem configurar nada). Em produção usa PostgreSQL via variável `ConnectionStrings__PostgreSQL`.

```csharp
// Program.cs — detecção automática
var pgConnStr = builder.Configuration.GetConnectionString("PostgreSQL");
var useSqlite = string.IsNullOrWhiteSpace(pgConnStr);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (useSqlite)
        options.UseSqlite($"Data Source={dbPath}");
    else
        options.UseNpgsql(pgConnStr, o => o.EnableRetryOnFailure(5));
});
```

O schema é criado via `EnsureCreated` no startup — sem necessidade de rodar migrations manualmente.

### 3.3 Tabelas principais

| Tabela | Descrição |
|---|---|
| `users` | Clientes e admins — com pontos de fidelidade e cashback |
| `products` | Cardápio (pratos, bebidas) com categorias, estoque e promoções |
| `product_categories` | Categorias com emoji (Entrada 🍽️, Bebida 🥤…) |
| `comandas` | Pedidos de mesa — status: Aberta / Fechada / Cancelada |
| `comanda_items` | Itens de cada comanda com snapshot do preço |
| `vendas_avulsas` | Vendas de balcão (PDV) — itens em JSON |
| `championships` | Eventos do restaurante — com vagas e inscrições |
| `crediarios` | Crédito interno (fiado) por cliente |
| `pagamento_crediarios` | Histórico de pagamentos do fiado |
| `announcements` | Anúncios e promoções |
| `perfis` | Perfis de operador com permissões granulares (JSON) |
| `lgpd_requests` | Solicitações LGPD (acesso / exclusão de dados) |
| `cookie_consents` | Registro de consentimento de cookies |

### 3.4 Autenticação e autorização

- **JWT HS256** em cookies `HttpOnly; SameSite=Lax` — nunca exposto no corpo da resposta
- **Refresh token** em cookie separado (90 dias)
- **RBAC** com 3 perfis base: `Admin`, `Operator`, `Customer`
- **Perfis customizados** com permissões granulares por rota (tabela `perfis`, JSON)
- **Rate limiting**: auth 5 req/min · API 200 req/min · global 300 req/min por IP

### 3.5 VendaAvulsa — migração do MongoDB para PostgreSQL

No SoftNerd, as vendas avulsas eram documentos MongoDB (event store). Aqui a entidade é uma tabela PostgreSQL com os **itens serializados em JSON** — mesma estratégia usada pelo Crediário:

```csharp
public class VendaAvulsa
{
    public Guid    Id           { get; set; }
    public string  PaymentMethod{ get; set; }
    public int     TotalInCents { get; set; }
    public string  ItensJson    { get; set; }  // List<VendaAvulsaItem> serializado
    // ...
}
```

Essa abordagem evita um JOIN extra e mantém os itens imutáveis após o registro.

---

## 4. Frontend — Next.js 14

### 4.1 Organização de rotas (App Router)

```
frontend/app/
├── (site)/              # Site público do restaurante
│   ├── page.tsx             Landing page
│   ├── cardapio/            Cardápio online público
│   └── eventos/             Lista de eventos
│
├── admin/               # Painel administrativo (requer Auth: Admin/Operator)
│   ├── dashboard/           Mesas abertas em tempo real (SignalR)
│   ├── venda-avulsa/        PDV — venda no balcão
│   ├── estoque/             Gestão do cardápio (produtos)
│   ├── eventos/             Gestão de eventos
│   ├── crediario/           Fiado e pagamentos
│   ├── financeiro/          Receita, custos e margens
│   ├── relatorios/          Vendas por categoria/produto
│   ├── usuarios/            Gestão de clientes
│   ├── qrcodes/             Geração de QR Codes por mesa
│   ├── anuncios/            Promoções e avisos
│   ├── categorias/          Categorias do cardápio
│   ├── perfis/              Perfis de operador
│   └── lgpd/                Gestão de solicitações LGPD
│
├── mesa/[mesa]/         # Flow cliente: escaneia QR → faz pedido
├── cliente/             # Área do cliente logado (histórico, pontos)
├── login/               # Autenticação
├── lgpd/                # Formulário público LGPD
├── privacidade/         # Política de privacidade
└── termos/              # Termos de uso
```

### 4.2 Comunicação com a API

Toda comunicação com o backend passa por `frontend/lib/api.ts` — um cliente Axios com:
- **Interceptor de request**: injeta token via cookie (automático)
- **Interceptor de response**: trata 401 e faz refresh automático do token

```typescript
// lib/api.ts — ponto único de acesso ao backend
export const api = axios.create({ baseURL: process.env.NEXT_PUBLIC_API_URL })
// interceptors de refresh token configurados aqui
```

### 4.3 Tempo real com SignalR

O dashboard de mesas usa SignalR para atualizações sem recarregar a página:

```typescript
// lib/signalr.ts
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/comanda", { accessTokenFactory: () => getToken() })
  .withAutomaticReconnect()
  .build()
```

---

## 5. Deploy

### 5.1 Stack de containers (produção)

```yaml
# docker-compose.prod.yml
services:
  nginx     # :80 — proxy reverso público
  frontend  # :3000 — Next.js (interno)
  api       # :5000 — ASP.NET Core (interno)
  postgres  # :5432 — banco de dados (interno)
```

Apenas a porta **80 do Nginx** é exposta ao mundo. Os demais containers se comunicam pela rede Docker interna `benditacoxinha_network`.

### 5.2 Fluxo de deploy

```
1. git push origin main
2. No VPS: bash /opt/benditacoxinha/deploy/update.sh
   ├── git pull
   ├── docker compose build
   ├── docker compose up -d
   └── docker image prune -f
```

### 5.3 Persistência de dados

```yaml
volumes:
  postgres_data:   # /var/lib/postgresql/data
  api_uploads:     # /app/wwwroot/uploads (imagens)
```

Os volumes sobrevivem a redeploys e reinicializações.

### 5.4 Health checks

| Endpoint | O que verifica |
|---|---|
| `GET /health` | Status do PostgreSQL |
| Docker healthcheck na API | `curl http://localhost:5000/health` a cada 30s |
| Docker healthcheck no Postgres | `pg_isready` a cada 10s |

---

## 6. Segurança

| Camada | Implementação |
|---|---|
| **Autenticação** | JWT HS256 em HttpOnly Cookie — nunca no localStorage |
| **Senhas** | BCrypt com salt aleatório |
| **CSRF** | SameSite=Lax + cookie HttpOnly |
| **Rate Limiting** | GlobalLimiter 300/min · auth 5/min · api 200/min |
| **SQL Injection** | EF Core parametriza todas as queries |
| **XSS** | `HtmlEncoder.Default.Encode()` em campos livres de e-mail |
| **Headers HTTP** | X-Content-Type-Options, X-Frame-Options, CSP, Referrer-Policy |
| **HTTPS** | Gerenciado pelo Cloudflare — API nunca termina TLS diretamente |
| **CORS** | Whitelist via `CorsSettings:AllowedOrigins` no `appsettings.json` |

---

## 7. Módulos pendentes (pós-reunião 28/06/2026)

Os itens abaixo aguardam decisão do cliente antes de serem implementados:

| Módulo | Status | Decisão necessária |
|---|---|---|
| **Site Público** | A fazer | Layout, seções, domínio |
| **Cashback / Fidelidade** | A definir | Cliente quer programa de pontos? |
| **Comanda — entrada** | A fazer | Só nome ou CPF opcional? |
| **Estoque nível** | A definir | Por prato (médio) ou ingredientes (completo)? |
| **NF-e / NFC-e** | A definir | Integração com SEFAZ ou sistema externo? |
| **iFood** | A definir | API oficial (homologação) ou só link no site? |
| **Pagamento online** | A definir | Delivery próprio ou só presencial? |

---

## 8. Convenções de código

### Backend
- **Namespace**: `BenditaCoxinha.*`
- **Controllers**: herdam `ControllerBase`, sem views
- **Serviços**: injetados via interface (`IProductService`, não `ProductService`)
- **Respostas**: sempre tipadas com `ActionResult<T>` + `ProducesResponseType`
- **Async/Await**: todas as operações de I/O são assíncronas
- **Centavos**: valores monetários armazenados em `int` (centavos) — conversão para `decimal` só na camada DTO

### Frontend
- **TypeScript strict** — sem `any` explícito
- **Server Components por padrão** — `"use client"` só quando necessário (interatividade, SignalR)
- **Tailwind classes** — sem CSS modules ou styled-components
- **api.ts** — toda chamada HTTP centralizada neste arquivo

---

## 9. Rodando em desenvolvimento

### 9.1 Backend

```bash
cd BenditaCoxinha
dotnet run
```

- Cria automaticamente o banco SQLite `benditacoxinha.db` na pasta do projeto
- Admin inicial: `admin@benditacoxinha.com.br` / senha definida em `ADMIN_SEED_PASSWORD` (padrão: `SenhaForte@123`)
- Swagger disponível em `http://localhost:5000/swagger`

### 9.2 Frontend

```bash
cd frontend
npm install
npm run dev
```

- App em `http://localhost:3000`
- Requer backend rodando em `http://localhost:5000` (configurado em `.env.local`)

### 9.3 Variáveis de ambiente (frontend dev)

Crie `frontend/.env.local`:

```env
NEXT_PUBLIC_API_URL=http://localhost:5000
```

---

## 10. Testes

```bash
# Testes unitários (xUnit)
cd tests/unit/BenditaCoxinha.Tests
dotnet test

# Testes de API (.http files — VS Code REST Client)
# Abra os arquivos em tests/api/
# Edite tests/api/http-client.env.json com o token
```

Cobertura atual: serviços de Auth, Product, Comanda, VendaAvulsa, Crediario, Championship, User, Announcement, LGPD.
