﻿using PSXAPI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using PSXAPI.Response;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections;
using System.Text;

namespace Poke1Protocol
{
    public class GameClient
    {
        #region From PSXAPI.DLL
        private TimeSpan pingUpdateTime = TimeSpan.FromSeconds(5.0);
        private readonly Timer timer;
        private DateTime lastPingResponseUtc;
        private bool receivedPing;
        private volatile int ping;
        private bool disposedValue;
        private double _lastCheckPing;
        #endregion

        private ProtocolTimeout _movementTimeout = new ProtocolTimeout();
        private ProtocolTimeout _teleportationTimeout = new ProtocolTimeout();
        private ProtocolTimeout _battleTimeout = new ProtocolTimeout();
        private ProtocolTimeout _loadingTimeout = new ProtocolTimeout();
        private ProtocolTimeout _swapTimeout = new ProtocolTimeout();
        private ProtocolTimeout _mountingTimeout = new ProtocolTimeout();
        private ProtocolTimeout _lootBoxTimeout = new ProtocolTimeout();
        private ProtocolTimeout _dialogTimeout = new ProtocolTimeout();
        private ProtocolTimeout _itemUseTimeout = new ProtocolTimeout();
        private ProtocolTimeout _npcBattleTimeout = new ProtocolTimeout();

        private Dictionary<string, int> _guildLogos;

        public string PlayerName { get; private set; }

        private string mapChatChannel = "";
        private string _partyChannel = "";
        private string _guildChannel = "";
        private string _guildName = "";

        private byte _guildEmbedId;

        public string MapName { get; private set; } = "";
        public string AreaName { get; private set; } = "";

        private Queue<PSXAPI.IProto> packets = new Queue<IProto>();

        public PSXAPI.Response.Level Level { get; private set; }

        public Battle ActiveBattle { get; private set; }

        private double _lastRTime;
        private int _lastTime;
        private double _lastCheckTime;
        private double _lastSentMovement;
        private DateTime _lastGameTime;
        private bool _needToSendSync = false;

        private bool _isLoggedIn = false;

        public double RunningForSeconds => GetRunningTimeInSeconds();

        public bool IsConnected { get; private set; }

        public bool IsMapLoaded =>
            Map != null;
        public bool IsTeleporting =>
            _teleportationTimeout.IsActive;
        public bool IsInactive =>
                    _movements.Count == 0
                    && !_movementTimeout.IsActive
                    && !_battleTimeout.IsActive
                    && !_loadingTimeout.IsActive
                    && !_mountingTimeout.IsActive
                    && !_teleportationTimeout.IsActive
                    && !_swapTimeout.IsActive
                    && !_dialogTimeout.IsActive
                    && !_lootBoxTimeout.IsActive
                    && !_itemUseTimeout.IsActive
                && !_npcBattleTimeout.IsActive;
        //&& !_fishingTimeout.IsActive
        //&& !_refreshingPCBox.IsActive
        //&& !_moveRelearnerTimeout.IsActive

        public const string Version = "0.60";

        private GameConnection _connection;

        public LootboxHandler RecievedLootBoxes { get; private set; }

        public Shop OpenedShop { get; private set; }
        public PlayerStats PlayerStats { get; private set; }

        public event Action ConnectionOpened;
        public event Action AreaUpdated;
        public event Action<Exception> ConnectionFailed;
        public event Action<Exception> ConnectionClosed;
        public event Action<PSXAPI.Response.LoginError> AuthenticationFailed;
        public event Action LoggedIn;
        public event Action<string, string> GameTimeUpdated;
        public event Action<string, int, int> PositionUpdated;
        public event Action<string, int, int> TeleportationOccuring;
        public event Action InventoryUpdated;
        public event Action PokemonsUpdated;
        public event Action<string> MapLoaded;
        public event Action<string> SystemMessage;
        public event Action<string> LootBoxMessage;
        public event Action<LootboxHandler> RecievedLootBox;
        public event Action<string> LogMessage;
        public event Action BattleStarted;
        public event Action<string> BattleMessage;
        public event Action BattleEnded;
        public event Action<List<PokedexPokemon>> PokedexUpdated;
        public event Action<PlayerInfos> PlayerUpdated;
        public event Action<PlayerInfos> PlayerAdded;
        public event Action<PlayerInfos> PlayerRemoved;
        public event Action<Level, Level> LevelChanged;
        public event Action RefreshChannelList;
        public event Action<string, string, string> ChannelMessage;
        public event Action<string, string, string> PrivateMessage;
        public event Action<string, string, string> LeavePrivateMessage;
        public event Action<string> DialogOpened;
        public event Action<PSXAPI.Response.Payload.LootboxRoll[], PSXAPI.Response.LootboxType> LootBoxOpened;
        public event Action<Guid> Evolving;
        public event Action<PSXAPI.Response.Payload.PokemonMoveID, int, Guid> LearningMove;
        public event Action<List<PlayerQuest>> QuestsUpdated;
        public event Action<PSXAPI.Response.Path> ReceivedPath;
        public event Action NpcReceieved;
        public event Action<Shop> ShopOpened;
        public event Action<string> ServerCommandException;
        public event Action BattleUpdated;
        public event Action<Npc> MoveToBattleWithNpc;
        public string[] DialogContent { get; private set; }
        private Queue<object> _dialogResponses = new Queue<object>();
        public bool IsScriptActive { get; private set; }
        public PSXAPI.Response.Time LastTimePacket { get; private set; }
        public string PokeTime { get; private set; }
        public string GameTime { get; private set; }
        public string Weather { get; private set; }
        public int PlayerX { get; private set; }
        public int PlayerY { get; private set; }
        public List<Pokemon> Team { get; private set; }
        public List<PlayerQuest> Quests { get; private set; }
        public List<PlayerEffect> Effects { get; private set; }
        public List<InventoryItem> Items { get; private set; }
        public Dictionary<string, ChatChannel> Channels { get; private set; }
        public List<string> Conversations { get; }
        public Dictionary<string, PlayerInfos> Players { get; }
        private Dictionary<string, PlayerInfos> _removedPlayers { get; }
        private DateTime _updatePlayers;
        public Random Rand { get; }

        private MapClient _mapClient;

        public Map Map { get; private set; }
        public int Money { get; private set; }
        public int Gold { get; private set; }
        public Dictionary<int, string> Badges { get; private set; }

        private List<Direction> _movements;
        private List<PSXAPI.Request.Move> _movementPackets;
        private Direction? _slidingDirection;
        private bool _surfAfterMovement;

        public bool IsInBattle { get; private set; }
        public bool IsOnGround { get; private set; }
        public bool IsSurfing { get; private set; }
        public bool IsBiking { get; private set; }
        public bool IsLoggedIn => _isLoggedIn && IsConnected && _connection.IsConnected;
        public bool CanUseCut { get; private set; }
        public bool CanUseSmashRock { get; private set; }
        public int PokedexOwned { get; private set; }
        public int PokedexSeen { get; private set; }
        public bool AreNpcReceived { get; private set; }

        private ScriptRequestType _currentScriptType { get; set; }
        private Script _currentScript { get; set; }
        public List<PokedexPokemon> PokedexPokemons { get; private set; }
        private List<Script> Scripts { get; }
        private List<Script> _cachedScripts { get; }
        private MapUsers _cachedNerbyUsers { get; set; }
        public GameClient(GameConnection connection, MapConnection mapConnection)
        {
            _mapClient = new MapClient(mapConnection, this);
            _mapClient.ConnectionOpened += MapClient_ConnectionOpened;
            _mapClient.ConnectionClosed += MapClient_ConnectionClosed;
            _mapClient.ConnectionFailed += MapClient_ConnectionFailed;
            _mapClient.MapLoaded += MapClient_MapLoaded;

            _connection = connection;
            _connection.PacketReceived += OnPacketReceived;
            _connection.Connected += OnConnectionOpened;
            _connection.Disconnected += OnConnectionClosed;

            RecievedLootBoxes = new LootboxHandler();
            RecievedLootBoxes.LootBoxMessage += RecievedLootBoxMsg;
            RecievedLootBoxes.RecievedBox += RecievedLootBoxes_RecievedBox;
            RecievedLootBoxes.BoxOpened += RecievedLootBoxes_BoxOpened;

            Rand = new Random();
            lastPingResponseUtc = DateTime.UtcNow;
            timer = new Timer(new TimerCallback(Timer), null, PingUpdateTime, PingUpdateTime);
            disposedValue = false;
            _movements = new List<Direction>();
            _movementPackets = new List<PSXAPI.Request.Move>();
            Team = new List<Pokemon>();
            Items = new List<InventoryItem>();
            Channels = new Dictionary<string, ChatChannel>();
            Conversations = new List<string>();
            Players = new Dictionary<string, PlayerInfos>();
            _removedPlayers = new Dictionary<string, PlayerInfos>();
            PokedexPokemons = new List<PokedexPokemon>();
            _cachedScripts = new List<Script>();
            Scripts = new List<Script>();
            Effects = new List<PlayerEffect>();
            Quests = new List<PlayerQuest>();
            _guildLogos = new Dictionary<string, int>();
            Badges = new Dictionary<int, string>();
        }

        private double GetRunningTimeInSeconds()
        {
            var totalTime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
            return totalTime.TotalSeconds;
        }

        private void AddDefaultChannels()
        {
            Channels.Add("General", new ChatChannel("default", "General", ""));
            Channels.Add("System", new ChatChannel("default", "General", ""));
            Channels.Add("Map", new ChatChannel("default", "Map", ""));
            Channels.Add("Party", new ChatChannel("default", "Party", ""));
            Channels.Add("Battle", new ChatChannel("default", "Battle", ""));
            Channels.Add("Guild", new ChatChannel("default", "Guild", ""));
        }


        public void Move(Direction direction)
        {
            _movements.Add(direction);
        }

        public void PushDialogAnswer(int index)
        {
            _dialogResponses.Enqueue(index);
        }

        public void PushDialogAnswer(string text)
        {
            _dialogResponses.Enqueue(text);
        }

        public void Open()
        {
            _connection.Connect();
        }

        public void Close(Exception error = null)
        {
            _connection.Close(error);
        }

        public void ClearPath() { _movements.Clear(); _movementPackets.Clear(); _lastSentMovement = RunningForSeconds; }

        public void Update()
        {
            _mapClient.Update();
            _connection.Update();

            _swapTimeout.Update();
            _movementTimeout.Update();
            _teleportationTimeout.Update();
            _battleTimeout.Update();
            _dialogTimeout.Update();
            _loadingTimeout.Update();
            _lootBoxTimeout.Update();
            _itemUseTimeout.Update();
            _mountingTimeout.Update();
            _npcBattleTimeout.Update();

            UpdateScript();
            UpdatePlayers();
            UpdateRegularPacket();
            UpdateMovement();
            UpdateTime();
            RecievedLootBoxes.UpdateFreeLootBox();
        }

