namespace AccessRequestHub.Api.Data.Entities;

public class Application
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int OwnerUserId { get; set; }
    public User? OwnerUser { get; set; }
}
