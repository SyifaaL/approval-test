namespace AccessRequestHub.Api.Data.Entities;

public class PolicyVersion
{
    public required string Code { get; set; }
    public DateTime EffectiveFrom { get; set; }
}
