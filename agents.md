# Project Informatie & AI Agents

## 1. Projectomschrijving
*Beschrijf hier het algemene project. Vul dit zo gedetailleerd mogelijk in, aangezien de AI-agents deze context gebruiken om hun beslissingen te nemen.*

**Projectnaam:** Oncologie
**Hoofddoel:** VR Mixed reality project waarbij awareness over kanker het hoofddoel is
**Huidige Status:** Opstart

## 2. Systeem & Architectuur
*Geef de agents context over de technologie.*
- **Technologieën:** Unity, C#
- **Vereisten:** Performant in de wijze van geen texturen of objecten met teveel polygonen, Mixed Reality, niet 100% echt of 100% virtual
- **Codeerstandaarden:** Professioneel, werk met pull requests op github.

---

## 3. AI Agents en hun Taken

Hieronder staan de actieve AI-agents binnen dit project en hun afgebakende verantwoordelijkheden. *Agents mogen elkaars bestanden niet overschrijven zonder dit via de vastgestelde workflow te doen.*

### Agent 1: De Architect / Planner
- **Doel:** Bepaalt de technische structuur van nieuwe features.
- **Taken:** 
  - Opsplitsen van grote taken in kleinere, werkbare features.
  - Aanmaken van de initiële outline of pseudocode.
- **Output:** Uitbreidingen van documentatie of architectuur-schema's.

### Agent 2: De Ontwikkelaar (Code Agent)
- **Doel:** Schrijven van de daadwerkelijke code en logica.
- **Taken:**
  - Leest de vereisten uit `Agents.md` en specifieke feature-tickets.
  - Genereert scripts of configuratiebestanden.
  - Test lokaal (voor zover mogelijk) op syntax-fouten.
- **Output:** Source code bestanden (bijv. `.cs`, `.py`, `.js`).

### Agent 3: De Reviewer (Kwaliteitscontrole)
- **Doel:** Nakijken van code voordat een menselijke ontwikkelaar dit valideert.
- **Taken:**
  - Controleren of de nieuwe code voldoet aan de projectomschrijving.
  - Zoeken naar bugs, security issues, en code smell.
- **Output:** Feedback achterlaten op Pull Requests.