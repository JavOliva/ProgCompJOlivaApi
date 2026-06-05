namespace ProgCompJOlivaApi.JudgeClients;

public interface IJudgeClient
{
    public string JudgeName { get; }

    public Task ConnectAsync();

    public Task StartAsync();

    public Task<Dictionary<string, int>> GetUsersRatings(IEnumerable<string> handles, CancellationToken ct = default);
}