        private void UpdateTime()
        {
            if (LastTimePacket != null)
            {
                if (_lastCheckTime + 3 < RunningForSeconds)
                {
                    _lastCheckTime = RunningForSeconds;
                    GameTime = LastTimePacket.GameDayTime.ToString() + " " + GetGameTime(LastTimePacket.GameTime, LastTimePacket.TimeFactor, _lastGameTime);
                    PokeTime = GetGameTime(LastTimePacket.GameTime, LastTimePacket.TimeFactor, _lastGameTime).Replace(" PM", "").Replace(" AM", "");
                    Weather = LastTimePacket.Weather.ToString();
                    GameTimeUpdated?.Invoke(GameTime, Weather);
                }
            }
        }

        // IDK POKEONE CHECK SOMETHING LIKE BELOW.
        private void UpdateRegularPacket()
        {

            if (_needToSendSync && IsInBattle && !_battleTimeout.IsActive)
            {
                Resync(false);
                _needToSendSync = false;
                _battleTimeout.Set(1500);
            }

            if (RunningForSeconds > _lastCheckPing + 2)
            {
                _lastCheckPing = RunningForSeconds;
                if (IsLoggedIn && !string.IsNullOrEmpty(PlayerName))
                {
                    if (Ping >= 5000)
                    {
                        if (PingUpdateTime.TotalSeconds != 2.0)
                        {
                            PingUpdateTime = TimeSpan.FromSeconds(5.0);
                        }
                    }
                    else
                    {
                        if (PingUpdateTime.TotalSeconds != 2.0)
                        {
                            PingUpdateTime = TimeSpan.FromSeconds(5.0);
                        }
                    }
                }
            }

            if (RunningForSeconds > _lastSentMovement + 0.6f && _movementPackets.Count > 0)
                SendMovemnetPackets();
        }

        // Don't ask me it is PokeOne's way lol...
        private void SendMovemnetPackets()
        {
            _lastSentMovement = RunningForSeconds;
            if (_movementPackets.Count > 0)
            {
                List<PSXAPI.Request.MoveAction> list = new List<PSXAPI.Request.MoveAction>();
                int i = 0;
                while (i < _movementPackets.Count)
                {
                    if (i + 1 >= _movementPackets.Count)
                    {
                        goto IL_FF;
                    }
                    if (_movementPackets[i].Actions[0] != PSXAPI.Request.MoveAction.TurnDown || _movementPackets[i + 1].Actions[0] != PSXAPI.Request.MoveAction.Down)
                    {
                        if (_movementPackets[i].Actions[0] != PSXAPI.Request.MoveAction.TurnLeft || _movementPackets[i + 1].Actions[0] != PSXAPI.Request.MoveAction.Left)
                        {
                            if (_movementPackets[i].Actions[0] != PSXAPI.Request.MoveAction.TurnRight || _movementPackets[i + 1].Actions[0] != PSXAPI.Request.MoveAction.Right)
                            {
                                if (_movementPackets[i].Actions[0] != PSXAPI.Request.MoveAction.TurnUp || _movementPackets[i + 1].Actions[0] != PSXAPI.Request.MoveAction.Up)
                                {
                                    goto IL_FF;
                                }
                            }
                        }
                    }
                    IL_118:
                    i++;
                    continue;
                    IL_FF:
                    list.Add(_movementPackets[i].Actions[0]);
                    goto IL_118;
                }
                if (list.Count == 2)
                    Console.WriteLine("Yes I am working like PokeOne :D");
                SendProto(new PSXAPI.Request.Move
                {
                    Actions = list.ToArray(),
                    Map = _movementPackets[0].Map,
                    X = _movementPackets[0].X,
                    Y = _movementPackets[0].Y
                });
                _movementPackets.Clear();
            }
        }

        private void CheckEvolving()
        {
            if (IsInBattle) return;
            var evolvingPoke = Team.FirstOrDefault(pok => pok.CanEvolveTo > PSXAPI.Response.Payload.PokemonID.missingno);

            if (evolvingPoke != null)
            {
                OnEvolving(evolvingPoke);
            }
        }

        private void CheckLearningMove()
        {
            if (IsInBattle) return;
            var learningPoke = Team.FirstOrDefault(pok => pok.LearnableMoves != null && pok.LearnableMoves.Length > 0);

            if (learningPoke != null)
            {
                OnLearningMove(learningPoke);
            }
        }

        private void UpdateMovement()
        {
            if (!IsMapLoaded) return;


            if (!_movementTimeout.IsActive && _movements.Count > 0)
            {
                //SendMovemnetPackets();
                Direction direction = _movements[0];
                _movements.RemoveAt(0);
                // before applying the movement
                int fromX = PlayerX;
                int fromY = PlayerY;
                if (ApplyMovement(direction))
                {
                    SendMovement(direction.ToMoveActions(), fromX, fromY); // PokeOne sends the (x,y) without applying the movement(but it checks the collisions) to the server.
                    _movementTimeout.Set(IsBiking ? 225 : 375);
                    if (Map.HasLink(PlayerX, PlayerY))
                    {
                        _teleportationTimeout.Set();
                    }
                    else
                    {
                        Npc battler = Map.Npcs.FirstOrDefault(npc => npc.CanBattle && npc.IsInLineOfSight(PlayerX, PlayerY));
                        if (battler != null)
                        {
                            battler.CanBattle = false;
                            LogMessage?.Invoke("The NPC " + (battler.NpcName ?? battler.Id.ToString()) + " saw us, interacting...");
                            int distanceFromBattler = DistanceBetween(PlayerX, PlayerY, battler.PositionX, battler.PositionY);
                            _npcBattleTimeout.Set(Rand.Next(1000, 2000) + distanceFromBattler);
                            ClearPath();
                            MoveToBattleWithNpc?.Invoke(battler);
                        }
                    }
                    _lootBoxTimeout.Cancel();
                }
                if (_movements.Count == 0 && _surfAfterMovement)
                {
                    _movementTimeout.Set(Rand.Next(750, 2000));
                }
            }

            if (!_movementTimeout.IsActive && _movements.Count == 0 && _surfAfterMovement)
            {
                _surfAfterMovement = false;
                UseSurf();
            }
        }

        private void UpdatePlayers()
        {
            if (_updatePlayers < DateTime.UtcNow)
            {
                foreach (string playerName in Players.Keys.ToArray())
                {
                    if (Players[playerName].IsExpired())
                    {
                        var player = Players[playerName];

                        PlayerRemoved?.Invoke(player);

                        if (_removedPlayers.ContainsKey(player.Name))
                            _removedPlayers[player.Name] = player;
                        else
                            _removedPlayers.Add(player.Name, player);

                        Players.Remove(playerName);
                    }
                }
                _updatePlayers = DateTime.UtcNow.AddSeconds(5);
            }
        }

        private void UpdateScript()
        {
            if (IsScriptActive && !_dialogTimeout.IsActive && Scripts.Count > 0)
            {
                var script = Scripts[0];
                Scripts.RemoveAt(0);

                DialogContent = script.Data;

                var type = script.Type;
                switch (type)
                {
                    case ScriptRequestType.Choice:
                        if (_dialogResponses.Count <= 0)
                        {
                            SendScriptResponse(script.ScriptID, "0");
                        }
                        else
                        {
                            SendScriptResponse(script.ScriptID, GetNextDialogResponse().ToString());
                        }
                        _dialogTimeout.Set();
                        break;
                    case ScriptRequestType.WalkNpc:
                        SendScriptResponse(script.ScriptID, "");
                        _dialogTimeout.Set();
                        break;
                    case ScriptRequestType.WalkUser:
                        SendScriptResponse(script.ScriptID, "");
                        _dialogTimeout.Set();
                        break;
                    case ScriptRequestType.WaitForInput:
                        SendScriptResponse(script.ScriptID, "");
                        _dialogTimeout.Set();
                        break;
                    case ScriptRequestType.Unfreeze:
                        if (script.Text is null)
                        {
                            _dialogResponses.Clear();
                        }
                        break;
                    case ScriptRequestType.Shop:
                        OpenedShop = new Shop(script.Data, script.ScriptID);
                        ShopOpened?.Invoke(OpenedShop);
                        break;
                    default:
#if DEBUG
                        Console.WriteLine($"UNKNOWN TYPE SCRIPT: {script.Type}");
#endif
                        break;

                }
            }
        }

        private int GetNextDialogResponse()
        {
            if (_dialogResponses.Count > 0)
            {
                object response = _dialogResponses.Dequeue();
                if (response is int)
                {
                    return (int)response;
                }
                else if (response is string)
                {
                    string text = ((string)response).ToUpperInvariant();
                    for (int i = 0; i < DialogContent.Length; ++i)
                    {
                        var option = Regex.Replace(DialogContent[i].ToUpperInvariant(), @"\[(\/|.\w+)\]", "");
                        if (option.ToUpperInvariant().Equals(text))
                        {
                            return i;
                        }
                    }
                }
            }
            return 0;
        }

        private bool ApplyMovement(Direction direction)
        {
            int destinationX = PlayerX;
            int destinationY = PlayerY;
            bool isOnGround = IsOnGround;
            bool isSurfing = IsSurfing;

            direction.ApplyToCoordinates(ref destinationX, ref destinationY);
            Map.MoveResult result = Map.CanMove(direction, destinationX, destinationY, isOnGround, isSurfing, CanUseCut, CanUseSmashRock);
            if (Map.ApplyMovement(direction, result, ref destinationX, ref destinationY, ref isOnGround, ref isSurfing))
            {
                PlayerX = destinationX;
                PlayerY = destinationY;
                IsOnGround = isOnGround;
                IsSurfing = isSurfing;
                CheckArea();
                if (result == Map.MoveResult.Icing)
                {
                    _movements.Insert(0, direction);
                }

                if (result == Map.MoveResult.Sliding)
                {
                    int slider = Map.GetSlider(destinationX, destinationY);
                    if (slider != -1)
                    {
                        _slidingDirection = Map.SliderToDirection(slider);
                    }
                }

                if (_slidingDirection != null)
                {
                    _movements.Insert(0, direction);
                }

                return true;
            }
            return false;
        }

        private void MapClient_ConnectionOpened()
        {
#if DEBUG
            Console.WriteLine("[+++] Connected to the map server");
#endif
            IsConnected = true;
            ConnectionOpened?.Invoke();
        }

