using Dto;
using FirebaseAdmin.Auth;
using Model;

namespace Service;

public class UtilService(
    UserService userService,
    EventService eventService,
    CommunityService communityService
)
{
    readonly UserService userService = userService;
    readonly EventService eventService = eventService;
    readonly CommunityService communityService = communityService;

    public async Task InitDb()
    {
        List<string> mails = ["prova", "prova1", "prova2", "prova3"];

        List<User> users = await CreateUsersAsync(mails);
        List<Profile> profiles = users.Select(user => user.MainProfile!).ToList();

        GenerateCommunities(profiles);

        GenerateEvents(profiles);
    }

    #region initDb
    private void GenerateEvents(List<Profile> profiles)
    {
        List<Tuple<EventDto, Profile>> events = GenerateEventDtos(profiles);
        events.Select(e => eventService.Create(e.Item1, e.Item2)).ToList();
    }

    private static List<Tuple<EventDto, Profile>> GenerateEventDtos(List<Profile> profiles)
    {
        List<Tuple<EventDto, Profile>> result = [];
        DateTime now = DateTime.UtcNow;
        result.Add(
            new Tuple<EventDto, Profile>(
                new EventDto
                {
                    Title = "Evento prova",
                    StartTime = now,
                    EndTime = now.AddHours(1),
                },
                profiles[0]
            )
        );

        return result;
    }

    private void GenerateCommunities(List<Profile> profiles)
    {
        List<Tuple<CreateCommunityDto, Profile>> normalPairs =
            GeneratePersonalAndSingleGroupCommunities(profiles);
        List<Tuple<CreateCommunityDto, Profile>> multiplePairs = GenerateMultipleGroupCommunities(
            profiles
        );

        List<Community> normalCommunities = normalPairs
            .Select(pair => communityService.Create(pair.Item1, pair.Item2))
            .ToList();
        List<Community> multipleCommunities = multiplePairs
            .Select(pair => communityService.Create(pair.Item1, pair.Item2))
            .ToList();

        try
        {
            multipleCommunities.Select(communityService.MakeMultiGroup).ToList();

            GroupDto group = new()
            {
                Name = "Secondary Group",
                GeneralForCommunity = false,
                ProfileHashes = [profiles[0].Hash, profiles[1].Hash, profiles[3].Hash],
            };

            communityService.CreateAndAddGroup(multipleCommunities[1], group, profiles[0]);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error initializing database entities.", ex);
        }
    }

    private async Task<List<User>> CreateUsersAsync(ICollection<string> mails)
    {
        List<User> users = [];
        foreach (var mail in mails)
        {
            users.Add(await CreateNewUserAsync(mail + "@mail.com", "password"));
        }
        return users;
    }

    private async Task<User> CreateNewUserAsync(string mail, string password)
    {
        try
        {
            string uid = await CreateOrRetrieveFirebaseUserUidWithEmailAndPassword(mail, password);
            return await userService.GetOrCreateAsync(uid);
        }
        catch (Exception e)
        {
            throw new Exception("Error with user creation: " + e.Message);
        }
    }

    private static async Task<string> CreateOrRetrieveFirebaseUserUidWithEmailAndPassword(
        string email,
        string password
    )
    {
        try
        {
            UserRecordArgs userRecordArgs = new()
            {
                Email = email,
                EmailVerified = false,
                Password = password,
                DisplayName = "WydUser",
                Disabled = false,
            };
            UserRecord userRecord = await AuthenticationService.CreateAccountAsync(userRecordArgs);
            return userRecord.Uid;
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("EMAIL_EXISTS"))
            {
                UserRecord userRecord = await AuthenticationService.RetrieveAccountFromMail(email);
                return userRecord.Uid;
            }
            else
                throw new Exception("Error creating Firebase user: " + ex.Message);
        }
    }

    private static List<
        Tuple<CreateCommunityDto, Profile>
    > GeneratePersonalAndSingleGroupCommunities(List<Profile> profiles)
    {
        List<Tuple<CreateCommunityDto, Profile>> pairs = [];

        // Personal Community

        pairs.Add(
            new Tuple<CreateCommunityDto, Profile>(
                new CreateCommunityDto()
                {
                    Id = 0,
                    Name = "Personal Community 1 2",
                    Type = CommunityType.Personal,
                    Profiles = [profiles[1].Id],
                },
                profiles[0]
            )
        );

        pairs.Add(
            new Tuple<CreateCommunityDto, Profile>(
                new CreateCommunityDto()
                {
                    Id = 0,
                    Name = "Personal Community 1 3",
                    Type = CommunityType.Personal,
                    Profiles = [profiles[2].Id],
                },
                profiles[0]
            )
        );

        pairs.Add(
            new Tuple<CreateCommunityDto, Profile>(
                new CreateCommunityDto()
                {
                    Id = 0,
                    Name = "Personal Community 1 4",
                    Type = CommunityType.Personal,
                    Profiles = [profiles[3].Id],
                },
                profiles[0]
            )
        );

        pairs.Add(
            new Tuple<CreateCommunityDto, Profile>(
                new CreateCommunityDto()
                {
                    Id = 0,
                    Name = "Personal Community 2 4",
                    Type = CommunityType.Personal,
                    Profiles = [profiles[1].Id],
                },
                profiles[3]
            )
        );

        pairs.Add(
            new Tuple<CreateCommunityDto, Profile>(
                new CreateCommunityDto()
                {
                    Id = 0,
                    Name = "Personal Community 3 4",
                    Type = CommunityType.Personal,
                    Profiles = [profiles[2].Id],
                },
                profiles[3]
            )
        );

        // Single Group Community
        pairs.Add(
            new Tuple<CreateCommunityDto, Profile>(
                new CreateCommunityDto()
                {
                    Id = 0,
                    Name = "Single Group 1_2",
                    Type = CommunityType.SingleGroup,
                    Profiles = [profiles[1].Id],
                },
                profiles[0]
            )
        );

        pairs.Add(
            new Tuple<CreateCommunityDto, Profile>(
                new CreateCommunityDto()
                {
                    Id = 0,
                    Name = "Single Group 1_2_3_4",
                    Type = CommunityType.SingleGroup,
                    Profiles = [profiles[1].Id, profiles[2].Id, profiles[3].Id],
                },
                profiles[0]
            )
        );

        return pairs;
    }

    private static List<Tuple<CreateCommunityDto, Profile>> GenerateMultipleGroupCommunities(
        List<Profile> profiles
    )
    {
        List<Tuple<CreateCommunityDto, Profile>> pairs = [];

        // General Community
        pairs.Add(
            new Tuple<CreateCommunityDto, Profile>(
                new CreateCommunityDto()
                {
                    Id = 0,
                    Name = "General Community",
                    Type = CommunityType.SingleGroup,
                    Profiles = [profiles[1].Id, profiles[2].Id],
                },
                profiles[0]
            )
        );

        pairs.Add(
            new Tuple<CreateCommunityDto, Profile>(
                new CreateCommunityDto()
                {
                    Id = 0,
                    Name = "General Community 1",
                    Type = CommunityType.SingleGroup,
                    Profiles = [profiles[1].Id, profiles[2].Id, profiles[3].Id],
                },
                profiles[0]
            )
        );

        return pairs;
    }

    #endregion
}
