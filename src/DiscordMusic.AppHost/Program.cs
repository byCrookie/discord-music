var builder = DistributedApplication.CreateBuilder(args);

builder
    .AddProject<Projects.DiscordMusic_Client>("discord-music", launchProfileName: "DiscordMusic")
    .WithCertificateTrustScope(CertificateTrustScope.None)
    .WithDeveloperCertificateTrust(false);

builder.Build().Run();