        private void MapClient_ConnectionFailed(Exception ex)
        {
            ConnectionFailed?.Invoke(ex);
            if (_connection.IsConnected)
                Close();
        }

        private void MapClient_ConnectionClosed(Exception ex)
        {
            Close(ex);
        }

        private void OnPacketReceived(string packet)
        {
#if DEBUG
            Console.WriteLine("Receiving Packet [<]: " + packet);
#endif
            ProcessPacket(packet);
        }

        private void OnConnectionOpened()
        {
#if DEBUG
            Console.WriteLine("[+++] Connection opened");
#endif
            _mapClient.Open();
            lastPingResponseUtc = DateTime.UtcNow;
            receivedPing = true;
        }

        private void OnConnectionClosed(Exception ex)
        {
            _isLoggedIn = false;
            _mapClient.Close();
            if (!IsConnected)
            {
#if DEBUG
                Console.WriteLine("[---] Connection failed");
#endif
                ConnectionFailed?.Invoke(ex);
            }
            else
            {
                IsConnected = false;
#if DEBUG
                Console.WriteLine("[---] Connection closed");
#endif
                ConnectionClosed?.Invoke(ex);
            }
            if (!disposedValue)
            {
                timer.Dispose();
                disposedValue = true;
            }
        }

        private void Timer(object obj)
        {
            if (IsConnected && receivedPing)
            {
                receivedPing = false;
                SendProto(new PSXAPI.Request.Ping
                {
                    DateTimeUtc = DateTime.UtcNow
                });
            }
        }

        private void SendSwapPokemons(int poke1, int poke2)
        {
            PSXAPI.Request.Reorder packet = new PSXAPI.Request.Reorder
            {
                Pokemon = Team[poke1 - 1].PokemonData.Pokemon.UniqueID,
                Position = poke2
            };
            SendProto(packet);
        }

        private void SendScriptResponse(Guid id, string response)
        {
            SendProto(new PSXAPI.Request.Script
            {
                Response = response,
                ScriptID = id
            });
        }

        private void SendTalkToNpc(Guid npcId)
        {
            SendProto(new PSXAPI.Request.Talk
            {
                NpcID = npcId
            });
        }

        public void SendProto(PSXAPI.IProto proto)
        {
            var array = Proto.Serialize(proto);
            if (array == null)
            {
                return;
            }
            string packet = Convert.ToBase64String(array);
            packet = proto._Name + " " + packet;
            SendPacket(packet);
        }
        public void SendPacket(string packet)
        {
#if DEBUG
            Console.WriteLine("Sending Packet [>]: " + packet);
#endif
            _connection.Send(packet);
        }

        private void SendRequestGuildLogo(string name, int version = 0)
        {
            if (_guildLogos.ContainsKey(name.ToUpperInvariant()))
            {
                if (_guildLogos[name.ToUpperInvariant()] != version)
                {
                    _guildLogos[name.ToUpperInvariant()] = version;
                    SendProto(new PSXAPI.Request.GuildEmblem
                    {
                        Name = name
                    });
                }
            }
            else
            {
                _guildLogos.Add(name.ToUpperInvariant(), version);
                SendProto(new PSXAPI.Request.GuildEmblem
                {
                    Name = name
                });
            }
        }

        public void SendMessage(string channel, string message)
        {
            if (channel == "Map")
                channel = mapChatChannel;
            if (channel == "Party")
                channel = _partyChannel;
            if (channel == "Guild")
                channel = _guildChannel;
            List<Guid> pokeList = new List<Guid>();
            SendProto(new PSXAPI.Request.ChatMessage
            {
                Channel = channel,
                Message = message,
                Pokemon = pokeList.ToArray()
            });
        }

        public void CloseChannel(string channel)
        {
            if (Channels.Any(c => c.Key == channel) || channel.StartsWith("map:"))
            {
                SendProto(new PSXAPI.Request.ChatJoin
                {
                    Channel = channel,
                    Action = PSXAPI.Request.ChatJoinAction.Leave
                });
            }
        }

        public void CloseConversation(string pmName)
        {
            if (Conversations.Contains(pmName))
            {
                SendProto(new PSXAPI.Request.Message
                {
                    Event = PSXAPI.Request.MessageEvent.Closed,
                    Name = pmName,
                    Text = ""
                });
            }
        }

        private void SendMovement(PSXAPI.Request.MoveAction[] actions, int fromX, int fromY)
        {
            OpenedShop = null;
            PlayerStats = null;

            var movePacket = new PSXAPI.Request.Move
            {
                Actions = actions,
                Map = MapName,
                X = fromX,
                Y = fromY
            };
            _movementPackets.Add(movePacket);
            //SendProto(movePacket);
        }

        public bool OpenLootBox(PSXAPI.Request.LootboxType type)
        {
            if (RecievedLootBoxes != null)
            {
                if (RecievedLootBoxes.TotalLootBoxes > 0)
                {
                    SendOpenLootBox(type);
                    _lootBoxTimeout.Set();
                    return true;
                }
            }
            return false;
        }


        public void TalkToNpc(Npc npc)
        {
            SendTalkToNpc(npc.Id);
            _dialogTimeout.Set();
        }

        private void SendCharacterCustomization(int gender, int skin, int hair, int haircolour, int eyes)
        {
            var packet = new PSXAPI.Request.Script
            {
                Response = string.Concat(new string[]
                {
                    gender.ToString(),
                    ",",
                    skin.ToString(),
                    ",",
                    eyes.ToString(),
                    ",",
                    hair.ToString(),
                    ",",
                    haircolour.ToString()
                }),
                ScriptID = _currentScript.ScriptID
            };
            SendProto(packet);
        }

        private void SendOpenLootBox(PSXAPI.Request.LootboxType type)
        {
            SendProto(new PSXAPI.Request.Lootbox
            {
                Action = PSXAPI.Request.LootboxAction.Open,
                Type = type
            });
        }

        private void SendJoinChannel(string channel)
        {
            SendProto(new PSXAPI.Request.ChatJoin
            {
                Channel = channel,
                Action = PSXAPI.Request.ChatJoinAction.Join
            });
        }

        public void SendPrivateMessage(string nickname, string text)
        {
            if (!Conversations.Contains(nickname))
                Conversations.Add(nickname);
            SendProto(new Message
            {
                Event = MessageEvent.Message,
                Name = nickname,
                Text = text
            });
        }

        private void SendAttack(int id, int selected, int opponent, bool megaEvo)
        {
            SendProto(new PSXAPI.Request.BattleBroadcast
            {
                RequestID = ActiveBattle.ResponseID,
                Message = string.Concat(new string[]
                {
                    "1|",
                    PlayerName,
                    "|",
                    opponent.ToString(),
                    "|",
                    ActiveBattle.Turn.ToString(),
                    "|",
                    (selected - 1).ToString(),
                    "|",
                    ActiveBattle.AttackTargetType(id)
                })
            });

            SendProto(new PSXAPI.Request.BattleMove
            {
                MoveID = id,
                Target = opponent,
                Position = selected,
                RequestID = ActiveBattle.ResponseID,
                MegaEvo = megaEvo,
                ZMove = false
            });
        }

        private void SendRunFromBattle(int selected)
        {
            SendProto(new PSXAPI.Request.BattleRun
            {
                RequestID = ActiveBattle.ResponseID
            });

            SendProto(new PSXAPI.Request.BattleBroadcast
            {
                RequestID = ActiveBattle.ResponseID,
                Message = string.Concat(new string[]
                {
                    "5|",
                    PlayerName,
                    "|0|",
                    ActiveBattle.Turn.ToString(),
                    "|",
                    (selected - 1).ToString()
                })
            });
        }

        private void SendChangePokemon(int currentPos, int newPos)
        {
            SendProto(new PSXAPI.Request.BattleSwitch
            {
                RequestID = ActiveBattle.ResponseID,
                Position = currentPos,
                NewPosition = newPos
            });
        }

        private void SendUseItemInBattle(int id, int targetId, int selected, int moveTarget = 0)
        {
            SendProto(new PSXAPI.Request.BattleBroadcast
            {
                RequestID = ActiveBattle.ResponseID,
                Message = string.Concat(new string[]
                    {
                        "2|",
                        PlayerName,
                        "|",
                        targetId.ToString(),
                        "|",
                        ActiveBattle.Turn.ToString(),
                        "|",
                        (selected - 1).ToString()
                    })
            });

            SendProto(new PSXAPI.Request.BattleItem
            {
                Item = id,
                RequestID = ActiveBattle.ResponseID,
                Target = targetId,
                TargetMove = moveTarget,
                Position = selected
            });
        }

        public void SendAcceptEvolution(Guid evolvingPokemonUid)
        {
            SendProto(new PSXAPI.Request.Evolve
            {
                Accept = true,
                Pokemon = evolvingPokemonUid
            });
        }

        public void SendCancelEvolution(Guid evolvingPokemonUid)
        {
            SendProto(new PSXAPI.Request.Evolve
            {
                Accept = false,
                Pokemon = evolvingPokemonUid
            });
        }

        private void SendUseItem(int id, int pokemonUid = 0, int moveId = 0)
        {
            var foundPoke = Team.Find(poke => poke.Uid == pokemonUid || poke.Uid * -1 == pokemonUid);
            SendProto(new PSXAPI.Request.UseItem
            {
                Item = id,
                Move = moveId,
                Pokemon = foundPoke != null ? foundPoke.UniqueID : default(Guid)
            });
        }

        private void SendUseMount()
        {
            SendProto(new PSXAPI.Request.Mount());
        }

        public bool UseMount()
        {
            if (IsMapLoaded && Map.IsOutside)
            {
                SendUseMount();
                return true;
            }
            return false;
        }

        private void SendShopPokemart(Guid scriptId, int itemId, int quantity)
        {
            var response = itemId + "," + quantity + "," + "0";
            SendScriptResponse(scriptId, response);
        }

        public bool BuyItem(int itemId, int quantity)
        {
            if (OpenedShop != null && OpenedShop.Items.Any(item => item.Id == itemId))
            {
                _itemUseTimeout.Set();
                SendShopPokemart(OpenedShop.ScriptId, itemId, quantity);
                CloseShop();
                return true;
            }
            return false;
        }

        public bool CloseShop()
        {
            if (OpenedShop != null)
            {
                SendScriptResponse(OpenedShop.ScriptId, "");
                OpenedShop = null;
                return true;
            }
            return false;
        }

        private void SendPlayerStatsRequest(string username = null)
        {
            SendProto(new PSXAPI.Request.Stats
            {
                Username = username
            });
        }

