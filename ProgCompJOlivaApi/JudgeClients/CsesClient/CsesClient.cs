namespace ProgCompJOlivaApi.JudgeClients.CsesClient;

public class CsesClient : IJudgeClient
{
    public string JudgeName => "Cses";

    public async Task ConnectAsync()
    {
        return;
    }

    public async Task<Dictionary<string, int>> GetUsersRatings(IEnumerable<string> handles, CancellationToken ct = default)
    {
        var ret = new Dictionary<string, int>();
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
