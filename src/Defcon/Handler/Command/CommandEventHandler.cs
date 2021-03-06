﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Defcon.Core.Entities.Discord.Embeds;
using Defcon.Library.Attributes;
using Defcon.Library.Extensions;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using Serilog;

namespace Defcon.Handler.Command
{
    public class CommandEventHandler
    {
        private readonly CommandsNextExtension commandsNext;
        private readonly ILogger logger;

        public CommandEventHandler(CommandsNextExtension commandsNext, ILogger logger)
        {
            this.commandsNext = commandsNext;
            this.logger = logger;

            this.commandsNext.CommandExecuted += CommandExecuted;
            this.commandsNext.CommandErrored += CommandErrored;
        }

        private async Task CommandExecuted(CommandsNextExtension sender, CommandExecutionEventArgs e)
        {
            Task.Run(async () =>
            {
                var command = e.Command;
                var context = e.Context;
                var guild = context.Guild;
                var channel = context.Channel;
                var user = context.User;

                if (!channel.IsPrivate)
                {
                    this.logger.Information(user != null 
                        ? $"The command '{command.QualifiedName}' has been executed by '{user.GetUsertag()}' in the channel '{channel.Name}' ({channel.Id}) on the guild '{guild.Name}' ({guild.Id})." 
                        : $"The command '{command.QualifiedName}' has been executed by an unknown user in a deleted channel on a unknown guild.");
                }
                else
                {
                    this.logger.Information($"The command '{command.QualifiedName}' has been executed by '{user.GetUsertag()}' ({user.Id}) in the direct message.");
                }
            });
        }

        private async Task CommandErrored(CommandsNextExtension sender, CommandErrorEventArgs e)
        {
            Task.Run(async () =>
            {
                var command = e.Command;
                var commandName = command?.QualifiedName ?? "unknown command";
                var context = e.Context;
                var guild = context.Guild;
                var channel = context.Channel;
                var user = context.User;

                var application = await context.Client.GetCurrentApplicationAsync().ConfigureAwait(true);

                if (!channel.IsPrivate)
                {
                    if (e.Exception is ChecksFailedException checksFailedException)
                    {
                        context = checksFailedException.Context;
                        var failedChecks = checksFailedException.FailedChecks;

                        var permissions = failedChecks.OfType<RequirePermissionsAttribute>()
                                                      .Select(x => x.Permissions.ToPermissionString());

                        var userPermissions = failedChecks.OfType<RequireUserPermissionsAttribute>()
                                                          .Select(x => x.Permissions.ToPermissionString());

                        var botPermissions = failedChecks.OfType<RequireBotPermissionsAttribute>()
                                                         .Select(x => x.Permissions.ToPermissionString());

                        var requiredPermissionsDetails = new StringBuilder();

                        if (failedChecks.Any(x => x is RequireOwnerAttribute))
                        {
                            requiredPermissionsDetails.AppendLine("Owner-only");
                        }

                        if (failedChecks.Any(x => x is RequirePrivilegedUserAttribute))
                        {
                            requiredPermissionsDetails.AppendLine("Privileged users only");
                        }

                        if (permissions.Any())
                        {
                            requiredPermissionsDetails.AppendLine(Formatter.InlineCode(string.Join(",", permissions)));
                        }

                        if (userPermissions.Any())
                        {
                            requiredPermissionsDetails.AppendLine($"User: {Formatter.InlineCode(string.Join(",", userPermissions))}");
                        }

                        if (botPermissions.Any())
                        {
                            requiredPermissionsDetails.AppendLine($"Bot: {Formatter.InlineCode(string.Join(",", botPermissions))}");
                        }

                        if (botPermissions.Any(x => Permissions.EmbedLinks.ToPermissionString().Equals(x)))
                        {
                            await context.RespondAsync($":no_entry: Access denied\nThe bot is requiring the following permission: {Formatter.InlineCode(Permissions.EmbedLinks.ToPermissionString())}").ConfigureAwait(true);
                        }
                        else
                        {
                            var embed = new Embed
                            {
                                Title = ":no_entry: Access denied",
                                Description = "Missing permissions are required to execute this command.",
                                Color = DiscordColor.IndianRed,
                                Fields = new List<EmbedField> { new EmbedField { Name = "Required Permissions", Value = requiredPermissionsDetails.ToString(), Inline = false } },
                                Footer = new EmbedFooter { Text = $"Command: {commandName} | Requested by {user.GetUsertag()} | {user.Id}", Icon = user.AvatarUrl }
                            };

                            await context.SendEmbedMessageAsync(embed).ConfigureAwait(true);
                        }
                    }

                    if (e.Exception is CommandNotFoundException commandNotFoundException)
                    {
                        var failedCommand = commandNotFoundException.CommandName;
                        var embed = new Embed { Title = ":no_entry: Command not found", Description = $"The command {Formatter.InlineCode(failedCommand)} does not exist.", Color = DiscordColor.Aquamarine, Footer = new EmbedFooter { Text = $"Requested on {guild.Name} | {guild.Id}", Icon = guild.IconUrl } };

                        await guild.GetMemberAsync(user.Id)
                                   .Result.SendEmbedMessageAsync(embed)
                                   .ConfigureAwait(true);
                    }
                    
                    if (application.Owners.Any(x => x.Id == user.Id))
                    {
                        if (e.Exception is ArgumentException argumentException)
                        {
                            var embed = new Embed
                            {
                                Title = ":no_entry: Argument Exception",
                                Description = $"{argumentException.Message}",
                                Color = DiscordColor.Aquamarine,
                                Fields = new List<EmbedField> { new EmbedField { Name = "Command Example", Value = Formatter.InlineCode($"{commandName} SOON AVAILABLE") } },
                                Footer = new EmbedFooter { Text = $"Requested on {guild.Name} | {guild.Id}", Icon = guild.IconUrl }
                            };

                            await guild.GetMemberAsync(user.Id)
                                       .Result.SendEmbedMessageAsync(embed)
                                       .ConfigureAwait(true);
                        }

                        if (e.Exception is InvalidOperationException invalidOperationException)
                        {
                            var embed = new Embed { Title = ":no_entry: Invalid Operation", Description = $"{invalidOperationException.Message}", Color = DiscordColor.Aquamarine, Footer = new EmbedFooter { Text = $"Requested on {guild.Name} | {guild.Id}", Icon = guild.IconUrl } };

                            await guild.GetMemberAsync(user.Id)
                                       .Result.SendEmbedMessageAsync(embed)
                                       .ConfigureAwait(true);
                        }
                    }

                    this.logger.Error(e.Exception, $"The command '{commandName}' has been errored by '{user.GetUsertag()}' in the channel '{channel.Name}' ({channel.Id}) on the guild '{guild.Name}' ({guild.Id}).");
                }
                else
                {
                    this.logger.Error(e.Exception, $"The command '{commandName}' has been errored by '{user.GetUsertag()}' ({user.Id}) in the direct message.");
                }
            });
        }
    }
}