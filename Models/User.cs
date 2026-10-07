using System;

namespace AccessRequestHub.Models;

public class User
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public UserRole Role { get; set; }
    public Guid? ManagerId { get; set; }
    
    public User? Manager { get; set; }
}
