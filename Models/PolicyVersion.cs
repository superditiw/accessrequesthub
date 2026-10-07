using System;

namespace AccessRequestHub.Models;

public class PolicyVersion
{
    public required string Code { get; set; }
    public DateTime EffectiveFrom { get; set; }
}
