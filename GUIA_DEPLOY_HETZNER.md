# Guia de Deploy na Hetzner (Batatas Fritas)

Parabéns! O seu projeto agora está empacotado e preparado para rodar em produção via Docker. Siga os passos abaixo para colocar o sistema no ar na [Hetzner Cloud](https://www.hetzner.com/cloud/).

## Passo 1: Criar o Servidor na Hetzner

1. Acesse o **Hetzner Cloud Console**.
2. Clique em **New Project** (se não tiver um) e depois em **Add Server**.
3. Escolha a localização (ex: Ashburn, VA para menor latência nas Américas).
4. Em **Image**, escolha **Ubuntu 24.04**.
5. Em **Type**, escolha **Standard** (Shared vCPU) e selecione o **CX22** (Intel) ou **CPX11** (AMD).
6. Adicione sua **Chave SSH** para facilitar o acesso.
7. Clique em **Create & Buy now**.
8. Anote o IP público do servidor gerado.

## Passo 2: Acessar o Servidor e Instalar Docker

Abra seu terminal no Mac e conecte-se ao servidor:
```bash
ssh root@SEU_IP_AQUI
```

Atualize os pacotes do servidor e instale o Docker:
```bash
# Atualize os pacotes
apt update && apt upgrade -y

# Instale dependências para o Docker
apt install ca-certificates curl gnupg lsb-release -y

# Baixe as chaves do Git/Docker
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc

# Adicione o repositório do Docker
echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu \
  $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
  tee /etc/apt/sources.list.d/docker.list > /dev/null

apt update
apt install docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin -y
```

## Passo 3: Enviar o Código para o Servidor

Você pode usar o Git para clonar seu repositório direto no servidor, ou enviar os arquivos do seu Mac.

**[Opção 1] Via Git (Recomendado):**
No servidor:
```bash
git clone ENDERECO_DO_SEU_REPOSITORIO batatasfritas
cd batatasfritas/BatatasFritas
```

**[Opção 2] Via SCP do seu Mac (caso não esteja no GitHub):**
Abra **outra aba do terminal no seu Mac**, vá para a pasta onde está `BatatasFritas` e rode:
```bash
scp -r ./BatatasFritas root@SEU_IP_AQUI:/root/
```
Depois, volte pro servidor e acesse a pasta:
```bash
cd /root/BatatasFritas
```

## Passo 4: Subir o Sistema (Deploy)

Uma vez dentro da pasta `BatatasFritas` no servidor da Hetzner, vamos construir as imagens locais e iniciar os serviços.

Edite o arquivo `docker-compose.prod.yml` caso queira mudar a senha do banco:
```bash
nano docker-compose.prod.yml
# Altere POSTGRES_PASSWORD para uma senha segura, depois Salve (Ctrl+O, Enter, Ctrl+X).
```

Inicie o Docker Compose em segundo plano (`-d`):
```bash
docker compose -f docker-compose.prod.yml up -d --build
```

O comando irá:
1. Efetuar o download do Postgres e do Caddy.
2. Compilar a **API (.NET 8)**.
3. Compilar o **Web Frontend (Blazor + Nginx)**.
4. Inicializar toda a estrutura no ar!

## Passo 5: Configurar seu Domínio (HTTPS Automático)

Acesse seu registrador de domínios (GoDaddy, Registro.br, etc.) e aponte os IPs para o seu servidor:
- Crie um registro tipo **A** com nome `www` e `nomedosite.com.br` apontando pro IP do servidor.
- Crie um registro tipo **A** com nome `api` apontando pro IP do servidor.

No servidor Hetzner, edite o arquivo **Caddyfile**:
```bash
nano Caddyfile
```
1. Remova os comentários (`#`) do bloco HTTPS.
2. Troque `seu-email@dominio.com.br` pelo seu email.
3. Altere o domínio virtual para os que você registrou.
4. Comente ou apague o bloco `:80`.

Reinicie o servidor proxy (Caddy):
```bash
docker compose -f docker-compose.prod.yml restart caddy
```
Pronto! Em alguns segundos você terá HTTPS 100% automático.

---
> **Validação Final**: Vá no navegador e digite o IP do servidor (ou seu domínio, se configurado) e o sistema do Batatas Fritas carregará! A API também estará disponível.
