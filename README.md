# 🍗 Bendita Coxinha — Sistema de Gestão para Restaurante

Sistema completo de gestão para restaurante, com PDV, comandas via QR Code, cardápio, eventos, financeiro e site público.

> Derivado do [SoftNerd](https://github.com/Taino-Edu/softNerd) — arquitetura modular reaproveitada e adaptada para o segmento de alimentação.

---

## ✨ Funcionalidades

| Módulo | Descrição |
|---|---|
| **PDV (Balcão)** | Venda direta sem login de cliente, múltiplas formas de pagamento |
| **Comandas / Mesas** | Cliente escaneia QR Code na mesa e faz pedido pelo celular em tempo real |
| **Cardápio** | Gestão de pratos e bebidas por categoria (Entrada / Prato / Sobremesa / Bebida) |
| **Eventos** | Shows, happy hours, jantares especiais — com inscrições e controle de vagas |
| **Crediário / Fiado** | Controle de crédito interno por cliente, com histórico de pagamentos |
| **Financeiro** | Receita, custo e margem por período — gráficos diários e exportação PDF |
| **Relatórios** | Vendas por categoria e produto, inadimplência do crediário |
| **Assistente IA** | Chat com contexto do negócio (mesas abertas, cardápio, caixa do dia) via Gemini |
| **Área do Cliente** | Histórico de pedidos, pontos de fidelidade, cashback, perfil editável |
| **Site Público** | Landing page, cardápio online, lista de eventos — voltado ao cliente final |
| **Admin** | Usuários, perfis/permissões, QR Codes das mesas, anúncios |

---

## 🛠️ Stack

### Backend
- **ASP.NET Core 8** — REST API
- **PostgreSQL 16** — banco relacional (único banco, sem MongoDB)
- **Entity Framework Core 8** — ORM com `EnsureCreated`
- **SignalR** — comandas em tempo real
- **JWT (HttpOnly Cookies)** — autenticação segura
- **Google Gemini 2.5 Flash** — assistente IA

### Frontend
- **Next.js 14** (App Router) + **TypeScript 5**
- **Tailwind CSS 3**
- **Axios** com interceptors de refresh token
- **@microsoft/signalr** — cliente SignalR

### Infraestrutura
- **Docker + Docker Compose** — containerização
- **Nginx 1.27** — proxy reverso
- **Cloudflare** — DNS + SSL/TLS
- **VPS Ubuntu 24.04** (Hostinger ou similar)

---

## 🚀 Rodando localmente

### Pré-requisitos
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org)
- Git

### Backend (SQLite automático em dev)

```bash
cd BenditaCoxinha
dotnet run
# API em http://localhost:5000
# Swagger em http://localhost:5000/swagger
```

### Frontend

```bash
cd frontend
npm install
npm run dev
# App em http://localhost:3000
```

Crie `frontend/.env.local`:
```env
NEXT_PUBLIC_API_URL=http://localhost:5000
```

> Em dev o backend usa **SQLite** automaticamente — não precisa instalar PostgreSQL.

---

## 🐳 Deploy em produção (VPS)

### Primeira vez

```bash
# No VPS como root:
curl -fsSL https://raw.githubusercontent.com/Taino-Edu/Bendita_coxinha/main/deploy/setup.sh | bash
```

### Atualizações

```bash
bash /opt/benditacoxinha/deploy/update.sh
```

### Stack em produção

```
Internet → Cloudflare (HTTPS) → Nginx :80 → frontend :3000
                                           → api     :5000
                                           → postgres :5432
```

---

## 📁 Estrutura do projeto

```
Bendita_coxinha/
├── BenditaCoxinha/          # Backend ASP.NET Core 8
│   ├── Controllers/         # 15 controllers REST
│   ├── Services/            # 11 serviços de negócio
│   ├── Models/PostgreSQL/   # Entidades EF Core
│   ├── DTOs/                # Request/Response objects
│   ├── Data/                # AppDbContext
│   ├── Hubs/                # SignalR (ComandaHub)
│   └── Middleware/          # JWT, CORS, Rate Limiting, Permissões
├── frontend/                # Next.js 14
│   ├── app/admin/           # Painel administrativo
│   ├── app/cliente/         # Área do cliente logado
│   ├── app/mesa/[mesa]/     # Flow QR Code → pedido na mesa
│   └── lib/                 # api.ts, signalr.ts, auth.ts
├── deploy/                  # Docker Compose + Nginx + scripts
└── tests/                   # Testes unitários (xUnit) + REST (.http)
```

---

## 🔐 Variáveis de ambiente obrigatórias (produção)

| Variável | Descrição |
|---|---|
| `POSTGRES_PASSWORD` | Senha do PostgreSQL |
| `JWT_SECRET` | Chave secreta JWT (mín. 32 chars, imutável após deploy) |
| `GEMINI_API_KEY` | Chave Google Gemini (IA) |
| `SMTP_PASSWORD` | Senha SMTP para e-mails |
| `ADMIN_SEED_PASSWORD` | Senha do admin criado no primeiro boot |

---

## 🗒️ Comandos úteis

```bash
# Ver logs em tempo real
cd /opt/benditacoxinha/deploy
docker compose -f docker-compose.prod.yml logs -f

# Reiniciar serviço específico
docker compose -f docker-compose.prod.yml restart api

# Health check
curl http://localhost/health
```

---

## 📄 Documentação técnica

Veja [DOCUMENTACAO-TECNICA.md](./DOCUMENTACAO-TECNICA.md) para arquitetura detalhada, decisões de design e guia de contribuição.
