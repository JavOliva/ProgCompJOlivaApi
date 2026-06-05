namespace ProgCompJOlivaApi.JudgeClients.CodeChef;

public class CodeChefClient : IJudgeClient
{
    public string JudgeName => "CodeChef";

    public async Task ConnectAsync()
    {
        return;
    }

    public async Task<Dictionary<string, int>> GetUsersRatings(IEnumerable<string> handles, CancellationToken ct = default)
    {
        Dictionary<string, int> ret = [];
        foreach (var handle in handles)
        {
            ret.TryAdd(handle, 0);
        }

        return ret;
    }

    public async Task StartAsync()
    {
        return;
    }
}