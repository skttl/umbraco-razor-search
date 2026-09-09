# Plan for rettelser før første release

Status: Implementering og verifikation er i gang på `main` til Umbraco 18 og `v17/main` til Umbraco 17. Se [verifikationsrapporten](release-validation.md) for gennemførte prøver og resterende releasekrav.

Grundlag: [releasevurdering](release-readiness-audit-2026-09-09.md).

## Beslutninger

- Målgruppen er almindelig offentlig websitesøgning med flere sprog, undertræer, indholdsfiltre, pagination og fremhævede uddrag.
- Fuzzy matching og wildcards indgår ikke i første release.
- Umbraco 17 og 18 skal understøttes. Umbraco 19 er en senere kompatibilitetskontrol.
- SQL Server indgår i første betas releaseverifikation, Examine er den første verificerede provider. Efterfølgende beslutning 2026-09-09: SQLite-verifikation springes over for første beta. SQLite forbliver tilgængelig, men de observerede samtidighedsfejl dokumenteres som en accepteret betabegrænsning. Core forbliver provider-uafhængig.
- Renderkøen forbliver i memory. Genstart må rydde køen, også almindelige publiceringsjobs. Der bygges ikke automatisk fortsættelse eller en holdbar jobkø alene for at håndtere genstart.
- Breaking changes i API og skema er acceptable. Pakken er ikke udgivet, og der skal ikke bygges bagudkompatibilitet uden et konkret behov.
- Acceptprøven skal supplere den lille demo med flere sprog, SQL Server, mange sider og fælles templateindhold.
- Separate versionsbranches: `main` følger nyeste understøttede Umbraco-major, aktuelt 18; `v17/main` vedligeholder 17. Ved overgangen til 19 oprettes `v18/main` fra den hidtidige `main`, hvorefter `main` opgraderes til 19. Ændringer, som gælder flere majorer, skal overføres og verificeres på de relevante branches.
- Load balancing understøttes fra første release med én dedikeret backofficeserver og flere frontendservere. Flere aktive backofficeservere er uden for første releases supportmatrix.
- Ét aktuelt snapshot pr. content key og kultur. URL og renderer er metadata, ikke ekstra snapshotidentiteter. En vellykket ny rendering erstatter det tidligere snapshot.
- Behold seneste brugbare snapshot ved renderfejl. Registrer seneste forsøg/fejl særskilt og brug et begrænset antal retries. Afpublicering, sletning og udelukkelse skal fortsat fjerne indhold fra søgningen.
- Kun invariant indhold og sprogvarianter understøttes. Segmenter fjernes fra første releases kontrakter.
- Editorers dokument-rebuild kræver publiceringsadgang til de berørte dokumenter inden for brugerens startnoder. Globale rebuilds og fulde HTML-snapshots er administratorfunktioner. API'et håndhæver rettighederne.
- Statusvisningen viser forventet indeksindhold, beregnet fra snapshots. Den nuværende sammenligning mellem to snapshotprojektioner fjernes. Faktisk providerindhold er ikke et krav til første release.
- Når en kultur angives, skal den matche en konfigureret Umbraco-kultur præcist. Der indføres ingen implicit sprogfamilie-søgning fra eksempelvis `da` til `da-DK`. En ukendt kultur giver en tydelig valideringsfejl.
- Søgning med en bestemt kultur inkluderer både netop den kultur og invariant indhold. Uden kultur søges kun invariant indhold. Der anvendes ingen implicit fallback til requestens kultur eller sitets standardsprog.
- Ændringer i templates, fælles indhold og extraction-konfiguration håndteres med manuel genopbygning af berørte sider eller hele sitet. Første release indeholder ikke automatisk sporing af disse afhængigheder.
- Alle pakkeindstillinger flyttes til `Umbraco:Community:RazorSearch`. Det tidligere rodelement `RazorSearch` understøttes ikke parallelt, i overensstemmelse med beslutningen om ingen unødvendig bagudkompatibilitet før første release.
- Core-NuGet-pakken skal inkludere et appsettings-schema, som automatisk registreres i den installerende Umbraco-app. Det skal også fungere, når core kommer ind transitivt via Examine-companionen.

