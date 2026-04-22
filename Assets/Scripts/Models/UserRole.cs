public enum UserRole
{
    User,
    Admin
}

public static class UserRoleParser
{
    public static UserRole ParseOrDefault(string roleValue)
    {
        if (string.IsNullOrWhiteSpace(roleValue))
        {
            return UserRole.User;
        }

        return roleValue.Trim().ToLowerInvariant() == "admin"
            ? UserRole.Admin
            : UserRole.User;
    }
}
