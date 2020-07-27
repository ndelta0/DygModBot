using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using DygBot.Models;
using DygBot.Preconditions;
using DygBot.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static DygBot.Services.GitHubService.ConfigClass.ServerConfigClass.CountChannelClass;

namespace DygBot.Modules
{
    [Summary("Moderation commands")]
    [RequireUser(312223735505747968, Group = "Permission")]
    [RequireOwner(Group = "Permission")]
    [RequireManagementRole(Group = "Permission")]
    public class ModerationModule : InteractiveBase<SocketCommandContext>
    {
        private static readonly string[] _banGifUrls =
            {
                "https://media1.tenor.com/images/11baffb759ae7ca9d984153cf53a7768/tenor.gif?itemid=8540510",
                "https://media1.giphy.com/media/H99r2HtnYs492/200.gif",
                "https://thumbs.gfycat.com/PlayfulFittingCaribou-size_restricted.gif",
                "https://i.imgur.com/l5AFFhc.gif",
                "https://i.kym-cdn.com/photos/images/original/001/127/426/f46.gif"
            };

        private readonly GitHubService _git;
        private readonly InteractiveService _interactive;
        private readonly AppDbContext _dbContext;
        private readonly Random _random;

        public ModerationModule(GitHubService git, InteractiveService interactive, AppDbContext dbContext, Random random)
        {
            _git = git;
            _interactive = interactive;
            _dbContext = dbContext;
            _random = random;
        }

        [Command("prefix")]
        [Summary("Changes bot prefix")]
        public async Task PrefixAsync([Summary("New prefix")] string prefix)
        {
            using (Context.Channel.EnterTypingState())  // Show the "typing" notification
            {
                if (prefix == _git.Config.Servers[Context.Guild.Id.ToString()].Prefix)  // Compare new prefix with old prefix
                    await ReplyAsync($"Prefix is already set to **{prefix}**");
                else
                {
                    _git.Config.Servers[Context.Guild.Id.ToString()].Prefix = prefix;   // Set the prefix
                    await _git.UploadConfig();  // Upload config
                    await ReplyAsync($"Prefix set to {prefix}");    // Reply
                }
            }
        }

        [Command("roles")]
        [Summary("Pokazuje role użytkownika")]
        public async Task RolesAsync([Summary("Użytkownik")] SocketGuildUser user = null)
        {
            if (user == null)
            {
                if (Context.User is SocketGuildUser)
                {
                    user = Context.User as SocketGuildUser;
                }
            }

            var roles = user.Roles.SkipWhile(x => x.IsEveryone);

            var description = new StringBuilder().AppendJoin('\n', roles).ToString();

            var embed = new EmbedBuilder
            {
                Title = $"Role użytkownika {user.Username}#{user.Discriminator}",
                Description = description
            }.WithCurrentTimestamp().Build();

            await ReplyAsync(embed: embed);
        }

        [Command("config-reload", true)]
        [Summary("Zmusza bota do przeładowania konfiguracji")]
        public async Task ConfigReloadAsync()
        {
            await _git.DownloadConfig();
            await _git.UploadConfig();
            await _git.DownloadConfig();
            await ReplyAndDeleteAsync("Konfiguracja przeładowana", timeout: TimeSpan.FromSeconds(5));
        }

