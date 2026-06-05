using ProgCompJOlivaApi.Models;
using ProgCompJOlivaApi.Services;
using ProgCompJOlivaApi.Utility;

namespace ProgCompJOlivaApi.Data;

public static class DbDevSeeder
{
    public static async Task SeedAsync(AppDbContext context, PasswordService passwordService, IWebHostEnvironment env)
    {
        if (context.Users.Any())
            return;

        var source = Path.Combine(AppContext.BaseDirectory, "SeedData");
        Console.WriteLine(source);
        var destination = env.WebRootPath;
        Console.WriteLine(destination);
        if (!Directory.Exists(source))
        {
            Console.WriteLine("Does not exist");
        }

        FileSystem.CopyDirectory(source, destination);

        var uchile = new Organization
        {
            Name = "Universidad de Chile",
            ShortName = "UChile",
            LogoUrl = "/organizations/logos/UChile.png"
        };

        var usm = new Organization
        {
            Name = "Universidad Tecnica Federico Santa Maria",
            ShortName = "UTFSM",
            LogoUrl = "/organizations/logos/UTFSM.png"
        };

        var puc = new Organization
        {
            Name = "Pontificia Universidad Catolica de Chile",
            ShortName = "PUC",
            LogoUrl = "/organizations/logos/PUC.png"
        };

        var udec = new Organization
        {
            Name = "Universidad de Concepcion",
            ShortName = "UDEC",
            LogoUrl = "/organizations/logos/UDEC.png"
        };

        List<Organization> organizations = [uchile, usm, puc, udec];
        foreach (var org in organizations)
        {
            context.Organizations.Add(org);
        }

        await context.SaveChangesAsync();

        var joliva = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "JOliva",
            IsActive = true,
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
            IsActive = true,
            FemTeamEligible = false,
            IsCompetitiveProgrammingActive = true,
            Email = "noc@noc.com",
            Names = "Dmitri",
            Surnames = "Dmitri",
            CodeforcesHandle = "svandich",
            AtcoderHandle = "svandich",
        };

        var yhatoh = new User
        {
            Id = Guid.NewGuid(),
            Nickname = "MrYhatoh",
            IsActive = true,
            FemTeamEligible = false,
            IsCompetitiveProgrammingActive = true,
            Email = "noc@noc.com",
            Names = "Gabriel",
            Surnames = "Carmona Tabja",
            CodeforcesHandle = "MrYhatoh",
            AtcoderHandle = "MrYhatoh",
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

        List<User> uchileUsers = [joliva, dmitri];

        List<User> pucUsers = [];

        List<User> usmUsers = [yhatoh, abner, scarleth];

        List<User> udecUsers = [];

        List<User> adminUsers = [joliva, yhatoh];

        List<User> normalUsers = [dmitri, abner, scarleth];

        List<User> allUsers = [joliva, dmitri, yhatoh, abner, scarleth];

        foreach (var user in uchileUsers)
        {
            user.OrganizationId = uchile.Id;
            user.Organization = uchile;
        }

        foreach (var user in pucUsers)
        {
            user.OrganizationId = puc.Id;
            user.Organization = puc;
        }

        foreach (var user in usmUsers)
        {
            user.OrganizationId = usm.Id;
            user.Organization = usm;
        }

        foreach (var user in udecUsers)
        {
            user.OrganizationId = udec.Id;
            user.Organization = udec;
        }

        foreach (var user in adminUsers)
        {
            context.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                User = user,
                RoleName = "Admin"
            });
        }

        foreach (var user in normalUsers)
        {
            context.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                User = user,
                RoleName = "User"
            });
        }

        foreach (var user in allUsers)
        {
            user.PasswordHash = passwordService.HashPassword(user, "123456");
            context.Users.Add(user);
        }

        await context.SaveChangesAsync();
    }

}
