﻿namespace Kokoro.Services;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Hosting;
using Discord.Commands;
using Discord.WebSocket;
using Kokoro.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class CommandHandler : KokoroService
{
    public static CommandHandler Instance;
    private readonly IServiceProvider _provider;
    private readonly CommandService _commandService;
    private readonly IConfiguration _config;
    public Dictionary<ulong, string> ServerPrefixes;

    public CommandHandler(DiscordSocketClient client, ILogger<DiscordClientService> logger, IServiceProvider provider, 
        CommandService commandService, DataAccessLayer dataAccessLayer, IConfiguration config)
        : base(client, logger, config, dataAccessLayer)
    {
        Instance = this;
        _provider = provider;
        _commandService = commandService;
        _config = config;
        Client.Ready += ClientReady;
        Client.JoinedGuild += ClientJoinedGuild;
    }

    private async Task ClientJoinedGuild(SocketGuild joinedGuild)
    {
        await DataAccessLayer.CreateGuildAsync(joinedGuild.Id);
    }

    private Task ClientReady()
    {
        ServerPrefixes = DataAccessLayer.GetPrefixes();
        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Client.MessageReceived += HandleMessage;
        _commandService.CommandExecuted += CommandExecutedAsync;
        await _commandService.AddModulesAsync(Assembly.GetEntryAssembly(), _provider);
    }

    private async Task HandleMessage(SocketMessage incomingMessage)
    {
        if (incomingMessage is not SocketUserMessage message) return;
        if (message.Source != MessageSource.User) return;

        int argPos = 0;
        var user = message.Author as SocketGuildUser;
        var prefix = ServerPrefixes[user.Guild.Id];
        if (!message.HasStringPrefix(prefix, ref argPos)
            && !message.HasMentionPrefix(Client.CurrentUser, ref argPos)) return;

        var context = new SocketCommandContext(Client, message);
        await _commandService.ExecuteAsync(context, argPos, _provider);
    }

    public async Task CommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
    {
        Logger.LogInformation("User {user} attempted to use command {command}", context.User, command.Value.Name);

        if (!command.IsSpecified || result.IsSuccess)
            return;

        await context.Channel.SendMessageAsync($"Error: {result}");
    }
}