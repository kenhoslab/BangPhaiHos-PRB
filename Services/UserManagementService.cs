using HealthCoverage.Data;
using HealthCoverage.Models.db;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HealthCoverage.Services;

public class UserManagementService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public static readonly string[] AllMenuKeys =
        { "dashboard", "report", "statistics", "import" };

    public UserManagementService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _dbFactory    = dbFactory;
        _userManager  = userManager;
        _roleManager  = roleManager;
    }

    // ── User list ────────────────────────────────────────────────────────────

    public async Task<List<UserRow>> GetUsersAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var users = await _userManager.Users.OrderBy(u => u.UserName).ToListAsync();

        var permissions = await db.MenuPermissions
            .ToListAsync();

        var permDict = permissions
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.MenuKey).ToHashSet());

        var rows = new List<UserRow>();
        foreach (var u in users)
        {
            var roles  = await _userManager.GetRolesAsync(u);
            var isAdmin = roles.Contains("Admin");
            rows.Add(new UserRow
            {
                Id         = u.Id,
                UserName   = u.UserName ?? "",
                Email      = u.Email ?? "",
                IsAdmin    = isAdmin,
                MenuKeys   = isAdmin
                    ? new HashSet<string>(AllMenuKeys)   // admin sees everything
                    : permDict.TryGetValue(u.Id, out var ks) ? ks : new HashSet<string>(),
            });
        }
        return rows;
    }

    // ── Toggle Admin role ────────────────────────────────────────────────────

    public async Task<(bool ok, string error)> SetAdminAsync(string userId, bool makeAdmin)
    {
        await EnsureRolesAsync();
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return (false, "ไม่พบผู้ใช้");

        IdentityResult result;
        if (makeAdmin)
            result = await _userManager.AddToRoleAsync(user, "Admin");
        else
            result = await _userManager.RemoveFromRoleAsync(user, "Admin");

        return result.Succeeded
            ? (true, "")
            : (false, string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    // ── Update menu permissions ──────────────────────────────────────────────

    public async Task<(bool ok, string error)> SetMenuPermissionsAsync(
        string userId, IEnumerable<string> menuKeys)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        // Remove all existing permissions for this user
        var existing = await db.MenuPermissions
            .Where(p => p.UserId == userId)
            .ToListAsync();
        db.MenuPermissions.RemoveRange(existing);

        // Add new ones
        foreach (var key in menuKeys)
        {
            db.MenuPermissions.Add(new MenuPermission
            {
                UserId  = userId,
                MenuKey = key,
            });
        }

        await db.SaveChangesAsync();
        return (true, "");
    }

    // ── Create user ──────────────────────────────────────────────────────────

    public async Task<(bool ok, string error)> CreateUserAsync(
        string userName, string email, string password)
    {
        var user = new IdentityUser
        {
            UserName = userName,
            Email    = email,
        };
        var result = await _userManager.CreateAsync(user, password);
        return result.Succeeded
            ? (true, "")
            : (false, string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    // ── Delete user ──────────────────────────────────────────────────────────

    public async Task<(bool ok, string error)> DeleteUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return (false, "ไม่พบผู้ใช้");

        // Remove menu permissions first
        await using var db = await _dbFactory.CreateDbContextAsync();
        var perms = await db.MenuPermissions.Where(p => p.UserId == userId).ToListAsync();
        db.MenuPermissions.RemoveRange(perms);
        await db.SaveChangesAsync();

        var result = await _userManager.DeleteAsync(user);
        return result.Succeeded
            ? (true, "")
            : (false, string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    // ── Reset password ───────────────────────────────────────────────────────

    public async Task<(bool ok, string error)> ResetPasswordAsync(string userId, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return (false, "ไม่พบผู้ใช้");

        var token  = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        return result.Succeeded
            ? (true, "")
            : (false, string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task EnsureRolesAsync()
    {
        if (!await _roleManager.RoleExistsAsync("Admin"))
            await _roleManager.CreateAsync(new IdentityRole("Admin"));
        if (!await _roleManager.RoleExistsAsync("Staff"))
            await _roleManager.CreateAsync(new IdentityRole("Staff"));
    }
}

public class UserRow
{
    public string Id        { get; set; } = "";
    public string UserName  { get; set; } = "";
    public string Email     { get; set; } = "";
    public bool   IsAdmin   { get; set; }
    public HashSet<string> MenuKeys { get; set; } = new();
}
