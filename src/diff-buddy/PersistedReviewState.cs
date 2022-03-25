namespace diff_buddy;

public class PersistedReviewState
{
    public string FromBranch { get; set; }
    public string ToBranch { get; set; }
    public ReviewStateItemData[] Items { get; set; }
    public string LastFile { get; set; }
    public int Offset { get; set; }
    public int Limit { get; set; }
    public string[] IgnoreFiles { get; set; }
    public string Repo { get; set; }
}