namespace AccessRequestHub.Api.Data.Entities;

public class User
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public required string Name { get; set; }
    public int? ManagerId { get; set; }
    public User? Manager { get; set; }
    public bool IsAuditor { get; set; }
}
