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
  - Benoemt afhankelijkheden, Unity-scènes/prefabs en mogelijke Mixed Reality-impact voordat ontwikkeling start.
  - Legt meetbare acceptatiecriteria vast, inclusief performance- en toegankelijkheidseisen.
- **Output:** Uitbreidingen van documentatie of architectuur-schema's.

### Agent 2: De Ontwikkelaar (Code Agent)
- **Doel:** Schrijven van de daadwerkelijke code en logica.
- **Taken:**
  - Leest de vereisten uit `Agents.md` en specifieke feature-tickets.
  - Genereert scripts of configuratiebestanden.
  - Test lokaal (voor zover mogelijk) op syntax-fouten.
  - Houdt wijzigingen klein, gericht en beperkt tot de toegewezen feature-branch.
  - Vermijdt onnodige textures, hoge polygon-aantallen en runtime-allocaties in veel uitgevoerde Unity-code.
- **Output:** Source code bestanden (bijv. `.cs`, `.py`, `.js`).

### Agent 3: De Reviewer (Kwaliteitscontrole)
- **Doel:** Nakijken van code voordat een menselijke ontwikkelaar dit valideert.
- **Taken:**
  - Controleren of de nieuwe code voldoet aan de projectomschrijving.
  - Zoeken naar bugs, security issues, en code smell.
  - Controleert Unity-specifieke risico's zoals ontbrekende referenties, ongebruikte serialized velden en performance-regressies.
  - Controleert of de PR-beschrijving testresultaten en bekende beperkingen vermeldt.
- **Output:** Feedback achterlaten op Pull Requests.

## 4. Samenwerkingsregels

- Werk nooit rechtstreeks op `main`; maak voor elke wijziging een aparte branch volgens `workflow.md`.
- Lees vóór een wijziging de relevante scene, prefab en scripts om conflicterende aanpassingen te voorkomen.
- Wijzig geen bestanden die door een andere agent zijn toegewezen zonder dit expliciet in de PR te vermelden.
- Houd commits klein en beschrijf duidelijk wat er verandert en waarom.
- Meld blokkades, aannames en niet-uitgevoerde tests in de pull request zodat een reviewer de juiste context heeft.

## 5. Definition of Done

Een taak is klaar wanneer aan alle onderstaande punten is voldaan:

- De acceptatiecriteria van de Architect zijn aantoonbaar verwerkt.
- De wijziging is getest in de Unity Editor voor zover de lokale omgeving dat toelaat.
- Er zijn geen nieuwe console errors of warnings die door de wijziging zijn veroorzaakt.
- De branch is gepusht en heeft een pull request naar `main` met een volledige beschrijving.
- De Reviewer heeft de performance- en Mixed Reality-impact gecontroleerd.
