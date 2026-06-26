#!/bin/bash
# =============================================================================
# update.sh — Atualiza o Bendita Coxinha no VPS
#
# USO:
#   bash /opt/benditacoxinha/deploy/update.sh
# =============================================================================

set -e

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

APP_DIR="/opt/benditacoxinha"

echo -e "${YELLOW}🔄 Atualizando Bendita Coxinha...${NC}"

cd "$APP_DIR"
git pull origin main

cp "$APP_DIR/.env" "$APP_DIR/deploy/.env"

cd "$APP_DIR/deploy"
docker compose -f docker-compose.prod.yml build --build-arg CACHEBUST="$(date +%s)"
docker compose -f docker-compose.prod.yml up -d

docker image prune -f

echo -e "${GREEN}✅ Atualização concluída!${NC}"
docker compose -f docker-compose.prod.yml ps
