namespace OpcPlc.PluginNodes;

using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;

/// <summary>
/// Defines the configuration folder, which holds the list of nodes.
/// </summary>
public class ConfigFolder
{
    public string Folder { get; set; }

    public List<ConfigFolder> FolderList { get; set; }

    public List<ConfigNode> NodeList { get; set; }
}

/// <summary>
/// Used to define the node, which will be published by the server.
/// </summary>
public class ConfigNode
{
    [JsonProperty(Required = Required.Always)]
    public dynamic NodeId { get; set; }

    public string Name { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    [DefaultValue("Int32")]
    public string DataType { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    [DefaultValue(-1)]
    public int ValueRank { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    [DefaultValue("CurrentReadOrWrite")]
    public string AccessLevel { get; set; }

    public string Description { get; set; }

    public object Value { get; set; }
}

/// <summary>
/// Defines the configuration folder, which holds the list of nodes.
/// </summary>
public class SimulatedConfigFolder
{
    public int? NamespaceIndex { get; set; }

    public string Folder { get; set; }

    public List<SimulatedConfigFolder> FolderList { get; set; }

    public List<SimulatedConfigNode> NodeList { get; set; }
}

/// <summary>
/// Used to define the node, which will be published by the server.
/// </summary>
public class SimulatedConfigNode
{
    [JsonProperty(Required = Required.Always)]
    public dynamic NodeId { get; set; }

    public string Name { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    [DefaultValue("Int32")]
    public string DataType { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    [DefaultValue(-1)]
    public int ValueRank { get; set; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    [DefaultValue("CurrentReadOrWrite")]
    public string AccessLevel { get; set; }

    public string Description { get; set; }

    public object Value { get; set; }

    public SimulatedParameters Parameters { get; set; }
}

public class SimulatedParameters
{
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    [DefaultValue(1000)]
    public uint IntervalMilliseconds { get; set; }
}

public class CountUpSimulatedParameters : SimulatedParameters
{
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    [DefaultValue(0)]
    public int Start { get; set; }
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    [DefaultValue(1)]
    public int StepBy { get; set; }
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    [DefaultValue(false)]
    public bool ShouldRestart { get; set; }
    public int RestartWhenLessThan { get; set; }
    public int RestartWhenGreaterThan { get; set; }
}

public class SequenceSimulatedParameters : SimulatedParameters
{
    [JsonProperty(Required = Required.Always)]
    public object[] Values { get; set; }
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    [DefaultValue(1)]
    public int StepBy { get; set; }
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    [DefaultValue(true)]
    public bool ShouldRestart { get; set; }
}