        // Asking trainer card info
        public bool AskForPlayerStats()
        {
            if (PlayerStats is null)
            {
                SendPlayerStatsRequest();
                _dialogTimeout.Set(Rand.Next(1500, 2000));
                return true;
            }
            return false;
        }

        public TimeSpan PingUpdateTime
        {
            get => pingUpdateTime;
            set
            {
                if (PingUpdateTime == value)
                {
                    return;
                }
                pingUpdateTime = value;
                timer.Change(TimeSpan.FromSeconds(0.0), value);
            }
        }

        public bool AutoCompleteQuest(PlayerQuest quest)
        {
            if (string.IsNullOrEmpty(quest.Id) || !quest.AutoComplete) return false;
            SendProto(new PSXAPI.Request.Quest
            {
                Action = PSXAPI.Request.QuestAction.Complete,
                ID = quest.Id
            });
            return true;
        }

        public bool RequestPathForInCompleteQuest(PlayerQuest quest)
        {
            if (quest.Target == Guid.Empty || quest.Completed || quest.IsRequestedForPath) return false;
            quest.UpdateRequests(true);
            SendProto(new PSXAPI.Request.Path
            {
                Request = quest.Target
            });
            return true;
        }

        public bool RequestPathForCompletedQuest(PlayerQuest quest)
        {
            if (quest.QuestData.TargetCompleted == Guid.Empty || !quest.Completed || quest.IsRequestedForPath) return false;
            quest.UpdateRequests(true);
            SendProto(new PSXAPI.Request.Path
            {
                Request = quest.QuestData.TargetCompleted
            });
            return true;
        }

        public int Ping
        {
            get
            {
                if (!IsConnected)
                {
                    return -1;
                }
                TimeSpan t = DateTime.UtcNow - lastPingResponseUtc;
                if (t > PingUpdateTime + TimeSpan.FromSeconds(2.0))
                {
                    return (int)t.TotalMilliseconds;
                }
                return ping;
            }
        }

        private void ProcessPacket(string packet)
        {
            var data = packet.Split(" ".ToCharArray());

            byte[] array = Convert.FromBase64String(data[1]);
            var type = Type.GetType($"PSXAPI.Response.{data[0]}, PSXAPI");

            if (type is null)
            {
                Console.WriteLine("Received Unknown Response: " + data[0]);
            }
            else
            {
                var proto = typeof(Proto).GetMethod("Deserialize").MakeGenericMethod(new Type[]
                {
                    type
                }).Invoke(null, new object[]
                {
                    array
                }) as IProto;

                if (proto is PSXAPI.Response.Ping)
                {
                    ping = (int)(DateTime.UtcNow - ((PSXAPI.Response.Ping)proto).DateTimeUtc).TotalMilliseconds;
                    lastPingResponseUtc = DateTime.UtcNow;
                    receivedPing = true;
                }
                else
                {
                    switch (proto)
                    {
                        case PSXAPI.Response.Greeting gr:
#if DEBUG
                            Console.WriteLine($"Server Version: {gr.ServerVersion}\nUsers Online: {gr.UsersOnline}");
#endif
                            break;
                        case PSXAPI.Response.Request req:
                            OnRequests(req);
                            break;
                        case PSXAPI.Response.GuildEmblem em:

                            break;
                        case PSXAPI.Response.Badges bd:
                            Console.WriteLine("BADGES: " + bd.All.Length);
                            break;
                        case PSXAPI.Response.Stats stats:
                            OnPlayerStats(stats);
                            break;
                        case PSXAPI.Response.Badge badge:
                            if (badge.Active)
                                Badges.Add(badge.Id, BadgeFromID(badge.Id));
                            break;
                        case PSXAPI.Response.Level lvl:
                            OnLevel(lvl);
                            break;
                        case PSXAPI.Response.LoginQueue queue:
                            LogMessage?.Invoke("[Login Queue]: Average Wait-Time: " + queue.EstimatedTime.FormatTimeString());
                            break;
                        case PSXAPI.Response.Money money:
                            Money = (int)money.Game;
                            Gold = (int)money.Gold;
                            InventoryUpdated?.Invoke();
                            break;
                        case PSXAPI.Response.Move move:
                            OnPlayerPosition(move, true);
                            break;
                        case PSXAPI.Response.Mount mtP:
                            OnMountUpdate(mtP);
                            break;
                        case PSXAPI.Response.Area area:
                            OnAreaPokemon(area);
                            break;
                        case PSXAPI.Response.ChatJoin join:
                            OnChannels(join);
                            break;
                        case PSXAPI.Response.ChatMessage msg:
                            OnChatMessage(msg);
                            break;
                        case PSXAPI.Response.Message pm:
                            OnPrivateMessage(pm);
                            break;
                        case PSXAPI.Response.Time time:
                            OnUpdateTime(time);
                            break;
                        case PSXAPI.Response.Login login:
                            CheckLogin(login);
                            break;
                        case PSXAPI.Response.Sync sync:
                            OnPlayerSync(sync);
                            break;
                        case PSXAPI.Response.DebugMessage dMsg:
                            if (dMsg.Message.Contains("Command Exception"))
                            {
                                ServerCommandException?.Invoke(dMsg.Message);
                                break;
                            }
                            SystemMessage?.Invoke(dMsg.Message);
                            break;
                        case PSXAPI.Response.InventoryPokemon iPoke:
                            OnTeamUpdated(new PSXAPI.Response.InventoryPokemon[] { iPoke });
                            break;
                        case PSXAPI.Response.DailyLootbox dl:
                            OnLootBoxRecieved(dl);
                            break;
                        case PSXAPI.Response.Lootbox bx:
                            OnLootBoxRecieved(bx);
                            break;
                        case PSXAPI.Response.MapUsers mpusers:
                            OnUpdatePlayer(mpusers);
                            break;
                        case PSXAPI.Response.Inventory Inv:
                            OnInventoryUpdate(Inv);
                            break;
                        case PSXAPI.Response.InventoryItem invItm:
                            UpdateItems(new PSXAPI.Response.InventoryItem[] { invItm });
                            break;
                        case PSXAPI.Response.Pokemon poke:
                            
                            break;
                        case PSXAPI.Response.Script sc:
                            OnScript(sc);
                            break;
                        case PSXAPI.Response.Battle battle:
                            OnBattle(battle);
                            break;
                        case PSXAPI.Response.PokedexUpdate dexUpdate:
                            OnPokedexUpdate(dexUpdate);
                            break;
                        case PSXAPI.Response.Reorder reorder:
                            OnReorderPokemon(reorder);
                            break;
                        case PSXAPI.Response.Evolve evolve:
                            OnEvolved(evolve);
                            break;
                        case PSXAPI.Response.Effect effect:
                            OnEffects(effect);
                            break;
                        case PSXAPI.Response.UseItem itm:
                            OnUsedItem(itm);
                            break;
                        case PSXAPI.Response.Quest quest:
                            OnQuest(new[] { quest });
                            break;
                        case PSXAPI.Response.Path path:
                            OnPathReceived(path);
                            break;
                        case PSXAPI.Response.Party party:
                            if (party.ChatID.ToString() != _partyChannel)
                            {
                                _partyChannel = party.ChatID.ToString();
                                SendJoinChannel(_partyChannel);
                            }
                            break;
                        case PSXAPI.Response.Guild guild:
                            OnGuild(guild);
                            break;
                        case PSXAPI.Response.Logout lg:
#if DEBUG
                            Console.WriteLine("[SYSTEM COMMAND]Got command to logout..");
#endif
                            Close();
                            break;
                    }
#if DEBUG

                    Console.WriteLine(proto._Name);
#endif
                }
            }
        }

        private void OnPlayerStats(Stats stats)
        {
            var playerStats = new PlayerStats(stats);
            if (playerStats.PlayerName == PlayerName || stats.Result == StatsResult.Self)
            {
                PlayerStats = playerStats;
                Badges.Clear();
                foreach (var id in PlayerStats.Badges)
                    Badges.Add(id, BadgeFromID(id));
            }
        }

        private void OnRequests(Request req)
        {

            if (req.Type.ToString().Contains("Decline")) return;

            SystemMessage?.Invoke($"{req.Sender} sent {req.Type.ToString().ToLowerInvariant()} request.");
            LogMessage?.Invoke("Saying no to the request....");
            PSXAPI.Request.RequestType type = PSXAPI.Request.RequestType.None;

            switch (req.Type)
            {
                case RequestType.Battle:
                    type = PSXAPI.Request.RequestType.Battle;
                    break;
                case RequestType.Friend:
                    type = PSXAPI.Request.RequestType.Friend;
                    break;
                case RequestType.Trade:
                    type = PSXAPI.Request.RequestType.Trade;
                    break;
                case RequestType.Guild:
                    type = PSXAPI.Request.RequestType.Guild;
                    break;
                case RequestType.Party:
                    type = PSXAPI.Request.RequestType.Party;
                    break;
            }

            SendProto(new PSXAPI.Request.Request
            {
                Accepted = false,
                Sender = req.Sender,
                Type = type
            });
        }

        private void OnGuild(Guild guild)
        {
            if (guild.Chat.ToString() != _guildChannel)
            {
                _guildChannel = guild.Chat.ToString();
                SendJoinChannel(_guildChannel);
            }
            if (_guildName != guild.Name)
            {
                _guildName = guild.Name;
                _guildEmbedId = guild.EmblemId;
                SendRequestGuildLogo(guild.Name, _guildEmbedId);
            }
            if (_guildEmbedId != guild.EmblemId)
            {
                _guildEmbedId = guild.EmblemId;
                SendRequestGuildLogo(guild.Name, _guildEmbedId);
            }
        }

        private void OnAreaPokemon(Area area)
        {
            if (area.Pokemon is null || area.Pokemon.Length <= 0) return;
            foreach (var poke in area.Pokemon)
            {
                var findDexPok = PokedexPokemons.Find(o => o.Id == poke.PokemonID);
                if (findDexPok != null)
                {
                    findDexPok.UpdateStatus(poke.Pokedex);
                }
            }
            PokedexUpdated?.Invoke(PokedexPokemons);
        }

        private void OnUsedItem(UseItem useItem)
        {
            switch (useItem.Result)
            {
                case UseItemResult.Success:

                    break;
                case UseItemResult.Failed:
                    SystemMessage?.Invoke($"Failed to use {ItemsManager.Instance.ItemClass.items.FirstOrDefault(i => i.ID == useItem.Item).Name}");
                    break;
                case UseItemResult.InvalidItem:
                case UseItemResult.InvalidPokemon:

                    break;

            }
        }

        private void OnPathReceived(Path path)
        {
            if (path.Links != null)
                ReceivedPath?.Invoke(path);
        }

