namespace ProgCompJOlivaApi.JudgeClients.AtcoderClient;

public class AtcoderClient : IJudgeClient
{
    public string JudgeName => "Atcoder";

    public async Task ConnectAsync()
    {
        return;
    }

    public async Task StartAsync()
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

        var entries = await AtcoderRankingScraper.GetUserRankingEntries();

        foreach (var entry in entries)
        {
            if (ret.TryGetValue(entry.Handle, out _))
            {
                ret[entry.Handle] = entry.CurrentRating;
            }
        }

        return ret;
    }
}
