#!/bin/bash
# =============================================================================
# setup.sh — Instalação automática Bendita Coxinha no VPS
# Ubuntu 24.04 LTS
#
# USO (como root no VPS):
#   curl -fsSL https://raw.githubusercontent.com/Taino-Edu/Bendita_coxinha/main/deploy/setup.sh | bash
# =============================================================================

set -e

CYAN='\033[0;36m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
BOLD='\033[1m'
NC='\033[0m'

APP_DIR="/opt/benditacoxinha"
REPO_URL="https://github.com/Taino-Edu/Bendita_coxinha.git"
TOTAL=7

banner() {
    echo -e "${CYAN}${BOLD}"
    echo "  ╔═══════════════════════════════════════════════╗"
    echo "  ║       🍗  BENDITA COXINHA  —  Setup VPS       ║"
    echo "  ╚═══════════════════════════════════════════════╝"
    echo -e "${NC}"
}

step() { echo -e "\n${YELLOW}${BOLD}[$1/$TOTAL] $2${NC}"; }
ok()   { echo -e "  ${GREEN}✅ $1${NC}"; }
warn() { echo -e "  ${RED}⚠️  $1${NC}"; }

banner

# 1. Atualizar sistema
step 1 "Atualizando sistema Ubuntu..."
apt-get update -y -qq
apt-get upgrade -y -qq
apt-get install -y -qq curl git openssl ufw
ok "Sistema atualizado"

# 2. Instalar Docker
step 2 "Instalando Docker..."
if command -v docker &>/dev/null; then
    ok "Docker já instalado ($(docker --version))"
else
    curl -fsSL https://get.docker.com | sh
    systemctl enable docker
    systemctl start docker
    ok "Docker instalado"
fi

if ! docker compose version &>/dev/null; then
    apt-get install -y -qq docker-compose-plugin
fi
ok "Docker Compose: $(docker compose version --short)"

# 3. Firewall
step 3 "Configurando firewall UFW..."
ufw --force reset 2>/dev/null || true
ufw default deny incoming
ufw default allow outgoing
ufw allow ssh
ufw allow 80/tcp
ufw --force enable
ok "Firewall: SSH(22) e HTTP(80) liberados"

# 4. Clonar repositório
step 4 "Clonando repositório..."
if [ -d "$APP_DIR/.git" ]; then
    cd "$APP_DIR"
    git pull origin main
    ok "Repositório atualizado"
else
    git clone --depth=1 "$REPO_URL" "$APP_DIR"
    ok "Repositório clonado em $APP_DIR"
fi
cd "$APP_DIR"

# 5. Variáveis de ambiente
step 5 "Configurando .env de produção..."
if [ ! -f "$APP_DIR/.env" ]; then
    POSTGRES_PASS=$(openssl rand -base64 24 | tr -dc 'a-zA-Z0-9' | head -c 24)
    JWT_SECRET=$(openssl rand -base64 64 | tr -d '\n')
    ADMIN_PASS=$(openssl rand -base64 16 | tr -dc 'a-zA-Z0-9' | head -c 16)

    cat > "$APP_DIR/.env" <<EOF
# Gerado por setup.sh em $(date)
# Edite e preencha GEMINI_API_KEY e SMTP_PASSWORD antes de continuar

# --- PostgreSQL ---
POSTGRES_DB=benditacoxinha
POSTGRES_USER=bendita_user
POSTGRES_PASSWORD=${POSTGRES_PASS}

# --- JWT (não altere após primeiro deploy) ---
JWT_SECRET=${JWT_SECRET}

# --- Admin inicial ---
ADMIN_SEED_PASSWORD=${ADMIN_PASS}

# --- E-mail via Resend (resend.com) ---
SMTP_HOST=smtp.resend.com
SMTP_PORT=587
SMTP_USERNAME=resend
SMTP_PASSWORD=PREENCHA_COM_API_KEY_DO_RESEND
SMTP_FROM_EMAIL=noreply@benditacoxinha.com.br

# --- Google Gemini IA ---
GEMINI_API_KEY=PREENCHA_COM_SUA_CHAVE_GEMINI
EOF
    ok ".env criado"
    warn "Edite o .env antes de continuar: nano $APP_DIR/.env"
    warn "Preencha: SMTP_PASSWORD e GEMINI_API_KEY"
    echo ""
    echo -e "${BOLD}  Pressione ENTER após editar o .env...${NC}"
    read -r
else
    ok ".env já existe, mantendo"
fi

# 6. Build
step 6 "Buildando imagens Docker (pode demorar ~5 min)..."
cp "$APP_DIR/.env" "$APP_DIR/deploy/.env"
cd "$APP_DIR/deploy"
docker compose -f docker-compose.prod.yml build --no-cache
ok "Imagens buildadas"

# 7. Subir containers
step 7 "Iniciando containers..."
docker compose -f docker-compose.prod.yml up -d

echo -n "  Aguardando API inicializar"
for i in {1..30}; do
    if docker compose -f docker-compose.prod.yml exec -T api curl -sf http://localhost:5000/health &>/dev/null 2>&1; then
        echo ""
        ok "API respondendo"
        break
    fi
    echo -n "."
    sleep 3
done

echo ""
echo -e "${GREEN}${BOLD}"
echo "  ╔══════════════════════════════════════════════════════╗"
echo "  ║      🎉  Bendita Coxinha instalado com sucesso!      ║"
echo "  ╠══════════════════════════════════════════════════════╣"
echo "  ║  🌐 Site:     https://benditacoxinha.com.br          ║"
echo "  ║  📁 Arquivos: /opt/benditacoxinha/                   ║"
echo "  ╠══════════════════════════════════════════════════════╣"
echo "  ║  Comandos úteis:                                     ║"
echo "  ║  • Logs:       cd /opt/benditacoxinha/deploy         ║"
echo "  ║                docker compose logs -f               ║"
echo "  ║  • Atualizar:  bash /opt/benditacoxinha/deploy/update.sh  ║"
echo "  ╚══════════════════════════════════════════════════════╝"
echo -e "${NC}"
