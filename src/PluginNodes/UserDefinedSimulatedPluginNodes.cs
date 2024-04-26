namespace OpcPlc.PluginNodes;

using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Opc.Ua;
using OpcPlc.Helpers;
using OpcPlc.PluginNodes.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Nodes that are configured via JSON file.
/// </summary>
public class UserDefinedSimulatedPluginNodes(TimeService timeService, ILogger logger) : PluginNodeBase(timeService, logger), IPluginNodes
{
    private string _nodesFileName;
    private PlcNodeManager _plcNodeManager;
    private int _namespaceIndex = (int)NamespaceType.OpcPlcApplications;
    private List<(BaseDataVariableState, SimulatedParameters)> _simulatedNodes = [];
    private Dictionary<BaseDataVariableState, object> _simulatedNodesState = [];
    private List<ITimer> _simulators = [];

    public void AddOptions(Mono.Options.OptionSet optionSet)
    {
        optionSet.Add(
            "nfs|nodesfilesim=",
            "the filename that contains the list of nodes included simulation parameters to be created in the OPC UA address space.",
            (string s) => _nodesFileName = s);
    }

    public void AddToAddressSpace(FolderState telemetryFolder, FolderState methodsFolder, PlcNodeManager plcNodeManager)
    {
        _plcNodeManager = plcNodeManager;

        if (!string.IsNullOrEmpty(_nodesFileName))
        {
            AddNodes((FolderState)telemetryFolder.Parent); // Root.
        }
    }

    public void StartSimulation()
    {
        foreach ((var node, var parameters) in _simulatedNodes)
        {
            switch (parameters)
            {
                case CountUpSimulatedParameters cuParameters:
                    _simulators.Add(CreateTimer(() => Simulate(node, cuParameters), parameters.IntervalMilliseconds));
                    break;
                case SequenceSimulatedParameters sParameters:
                    _simulators.Add(CreateTimer(() => Simulate(node, sParameters), parameters.IntervalMilliseconds));
                    break;
                default:
                    throw new InvalidOperationException($"Missing Simulate method for type {parameters.GetType()}");
            }
        }
    }

    private ITimer CreateTimer(Action simulate, uint nodeRate)
    {
        if (nodeRate >= 50)
        {
            return _timeService.NewTimer((s, e) => simulate(), nodeRate);
        }

        return _timeService.NewFastTimer((s, e) => simulate(), nodeRate);
    }

    public void StopSimulation()
    {
        foreach (var simulator in _simulators)
        {
            simulator.Enabled = false;
        }
    }

    private void Simulate(BaseDataVariableState node, CountUpSimulatedParameters cuParameters)
    {
        lock (node)
        {
            int? value = _simulatedNodesState[node] as int?;
            if (value == null)
            {
                value = cuParameters.Start;
                SetValue(node, value);
                _simulatedNodesState[node] = value;
                return;
            }

            value += cuParameters.StepBy;
            if (cuParameters.ShouldRestart)
            {
                if (value < cuParameters.RestartWhenLessThan) value = cuParameters.Start;
                if (value > cuParameters.RestartWhenGreaterThan) value = cuParameters.Start;
            }

            SetValue(node, value);
            _simulatedNodesState[node] = value;
        }
    }

    private void Simulate(BaseDataVariableState node, SequenceSimulatedParameters sParameters)
    {
        lock (node)
        {
            int? index = _simulatedNodesState[node] as int?;
            if (index == null)
            {
                index = 0;
                SetValue(node, sParameters.Values[index.Value]);
                _simulatedNodesState[node] = index;
                return;
            }

            index += sParameters.StepBy;
            if (index >= sParameters.Values.Length && !sParameters.ShouldRestart)
            {
                return;
            }

            index %= sParameters.Values.Length;
            SetValue(node, sParameters.Values[index.Value]);
            _simulatedNodesState[node] = index;
        }
    }

    private void SetValue<T>(BaseVariableState variable, T value)
    {
        variable.Value = value;
        variable.Timestamp = _timeService.Now();
        variable.ClearChangeMasks(_plcNodeManager.SystemContext, includeChildren: false);
    }

    private void AddNodes(FolderState folder)
    {
        try
        {
            string json = File.ReadAllText(_nodesFileName);

            var cfgFolder = JsonConvert.DeserializeObject<SimulatedConfigFolder>(json, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                MissingMemberHandling = MissingMemberHandling.Ignore,
            });

            _logger.LogInformation($"Processing node information configured in {_nodesFileName}");

            Nodes = AddNodes(folder, cfgFolder).ToList();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error loading user defined node file {file}: {error}", _nodesFileName, e.Message);
        }


