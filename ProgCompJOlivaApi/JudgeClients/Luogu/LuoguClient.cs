namespace ProgCompJOlivaApi.JudgeClients.Luogu;

public class Luogu : IJudgeClient
{
    public string JudgeName => "Luogu";

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