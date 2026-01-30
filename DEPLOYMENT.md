# Deployment Guide - MD.converter360

Acest ghid te ajută să faci deploy pe **Render.com** (gratuit) și să faci update-uri de aici, din Claude Code.

## Cerințe

1. Cont GitHub (gratuit): https://github.com/signup
2. Cont Render (gratuit, fără card): https://render.com/

---

## Pasul 1: Creează Repository pe GitHub

### Opțiunea A: Prin interfață web

1. Mergi la https://github.com/new
2. **Repository name**: `md-converter360`
3. **Visibility**: Public (pentru free tier)
4. **NU bifa** "Initialize with README" (avem deja fișiere)
5. Click **Create repository**

### Opțiunea B: Folosind GitHub CLI (dacă e instalat)

```bash
gh repo create md-converter360 --public --source=. --remote=origin --push
```

---

## Pasul 2: Push la GitHub

După ce ai creat repo-ul gol pe GitHub, rulează aceste comenzi din folderul `MD.converter360`:

```bash
# Adaugă remote-ul GitHub (înlocuiește USERNAME cu username-ul tău)
git remote add origin https://github.com/USERNAME/md-converter360.git

# Push la GitHub
git branch -M main
git push -u origin main
```

---

## Pasul 3: Deploy pe Render

1. Mergi la https://render.com/
2. **Sign up** cu contul GitHub
3. Click **New** → **Blueprint**
4. **Connect** repo-ul `md-converter360`
5. Render va detecta automat `render.yaml` și va crea:
   - `md-converter-api` - Backend (Web Service)
   - `md-converter-web` - Frontend (Static Site)
6. Click **Apply** și așteaptă (~5-10 minute)

### URL-uri după deploy

După finalizare, vei avea:
- **Frontend**: https://md-converter-web.onrender.com
- **Backend API**: https://md-converter-api.onrender.com
- **Swagger**: https://md-converter-api.onrender.com/swagger

---

## Cum Faci Update-uri

Fiecare push la GitHub declanșează automatic un nou deploy pe Render.

### Din Claude Code:

1. Fă modificările necesare
2. Rulează:

```bash
cd D:/AI_projects/MD.converter360
git add -A
git commit -m "Descriere modificări"
git push
```

3. În 2-5 minute, modificările sunt live pe Render

### Sau cere-mi mie:

Spune-mi ce modificări vrei și eu fac:
- Modificare cod
- Commit
- Push la GitHub
- Render face deploy automat

---

## Testare Locală cu Docker

Dacă vrei să testezi înainte de deploy:

```bash
cd D:/AI_projects/MD.converter360
docker-compose up --build
```

Apoi accesează:
- Frontend: http://localhost:5172
- Backend: http://localhost:5294
- Swagger: http://localhost:5294/swagger

---

## Troubleshooting

### Aplicația nu pornește pe Render

1. Verifică logs în Render Dashboard
2. Asigură-te că Dockerfile-urile sunt corecte
3. Verifică dacă toate dependențele sunt în `package.json` și `.csproj`

### Backend e lent la prima încărcare

Normal pe free tier - Render oprește serviciul după 15 min de inactivitate. Prima cerere durează ~30 secunde pentru "wake up".

### CORS errors

Backend-ul are deja CORS configurat pentru toate originile. Dacă apar erori:
1. Verifică URL-ul backend-ului în `render.yaml`
2. Verifică că rewrite rules sunt corecte

### Build failed

Verifică:
- .NET 8 SDK (nu 10!) - Backend
- Node 20 - Frontend
- Toate dependențele listate corect

---

## Configurații Avansate

### Schimbă numele serviciilor

Editează `render.yaml`:
```yaml
services:
  - name: nume-custom-api  # schimbă aici
```

Apoi push și re-apply Blueprint în Render.

### Adaugă variabile de environment

În Render Dashboard → Service → Environment:
- Adaugă variabile precum API keys, connections strings, etc.

### Upgrade la plan plătit

Dacă ai nevoie de:
- Mai multă RAM/CPU
- Persistent storage
- Custom domains
- No sleep after inactivity

Render oferă planuri de la $7/lună per serviciu.

---

## Resurse

- [Render Docs](https://render.com/docs)
- [Render Blueprints](https://render.com/docs/blueprint-spec)
- [GitHub Docs](https://docs.github.com)

---

## Suport

Dacă ai probleme, întreabă-mă și te ajut să depanăm!
