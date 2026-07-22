using ProgCompJOlivaApi.Models;
using ProgCompJOlivaApi.Services;
using ProgCompJOlivaApi.Utility;

namespace ProgCompJOlivaApi.Data;

public static class DbDevSeeder
{
    /// <summary>
    /// Incremental seeder: runs on every startup and only inserts what is missing —
    /// organizations matched by ShortName, users matched by Nickname. Adding a new user
    /// below makes it appear on the next run without wiping the database.
    /// </summary>
    public static async Task SeedAsync(AppDbContext context, PasswordService passwordService, IWebHostEnvironment env)
    {
        // Copy new SeedData assets (logos, standings sources) into wwwroot without
        // overwriting files that already exist there.
        var source = Path.Combine(AppContext.BaseDirectory, "SeedData");
        FileSystem.CopyDirectory(source, env.WebRootPath, overwrite: false);

        // --- Organizations ---

        List<Organization> seedOrganizations =
        [
            new()
            {
                Name = "Universidad de Chile",
                ShortName = "UChile",
                LogoUrl = "/organizations/logos/UChile.png"
            },
            new()
            {
                Name = "Universidad Tecnica Federico Santa Maria",
                ShortName = "UTFSM",
                LogoUrl = "/organizations/logos/UTFSM.png"
            },
            new()
            {
                Name = "Pontificia Universidad Catolica de Chile",
                ShortName = "PUC",
                LogoUrl = "/organizations/logos/PUC.png"
            },
            new()
            {
                Name = "Universidad de Concepcion",
                ShortName = "UDEC",
                LogoUrl = "/organizations/logos/UDEC.png"
            },
            new()
            {
                Name = "Olimpiada Chilena de Informática",
                ShortName = "OCI",
                LogoUrl = "/organizations/logos/OCI.png"
            },
            new()
            {
                Name = "Universidad Católica del Norte",
                ShortName = "UCN",
                LogoUrl = "/organizations/logos/UCN.png"
            },
        ];

        var existingOrgShortNames = context.Organizations.Select(o => o.ShortName).ToHashSet();
        foreach (var org in seedOrganizations.Where(o => !existingOrgShortNames.Contains(o.ShortName)))
        {
            context.Organizations.Add(org);
        }

        await context.SaveChangesAsync();

        var organizations = context.Organizations.ToDictionary(o => o.ShortName);

        // --- Users ---

        var joliva = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "JOliva",
            IsActive = false,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = false,
            Email = "jav.oliva.silva@gmail.com",
            Names = "Javier Ignacio",
            Surnames = "Oliva Silva",
            CodeforcesHandle = "JOliva",
            AtcoderHandle = "JOliva",
            CsesHandle = "javoliva",
            CsesId = "74066",
            LuoguHandle = "JOliva",
            CodeChefHandle = "javolivasilva",
            LeetCodeHandle = "JavOliva",
        };