        [Command("ban")]
        [Summary("Banuje użytkownika")]
        public async Task BanAsync([Summary("Osoba do zbanowania")] IGuildUser user, [Summary("Powód bana")][Remainder] string reason = "")
        {
            using (Context.Channel.EnterTypingState())  // Show the "typing" notification
            {
                if (user is SocketGuildUser member)
                {
                    if (Context.User.Id != Context.Guild.OwnerId)
                    {
                        var roles = ((SocketGuildUser)Context.User).Roles.Select(x => x.Id.ToString());
                        bool canWarn = roles.Intersect(_git.Config.Servers[Context.Guild.Id.ToString()].ManagementRoles).Count() > 0;
                        canWarn = !(member.Roles.Select(x => x.Id.ToString()).Intersect(_git.Config.Servers[Context.Guild.Id.ToString()].ManagementRoles).Count() > 0);

                        if (!canWarn)
                        {
                            await ReplyAsync("Nie możesz upomnieć tej osoby");
                            return;
                        }
                    }

                    reason = string.IsNullOrWhiteSpace(reason) ? "Powód nie został podany" : reason;

                    var banEntry = new Ban
                    {
                        UserId = member.Id,
                        GuildId = Context.Guild.Id,
                        BanEnd = DateTime.MinValue,
                        Reason = reason,
                        WhoBanned = Context.User.Id
                    };

                    await _dbContext.Bans.AddAsync(banEntry);
                    await _dbContext.SaveChangesAsync();

                    var gifUrl = _banGifUrls[_random.Next(0, _banGifUrls.Length + 1)];

                    var embedBuilder = new EmbedBuilder()
                        .WithTitle("__Użytkownik został zbanowany__")
                        .WithColor(new Color(0xFF3500))
                        .WithDescription($"ID użytkownika: {member.Id}")
                        .WithAuthor(author =>
                        {
                            author
                                .WithName($"Ban | {member.Username}#{member.Discriminator}")
                                .WithIconUrl(member.GetAvatarUrl());
                        })
                        .WithThumbnailUrl(gifUrl)
                        .WithFooter(footer =>
                        {
                            footer
                                .WithText($"Ban ID: {banEntry.Id}")
                                .WithIconUrl("https://media1.tenor.com/images/194fd6382c6329e06f3ad41ab84557aa/tenor.gif?itemid=12967526");
                        })
                        .WithTimestamp(DateTimeOffset.UtcNow)
                        .AddField("Powód:", $"{reason}")
                        .AddField("Banująca osoba:", $"{Context.User.Mention}", true);
                    if (banEntry.BanEnd == DateTime.MinValue)
                    {
                        embedBuilder.AddField("Wygaśnięcie bana:", "Permamentny", true);
                    }
                    else
                    {
                        embedBuilder.AddField("Wygaśnięcie bana:", $"{banEntry.BanEnd.ToShortDateString()} {banEntry.BanEnd.ToShortTimeString()} UTC", true);
                    }
                    var embed = embedBuilder.Build();

                    var targetChannel = _git.Config.Servers[Context.Guild.Id.ToString()].NotificationChannelId;
                    if (targetChannel == default)
                    {
                        await ReplyAsync(embed: embed);
                    }
                    else
                    {
                        var channel = Context.Guild.GetTextChannel(targetChannel);
                        if (channel == null)
                        {
                            await ReplyAsync(embed: embed);
                        }
                        else
                        {
                            await channel.SendMessageAsync(embed: embed);
                        }
                    }

                    var embedBuilder2 = new EmbedBuilder()
                        .WithTitle("__Dostałeś/-aś bana na serwerze__")
                        .WithColor(new Color(0xFF3500))
                        .WithTimestamp(DateTimeOffset.UtcNow)
                        .WithFooter(footer => {
                            footer
                                .WithText($"Ban ID: {banEntry.Id}")
                                .WithIconUrl("https://media1.tenor.com/images/194fd6382c6329e06f3ad41ab84557aa/tenor.gif?itemid=12967526");
                        })
                        .WithThumbnailUrl(gifUrl)
                        .WithAuthor(author => {
                            author
                                .WithName($"Serwer: {Context.Guild.Name}")
                                .WithIconUrl(Context.Guild.IconUrl);
                        })
                        .AddField("Powód:", $"{reason}")
                        .AddField("Banująca osoba:", $"{Context.User.Username}#{Context.User.DiscriminatorValue}", true);
                    if (banEntry.BanEnd == DateTime.MinValue)
                    {
                        embedBuilder2.AddField("Wygaśnięcie bana:", "Permamentny", true);
                    }
                    else
                    {
                        embedBuilder2.AddField("Wygaśnięcie bana:", $"{banEntry.BanEnd.ToShortDateString()} {banEntry.BanEnd.ToShortTimeString()} UTC", true);
                    }
                    embedBuilder2.AddField("===============================================", "Jeśli nie zgadzasz się z tą decyzją, napisz do osoby, która cię zbanowała");
                    var embed2 = embedBuilder2.Build();
                    await (await member.GetOrCreateDMChannelAsync()).SendMessageAsync(embed: embed2);

                    await member.BanAsync(reason: reason);
                }
            }
        }

