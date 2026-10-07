using AccessRequestHub.Models;

namespace AccessRequestHub.Services;

public class CurrentUserService
{
    public User? User { get; set; }
    public event System.Action? OnUserChanged;

    public void SetUser(User user)
    {
        User = user;
        OnUserChanged?.Invoke();
    }
}
