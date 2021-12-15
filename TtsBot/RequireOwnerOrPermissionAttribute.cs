using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;

namespace TtsBot;

public class RequireOwnerOrPermissionAttribute : CheckBaseAttribute
{
    public RequireOwnerOrPermissionAttribute(Permissions permissions) {
        Permissions = permissions;
    }
    public Permissions Permissions { get; }
    public override async Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help) {
        if (ctx.Guild == null!)
            return false;
        DiscordMember member = ctx.Member;
        if (member == null!)
            return false;
        if ((long)member.Id == (long)ctx.Guild.OwnerId)
            return true;
        Permissions permissions = ctx.Channel.PermissionsFor(member);
        if ((permissions & Permissions.Administrator) != Permissions.None)
            return true;
        if ((permissions & Permissions) == Permissions) return true;
        DiscordApplication currentApplication = ctx.Client.CurrentApplication;
        DiscordUser currentUser = ctx.Client.CurrentUser;
        return !(currentApplication != null!)
            ? (long)ctx.User.Id == (long)currentUser.Id
            : currentApplication.Owners.Any<DiscordUser>(x => (long)x.Id == (long)ctx.User.Id);

    }
}