using Microsoft.Data.SqlClient;
using SqlKata.Compilers;
using SqlKata;
using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;

namespace AonGenerator;

internal class Generator
{
    protected SqlConnection? connection;
    protected Dictionary<string, Dictionary<int, DbRow>> tableRows = [];
    protected Dictionary<string, Dictionary<int, List<DbRow>>>? traitRows;
    protected Dictionary<string, Dictionary<int, DbRow>> typeRows = [];

    protected Generator(Options options)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = options.DbHost,
            InitialCatalog = options.DbDatabase,
            UserID = options.DbUser,
            Password = options.DbPassword,
        };

        if (IPAddress.IsLoopback(IPAddress.Parse(options.DbHost)))
        {
            builder.TrustServerCertificate = true;
        }

        connection = new SqlConnection(builder.ConnectionString);
    }

    public static void Generate(Options options)
    {
        var generator = new Generator(options);

        var indexFunctions = generator.GetDocumentFunctions();

        var totalWatch = Stopwatch.StartNew();
        int totalCount = 0;
        string outputPath = @"C:\Downloads\Aon";

        foreach ((var type, var fun) in indexFunctions)
        {
            Console.WriteLine($"Generating {type}... ");

            var watch = Stopwatch.StartNew();
            var docs = fun();

            if (docs.Count == 0)
            {
                Console.WriteLine("Error: 0 documents.");

                continue;
            }

            foreach (var doc in docs)
            {
                var path = Path.Combine(outputPath, doc.Url.Trim('/').Replace('/', '\\') + ".html");
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "");
                var file = new StreamWriter(path);
                file.Write(GetDocumentHtml(doc));
                file.Dispose();
            }

            watch.Stop();

            Console.WriteLine($"Done. Generated {docs.Count} documents in {watch.ElapsedMilliseconds} ms.");
            totalCount += docs.Count;
        }

        totalWatch.Stop();

        Console.WriteLine($"All done. Generated {totalCount} documents in {totalWatch.ElapsedMilliseconds} ms.");
    }

    protected Dictionary<string, Func<List<Document>>> GetDocumentFunctions()
    {
        return new Dictionary<string, Func<List<Document>>>()
        {
            { "creature", GetCreatureDocuments },
        };
    }

    protected static string GetDocumentHtml(Document doc)
    {
        return $$"""
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>{{doc.Title}}</title>
                {{GetStyle()}}
            </head>
            <body>
                <div class="column gap-medium">
                  <header>
                    <div style="font-size: 32px;">
                      Archives of Nethys
                    </div>
                  </header>
                  <div class="row gap-medium">
                    <navigation>
                      Menu goes here
                    </navigation>
                    <main id="content" class="column gap-medium">
                      {{doc.Content}}
                    </main>
                  </div>
                </div>
                {{GetJavascript()}}
            </body>
            </html>
            """;
    }

    protected static string GetStyle()
    {
        return """
            <style>
                body {
                    background-color: black;
                    color: white;
                }

                a {
                    color: inherit;
                }

                h1, h2, h3, h4, h5, h6, p {
                    margin: 0;
                }

                hr {
                    margin: 0;
                    width: 100%;
                }

                .column {
                    display: flex;
                    flex-direction: column;
                }

                .row {
                    display: flex;
                    flex-direction: row;
                }

                .gap-tiny {
                    gap: 4px;
                }

                .gap-medium {
                    gap: 12px;
                }

                .justify-between {
                    justify-content: space-between;
                }

                .trait {
                    background-color: #531004;
                    border-color: #d5c489;
                    border-style: double;
                    border-width: 2px;
                    color: white;
                    font-size: 1em;
                    font-style: normal;
                    font-variant: small-caps;
                    font-weight: 700;
                    padding: 3px 5px;
                }

                .trait a {
                    text-decoration: none;
                }

                .trait a:hover {
                    text-decoration: underline;
                }
            </style>
            """;
    }

    protected static string GetJavascript()
    {
        return """
            <script>
              class CreatureValue extends HTMLElement {
                static observedAttributes = ['value', 'level'];

                constructor() {
                  super()
                }

                connectedCallback() {
                  this.updateHtml(window.location.href)
                  window.navigation.addEventListener('navigate', (event) =>
                    this.updateHtml(event.destination.url)
                  )
                }

                attributeChangedCallback(name, oldValue, newValue) {
                  this.updateHtml(window.location.href)
                }

                updateHtml(urlString) {
                  let url = new URL(urlString)
                  let urlParams = new URLSearchParams(url.search)
                  let value = this.getAttribute('value')
                  let level = parseInt(this.getAttribute('level'))
                  let type = this.getAttribute('type')

                  if (!value) {
                    return;
                  }

                  value = parseInt(value)

                  if (urlParams.get('elite')) {
                    if (type == 'level') {
                      if (value <= 0) {
                        value += 2
                      } else {
                        value += 1
                      }
                    } else if (type == 'hp') {
                      if (level <= 1) {
                        value += 10
                      } else if (level <= 4) {
                        value += 15
                      } else if (level <= 19) {
                        value += 20
                      } else {
                        vlaue += 30
                      }
                    } else {
                      value += 2
                    }
                  }

                  if (urlParams.get('weak')) {
                    if (type == 'level') {
                      if (value == 1) {
                        value -= 2
                      } else {
                        value -= 1
                      }
                    } else if (type == 'hp') {
                      if (level <= 1) {
                        value -= 10
                      } else if (level <= 4) {
                        value -= 15
                      } else if (level <= 19) {
                        value -= 20
                      } else {
                        vlaue -= 30
                      }
                    } else {
                      value -= 2
                    }
                  }

                  if (urlParams.get('pwl') && type != 'hp' && type != 'level') {
                    value -= Math.max(0, level)
                  }

                  if (value >= 0 && this.getAttribute('with-sign') !== null) {
                    value = '+' + value
                  }

                  this.innerHTML = value
                }
              }

              class ParameterToggle extends HTMLElement {
                static observedAttributes = ['parameter', 'value', 'add-label', 'remove-label'];

                constructor() {
                  super()
                  this.attachShadow({ mode: 'open' })
                }

                connectedCallback() {
                  this.updateHtml(window.location.href)
                  window.navigation.addEventListener('navigate', (event) =>
                    this.updateHtml(event.destination.url)
                  )
                }

                attributeChangedCallback(name, oldValue, newValue) {
                  this.updateHtml(window.location.href)
                }

                updateHtml(urlString) {
                  let url = new URL(urlString)
                  let urlParams = new URLSearchParams(url.search)
                  let param = this.getAttribute('parameter')
                  let value = this.getAttribute('value')
                  let label = '';

                  if (urlParams.get(param) == value) {
                    label = this.getAttribute('remove-label')
                  } else {
                    label = this.getAttribute('add-label')
                  }

                  let button = document.createElement('button')
                  button.innerHTML = label
                  button.onclick = function() {
                    if (urlParams.get(param) == value) {
                      urlParams.delete(param)
                    } else {
                      urlParams.append(param, value)
                    }

                    url.search = urlParams.toString()
                    window.history.replaceState(null, '', url.toString())
                  }

                  this.shadowRoot.innerHTML = ''
                  this.shadowRoot.append(button)
                }
              }

              customElements.define("creature-value", CreatureValue)
              customElements.define("parameter-toggle", ParameterToggle)
            </script>

            """;
    }

    /**
     * Type Document functions
     */

    protected List<Document> GetCreatureDocuments()
    {
        var docs = new List<Document>();
        var type = "creature";

        var rows = GetTypeRows(type);

        foreach ((int id, DbRow row) in rows)
        {
            var doc = new Document();
            var traits = GetTraitRows(type, id);

            doc.Url = GetDocumentUrl(type, row);

            int level = row.Ints["CreatureLevel"] ?? 0;

            List<string> hpParts = [
                $"""
                <b>HP</b> <creature-value value="{row.Ints["HP"]}" type="hp" level="{level}"></creature-value>
                {row.Strings["HPOther"].WrapWithParentheses()}
                {(row.Strings["Regeneration"] != null ? $", regeneration {row.Strings["Regeneration"]}" : "")}
                """,
                row.Strings["Hardness"] != null ? $"<b>Hardness</b> {row.Strings["Hardness"]}" : "",
                row.Strings["Immunities"] != null ? $"<b>Immunities</b> {row.Strings["Immunities"]}" : "",
                row.Strings["Resistances"] != null ? $"<b>Resistances</b> {row.Strings["Resistances"]}" : "",
                row.Strings["Weaknesses"] != null ? $"<b>Weaknesses</b> {row.Strings["Weaknesses"]}" : ""
            ];

            doc.Content =
                $"""
                <h1 class="title">{row.Strings["TitleName"] ?? row.Strings["Name"]}</h1>

                <p>{row.Strings["Description"]}</p>

                <div class="row gap-tiny">
                  <parameter-toggle
                    parameter="elite"
                    value="true"
                    add-label="Apply Elite template"
                    remove-label="Remove Elite template"
                  ></parameter-toggle>
                  <parameter-toggle
                    parameter="weak"
                    value="true"
                    add-label="Apply Weak template"
                    remove-label="Remove Weak template"
                  ></parameter-toggle>
                  <parameter-toggle
                    parameter="pwl"
                    value="true"
                    add-label="Apply PWL"
                    remove-label="Remove PWL"
                  ></parameter-toggle>
                </div>

                <h1 class="row justify-between title">
                  <div>{row.Strings["Name"]}</div>
                  <div>Creature <creature-value value="{level}" type="level"></creature-value></div>
                </h1>

                <div class="column gap-tiny">

                  {GetTraitsHtml(traits, alignment: null, size: null)}

                  {/* Source */""}

                  <div><b>Perception</b>
                    <creature-value value="{row.Ints["PerceptionMod"]}" level="{level}" with-sign></creature-value>
                    {(row.Strings["PerceptionOther"] != null ? $"; {row.Strings["PerceptionOther"]}" : "")}
                  </div>

                  {/* Languages */""}

                  {/* Skills */""}

                  <div>
                    <b>Str</b> {row.Ints["STR"]?.WithSign()},
                    <b>Dex</b> {row.Ints["DEX"]?.WithSign()},
                    <b>Con</b> {row.Ints["CON"]?.WithSign()},
                    <b>Int</b> {row.Ints["INT"]?.WithSign()},
                    <b>Wis</b> {row.Ints["WIS"]?.WithSign()},
                    <b>Cha</b> {row.Ints["CHA"]?.WithSign()}
                  </div>

                  <div>{row.Strings["TopAbilities"]}</div>

                  {/* Items */""}

                </div>

                <hr />

                <div class="column gap-tiny">

                  <div>
                    <b>AC</b> <creature-value value="{row.Ints["AC"]}" level="{level}"></creature-value> {row.Strings["ACOther"]},
                    <b>Fort</b> <creature-value value="{row.Ints["Fort"]}" level="{level}" with-sign></creature-value> {row.Strings["FortOther"].WrapWithParentheses()},
                    <b>Ref</b> <creature-value value="{row.Ints["Ref"]}" level="{level}" with-sign></creature-value> {row.Strings["RefOther"].WrapWithParentheses()},
                    <b>Will</b> <creature-value value="{row.Ints["Will"]}" level="{level}" with-sign></creature-value> {row.Strings["WillOther"].WrapWithParentheses()}
                  </div>

                  <div>
                    {hpParts.Where(s => s != "").Join("; ")}
                  </div>

                  <div>
                    {row.Strings["MiddleAbilities"]}
                  </div>

                </div>

                <hr />

                <div class="column gap-tiny">

                  {/* Speed */""}

                  {/* Attacks */""}

                  {/* Spells */""}

                  {/* Special attacks */""}

                </div>
                """;

            doc.Content = ReplaceTags(doc.Content) ?? "";

            docs.Add(doc);
        }

        return docs;
    }

    /**
     * Utility functions
     */

    protected string GetDocumentId(string type, DbRow row)
    {
        string prefix = type switch
        {
            "action" => "actions",
            "ancestry" =>
                row.Bools["Versatile"] == true ? "ancestries--versatile-heritages"
                    : row.Bools["HalfHuman"] == true ? "ancestries--half-human-heritages"
                    : "ancestries",
            "animal-companion" => row.Bools["Undead"] == true
                ? "companions--undead-companions"
                : "companions--animal-companions",
            "animal-companion-advanced" => "companions--animal-companions--advanced-options",
            "animal-companion-specialization" => "companions--animal-companions--specializations",
            "animal-companion-unique" => "companions--animal-companions",
            "arcane-school" => "classes--wizard--arcane-schools",
            "arcane-thesis" => "classes--wizard--arcane-theses",
            "archetype" => "archetypes",
            "armor" => "equipment--armor--base-armor",
            "armor-group" => "equipment--armor--specializations",
            "article" => "setting-articles",
            "background" => "backgrounds",
            "bloodline" => "classes--sorcerer--bloodlines",
            "campsite-meal" => "kingmaker--campsite-meals",
            "cause" => "classes--champion--causes",
            "class" => "classes",
            //"class-sample" => GetClassSampleDocumentIdPrefix(row),
            "condition" => "conditions",
            "conscious-mind" => "classes--psychic--conscious-minds",
            "creature" => "creatures",
            "creature-ability" => "creatures--abilities",
            "creature-family" => "creatures--families",
            "creature-theme-template" => "creatures--templates",
            "curse" => "curses",
            "deity" => "deities",
            "deity-category" => "deities--deity-categories",
            "deviant-ability-classification" => "feats--deviant-feats",
            "disease" => "diseases",
            "doctrine" => "classes--cleric--doctrines",
            "domain" => "domains",
            "druidic-order" => "classes--druid--druidic-orders",
            "eidolon" => "classes--summoner--eidolons",
            "element" => "classes--kineticist--elements",
            //"equipment" => GetEquipmentDocumentIdPrefix(row),
            "equipment" => "equipment", // TODO
            "equipment-child" => "",
            //"familiar-ability" => GetFamiliarDocumentIdPrefix(row),
            "familiar-ability" => "familiar-abilities", // TODO
            "familiar-specific" => "familiars--specific-familiars",
            "feat" => "feats",
            "hazard" => "hazards",
            //"heritage" => GetHeritageDocumentIdPrefix(row),
            "heritage" => "heritages", // TODO
            "hellknight-order" => "hellknight-orders",
            "hunters-edge" => "classes-ranger-hunters-edges",
            "hybrid-study" => "classes--magus--hybrid-studies",
            "implement" => "classes--thaumaturge--implements",
            "innovation" => "classes--inventor--innovations",
            "instinct" => "classes--barbarian--instincts",
            "kingdom-event" => "kingmaker--events",
            "kingdom-structure" => "kingmaker--structures",
            "language" => "language",
            "lesson" => "classes--witch--lessons",
            "methodology" => "classes--investigator--methodologies",
            "muse" => "classes--bard--muses",
            "mystery" => "classes--oracle--mysteries",
            "patron" => "classes--witch--patron-themes",
            "plane" => "planes",
            "racket" => "classes--rogue--rackets",
            "relic" => "equipment--relics",
            "research-field" => "classes--alchemist--research-fields",
            "ritual" => "rituals",
            //"rules" => GetRulesDocumentIdPrefix(row),
            "rules" => "rules", // TODO
            "set-relic" => "equipment--relics--set-relics",
            "shield" => "equipment--shields--base-shields",
            "siege-weapon" => "equipment--siege-weapons",
            "skill" => row.Bools["Kingmaker"] == true
                ? "kingmaker--skills"
                : "skills",
            "skill-general-action" => row.Bools["Kingmaker"] == true
                ? "kingmaker--skills--general-skill-actions"
                : "skills--general-skill-actions",
            "source" => "sources",
            "spell" => "spells",
            "style" => "classes--swashbuckler--styles",
            "subconscious-mind" => "classes--psychic--subconscious-minds",
            "tenet" => "classes--champion--tenets",
            "tradition" => "spells",
            "trait" => "traits",
            "vehicle" => "equipment--vehicles",
            "warfare-army" => "kingmaker--armies",
            "warfare-tactic" => "kingmaker--warfare-tactics",
            "way" => "classes--gunslinger--ways",
            "weapon" => "equipment--weapons--base-weapons",
            "weapon-group" => "equipment--weapons--critical-specializations",
            "weather-hazard" => "hazards--weather-hazards",
            _ => throw new ArgumentException($"GetDocumentId for type '{type}' not implemented"),
        };

        string suffix = "";
        DbRow? classRow;

        switch (type)
        {
            case "class-feature":
                classRow = GetTypeRow("class", row.Ints["ClassesID"] ?? 0);

                return $"classes--{classRow?.Strings["Name"]?.UrlFormat()}--feature-{row.Strings["Name"]?.UrlFormat()}";

            case "class-kit":
                classRow = GetTypeRow("class", row.Ints["ClassesID"] ?? 0);

                return $"classes--{classRow?.Strings["Name"]?.UrlFormat()}--class-kit";

            case "equipment-child":
                var parentRow = GetTypeRow("equipment", row.Ints["TreasureID"] ?? 0) ?? new DbRow();

                return GetDocumentId("equipment", parentRow);
        }

        string idKey = GetIdColumn(GetTypeTable(type));
        string nameKey = type switch
        {
            "deity-category" => "CategoryName",
            _ => "Name",
        };

        return $"{prefix}--{row.Ints[idKey]}-{row.Strings[nameKey]?.UrlFormat()}{suffix}";
    }

    protected string GetDocumentUrl(string type, DbRow row)
    {
        string id = GetDocumentId(type, row);

        return "/" + id.Replace("--", "/");
    }

    protected static string GetIdColumn(string table)
    {
        return table switch
        {
            "ClassConsciousMindCantrips" => "ClassConsciousMindCantripID",
            "DomainApocryphal" => "DomainApocryphaID",
            "Duplicates" => "DuplcatesID",
            "TreasureItemBonuses" => "TreasureItemBonusID",
            _ => $"{table}ID",
        };
    }

    protected Dictionary<int, DbRow> GetTableRows(string table)
    {
        var rows = tableRows.GetValueOrDefault(table);

        if (rows != null)
        {
            return rows;
        }

        string idKey = GetIdColumn(table);

        rows = FetchRows(new Query(table))
            .ToDictionary(r => r.Ints[idKey] ?? 0, r => r);

        tableRows[table] = rows;

        return rows;
    }

    protected List<DbRow> GetTraitRows(string type, int id)
    {
        return GetTraitRowsForTable(GetTypeTable(type), id);
    }

    protected List<DbRow> GetTraitRowsForTable(string table, int id)
    {
        if (traitRows == null)
        {
            var rows = FetchRows(new Query()
                .Select("TableName", "ObjectID", "Name", "NameOverride", "Traits.TraitDefinitionsID")
                .From("Traits")
                .Join("TraitDefinitions", "TraitDefinitions.TraitDefinitionsID", "Traits.TraitDefinitionsID")
            );

            traitRows = rows
                .GroupBy(r => r.Strings["TableName"])
                .ToDictionary(
                    g => g.Key ?? "",
                    g => g.GroupBy(r => r.Ints["ObjectID"]).ToDictionary(g2 => g2.Key ?? 0, g2 => g2.ToList())
                );
        }

        return traitRows
            .GetValueOrDefault(table, new Dictionary<int, List<DbRow>>())
            .GetValueOrDefault(id, new List<DbRow>())
            .OrderBy(r => r.Strings["Name"])
            .ToList();
    }

    protected string GetTraitsHtml(IEnumerable<DbRow> traits, string? alignment = null, string? size = null)
    {
        List<string> tags = [];

        var rarities = new List<string>() { "uncommon", "rare", "unique" };

        traits
            .Where(r => rarities.Contains(r.Strings["Name"]?.ToLower() ?? ""))
            .ForEach(r => tags.Add(GetTraitTag(r)));

        if (alignment != null && GetTypeRow("rules", 95) is DbRow alignmentRulesRow)
        {
            tags.Add($"""<div class="traitalignment"><a href="{GetDocumentUrl("rules", alignmentRulesRow)}">{alignment}</a></div>""");
        }

        if (size != null)
        {
            tags.Add($"""<div class="traitsize">{size}"</div>""");
        }

        traits
            .Where(r => !rarities.Contains(r.Strings["Name"]?.ToLower() ?? ""))
            .Where(r => r.Strings["Name"]?.ToLower() != "common")
            .OrderBy(r => r.Strings["Name"])
            .ForEach(r => tags.Add(GetTraitTag(r)));

        return $"""<div class="row">{tags.Join("")}</div>""";
    }

    protected string GetTraitTag(DbRow row)
    {
        string name = row.Strings["NameOverride"] ?? row.Strings["Name"] ?? "";
        string url = GetDocumentUrl("trait", row);

        return $"""<div class="trait"><a href="{url}">{name}</a></div>""";
    }

    protected DbRow? GetTypeRow(string type, int id)
    {
        var rows = typeRows.GetValueOrDefault(type);

        if (rows != null)
        {
            return rows.GetValueOrDefault(id);
        }

        return GetTypeRows(type).GetValueOrDefault(id);
    }

    protected Dictionary<int, DbRow> GetTypeRows(string type)
    {
        var rows = typeRows.GetValueOrDefault(type);

        if (rows != null)
        {
            return rows;
        }

        string idKey = GetIdColumn(GetTypeTable(type));
        rows = (type switch
        {
            _ => FetchRows(new Query(GetTypeTable(type))),
        }).ToDictionary(r => r.Ints[idKey] ?? 0, r => r);

        typeRows[type] = rows;

        return rows;
    }

    protected static string GetTypeTable(string type)
    {
        return type switch
        {
            "action" => "Actions",
            "activity" => "Activities",
            "ancestry" => "Ancestries",
            "animal-companion" => "AnimalCompanions",
            "animal-companion-advanced" => "AnimalCompanionsMature",
            "animal-companion-specialization" => "AnimalCompanionSpecializations",
            "animal-companion-unique" => "AnimalCompanionsUnique",
            "arcane-school" => "ClassArcaneSchools",
            "arcane-thesis" => "ClassArcaneThesis",
            "archetype" => "Archetypes",
            "armor" => "GearArmor",
            "armor-group" => "GearArmorGroups",
            "article" => "APArticles",
            "background" => "Backgrounds",
            "bloodline" => "ClassBloodlines",
            "campsite-meal" => "CampsiteMeals",
            "category-page" => "PageHeaders",
            "cause" => "ClassChampionCauses",
            "class" => "Classes",
            "class-feature" => "ClassFeatures",
            "class-kit" => "GearClassKits",
            "class-sample" => "ClassSamples",
            "condition" => "Conditions",
            "conscious-mind" => "ClassConsciousMind",
            "creature" => "Monsters",
            "creature-adjustment" => "MonsterAdjustments",
            "creature-family" => "MonsterFamilies",
            "creature-theme-template" => "MonsterThemeTemplates",
            "curse" => "AfflictionCurses",
            "deity" => "Deities",
            "deity-category" => "DeityCategories",
            "deviant-ability-classification" => "FeatDeviantGroups",
            "disease" => "AfflictionDiseases",
            "divine-intercession" => "DivineIntercessions",
            "doctrine" => "ClassDoctrines",
            "domain" => "Domains",
            "druidic-order" => "ClassDruidOrders",
            "duplicate" => "Duplicates",
            "eidolon" => "Eidolons",
            "element" => "ClassElementJunctions",
            "emotional-state" => "EmotionalStates",
            "equipment" => "Treasure",
            "equipment-category" => "TreasureCategories",
            "equipment-child" => "TreasureChildren",
            "essence-power" => "SoulforgeEssencePowers",
            "familiar-ability" => "FamiliarAbilities",
            "familiar-specific" => "FamiliarsSpecific",
            "feat" => "Feats",
            "feat-group" => "FeatGroups",
            "hazard" => "Hazards",
            "hellknight-order" => "HellknightOrders",
            "hellknight-order-ability" => "HellknightOrderAbilities",
            "heritage" => "AncestryHeritages",
            "hunters-edge" => "ClassHuntersEdges",
            "hybrid-study" => "ClassHybridStudies",
            "implement" => "ClassImplements",
            "innovation" => "ClassInnovations",
            "instinct" => "ClassInstincts",
            "kingdom-event" => "KingdomEvents",
            "kingdom-structure" => "KingdomStructures",
            "language" => "Languages",
            "lesson" => "ClassLessons",
            "methodology" => "ClassMethodologies",
            "creature-ability" => "MonsterAbilities",
            "muse" => "ClassMuses",
            "mystery" => "ClassMysteries",
            "page" => "PageHeaders",
            "patron" => "ClassPatrons",
            "plane" => "Planes",
            "racket" => "ClassRackets",
            "relic" => "GearRelics",
            "research-field" => "ClassResearchFields",
            "ritual" => "Rituals",
            "rules" => "Rules",
            "set-relic" => "GearRelicsSets",
            "shield" => "GearShields",
            "siege-weapon" => "SiegeEngines",
            "skill" => "Skills",
            "skill-general-action" => "SkillGeneralActions",
            "skill-general-child" => "SkillGeneralChildren",
            "source" => "Sources",
            "spell" => "Spells",
            "style" => "ClassStyles",
            "subconscious-mind" => "ClassSubconsciousMind",
            "tenet" => "ClassTenets",
            "tradition" => "SpellTraditionDefinitions",
            "trait" => "TraitDefinitions",
            "vehicle" => "Vehicles",
            "warfare-army" => "WarfareArmies",
            "warfare-tactic" => "WarfareTactics",
            "way" => "ClassGunslingerWays",
            "weapon" => "GearWeapons",
            "weapon-group" => "GearWeaponGroups",
            "weather-hazard" => "HazardsWeather",
            _ => throw new ArgumentException($"GetTypeTable for type '{type}' not implemented"),
        };
    }

    protected string? ReplaceTags(string? input)
    {
        if (input == null)
        {
            return null;
        }

        Dictionary<int, DbRow> tablesHtmlRows = GetTableRows("TablesHTML");

        foreach (Match match in Regex.Matches(input, @"<%?TABLES?\.?HTML#(\d+)%%>"))
        {
            int id = int.Parse(match.Groups[1].Value);
            input = input.Replace(
                match.Groups[0].Value,
                $"\n\n## {tablesHtmlRows[id].Strings["Name"]}\n{tablesHtmlRows[id].Strings["TableCode"]}"
            );
        }

        /*
        foreach (Match match in Regex.Matches(input, @"<%?CRIT(ICAL)?.EFFECTS#(\d+)%%>"))
        {
            int id = int.Parse(match.Groups[2].Value);
            input = input.Replace(
                match.Groups[0].Value,
                "\n\n" + GetCriticalEffectsMarkdown(id)
            );
        }
        */

        /*
        foreach (Match match in Regex.Matches(input, @"<%?ACTION.TYPES?#(\d+)%%>"))
        {
            int actionId = int.Parse(match.Groups[1].Value);
            input = input.Replace(
                match.Groups[0].Value,
                @$"<actions string=""{GetActionsFromId(actionId)}"" />"
            );
        }
        */

        input = ReplaceLinkWithIdTags(input);
        input = ReplaceLinkTags(input);
        //input = ReplaceSummonTags(input);

        /*
        foreach (Match match in Regex.Matches(input, @"<%?([\w\.]+)#(\d+)%%>"))
        {
            string type = match.Groups[1].Value;
            int id = int.Parse(match.Groups[2].Value);

            type = TranslateType(type);

            var row = GetTypeRow(type, id);

            if (row != null)
            {
                if (type == "action" && (row.Bools["ActivateAction"] ?? false))
                {
                    input = input.Replace(
                        match.Groups[0].Value,
                        "\n\n" + GetActivateActionMarkdown(row)
                    );
                }
                else
                {
                    input = input.Replace(
                        match.Groups[0].Value,
                        $"\n\n<document level=\"2\" id=\"{GetDocumentId(type, row)}\" />"
                    );
                }
            }
        }
        */

        input = input
            .Replace("<hr>", "<hr />")
            .Replace("<br>", "<br />")
            .Replace("<br/ >", "<br />")
            .Replace("</br >", "<br />")
            .Replace("</br>", "<br />");

        input = Regex.Replace(input, @"(\s)+,", ",", RegexOptions.Multiline);
        input = Regex.Replace(input, @"(\s)+;", ";", RegexOptions.Multiline);

        return input;
    }

    protected string ReplaceLinkWithIdTags(string input)
    {
        foreach (Match match in Regex.Matches(input, @"<%?([\w\.]+)%(\d+)%%>(.*?)<%END>"))
        {
            string type = match.Groups[1].Value;
            int id = int.Parse(match.Groups[2].Value);
            DbRow? row;
            string url = "";

            switch (type)
            {
                case "ALCHEMICAL.CATEGORIES":
                    row = GetTableRows("TreasureCategories")[id];
                    url = $"/equipment/alchemical-items/{row.Strings["Name"]?.UrlFormat()}";

                    break;

                case "ANCESTRY.HERITAGES":
                    row = GetTypeRow("heritage", id);

                    if (row == null)
                    {
                        continue;
                    }

                    url = GetDocumentUrl("heritage", row);

                    break;

                case "ARCHETYPES.CATEGORY":
                    row = GetTableRows("ArchetypeCategories")[id];
                    url = $"/archetypes/{row.Strings["Name"]?.UrlFormat()}";

                    break;

                case "CLASS.DOCTRINES":
                    row = GetTypeRow("doctrine", id);

                    if (row == null)
                    {
                        continue;
                    }

                    url = GetDocumentUrl("doctrine", row);

                    break;

                case "CONSUMABLES":
                    row = GetTableRows("TreasureCategories")[id];
                    url = $"/equipment/consumables/{row.Strings["Name"]?.UrlFormat()}";

                    break;

                case "FEAT.TRAIT":
                case "FEAT.TRAITS": // TODO: Rules 1751
                    row = GetTypeRow("trait", id);
                    var traitGroupRows = GetTableRows("TraitGroups").Values;

                    url = $"/feats?include-traits={HttpUtility.UrlEncode(row?.Strings["Name"])}&sort=level-asc%3Bname-asc";

                    var traitGroupRow = traitGroupRows
                        .FirstOrDefault(r => r.Ints["TraitDefinitionsID"] == row?.Ints["TraitDefinitionsID"]);

                    if (traitGroupRow != null)
                    {
                        switch(traitGroupRow.Strings["GroupName"])
                        {
                            case "Ancestry":
                                var ancestryRow = GetTypeRows("ancestry")
                                    .Values
                                    .FirstOrDefault(r => r.Strings["Name"] == row?.Strings["Name"]);

                                if (ancestryRow != null)
                                {
                                    url = GetDocumentUrl("ancestry", ancestryRow) + "/feats";
                                }

                                break;

                            case "Class":
                                var classRow = GetTypeRows("class")
                                    .Values
                                    .FirstOrDefault(r => r.Strings["Name"] == row?.Strings["Name"]);

                                if (classRow != null)
                                {
                                    url = GetDocumentUrl("class", classRow) + "/feats";
                                }

                                break;
                        }
                    }

                    break;

                case "GENERAL.SKILLS":
                    row = GetTypeRow("skill-general-action", id);
                    url = $"/skills/general-skill-actions/{row?.Strings["Name"]?.UrlFormat()}";

                    break;

                case "HERITAGES.CATEGORY":
                    row = GetTypeRow("ancestry", id);
                    url = $"/ancestries/{row?.Strings["Name"]?.UrlFormat()}/heritages";

                    break;

                case "MONSTERS.ELITE":
                    row = GetTypeRow("creature", id);

                    if (row == null)
                    {
                        continue;
                    }

                    url = GetDocumentUrl("creature", row) + "?elite=true";

                    break;

                case "MONSTERS.WEAK":
                    row = GetTypeRow("creature", id);

                    if (row == null)
                    {
                        continue;
                    }

                    url = GetDocumentUrl("creature", row) + "?weak=true";

                    break;

                case "RUNES":
                    row = GetTableRows("TreasureCategories")[id];
                    url = $"/equipment/runes/{row.Strings["Name"]?.UrlFormat()}";

                    break;

                case "RELIC.ASPECTS":
                    url = $"/equipment/relics"; // TODO: Add filter. Example: Creature 1103

                    break;

                case "SKILLS.GENERAL":
                    url = ""; // TODO: Used by rules/classes/equipment-3246

                    break;

                case "TRADITIONS":
                    if (id == 0)
                    {
                        url = "/spells";
                    }
                    else
                    {
                        row = GetTypeRow("tradition", id) ?? new();
                        url = GetDocumentUrl("tradition", row);
                    }

                    break;

                case "TREASURE.CATEGORIES":
                case "TREASURECATEGORIES":
                    row = GetTableRows("TreasureCategories")[id];
                    url = $"/equipment/{row.Strings["Name"]?.UrlFormat()}";

                    break;

                case "TREASURE.WORN.CATEGORY":
                    row = GetTableRows("TreasureCategories")[id];
                    url = $"/equipment/worn-items/{row.Strings["Name"]?.UrlFormat()}";

                    break;

                default:
                    type = TagToType(type);
                    row = GetTypeRow(type, id);

                    if (row != null && type == "activity")
                    {
                        row = GetTypeRows("action").Values.FirstOrDefault(r => r.Strings["Name"] == row.Strings["Name"]) ?? new();
                        type = "action";
                    }

                    if (row == null)
                    {
                        continue;
                    }

                    url = GetDocumentUrl(type, row);

                    break;
            }

            input = input.Replace(
                match.Groups[0].Value,
                $"""<a href="{url}">{match.Groups[3].Value}</a>"""
            );
        }

        return input;
    }

    protected string ReplaceLinkTags(string input)
    {
        foreach (Match match in Regex.Matches(input, @"<%?([\w\.]+)%%>(.*?)<%END>").Cast<Match>())
        {
            string tag = match.Groups[1].Value;
            string url = tag switch
            {
                "ACTIONS.HOME" => "/actions",
                "AFFLICTIONS.HOME" => "/navigation",
                "ANCESTRIES.HOME" => "/ancestries",
                "ANIMAL.COMPANIONS.ADVANCED.ALL" => "/companions/animal-companions/advanced-options",
                "ANIMAL.COMPANIONS.SPECIALIZED.ALL" => "/companions/animal-companions/specializations",
                "ANIMALS" => "/equipment/animals",
                "ARCHETYPES.HOME" => "/archetypes",
                "ARMOR.GROUPS.HOME" => "/equipment/armor/specializations",
                "ARTICLES.HOME" => "/setting-articles",
                "BACKGROUNDS.HOME" => "/backgrounds",
                "BEAST.GUNS.HOME" => "/equipment/beast-guns",
                "CLASS.ARCANE.SCHOOLS.HOME" => "/classes/wizard/arcane-schools",
                "CLASS.ARCANE.THESIS.HOME" => "/classes/wizard/arcane-theses",
                "CLASS.BLOODLINES" => "/classes/sorcerer/bloodlines",
                "CLASS.BLOODLINES.HOME" => "/classes/sorcerer/bloodlines",
                "CLASS.CHAMPION.CAUSES.HOME" => "/classes/champion/causes",
                "CLASS.CONSCIOUS.MINDS.HOME" => "/classes/psychic/conscious-minds",
                "CLASS.DOCTRINES" => "/classes/cleric/doctrines",
                "CLASS.DRUID.ORDERS.HOME" => "/classes/druid/druidic-orders",
                "CLASS.ELEMENTS.ALL" => "/classes/kineticist/elements",
                "CLASS.HUNTERS.EDGES.HOME" => "/classes/ranger/hunters-edges",
                "CLASS.HYBRID.STUDIES.ALL" => "/classes/magus/hybrid-studies",
                "CLASS.IMPLEMENTS.HOME" => "/classes/thaumaturge/implements",
                "CLASS.INOVATIONS.HOME" => "/classes/inventor/innovations", // TODO: Fix
                "CLASS.INNOVATIONS.HOME" => "/classes/inventor/innovations",
                "CLASS.INSTINCTS.HOME" => "/classes/barbarian/instincts",
                "CLASS.METHODOLOGIES" => "/classes/investigator/methodologies",
                "CLASS.MUSES.HOME" => "/classes/bard/muses",
                "CLASS.MYSTERIES.ALL" => "/classes/oracle/mysteries",
                "CLASS.RACKETS.HOME" => "/classes/rogue/rackets",
                "CLASS.RESEARCH.FIELDS.HOME" => "/classes/alchemist/research-fields",
                "CLASS.SWASH.STYLES.ALL" => "/classes/swashbuckler/styles",
                "CLASS.SUBCONSCIOUS.MINDS.HOME" => "/classes/psychic/subconscious-minds",
                "CLASS.TENETS.HOME" => "/classes/champion/tenets",
                "CLASS.WAYS" => "/classes/gunslinger/ways",
                "CLASS.WAYS.HOME" => "/classes/gunslinger/ways",
                "CLASS.WITCH.LESSONS.ALL" => "/classes/witch/lessons",
                "CLASS.WITCH.PATRONS.ALL" => "/classes/witch/patron-themes",
                "CLASSES.HOME" => "/classes",
                "COMPANIONS" => "/animal-companions",
                "COMPANIONS.HOME" => "/animal-companions",
                "COMPANIONS.SPECIALIZED" => "/animal-companions/specialized",
                "CONDITIONS.ALL" => "/conditions",
                "CONSTRUCT.COMPANIONS" => "/companions/construct-companions",
                "CREATURES.HOME" => "/creatures",
                "DEITIES.HOME" => "/deities",
                "DISEASES.ALL" => "/diseases",
                "DOMAINS.HOME" => "/domains",
                "EIDOLONS.HOME" => "/classes/summoner/eidolons",
                "FAMILIARS" => "/familiar-abilities",
                "FAMILIARS.SPECIFIC.ALL" => "/familiars/specific-familiars",
                "FEAT.DEVIANT" => "/feats/deviant-feats",
                "FEATS.HOME" => "/feats",
                "GEAR.ARMOR.ALL" => "/equipment/armor",
                "GEAR.WEAPONS.ALL" => "/equipment/weapons",
                "GEAR.SHIELDS.ALL" => "/equipment/shields",
                "GM.SCREEN" => "/gm-screen",
                "HAZARDS.HOME" => "/hazards",
                "LANGUAGES.HOME" => "/languages",
                "PFS" => "/pathfinder-society",
                "PLANES.HOME" => "/planes",
                "RELIC.HOME" => "/equipment/relics",
                "RITUALS.HOME" => "/rituals",
                "RULES.HOME" => "/rules",
                "SIEGE.WEAPONS.HOME" => "/equipment/siege-weapons",
                "SETTING.HOME" => "/navigation",
                "SKILLS.HOME" => "/skills",
                "SPELLS.FOCUS.ALL" => "/spells/focus-spells",
                "SPELLS.HOME" => "/spells",
                "TRAITS.HOME" => "/traits",
                "TREASURE.HOME" => "/equipment",
                "UMR.HOME" => "/creatures/abilities",
                "VEHICLES.ALL" => "/equipment/vehicles",
                "VERSATILE.HOME" => "/ancestries/versatile-heritages",
                "WARFARE.ACTIONS" => "/kingmaker/war-actions",
                "WEAPON.GROUPS.HOME" => "/equipment/weapons/specializations",
                _ => throw new ArgumentException($"Unknown tag {tag}."),
            };

            input = input.Replace(
                match.Groups[0].Value,
                $"""<a href="{url}">{match.Groups[2].Value}</a>"""
            );
        }

        return input;
    }

    protected static string TagToType(string type)
    {
        var typeTranslations = new Dictionary<string, string>()
        {
            { "ACTIONS", "action" },
            { "ACTIVITIES", "activity" },
            { "AFFLICTION.CURSES", "curse" },
            { "ANCESTRIES", "ancestry" },
            { "ANIMAL.COMPANIONS.ADVANCED", "animal-companion-advanced" },
            { "ANIMAL.COMPANIONS.SPECIALIZED", "animal-companion-specialization" },
            { "ARCHETYPES", "archetype" },
            { "ARMOR", "armor" },
            { "ARMOR.GROUPS", "armor-group" },
            { "BACKGROUNDS", "background" },
            { "CLASSES", "class" },
            { "CLASS.ARCANE.SCHOOLS", "arcane-school" },
            { "CLASS.ARCANE.THESIS", "arcane-thesis" },
            { "CLASS.BLOODLINES", "bloodline" },
            { "CLASS.CHAMPION.CAUSES", "cause" },
            { "CLASS.CONSCIOUS.MINDS", "conscious-mind" },
            { "CLASS.DOCTRINES", "doctrine" },
            { "CLASS.DRUID.ORDERS", "druidic-order" },
            { "CLASS.HUNTERS.EDGES", "hunters-edge" },
            { "CLASS.HYBRID.STUDIES", "hybrid-study" },
            { "CLASS.IMPLEMENTS", "implement" },
            { "CLASS.INNOVATION", "innovation" },
            { "CLASS.INNOVATIONS", "innovation" },
            { "CLASS.INSTINCTS", "instinct" },
            { "CLASS.KITS", "class-kit" },
            { "CLASS.METHODOLOGIES", "methodology" },
            { "CLASS.MUSES", "muse" },
            { "CLASS.MYSTERIES", "mystery" },
            { "CLASS.RACKETS", "racket" },
            { "CLASS.RESEARCH.FIELDS", "research-field" },
            { "CLASS.SUBCONSCIOUS.MINDS", "subconscious-mind" },
            { "CLASS.SWASH.STYLES", "style" },
            { "CLASS.TENETS", "tenet" },
            { "CLASS.WAYS", "way" },
            { "CLASS.WITCH.PATRONS", "patron" },
            { "CLASS.WITCH.LESSONS", "lesson" },
            { "COMPANIONS", "animal-companion" },
            { "CONDITIONS", "condition" },
            { "CURSES", "curse" },
            { "DEITIES", "deity" },
            { "DEITY.CATEGORIES", "deity-category" },
            { "DISEASES", "disease" },
            { "DOMAINS", "domain" },
            { "EIDOLONS", "eidolon" },
            { "EQUIPMENT", "equipment" },
            { "EXPLORATION.ACTIVITIES", "activity" },
            { "FEATS", "feat" },
            { "FEATS.DEVIANT", "deviant-ability-classification" },
            { "FAMILIAR.ABILITIES", "familiar-ability" },
            { "FAMILIARS.SPECIFIC", "familiar-specific" },
            { "GEAR", "equipment" },
            { "GEAR.ARMOR", "armor" },
            { "GEAR.SHIELDS", "shield" },
            { "GEAR.WEAPONS", "weapon" },
            { "HAZARDS", "hazard" },
            { "HAZARD.WEATHER", "weather-hazard" },
            { "HAZARDS.WEATHER", "weather-hazard" },
            { "HERITAGES", "heritage" },
            { "KINGDOM.EVENTS", "kingdom-event" },
            { "KINGDOM.STRUCTURES", "kingdom-structure" },
            { "LANGUAGES", "language" },
            { "MONSTERS", "creature" },
            { "MON.FAMILY", "creature-family" },
            { "NPC", "creature" },
            { "NPCS", "creature" },
            { "PLANES", "plane" },
            { "RITUALS", "ritual" },
            { "RELIC.GIFTS", "relic" },
            { "RULES", "rules" },
            { "SHIELDS", "shield" },
            { "SIEGE.WEAPONS", "siege-weapon" },
            { "SKILLS", "skill" },
            { "SOURCES", "source" },
            { "SPELLS", "spell" },
            { "UMR", "creature-ability" },
            { "TRADITIONS", "tradition" },
            { "TRAITS", "trait" },
            { "TREASURE", "equipment" },
            { "VEHICLES", "vehicle" },
            { "WARFARE.TACTICS", "warfare-tactic" },
            { "WEAPONS", "weapon" },
            { "WEAPON.GROUPS", "weapon-group" },
        };

        return typeTranslations.GetValueOrDefault(type, type);
    }

    protected List<DbRow> FetchRows(Query query)
    {
        var compiler = new SqlServerCompiler();
        var compiledQuery = compiler.Compile(query);

        return FetchRows(compiledQuery.Sql, compiledQuery.NamedBindings);
    }

    protected List<DbRow> FetchRows(string query, Dictionary<string, object>? parameters = null)
    {
        var command = new SqlCommand(query, connection);

        if (parameters != null)
        {
            parameters.ToList().ForEach(kv => command.Parameters.AddWithValue(kv.Key, kv.Value));
        }

        command.Connection.Open();

        SqlDataReader reader = command.ExecuteReader();
        IReadOnlyCollection<DbColumn> columns = reader.GetColumnSchema();
        var rows = new List<DbRow>();

        while (reader.Read())
        {
            var row = new DbRow();

            foreach (DbColumn? column in columns)
            {
                if (column.ColumnOrdinal == null || column.DataType == null)
                {
                    continue;
                }

                switch (column.DataType.Name)
                {
                    case "Boolean":
                        if (reader.IsDBNull(column.ColumnOrdinal ?? 0))
                        {
                            row.Bools.Add(column.ColumnName, null);
                        }
                        else
                        {
                            row.Bools.Add(column.ColumnName, reader.GetBoolean(column.ColumnOrdinal ?? 0));
                        }

                        break;

                    case "DateTime":
                        if (reader.IsDBNull(column.ColumnOrdinal ?? 0))
                        {
                            row.DateTimes.Add(column.ColumnName, null);
                        }
                        else
                        {
                            row.DateTimes.Add(column.ColumnName, reader.GetDateTime(column.ColumnOrdinal ?? 0));
                        }

                        break;

                    case "Decimal":
                        if (reader.IsDBNull(column.ColumnOrdinal ?? 0))
                        {
                            row.Decimals.Add(column.ColumnName, null);
                        }
                        else
                        {
                            row.Decimals.Add(column.ColumnName, reader.GetDecimal(column.ColumnOrdinal ?? 0));
                        }

                        break;

                    case "Int32":
                        if (reader.IsDBNull(column.ColumnOrdinal ?? 0))
                        {
                            row.Ints.Add(column.ColumnName, null);
                        }
                        else
                        {
                            row.Ints.Add(column.ColumnName, reader.GetInt32(column.ColumnOrdinal ?? 0));
                        }

                        break;

                    case "String":
                        if (reader.IsDBNull(column.ColumnOrdinal ?? 0))
                        {
                            row.Strings.Add(column.ColumnName, null);
                        }
                        else
                        {
                            row.Strings.Add(column.ColumnName, reader.GetString(column.ColumnOrdinal ?? 0));
                        }

                        break;

                    default:
                        throw new NotSupportedException();
                }
            }

            rows.Add(row);
        }

        command.Connection.Close();

        return rows;
    }
}
