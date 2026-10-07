using System;

namespace AccessRequestHub.Models;

public class Application
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public Guid SystemOwnerId { get; set; }
    
    public User? SystemOwner { get; set; }
}
