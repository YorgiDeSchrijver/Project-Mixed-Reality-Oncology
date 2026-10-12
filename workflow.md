# Ontwikkelingsworkflow voor AI & Menselijke Samenwerking

Dit project gebruikt een strikte, op Pull Requests (PR) gebaseerde workflow om ervoor te zorgen dat code altijd gevalideerd wordt voordat het in de hoofdversie terechtkomt.

## Stap 1: Nieuwe Feature Branch Aanmaken
Niemand (ook geen AI-agent) werkt rechtstreeks op de `main` branch. 
- Voor elke nieuwe feature of bugfix wordt een aparte branch aangemaakt.
- Haal eerst de actuele `main` op en maak de branch daar vanaf.
- **Naamgeving:** `feature/[naam-van-feature]`, `bugfix/[korte-beschrijving]`, `docs/[onderwerp]` of `chore/[onderwerp]`.
- *Commando:* `git checkout -b feature/nieuwe-functionaliteit`

## Stap 2: Ontwikkeling door de AI Agent
De AI-agent voert zijn taken uit binnen de geïsoleerde branch.
- De agent raadpleegt `Agents.md` voor de algemene regels en context.
- De agent schrijft de code en slaat dit op.
- De agent doet regelmatige, duidelijke commits.
- De agent test in de Unity Editor wanneer de wijziging Unity-assets, scènes, prefabs of C#-scripts raakt.
- De agent controleert de Console op nieuwe errors en warnings die door de wijziging ontstaan.
- *Commando:* `git commit -m "Voeg functionaliteit X toe volgens de vereisten"`

## Stap 3: Pull Request (PR) Aanmaken
Zodra de agent klaar is met de taak, wordt de code naar de remote repository gepusht en wordt er een Pull Request geopend richting `main`.
- De agent (of sturende persoon) maakt de PR aan.
- De beschrijving van de PR bevat:
  - Wat er precies is toegevoegd of aangepast.
  - Welke bestanden zijn geraakt.
  - Eventuele opmerkingen of punten van twijfel voor de reviewer.
  - Uitgevoerde tests en tests die niet konden worden uitgevoerd.
  - De impact op performance, polygoonbudget en textures, wanneer relevant.
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

## Pull Request Checklist

Voor het openen van een PR controleert de auteur:

- [ ] De branch is gebaseerd op een recente `main`.
- [ ] De wijziging heeft een gerichte commitgeschiedenis en bevat geen ongerelateerde bestanden.
- [ ] Relevante Unity Editor-tests zijn uitgevoerd en de Console bevat geen nieuwe fouten.
- [ ] De PR-beschrijving vermeldt doel, gewijzigde bestanden, testresultaten en bekende beperkingen.
- [ ] Voor MR-assets is de performance-impact beoordeeld op textures, polygooncomplexiteit en runtime-werk.

Voor het mergen controleert de reviewer:

- [ ] De acceptatiecriteria en projectvereisten uit `agents.md` zijn gevolgd.
- [ ] De wijzigingen veroorzaken geen duidelijke regressies of ontbrekende Unity-referenties.
- [ ] Onuitgevoerde tests en open risico's zijn expliciet aanvaard door een menselijke beheerder.