        private void OnEffects(Effect effect)
        {
            if (effect.Effects is null) return;

            if (effect.Type == EffectUpdateType.All)
            {
                Effects.Clear();
                foreach (var ef in effect.Effects)
                {
                    Effects.Add(new PlayerEffect(ef, DateTime.UtcNow));
                }
            }
            else if (effect.Type == EffectUpdateType.AddOrUpdate)
            {
                foreach (var ef in effect.Effects)
                {
                    var foundEf = Effects.Find(e => e.UID == ef.UID);
                    if (foundEf != null)
                    {
                        foundEf = new PlayerEffect(ef, DateTime.UtcNow);
                    }
                    else
                    {
                        Effects.Add(new PlayerEffect(ef, DateTime.UtcNow));
                    }
                }
            }
            else if (effect.Type == EffectUpdateType.Remove)
            {
                foreach (var ef in effect.Effects)
                {
                    var foundEf = Effects.Find(e => e.UID == ef.UID);
                    if (foundEf != null)
                    {
                        Effects.Remove(foundEf);
                    }
                }
            }
        }

        private void OnEvolved(Evolve evolve)
        {
            if (evolve.Result == EvolutionResult.Success)
                SystemMessage?.Invoke($"{PokemonManager.Instance.GetNameFromEnum(evolve.Previous)} evolved into " +
                    $"{PokemonManager.Instance.GetNameFromEnum(evolve.Pokemon.Pokemon.Payload.PokemonID)}");
            else if (evolve.Result == EvolutionResult.Failed)
                SystemMessage?.Invoke("Failed to evolve!");
            else if (evolve.Result == EvolutionResult.Canceled)
                SystemMessage?.Invoke($"{PokemonManager.Instance.GetNameFromEnum(evolve.Previous)} did not evolve!");

            if (evolve.Pokemon != null)
                OnTeamUpdated(new[] { evolve.Pokemon });
        }

        private void OnPrivateMessage(Message pm)
        {
            if (pm != null)
            {
                if (pm.Event == MessageEvent.Message)
                {
                    if (!Conversations.Contains(pm.Name))
                        Conversations.Add(pm.Name);
                    if (!string.IsNullOrEmpty(pm.Text))
                        PrivateMessage?.Invoke(pm.Name, pm.Name, pm.Text);
                }
                else
                {
                    Conversations.Remove(pm.Name);
                    var removeMsg = pm.Event == MessageEvent.Closed ? $"{pm.Name} closed the Chat Window." : $"{pm.Name} is offline.";
                    LeavePrivateMessage?.Invoke(pm.Name, "System", removeMsg);
                }
            }
        }

        private void OnChatMessage(ChatMessage msg)
        {

            if (string.IsNullOrEmpty(msg.Channel))
            {
                foreach (var m in msg.Messages)
                    SystemMessage?.Invoke(m.Message);

                return;
            }
            var rchannelName = msg.Channel;

            if ((msg.Channel.ToLower() == mapChatChannel.ToLower() || msg.Channel.StartsWith("map:")) && !string.IsNullOrEmpty(msg.Channel))
            {
                msg.Channel = "Map";
            }
            if (msg.Channel.ToLower() == _partyChannel.ToLower() && !string.IsNullOrEmpty(msg.Channel))
            {
                msg.Channel = "Party";
            }
            if (msg.Channel.ToLower() == _guildChannel.ToLower() && !string.IsNullOrEmpty(msg.Channel))
            {
                msg.Channel = "Guild";
            }

            if (Channels.ContainsKey(msg.Channel) && !string.IsNullOrEmpty(msg.Channel))
            {
                var channelName = msg.Channel;
                foreach (var message in msg.Messages)
                {

                    ChannelMessage?.Invoke(channelName, message.Username, message.Message);
                }
            }
            else
            {
                Channels.Add(msg.Channel, new ChatChannel("", msg.Channel, rchannelName));
                RefreshChannelList?.Invoke();
            }
        }

        private void OnUpdatePlayer(MapUsers mpusers)
        {
            var data = mpusers.Users;
            DateTime expiration = DateTime.UtcNow.AddSeconds(20);

            bool isNewPlayer = false;
            foreach (var user in data)
            {
                PlayerInfos player;
                if (Players.ContainsKey(user.Username))
                {
                    player = Players[user.Username];
                    player.Update(user, expiration);
                }
                else if (_removedPlayers.ContainsKey(user.Username))
                {
                    player = _removedPlayers[user.Username];
                    player.Update(user, expiration);
                }
                else
                {
                    isNewPlayer = true;
                    player = new PlayerInfos(user, expiration);
                }
                player.Updated = DateTime.UtcNow;

                Players[player.Name] = player;

                if (isNewPlayer || player.Actions.Any(ac => ac.Action == PSXAPI.Response.Payload.MapUserAction.Enter))
                {
                    if (!string.IsNullOrEmpty(player?.GuildName))
                        SendRequestGuildLogo(player.GuildName);
                    PlayerAdded?.Invoke(player);
                }
                else if (player.Actions.Any(ac => ac.Action == PSXAPI.Response.Payload.MapUserAction.Leave))
                {
                    PlayerRemoved?.Invoke(player);
                    if (_removedPlayers.ContainsKey(player.Name))
                        _removedPlayers[player.Name] = player;
                    else
                        _removedPlayers.Add(player.Name, player);
                    Players.Remove(player.Name);
                }
                else if (player.Actions.Any(ac => ac.Action != PSXAPI.Response.Payload.MapUserAction.Leave))
                {
                    PlayerUpdated?.Invoke(player);
                }
            }
        }

        private void OnReorderPokemon(Reorder reorder)
        {
            if (reorder.Box == 0)
            {
                if (reorder.Pokemon != null)
                {
                    if (reorder.Pokemon.Length > 0)
                    {
                        var i = 0;
                        foreach (var id in reorder.Pokemon)
                        {
                            var poke = Team.Find(x => x.PokemonData.Pokemon.UniqueID == id);

                            if (i <= Team.Count - 1)
                                poke.UpdatePosition(i + 1);

                            i++;
                        }
                    }
                }
            }

            SortTeam();

            PokemonsUpdated?.Invoke();
            //else PC box..
        }

        private void SortTeam()
        {
            Team = Team.OrderBy(poke => poke.Uid).ToList();
        }

        private void OnBattle(PSXAPI.Response.Battle battle)
        {
            ClearPath();
            _slidingDirection = null;
            PlayerStats = null;

            IsInBattle = !battle.Ended;

            if (ActiveBattle != null && !ActiveBattle.OnlyInfo)
            {
                ActiveBattle.UpdateBattle(PlayerName, battle, Team);
            }
            else
            {
                ActiveBattle = new Battle(PlayerName, battle, Team);
            }

            if (!ActiveBattle.OnlyInfo && IsInBattle && IsLoggedIn && ActiveBattle.Turn <= 1 && !ActiveBattle.IsUpdated)
            {
                // encounter message coz the server doesn't send it.
                _needToSendSync = true;
                var firstEncounterMessage = ActiveBattle.IsWild ? ActiveBattle.IsShiny ?
                        $"A wild shiny {PokemonManager.Instance.Names[ActiveBattle.OpponentId]} has appeared!"
                        : $"A wild {PokemonManager.Instance.Names[ActiveBattle.OpponentId]} has appeared!" : ActiveBattle.IsShiny ?
                        $"Opponent sent out shiny {PokemonManager.Instance.Names[ActiveBattle.OpponentId]}!"
                        : $"Opponent sent out {PokemonManager.Instance.Names[ActiveBattle.OpponentId]}!";
                BattleMessage?.Invoke(firstEncounterMessage);
                _battleTimeout.Set(Rand.Next(4000, 6000));
                BattleStarted?.Invoke();                
            }

            if (ActiveBattle != null)
                ActiveBattle.AlreadyCaught = IsCaughtById(ActiveBattle.OpponentId);

            OnBattleMessage(battle.Log);
            BattleUpdated?.Invoke();
        }

        private void ActiveBattleMessage(string txt)
            => BattleMessage?.Invoke(txt);

        private void OnBattleMessage(string[] logs)
        {
            ActiveBattle.ProcessLog(logs, Team, ActiveBattleMessage);

            PokemonsUpdated?.Invoke();

            if (ActiveBattle.IsFinished)
            {
                _battleTimeout.Set(ActiveBattle.PokemonCount > 1 ? Rand.Next(3500, 6000) : Rand.Next(2000, 5000));
            }
            else
            {
                _battleTimeout.Set(ActiveBattle.PokemonCount > 1 ? Rand.Next(3500, 5000) : Rand.Next(2000, 4000));
            }
            if (ActiveBattle.IsFinished)
            {
                IsInBattle = false;
                ActiveBattle = null;
                BattleEnded?.Invoke();
            }
        }

        private void OnMountUpdate(Mount mtP)
        {
            if (mtP.MountType == MountType.Bike || mtP.MountType == MountType.Pokemon)
            {
                IsBiking = true;
                IsOnGround = true;
            }
            else if (mtP.MountType == MountType.None)
            {
                IsBiking = false;
                IsOnGround = true;
                IsSurfing = false;
            }
            else if (mtP.MountType == MountType.Surfing)
            {
                IsBiking = false;
                IsOnGround = false;
                IsSurfing = true;
            }
        }

