﻿using Orleans;
using Orleans.Runtime.Configuration;
using System;
using System.IO;
using System.Management.Automation;
using System.Net;

namespace OrleansPSUtils
{
    using System.Collections.Generic;

    [Cmdlet(VerbsLifecycle.Start, "GrainClient", DefaultParameterSetName = DefaultSet)]
    public class StartGrainClient : PSCmdlet
    {
        private const string DefaultSet = "Default";
        private const string FilePathSet = "FilePath";
        private const string FileSet = "File";
        private const string ConfigSet = "Config";
        private const string EndpointSet = "Endpoint";

        [Parameter(Position = 1, Mandatory = true, ValueFromPipeline = true, ParameterSetName = FilePathSet)]
        public string ConfigFilePath { get; set; }

        [Parameter(Position = 2, Mandatory = true, ValueFromPipeline = true, ParameterSetName = FileSet)]
        public FileInfo ConfigFile { get; set; }

        [Parameter(Position = 3, Mandatory = true, ValueFromPipeline = true, ParameterSetName = ConfigSet)]
        public ClientConfiguration Config { get; set; }

        [Parameter(Position = 4, Mandatory = true, ValueFromPipeline = true, ParameterSetName = EndpointSet)]
        public IPEndPoint GatewayAddress { get; set; }

        [Parameter(Position = 5, ValueFromPipeline = true, ParameterSetName = EndpointSet)]
        public bool OverrideConfig { get; set; } = true;

        [Parameter(Position = 6, ValueFromPipeline = true, ParameterSetName = FilePathSet)]
        [Parameter(Position = 6, ValueFromPipeline = true, ParameterSetName = FileSet)]
        [Parameter(Position = 6, ValueFromPipeline = true, ParameterSetName = ConfigSet)]
        [Parameter(Position = 6, ValueFromPipeline = true, ParameterSetName = EndpointSet)]
        public TimeSpan Timeout { get; set; } = TimeSpan.Zero;

        protected override void ProcessRecord()
        {
            try
            {
                WriteVerbose($"[{DateTime.UtcNow}] Initializing Orleans Grain Client");
                IClusterClient client;
                switch (ParameterSetName)
                {
                    case FilePathSet:
                        WriteVerbose($"[{DateTime.UtcNow}] Using config file at '{ConfigFilePath}'...");
                        if (string.IsNullOrWhiteSpace(ConfigFilePath))
                            throw new ArgumentNullException(nameof(ConfigFilePath));
                        client = ClusterClient.Create(ConfigFilePath);
                        break;
                    case FileSet:
                        WriteVerbose($"[{DateTime.UtcNow}] Using provided config file...");
                        if (ConfigFile == null)
                            throw new ArgumentNullException(nameof(ConfigFile));
                        client = ClusterClient.Create(ConfigFile);
                        break;
                    case ConfigSet:
                        WriteVerbose($"[{DateTime.UtcNow}] Using provided 'ClientConfiguration' object...");
                        if (Config == null)
                            throw new ArgumentNullException(nameof(Config));
                        client = ClusterClient.Create(Config);
                        break;
                    case EndpointSet:
                        WriteVerbose($"[{DateTime.UtcNow}] Using default Orleans Grain Client initializer");
                        if (GatewayAddress == null)
                            throw new ArgumentNullException(nameof(GatewayAddress));
                        var config = this.GetOverriddenConfig();
                        client = ClusterClient.Create(config);
                        break;
                    default:
                        WriteVerbose($"[{DateTime.UtcNow}] Using default Orleans Grain Client initializer");
                        client = ClusterClient.Create();
                        break;
                }

                if (Timeout != TimeSpan.Zero)
                    client.ResponseTimeout = this.Timeout;
                client.Start().Wait();
                this.SetClient(client);
                this.WriteObject(client);
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, ex.GetType().Name, ErrorCategory.InvalidOperation, this));
            }
        }

        private ClientConfiguration GetOverriddenConfig()
        {
            var config = ClientConfiguration.StandardLoad();
            if (config == null)
            {
                Console.WriteLine("Error loading standard client configuration file.");
                throw new ArgumentException("Error loading standard client configuration file");
            }
            if (this.OverrideConfig)
            {
                config.Gateways = new List<IPEndPoint>(new[] { this.GatewayAddress });
            }
            else if (!config.Gateways.Contains(this.GatewayAddress))
            {
                config.Gateways.Add(this.GatewayAddress);
            }
            config.PreferedGatewayIndex = config.Gateways.IndexOf(this.GatewayAddress);
            return config;
        }

        protected override void StopProcessing()
        {
            var client = this.GetClient();
            client?.Stop();
            client?.Dispose();
            this.SetClient(null);
        }
    }
}
