using Godot;
using Godot.Collections;
using System;

public partial class LanListener : Node
{
    public const int ListenPort = 8999;

    private PacketPeerUdp _udpListener = new();
    private double _expireTime = 3.0;

    public static LanListener Instance;
    public Dictionary<string, Dictionary> DiscoveredServers = [];

    [Signal] public delegate void ListUpdatedEventHandler();

    public override void _Ready()
    {
        Error result = _udpListener.Bind(ListenPort);
        if (result != Error.Ok)
        {
            GD.Print($"Failed to bind LAN listener to port {ListenPort}, {result}");
            QueueFree();
            return;
        }

        Instance = this;
        GD.Print($"LAN Listener bound to port {ListenPort}");
    }

    public override void _Process(double delta)
    {
        while (_udpListener.GetAvailablePacketCount() > 0)
        {
            string senderIp = _udpListener.GetPacketIP();
            int senderPort = _udpListener.GetPacketPort();
            byte[] rawData = _udpListener.GetPacket();

            if (senderIp == string.Empty) return;

            string payload = System.Text.Encoding.UTF8.GetString(rawData);
            ParseServerData(senderIp, payload);
        }

        ClearExpiredServers();
    }

    public T GetServerValue<T>(string ip, string value)
    {
        try
        {
            return (T)Convert.ChangeType((string)DiscoveredServers[ip][value], typeof(T));
        }
        catch
        {
            return default;
        }        
    }

    public static T GetPayloadValue<T>(Dictionary info, string value)
    {
        try
        {
            return (T)Convert.ChangeType((string)info[value], typeof(T));
        }
        catch
        {
            return default;
        }
    }

    private void ParseServerData(string ip, string payload)
    {
        string[] parts = payload.Split('|');
        if (parts.Length < 5) return;

        Dictionary info = new()
        {
            { "Ip", ip },
            { "HostName", parts[0] },
            { "MapName", parts[1] },
            { "PlayersCount", parts[2] },
            { "MaximumPlayers", parts[3] },
            { "Available", parts[4] },
            { "LastSeen", Time.GetUnixTimeFromSystem() }
        };

        if (!DiscoveredServers.ContainsKey(ip)) // add new server
        {
            DiscoveredServers.Add(ip, info);
            GD.Print($"Discovered new host {ip}");
            EmitSignal(SignalName.ListUpdated);
        }
        else // update already existing server
        {
            int previousCount = GetServerValue<int>(ip, "PlayersCount"), newCount = GetPayloadValue<int>(info, "PlayersCount");
            bool previousState = GetServerValue<bool>(ip, "Available"), newState = GetPayloadValue<bool>(info, "Available");

            DiscoveredServers[ip] = info;
            if (previousCount != newCount || previousState != newState) EmitSignal(SignalName.ListUpdated);
        }
    }

    private void ClearExpiredServers()
    {
        double currentTime = Time.GetUnixTimeFromSystem();
        Array<string> expired = [];

        foreach(var server in DiscoveredServers)
        {
            if (currentTime - (double)server.Value["LastSeen"] > _expireTime)
            {
                expired.Add(server.Key);
            }
        }

        if(expired.Count > 0)
        {
            foreach (var server in expired) DiscoveredServers.Remove(server);
            EmitSignal(SignalName.ListUpdated);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _udpListener.Close();
        base.Dispose(disposing);
    }
}
