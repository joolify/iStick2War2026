# Importera QA-listan till Google Sheets

Jag kan inte skapa filer direkt i ditt Google-konto, men CSV-filerna i den här mappen importeras på några minuter till ett kalkylblad med flera flikar.

**Baseline:** `v0.1.0-rc2`  
**Källa:** [0.1.0-manual-qa.md](../0.1.0-manual-qa.md)

---

## Snabbaste vägen (rekommenderad)

1. Öppna [Google Sheets](https://sheets.google.com) → **Tomt** kalkylblad.
2. Döp dokumentet t.ex. till **iStick2War QA 0.1.0-rc2**.
3. **Fil → Importera → Uppladdning** → välj `0.1.0-checklist.csv`.
   - Plats: **Ersätt kalkylblad**
   - Separator: **Komma**
   - Konvertera text till siffror/datum: **av** (så ID som F1-01 inte blir datum)
4. Byt namn på fliken till **Checklist**.
5. Importera övriga filer som **nya blad** (samma meny, välj **Infoga nytt blad**):
   - `0.1.0-vagor.csv` → flik **Vågor**
   - `0.1.0-resolution.csv` → flik **Resolution**
   - `0.1.0-vapen.csv` → flik **Vapen**

---

## Alternativ: via Google Drive

1. Ladda upp hela mappen `docs/qa/google-sheets/` till Google Drive.
2. Högerklicka på `0.1.0-checklist.csv` → **Öppna med → Google Kalkylark**.
3. Importera resten som ovan (nya blad).

---

## Tips efter import

### Status-kolumn (Checklist)

1. Markera kolumn **Status** (kolumn I).
2. **Data → Datavalidering**.
3. Kriterium: **Listruta** med värden: `Tom, PASS, FAIL, BLOCKED, SKIP`.

### Villkorlig formatering

- **PASS** → grön bakgrund  
- **FAIL** → röd bakgrund  
- **BLOCKED** → orange bakgrund  
- **Release blocker = Ja** och **Status = FAIL** → markera raden (filter på kolumn G + I)

### Sortera efter prioritet

1. Markera hela tabellen (inkl. rubriker).
2. **Data → Skapa ett filter**.
3. Sortera kolumn **Prioritet** stigande (1 = Game Over först).

### Frys rubrikrad

**Visa → Frys → 1 rad**

---

## Filer

| Fil | Innehåll |
|-----|----------|
| `0.1.0-checklist.csv` | Alla testpunkter (~100 rader) |
| `0.1.0-vagor.csv` | Våg 1–15 balansanteckningar |
| `0.1.0-resolution.csv` | Game View smoke per aspect |
| `0.1.0-vapen.csv` | Vapen per kolumn |

---

## Uppdatera från repo

Om checklistan ändras i git: exportera eller kopiera nya CSV-filer och **Fil → Importera** igen (ersätt blad), eller redigera direkt i Sheets.