        var dmitri = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "Dmitri",
            IsActive = false,
            FemTeamEligible = false,
            IsCompetitiveProgrammingActive = true,
            Email = "",
            Names = "Dmitri",
            Surnames = "Ramirez",
            CodeforcesHandle = "svandich",
            AtcoderHandle = "svandich",
        };

        var yhatoh = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "MrYhatoh",
            IsActive = false,
            FemTeamEligible = false,
            IsCompetitiveProgrammingActive = true,
            Email = "gabriel.carmona@sansano.usm.cl",
            Names = "Gabriel",
            Surnames = "Carmona",
            CodeforcesHandle = "MrYhatoh",
            AtcoderHandle = "MrYhatoh",
            CsesHandle = "MrYhatoh",
            CsesId = "83810"
        };

        var panda = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "storrealbac",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = false,
            Email = "sebastian.torrealba@usm.cl",
            Names = "Sebastian",
            Surnames = "Torrealba",
            CodeforcesHandle = "storrealbac",
            AtcoderHandle = "storrealbac",
            CsesHandle = "sebaxelpanda",
            CsesId = "49521"
        };

        var charleslakes = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "CharlesLakes",
            IsActive = false,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = false,
            Email = "carlos.lagosc@usm.cl",
            Names = "Carlos",
            Surnames = "Lagos",
            CodeforcesHandle = "CharlesLakes",
            AtcoderHandle = "CharlesLakes",
            CsesHandle = "CharlesLakes",
            CsesId = "138667"
        };

        var abner = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "abner_vidal",
            IsActive = true,
            FemTeamEligible = false,
            IsCompetitiveProgrammingActive = true,
            Email = "noc@noc.com",
            Names = "Abner",
            Surnames = "Vidal",
            CodeforcesHandle = "abner_vidal",
            AtcoderHandle = "abner_vidal",
            CsesHandle = "vidal.abner",
            CsesId = "157346"
        };

        var cata = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "Xazshy",
            IsActive = true,
            FemTeamEligible = true,
            IsCompetitiveProgrammingActive = true,
            Email = "",
            Names = "Catalina",
            Surnames = "Diaz",
            CodeforcesHandle = "Xazshy",
            AtcoderHandle = "Xazshy",
            CsesHandle = "Xazshy",
            CsesId = "270687"
        };

        var eva = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "evwng",
            IsActive = true,
            FemTeamEligible = true,
            IsCompetitiveProgrammingActive = true,
            Email = "",
            Names = "Eva",
            Surnames = "Wang",
            CodeforcesHandle = "evwng",
            AtcoderHandle = "",
            CsesHandle = "evwng",
            CsesId = "270888"
        };

        var dariasc = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "dariasc",
            IsActive = false,
            FemTeamEligible = false,
            IsCompetitiveProgrammingActive = true,
            Email = "-",
            Names = "Diego",
            Surnames = "Arias",
            CodeforcesHandle = "dariasc",
            AtcoderHandle = "dariasc",
            CsesHandle = "dariasc",
            CsesId = "58188"
        };

        var m1tu = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "M1tu",
            IsActive = false,
            FemTeamEligible = false,
            IsCompetitiveProgrammingActive = true,
            Email = "-",
            Names = "Gabriel",
            Surnames = "Carmona",
            CodeforcesHandle = "m1tu",
            AtcoderHandle = "m1tu",
            CsesHandle = "m1tu",
            CsesId = "114575"
        };

        var martinrt = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "MartinRT",
            IsActive = true,
            FemTeamEligible = false,
            IsCompetitiveProgrammingActive = true,
            Email = "-",
            Names = "Martin",
            Surnames = "Ruiz-Tagle",
            CodeforcesHandle = "MartinRT",
            AtcoderHandle = "MartinRT",
            CsesHandle = "MartinRT",
            CsesId = "358140"
        };

        var vi = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "vivivi",
            IsActive = true,
            FemTeamEligible = false,
            IsCompetitiveProgrammingActive = true,
            Email = "-",
            Names = "Vicente",
            Surnames = "Villarroel",
            CodeforcesHandle = "vivivi",
            AtcoderHandle = "vivivi",
            CsesHandle = "",
            CsesId = ""
        };

        var mapache = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "MapacheTactico",
            IsActive = true,
            FemTeamEligible = false,
            IsCompetitiveProgrammingActive = true,
            Email = "-",
            Names = "Alonso",
            Surnames = "Nunez",
            CodeforcesHandle = "MapacheTactico",
            AtcoderHandle = "MapacheTactico",
            CsesHandle = "",
            CsesId = ""
        };

        var junaeb = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "elJunaeb",
            IsActive = true,
            FemTeamEligible = false,
            IsCompetitiveProgrammingActive = true,
            Email = "-",
            Names = "Javier",
            Surnames = "Calfuala",
            CodeforcesHandle = "elJunaeb",
            AtcoderHandle = "elJunaeb",
            CsesHandle = "jajajajavier",
            CsesId = "133383"
        };

        var vlxn = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "Vlxn",
            IsActive = true,
            FemTeamEligible = false,
            IsCompetitiveProgrammingActive = true,
            Email = "",
            Names = "Alejandro",
            Surnames = "Molina",
            CodeforcesHandle = "Vlxn",
            AtcoderHandle = "Vlxn",
            CsesHandle = "",
            CsesId = ""
        };

        var amandis = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "niainamandis",
            IsActive = true,
            FemTeamEligible = true,
            IsCompetitiveProgrammingActive = true,
            Email = "-",
            Names = "Amanda",
            Surnames = "Nunez",
            CodeforcesHandle = "niainamandis",
            AtcoderHandle = "niainamandis",
            CsesHandle = "niainamandis",
            CsesId = "318797"
        };

        var metayer = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "ant_metayer",
            IsActive = false,
            FemTeamEligible = false,
            IsCompetitiveProgrammingActive = false,
            Email = "-",
            Names = "Antoine",
            Surnames = "Metayer",
            CodeforcesHandle = "ant_metayer",
            AtcoderHandle = "",
            CsesHandle = "",
            CsesId = ""
        };

        var pauwu = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "angeldeazza",
            IsActive = true,
            FemTeamEligible = true,
            IsCompetitiveProgrammingActive = true,
            Email = "-",
            Names = "Paula",
            Surnames = "Carrion",
            CodeforcesHandle = "pauwu",
            AtcoderHandle = "",
            CsesHandle = "",
            CsesId = ""
        };

        var ale = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "AlePatata92",
            IsActive = true,
            FemTeamEligible = true,
            IsCompetitiveProgrammingActive = true,
            Email = "-",
            Names = "Alejandra",
            Surnames = "Campos",
            CodeforcesHandle = "AlePatata92",
            AtcoderHandle = "AlePatata92",
            CsesHandle = "AlePatata92",
            CsesId = "181017"
        };

        var emeoww = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "emeoww",
            IsActive = true,
            FemTeamEligible = true,
            IsCompetitiveProgrammingActive = true,
            Email = "-",
            Names = "Emilia",
            Surnames = "Lenam",
            CodeforcesHandle = "emeoww",
            AtcoderHandle = "emeoww",
            CsesHandle = "emeoww",
            CsesId = "371187"
        };

        var egoex = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "EgoExNihilo",
            IsActive = true,
            FemTeamEligible = false,
            IsCompetitiveProgrammingActive = true,
            Email = "-",
            Names = "Eduardo",
            Surnames = "Vergara",
            CodeforcesHandle = "EgoExNihilo",
            AtcoderHandle = "EgoExNihilo",
            CsesHandle = "",
            CsesId = ""
        };

        var cot3 = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "Cot3",
            IsActive = true,
            FemTeamEligible = true,
            IsCompetitiveProgrammingActive = true,
            Email = "-",
            Names = "Maria Jose",
            Surnames = "Zambrano",
            CodeforcesHandle = "cot3",
            AtcoderHandle = "cot3",
            CsesHandle = "",
            CsesId = ""
        };

        var deragonie = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "deragonie",
            IsActive = true,
            FemTeamEligible = true,
            IsCompetitiveProgrammingActive = true,
            Email = "-",
            Names = "Daniela",
            Surnames = "Espinoza",
            CodeforcesHandle = "deragonie",
            AtcoderHandle = "deragonie",
            CsesHandle = "deragonie",
            CsesId = "329283"
        };

        var maca = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "maca",
            IsActive = true,
            FemTeamEligible = true,
            IsCompetitiveProgrammingActive = true,
            Email = "-",
            Names = "Macarena",
            Surnames = "Rivas",
            CodeforcesHandle = "macaa.a_",
            AtcoderHandle = "macaa",
            CsesHandle = "",
            CsesId = ""
        };

        var scarleth = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "Scarl3th",
            IsActive = true,
            FemTeamEligible = true,
            IsCompetitiveProgrammingActive = true,
            Email = "noc@noc.com",
            Names = "Scarleth",
            Surnames = "Bazaes",
            CodeforcesHandle = "Scarl3th",
            AtcoderHandle = "Scarl3th",
        };

        var azocar = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "ElNChou",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = false,
            Email = "",
            Names = "Javier",
            Surnames = "Azocar",
            CodeforcesHandle = "ElNChou",
            AtcoderHandle = "ElNChou",
            CsesHandle = "ElNChou",
            CsesId = "445208",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var rajevic = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "Distort",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = false,
            Email = "",
            Names = "Tomas",
            Surnames = "Rajevic",
            CodeforcesHandle = "ThisIsThisAndThatIsThat",
            AtcoderHandle = "Distort",
            CsesHandle = "distort",
            CsesId = "351957",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var fleqi = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "fleqi",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = false,
            Email = "",
            Names = "Felipe",
            Surnames = "Cabezas",
            CodeforcesHandle = "fleqi",
            AtcoderHandle = "fleqi",
            CsesHandle = "fleqi",
            CsesId = "418398",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var ciruela = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "Ciruela",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = false,
            Email = "",
            Names = "Felipe",
            Surnames = "Jara",
            CodeforcesHandle = "Ciruela",
            AtcoderHandle = "",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var lamp = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "HolaSoyLamp",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = false,
            Email = "",
            Names = "Luciano",
            Surnames = "Massa",
            CodeforcesHandle = "HolaSoyLamp",
            AtcoderHandle = "",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var ilopez = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "ilopez15",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = false,
            Email = "",
            Names = "Ignacio",
            Surnames = "Lopez",
            CodeforcesHandle = "ilopez15",
            AtcoderHandle = "ilopez15",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var martina = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "lu_0",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = true,
            Email = "",
            Names = "Martina",
            Surnames = "Lucero",
            CodeforcesHandle = "lu_0",
            AtcoderHandle = "",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var patricia = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "patrici4",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = true,
            Email = "",
            Names = "Patricia",
            Surnames = "vera",
            CodeforcesHandle = "patrici4",
            AtcoderHandle = "",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var salas = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "daridius",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = false,
            Email = "",
            Names = "Diego",
            Surnames = "Salas",
            CodeforcesHandle = "daridius",
            AtcoderHandle = "",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var blaz = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "blaz",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = false,
            Email = "",
            Names = "Blaz",
            Surnames = "Korecic",
            CodeforcesHandle = "blaz",
            AtcoderHandle = "Blaz",
            CsesHandle = "bkorecic",
            CsesId = "69586",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var baez = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "AngrySeal",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = false,
            Email = "",
            Names = "Daniel",
            Surnames = "Baez",
            CodeforcesHandle = "AngrySeal",
            AtcoderHandle = "AngrySeal",
            CsesHandle = "AngrySeal",
            CsesId = "107392",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var davicom = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "Davicom",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = false,
            Email = "",
            Names = "David",
            Surnames = "Ibanez",
            CodeforcesHandle = "Davicom",
            AtcoderHandle = "Davicom",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var curauma = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "curauma03",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = true,
            Email = "",
            Names = "Fernanda",
            Surnames = "Gutierrez",
            CodeforcesHandle = "curauma03",
            AtcoderHandle = "",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var lazyelekid = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "lazyelekid",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = false,
            Email = "",
            Names = "Francisco",
            Surnames = "Castro",
            CodeforcesHandle = "lazyelekid",
            AtcoderHandle = "",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var zertex = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "Zertex",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = false,
            Email = "",
            Names = "Gerard",
            Surnames = "Cathalifaud",
            CodeforcesHandle = "Zertex",
            AtcoderHandle = "",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var dante = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "Jayki",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = false,
            Email = "",
            Names = "Dante",
            Surnames = "Mardones",
            CodeforcesHandle = "Jayki",
            AtcoderHandle = "Jayki",
            CsesHandle = "Jayki",
            CsesId = "83700",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var maximo = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "vMaximo",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = false,
            Email = "",
            Names = "Maximo",
            Surnames = "Flores",
            CodeforcesHandle = "vMaximo",
            AtcoderHandle = "",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var marcelo = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "Marceantasy",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = false,
            Email = "",
            Names = "Marcelo",
            Surnames = "Lemus",
            CodeforcesHandle = "Marceantasy",
            AtcoderHandle = "Marceantasy",
            CsesHandle = "Marceantasy",
            CsesId = "32010",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var enzo = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "Masterkrab",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = false,
            Email = "",
            Names = "Enzo",
            Surnames = "Vivallo",
            CodeforcesHandle = "Masterkrab",
            AtcoderHandle = "masterkrab",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var nova = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "Bors__",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = false,
            Email = "",
            Names = "Matias",
            Surnames = "Nova",
            CodeforcesHandle = "Bors__",
            AtcoderHandle = "",
            CsesHandle = "Bors",
            CsesId = "100688",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var rafoka = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "Rafoka",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = false,
            Email = "",
            Names = "Rafael",
            Surnames = "Baeza",
            CodeforcesHandle = "Rafoka",
            AtcoderHandle = "Rafoka",
            CsesHandle = "Rafoka",
            CsesId = "304324",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var poloyuyu = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "poloyuyu",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = false,
            Email = "",
            Names = "Jaime",
            Surnames = "Inostroza",
            CodeforcesHandle = "poloyuyu",
            AtcoderHandle = "poloyuyu",
            CsesHandle = "poloyuyu",
            CsesId = "176707",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var morrison = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "lucas.morrison",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = false,
            Email = "",
            Names = "Lucas",
            Surnames = "Morrison",
            CodeforcesHandle = "lucas.morrison",
            AtcoderHandle = "",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var cmauch = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "CMauch",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = false,
            Email = "",
            Names = "Christian",
            Surnames = "Mauch",
            CodeforcesHandle = "CMauch",
            AtcoderHandle = "CMauch",
            CsesHandle = "CMauch",
            CsesId = "424919",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var incognito = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "IncognitoDR",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = false,
            Email = "",
            Names = "Daniel",
            Surnames = "Quispe",
            CodeforcesHandle = "IncognitoDR",
            AtcoderHandle = "IncognitoDR",
            CsesHandle = "IncognitoDR",
            CsesId = "295853",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var calvito = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "calvito",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = false,
            Email = "",
            Names = "Martin",
            Surnames = "Caballero",
            CodeforcesHandle = "calvito",
            AtcoderHandle = "",
            CsesHandle = "calvito",
            CsesId = "332144",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var bastianj = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "Tocomplemaxxing",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = false,
            Email = "",
            Names = "Bastian",
            Surnames = "Jimenez",
            CodeforcesHandle = "BastianJimenez",
            AtcoderHandle = "",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var bastianw = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "bastifwp",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = false,
            Email = "",
            Names = "Bastian",
            Surnames = "Wohlwend",
            CodeforcesHandle = "bastifwp",
            AtcoderHandle = "bastifwp",
            CsesHandle = "bastifwp",
            CsesId = "367405",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var laxaro = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "Laxaro",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = false,
            Email = "",
            Names = "Alvaro",
            Surnames = "Rojas",
            CodeforcesHandle = "Laxaro",
            AtcoderHandle = "Laxaro",
            CsesHandle = "Laxaro",
            CsesId = "191002",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var johan = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "Johannsonmanson",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = false,
            Email = "",
            Names = "Johann",
            Surnames = "Vasquez",
            CodeforcesHandle = "Johannsonmanson",
            AtcoderHandle = "Johannsonmanson",
            CsesHandle = "Johannsonmanson",
            CsesId = "197684",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var cromane = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "cromane",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = false,
            Email = "",
            Names = "Ernesto",
            Surnames = "Barria",
            CodeforcesHandle = "cromane",
            AtcoderHandle = "cromane",
            CsesHandle = "cromane",
            CsesId = "197626",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var inettle = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "inettle",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = true,
            Email = "",
            Names = "Ignacia",
            Surnames = "Nettle",
            CodeforcesHandle = "inettle",
            AtcoderHandle = "",
            CsesHandle = "inettle",
            CsesId = "428771",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var ferniwispiwis = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "ferniwispiwis",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = true,
            Email = "",
            Names = "Fernanda",
            Surnames = "Castro",
            CodeforcesHandle = "ferniwispiwis",
            AtcoderHandle = "",
            CsesHandle = "ferniwispiwis",
            CsesId = "436957",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var aceituna = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "Aceituna",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = true,
            Email = "",
            Names = "Soffia",
            Surnames = "Romero",
            CodeforcesHandle = "Aceituna",
            AtcoderHandle = "",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var opheliamnda = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "ohpeliamnda",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = true,
            Email = "",
            Names = "Amanda",
            Surnames = "Leyton",
            CodeforcesHandle = "opheliamnda",
            AtcoderHandle = "opheliamnda",
            CsesHandle = "opheliamnda",
            CsesId = "351685",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var kote = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "KoteKote",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = true,
            Email = "",
            Names = "Maria Jose",
            Surnames = "Parra",
            CodeforcesHandle = "KoteKote",
            AtcoderHandle = "",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var sunrayito = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "sunrayito",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = true,
            Email = "",
            Names = "Javiera",
            Surnames = "Leon",
            CodeforcesHandle = "sunrayito",
            AtcoderHandle = "sunrayito",
            CsesHandle = "sunrayito",
            CsesId = "276861",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var ccserm = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "ccserm",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = true,
            Email = "",
            Names = "Claudia",
            Surnames = "Cser",
            CodeforcesHandle = "ccserm",
            AtcoderHandle = "ccserm",
            CsesHandle = "ccserm",
            CsesId = "154808",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var albfr = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "albfr",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = false,
            Email = "",
            Names = "Alberto",
            Surnames = "Ferrada",
            CodeforcesHandle = "albfr",
            AtcoderHandle = "albfr",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var burleque = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "Burleque",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = false,
            Email = "",
            Names = "Leonardo",
            Surnames = "Lovera",
            CodeforcesHandle = "Burleque",
            AtcoderHandle = "burleque",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var fpino = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "CopperQueen",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = true,
            Email = "",
            Names = "Felicia",
            Surnames = "Pino",
            CodeforcesHandle = "CopperQueen",
            AtcoderHandle = "",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var danino = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "danino__",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = true,
            Email = "",
            Names = "Daniela",
            Surnames = "Novoa",
            CodeforcesHandle = "danino__",
            AtcoderHandle = "",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var angie = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "Angie161",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = true,
            Email = "",
            Names = "Angie",
            Surnames = "Ramirez",
            CodeforcesHandle = "Angie161",
            AtcoderHandle = "Angie161",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var dai = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "Dai0w0",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = true,
            Email = "",
            Names = "Diana",
            Surnames = "Diaz",
            CodeforcesHandle = "Dai0w0",
            AtcoderHandle = "Dai0w0",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var danny = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "Danny_P010",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = true,
            Email = "",
            Names = "Daniela",
            Surnames = "Vogt",
            CodeforcesHandle = "Danny_P010",
            AtcoderHandle = "Danny_P010",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var rominap = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "romina.p0601",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = true,
            Email = "",
            Names = "Romina",
            Surnames = "Parra",
            CodeforcesHandle = "romina.p0601",
            AtcoderHandle = "",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var vjuri = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "vjuri",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = true,
            Email = "",
            Names = "Valentina",
            Surnames = "Juri",
            CodeforcesHandle = "vjuri",
            AtcoderHandle = "",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var balticami = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "balticami27",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = true,
            Email = "",
            Names = "Camila",
            Surnames = "Riquelme",
            CodeforcesHandle = "balticami27",
            AtcoderHandle = "balticami27",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var mimiimi = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "mimiimi",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = true,
            Email = "",
            Names = "Camila",
            Surnames = "Sanchez",
            CodeforcesHandle = "mimiimi",
            AtcoderHandle = "",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var aricarrasco = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "aricarrasco",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = true,
            Email = "",
            Names = "Amparo",
            Surnames = "Carrasco",
            CodeforcesHandle = "aricarrasco",
            AtcoderHandle = "aricarrasco",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var vixo = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "visho33",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = false,
            Email = "",
            Names = "Vicente",
            Surnames = "Opazo",
            CodeforcesHandle = "visho33",
            AtcoderHandle = "visho33",
            CsesHandle = "visho33",
            CsesId = "19918",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var naxo = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "MrNachoX",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = false,
            Email = "",
            Names = "Ignacio",
            Surnames = "Munoz",
            CodeforcesHandle = "MrNachoX",
            AtcoderHandle = "MrNachoX",
            CsesHandle = "MrNachoX",
            CsesId = "178165",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var andr = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "kovaxis",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = false,
            Email = "",
            Names = "Martin",
            Surnames = "Andrighetti",
            CodeforcesHandle = "kovaxis",
            AtcoderHandle = "kovaxis",
            CsesHandle = "kovaxis",
            CsesId = "145098",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var marinkovic = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "Marinkovic",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = false,
            Email = "",
            Names = "Javier",
            Surnames = "Marinkovic",
            CodeforcesHandle = "Marinkovic",
            AtcoderHandle = "Marinkovic",
            CsesHandle = "Marinkovic",
            CsesId = "74546",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var benjar = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "BenjaR",
            IsActive = false,
            IsCompetitiveProgrammingActive = false,
            FemTeamEligible = false,
            Email = "",
            Names = "Benjamin",
            Surnames = "Rubio",
            CodeforcesHandle = "BenjaR",
            AtcoderHandle = "benjar",
            CsesHandle = "BenjaR",
            CsesId = "166890",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var delorme = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "sdcc",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = true,
            Email = "",
            Names = "Sofia",
            Surnames = "Delorme",
            CodeforcesHandle = "sdcc",
            AtcoderHandle = "",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var ema = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "emoide",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = true,
            Email = "",
            Names = "Ema",
            Surnames = "Oliva",
            CodeforcesHandle = "emoide",
            AtcoderHandle = "emoide123",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var constanzaL = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "ConstanzaL",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = true,
            Email = "",
            Names = "Constanza",
            Surnames = "Lobos",
            CodeforcesHandle = "ConstanzaL",
            AtcoderHandle = "",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var danielaabdala = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "danielasaraay",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = true,
            Email = "",
            Names = "Daniela",
            Surnames = "Abdala",
            CodeforcesHandle = "danielasaraay",
            AtcoderHandle = "",
            CsesHandle = "",
            CsesId = "",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        var heuremi = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "HeuRemi",
            IsActive = true,
            IsCompetitiveProgrammingActive = true,
            FemTeamEligible = true,
            Email = "",
            Names = "Constanza",
            Surnames = "Vazquez",
            CodeforcesHandle = "HeuRemi",
            AtcoderHandle = "HeuRemi",
            CsesHandle = "HeuRemi",
            CsesId = "327214",
            LuoguHandle = "",
            CodeChefHandle = "",
            LeetCodeHandle = ""
        };

        // (user, organization ShortName, role)
        List<(User User, string Org, string Role)> seedUsers =
        [
            (joliva, "UChile", "Admin"),
            (dmitri, "UChile", "User"),
            (rajevic, "UChile", "User"),
            (azocar, "UChile", "User"),
            (yhatoh, "UTFSM", "Admin"),
            (abner, "UTFSM", "User"),
            (scarleth, "UTFSM", "User"),
            (panda, "UTFSM", "User"),
            (charleslakes, "UTFSM", "User"),
            (dariasc, "UChile", "User"),
            (m1tu, "UChile", "User"),
            (martinrt, "UChile", "User"),
            (vi, "UChile", "User"),
            (mapache, "UChile", "User"),
            (junaeb, "UChile", "User"),
            (vlxn, "UChile", "User"),
            (amandis, "UChile", "User"),
            (metayer, "UChile", "User"),
            (pauwu, "UChile", "User"),
            (ale, "UChile", "User"),
            (emeoww, "UChile", "User"),
            (egoex, "UChile", "User"),
            (cot3, "UChile", "User"),
            (deragonie, "UChile", "User"),
            (maca, "UChile", "User"),
            (danny, "UChile", "User"),
            (fleqi, "UChile", "User"),
            (ciruela, "UChile", "User"),
            (lamp, "UChile", "User"),
            (ilopez, "UChile", "User"),
            (martina, "UChile", "User"),
            (patricia, "UChile", "User"),
            (marinkovic, "UChile", "User"),
            (salas, "UChile", "User"),
            (blaz, "UChile", "User"),
            (davicom, "UChile", "User"),
            (curauma, "UChile", "User"),
            (baez, "UChile", "User"),
            (lazyelekid, "UChile", "User"),
            (zertex, "UChile", "User"),
            (maximo, "UChile", "User"),
            (dante, "UChile", "User"),
            (aricarrasco, "PUC", "User"),
            (marcelo, "PUC", "User"),
            (nova, "PUC", "User"),
            (enzo, "PUC", "User"),
            (eva, "UTFSM", "User"),
            (cata, "UTFSM", "User"),
            (rafoka, "UTFSM", "User"),
            (poloyuyu, "UTFSM", "User"),
            (morrison, "UTFSM", "User"),
            (cmauch, "UTFSM", "User"),
            (incognito, "UTFSM", "User"),
            (calvito, "UTFSM", "User"),
            (bastianj, "UTFSM", "User"),
            (bastianw, "UTFSM", "User"),
            (laxaro, "UTFSM", "User"),
            (aceituna, "UTFSM", "User"),
            (johan, "UTFSM", "User"),
            (cromane, "UTFSM", "User"),
            (inettle, "UTFSM", "User"),
            (ferniwispiwis, "UTFSM", "User"),
            (sunrayito, "PUC", "User"),
            (vjuri, "PUC", "User"),
            (kote, "PUC", "User"),
            (opheliamnda, "PUC", "User"),
            (ccserm, "UDEC", "User"),
            (albfr, "UDEC", "User"),
            (burleque, "UDEC", "User"),
            (fpino, "UDEC", "User"),
            (danino, "UDEC", "User"),
            (angie, "UDEC", "User"),
            (rominap, "PUC", "User"),
            (mimiimi, "PUC", "User"),
            (benjar, "PUC", "User"),
            (vixo, "PUC", "User"),
            (naxo, "PUC", "User"),
            (andr, "PUC", "User"),
            (balticami, "PUC", "User"),
            (dai, "UChile", "User"),
            (delorme, "OCI", "User"),
            (ema, "OCI", "User"),
            (constanzaL, "OCI", "User"),
            (danielaabdala, "OCI", "User"),
            (heuremi, "UCN", "User")
        ];

        var existingNicknames = context.Users.Select(u => u.Nickname).ToHashSet();

        foreach (var (user, orgShortName, role) in seedUsers)
        {
            if (existingNicknames.Contains(user.Nickname))
                continue;

            var org = organizations[orgShortName];
            user.OrganizationId = org.Id;
            user.Organization = org;
            user.PasswordHash = passwordService.HashPassword(user, "123456");

            context.Users.Add(user);
            context.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                User = user,
                RoleName = role
            });
        }

        await context.SaveChangesAsync();
    }
}
