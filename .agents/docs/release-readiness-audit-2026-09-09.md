# Releasevurdering af RazorSearch

Gennemgået 9. september 2026 på commit `53ea79f0536e03c6ff37b5d9464832b21194eac3`.

**Anbefaling: Vent med både stabil release og offentlig beta.** Grundideen passer til at overtage FullTextSearchs rolle oven på Umbraco Search, men der er reproducerede fejl i pakning, indeksopdatering, konfiguration og tekstudtræk. Efter rettelserne nedenfor er en afgrænset beta et rimeligt næste skridt.

En første beta bør have en udtrykkelig supportmatrix for Umbraco, Search Core, provider og database. Umbraco 19-kompatibilitet er ikke verificeret her. Upstream beskriver overgangen som tidligst fra version 19, og Search er fortsat beskrevet som beta i repositoryets README. Se [Umbraco Search](https://github.com/umbraco/Umbraco.Cms.Search).

## Det fungerer allerede

- Core registrerer sit eget indeks og lader hosten eje `AddSearchCore()` og providerregistrering. Examine-specifik opsætning ligger i companion-pakken.
- Søgning bruger providerens fulltext, rangering og metadatafiltre. Snapshotopslag for resultater samles i ét opslag for sidens dokumenter.
- Der findes håndtering af publicering, afpublicering, flytning og sletning samt backofficeværktøjer til genopbygning.
- Summary-builderen HTML-encoder både tekst og matches, før det konfigurerede highlight-markup indsættes.
- Der er allerede installations-, konfigurations-, drifts- og migrationsdokumentation at arbejde videre fra.

## Verifikation

| Kontrol | Resultat |
| --- | --- |
| `dotnet build Umbraco.Community.RazorSearch.slnx` | Bestået, 0 fejl og 110 advarsler, overvejende NuGet-sikkerhedsadvarsler. Antallet inkluderer gentagelser. |
| `npm run check` | Bestået. |
| `npm run build` | Bestået. |
| Release-pack af begge pakker med version `0.1.0-beta.1` | Begge fejler med `NU5019`, manglende `assets/icon.png`. |
| Workflow kontrolleret med actionlint 1.7.7 | Bestået. Den downloadede binær blev checksumkontrolleret. |
| Nye SQLite-migrationer | Alle tre RazorSearch-migrationer anvendt på en tom database, derefter 0 ventende migrationer. |
| Eksisterende demo | Forside og `/search/?q=umbraco` svarer HTTP 200. Søgningen viser 10 resultater på første side. |
| Anonymt kald til queue-status | HTTP 401. |
| Isolerede kørsler af den aktuelle kode | Bekræfter kladde-sletning, mistet ny køopgave, forkert konfigurationsbinding og HTML-udtræk, se nedenfor. |
| `npm audit` | 34 rapporterede afhængighedsposter: 1 critical, 6 high, 26 moderate og 1 low. Det er udviklingsafhængigheder, ikke dokumentation for 34 udnyttelige fejl i det distribuerede bundle. |
| Marketplace JSON | Nøgler, obligatoriske felter og relevante enumværdier kontrolleret mod det aktuelle schema. Ingen fundne schemafejl i disse kontroller. Fuld schema-validator var ikke installeret. |

Demoen kørte allerede og blev ikke genstartet eller ændret. Dens database blev kun læst. Derfor dokumenterer HTTP-kontrollen eksisterende demoadfærd, ikke en ny installation af det byggede NuGet-artefakt. Et fuldt installationsforsøg med NuGet kan først gennemføres, når pack virker.

De isolerede kørsler anvendte et midlertidigt consoleprojekt med reference til pakkens aktuelle kildekode. De brugte simple stand-ins for eksterne interfaces og ingen produktionsdatabase. De erstatter ikke integrationstest gennem Umbracos backoffice.

## Bør rettes før offentlig beta

### 1. Pakkerne kan ikke pakkes

Begge `.csproj`-filer inkluderer `../../assets/icon.png`, men filen findes hverken i arbejdsområdet eller Git. Releaseflowet stopper derfor før publicering.

**Bevis:** Begge pack-kørsler gav `error NU5019: File not found ... assets/icon.png`.

**Retning:** Tilføj det rigtige ikon, og gennemfør pack og installation af begge producerede pakker i en separat app. Kontroller også, at App_Plugins-manifest, JS-filer, version og README følger med.

Kilde: [Core-projekt](../../src/Umbraco.Community.RazorSearch/Umbraco.Community.RazorSearch.csproj), linje 14, og [Examine-projekt](../../src/Umbraco.Community.RazorSearch.Examine/Umbraco.Community.RazorSearch.Examine.csproj), linje 13.

### 2. Gem kladde fjerner publiceret indhold fra søgningen

`RazorSearchContentIndexChangeStrategy.HandleAsync()` behandler alle dokumentændringer. Hvis `ContentState` ikke er `Published`, kalder den `DeleteAsync`. En kladdeopdatering skal ignoreres af dette indeks, så den eksisterende publicerede version fortsat kan findes.

**Reproduceret:** En `Draft/Refresh`-ændring for et indholdselement med `Published=true` gav præcis ét `IIndexer.DeleteAsync`-kald.

Upstreams `ContentIndexingService` videresender ændringer til strategierne uden at filtrere kladder fra. Upstreams egen published-strategi foretager netop dette filter. Dette er verificeret mod commit `c0782569b48b112e0a7903b8dcc368041f33c934`, som står i de installerede Search-pakkers metadata, ikke kun mod den nyeste upstreamkode. Se [den anvendte Search Core-kode](https://github.com/umbraco/Umbraco.Cms.Search/blob/c0782569b48b112e0a7903b8dcc368041f33c934/src/Umbraco.Cms.Search.Core/Services/ContentIndexing/ContentIndexingService.cs).

**Retning:** Filtrer relevante publicerede ændringer før slette-/opdateringslogikken. Verificer gem kladde, publicer og afpublicer hver for sig.

Kilde: [indeksstrategi](../../src/Umbraco.Community.RazorSearch/Indexing/RazorSearchContentIndexChangeStrategy.cs), linje 46-68.

### 3. Konfigurerede kilder erstatter ikke standardkilderne

Options indeholder allerede arrays med standardværdier. ConfigurationBinder føjer konfigurerede arrayelementer til disse. Derfor virker dokumentationens prioriterede kilder og begrænsning til bestemte HTML-områder ikke som beskrevet.

**Reproduceret med samme options-type og .NET-konfigurationsbinding som pakken:**

```text
Konfigureret title: property:seoTitle, selector:h1
Faktisk title:      selector:title, property:seoTitle, selector:h1

Konfigureret body:  selector:main
Faktisk body:       selector:body, selector:main

HTML: <body>Navigation<main>Only desired text</main></body>
Udtræk: Navigation Only desired text Only desired text
```

En eksisterende `<title>` vinder således over den ønskede SEO-property, og navigation bliver søgbar trods `main`-konfiguration.

**Retning:** Bind eksplicit konfigurerede arrays uden forudfyldte standardelementer. Anvend defaults bagefter, når en kilde ikke er konfigureret. Fjern samtidig global, muterbar extraction-state fra options-konstruktører/settere, så nye options-instanser ikke ændrer andre kalds indstillinger.

Kilde: [extraction-options](../../src/Umbraco.Community.RazorSearch/Configuration/RazorSearchSnapshotExtractionOptions.cs), linje 7-73.

### 4. Publicering under aktiv rendering kan miste den nyeste version

Køen deduplikerer både ventende og igangværende jobs på content key og kultur. En publicering under rendering opretter hverken et efterfølgende job eller markerer indholdet til ny rendering.

**Reproduceret:** Et job for `/old` blev markeret som running. Et nyt job for samme content key og kultur med `/new` returnerede `WasQueued=False` og den gamle route. Hvis første HTTP-request allerede har læst den gamle version, forbliver snapshot forældet efter køens afslutning.

**Retning:** Bevar et efterfølgende refresh, når en ændring ankommer under rendering, eller brug versionskontrol ved afslutning. Deduplikationsnøglen bør også tage stilling til segment og renderer, som det offentlige API understøtter.

Kilde: [renderkø](../../src/Umbraco.Community.RazorSearch/Services/InMemoryRazorSearchRenderQueue.cs), linje 44-55 og 225-269.

### 5. URL-ændringer kan beholde gammel søgbar tekst

Snapshot-identiteten inkluderer `Route`. En ny URL giver derfor en ny række. Almindelig publicering rydder ikke den tidligere route; oprydning af hele undertræet sker kun i flyttehåndteringen. Indeksprojektionen samler tekst fra alle succesfulde snapshots for samme kultur/segment.

**Konsekvens udledt af kodeflowet:** Omdøb en side eller skift URL og tekst, og den gamle tekst kan fortsat give matches. Omdøbning af en forælder kan desuden efterlade børnenes snapshot-URL'er forældede. En genopbygning, som blot renderer igen, fjerner ikke de gamle rækker.

**Retning:** Definér hvilke snapshots der er aktuelle for hver variant og renderer. Fjern eller ugyldiggør gamle routes ved URL-ændringer, inklusive berørte efterkommere. Verificer også, at gamle jobs ikke kan skrive forældede snapshots tilbage efter en flytning eller afpublicering.

Kilder: [snapshot-upsert](../../src/Umbraco.Community.RazorSearch/Persistence/Stores/EfCoreRazorSearchSnapshotStore.cs), linje 139-146; [projektion](../../src/Umbraco.Community.RazorSearch/Indexing/RazorSearchSnapshotIndexProjection.cs), linje 18-31; [livscyklushåndtering](../../src/Umbraco.Community.RazorSearch/Notifications/RazorSearchContentLifecycleNotificationHandler.cs).

### 6. Gyldig HTML giver forkert tekst

Den hjemmelavede HTML-parser bruger en regex, der stopper et tag ved første `>`, også når tegnet står i en citeret attribut. Desuden indsættes mellemrum mellem alle tekstnoder, også inde i ord.

**Reproduceret:**

```text
<body><div title="a > b">Visible</div></body>  => b">Visible
<body>hel<strong>lo</strong></body>           => hel lo
```

**Retning:** Brug en HTML-parser, der håndterer citerede attributter og normal HTML-struktur. Bevar tekstsammenhæng ved inline-elementer og brug relevante separatorer ved blokke. CSS-understøttelsens begrænsninger bør fortsat fremgå af dokumentationen.

Kilde: [HTML-parser](../../src/Umbraco.Community.RazorSearch/Services/RazorSearchHtmlDocument.cs), linje 104-139 og 183-190.

### 7. Management-API'et mangler dokument- og operationsrettigheder

Controlleren kræver kun `BackOfficeAccess`. Der kontrolleres ikke adgang til det konkrete dokument, brugerens startnoder eller retten til at genopbygge hele sitet. Statusopslaget returnerer blandt andet gemt HTML.

Anonyme kald afvises korrekt med 401. Problemet er afgrænsning mellem allerede autentificerede backofficebrugere, ikke anonym adgang. En begrænset editor bør ikke kunne hente vilkårlige snapshots eller starte globale rebuilds alene ved at kalde API'et direkte.

**Retning:** Tilføj dokumentbaseret autorisation og særskilt rettighed til globale operationer. Verificer med en editor begrænset til ét undertræ. Den konkrete editoradfærd er ikke afprøvet i denne audit.

Kilder: [management-controller](../../src/Umbraco.Community.RazorSearch/Controllers/RazorSearchManagementController.cs), linje 13-16; [management-service](../../src/Umbraco.Community.RazorSearch/Api/DefaultRazorSearchManagementService.cs), linje 113-159 og 639 ff.

### 8. Afhængighedsbaseline skal opdateres og afgrænses

Core angiver Umbraco `17.0.0`, Search Core `1.0.0` og EF Core `10.0.0`. Companion bruger Examine-provider `1.0.0-beta.9`; demoen bruger Umbraco `17.5.1`. Begge pakker undtager `NU1902` og `NU1903` fra warnings-as-errors.

Buildet rapporterede blandt andet sårbare versioner af Umbraco.Cms, MessagePack, Microsoft.OpenApi, SQLitePCLRaw og System.Security.Cryptography.Xml. npm audit rapporterede også problemer i udviklingsværktøjerne. Det kræver opdatering og konkret vurdering af relevante advisories, ikke blot at slå audit fra.

NuGets versionsfeeds viser nu separate 17.x- og 18.x-linjer for Search. Vælg en testet kombination frem for automatisk at opgradere til højeste major. De nuværende Core `1.0.0` og provider `1.0.0-beta.9` stammer fra samme upstreamcommit, så versionsnavnene er ikke i sig selv bevis for inkompatibilitet.

**Retning:** Fastlæg en understøttet, opdateret baseline. Begræns Umbraco-majorintervallet til verificerede versioner, og dokumentér preview-status for provider. De nuværende åbne minimumsafhængigheder dokumenterer ikke kompatibilitet med Umbraco 18 eller 19. Se [Umbracos vejledning om versionsgrænser](https://docs.umbraco.com/umbraco-cms/extending/packages/maintaining-packages).

## Drifts- og funktionsmangler, som skal indgå i betaens afgrænsning

### 9. Backfill kan ikke fortsætte efter grænsen

Management-servicen vælger de første 250 publicerede dokumenter som standard og højst 5.000. Der findes ingen cursor eller offset. Gentagne globale rebuilds starter forfra med de samme dokumenter. `WasTruncated` fortæller om problemet, men giver ikke en fortsættelse.

Samtidig afventer HTTP-kaldet alle enqueue-operationer. Når den begrænsede kanal med standardkapacitet 256 fyldes, venter requesten på den ene worker. Et stort eller flersproget site kan derfor ramme request-/proxytimeout, før API'et returnerer 202.

**Retning:** Start en baggrundsoperation, som selv gennemløber indholdet i batches og kan fortsættes. Det løser både requestvarighed og grænsen. Indtil da bør betaen ikke love en ubetinget "rebuild all".

Kilder: [management-service](../../src/Umbraco.Community.RazorSearch/Api/DefaultRazorSearchManagementService.cs), linje 23-24, 313-361 og 433; [kø](../../src/Umbraco.Community.RazorSearch/Services/InMemoryRazorSearchRenderQueue.cs), linje 28-38 og 116.

### 10. Fejl og genstart har utilstrækkelig recovery

Ved HTTP 500 gemmes det tidligere indhold, men status ændres til `Failed`, og snapshot-refresh fjerner dermed siden fra det succesbaserede indeks. En timeout/exception håndteres anderledes og ændrer kun køstatus. Der er ingen automatisk retry. En kortvarig HTTP-fejl kan derfor gøre indhold usøgbart indtil næste vellykkede rendering.

Køen er kun i hukommelsen, så ventende jobs går tabt ved genstart. Afsluttede jobs og statusser fjernes aldrig fra dictionaries. Over tid vokser både hukommelsesforbrug og arbejdet ved statusopslag. Snapshotlagring og anmodning om indeksrefresh er heller ikke en samlet holdbar operation med recovery ved crash mellem trinnene.

**Retning:** Adskil seneste renderforsøg fra seneste brugbare snapshot, tilføj begrænsede retries og oprydning i jobhistorik. Dokumentér genstarts-/recoveryforløb. En enkelt instans med manuel recovery kan være en betaafgrænsning; load balancing kræver særskilt verifikation af jobejerskab og samtidige writes.

Kilder: [worker](../../src/Umbraco.Community.RazorSearch/Services/RazorSearchRenderQueueHostedService.cs), linje 95-140; [kø](../../src/Umbraco.Community.RazorSearch/Services/InMemoryRazorSearchRenderQueue.cs), linje 13-15 og 167-222; [snapshot-refresh](../../src/Umbraco.Community.RazorSearch/Indexing/RefreshingRazorSearchSnapshotStore.cs).

### 11. SQL Server kan afvise lovlige modelværdier

Det unikke indeks indeholder `Route` med 2.048 tegn plus content key, kultur, segment og renderer. På SQL Server overstiger tilstrækkeligt lange faktiske værdier grænsen på 1.700 bytes for en nonclustered index key. At migrationen kan oprette indekset på en tom database beviser ikke, at alle efterfølgende inserts kan lykkes. Se [Microsofts størrelsesgrænser](https://learn.microsoft.com/en-us/sql/sql-server/maximum-capacity-specifications-for-sql-server?view=sql-server-ver17).

`TitleText` er desuden begrænset til 512 tegn uden validering eller trunkering inden lagring. SQLite håndhæver ikke disse længder på samme måde.

**Retning:** Brug en afgrænset nøgle, eksempelvis en route-hash, hvis route fortsat skal være del af identiteten. Verificer korte/lange URL'er og titler på SQL Server. SQL Server er ikke kørt i denne audit; en SQLite-only beta skal sige det udtrykkeligt.

Kilde: [entity-konfiguration](../../src/Umbraco.Community.RazorSearch/Persistence/Configurations/RazorSearchSnapshotEntityConfiguration.cs), linje 15-38 og 56-65.

### 12. Kultur, sideantal og medlemssøgning kræver afklaring

- En neutral kultur som `da` oversættes til `null` før providersøgningen. Kulturfiltrering sker derefter under snapshotvalg, efter providerens pagination. Det kan give korte/tomme sider og forkert total, og en matchende tekst fra én kultur kan forbindes med et snapshot fra en anden kultur.
- En side uden provider-dokumenter returnerer `Total=0`, også når man blot er gået forbi sidste side i et ellers ikke-tomt søgeresultat.
- `AccessContext` er altid anonym. Indekset modtager content protection, men tjenesten tilbyder ikke søgning ud fra det aktuelle medlems adgang. Det bør være en udtrykkelig funktionsgrænse.
- Automatiske jobs enumererer kulturer, men ikke segmenter, selv om segment indgår i de offentlige modeller.

**Retning:** Verificer en dansk/engelsk installation med flere søgeresultatsider og konkrete medlemsrettigheder. Understøt kun eksakte kulturer i betaen, hvis neutral kultur ikke kan filtreres korrekt før pagination.

Kilde: [søgetjeneste](../../src/Umbraco.Community.RazorSearch/Searching/RazorSearchService.cs), linje 49-66, 104-110, 303-321 og 342-360.

### 13. Backoffice viser en beregning som faktisk indeksstatus

`IndexedEntries` beregnes af de samme database-snapshots, som sammenligningen tager udgangspunkt i. Der spørges ikke i den faktiske provider. UI-tekster som "Indexed" og "Matches index" kan derfor se korrekte ud, selv når et provider-refresh er fejlet eller kladde-fejlen har slettet dokumentet. Summary vises også i projektionen, selv om den ikke er et søgbart indeksfelt.

**Retning:** Vis det som forventet indeksindhold, eller hent faktisk indeksstatus og sammenlign med den. Det er væsentligt for at kunne stole på releaseverifikation i backoffice.

Kilder: [management-service](../../src/Umbraco.Community.RazorSearch/Api/DefaultRazorSearchManagementService.cs), linje 132-136; [statusmodal](../../src/Umbraco.Community.RazorSearch/Client/src/razor-search-status-modal.element.ts), linje 662-740 og 821.

## Dokumentation og metadata

### Dokumentation, der konkret skal rettes

1. **Controller-eksemplerne kompilerer ikke som vist.** `public override async Task<IActionResult> Index()` kan ikke override Umbraco 17s `IActionResult Index()`. Signaturen er verificeret mod den installerede assembly. Vælg et understøttet async-controllerpattern og vis et komplet eksempel. Kilder: [usage](../../docs/usage/README.md), linje 39, og [migration](../../docs/migrating-from-fulltextsearch/README.md), linje 52.
2. **Request-modellens namespace mangler flere steder.** `RazorSearch`-modellen ligger i `.Models`, ikke i pakkens rodnamespace. Vis de nødvendige usings og definitionen af den ekstra `SearchResult`-property/viewmodel, som eksemplerne forudsætter.
3. **Forkert pakkeangivelse for `AddSearchCore()`.** Den anvendte extension ligger i `Umbraco.Cms.Search.Core.DependencyInjection` fra `Umbraco.Cms.Search.Core`. README-filerne og installation kalder pakken `Umbraco.Cms.Search`.
4. **Quickstart mangler et samlet, kørbart provider-eksempel.** Vis alle pakker, namespaces, `AddSearchCore()`, `AddExamineSearchProvider()` og `.Build()`, samt hvordan første backfill startes i Settings. Under beta skal installkommandoerne vælge en eksplicit prerelease-version eller bruge `--prerelease`.
5. **NuGet-README mangler links til den udførlige dokumentation.** Tilføj absolutte GitHub-links til installation, konfiguration, migration og kendte begrænsninger i både core- og companion-README.
6. **Migrationstabellen overdriver direkte kompatibilitet.** Gamle root-ID'er er `int`; de nye er `Guid` keys. Dokumentér også forskellen i resultategenskaber, total/pages og `SummaryHtml`.
7. **Funktionsforskelle fra FullTextSearch er ufuldstændige.** Beskriv manglende request-options for fuzzy matching, automatiske wildcards, brugerdefineret query, indeksvalg, summarylængde og highlighting til/fra samt tidligere cache-notifications. Adskil providerens eventuelle querysyntaks fra RazorSearchs offentlige API. Sammenligningen er baseret på [FullTextSearchs udviklerguide](https://github.com/skttl/umbraco-fulltextsearch8/blob/d20c97eb869f18259269edb1cb106c3062e2bb87/docs/developers-guide-v5.md); dokumentér også hvilken FullTextSearch-version migrationen gælder for.
8. **Driftsvejledning mangler konkrete grænser.** Tilføj queue-recovery ved genstart, fejl/retries, backfillgrænser, SQL Server, multisite/domæneændringer, load balancing og genopbygning efter ændrede templates, extraction-regler eller fælles indhold.
9. **Renderkontekst er ikke en autorisationsmekanisme.** Forklar dens rolle ved caches/CDN, redirects, cookieafhængig rendering og personlig tekst. Den deterministiske fallback-token bør ikke præsenteres som en hemmelig adgangskontrol. Custom-renderer-eksemplets `change-me`-header aktiverer kun konteksten, hvis hostens token passer.
10. **Agents.md beskriver en ældre arkitektur.** Den nævner bidrag via `IContentIndexer`, hvor den aktuelle kode bruger en separat `IContentChangeStrategy`. Repositorylayout og releasebeskrivelse mangler companion-pakken. CONTRIBUTING mangler reproducerbar opsætning af demodata.

### Metadata og releasepræsentation

- Package ID, forfatter, MIT-license expression, repository/project URL, README-felter og Marketplace-tag er til stede. Der er ikke grundlag for at kalde `Category: Developer Tools` eller `PackageType: Package` ugyldige; begge værdier findes i det aktuelle schema.
- Vælg kategorien `Search`, og tilføj `DocumentationUrl` samt relevante screenshots. Det er kvalitetsforbedringer, ikke selvstændige tekniske releaseblokeringer.
- Begge NuGet-pakker har Marketplace-tag og samme project URL. De vil derfor pege mod samme fælles Marketplace-metadata. Beslut om companion skal have egen listing, egne pakke-specifikke JSON-metadata og `IsSubPackageOf`, eller om kun hovedpakken skal listes. Se [Marketplace-vejledningen](https://docs.umbraco.com/umbraco-dxp/marketplace/listing-your-package).
- Tilføj udtrykkelig beta-status, supportmatrix, kendte begrænsninger og supportvej i README. Angiv, at erstatningen gælder sites på den nye Umbraco Search-platform.
- Versionsnummeret kommer fra tag ved release. De lokale projektfiler har intet eksplicit pakkenummer, mens klienten bruger `0.1.0`. Dokumentér versioneringsmodellen, så lokale packs ikke forveksles med en planlagt stabil `1.0.0`.
- Releaseflowets changelog bliver genereret fra fem commits, herunder to med teksten `wip`. Skriv kuraterede release notes til første beta. En ikoncommit og et tag alene giver ikke brugbare release notes.
- Tilføj gerne SECURITY.md og en enkel issue-skabelon. Det er vedligeholdelsesforbedringer og ikke krav om en større proces.

## CI og manglende releasebevis

Der findes kun et tag-udløst releaseworkflow. Det bygger og pakker begge pakker og sætter GitHub-releasens prereleaseflag, hvilket er godt. Det kører ikke TypeScript-check, solution/demo-build eller automatiserede adfærdskontroller. `npm install` bør erstattes af `npm ci`, så lockfilen er styrende. Tilføj et PR-build, som også opdager pack-fejl før tagget.

NuGet-push bruger en bred wildcard og har ingen `--skip-duplicate`. Hvis kun den ene pakke når at blive publiceret, kan genkørsel stoppe på den allerede udgivne pakke. Pak til en bestemt artefaktmappe, publicér kun disse filer og gør genkørsel efter delvis succes forudsigelig. Den faktiske push er ikke afprøvet.

Der er ingen automatiseret testsuite. Før stabil release bør der være regressionstest omkring de reproducerede fejl og integrationstest for publish/draft/unpublish, slettet/flyttet undertræ, flersprog, exclusions, SQL Server/SQLite, første installation og provider-rebuild. Det er disse forløb, der afgør om pakkens kerne virker, også når et simpelt build er grønt.

Historikken består af fem commits fra én forfatter og er ikke shallow. Der er for lidt historik og for upræcise commitbeskeder til at udlede egentlige bug- eller churnmønstre. Test- og buildresultaterne vejer derfor mere end Git-signalerne i denne vurdering.

## Forslag til releasegrænse

**Før en offentlig beta:** Ret fund 1-8 og de ødelagte dokumentationseksempler. Afklar SQL Server, backfill, kultur og recovery i betaens supportmatrix. Tilføj et CI-build og regressionstest for de reproducerede fejl. Installer derefter begge NuGet-artefakter i en separat, ny Umbraco-app og gennemfør publicering, kladdelagring, genpublicering, afpublicering og backfill.

**Før stabil release:** Luk de resterende drifts- og søgeresultatfejl, verificer de lovede databaser/providers og dokumentér en fuld migration fra en navngiven FullTextSearch-version. Umbraco 19 skal have sin egen kompatibilitetskontrol, når den relevante platformversion er tilgængelig.

Der er ikke ændret implementering, dependencies eller releaseopsætning i denne audit. Rapporten er den eneste nye fil i repositoryet.