        private void OnScript(PSXAPI.Response.Script data)
        {
            if (data is null) return;

            var id = data.ScriptID;
            var type = data.Type;

#if DEBUG
            if (data.Text != null)
            {
                foreach (var s in data.Text)
                {
                    Console.WriteLine(s.Text);
                }
            }
#endif

            var active = true;

            _currentScriptType = type;
            _currentScript = data;
            if (IsLoggedIn && _cachedScripts.Count > 0 && IsMapLoaded) // processing _cachedScripts, these scripts are received before getting fully logged int!
            {
                switch (type)
                {
                    case ScriptRequestType.Choice:
                        if (data.Text != null)
                        {
                            if (data.Text.ToList().Any(x => x.Text.Contains("start your journey"))
                                || data.Data.ToList().Any(x => x == "Kanto" || x == "Johto"))
                            {
                                SendProto(new PSXAPI.Request.Script
                                {
                                    Response = "0",
                                    ScriptID = data.ScriptID
                                });
                                active = false;
                            }
                        }
                        break;
                    case ScriptRequestType.Unfreeze:
                        if (data.Text != null)
                        {
                            foreach (var text in data.Text)
                            {
                                var st = text.Text;
                                var index = st.IndexOf("(");
                                var scriptType = st.Substring(0, index);
                                switch (scriptType)
                                {
                                    case "setlos":
                                        var command = st.Replace(scriptType, "").Replace("(", "").Replace(")", "");
                                        var npcId = Guid.Parse(command.Split(',')[0]);
                                        var los = Convert.ToInt32(command.Split(',')[1]);
                                        if (Map.OriginalNpcs.Find(x => x.Id == npcId) != null && Map.Npcs.Find(x => x.Id == npcId) != null)
                                        {
                                            Map.OriginalNpcs.Find(x => x.Id == npcId).UpdateLos(los);
                                            Map.Npcs.Find(x => x.Id == npcId).UpdateLos(los);
                                        }
                                        active = false;
                                        break;
                                    case "enablenpc":
                                        command = st.Replace(scriptType, "").Replace("(", "").Replace(")", "");
                                        npcId = Guid.Parse(command.Split(',')[0]);
                                        var hide = command.Split(',')[1] == "0";

                                        if (Map.OriginalNpcs.Find(x => x.Id == npcId) != null && Map.Npcs.Find(x => x.Id == npcId) != null)
                                        {
                                            Map.OriginalNpcs.Find(x => x.Id == npcId).Visible(hide);
                                            Map.Npcs.Remove(Map.OriginalNpcs.Find(x => x.Id == npcId));
                                        }
                                        active = false;
                                        break;
                                }
                            }
                        }
                        break;
                }
            }
            else if (!IsLoggedIn || !IsMapLoaded)
                _cachedScripts.Add(data);

            //_cachedScripts.Remove(data);

            if (_cachedScripts.Count <= 0 && active && IsLoggedIn && data.Text != null)
            {
                foreach (var scriptText in data.Text)
                {
                    if (!scriptText.Text.EndsWith(")") && scriptText.Text.IndexOf("(") == -1)
                        DialogOpened?.Invoke(Regex.Replace(scriptText.Text, @"\[(\/|.\w+)\]", ""));
                    else
                        ProcessScriptMessage(scriptText.Text);
                }
            }


            IsScriptActive = active && IsLoggedIn && _cachedScripts.Count <= 0;
            if (active && IsLoggedIn && _cachedScripts.Count <= 0)
            {
                _dialogTimeout.Set(Rand.Next(1500, 4000));
                Scripts.Add(data);
            }
        }

        private void ProcessScriptMessage(string text)
        {
            var st = text;
            var index = st.IndexOf("(");
            var scriptType = st.Substring(0, index);
            switch (scriptType)
            {
                case "setlos":
                    var command = st.Replace(scriptType, "").Replace("(", "").Replace(")", "");
                    var npcId = Guid.Parse(command.Split(',')[0]);
                    var los = Convert.ToInt32(command.Split(',')[1]);
                    if (Map.OriginalNpcs.Find(x => x.Id == npcId) != null)
                    {
                        Map.OriginalNpcs.Find(x => x.Id == npcId).UpdateLos(los);
                    }
                    break;
                case "enablenpc":
                    command = st.Replace(scriptType, "").Replace("(", "").Replace(")", "");
                    npcId = Guid.Parse(command.Split(',')[0]);
                    var hide = command.Split(',')[1] == "0";
                    if (Map.OriginalNpcs.Find(x => x.Id == npcId) != null)
                    {
                        Map.OriginalNpcs.Find(x => x.Id == npcId).Visible(hide);
                    }
                    break;
            }
            OnNpcs(Map.OriginalNpcs);
        }

        private void OnLootBoxRecieved(IProto dl, TimeSpan? timeSpan = null)
        {
            if (dl is PSXAPI.Response.DailyLootbox db)
                RecievedLootBoxes.HandleDaily(db, timeSpan.GetValueOrDefault() == null ? db.Timer : timeSpan);
            else if (dl is PSXAPI.Response.Lootbox lb)
                RecievedLootBoxes.HandleLootbox(lb);
        }

        private void RecievedLootBoxes_BoxOpened(PSXAPI.Response.Payload.LootboxRoll[] rewards, LootboxType type)
        {
            _lootBoxTimeout.Set(Rand.Next(3000, 4000));
            LootBoxOpened?.Invoke(rewards, type);
        }

        private void RecievedLootBoxes_RecievedBox(LootboxHandler obj)
        {
            RecievedLootBox?.Invoke(obj);
            _lootBoxTimeout.Set(Rand.Next(1500, 2000));
        }

        private void RecievedLootBoxMsg(string ob) => LootBoxMessage?.Invoke(ob);

        private void OnPlayerSync(PSXAPI.Response.Sync sync)
        {
            OnPlayerPosition(sync, false);
        }

        private void CheckLogin(PSXAPI.Response.Login login)
        {
            if (login.Result == PSXAPI.Response.LoginResult.Success)
            {
                OnLoggedIn(login);
            }
            else
            {
                AuthenticationFailed?.Invoke(login.Error);
                Close();
            }
        }

        private void OnPlayerPosition(IProto proto, bool sync = true)
        {
            if (proto is PSXAPI.Response.Move movement)
            {
                if (MapName != movement.Map || PlayerX != movement.X || PlayerY != movement.Y)
                {
                    TeleportationOccuring?.Invoke(movement.Map, movement.X, movement.Y);

                    PlayerX = movement.X;
                    PlayerY = movement.Y;

                    _teleportationTimeout.Cancel();

                    string prev = mapChatChannel;
                    mapChatChannel = "map:" + movement.Map;
                    if (mapChatChannel != prev && MapName != movement.Map)
                    {
                        if (Channels.ContainsKey("Map") && !string.IsNullOrEmpty(Channels["Map"].ChannelName))
                            CloseChannel(Channels["Map"].ChannelName);

                        SendJoinChannel(mapChatChannel);
                    }

                    LoadMap(movement.Map);
                }
                if (sync)
                    Resync(MapName == movement.Map);
            }
            else if (proto is PSXAPI.Response.Sync syncP)
            {
                if (MapName != syncP.Map || PlayerX != syncP.PosX || PlayerY != syncP.PosY)
                {
                    TeleportationOccuring?.Invoke(syncP.Map, syncP.PosX, syncP.PosY);

                    PlayerX = syncP.PosX;
                    PlayerY = syncP.PosY;

                    _teleportationTimeout.Cancel();

                    string prev = mapChatChannel;
                    mapChatChannel = "map:" + syncP.Map;
                    if (mapChatChannel != prev && MapName != syncP.Map)
                    {
                        if (Channels.ContainsKey("Map") && !string.IsNullOrEmpty(Channels["Map"].ChannelName))
                            CloseChannel(Channels["Map"].ChannelName);
                        SendJoinChannel(mapChatChannel);
                    }

                    LoadMap(syncP.Map);
                }
                if (sync)
                    Resync(MapName == syncP.Map);

            }

            CheckArea();
        }

        private void OnPokedexData(PSXAPI.Response.Pokedex data)
        {
            if (data.Entries != null)
            {
                foreach (var en in data.Entries)
                {
                    PokedexPokemons.Add(new PokedexPokemon(en));
                }
                PokedexOwned = data.Caught;
                PokedexSeen = data.Seen;
            }
            PokedexUpdated?.Invoke(PokedexPokemons);
        }

        private void OnPokedexUpdate(PSXAPI.Response.PokedexUpdate data)
        {
            if (data.Entry != null)
            {
                var dexPoke = new PokedexPokemon(data.Entry);
                if (PokedexPokemons.Count > 0)
                {
                    var findDex = PokedexPokemons.Find(x => x.Id == dexPoke.Id);
                    if (findDex != null)
                    {
                        PokedexPokemons.Insert(PokedexPokemons.IndexOf(findDex), dexPoke);
                        PokedexPokemons.Remove(findDex);
                    }
                    else
                    {
                        PokedexPokemons.Add(dexPoke);
                    }
                }
                else
                    PokedexPokemons.Add(dexPoke);
            }
            RequestArea(MapName, AreaName);
            PokedexUpdated?.Invoke(PokedexPokemons);
        }

        public void RequestArea(string Map, string Areaname)
        {
            SendProto(new PSXAPI.Request.Area
            {
                Map = Map.ToLowerInvariant(),
                AreaName = Areaname
            });
        }

        public bool IsCaughtById(int id)
        {
            return PokedexPokemons.Count > 0 && PokedexPokemons.Any(x => x.Caught && x.Id == id);
        }

        public bool IsCaughtByName(string name)
        {
            return PokedexPokemons.Count > 0 && PokedexPokemons.Any(x => x.Caught && x.Name.ToLowerInvariant() == name.ToLowerInvariant());
        }

        private void OnLoggedIn(PSXAPI.Response.Login login)
        {
            _isLoggedIn = true;
            PlayerName = login.Username;

            Console.WriteLine("[Login] Authenticated successfully");
            LoggedIn?.Invoke();

            if (_currentScript != null)
            {
                switch (_currentScriptType)
                {
                    case ScriptRequestType.Customize:
                        //Character!

                        SendCharacterCustomization(0, Rand.Next(0, 3), Rand.Next(0, 13), Rand.Next(0, 27), Rand.Next(0, 4));

                        break;
                    case ScriptRequestType.Unfreeze:

                        break;
                }
            }
            if (login.Level != null)
                OnLevel(login.Level);
            OnTeamUpdated(login.Inventory.ActivePokemon);
            OnInventoryUpdate(login.Inventory);
            OnMountUpdate(login.Mount);
            OnPlayerPosition(login.Position, false);

            if (login.Pokedex != null)
            {
                OnPokedexData(login.Pokedex);
            }

            if (login.Battle != null)
                OnBattle(login.Battle);

            if (login.Quests != null && login.Quests.Length > 0)
                OnQuest(login.Quests);

            if (login.Effects != null)
                OnEffects(login.Effects);

            if (login.Time != null)
            {
                OnUpdateTime(login.Time);
            }

            if (login.DailyLootbox != null)
                OnLootBoxRecieved(login.DailyLootbox, login.DailyReset);
            if (login.Lootboxes != null)
                foreach (var lootBox in login.Lootboxes)
                    OnLootBoxRecieved(lootBox);
            if (login.NearbyUsers != null)
                _cachedNerbyUsers = login.NearbyUsers;

            if (login.Party != null)
            {
                if (login.Party.ChatID.ToString() != _partyChannel)
                {
                    _partyChannel = login.Party.ChatID.ToString();
                    SendJoinChannel(_partyChannel);
                }
            }
            if (login.Guild != null)
            {
                OnGuild(login.Guild);
            }


            AddDefaultChannels();
            if (RecievedLootBoxes.TotalLootBoxes > 0)
            {
                RecievedLootBox?.Invoke(RecievedLootBoxes);
            }
        }