## Konfigurationsplacering og appsettings-schema

Den nye struktur er:

```json
{
  "Umbraco": {
    "Community": {
      "RazorSearch": {
        "DefaultRenderer": "http",
        "SnapshotExtraction": {
          "BodySources": [
            { "Type": "selector", "Selector": "main" }
          ]
        }
      }
    }
  }
}
```

Bindingsnøglen er `Umbraco:Community:RazorSearch`. Miljøvariabler bruger eksempelvis `Umbraco__Community__RazorSearch__RenderRequestToken`.

Referenceimplementationen er verificeret i [AzureBlob-projektfilen](https://github.com/umbraco/Umbraco.StorageProviders/blob/c9779f8ed38ad03f8e45c235d28f1dbb14ac66ee/src/Umbraco.StorageProviders.AzureBlob/Umbraco.StorageProviders.AzureBlob.csproj) og dens [buildTransitive-props](https://github.com/umbraco/Umbraco.StorageProviders/blob/c9779f8ed38ad03f8e45c235d28f1dbb14ac66ee/src/Umbraco.StorageProviders.AzureBlob/buildTransitive/Umbraco.StorageProviders.AzureBlob.props). Den nævnte Cloud-pakke får mekanismen via sin afhængighed af denne pakke.

Planlagt pakning og registrering:

- Generer `appsettings-schema.Umbraco.Community.RazorSearch.json` fra schema-wrappertyper med `Umbraco -> Community -> RazorSearch`, der refererer til de faktiske options-typer. Brug Umbracos schema-generering som i AzureBlob-eksemplet, og verificer understøttelsen på begge versionslinjer.
- Pak schemafilen i NuGet-pakkens rod sammen med `buildTransitive/Umbraco.Community.RazorSearch.props`. Propsfilen registrerer schemaet som `UmbracoJsonSchemaFiles` med passende vægt efter CMS-schemaet.
- Lad Umbracos eksisterende buildtargets kopiere schemaet til den installerende apps projektmappe og tilføje referencen i dens `appsettings-schema.json`. Det sker ved build efter pakkeinstallation/restore, ikke gennem et installationsscript eller en manuel redigering af appsettings.
- Schemaet beskriver de færdige indstillinger, værdityper, dokumentation, defaults og tilladte source-typer. Det må ikke begrænse navne på custom renderere til kun den indbyggede HTTP-renderer.
- Skeln mellem en manglende options-værdi og en eksplicit tom source-liste. Schemaet skal passe til runtime-bindingen og må ikke genindføre defaults, som tilsidesætter brugerens valg.
- Schema-wrapperen må ikke afvise andre indstillinger under `Umbraco` eller `Umbraco:Community`. Pakkens egne options og selector/property-kilder valideres så præcist, som schemaformatet understøtter; runtime-validering bevares.
- Generer schemaet under build, så en ændret options-type opdaterer det. Kontroller clean build, inkrementelt build og pack, så schemaet altid eksisterer før pakning og ikke bliver forældet.

Accept: Installation af core direkte og via companion efterfulgt af build giver schemafil og en fungerende reference uden manuelle trin på Umbraco 17 og 18. Gentaget build duplikerer ikke referencen. En konkret options-ændring genfindes i det genererede schema. JSON-eksempler med den nye struktur validerer, og et fejlagtigt værditypevalg fanges.

## Kulturkontrakt

For et site med de konfigurerede kulturer `da-DK` og `en-US`:

| Forespørgsel | Indhold, som kan matche |
| --- | --- |
| Ingen kultur | Kun invariant indhold |
| `InCulture("da-DK")` | Dansk og invariant indhold |
| `InCulture("en-US")` | Engelsk og invariant indhold |
| `InCulture("da")` | Valideringsfejl, da kulturen ikke er konfigureret præcist sådan |

Tabellen angiver kulturafgrænsningen. Publiceringsstatus, beskyttelse, root-scope, content-type-filtre og exclusions gælder fortsat. Kulturer sammenlignes uden forskel på store og små bogstaver og normaliseres til den konfigurerede form; sprogfamilier udvides ikke.

Et invariant dokument skal kun optræde én gang pr. søgeresultat. Invariant indhold betyder et invariant dokument/snapshot, ikke en ekstra sprogvariant oprettet ud fra enkelte invariante properties på et flersproget dokument. Kulturafgrænsning og eventuel deduplikering må ikke implementeres ved at fjerne resultater efter providerens pagination.

## Undersøgelser

- Search Core 17.1.0 og provider 17.1.0-beta.1 har Umbraco-afhængigheder afgrænset til 17.x. De tilsvarende 18.1.0/18.1.0-beta.1-pakker er afgrænset til 18.x. Begge bruger net10.0. Separate release-linjer passer til denne opdeling; præcise minimumsversioner fastlægges ved dependency- og kompatibilitetskontrol.
- Den eksisterende in-memory-kø benyttes både ved publicering og manuelle rebuilds. Snapshotdata ligger i databasen.
- Den nuværende worker har ingen eksplicit afgrænsning efter Umbracos serverrolle.
- RazorSearch kalder allerede `IDistributedContentIndexRefresher` efter snapshotlagring og sletning. Umbraco Search skal fortsat eje distribution af indeksopdateringer; RazorSearch skal håndtere sin renderkø, snapshotidentitet, samtidighed og administrationsstatus.
- Umbraco understøtter fra 17 også flere backofficeservere, hvor alle kan have rollen SchedulingPublisher. Et tjek af serverrollen alene sikrer derfor ikke én renderworker i den topologi. Se [officiel vejledning](https://docs.umbraco.com/umbraco-cms/fundamentals/setup/server-setup/load-balancing/load-balancing-backoffice).

## Load balancing

Besluttet topologi: én dedikeret backofficeserver og flere frontendservere med fælles SQL Server-database og separate lokale Examine-indekser. SQLite er beregnet til én app-instans. Verifikation af SQLite er efterfølgende fravalgt som releasekrav for første beta.

- Backofficeserveren ejer renderkø, retries og køstatus. Frontendserverne håndterer forespørgsler og deres egne indeksopdateringer.
- Umbraco Search distribuerer allerede indeksændringer gennem Umbracos DistributedCache. RazorSearchs indeksregistrering bruger standarden `sameOriginOnly=false`, så lokale indeksopdateringer kan ske på alle noder. Dette er verificeret mod både Search 17.1.0 og 18.1.0.
- `sameOriginOnly=true` betyder den proces, der initierede ændringen, ikke SchedulingPublisher. Dette kan være relevant for en fremtidig provider med delt eksternt indeks, men er ikke en erstatning for at styre renderkøen.
- Rendering skal læse den aktuelle publicerede version. Den nuværende HTTP-renderer følger den absolutte publicerede URL, så en load balancer eller CDN kan sende requesten til en endnu ikke opdateret server/cache. Planen skal fastlægge renderdestination og værts-/kulturhåndtering uden at ændre søgeresultatets offentlige URL.
- Verificer databaseskrivning, migration/startsekvens, indeks på en nytilføjet frontendserver og renderingens fælles konfiguration/token.
- Acceptprøven skal starte mindst to separate app-instanser med fælles database og separate indeksmapper. Publicering på backoffice skal give ens resultater på frontend efter synkronisering; afpublicering skal fjerne dem. Test også URL-ændring og genstart under et rebuild.

Flere aktive backofficeservere kræver yderligere beslutninger om jobplacering, samtidige snapshotopdateringer og køstatus på tværs af processer. Umbracos distribuerede jobmekanisme kan koordinere udførelse, men flytter ikke automatisk RazorSearchs eksisterende in-memory-jobs og status mellem serverne.

## Implementeringsplan

Planen opdeles i afgrænsede ændringer med acceptkriterier. Produktvalgene ovenfor er besluttet. Konkrete biblioteksversioner og øvrige tekniske detaljer vælges og verificeres under implementeringen inden for disse kontrakter. Der er ikke oprettet branches, commits eller releases under planlægningen.

### 1. Versionslinjer, afhængigheder og buildgrundlag

- Opret `v17/main` fra et fælles udgangspunkt før opgraderingen af `main` til Umbraco 18. Undgå samtidig, uafhængig udvikling af samme fejlrettelse på begge branches: overfør fælles rettelser og verificer hver linje.
- Opdater CMS, Search Core, Examine-provider, EF Core og klientafhængigheder til en sammenhængende, verificeret kombination pr. branch. Undersøg relevante NuGet/npm-advisories; undgå generelle undtagelser, der skjuler nye sikkerhedsproblemer.
- Afgræns NuGet-afhængigheder til den understøttede CMS-major. Samordn core-, companion- og klientversion pr. release; foreslået første versionsformat er `18.0.0-beta.1` og `17.0.0-beta.1`.
- Tilføj det manglende pakkeikon og kontroller begge pakkers metadata.
- Etabler schema-generering og transitive NuGet-buildassets som beskrevet ovenfor. Schemaets optionsindhold følger de efterfølgende ændringer i modellen.
- Tilføj passende .NET-regressionstest og et PR-workflow, som bruger `npm ci`, klientcheck, klientbuild, solution-build og pack af begge pakker. Adfærdstest følger rettelserne i de efterfølgende trin.

Accept: Begge branches bygger og pakker. NuGet-version, companion-afhængighed og klientmanifest stemmer. Pakkernes indhold omfatter alle nødvendige App_Plugins-filer samt appsettings-schema og buildTransitive-registrering. Dokumenterede minimumsversioner testes særskilt fra de nyeste patches.

### 2. Snapshotmodel og publiceringslivscyklus

- Erstat route-/rendererbaseret identitet med en unik nøgle for content key og normaliseret kultur. Fjern segmenter fra modeller, API og skema.
- Adskil seneste brugbare snapshot fra oplysninger om seneste renderforsøg. Fejl må ikke overskrive gyldig tekst eller gøre den usøgbar alene ved at ændre renderstatus.
- Forenkling af migrationshistorikken er tilladt før første udgivelse. Dokumentér en enkel nulstilling af den lokale udviklingsdatabase i stedet for at bygge kompatibilitet med uudgivne skemaer.
- Ignorer kladdeændringer i det publicerede indeks. Afpublicering, sletning, recycle bin og udelukkelse skal fjerne de relevante dokumenter/kulturer, også når efterkommere mister deres publicerede vej gennem træet.
- Ugyldiggør arbejde, som er blevet overhalet af flytning, URL-ændring, afpublicering eller en nyere publicering. Kontroller fortsat publiceringsstatus og den gældende jobgeneration før en rendering accepteres.
- Ved URL-ændring erstattes route-metadata; gamle tekstbidrag må ikke forblive i projektionen. Opdater berørte efterkommere ved ændringer af deres URL.
- Udsend Search-refresh efter accepterede databaseændringer. Bevar provider-uafhængigheden og lad Umbraco Search distribuere indeksarbejdet.

Accept: Reproducerede fejl for gem kladde, gamle routes og forsinkede jobs er dækket. Et gammelt job kan ikke gøre afpubliceret indhold søgbart igen. Lange offentlige URL'er kan lagres på SQL Server uden at indgå i en for stor indeksnøgle. Titelgrænser håndteres eksplicit.

### 3. In-memory-kø, backfill og fejl

- Brug content key og kultur som deduplikationsnøgle. Ventende arbejde kan samles; ændringer under rendering skal efterlade et efterfølgende job for den nyeste generation.
- Find den gældende publicerede route ved udførelse frem for at lade en gammel enqueue-URL styre et senere job.
- Gør rebuild til en baggrundsoperation: API'et returnerer efter accept af operationen, mens indhold gennemløbes i batches. Fjern den tavse slutgrænse på 250/5.000 dokumenter. Køens kapacitet må regulere baggrundsproducenten uden at fastholde management-requesten.
- Begræns retries til midlertidige fejl og giv HTTP-fejl og exceptions en ensartet registrering. Opryd afsluttede jobs og batches med begrænset retention.
- Ved genstart bortfalder kø og jobhistorik som aftalt. Gemte snapshots og oplysninger om seneste renderforsøg bliver bevaret. Dokumentér, at en ny manuel genopbygning kan være nødvendig.

Accept: Hurtige gentagne publiceringer ender med den nyeste version. Et rebuild med over 5.000 dokumenter og flere kulturer omfatter alle egnede dokumenter. Midlertidig HTTP-fejl fjerner ikke et brugbart snapshot. Køhistorik vokser ikke ubegrænset.

### 4. HTML-rendering og extraction

- Skift `Constants.ConfigurationSection` og optionsbinding til `Umbraco:Community:RazorSearch`. Opdater demoens indstillinger, valideringsbeskeder og konfigurationseksempler, inklusive miljøvariabler. Det historiske auditdokument bevares som beskrivelse af den gennemgåede kode.
- Erstat regex-baseret HTML-parsing og den hjemmelavede CSS-fortolkning med en etableret HTML/CSS-parser. Bevar inline-tekstsammenhæng og relevante separatorer mellem blokke.
- Fjern global extraction-state fra options. Hver rendering bruger ét valideret sæt indstillinger.
- Bind eksplicit konfigurerede kildearrays som erstatninger for defaults. Bevar den aftalte prioritering: første ikke-tomme title/summary, samling af heading/body.
- Indfør tydelig validering af kildetyper, selectors og rendererindstillinger. Foreslået konfigurationskontrakt: fraværende kildeindstilling bruger defaults, eksplicit tom liste deaktiverer den pågældende kildegruppe. Fejlkonfiguration må ikke stiltiende udvide extraction til hele siden.
- Kontroller HTTP-status, indholdstype og redirects, så vilkårlige succesfulde svar ikke automatisk bliver gyldige HTML-snapshots.

Accept: `hel<strong>lo</strong>` bliver `hello`; `>` i en citeret attribut forurener ikke tekst. Konfigureret `main` omfatter ikke navigation uden for `main`. SEO-property kan prioriteres før `<title>`. HTML i uddrag encodes korrekt.

### 5. Søgekontrakt og kultur

- Valider angivet kultur mod Umbracos konfigurerede kulturer. Fjern neutral-kultur-til-null-omskrivningen og kulturfiltrering efter pagination.
- Implementer kulturkontrakten ovenfor: angivet kultur plus invariant indhold, og kun invariant indhold ved udeladt kultur. Verificer providerens adfærd for både null-kultur og præcis kultur på begge versionslinjer; stol ikke på, at null automatisk betyder invariant-only.
- Match provider-dokumenter med det korrekte snapshot for den forespurgte kultur. Bevar providerens rangering og total, også når en side er tom, fordi skip ligger efter sidste resultat.
- Verificer root-, content-type- og exclusion-filtre før pagination. Beregn offentlige resultat-URL'er uden at eksponere intern renderadresse.
- Behold offentlig søgning med anonym access context. Understøt ikke medlemstilpassede eller segmentbaserede resultater i første release.

Accept: Dansk og engelsk tekst blandes ikke mellem matches og uddrag. Invariant indhold findes både med `da-DK`, med `en-US` og uden kultur, uden dubletter. Et sprogvariant dokument findes ikke uden kultur eller på grundlag af tekst fra et andet sprog. Total og pagination er korrekte ved flere sider og ved opslag efter sidste side. Ukendt kultur giver en tydelig fejl. Beskyttet eller udelukket indhold vises ikke i offentlig søgning.

### 6. Backoffice og load balancing

- Begræns worker og management-operationer til den dedikerede backofficeserver; enkeltserverdrift fungerer fortsat. Frontendservere får indeksopdateringer gennem Search og læser fælles snapshots.
- Definér en intern renderdestination, som kan hente frisk publiceret indhold uden at miste offentlig host, path og kultur. Adskil denne adresse fra resultat-URL'en. Verificer konfiguration/token på de relevante noder.
- Kontroller migrations- og deploymentrækkefølge, så frontendserverne ikke begynder at bruge et manglende eller inkompatibelt skema.
- Håndhæv dokumentrettigheder og startnoder server-side, også ved subtree-rebuilds. Administrator kræves for globale rebuilds og rå HTML. En UI-betingelse er kun et supplement til API-kontrollen.
- Omdøb indeksfanen til forventet indeksindhold og fjern den misvisende diff. Gør det tydeligt, om der findes et brugbart snapshot, om seneste renderforsøg fejlede, og om nyt arbejde venter.

Accept: En begrænset editor kan ikke starte arbejde på eller hente HTML fra utilgængelige dokumenter via direkte API-kald. Én backoffice- og mindst én frontendproces med fælles SQL Server viser samme korrekte søgeindhold efter synkronisering. En ny frontend kan opbygge sit lokale indeks fra snapshots. Statusvisningen lover ikke bevis for providerens aktuelle indhold.

### 7. Dokumentation, metadata og release

- Skriv et komplet, kørbart installations- og søgeeksempel for hver versionslinje. Ret async-controllerpattern, namespaces og placeringen af `AddSearchCore()`. Tilføj absolutte dokumentationslinks i pakkernes README-filer.
- Brug udelukkende `Umbraco:Community:RazorSearch` i den gældende dokumentation og begge pakkers README-filer. Beskriv schemaets automatiske registrering ved build efter NuGet-installation og hvordan editoren benytter appens `appsettings-schema.json`.
- Dokumentér breaking changes fra FullTextSearch, keys kontra numeriske IDs, resultategenskaber og funktioner, som ikke følger med. Formulér fuzzy/wildcards som fravær af egne RazorSearch-funktioner; undgå at love eller afvise al providerspecifik querysyntaks.
- Dokumentér kulturkontrakten, publiceringsforsinkelse, renderfejl/retries, genstart og manuel recovery samt load-balancing-topologien. Vis manuel genopbygning efter ændringer i templates, fælles indhold og extraction-konfiguration, også som et konkret deploymenttrin hvor relevant.
- Opdater Marketplace-kategori, dokumentationslink, screenshots og beskrivelse. Foreslået listing: hovedpakken er den primære indgang; companion beskrives tydeligt som Examine-tilvalg uden en konkurrerende, identisk listing.
- Samordn versionsbranches og release-tags. Valider at taggets major svarer til branchens dependencies, også når releaseworkflowet kører på et tag. Pak til en bestemt artefaktmappe og gør NuGet-genkørsel efter delvis publicering forudsigelig.
- Opdater Agents.md og CONTRIBUTING til den faktiske arkitektur og en reproducerbar demoopsætning. Skriv kuraterede beta-release notes og kendte begrænsninger.

Accept: En ny udvikler kan installere den korrekte beta, sætte provider op, genopbygge snapshots og udføre sin første søgning ved at følge dokumentationen. Metadata og klientversion matcher den faktiske pakke.

### 8. Samlet releaseprøve

- Installer de producerede core- og companion-NuGet-pakker i separate acceptapps. Project references i demoen er ikke tilstrækkeligt installationsbevis.
- Verificer Umbraco 17 og 18 med SQL Server og den aftalte load-balancing-topologi. SQLite-verifikation er efter den seneste beslutning udskudt fra første beta.
- Brug et datasæt med invariant indhold, flere sprog, flere sites/undertræer, beskyttet indhold, lange URL'er og over 5.000 dokumenter.
- Afprøv gem kladde, publish, hurtig republish, delvis kultur-afpublicering, afpubliceret forælder, move, recycle bin, delete, exclusions, renderfejl, URL-ændring og genstart under rebuild.
- Afprøv alle rækker i kulturkontrakten med flere sider af blandet invariant og sprogvariant indhold. Verificer, at manuel genopbygning opdaterer snapshots efter ændrede templates og extraction-regler.
- Verificer statiske assets og backofficeværktøjer fra de installerede pakker, ikke kun fra klientens udviklingsbuild.
- Verificer schemafil, buildTransitive-props og den resulterende `appsettings-schema.json` i acceptapps med direkte core-reference og med kun companion-reference. Kontroller at appen binder de nye indstillinger, og at editorens completion/validering virker under `Umbraco:Community:RazorSearch`.

Releasekrav: Alle aftalte acceptkriterier består på den understøttede matrix. Resterende begrænsninger skal være eksplicit accepterede og dokumenterede. Publicering er et efterfølgende trin efter implementering og verifikation.