        [Command("ban")]
        [Summary("Banuje użytkownika")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task BanAsync([Summary("Osoba do zbanowania")] IGuildUser user, [Summary("Czas bana")] TimeSpan timeSpan, [Summary("Powód bana")][Remainder] string reason = "")
        {
            using (Context.Channel.EnterTypingState())  // Show the "typing" notification
            {
                if (user is SocketGuildUser member)
                {
                    if (Context.User.Id != Context.Guild.OwnerId)
                    {
                        var roles = ((SocketGuildUser)Context.User).Roles.Select(x => x.Id.ToString());
                        bool canWarn = roles.Intersect(_git.Config.Servers[Context.Guild.Id.ToString()].ManagementRoles).Count() > 0;
                        canWarn = !(member.Roles.Select(x => x.Id.ToString()).Intersect(_git.Config.Servers[Context.Guild.Id.ToString()].ManagementRoles).Count() > 0);

                        if (!canWarn)
                        {
                            await ReplyAsync("Nie możesz upomnieć tej osoby");
                            return;
                        }
                    }

                    reason = string.IsNullOrWhiteSpace(reason) ? "Powód nie został podany" : reason;

                    var banEntry = new Ban
                    {
                        UserId = member.Id,
                        GuildId = Context.Guild.Id,
                        BanEnd = DateTime.UtcNow.Add(timeSpan),
                        Reason = reason,
                        WhoBanned = Context.User.Id
                    };

                    await _dbContext.Bans.AddAsync(banEntry);
                    await _dbContext.SaveChangesAsync();

                    var gifUrl = _banGifUrls[_random.Next(0, _banGifUrls.Length + 1)];

                    var embedBuilder = new EmbedBuilder()
                        .WithTitle("__Użytkownik został zbanowany__")
                        .WithColor(new Color(0xFF3500))
                        .WithDescription($"ID użytkownika: {member.Id}")
                        .WithAuthor(author =>
                        {
                            author
                                .WithName($"Ban | {member.Username}#{member.Discriminator}")
                                .WithIconUrl(member.GetAvatarUrl());
                        })
                        .WithThumbnailUrl(gifUrl)
                        .WithFooter(footer =>
                        {
                            footer
                                .WithText($"Ban ID: {banEntry.Id}")
                                .WithIconUrl("https://media1.tenor.com/images/194fd6382c6329e06f3ad41ab84557aa/tenor.gif?itemid=12967526");
                        })
                        .WithTimestamp(DateTimeOffset.UtcNow)
                        .AddField("Powód:", $"{reason}")
                        .AddField("Banująca osoba:", $"{Context.User.Mention}", true);
                    if (banEntry.BanEnd == DateTime.MinValue)
                    {
                        embedBuilder.AddField("Wygaśnięcie bana:", "Permamentny", true);
                    }
                    else
                    {
                        embedBuilder.AddField("Wygaśnięcie bana:", $"{banEntry.BanEnd.ToShortDateString()} {banEntry.BanEnd.ToShortTimeString()} UTC", true);
                    }
                    var embed = embedBuilder.Build();

                    var targetChannel = _git.Config.Servers[Context.Guild.Id.ToString()].NotificationChannelId;
                    if (targetChannel == default)
                    {
                        await ReplyAsync(embed: embed);
                    }
                    else
                    {
                        var channel = Context.Guild.GetTextChannel(targetChannel);
                        if (channel == null)
                        {
                            await ReplyAsync(embed: embed);
                        }
                        else
                        {
                            await channel.SendMessageAsync(embed: embed);
                        }
                    }

                    var embedBuilder2 = new EmbedBuilder()
                        .WithTitle("__Dostałeś/-aś bana na serwerze__")
                        .WithColor(new Color(0xFF3500))
                        .WithTimestamp(DateTimeOffset.UtcNow)
                        .WithFooter(footer => {
                            footer
                                .WithText($"Ban ID: {banEntry.Id}")
                                .WithIconUrl("https://media1.tenor.com/images/194fd6382c6329e06f3ad41ab84557aa/tenor.gif?itemid=12967526");
                        })
                        .WithThumbnailUrl(gifUrl)
                        .WithAuthor(author => {
                            author
                                .WithName($"Serwer: {Context.Guild.Name}")
                                .WithIconUrl(Context.Guild.IconUrl);
                        })
                        .AddField("Powód:", $"{reason}")
                        .AddField("Banująca osoba:", $"{Context.User.Username}#{Context.User.DiscriminatorValue}", true);
                    if (banEntry.BanEnd == DateTime.MinValue)
                    {
                        embedBuilder2.AddField("Wygaśnięcie bana:", "Permamentny", true);
                    }
                    else
                    {
                        embedBuilder2.AddField("Wygaśnięcie bana:", $"{banEntry.BanEnd.ToShortDateString()} {banEntry.BanEnd.ToShortTimeString()} UTC", true);
                    }
                    embedBuilder2.AddField("===============================================", "Jeśli nie zgadzasz się z tą decyzją, napisz do osoby, która cię zbanowała");
                    var embed2 = embedBuilder2.Build();
                    await (await member.GetOrCreateDMChannelAsync()).SendMessageAsync(embed: embed2);

                    await member.BanAsync(reason: reason);
                }
            }
        }

        [Command("warn")]
        [Summary("Upomina użytkownika")]
        public async Task WarnAsync([Summary("Osoba do upomnienia")] IGuildUser user, [Summary("Powód upomnienia")][Remainder] string reason)
        {
            using (Context.Channel.EnterTypingState())  // Show the "typing" notification
            {
                TimeSpan expiration = TimeSpan.FromDays(7);

                if (user is SocketGuildUser member)
                {
                    if (Context.User.Id != Context.Guild.OwnerId)
                    {
                        var roles = ((SocketGuildUser)Context.User).Roles.Select(x => x.Id.ToString());
                        bool canWarn = roles.Intersect(_git.Config.Servers[Context.Guild.Id.ToString()].ManagementRoles).Count() > 0;
                        canWarn = !(member.Roles.Select(x => x.Id.ToString()).Intersect(_git.Config.Servers[Context.Guild.Id.ToString()].ManagementRoles).Count() > 0);

                        if (!canWarn)
                        {
                            await ReplyAsync("Nie możesz upomnieć tej osoby");
                            return;
                        }
                    }

                    var warnEntry = new Warn
                    {
                        UserId = member.Id,
                        GuildId = Context.Guild.Id,
                        WarnExpiration = DateTime.UtcNow.Add(expiration),
                        Reason = reason,
                        WhoWarned = Context.User.Id
                    };

                    await _dbContext.Warns.AddAsync(warnEntry);
                    await _dbContext.SaveChangesAsync();

                    var embed = new EmbedBuilder()
                        .WithTitle("__Użytkownik został upomniany__")
                        .WithColor(new Color(0xFFCD00))
                        .WithDescription($"ID użytkownika: {member.Id}")
                        .WithAuthor(author =>
                        {
                            author
                                .WithName($"Warn | {member.Username}#{member.Discriminator}")
                                .WithIconUrl(member.GetAvatarUrl());
                        })
                        .WithFooter(footer =>
                        {
                            footer
                                .WithText($"Warn ID: {warnEntry.Id}")
                                .WithIconUrl("https://emoji.gg/assets/emoji/2891_RedAlert.gif");
                        })
                        .WithTimestamp(DateTimeOffset.UtcNow)
                        .AddField("Powód:", $"{reason}", true)
                        .AddField("Osoba upominająca:", $"{Context.User.Mention}", true)
                        .AddField("Wygaśnięcie upomnienia:", $"{warnEntry.WarnExpiration.ToShortDateString()} {warnEntry.WarnExpiration.ToShortTimeString()} UTC", true)
                        .AddField("Liczba upomnień dla użytkownika:", $"Aktywne: {_dbContext.Warns.Count(x => x.UserId == member.Id && x.GuildId == Context.Guild.Id && !x.Expired)}\nŁącznie: {_dbContext.Warns.Count(x => x.UserId == member.Id && x.GuildId == Context.Guild.Id)}", true)
                        .Build();

                    var targetChannel = _git.Config.Servers[Context.Guild.Id.ToString()].NotificationChannelId;
                    if (targetChannel == default)
                    {
                        await ReplyAsync(embed: embed);
                    }
                    else
                    {
                        var channel = Context.Guild.GetTextChannel(targetChannel);
                        if (channel == null)
                        {
                            await ReplyAsync(embed: embed);
                        }
                        else
                        {
                            await channel.SendMessageAsync(embed: embed);
                        }
                    }

                    var embed2 = new EmbedBuilder()
                        .WithTitle("__Zostałeś/-aś upomniany/-a na serwerze__")
                        .WithColor(new Color(0xFFCD00))
                        .WithTimestamp(DateTimeOffset.UtcNow)
                        .WithFooter(footer => {
                            footer
                                .WithText($"Warn ID: {warnEntry.Id}")
                                .WithIconUrl("https://emoji.gg/assets/emoji/2891_RedAlert.gif");
                        })
                        .WithAuthor(author => {
                            author
                                .WithName($"Serwer: {Context.Guild.Name}")
                                .WithIconUrl(Context.Guild.IconUrl);
                        })
                        .AddField("Powód:", $"{reason}", true)
                        .AddField("Osoba upominająca:", $"{Context.User.Mention}", true)
                        .AddField("Wygaśnięcie upomnienia:", $"{warnEntry.WarnExpiration.ToShortDateString()} {warnEntry.WarnExpiration.ToShortTimeString()} UTC", true)
                        .AddField("Liczba upomnień dla użytkownika:", $"Aktywne: {_dbContext.Warns.Count(x => x.UserId == member.Id && x.GuildId == Context.Guild.Id && !x.Expired)}\nŁącznie: {_dbContext.Warns.Count(x => x.UserId == member.Id && x.GuildId == Context.Guild.Id)}", true)
                        .AddField("===============================================", "Jeśli nie zgadzasz się z tą decyzją, napisz do osoby, która cię upomniała")
                        .Build();
                    await (await member.GetOrCreateDMChannelAsync()).SendMessageAsync(embed: embed2);
                }
            }
        }

        [Command("warn")]
        [Summary("Upomina użytkownika")]
        public async Task WarnAsync([Summary("Osoba do upomnienia")] IGuildUser user, [Summary("Czas wygaśnięcia upomnienia")] TimeSpan expiration, [Summary("Powód upomnienia")][Remainder] string reason)
        {
            using (Context.Channel.EnterTypingState())  // Show the "typing" notification
            {
                if (user is SocketGuildUser member)
                {
                    if (Context.User.Id != Context.Guild.OwnerId)
                    {
                        var roles = ((SocketGuildUser)Context.User).Roles.Select(x => x.Id.ToString());
                        bool canWarn = roles.Intersect(_git.Config.Servers[Context.Guild.Id.ToString()].ManagementRoles).Count() > 0;
                        canWarn = !(member.Roles.Select(x => x.Id.ToString()).Intersect(_git.Config.Servers[Context.Guild.Id.ToString()].ManagementRoles).Count() > 0);

                        if (!canWarn)
                        {
                            await ReplyAsync("Nie możesz upomnieć tej osoby");
                            return;
                        }
                    }

                    var warnEntry = new Warn
                    {
                        UserId = member.Id,
                        GuildId = Context.Guild.Id,
                        WarnExpiration = DateTime.UtcNow.Add(expiration),
                        Reason = reason,
                        WhoWarned = Context.User.Id
                    };

                    await _dbContext.Warns.AddAsync(warnEntry);
                    await _dbContext.SaveChangesAsync();

                    var embed = new EmbedBuilder()
                        .WithTitle("__Użytkownik został upomniany__")
                        .WithColor(new Color(0xFFCD00))
                        .WithDescription($"ID użytkownika: {member.Id}")
                        .WithAuthor(author =>
                        {
                            author
                                .WithName($"Warn | {member.Username}#{member.Discriminator}")
                                .WithIconUrl(member.GetAvatarUrl());
                        })
                        .WithFooter(footer =>
                        {
                            footer
                                .WithText($"Warn ID: {warnEntry.Id}")
                                .WithIconUrl("https://emoji.gg/assets/emoji/2891_RedAlert.gif");
                        })
                        .WithTimestamp(DateTimeOffset.UtcNow)
                        .AddField("Powód:", $"{reason}", true)
                        .AddField("Osoba upominająca:", $"{Context.User.Mention}", true)
                        .AddField("Wygaśnięcie upomnienia:", $"{warnEntry.WarnExpiration.ToShortDateString()} {warnEntry.WarnExpiration.ToShortTimeString()} UTC", true)
                        .AddField("Liczba upomnień dla użytkownika:", $"Aktywne: {_dbContext.Warns.Count(x => x.UserId == member.Id && x.GuildId == Context.Guild.Id && !x.Expired)}\nŁącznie: {_dbContext.Warns.Count(x => x.UserId == member.Id && x.GuildId == Context.Guild.Id)}", true)
                        .Build();

                    var targetChannel = _git.Config.Servers[Context.Guild.Id.ToString()].NotificationChannelId;
                    if (targetChannel == default)
                    {
                        await ReplyAsync(embed: embed);
                    }
                    else
                    {
                        var channel = Context.Guild.GetTextChannel(targetChannel);
                        if (channel == null)
                        {
                            await ReplyAsync(embed: embed);
                        }
                        else
                        {
                            await channel.SendMessageAsync(embed: embed);
                        }
                    }

                    var embed2 = new EmbedBuilder()
                        .WithTitle("__Zostałeś/-aś upomniany/-a na serwerze__")
                        .WithColor(new Color(0xFFCD00))
                        .WithTimestamp(DateTimeOffset.UtcNow)
                        .WithFooter(footer => {
                            footer
                                .WithText($"Warn ID: {warnEntry.Id}")
                                .WithIconUrl("https://emoji.gg/assets/emoji/2891_RedAlert.gif");
                        })
                        .WithAuthor(author => {
                            author
                                .WithName($"Serwer: {Context.Guild.Name}")
                                .WithIconUrl(Context.Guild.IconUrl);
                        })
                        .AddField("Powód:", $"{reason}", true)
                        .AddField("Osoba upominająca:", $"{Context.User.Mention}", true)
                        .AddField("Wygaśnięcie upomnienia:", $"{warnEntry.WarnExpiration.ToShortDateString()} {warnEntry.WarnExpiration.ToShortTimeString()} UTC", true)
                        .AddField("Liczba upomnień dla użytkownika:", $"Aktywne: {_dbContext.Warns.Count(x => x.UserId == member.Id && x.GuildId == Context.Guild.Id && !x.Expired)}\nŁącznie: {_dbContext.Warns.Count(x => x.UserId == member.Id && x.GuildId == Context.Guild.Id)}", true)
                        .AddField("===============================================", "Jeśli nie zgadzasz się z tą decyzją, napisz do osoby, która cię upomniała")
                        .Build();
                    await (await member.GetOrCreateDMChannelAsync()).SendMessageAsync(embed: embed2);
                }
            }
        }

        [Command("get-bans")]
        [Summary("Wyświetla informacje na temat bana użytkownika")]
        public async Task GetBansAsync([Summary("Użytkownik")] IUser user)
        {
            using (Context.Channel.EnterTypingState())
            {
                var bans = await _dbContext.Bans.ToAsyncEnumerable().Where(x => x.UserId == user.Id).OrderBy(x => x.BanEnd).OrderByDescending(x => x.Finished).ToListAsync();

                if (bans.Count() > 0)
                {
                    List<string> pages = new List<string>(bans.Count());

                    bans.ForEach(x => pages.Add($"Powód: {x.Reason}\nKoniec: {x.BanEnd.ToShortDateString()} {x.BanEnd.ToShortTimeString()} UTC\nZakończony: {x.Finished}\nKto banował: <@{x.WhoBanned}>"));

                    var msg = new PaginatedMessage
                    {
                        Title = $"Bany dla użytkownika {user.Username}#{user.Discriminator}",
                        Pages = pages
                    };

                    await PagedReplyAsync(msg);

                    return;
                }

                await ReplyAsync("Ten użytkownik nie ma historii banów");
            }
        }

        [Command("get-warns")]
        [Summary("Wyświetla informacje na temat upomnień użytkownika")]
        public async Task GetWarnsAsync([Summary("Użytkownik")] IUser user)
        {
            using (Context.Channel.EnterTypingState())
            {
                var warns = await _dbContext.Warns.ToAsyncEnumerable().Where(x => x.UserId == user.Id).OrderBy(x => x.WarnExpiration).OrderByDescending(x => x.Expired).ToListAsync();

                if (warns.Count() > 0)
                {
                    List<string> pages = new List<string>(warns.Count());

                    warns.ForEach(x => pages.Add($"Powód: {x.Reason}\nKoniec: {x.WarnExpiration.ToShortDateString()} {x.WarnExpiration.ToShortTimeString()} UTC\nZakończony: {x.Expired}\nKto upomniał: <@{x.WhoWarned}>"));

                    var msg = new PaginatedMessage
                    {
                        Title = $"Upomnienia dla użytkownika {user.Username}#{user.Discriminator}",
                        Pages = pages
                    };

                    await PagedReplyAsync(msg);

                    return;
                }
                await ReplyAsync("Ten użytkownik nie ma historii upomnień");
            }
        }

        [Command("unban")]
        [Summary("Cofa bana dla użytkownika")]
        public async Task UnbanAsync([Summary("Użytkownik")] IUser user)
        {
            using (Context.Channel.EnterTypingState())
            {
                bool isBanned = false;
                isBanned = (await Context.Guild.GetBanAsync(user)) != null;
                if (isBanned)
                {
                    await Context.Guild.RemoveBanAsync(user);

                    var ban = _dbContext.Bans.FirstOrDefault(x => !x.Finished);
                    if (ban != null)
                    {
                        ban.Finished = true;
                        _dbContext.Bans.Update(ban);
                        await _dbContext.SaveChangesAsync();
                    }

                    await ReplyAsync("Użytkownik odbanowany");

                    var dmEmbed = new EmbedBuilder()
                        .WithTitle("__Zostałeś/-aś odbanowana na serwerze__")
                        .WithColor(new Color(0x4AFF00))
                        .WithTimestamp(DateTimeOffset.UtcNow)
                        .WithFooter(footer =>
                        {
                            footer
                                .WithText($"Ban ID: {ban.Id}");
                        })
                        .WithAuthor(author =>
                        {
                            author
                                .WithName($"{Context.Guild.Name}")
                                .WithIconUrl(Context.Guild.IconUrl);
                        })
                        .AddField("Link do dołączenia do serwera:", $"{Context.Guild.DefaultChannel.CreateInviteAsync(null, 1, isUnique: true)}")
                        .Build();

                    await (await user.GetOrCreateDMChannelAsync()).SendMessageAsync(embed: dmEmbed);

                    return;
                }
                await ReplyAsync("Ten użytkownik nie jest zbanowany");
            }
        }

        [Command("test")]
        [Summary("test")]
        [RequireUser(312223735505747968)]
        public async Task TestAsync(TimeSpan timeSpan)
        {
            Console.WriteLine(timeSpan);
            await ReplyAsync();
        }

        [Group("managementRole")]
        public class ManagementRoleModule : InteractiveBase<SocketCommandContext>
        {
            private readonly GitHubService _git;
            private readonly InteractiveService _interactive;

            public ManagementRoleModule(GitHubService git, InteractiveService interactive)
            {
                _git = git;
                _interactive = interactive;
            }


            [Command("add")]
            [Summary("Adds role to list of roles that can use this bot")]
            public async Task AddManagementRoleAsync([Summary("Role")] IRole role)
            {
                using (Context.Channel.EnterTypingState())  // Show the "typing" notification
                {
                    var roleIdString = role.Id.ToString();  // Get role ID
                    if (_git.Config.Servers[Context.Guild.Id.ToString()].ManagementRoles.Contains(roleIdString))    // Check for information about role in config
                    {
                        await ReplyAsync("This role is already in the list");
                    }
                    else
                    {
                        _git.Config.Servers[Context.Guild.Id.ToString()].ManagementRoles.Add(roleIdString); // Add role to config
                        await _git.UploadConfig();
                        await ReplyAsync("Role added to list");
                    }
                }
            }

            [Command("remove")]
            [Summary("Removes role from list of roles that can use this bot")]
            public async Task RemoveManagementRoleAsync([Summary("Role")] IRole role)
            {
                using (Context.Channel.EnterTypingState())
                {
                    var roleIdString = role.Id.ToString();
                    if (_git.Config.Servers[Context.Guild.Id.ToString()].ManagementRoles.Contains(roleIdString))
                    {
                        _git.Config.Servers[Context.Guild.Id.ToString()].ManagementRoles.Remove(roleIdString);
                        await _git.UploadConfig();
                        await ReplyAsync("Role removed from list");
                    }
                    else
                    {
                        await ReplyAsync("Role not in list");
                    }
                }
            }

        }

        [Group("countChannel")]
        public class CountChannelModule : InteractiveBase<SocketCommandContext>
        {
            private readonly GitHubService _git;
            private readonly InteractiveService _interactive;

            public CountChannelModule(GitHubService git, InteractiveService interactive)
            {
                _git = git;
                _interactive = interactive;
            }

            [Command("set")]
            [Summary("Sets specified channel as a count channel")]
            public async Task SetCountChannel([Summary("Channel")] IChannel channel, [Summary("Property to display")] CountPropertyEnum property, [Summary("Channel naming format (use %num% as a placeholder for the number)")][Remainder] string template)
            {
                using (Context.Channel.EnterTypingState())
                {
                    if (!template.Contains("%num%"))    // Check if template contains placeholder
                    {
                        await ReplyAsync("Template doesn't contain **%num%** placeholder");
                    }
                    else
                    {
                        var guildId = Context.Guild.Id.ToString();  // Get server ID
                        var channelId = channel.Id.ToString();  // Get channel ID
                        _git.Config.Servers[guildId].CountChannels[channelId] = new GitHubService.ConfigClass.ServerConfigClass.CountChannelClass   // Create new/Update config
                        {
                            Property = property,
                            Template = template
                        };
                        await _git.UploadConfig();
                        await ReplyAsync($"Channel **{channel}** set successfully");
                    }
                }
            }

            [Command("clear")]
            [Summary("Removes counting functionality from channel")]
            public async Task ClearCountChannel([Summary("Channel")] IChannel channel)
            {
                using (Context.Channel.EnterTypingState())
                {
                    var guildId = Context.Guild.Id.ToString();
                    var channelId = channel.Id.ToString();
                    _git.Config.Servers[guildId].CountChannels.Remove(channelId);
                    await _git.UploadConfig();
                    await ReplyAsync($"Channel **{channel}** cleared successfully");
                }
            }
        }

        [Group("vcTextRole")]
        public class VcTextRoleModule : InteractiveBase<SocketCommandContext>
        {
            private readonly GitHubService _git;
            private readonly InteractiveService _interactive;

            public VcTextRoleModule(GitHubService git, InteractiveService interactive)
            {
                _git = git;
                _interactive = interactive;
            }

            [Command("set")]
            [Summary("Sets a role to give on entering a VC")]
            public async Task SetVcTextRole([Summary("Channel")] IVoiceChannel channel, [Summary("Role")] IRole role)
            {
                using (Context.Channel.EnterTypingState())
                {
                    var guildId = Context.Guild.Id.ToString();
                    _git.Config.Servers[guildId].VcTextRole[channel.Id.ToString()] = role.Id.ToString();    // Set role ID for channel
                    await _git.UploadConfig();
                    await ReplyAsync("Role successfully bound to voice chat");
                }
            }

            [Command("clear")]
            [Summary("Clears a role given on entering a VC")]
            public async Task ClearVcTextRole([Summary("Channel")] IVoiceChannel channel)
            {
                using (Context.Channel.EnterTypingState())
                {
                    var guildId = Context.Guild.Id.ToString();
                    _git.Config.Servers[guildId].VcTextRole.Remove(channel.Id.ToString());  // Clear channel
                    await _git.UploadConfig();
                    await ReplyAsync("Role successfully unbound from voice chat");
                }
            }
        }

        [Group("autoreact")]
        public class AutoreactModule : InteractiveBase<SocketCommandContext>
        {
            private readonly GitHubService _git;
            private readonly InteractiveService _interactive;

            public AutoreactModule(GitHubService git, InteractiveService interactive)
            {
                _git = git;
                _interactive = interactive;
            }

            [Command("set")]
            [Summary("Sets roles to react with and channel to react on")]
            public async Task SetAutoreact([Summary("Channel")] ITextChannel channel, [Summary("Space separated Emotes/Emojis")] params string[] emotes)
            {
                using (Context.Channel.EnterTypingState())
                {
                    var guildId = Context.Guild.Id.ToString();
                    foreach (var text in emotes)
                    {
                        IEmote emote;
                        try
                        {
                            emote = Emote.Parse(text);  // Try parse emote (<:dyg:708782038521741352>)
                        }
                        catch (Exception)   // If emote is not a custom server emote
                        {
                            if (new Regex("[A-z0-9]").IsMatch(text))    // Check if text is a unicode emoji
                            {
                                await ReplyAsync($"**{text}** is not a valid emote");
                                return;
                            }
                            _ = new Emoji(text);    // Create emoji (👍)
                        }
                    }
                    _git.Config.Servers[guildId].AutoReact[channel.Id.ToString()] = emotes.ToList();    // Add emotes to config
                    await _git.UploadConfig();
                    await ReplyAsync($"AutoReply on channel **{channel}** set up successfully");
                }
            }

            [Command("clear")]
            [Summary("Clears autoreact from channel")]
            public async Task ClearAutoreact([Summary("Channel")] ITextChannel channel)
            {
                using (Context.Channel.EnterTypingState())
                {
                    var guildId = Context.Guild.Id.ToString();
                    _git.Config.Servers[guildId].AutoReact.Remove(channel.Id.ToString());   // Clear emotes
                    await _git.UploadConfig();
                    await ReplyAsync($"AutoReply on channel **{channel}** cleared successfully");
                }
            }
        }

        [Group("commandLimit")]
        public class CommandLimitModule : InteractiveBase<SocketCommandContext>
        {
            private readonly GitHubService _git;
            private readonly InteractiveService _interactive;
            private readonly CommandService _service;

            public CommandLimitModule(GitHubService git, CommandService service, InteractiveService interactive)
            {
                _git = git;
                _service = service;
                _interactive = interactive;
            }

            [Command("add")]
            [Summary("Adds a channel where a command can be used in")]
            public async Task AddCommandLimitAsync([Summary("Channel")] ITextChannel channel, [Summary("Command")][Remainder] string command)
            {
                using (Context.Channel.EnterTypingState())
                {
                    if (!_service.Commands.Any(x => x.Aliases.Contains(command)))
                    {
                        await ReplyAsync($"No command has alias of {command}");
                    }
                    else
                    {
                        command = _service.Commands.First(x => x.Aliases.Contains(command)).Name;
                        if (_git.Config.Servers[Context.Guild.Id.ToString()].CommandLimit.ContainsKey(command))
                        {
                            if (_git.Config.Servers[Context.Guild.Id.ToString()].CommandLimit[command].Contains(channel.Id.ToString()))
                            {
                                await ReplyAsync("Command already limited to that channel");
                            }
                            else
                            {
                                _git.Config.Servers[Context.Guild.Id.ToString()].CommandLimit[command].Add(channel.Id.ToString());
                                await _git.UploadConfig();
                                await ReplyAsync($"Command **{command}** limited to channel {channel.Mention}");
                            }
                        }
                        else
                        {
                            _git.Config.Servers[Context.Guild.Id.ToString()].CommandLimit[command] = new List<string> { channel.Id.ToString() };
                            await _git.UploadConfig();
                            await ReplyAsync($"Command **{command}** limited to channel {channel.Mention}");
                        }
                    }
                }
            }

            [Command("remove")]
            [Summary("Removes a channel where a command can be used in")]
            public async Task RemoveCommandLimitAsync([Summary("Channel")] ITextChannel channel, [Summary("Command")][Remainder] string command)
            {
                using (Context.Channel.EnterTypingState())
                {
                    if (!_service.Commands.Any(x => x.Aliases.Contains(command)))
                    {
                        await ReplyAsync($"No command has alias of {command}");
                    }
                    else
                    {
                        command = _service.Commands.First(x => x.Aliases.Contains(command)).Name;
                        if (_git.Config.Servers[Context.Guild.Id.ToString()].CommandLimit.ContainsKey(command))
                        {
                            _git.Config.Servers[Context.Guild.Id.ToString()].CommandLimit.Remove(command);
                            await _git.UploadConfig();
                            await ReplyAsync($"Command **{command}** removed from channel {channel.Mention}");
                        }
                        else
                        {
                            await ReplyAsync("Command was not limited to that channel");
                        }
                    }
                }
            }

            [Command("show")]
            [Summary("Shows what channels command can be used in")]
            public async Task ShowCommandLimitAsync([Summary("Command")][Remainder] string command)
            {
                using (Context.Channel.EnterTypingState())
                {
                    if (!_service.Commands.Any(x => x.Aliases.Contains(command)))
                    {
                        await ReplyAsync($"No command has alias of {command}");
                    }
                    else
                    {
                        string message = "Channels where command can be used:\n";
                        if (_git.Config.Servers[Context.Guild.Id.ToString()].CommandLimit.ContainsKey(command))
                        {
                            foreach (var channelId in _git.Config.Servers[Context.Guild.Id.ToString()].CommandLimit[command])
                            {
                                message += $"<#{channelId}>\n";
                            }
                        }
                        else
                        {
                            message += "All channels (no limits)";
                        }
                        await ReplyAsync(message);
                    }
                }
            }
        }
    }
}