        private void OnQuest(Quest[] quests)
        {
            foreach (var quest in quests)
            {
                var foundQuest = Quests.Find(q => q.Id == quest.ID);
                if (foundQuest != null)
                    Quests.Remove(foundQuest);

                Quests.Add(new PlayerQuest(quest));
            }
            QuestsUpdated?.Invoke(Quests);
        }

        private void OnLevel(Level data)
        {
            var preLevel = Level;
            Level = data;
            LevelChanged?.Invoke(preLevel, Level);
        }
        private void OnChannels(PSXAPI.Response.ChatJoin join)
        {
#if DEBUG
            Console.WriteLine("Received Channel: " + join.Channel);
#endif
            if (join.Result == PSXAPI.Response.ChatJoinResult.Joined)
            {
                var rchannelName = join.Channel;
                if (join.Channel.ToLower() == mapChatChannel.ToLower() || join.Channel.StartsWith("map:"))
                {
                    join.Channel = "Map";
                    if (Channels.ContainsKey(join.Channel))
                    {
                        if (Channels[join.Channel].ChannelName != rchannelName)
                        {                            
                            Channels[join.Channel] = new ChatChannel("default", join.Channel, rchannelName);
                        }
                    }
                }
                if (join.Channel.ToLower() == _partyChannel.ToLower())
                {
                    join.Channel = "Party";
                    if (Channels.ContainsKey(join.Channel))
                    {
                        if (Channels[join.Channel].ChannelName != rchannelName)
                        {
                            Channels[join.Channel] = new ChatChannel("default", join.Channel, rchannelName);
                        }
                    }
                }
                if (join.Channel.ToLower() == _guildChannel.ToLower())
                {
                    join.Channel = "Guild";
                    if (Channels.ContainsKey(join.Channel))
                    {
                        if (Channels[join.Channel].ChannelName != rchannelName)
                        {
                            Channels[join.Channel] = new ChatChannel("default", join.Channel, rchannelName);
                        }
                    }
                }
                if (!Channels.ContainsKey(join.Channel))
                {
                    Channels.Add(join.Channel, new ChatChannel("", join.Channel, rchannelName));
                }
            }
            else if (join.Result == ChatJoinResult.Left)
            {
                if (Channels.ContainsKey(join.Channel))
                    Channels.Remove(join.Channel);
            }
            RefreshChannelList?.Invoke();
        }
        private void OnTeamUpdated(PSXAPI.Response.InventoryPokemon[] pokemons)
        {
            if (pokemons is null) return;
            if (pokemons.Length <= 1)
            {
#if DEBUG
                Console.WriteLine("Received One Pokemon Data!");
#endif
                if (Team.Count > 0)
                {
                    var poke = new Pokemon(pokemons[0]);
                    var foundPoke = Team.Find(x => x.PokemonData.Pokemon.UniqueID == pokemons[0].Pokemon.UniqueID);
                    if (foundPoke != null)
                    {
                        foundPoke.UpdatePokemonData(pokemons[0]);
                    }
                    else if (poke.PokemonData.Box == 0)
                    {
                        Team.Add(poke);
                    }
                }
                else
                {
                    Team.Clear();
                    foreach (var poke in pokemons)
                    {
                        Team.Add(new Pokemon(poke));
                    }
                }
            }
            else
            {
                Team.Clear();
                foreach (var poke in pokemons)
                {
                    Team.Add(new Pokemon(poke));
                }
            }

            CheckEvolving();
            CheckLearningMove();

            CanUseCut = HasCutAbility();
            CanUseSmashRock = HasRockSmashAbility();

            if (_swapTimeout.IsActive)
            {
                _swapTimeout.Set(Rand.Next(500, 1000));
            }

            SortTeam();
            PokemonsUpdated?.Invoke();
        }

        private void OnLearningMove(Pokemon learningPoke)
        {
            LearningMove?.Invoke(learningPoke.LearnableMoves[0],
                learningPoke.Uid, learningPoke.UniqueID);
            _itemUseTimeout.Cancel();
        }

        private void OnEvolving(Pokemon poke)
        {
            Evolving?.Invoke(poke.UniqueID);
        }

        public bool SwapPokemon(int pokemon1, int pokemon2)
        {
            if (IsInBattle || pokemon1 < 1 || pokemon2 < 1 || Team.Count < pokemon1 || Team.Count < pokemon2 || pokemon1 == pokemon2)
            {
                return false;
            }
            if (!_swapTimeout.IsActive)
            {
                SendSwapPokemons(pokemon1, pokemon2);
                _swapTimeout.Set();
                return true;
            }
            return false;
        }

        private void OnInventoryUpdate(PSXAPI.Response.Inventory data)
        {
            Money = (int)data.Money;
            Gold = (int)data.Gold;
            UpdateItems(data.Items);
        }

        private void UpdateItems(PSXAPI.Response.InventoryItem[] items)
        {
            if (items != null && items.Length == 1)
            {
                var item = new InventoryItem(items[0]);
                if (Items.Count > 0)
                {
                    var foundItem = Items.Find(x => x.Id == item.Id);
                    if (foundItem != null)
                    {
                        if (item.Quantity > 0)
                            Items[Items.IndexOf(foundItem)] = item;
                        else
                            Items.Remove(foundItem);
                    }
                    else
                    {
                        Items.Add(item);
                    }
                }
                else
                {
                    Items.Clear();
                    Items.Add(item);
                }
            }
            else if (items != null)
            {
                Items.Clear();
                foreach (var item in items)
                {
                    Items.Add(new InventoryItem(item));
                }
            }
            InventoryUpdated?.Invoke();
        }

        private void OnUpdateTime(PSXAPI.Response.Time time)
        {
            LastTimePacket = time;
            _lastGameTime = DateTime.UtcNow;
            GameTime = time.GameDayTime.ToString() + " " + GetGameTime(time.GameTime, time.TimeFactor, _lastGameTime);
            PokeTime = GetGameTime(LastTimePacket.GameTime, LastTimePacket.TimeFactor, _lastGameTime).Replace(" PM", "").Replace(" AM", "");
            Weather = time.Weather.ToString();
            GameTimeUpdated?.Invoke(GameTime, Weather);
        }

        public void SendAuthentication(string username, string password)
        {
            SendProto(new PSXAPI.Request.Login
            {
                Name = username,
                Password = password,
                Platform = PSXAPI.Request.ClientPlatform.PC,
                Version = Version
            });
        }
        public string GetGameTime(TimeSpan time, double sc, DateTime dt)
        {
            TimeSpan timeSpan = time;
            timeSpan = timeSpan.Add(TimeSpan.FromSeconds((DateTime.UtcNow - dt).TotalSeconds * sc));
            if (RunningForSeconds >= _lastRTime + 300f)
            {
                SendProto(new PSXAPI.Request.Time());
                _lastRTime = RunningForSeconds;
            }

            if (_lastTime != time.Hours)
                SendProto(new PSXAPI.Request.Time());
            _lastTime = timeSpan.Hours;

            string result;
            if (timeSpan.Hours >= 12)
            {
                if (timeSpan.Hours == 12)
                {
                    result = "12:" + timeSpan.Minutes.ToString().PadLeft(2, '0') + " PM";
                }
                else
                {
                    result = (timeSpan.Hours - 12).ToString() + ":" + timeSpan.Minutes.ToString().PadLeft(2, '0') + " PM";
                }
            }
            else if (timeSpan.Hours == 0)
            {
                result = "12:" + timeSpan.Minutes.ToString().PadLeft(2, '0') + " AM";
            }
            else
            {
                result = timeSpan.Hours.ToString() + ":" + timeSpan.Minutes.ToString().PadLeft(2, '0') + " AM";
            }
            return result;
        }

        public void RunFromBattle(int selected = 0)
        {
            if (selected == 0)
                selected = ActiveBattle.CurrentBattlingPokemonIndex;
            SendRunFromBattle(selected);
            _battleTimeout.Set(ActiveBattle.PokemonCount > 1 ? Rand.Next(2500, 4000) : Rand.Next(1500, 3000));
        }

        public void UseAttack(int number, int selectedPoke = 0, int opponent = 0, bool megaEvolve = false)
        {
            if (selectedPoke == 0)
                selectedPoke = ActiveBattle.CurrentBattlingPokemonIndex;
            SendAttack(number, selectedPoke, opponent, megaEvolve);
            _battleTimeout.Set(ActiveBattle.PokemonCount > 1 ? Rand.Next(3500, 4000) : Rand.Next(1500, 3000));
        }

        public void UseItem(int id, int pokemonUid = 0, int moveId = 0)
        {
            if (!(pokemonUid >= 0 && pokemonUid <= 6) || !HasItemId(id))
            {
                return;
            }
            var item = GetItemFromId(id);
            if (item == null || item.Quantity == 0)
            {
                return;
            }
            if (pokemonUid == 0) // simple use
            {
                pokemonUid = ActiveBattle.CurrentBattlingPokemonIndex;
                if (!_itemUseTimeout.IsActive && !IsInBattle && item.CanBeUsedOutsideOfBattle)
                {
                    SendUseItem(item.Id);
                    _itemUseTimeout.Set();
                }
                else if (!_battleTimeout.IsActive && IsInBattle && item.CanBeUsedInBattle && !item.CanBeUsedOnPokemonInBattle)
                {
                    SendUseItemInBattle(item.Id, pokemonUid, pokemonUid, moveId);
                    _battleTimeout.Set(ActiveBattle.PokemonCount > 1 ? Rand.Next(3500, 4000) : Rand.Next(1500, 3000));
                }
            }
            else // use item on pokemon
            {
                if (!_itemUseTimeout.IsActive && !IsInBattle && item.CanBeUsedOnPokemonOutsideOfBattle)
                {
                    SendUseItem(item.Id, pokemonUid, moveId);
                    _itemUseTimeout.Set();
                }
                else if (!_battleTimeout.IsActive && IsInBattle && item.CanBeUsedOnPokemonInBattle)
                {
                    SendUseItemInBattle(item.Id, pokemonUid, pokemonUid, moveId);
                    _battleTimeout.Set(ActiveBattle.PokemonCount > 1 ? Rand.Next(3500, 4000) : Rand.Next(1500, 3000));
                }
            }
        }

        public void LearnMove(Guid pokemonUniqueId, PSXAPI.Response.Payload.PokemonMoveID learningMoveId, int moveToForget)
        {
            var accept = true;
            if (moveToForget >= 5)
            {
                accept = false;
                moveToForget = 0;
            }
            SendProto(new PSXAPI.Request.Learn
            {
                Accept = accept,
                Move = learningMoveId,
                Pokemon = pokemonUniqueId,
                Position = moveToForget
            });
            _swapTimeout.Set();
        }

