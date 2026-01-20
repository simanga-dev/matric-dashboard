# Deployment Guide - Dokploy on VPN

## Prerequisites

- Dokploy installed on your VPN server
- Domain pointing to your VPN server IP
- GitHub/GitLab repo with this code

---

## Step 1: Deploy Convex to Production

```bash
# Login to Convex (if not already)
npx convex login

# Deploy to production
npx convex deploy
```

Save the production URL (e.g., `https://your-project-123.convex.cloud`)

---

## Step 2: Dokploy Application Setup

### Create Application

1. Open Dokploy dashboard
2. Click **"Create Application"**
3. Choose **"Docker"** as build type
4. Connect your Git repository

### Environment Variables

Add these in Dokploy → Application → Environment:

| Variable | Value |
|----------|-------|
| `VITE_CONVEX_URL` | `https://your-project-123.convex.cloud` |
| `NODE_ENV` | `production` |
| `PORT` | `3000` |

### Build Settings

- **Dockerfile Path**: `./Dockerfile`
- **Port**: `3000`

---

## Step 3: Domain & SSL Configuration

### In Dokploy Dashboard:

1. Go to **Application → Domains**
2. Click **"Add Domain"**
3. Configure:

```
Domain: matric.yourdomain.com
Port: 3000
HTTPS: Enabled
Certificate: Let's Encrypt (auto)
```

### DNS Configuration

Add an **A record** in your DNS provider:

| Type | Name | Value | TTL |
|------|------|-------|-----|
| A | matric | YOUR_VPN_SERVER_IP | 300 |

Or for root domain:

| Type | Name | Value | TTL |
|------|------|-------|-----|
| A | @ | YOUR_VPN_SERVER_IP | 300 |

### SSL Certificate (Let's Encrypt)

Dokploy handles this automatically when you:
1. Enable HTTPS on the domain
2. Select "Let's Encrypt" as certificate provider
3. Ensure port 80 is accessible for ACME challenge

---

## Step 4: Traefik Configuration (Dokploy uses Traefik)

Dokploy auto-generates Traefik labels, but if you need custom config:

```yaml
# Optional: Custom headers for security
labels:
  - "traefik.http.middlewares.matric-headers.headers.stsSeconds=31536000"
  - "traefik.http.middlewares.matric-headers.headers.stsIncludeSubdomains=true"
  - "traefik.http.middlewares.matric-headers.headers.forceSTSHeader=true"
```

---

## Step 5: Deploy

1. Click **"Deploy"** in Dokploy
2. Watch build logs for errors
3. Once green, access via your domain

---

## Verify Deployment

```bash
# Check if site is up
curl -I https://matric.yourdomain.com

# Expected: HTTP/2 200
```

---

## Troubleshooting

### Build Fails

```bash
# Check Dockerfile builds locally
docker build -t matric-dashboard .
docker run -p 3000:3000 -e VITE_CONVEX_URL=https://your-convex-url.convex.cloud matric-dashboard
```

### SSL Certificate Issues

- Ensure port 80 is open for Let's Encrypt challenge
- Check DNS propagation: `dig matric.yourdomain.com`
- Wait 5-10 mins for DNS to propagate

### Convex Connection Issues

- Verify `VITE_CONVEX_URL` is correct
- Check Convex dashboard for deployment status
- Ensure Convex functions are deployed: `npx convex deploy`

### Container Won't Start

```bash
# Check logs in Dokploy dashboard
# Or SSH into server:
docker logs <container_id>
```

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                    INTERNET                              │
└─────────────────────┬───────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────┐
│              YOUR VPN SERVER                             │
│  ┌───────────────────────────────────────────────────┐  │
│  │                 TRAEFIK                            │  │
│  │  - SSL Termination (Let's Encrypt)                │  │
│  │  - Reverse Proxy                                  │  │
│  │  - Routes: matric.yourdomain.com → :3000          │  │
│  └───────────────────────┬───────────────────────────┘  │
│                          │                               │
│  ┌───────────────────────▼───────────────────────────┐  │
│  │           DOCKER CONTAINER                         │  │
│  │  ┌─────────────────────────────────────────────┐  │  │
│  │  │  Node.js + Nitro SSR Server                 │  │  │
│  │  │  - TanStack Start (React 19)                │  │  │
│  │  │  - Port 3000                                │  │  │
│  │  └─────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
                      │
                      │ HTTPS API Calls
                      ▼
┌─────────────────────────────────────────────────────────┐
│                 CONVEX CLOUD                             │
│  - Database                                              │
│  - Serverless Functions                                  │
│  - Real-time subscriptions                               │
│  URL: https://your-project-123.convex.cloud             │
└─────────────────────────────────────────────────────────┘
```

---

## Security Checklist

- [ ] HTTPS enabled with valid SSL cert
- [ ] Environment variables not exposed in client
- [ ] Convex API keys in server-side only
- [ ] Firewall: Only ports 80, 443 open
- [ ] Regular security updates on VPN server

---

## Updating the App

1. Push changes to Git
2. In Dokploy, click **"Redeploy"**
3. Or enable auto-deploy on push in settings