        _logger.LogInformation("Completed processing user defined node file");
    }

    private IEnumerable<NodeWithIntervals> AddNodes(FolderState folder, SimulatedConfigFolder cfgFolder)
    {
        _logger.LogDebug($"Create folder {cfgFolder.Folder}");
        FolderState userNodesFolder = _plcNodeManager.CreateFolder(
            folder,
            path: cfgFolder.Folder,
            name: cfgFolder.Folder,
            _namespaceIndex);

        foreach (var node in cfgFolder.NodeList)
        {
            bool isDecimal = node.NodeId is long;
            bool isString = node.NodeId is string;

            if (!isDecimal && !isString)
            {
                _logger.LogError($"The type of the node configuration for node with name {node.Name} ({node.NodeId.GetType()}) is not supported. Only decimal, string, and guid are supported. Defaulting to string.");
                node.NodeId = node.NodeId.ToString();
            }

            bool isGuid = false;
            if (Guid.TryParse(node.NodeId.ToString(), out Guid guidNodeId))
            {
                isGuid = true;
                node.NodeId = guidNodeId;
            }

            string typedNodeId = isDecimal
                ? $"i={node.NodeId.ToString()}"
                : isGuid
                    ? $"g={node.NodeId.ToString()}"
                    : $"s={node.NodeId.ToString()}";

            if (string.IsNullOrEmpty(node.Name))
            {
                node.Name = typedNodeId;
            }

            if (string.IsNullOrEmpty(node.Description))
            {
                node.Description = node.Name;
            }

            _logger.LogDebug("Create node with Id {typedNodeId}, BrowseName {name} and type {type} in namespace with index {namespaceIndex}",
                typedNodeId,
                node.Name,
                (string)node.NodeId.GetType().Name,
                _plcNodeManager.NamespaceIndexes[_namespaceIndex]);

            var variable = CreateBaseVariable(userNodesFolder, node);

            if (node.Parameters != null)
            {
                _simulatedNodes.Add((variable, node.Parameters));
                _simulatedNodesState[variable] = null;
            }

            var nodeId = isString
                ? new NodeId(node.NodeId, _plcNodeManager.NamespaceIndexes[(int)NamespaceType.OpcPlcApplications])
                : (NodeId)node.NodeId;

            yield return PluginNodesHelper.GetNodeWithIntervals(nodeId, _plcNodeManager);
        }

        foreach (var childNode in AddFolders(userNodesFolder, cfgFolder))
        {
            yield return childNode;
        }
    }

    private IEnumerable<NodeWithIntervals> AddFolders(FolderState folder, SimulatedConfigFolder cfgFolder)
    {
        if (cfgFolder.FolderList is null)
        {
            yield break;
        }

        foreach (var childFolder in cfgFolder.FolderList)
        {
            foreach (var node in AddNodes(folder, childFolder))
            {
                yield return node;
            }
        }
    }

    /// <summary>
    /// Creates a new variable.
    /// </summary>
    public BaseDataVariableState CreateBaseVariable(NodeState parent, SimulatedConfigNode node)
    {
        if (!Enum.TryParse(node.DataType, out BuiltInType nodeDataType))
        {
            _logger.LogError($"Value '{node.DataType}' of node '{node.NodeId}' cannot be parsed. Defaulting to 'Int32'");
            node.DataType = "Int32";
        }

        // We have to hard code the conversion here, because AccessLevel is defined as byte in OPC UA lib.
        byte accessLevel;
        try
        {
            accessLevel = (byte)(typeof(AccessLevels).GetField(node.AccessLevel).GetValue(null));
        }
        catch
        {
            _logger.LogError($"AccessLevel '{node.AccessLevel}' of node '{node.Name}' is not supported. Defaulting to 'CurrentReadOrWrite'");
            node.AccessLevel = "CurrentRead";
            accessLevel = AccessLevels.CurrentReadOrWrite;
        }

        return _plcNodeManager.CreateBaseVariable(parent, node.NodeId, node.Name, new NodeId((uint)nodeDataType), node.ValueRank, accessLevel, node.Description, NamespaceType.OpcPlcApplications, node?.Value);
    }
}