        public void ChangePokemon(int number)
        {
            SendChangePokemon(ActiveBattle.CurrentBattlingPokemonIndex, number);

            ActiveBattle?.UpdateSelectedPokemon(number);

            _battleTimeout.Set();
        }

        public void UseSurfAfterMovement()
        {
            _surfAfterMovement = true;
        }

        public void UseSurf()
        {
            SendProto(new PSXAPI.Request.Effect
            {
                Action = PSXAPI.Request.EffectAction.Use,
                UID = GetEffectFromName("Surf").UID
            });
            _mountingTimeout.Set();
        }

        public int DistanceTo(int cellX, int cellY)
        {
            return Math.Abs(PlayerX - cellX) + Math.Abs(PlayerY - cellY);
        }

        public static int DistanceBetween(int fromX, int fromY, int toX, int toY)
        {
            return Math.Abs(fromX - toX) + Math.Abs(fromY - toY);
        }

        private void LoadMap(string mapName)
        {
            mapName = MapClient.RemoveExtension(mapName);

            _loadingTimeout.Set(Rand.Next(1500, 4000));

            ClearPath();
            OpenedShop = null;
            PlayerStats = null;
            _surfAfterMovement = false;
            _slidingDirection = null;
            _dialogResponses.Clear();
            _movementTimeout.Cancel();
            _mountingTimeout.Cancel();
            _itemUseTimeout.Cancel();

            if (Map is null || mapName != MapName)
            {
                DownloadMap(mapName);
            }
        }

        private void DownloadMap(string mapName)
        {

            Console.WriteLine("[Map] Requesting: " + MapName);

            AreNpcReceived = false;

            Map = null;
            MapName = mapName;
            _mapClient.DownloadMap(mapName);
            Players.Clear();
            _removedPlayers.Clear();
        }
        private void MapClient_MapLoaded(string mapName, Map map)
        {
            Players.Clear();
            Map = map;
            Map.AreaUpdated += Map_AreaUpdated;
            OnNpcs(map.OriginalNpcs);

            if (Map.IsSessioned)
            {
                Resync();
            }
            if (mapName.ToLowerInvariant() == MapName.ToLowerInvariant()) // well the received map is always upper case, meh idk if I did something wrong.
            {
                Players.Clear();
                _removedPlayers.Clear();
                CheckArea();
                MapLoaded?.Invoke(AreaName);
            }

            if (_cachedNerbyUsers != null && _cachedNerbyUsers.Users != null)
            {
                OnUpdatePlayer(_cachedNerbyUsers);
            }

            CanUseCut = HasCutAbility();
            CanUseSmashRock = HasRockSmashAbility();
#if DEBUG

            if (Map.MapDump.Areas != null && Map.MapDump.Areas.Count > 0)
            {
                foreach (var area in Map.MapDump.Areas)
                {
                    Console.WriteLine($"[{Map.MapDump.Areas.IndexOf(area)}]: {area.AreaName}");
                }
            }
#endif
        }

        private void OnNpcs(List<Npc> originalNpcs)
        {
            Map.Npcs.Clear();
            foreach (var npc in originalNpcs)
            {
                if (npc.Data.Settings.Enabled && npc.IsVisible)
                    Map.Npcs.Add(npc);
            }
            if (_cachedScripts.Count > 0)
            {
                foreach (var script in _cachedScripts)
                {
                    OnScript(script);
                }
                _cachedScripts.Clear();
            }

            AreNpcReceived = true;
            NpcReceieved?.Invoke();
        }

        private void Map_AreaUpdated()
        {
            AreaUpdated?.Invoke();
        }

        public void CheckArea()
        {
            if (MapName.ToLowerInvariant() == "default")
                return;

            if (Map != null)
            {
                Map?.UpdateArea();
                if (Map.MapDump.Areas != null && Map.MapDump.Areas.Count > 0)
                {
                    foreach (MAPAPI.Response.Area area in Map.MapDump.Areas)
                    {
                        if (PlayerX >= area.StartX && PlayerX <= area.EndX && PlayerY >= area.StartY && PlayerY <= area.EndY)
                        {
                            if (area.AreaName.Equals(AreaName, StringComparison.InvariantCultureIgnoreCase) == false)
                                RequestArea(MapName, area.AreaName);

                            AreaName = area.AreaName;
                            PositionUpdated?.Invoke(AreaName, PlayerX, PlayerY);
                            return;
                        }
                    }
                    if (AreaName != Map.MapDump.Settings.MapName)
                    {
                        AreaName = Map.MapDump.Settings.MapName;
                        RequestArea(MapName, AreaName);
                    }
                }
                else if (AreaName != Map.MapDump.Settings.MapName)
                {
                    AreaName = Map.MapDump.Settings.MapName;
                    RequestArea(MapName, AreaName);
                }


                PositionUpdated?.Invoke(AreaName, PlayerX, PlayerY);
            }
        }
        public void Resync(bool mapLoad = true)
        {
            var _syncId = Guid.NewGuid();
            SendProto(new PSXAPI.Request.Sync
            {
                ID = _syncId,
                MapLoad = mapLoad
            });
        }

        public bool HasSurfAbility()
        {
            return HasMove("Surf") && HasEffectName("Surf");
        }

        public bool HasCutAbility()
        {
            return (HasMove("Cut")) &&
                (Map?.Region == "1" && HasItemName("Cascade Badge") ||
                Map?.Region == "2" && HasItemName("Hive Badge") ||
                Map?.Region == "3" && HasItemName("Stone Badge") ||
                Map?.Region == "4" && HasItemName("Forest Badge"));
        }
        public bool HasRockSmashAbility()
        {
            return HasMove("Rock Smash");
        }

        public bool PokemonUidHasMove(int pokemonUid, string moveName)
        {
            return Team.FirstOrDefault(p => p.Uid == pokemonUid)?.Moves.Any(m => m.Name?.Equals(moveName, StringComparison.InvariantCultureIgnoreCase) ?? false) ?? false;
        }

        public bool HasMove(string moveName)
        {
            return Team.Any(p => p.Moves.Any(m => m.Name?.Equals(moveName, StringComparison.InvariantCultureIgnoreCase) ?? false));
        }

        public int GetMovePosition(int pokemonUid, string moveName)
        {
            return Team[pokemonUid].Moves.FirstOrDefault(m => m.Name?.Equals(moveName, StringComparison.InvariantCultureIgnoreCase) ?? false)?.Position ?? -1;
        }

        public InventoryItem GetItemFromId(int id)
        {
            return Items.FirstOrDefault(i => i.Id == id && i.Quantity > 0);
        }

        public bool HasItemId(int id)
        {
            return GetItemFromId(id) != null;
        }

        public bool HasPokemonInTeam(string pokemonName)
        {
            return FindFirstPokemonInTeam(pokemonName) != null;
        }

        public Pokemon FindFirstPokemonInTeam(string pokemonName)
        {
            return Team.FirstOrDefault(p => p.Name.Equals(pokemonName, StringComparison.InvariantCultureIgnoreCase));
        }

        public InventoryItem GetItemFromName(string itemName)
        {
            return Items.FirstOrDefault(i => (
                (i.Name.Equals(itemName, StringComparison.InvariantCultureIgnoreCase)
                    || (ItemsManager.Instance.ItemClass.items.Any(itm => 
                    itm.BattleID.Equals(itemName.RemoveAllUnknownSymbols().Replace(" ", ""), StringComparison.InvariantCultureIgnoreCase)
                    && itm.ID == i.Id)))
                    && i.Quantity > 0));
        }

        public bool HasItemName(string itemName)
        {
            return GetItemFromName(itemName) != null;
        }

        public PlayerEffect GetEffectFromName(string effectName)
        {
            return Effects.FirstOrDefault(e => e.Name.Equals(effectName, StringComparison.InvariantCultureIgnoreCase) && e.UID != Guid.Empty);
        }

        public bool HasEffectName(string effectName) => GetEffectFromName(effectName) != null;
        public static string BadgeFromID(int id)
        {
            string result;
            switch (id)
            {
                case 1:
                    result = "Boulder Badge";
                    break;
                case 2:
                    result = "Cascade Badge";
                    break;
                case 3:
                    result = "Thunder Badge";
                    break;
                case 4:
                    result = "Rainbow Badge";
                    break;
                case 5:
                    result = "Soul Badge";
                    break;
                case 6:
                    result = "Marsh Badge";
                    break;
                case 7:
                    result = "Volcano Badge";
                    break;
                case 8:
                    result = "Earth Badge";
                    break;
                case 9:
                    result = "Zephyr Badge";
                    break;
                case 10:
                    result = "Hive Badge";
                    break;
                case 11:
                    result = "Plain Badge";
                    break;
                case 12:
                    result = "Fog Badge";
                    break;
                case 13:
                    result = "Storm Badge";
                    break;
                case 14:
                    result = "Mineral Badge";
                    break;
                case 15:
                    result = "Glacier Badge";
                    break;
                case 16:
                    result = "Rising Badge";
                    break;
                case 17:
                    result = "Stone Badge";
                    break;
                case 18:
                    result = "Knuckle Badge";
                    break;
                case 19:
                    result = "Dynamo Badge";
                    break;
                case 20:
                    result = "Heat Badge";
                    break;
                case 21:
                    result = "Balance Badge";
                    break;
                case 22:
                    result = "Feather Badge";
                    break;
                case 23:
                    result = "Mind Badge";
                    break;
                case 24:
                    result = "Rain Badge";
                    break;
                case 25:
                    result = "Coal Badge";
                    break;
                case 26:
                    result = "Forest Badge";
                    break;
                case 27:
                    result = "Cobble Badge";
                    break;
                case 28:
                    result = "Fen Badge";
                    break;
                case 29:
                    result = "Relic Badge";
                    break;
                case 30:
                    result = "Mine Badge";
                    break;
                case 31:
                    result = "Icicle Badge";
                    break;
                case 32:
                    result = "Beacon Badge";
                    break;
                default:
                    result = "";
                    break;
            }
            return result;
        }

        public static string GetStatus(string status)
        {
            switch (status)
            {
                case "slp":
                    status = "Sleep";
                    break;
                case "brn":
                    status = "Brun";
                    break;
                case "psn":
                    status = "Posion";
                    break;
                case "par":
                    status = "Paralized";
                    break;
                case "tox":
                    status = "Posion";
                    break;
                default:
                    status = "";
                    break;
            }
            return status;
        }
    }

    public static class StringExtention
    {
        public static string RemoveAllUnknownSymbols(this string s)
        {
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                byte b = (byte)c;
                if (b > 32) //In general, all characters below 32 are non-printable.
                    result.Append(c);
            }
            return result.ToString();
        }
    }
}
