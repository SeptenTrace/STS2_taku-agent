using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using TakuAgentMod.Diagnostics;
using TakuAgentMod.Execution;
using TakuAgentMod.State.Builders;
using TakuAgentMod.State.Snapshots;

namespace TakuAgentMod.Observation;

internal static class ObservationServer
{
    private const int DefaultPort = 15527;

    private static readonly ConcurrentQueue<Action> MainThreadQueue = new();
    private static readonly GameSnapshotBuilder SnapshotBuilder = new();
    private static readonly ActionExecutionLogExporter ActionExecutionLogExporter = new();
    private static readonly GameSnapshotDebugExporter GameSnapshotDebugExporter = new();
    private static readonly object SnapshotStateLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static HttpListener? _listener;
    private static Thread? _serverThread;
    private static bool _started;
    private static GameSnapshot? _lastSnapshot;
    private static string? _lastSnapshotSignature;
    private static int _observationVersion;

    public static void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;

        try
        {
            SceneTree tree = (SceneTree)Engine.GetMainLoop();
            tree.Connect(SceneTree.SignalName.ProcessFrame, Callable.From(ProcessMainThreadQueue));

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{DefaultPort}/");
            _listener.Prefixes.Add($"http://127.0.0.1:{DefaultPort}/");
            _listener.Start();

            _serverThread = new Thread(ServerLoop)
            {
                IsBackground = true,
                Name = "TakuAgentObservationServer"
            };
            _serverThread.Start();

            BattleStateCaptureService.Log($"Observation server started on http://localhost:{DefaultPort}/");
        }
        catch (Exception ex)
        {
            BattleStateCaptureService.Log($"Observation server failed to start: {ex}");
        }
    }

    private static void ProcessMainThreadQueue()
    {
        int processed = 0;
        while (MainThreadQueue.TryDequeue(out Action? action) && processed < 10)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                BattleStateCaptureService.Log($"Observation server main-thread action error: {ex}");
            }

            processed++;
        }
    }

    private static Task<T> RunOnMainThread<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>();
        MainThreadQueue.Enqueue(() =>
        {
            try
            {
                tcs.SetResult(func());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    private static void ServerLoop()
    {
        while (_listener?.IsListening == true)
        {
            try
            {
                HttpListenerContext context = _listener.GetContext();
                ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    private static void HandleRequest(HttpListenerContext context)
    {
        try
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 204;
                response.Close();
                return;
            }

            string path = request.Url?.AbsolutePath ?? "/";
            if (request.HttpMethod == "POST")
            {
                if (path == "/api/v1/actions/execute")
                {
                    HandleExecuteActionRequest(request, response);
                    return;
                }

                SendError(response, 405, "Only /api/v1/actions/execute supports POST.");
                return;
            }

            if (request.HttpMethod != "GET")
            {
                SendError(response, 405, "Only GET is supported for read endpoints.");
                return;
            }

            if (path == "/")
            {
                SendJson(response, new
                {
                    name = "taku-agent-observer",
                    status = "ok",
                    port = DefaultPort,
                    endpoints = new[]
                    {
                        "/api/v1/context",
                        "/api/v1/menu",
                        "/api/v1/observation/compact",
                        "/api/v1/observation/delta",
                        "/api/v1/actions/execute",
                        "/api/v1/capabilities",
                        "/api/v1/actions",
                        "/api/v1/knowledge/current",
                        "/api/v1/combat/actions",
                        "/api/v1/fake-merchant",
                        "/api/v1/bundle-selection",
                        "/api/v1/relic-selection",
                        "/api/v1/crystal-sphere",
                        "/api/v1/overlay"
                    }
                });
                return;
            }

            if (path == "/api/v1/capabilities")
            {
                SendJson(response, new
                {
                    stateFirst = "/api/v1/context",
                    observation = "/api/v1/observation/compact",
                    endpoints = ObservationApiCatalog.Endpoints
                });
                return;
            }

            GameSnapshot snapshot = RunOnMainThread(() => SnapshotBuilder.Build()).GetAwaiter().GetResult();
            ActionSurfaceSnapshot actionSurface = ActionSurfaceBuilder.Build(snapshot);
            CurrentKnowledgeSnapshot knowledge = CurrentKnowledgeBuilder.Build(snapshot);
            (GameSnapshot? previousSnapshot, int version, bool changed) = UpdateObservationState(snapshot);
            ObservationDeltaSnapshot delta = ObservationDeltaBuilder.Build(previousSnapshot, snapshot, version, changed);

            switch (path)
            {
                case "/api/v1/context":
                    SendJson(response, snapshot.Context);
                    return;
                case "/api/v1/menu":
                    RequireSection(response, snapshot.Menu, "Main menu state is unavailable.");
                    return;
                case "/api/v1/observation/compact":
                    SendJson(response, snapshot.CompactObservation);
                    return;
                case "/api/v1/observation/delta":
                    SendJson(response, delta);
                    return;
                case "/api/v1/actions":
                    SendJson(response, actionSurface);
                    return;
                case "/api/v1/run":
                    RequireSection(response, snapshot.Run, "No run is active.");
                    return;
                case "/api/v1/player/summary":
                    RequireSection(response, BuildPlayerSummary(snapshot.Player), "Player state is unavailable.");
                    return;
                case "/api/v1/player/deck":
                    RequireSection(response, snapshot.Player?.Deck, "Deck summary is unavailable.");
                    return;
                case "/api/v1/player/relics":
                    RequireSection(response, snapshot.Player?.Relics, "Relic data is unavailable.");
                    return;
                case "/api/v1/player/potions":
                    RequireSection(response, snapshot.Player?.Potions, "Potion data is unavailable.");
                    return;
                case "/api/v1/player/status":
                    RequireSection(response, snapshot.Player?.Status, "Player status data is unavailable.");
                    return;
                case "/api/v1/knowledge/current":
                    SendJson(response, knowledge);
                    return;
                case "/api/v1/knowledge/cards":
                    SendJson(response, knowledge.Cards);
                    return;
                case "/api/v1/knowledge/relics":
                    SendJson(response, knowledge.Relics);
                    return;
                case "/api/v1/knowledge/potions":
                    SendJson(response, knowledge.Potions);
                    return;
                case "/api/v1/knowledge/status":
                    SendJson(response, knowledge.Statuses);
                    return;
                case "/api/v1/combat/summary":
                    if (snapshot.Combat is null)
                    {
                        SendError(response, 409, "Combat is not active.");
                        return;
                    }

                    SendJson(response, new
                    {
                        snapshot.Combat.RoomType,
                        snapshot.Combat.Round,
                        snapshot.Combat.Side,
                        HandCount = snapshot.Combat.Hand.Count,
                        EnemyCount = snapshot.Combat.Enemies.Count,
                        IncomingDamage = snapshot.Combat.Enemies.Sum(enemy => enemy.IncomingDamage ?? 0),
                        PlayableCards = snapshot.Combat.Hand.Count(card => card.CanPlay),
                        PotionActions = snapshot.Combat.AvailableActions.Count(action => action.ActionType is "use_potion" or "discard_potion"),
                        ActionCount = snapshot.Combat.AvailableActions.Count,
                        snapshot.Combat.Piles
                    });
                    return;
                case "/api/v1/combat/actions":
                    RequireSection(response, snapshot.Combat?.AvailableActions, "Combat actions are unavailable.");
                    return;
                case "/api/v1/combat/hand":
                    RequireSection(response, snapshot.Combat?.Hand, "Combat hand is unavailable.");
                    return;
                case "/api/v1/combat/enemies":
                    RequireSection(response, snapshot.Combat?.Enemies, "Combat enemy data is unavailable.");
                    return;
                case "/api/v1/combat/piles":
                    RequireSection(response, snapshot.Combat?.PileDetails, "Combat pile data is unavailable.");
                    return;
                case "/api/v1/map/summary":
                    RequireSection(response, snapshot.Map, "Map state is unavailable.");
                    return;
                case "/api/v1/event":
                    RequireSection(response, snapshot.Event, "Event state is unavailable.");
                    return;
                case "/api/v1/fake-merchant":
                    RequireSection(response, snapshot.FakeMerchant, "Fake merchant state is unavailable.");
                    return;
                case "/api/v1/shop":
                    RequireSection(response, snapshot.Shop, "Shop state is unavailable.");
                    return;
                case "/api/v1/rest-site":
                    RequireSection(response, snapshot.RestSite, "Rest-site state is unavailable.");
                    return;
                case "/api/v1/rewards":
                    RequireSection(response, snapshot.Rewards, "Rewards state is unavailable.");
                    return;
                case "/api/v1/card-reward":
                    RequireSection(response, snapshot.CardReward, "Card reward state is unavailable.");
                    return;
                case "/api/v1/card-selection":
                    RequireSection(response, snapshot.CardSelection, "Card selection state is unavailable.");
                    return;
                case "/api/v1/bundle-selection":
                    RequireSection(response, snapshot.BundleSelection, "Bundle selection state is unavailable.");
                    return;
                case "/api/v1/relic-selection":
                    RequireSection(response, snapshot.RelicSelection, "Relic selection state is unavailable.");
                    return;
                case "/api/v1/crystal-sphere":
                    RequireSection(response, snapshot.CrystalSphere, "Crystal Sphere state is unavailable.");
                    return;
                case "/api/v1/treasure":
                    RequireSection(response, snapshot.Treasure, "Treasure state is unavailable.");
                    return;
                case "/api/v1/overlay":
                    RequireSection(response, snapshot.Overlay, "Overlay state is unavailable.");
                    return;
                case "/api/v1/state/full":
                    SendJson(response, snapshot);
                    return;
                default:
                    SendError(response, 404, $"Unknown endpoint: {path}");
                    return;
            }
        }
        catch (Exception ex)
        {
            try
            {
                SendError(context.Response, 500, $"Observation server error: {ex.Message}");
            }
            catch
            {
            }
        }
    }

    private static void RequireSection<T>(HttpListenerResponse response, T? data, string errorMessage)
    {
        if (data is null)
        {
            SendError(response, 409, errorMessage);
            return;
        }

        SendJson(response, data);
    }

    private static void HandleExecuteActionRequest(HttpListenerRequest request, HttpListenerResponse response)
    {
        if (!TryParseActionRequest(request, out string? actionType, out Dictionary<string, JsonElement> parameters, out string error))
        {
            SendError(response, 400, error);
            return;
        }

        string correlationId = request.Headers["X-Sts-Correlation-Id"] ?? Guid.NewGuid().ToString("n");
        GameSnapshot beforeSnapshot = RunOnMainThread(() => SnapshotBuilder.Build()).GetAwaiter().GetResult();
        string beforeSignature = BuildSnapshotSignature(beforeSnapshot);
        ActionSurfaceSnapshot beforeActions = ActionSurfaceBuilder.Build(beforeSnapshot);

        ActionExecutionOutcome outcome = RunOnMainThread(() => ActionExecutor.Execute(actionType!, parameters)).GetAwaiter().GetResult();
        (GameSnapshot afterSnapshot, bool changed) = WaitForPostActionSnapshot(actionType!, beforeSignature, beforeSnapshot, outcome.Success);
        (_, int version, bool distinctChanged) = UpdateObservationState(afterSnapshot);

        bool deltaChanged = changed || distinctChanged;
        ObservationDeltaSnapshot delta = ObservationDeltaBuilder.Build(beforeSnapshot, afterSnapshot, version, deltaChanged);
        ActionSurfaceSnapshot currentActions = ActionSurfaceBuilder.Build(afterSnapshot);
        ActionRecoveryDescriptor recovery = BuildRecoveryDescriptor(outcome, afterSnapshot, currentActions);
        string? debugSnapshotPath = outcome.Success ? null : GameSnapshotDebugExporter.Write($"action_failure_{actionType}", afterSnapshot);

        ActionExecutionLogExporter.Append(new ActionExecutionLogEntry(
            Timestamp: DateTimeOffset.Now,
            CorrelationId: correlationId,
            ActionType: actionType!,
            Parameters: SerializeParameters(parameters),
            Success: outcome.Success,
            Message: outcome.Message,
            ReasonCode: recovery.ReasonCode,
            Retryable: recovery.Retryable,
            RunContext: BuildRunContextLog(afterSnapshot.Run),
            PlayerBefore: BuildPlayerResourceLog(beforeSnapshot.Player),
            PlayerAfter: BuildPlayerResourceLog(afterSnapshot.Player),
            ActionSurfaceBefore: BuildActionSurfaceLog(beforeActions),
            ActionSurfaceAfter: BuildActionSurfaceLog(currentActions),
            StateTypeBefore: beforeSnapshot.Context.StateType,
            StateTypeAfter: afterSnapshot.Context.StateType,
            IsStableBefore: beforeSnapshot.Context.IsStable,
            IsStableAfter: afterSnapshot.Context.IsStable,
            ObservationVersion: version,
            ObservationChanged: delta.Changed,
            ChangedSections: delta.ChangedSections,
            Facts: delta.Facts,
            DebugSnapshotPath: debugSnapshotPath));

        response.StatusCode = outcome.Success ? 200 : 409;
        SendJson(response, new
        {
            status = outcome.Success ? "ok" : "error",
            correlationId,
            actionType,
            message = outcome.Message,
            context = afterSnapshot.Context,
            delta,
            recovery,
            actions = outcome.Success ? null : currentActions,
            debugSnapshotPath,
            suggestedNext = outcome.Success ? ["/api/v1/observation/delta"] : recovery.NextQueries
        });
    }

    private static bool TryParseActionRequest(
        HttpListenerRequest request,
        out string? actionType,
        out Dictionary<string, JsonElement> parameters,
        out string error)
    {
        actionType = null;
        parameters = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        error = string.Empty;

        try
        {
            using var stream = request.InputStream;
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "Request body must be a JSON object.";
                return false;
            }

            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (property.NameEquals("actionType") || property.NameEquals("action_type") || property.NameEquals("action"))
                {
                    actionType = property.Value.GetString();
                    continue;
                }

                if (property.NameEquals("parameters"))
                {
                    if (property.Value.ValueKind != JsonValueKind.Object)
                    {
                        error = "'parameters' must be a JSON object.";
                        return false;
                    }

                    foreach (JsonProperty nested in property.Value.EnumerateObject())
                    {
                        parameters[nested.Name] = nested.Value.Clone();
                    }

                    continue;
                }

                parameters[property.Name] = property.Value.Clone();
            }
        }
        catch (Exception ex)
        {
            error = $"Invalid JSON body: {ex.Message}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(actionType))
        {
            error = "Missing 'actionType'.";
            return false;
        }

        return true;
    }

    private static (GameSnapshot Snapshot, bool Changed) WaitForPostActionSnapshot(string actionType, string beforeSignature, GameSnapshot beforeSnapshot, bool shouldWaitForChange)
    {
        if (!shouldWaitForChange)
        {
            return (RunOnMainThread(() => SnapshotBuilder.Build()).GetAwaiter().GetResult(), false);
        }

        GameSnapshot latestSnapshot = beforeSnapshot;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Thread.Sleep(75);
            latestSnapshot = RunOnMainThread(() => SnapshotBuilder.Build()).GetAwaiter().GetResult();
            string latestSignature = BuildSnapshotSignature(latestSnapshot);
            if (!string.Equals(beforeSignature, latestSignature, StringComparison.Ordinal))
            {
                return WaitForStablePostActionState(actionType, beforeSnapshot, latestSnapshot);
            }
        }

        latestSnapshot = RunOnMainThread(() => SnapshotBuilder.Build()).GetAwaiter().GetResult();
        bool changed = !string.Equals(beforeSignature, BuildSnapshotSignature(latestSnapshot), StringComparison.Ordinal);
        return (latestSnapshot, changed);
    }

    private static (GameSnapshot Snapshot, bool Changed) WaitForStablePostActionState(string actionType, GameSnapshot beforeSnapshot, GameSnapshot changedSnapshot)
    {
        GameSnapshot latestSnapshot = changedSnapshot;
        if (!ShouldKeepWaitingForStableState(actionType, beforeSnapshot, latestSnapshot))
        {
            return (latestSnapshot, true);
        }

        for (int attempt = 0; attempt < 40; attempt++)
        {
            Thread.Sleep(100);
            latestSnapshot = RunOnMainThread(() => SnapshotBuilder.Build()).GetAwaiter().GetResult();
            if (!ShouldKeepWaitingForStableState(actionType, beforeSnapshot, latestSnapshot))
            {
                break;
            }
        }

        return (latestSnapshot, true);
    }

    private static bool ShouldKeepWaitingForStableState(string actionType, GameSnapshot beforeSnapshot, GameSnapshot currentSnapshot)
    {
        bool startedInCombat = beforeSnapshot.Context.StateType is "monster" or "elite" or "boss";
        bool stillInCombat = currentSnapshot.Context.StateType is "monster" or "elite" or "boss";

        if (startedInCombat &&
            actionType is "play_card" or "use_potion" or "discard_potion" &&
            stillInCombat &&
            currentSnapshot.Combat is not null &&
            currentSnapshot.Combat.Enemies.Count == 0)
        {
            return true;
        }

        if (actionType == "end_turn" &&
            startedInCombat &&
            stillInCombat &&
            currentSnapshot.Combat is not null)
        {
            if (currentSnapshot.Combat.Side.Equals("enemy", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (currentSnapshot.Combat.Side.Equals("player", StringComparison.OrdinalIgnoreCase) &&
                (currentSnapshot.Combat.AvailableActions.Count == 0 || currentSnapshot.Combat.Hand.Count == 0))
            {
                return true;
            }
        }

        if (actionType == "select_card_reward" &&
            string.Equals(currentSnapshot.Context.StateType, "card_reward", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (actionType == "proceed" &&
            string.Equals(beforeSnapshot.Context.StateType, currentSnapshot.Context.StateType, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (actionType == "choose_map_node" &&
            string.Equals(currentSnapshot.Context.StateType, "unknown", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (actionType == "choose_map_node" &&
            string.Equals(beforeSnapshot.Context.StateType, "map", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(currentSnapshot.Context.StateType, "map", StringComparison.OrdinalIgnoreCase) &&
            currentSnapshot.Map is not null &&
            currentSnapshot.Map.NextOptions.Count == 0)
        {
            return true;
        }

        if (actionType is "proceed" or "choose_event_option" or "confirm_selection" &&
            string.Equals(beforeSnapshot.Context.StateType, "unknown", StringComparison.OrdinalIgnoreCase) == false &&
            string.Equals(currentSnapshot.Context.StateType, "unknown", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static (GameSnapshot? PreviousSnapshot, int Version, bool Changed) UpdateObservationState(GameSnapshot snapshot)
    {
        string signature = BuildSnapshotSignature(snapshot);

        lock (SnapshotStateLock)
        {
            if (_observationVersion == 0)
            {
                _observationVersion = 1;
                _lastSnapshot = snapshot;
                _lastSnapshotSignature = signature;
                return (null, _observationVersion, true);
            }

            bool changed = !string.Equals(_lastSnapshotSignature, signature, StringComparison.Ordinal);
            GameSnapshot? previousSnapshot = _lastSnapshot;
            if (changed)
            {
                _observationVersion++;
                _lastSnapshot = snapshot;
                _lastSnapshotSignature = signature;
            }

            return (previousSnapshot, _observationVersion, changed);
        }
    }

    private static PlayerSummarySnapshot? BuildPlayerSummary(PlayerStateSnapshot? player)
    {
        if (player is null)
        {
            return null;
        }

        return new PlayerSummarySnapshot(
            CharacterId: player.CharacterId,
            Character: player.Character,
            CurrentHp: player.CurrentHp,
            MaxHp: player.MaxHp,
            Block: player.Block,
            Gold: player.Gold,
            Energy: player.Energy,
            MaxEnergy: player.MaxEnergy,
            Stars: player.Stars,
            DeckCount: player.DeckCount,
            UniqueCards: player.Deck.Cards.Count,
            UpgradedCards: player.Deck.Cards.Sum(card => card.UpgradedCopies),
            RelicIds: player.Relics.Select(relic => relic.Id).ToArray(),
            PotionIds: player.Potions.Select(potion => potion.Id).ToArray(),
            Status: player.Status);
    }

    private static string BuildSnapshotSignature(GameSnapshot snapshot)
    {
        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    private static Dictionary<string, string?> SerializeParameters(IReadOnlyDictionary<string, JsonElement> parameters)
    {
        return parameters.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ValueKind == JsonValueKind.String ? pair.Value.GetString() : pair.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static ActionRecoveryDescriptor BuildRecoveryDescriptor(ActionExecutionOutcome outcome, GameSnapshot snapshot, ActionSurfaceSnapshot currentActions)
    {
        if (outcome.Success)
        {
            return new ActionRecoveryDescriptor(
                ReasonCode: "ok",
                Retryable: false,
                NextStep: "read_delta",
                NextQueries: ["/api/v1/observation/delta"]);
        }

        string reasonCode = ClassifyFailureReason(outcome.Message);
        bool retryable = reasonCode is "state_changed" or "transition" or "actions_disabled";
        string nextStep = retryable
            ? "re-read context and actions before retrying"
            : "do not retry blindly; inspect current actions first";

        var nextQueries = new List<string> { "/api/v1/context", "/api/v1/actions" };
        if (snapshot.Context.StateType is "monster" or "elite" or "boss")
        {
            nextQueries.Add("/api/v1/combat/summary");
        }

        return new ActionRecoveryDescriptor(
            ReasonCode: reasonCode,
            Retryable: retryable,
            NextStep: nextStep,
            NextQueries: nextQueries);
    }

    private static RunContextLogSnapshot? BuildRunContextLog(RunSnapshot? run)
    {
        if (run is null)
        {
            return null;
        }

        return new RunContextLogSnapshot(
            Act: run.Act,
            Floor: run.Floor,
            RoomType: run.RoomType,
            MapCol: run.CurrentMapCoord?.Col,
            MapRow: run.CurrentMapCoord?.Row);
    }

    private static PlayerResourceLogSnapshot? BuildPlayerResourceLog(PlayerStateSnapshot? player)
    {
        if (player is null)
        {
            return null;
        }

        return new PlayerResourceLogSnapshot(
            CurrentHp: player.CurrentHp,
            MaxHp: player.MaxHp,
            Block: player.Block,
            Gold: player.Gold,
            Energy: player.Energy,
            MaxEnergy: player.MaxEnergy,
            Stars: player.Stars,
            PotionCount: player.Potions.Count,
            RelicCount: player.Relics.Count);
    }

    private static ActionSurfaceLogSummary BuildActionSurfaceLog(ActionSurfaceSnapshot actionSurface)
    {
        return new ActionSurfaceLogSummary(
            StateType: actionSurface.StateType,
            ActionCount: actionSurface.Actions.Count,
            ActionTypes: actionSurface.Actions.Select(action => action.ActionType).ToArray());
    }

    private static string ClassifyFailureReason(string message)
    {
        string text = message.ToLowerInvariant();
        if (text.Contains("out of range") || text.Contains("missing 'index'") || text.Contains("unknown action"))
        {
            return "invalid_input";
        }

        if (text.Contains("target"))
        {
            return "invalid_target";
        }

        if (text.Contains("not open") || text.Contains("not active") || text.Contains("not in player play phase") || text.Contains("cannot end turn while"))
        {
            return "state_changed";
        }

        if (text.Contains("actions are currently disabled"))
        {
            return "actions_disabled";
        }

        if (text.Contains("inventory is not ready") || text.Contains("dialogue") || text.Contains("not visible"))
        {
            return "transition";
        }

        if (text.Contains("cannot be") || text.Contains("locked") || text.Contains("not enough gold") || text.Contains("empty"))
        {
            return "illegal_action";
        }

        return "execution_error";
    }

    private static void SendJson(HttpListenerResponse response, object data)
    {
        string json = JsonSerializer.Serialize(data, JsonOptions);
        byte[] buffer = Encoding.UTF8.GetBytes(json);
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
        response.Close();
    }

    private static void SendError(HttpListenerResponse response, int statusCode, string message)
    {
        response.StatusCode = statusCode;
        SendJson(response, new
        {
            status = "error",
            statusCode,
            error = message
        });
    }
}

internal sealed record ActionRecoveryDescriptor(
    string ReasonCode,
    bool Retryable,
    string NextStep,
    IReadOnlyList<string> NextQueries);
