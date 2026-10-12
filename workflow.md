# Ontwikkelingsworkflow voor AI & Menselijke Samenwerking

Dit project gebruikt een strikte, op Pull Requests (PR) gebaseerde workflow om ervoor te zorgen dat code altijd gevalideerd wordt voordat het in de hoofdversie terechtkomt.

## Stap 1: Nieuwe Feature Branch Aanmaken
Niemand (ook geen AI-agent) werkt rechtstreeks op de `main` branch. 
- Voor elke nieuwe feature of bugfix wordt een aparte branch aangemaakt.
- **Naamgeving:** `feature/[naam-van-feature]` of `bugfix/[korte-beschrijving]`.
- *Commando:* `git checkout -b feature/nieuwe-functionaliteit`

## Stap 2: Ontwikkeling door de AI Agent
De AI-agent voert zijn taken uit binnen de geïsoleerde branch.
- De agent raadpleegt `Agents.md` voor de algemene regels en context.
- De agent schrijft de code en slaat dit op.
- De agent doet regelmatige, duidelijke commits.
- *Commando:* `git commit -m "Voeg functionaliteit X toe volgens de vereisten"`

## Stap 3: Pull Request (PR) Aanmaken
Zodra de agent klaar is met de taak, wordt de code naar de remote repository gepusht en wordt er een Pull Request geopend richting `main`.
- De agent (of sturende persoon) maakt de PR aan.
- De beschrijving van de PR bevat:
  - Wat er precies is toegevoegd of aangepast.
  - Welke bestanden zijn geraakt.
  - Eventuele opmerkingen of punten van twijfel voor de reviewer.
- *Commando:* `git push origin feature/nieuwe-functionaliteit`

## Stap 4: Validatie (Mens / Review Agent)
De Pull Request staat nu open en wacht op goedkeuring.
- Een menselijke ontwikkelaar (of de AI Review Agent) leest de code door.
- Er wordt gecontroleerd of de code werkt, netjes is, en geen bestaande systemen breekt.
- Als er fouten zijn, wordt er feedback gegeven en moet de Code Agent op dezelfde branch aanpassingen doen (terug naar Stap 2).

## Stap 5: Accepteren en Push naar Main
Als de validatie succesvol is, wordt de code goedgekeurd (`Approved`).
- De menselijke beheerder voert de merge uit.
- De feature branch mag daarna verwijderd worden om de repository schoon te houden.
- De `main` branch is nu veilig en succesvol geüpdatet met de nieuwe feature!