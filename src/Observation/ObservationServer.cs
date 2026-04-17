using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using TakuAgentMod.Diagnostics;
using TakuAgentMod.State.Builders;
using TakuAgentMod.State.Snapshots;

namespace TakuAgentMod.Observation;

internal static class ObservationServer
{
    private const int DefaultPort = 15527;

    private static readonly ConcurrentQueue<Action> MainThreadQueue = new();
    private static readonly GameSnapshotBuilder SnapshotBuilder = new();
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
            response.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 204;
                response.Close();
                return;
            }

            if (request.HttpMethod != "GET")
            {
                SendError(response, 405, "Only GET is supported.");
                return;
            }

            string path = request.Url?.AbsolutePath ?? "/";
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
                        "/api/v1/observation/compact",
                        "/api/v1/observation/delta",
                        "/api/v1/capabilities",
                        "/api/v1/actions",
                        "/api/v1/knowledge/current",
                        "/api/v1/combat/actions"
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
                case "/api/v1/treasure":
                    RequireSection(response, snapshot.Treasure, "Treasure state is unavailable.");
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

    private static (GameSnapshot? PreviousSnapshot, int Version, bool Changed) UpdateObservationState(GameSnapshot snapshot)
    {
        string signature = JsonSerializer.Serialize(snapshot, JsonOptions);

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
        SendJson(response, new { error = message });
    }
}
