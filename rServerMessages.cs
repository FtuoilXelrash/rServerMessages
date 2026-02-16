using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Text;

using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Libraries;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("rServerMessages", "Ftuoil Xelrash", "1.0.7")]
    [Description("Logs essential server events to Discord channels using webhooks")]
    public class rServerMessages : RustPlugin
    {
        #region Variables

        [PluginReference] private readonly Plugin AntiSpam, BetterChatMute, UFilter;

        private readonly Queue<QueuedMessage> _queue = new();
        private readonly StringBuilder _sb = new();

        private int _retryCount = 0;
        private const int _maxRetries = 5; // Maximum retry attempts before giving up
        private object _resultCall;
        private QueuedMessage _nextMessage;
        private QueuedMessage _queuedMessage;
        private string[] _profanities;
        private Timer _timerQueue;
        private Timer _timerQueueCooldown;
        private bool _isProcessingQueue = false; // Prevent recursive queue processing

        private readonly List<Regex> _regexTags = new()
        {
            new("<color=.+?>", RegexOptions.Compiled),
            new("<size=.+?>", RegexOptions.Compiled)
        };

        private readonly List<string> _tags = new()
        {
            "</color>",
            "</size>",
            "<i>",
            "</i>",
            "<b>",
            "</b>"
        };

        // Simple entity type mappings for basic death messages
        private readonly Dictionary<string, string> _entityNames = new()
        {
            // Players
            ["BasePlayer"] = "Player",
            
            // NPCs
            ["Scientist"] = "Scientist",
            ["ScientistNPC"] = "Scientist", 
            ["HTNPlayer"] = "Heavy Scientist",
            ["NPCMurderer"] = "Murderer",
            ["scarecrow"] = "Scarecrow",
            ["ScientistNPCNew"] = "Scientist",
            ["tunneldweller"] = "Tunnel Dweller",
            ["underwaterdweller"] = "Underwater Dweller",
            ["ZombieNPC"] = "Zombie",
            ["GingerbreadNPC"] = "Gingerbread Man",
            ["NPCShopKeeper"] = "Shopkeeper",
            
            // Animals
            ["Bear"] = "Bear",
            ["Boar"] = "Boar", 
            ["Chicken"] = "Chicken",
            ["Stag"] = "Stag",
            ["Wolf"] = "Wolf",
            ["Wolf2"] = "Wolf",
            ["Polarbear"] = "Polar Bear",
            ["Horse"] = "Horse",
            ["RidableHorse"] = "Horse",
            ["FarmableAnimal"] = "Farm Animal",
            ["Crocodile"] = "Crocodile",
            ["SnakeHazard"] = "Snake",
            ["Panther"] = "Panther",
            ["Tiger"] = "Tiger",
            ["SimpleShark"] = "Shark",
            ["BaseFishNPC"] = "Fish",
            
            // Vehicles/Military
            ["BaseHelicopter"] = "Patrol Helicopter",
            ["PatrolHelicopter"] = "Patrol Helicopter", 
            ["BradleyAPC"] = "Bradley APC",
            ["Minicopter"] = "Minicopter",
            ["ScrapTransportHelicopter"] = "Scrap Helicopter",
            ["AttackHelicopter"] = "Attack Helicopter",
            
            // Turrets/Traps
            ["AutoTurret"] = "Auto Turret",
            ["GunTrap"] = "Shotgun Trap",
            ["FlameTurret"] = "Flame Turret", 
            ["NPCAutoTurret"] = "Sentry Turret",
            ["SamSite"] = "SAM Site",
            ["Landmine"] = "Landmine",
            ["BearTrap"] = "Bear Trap",
            ["TeslaCoil"] = "Tesla Coil",
            
            // Structures
            ["Barricade"] = "Barricade",
            ["SimpleBuildingBlock"] = "Wall",
            ["CodeLock"] = "Code Lock",
            
            // Fire/Heat
            ["FireBall"] = "Fire",
            ["BaseOven"] = "Heat Source",
            ["Campfire"] = "Campfire",
            
            // Other
            ["BeeSwarmAI"] = "Bee Swarm"
        };

        private class QueuedMessage
        {
            public string WebhookUrl { get; set; }
            public string Message { get; set; }
            public DiscordMessage DiscordMessage { get; set; }
            public bool IsEmbed { get; set; }
        }

        private class PlayerNameChangeLogData
        {
            [JsonProperty("NameChanges")]
            public List<NameChangeEntry> NameChanges { get; set; } = new List<NameChangeEntry>();
        }

        private class NameChangeEntry
        {
            [JsonProperty("SteamID")]
            public string SteamID { get; set; }

            [JsonProperty("OldName")]
            public string OldName { get; set; }

            [JsonProperty("NewName")]
            public string NewName { get; set; }

            [JsonProperty("TimestampUTC")]
            public string TimestampUTC { get; set; }

            [JsonProperty("TimestampLocal")]
            public string TimestampLocal { get; set; }

            [JsonProperty("IPAddress")]
            public string IPAddress { get; set; }

            [JsonProperty("ServerName")]
            public string ServerName { get; set; }
        }

        private PlayerNameChangeLogData _nameChangeLogData;

        private class BedRenameLogData
        {
            [JsonProperty("BedRenames")]
            public List<BedRenameLogEntry> BedRenames { get; set; } = new List<BedRenameLogEntry>();
        }

        private class BedRenameLogEntry
        {
            [JsonProperty("PlayerSteamID")]
            public string PlayerSteamID { get; set; }

            [JsonProperty("PlayerName")]
            public string PlayerName { get; set; }

            [JsonProperty("OwnerSteamID")]
            public string OwnerSteamID { get; set; }

            [JsonProperty("OwnerName")]
            public string OwnerName { get; set; }

            [JsonProperty("DeployerSteamID")]
            public string DeployerSteamID { get; set; }

            [JsonProperty("DeployerName")]
            public string DeployerName { get; set; }

            [JsonProperty("ItemType")]
            public string ItemType { get; set; }

            [JsonProperty("OldName")]
            public string OldName { get; set; }

            [JsonProperty("NewName")]
            public string NewName { get; set; }

            [JsonProperty("Position")]
            public string Position { get; set; }

            [JsonProperty("TimestampUTC")]
            public string TimestampUTC { get; set; }

            [JsonProperty("TimestampLocal")]
            public string TimestampLocal { get; set; }

            [JsonProperty("ServerName")]
            public string ServerName { get; set; }

            [JsonProperty("WasBlacklisted")]
            public bool WasBlacklisted { get; set; }

            [JsonProperty("BlacklistTermsDetected")]
            public string BlacklistTermsDetected { get; set; }
        }

        private BedRenameLogData _bedRenameLogData;

        private class ExplosiveLogData
        {
            [JsonProperty("Events")]
            public List<ExplosiveLogEntry> Events { get; set; } = new List<ExplosiveLogEntry>();
        }

        private class ExplosiveLogEntry
        {
            [JsonProperty("PlayerSteamID")]
            public string PlayerSteamID { get; set; }

            [JsonProperty("PlayerName")]
            public string PlayerName { get; set; }

            [JsonProperty("IsNPC")]
            public bool IsNPC { get; set; }

            [JsonProperty("ExplosiveType")]
            public string ExplosiveType { get; set; }

            [JsonProperty("Position")]
            public string Position { get; set; }

            [JsonProperty("TimestampUTC")]
            public string TimestampUTC { get; set; }

            [JsonProperty("TimestampLocal")]
            public string TimestampLocal { get; set; }

            [JsonProperty("ServerName")]
            public string ServerName { get; set; }
        }

        private ExplosiveLogData _c4LogData;
        private ExplosiveLogData _rocketLogData;

        #endregion Variables

        #region Initialization

        private void Init()
        {
            UnsubscribeHooks();
            LoadNameChangeLogData();
            LoadBedRenameLogData();
            LoadC4LogData();
            LoadRocketLogData();
        }

        private void Unload()
        {
            _timerQueue?.Destroy();
            _timerQueueCooldown?.Destroy();
        }

        private void OnServerInitialized(bool isStartup)
        {
            if (isStartup && _configData.ServerStateSettings.Enabled)
            {
                DiscordSendMessage(Lang(LangKeys.Event.Initialized), _configData.GlobalSettings.ServerMessagesWebhook);
            }

            SubscribeHooks();
        }

        private void OnServerShutdown()
        {
            if (_configData.ServerStateSettings.Enabled)
            {
                string url = _configData.GlobalSettings.ServerMessagesWebhook;

                if (!string.IsNullOrEmpty(url))
                {
                    webrequest.Enqueue(url, new DiscordMessage(Lang(LangKeys.Event.Shutdown)).ToJson(), DiscordSendMessageCallback, null, RequestMethod.POST, _headers);
                }
            }
        }

        #endregion Initialization

        #region Configuration

        private ConfigData _configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Global settings")]
            public GlobalSettings GlobalSettings { get; set; } = new();

            [JsonProperty(PropertyName = "Air Event settings")]
            public EventSettings AirEventSettings { get; set; } = new();

            [JsonProperty(PropertyName = "Airfield Event settings")]
            public EventSettings AirfieldEventSettings { get; set; } = new();

            [JsonProperty(PropertyName = "Arctic Base Event settings")]
            public EventSettings ArcticBaseEventSettings { get; set; } = new();

            [JsonProperty(PropertyName = "Armored Train Event settings")]
            public EventSettings ArmoredTrainEventSettings { get; set; } = new();

            [JsonProperty(PropertyName = "Chat settings")]
            public EventSettings ChatSettings { get; set; } = new();

            [JsonProperty(PropertyName = "Chat (Team) settings")]
            public EventSettings ChatTeamSettings { get; set; } = new();

            [JsonProperty(PropertyName = "Christmas settings")]
            public EventSettings ChristmasSettings { get; set; } = new();

            [JsonProperty(PropertyName = "Easter settings")]
            public EventSettings EasterSettings { get; set; } = new();

            [JsonProperty(PropertyName = "Gas Station Event settings")]
            public EventSettings GasStationEventSettings { get; set; } = new();

            [JsonProperty(PropertyName = "Halloween settings")]
            public EventSettings HalloweenSettings { get; set; } = new();

            [JsonProperty(PropertyName = "Permissions settings")]
            public EventSettings PermissionsSettings { get; set; } = new();

            [JsonProperty(PropertyName = "Player death settings")]
            public DeathSettings PlayerDeathSettings { get; set; } = new();

            [JsonProperty(PropertyName = "Player connect advanced info settings")]
            public EventSettings PlayerConnectedInfoSettings { get; set; } = new();

            [JsonProperty(PropertyName = "Player disconnect settings")]
            public EventSettings PlayerDisconnectedSettings { get; set; } = new();

            [JsonProperty(PropertyName = "Raidable Bases settings")]
            public EventSettings RaidableBasesSettings { get; set; } = new();

            [JsonProperty(PropertyName = "Rcon command settings")]
            public EventSettings RconCommandSettings { get; set; } = new();

            [JsonProperty(PropertyName = "Rcon connection settings")]
            public EventSettings RconConnectionSettings { get; set; } = new();

            [JsonProperty(PropertyName = "SantaSleigh settings")]
            public EventSettings SantaSleighSettings { get; set; } = new();

            [JsonProperty(PropertyName = "Server messages settings")]
            public EventSettings ServerMessagesSettings { get; set; } = new();

            [JsonProperty(PropertyName = "Server state settings")]
            public EventSettings ServerStateSettings { get; set; } = new();

            [JsonProperty(PropertyName = "Sputnik Event settings")]
            public EventSettings SputnikEventSettings { get; set; } = new();

            [JsonProperty(PropertyName = "Supermarket Event settings")]
            public EventSettings SupermarketEventSettings { get; set; } = new();

            [JsonProperty(PropertyName = "User Banned settings")]
            public EventSettings UserBannedSettings { get; set; } = new();

            [JsonProperty(PropertyName = "User Kicked settings")]
            public EventSettings UserKickedSettings { get; set; } = new();

            [JsonProperty(PropertyName = "User Muted settings")]
            public EventSettings UserMutedSettings { get; set; } = new();

            [JsonProperty(PropertyName = "User Name Updated settings")]
            public EventSettings UserNameUpdateSettings { get; set; } = new();

            [JsonProperty(PropertyName = "Bed/Bag/Towel Rename settings")]
            public BedRenameSettings BedRenameSettings { get; set; } = new();

            [JsonProperty(PropertyName = "C4 Log settings")]
            public ExplosiveLogSettings C4LogSettings { get; set; } = new();

            [JsonProperty(PropertyName = "Rocket Log settings")]
            public ExplosiveLogSettings RocketLogSettings { get; set; } = new();
        }

        private class GlobalSettings
        {
            [JsonProperty(PropertyName = "Log to console?")]
            public bool LoggingEnabled { get; set; } = false;

            [JsonProperty(PropertyName = "Use AntiSpam plugin on chat messages")]
            public bool UseAntiSpam { get; set; } = false;

            [JsonProperty(PropertyName = "Use UFilter plugin on chat messages")]
            public bool UseUFilter { get; set; } = false;

            [JsonProperty(PropertyName = "Hide admin connect/disconnect messages")]
            public bool HideAdmin { get; set; } = false;

            [JsonProperty(PropertyName = "Hide NPC death messages")]
            public bool HideNPC { get; set; } = false;

            [JsonProperty(PropertyName = "Include death coordinates in death messages")]
            public bool IncludeDeathCoordinates { get; set; } = true;

            [JsonProperty(PropertyName = "Use Discord Embeds for death messages")]
            public bool UseEmbedForDeaths { get; set; } = true;

            [JsonProperty(PropertyName = "Use enhanced embeds for connections")]
            public bool UseEmbedForConnections { get; set; } = true;

            [JsonProperty(PropertyName = "Show country information (requires internet)")]
            public bool ShowCountryInfo { get; set; } = true;

            [JsonProperty(PropertyName = "Show server population in connection messages")]
            public bool ShowServerPopulation { get; set; } = true;

            [JsonProperty(PropertyName = "Show combat details in death messages")]
            public bool ShowCombatDetails { get; set; } = true;

            [JsonProperty(PropertyName = "Use enhanced embeds for server messages")]
            public bool UseEmbedForServerMessages { get; set; } = true;

            [JsonProperty(PropertyName = "Show kill distance in PvP deaths")]
            public bool ShowKillDistance { get; set; } = true;

            [JsonProperty(PropertyName = "High damage threshold for special kills")]
            public float HighDamageThreshold { get; set; } = 75f;

            [JsonProperty(PropertyName = "Use enhanced embeds for RCON messages")]
            public bool UseEmbedForRcon { get; set; } = true;

            [JsonProperty(PropertyName = "Replacement string for tags")]
            public string TagsReplacement { get; set; } = "`";

            [JsonProperty(PropertyName = "Queue interval (1 message per ? seconds)")]
            public float QueueInterval { get; set; } = 1f;

            [JsonProperty(PropertyName = "Queue cooldown if connection error (seconds)")]
            public float QueueCooldown { get; set; } = 60f;

            [JsonProperty(PropertyName = "Public Chat Webhook URL")]
            public string PublicChatWebhook { get; set; } = string.Empty;

            [JsonProperty(PropertyName = "Private Admin Webhook URL")]
            public string PrivateAdminWebhook { get; set; } = string.Empty;

            [JsonProperty(PropertyName = "Server Messages Webhook URL")]
            public string ServerMessagesWebhook { get; set; } = string.Empty;

            [JsonProperty(PropertyName = "RCON command blacklist", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> RCONCommandBlacklist { get; set; } = new()
            {
                "serverinfo",
                "server.hostname",
                "server.headerimage",
                "server.description",
                "server.url",
                "playerlist",
                "status"
            };

            [JsonProperty(PropertyName = "RCON trusted IPs (hide connections from these)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> RCONTrustedIPs { get; set; } = new()
            {
                "127.0.0.1",
                "::1"
            };

            [JsonProperty(PropertyName = "Steam Web API Key (for profile data)")]
            public string SteamWebAPIKey { get; set; } = string.Empty;
        }

        private class EventSettings
        {
            [JsonProperty(PropertyName = "Enabled?")]
            public bool Enabled { get; set; } = false;
        }

        private class DeathSettings
        {
            [JsonProperty(PropertyName = "Enabled?")]
            public bool Enabled { get; set; } = false;
            
            [JsonProperty(PropertyName = "Enable PvP deaths")]
            public bool EnablePvP { get; set; } = true;
            
            [JsonProperty(PropertyName = "Enable PvE deaths")]
            public bool EnablePvE { get; set; } = true;
            
            [JsonProperty(PropertyName = "Enable suicide deaths")]
            public bool EnableSuicide { get; set; } = true;
            
            [JsonProperty(PropertyName = "Enable drowning deaths")]
            public bool EnableDrowning { get; set; } = true;
        }

        private class BedRenameSettings
        {
            [JsonProperty(PropertyName = "Enabled?")]
            public bool Enabled { get; set; } = false;

            [JsonProperty(PropertyName = "Log to file?")]
            public bool LogToFile { get; set; } = true;

            [JsonProperty(PropertyName = "Send Discord embed?")]
            public bool SendDiscordEmbed { get; set; } = true;

            [JsonProperty(PropertyName = "Blacklist settings")]
            public BedRenameBlacklistSettings Blacklist { get; set; } = new();
        }

        private class BedRenameBlacklistSettings
        {
            [JsonProperty(PropertyName = "Enabled?")]
            public bool Enabled { get; set; } = false;

            [JsonProperty(PropertyName = "Block rename when blacklisted?")]
            public bool BlockRename { get; set; } = true;

            [JsonProperty(PropertyName = "Blacklisted terms (simple)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> BlacklistedTerms { get; set; } = new()
            {
                "term1",
                "term2",
                "term3"
            };

            [JsonProperty(PropertyName = "Blacklisted REGEX (advanced)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> BlacklistedREGEX { get; set; } = new()
            {
                "REGEX1",
                "REGEX2",
                "REGEX3"
            };

            [JsonProperty(PropertyName = "Leet conversion enabled?")]
            public bool LeetConversionEnabled { get; set; } = false;

            [JsonProperty(PropertyName = "Leet conversion table", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, string> LeetTable { get; set; } = new()
            {
                {"}{", "h"},
                {"|-|", "h"},
                {"]-[", "h"},
                {"/-/", "h"},
                {"|{", "k"},
                {"/\\/\\", "m"},
                {"|\\|", "n"},
                {"/\\/", "n"},
                {"()", "o"},
                {"[]", "o"},
                {"vv", "w"},
                {"\\/\\/", "w"},
                {"><", "x"},
                {"2", "z"},
                {"4", "a"},
                {"@", "a"},
                {"8", "b"},
                {"$", "s"},
                {"7", "t"},
                {"|", "l"},
                {"1", "i"},
                {"!", "i"},
                {"0", "o"},
                {"3", "e"},
                {"6", "g"},
                {"9", "g"},
                {"&", "g"},
                {"#", "h"},
                {"5", "s"}
            };
        }

        private class ExplosiveLogSettings
        {
            [JsonProperty(PropertyName = "Enabled?")]
            public bool Enabled { get; set; } = false;

            [JsonProperty(PropertyName = "Log to file?")]
            public bool LogToFile { get; set; } = true;

            [JsonProperty(PropertyName = "Send Discord embed?")]
            public bool SendDiscordEmbed { get; set; } = true;

            [JsonProperty(PropertyName = "Hide NPC usage?")]
            public bool HideNPC { get; set; } = true;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _configData = Config.ReadObject<ConfigData>();
                if (_configData == null)
                {
                    LoadDefaultConfig();
                }
                ValidateConfig();
                SaveConfig();
            }
            catch
            {
                PrintError("The configuration file is corrupted");
                LoadDefaultConfig();
                ValidateConfig();
                SaveConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            _configData = new ConfigData();
        }

        protected override void SaveConfig() => Config.WriteObject(_configData);

        private void ValidateConfig()
        {
            bool needsSave = false;

            // Validate Global Settings
            if (_configData.GlobalSettings == null)
            {
                _configData.GlobalSettings = new GlobalSettings();
                PrintWarning("GlobalSettings was null, reset to default");
                needsSave = true;
            }

            // Validate QueueInterval
            if (_configData.GlobalSettings.QueueInterval <= 0)
            {
                _configData.GlobalSettings.QueueInterval = 1f;
                PrintWarning("QueueInterval was invalid, reset to 1.0");
                needsSave = true;
            }

            // Validate QueueCooldown
            if (_configData.GlobalSettings.QueueCooldown <= 0)
            {
                _configData.GlobalSettings.QueueCooldown = 60f;
                PrintWarning("QueueCooldown was invalid, reset to 60.0");
                needsSave = true;
            }

            // Validate TagsReplacement
            if (string.IsNullOrEmpty(_configData.GlobalSettings.TagsReplacement))
            {
                _configData.GlobalSettings.TagsReplacement = "`";
                PrintWarning("TagsReplacement was empty, reset to '`'");
                needsSave = true;
            }

            // Validate RCONCommandBlacklist
            if (_configData.GlobalSettings.RCONCommandBlacklist == null)
            {
                _configData.GlobalSettings.RCONCommandBlacklist = new List<string> { "playerlist", "status" };
                PrintWarning("RCONCommandBlacklist was null, reset to default");
                needsSave = true;
            }

            // Validate RCONTrustedIPs
            if (_configData.GlobalSettings.RCONTrustedIPs == null)
            {
                _configData.GlobalSettings.RCONTrustedIPs = new List<string> { "127.0.0.1", "::1" };
                PrintWarning("RCONTrustedIPs was null, reset to default");
                needsSave = true;
            }

            // Validate HighDamageThreshold
            if (_configData.GlobalSettings.HighDamageThreshold <= 0)
            {
                _configData.GlobalSettings.HighDamageThreshold = 75f;
                PrintWarning("HighDamageThreshold was invalid, reset to 75.0");
                needsSave = true;
            }

            // Validate Steam API Key
            if (string.IsNullOrEmpty(_configData.GlobalSettings.SteamWebAPIKey))
            {
                // Silently skip Steam profile data if no API key configured
            }
            else if (_configData.GlobalSettings.SteamWebAPIKey.Length != 32)
            {
                PrintWarning("Steam Web API Key appears to be invalid (should be 32 characters). Steam profile data may not work.");
            }

            // Validate Webhook URLs
            if (!IsValidWebhookUrl(_configData.GlobalSettings.PublicChatWebhook) && !string.IsNullOrEmpty(_configData.GlobalSettings.PublicChatWebhook))
            {
                PrintWarning("PublicChatWebhook URL appears to be invalid");
            }

            if (!IsValidWebhookUrl(_configData.GlobalSettings.PrivateAdminWebhook) && !string.IsNullOrEmpty(_configData.GlobalSettings.PrivateAdminWebhook))
            {
                PrintWarning("PrivateAdminWebhook URL appears to be invalid");
            }

            if (!IsValidWebhookUrl(_configData.GlobalSettings.ServerMessagesWebhook) && !string.IsNullOrEmpty(_configData.GlobalSettings.ServerMessagesWebhook))
            {
                PrintWarning("ServerMessagesWebhook URL appears to be invalid");
            }

            // Validate ALL Event Settings - Force creation if missing
            if (_configData.AirEventSettings == null)
            {
                _configData.AirEventSettings = new EventSettings();
                PrintWarning("AirEventSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.AirfieldEventSettings == null)
            {
                _configData.AirfieldEventSettings = new EventSettings();
                PrintWarning("AirfieldEventSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.ArcticBaseEventSettings == null)
            {
                _configData.ArcticBaseEventSettings = new EventSettings();
                PrintWarning("ArcticBaseEventSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.ArmoredTrainEventSettings == null)
            {
                _configData.ArmoredTrainEventSettings = new EventSettings();
                PrintWarning("ArmoredTrainEventSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.ChatSettings == null)
            {
                _configData.ChatSettings = new EventSettings();
                PrintWarning("ChatSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.ChatTeamSettings == null)
            {
                _configData.ChatTeamSettings = new EventSettings();
                PrintWarning("ChatTeamSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.ChristmasSettings == null)
            {
                _configData.ChristmasSettings = new EventSettings();
                PrintWarning("ChristmasSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.EasterSettings == null)
            {
                _configData.EasterSettings = new EventSettings();
                PrintWarning("EasterSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.GasStationEventSettings == null)
            {
                _configData.GasStationEventSettings = new EventSettings();
                PrintWarning("GasStationEventSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.HalloweenSettings == null)
            {
                _configData.HalloweenSettings = new EventSettings();
                PrintWarning("HalloweenSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.PermissionsSettings == null)
            {
                _configData.PermissionsSettings = new EventSettings();
                PrintWarning("PermissionsSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.PlayerDeathSettings == null)
            {
                _configData.PlayerDeathSettings = new DeathSettings();
                PrintWarning("PlayerDeathSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.PlayerConnectedInfoSettings == null)
            {
                _configData.PlayerConnectedInfoSettings = new EventSettings();
                PrintWarning("PlayerConnectedInfoSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.PlayerDisconnectedSettings == null)
            {
                _configData.PlayerDisconnectedSettings = new EventSettings();
                PrintWarning("PlayerDisconnectedSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.RaidableBasesSettings == null)
            {
                _configData.RaidableBasesSettings = new EventSettings();
                PrintWarning("RaidableBasesSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.RconCommandSettings == null)
            {
                _configData.RconCommandSettings = new EventSettings();
                PrintWarning("RconCommandSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.RconConnectionSettings == null)
            {
                _configData.RconConnectionSettings = new EventSettings();
                PrintWarning("RconConnectionSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.SantaSleighSettings == null)
            {
                _configData.SantaSleighSettings = new EventSettings();
                PrintWarning("SantaSleighSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.ServerMessagesSettings == null)
            {
                _configData.ServerMessagesSettings = new EventSettings();
                PrintWarning("ServerMessagesSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.ServerStateSettings == null)
            {
                _configData.ServerStateSettings = new EventSettings();
                PrintWarning("ServerStateSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.SputnikEventSettings == null)
            {
                _configData.SputnikEventSettings = new EventSettings();
                PrintWarning("SputnikEventSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.SupermarketEventSettings == null)
            {
                _configData.SupermarketEventSettings = new EventSettings();
                PrintWarning("SupermarketEventSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.UserBannedSettings == null)
            {
                _configData.UserBannedSettings = new EventSettings();
                PrintWarning("UserBannedSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.UserKickedSettings == null)
            {
                _configData.UserKickedSettings = new EventSettings();
                PrintWarning("UserKickedSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.UserMutedSettings == null)
            {
                _configData.UserMutedSettings = new EventSettings();
                PrintWarning("UserMutedSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.UserNameUpdateSettings == null)
            {
                _configData.UserNameUpdateSettings = new EventSettings();
                PrintWarning("UserNameUpdateSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.BedRenameSettings == null)
            {
                _configData.BedRenameSettings = new BedRenameSettings();
                PrintWarning("BedRenameSettings was null, created new instance");
                needsSave = true;
            }
            else if (_configData.BedRenameSettings.Blacklist == null)
            {
                _configData.BedRenameSettings.Blacklist = new BedRenameBlacklistSettings();
                PrintWarning("BedRenameSettings.Blacklist was null, created new instance");
                needsSave = true;
            }

            if (_configData.C4LogSettings == null)
            {
                _configData.C4LogSettings = new ExplosiveLogSettings();
                PrintWarning("C4LogSettings was null, created new instance");
                needsSave = true;
            }

            if (_configData.RocketLogSettings == null)
            {
                _configData.RocketLogSettings = new ExplosiveLogSettings();
                PrintWarning("RocketLogSettings was null, created new instance");
                needsSave = true;
            }

            if (needsSave)
            {
                PrintWarning("Configuration validation completed - saving updated config");
            }
        }

        private bool IsValidWebhookUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            return url.StartsWith("https://discord.com/api/webhooks/") || url.StartsWith("https://discordapp.com/api/webhooks/");
        }

        #endregion Configuration

        #region Localization

        public string Lang(string key, string userIDString = null, params object[] args)
        {
            try
            {
                return string.Format(lang.GetMessage(key, this, userIDString).Replace("{time}", $"<t:{DateTimeOffset.Now.ToUnixTimeSeconds()}:t>"), args);
            }
            catch (Exception ex)
            {
                PrintError($"Lang Key '{key}' threw exception:\n{ex}");
                throw;
            }
        }

        private static class LangKeys
        {
            public static class Event
            {
                private const string Base = nameof(Event) + ".";
                public const string AirEvent = Base + nameof(AirEvent);
                public const string AirfieldEvent = Base + nameof(AirfieldEvent);
                public const string ArcticBaseEvent = Base + nameof(ArcticBaseEvent);
                public const string ArmoredTrainEvent = Base + nameof(ArmoredTrainEvent);
                public const string Chat = Base + nameof(Chat);
                public const string ChatTeam = Base + nameof(ChatTeam);
                public const string Christmas = Base + nameof(Christmas);
                public const string Death = Base + nameof(Death);
                public const string Easter = Base + nameof(Easter);
                public const string EasterWinner = Base + nameof(EasterWinner);
                public const string GasStationEvent = Base + nameof(GasStationEvent);
                public const string Halloween = Base + nameof(Halloween);
                public const string HalloweenWinner = Base + nameof(HalloweenWinner);
                public const string Initialized = Base + nameof(Initialized);
                public const string PlayerConnected = Base + nameof(PlayerConnected);
                public const string PlayerConnectedInfo = Base + nameof(PlayerConnectedInfo);
                public const string PlayerDisconnected = Base + nameof(PlayerDisconnected);
                public const string RconCommand = Base + nameof(RconCommand);
                public const string RconConnection = Base + nameof(RconConnection);
                public const string SantaSleigh = Base + nameof(SantaSleigh);
                public const string ServerMessage = Base + nameof(ServerMessage);
                public const string Shutdown = Base + nameof(Shutdown);
                public const string SputnikEvent = Base + nameof(SputnikEvent);
                public const string SupermarketEvent = Base + nameof(SupermarketEvent);
                public const string UserBanned = Base + nameof(UserBanned);
                public const string UserKicked = Base + nameof(UserKicked);
                public const string UserMuted = Base + nameof(UserMuted);
                public const string UserNameUpdated = Base + nameof(UserNameUpdated);
                public const string UserUnbanned = Base + nameof(UserUnbanned);
                public const string UserUnmuted = Base + nameof(UserUnmuted);
            }

            public static class Permission
            {
                private const string Base = nameof(Permission) + ".";
                public const string GroupCreated = Base + nameof(GroupCreated);
                public const string GroupDeleted = Base + nameof(GroupDeleted);
                public const string UserGroupAdded = Base + nameof(UserGroupAdded);
                public const string UserGroupRemoved = Base + nameof(UserGroupRemoved);
                public const string UserPermissionGranted = Base + nameof(UserPermissionGranted);
                public const string UserPermissionRevoked = Base + nameof(UserPermissionRevoked);
            }

            public static class Plugin
            {
                private const string Base = nameof(Plugin) + ".";
                public const string RaidableBaseCompleted = Base + nameof(RaidableBaseCompleted);
                public const string RaidableBaseEnded = Base + nameof(RaidableBaseEnded);
                public const string RaidableBaseStarted = Base + nameof(RaidableBaseStarted);
                public const string TimedGroupAdded = Base + nameof(TimedGroupAdded);
                public const string TimedGroupExtended = Base + nameof(TimedGroupExtended);
                public const string TimedPermissionExtended = Base + nameof(TimedPermissionExtended);
                public const string TimedPermissionGranted = Base + nameof(TimedPermissionGranted);
            }

            public static class Format
            {
                private const string Base = nameof(Format) + ".";
                public const string Day = Base + nameof(Day);
                public const string Days = Base + nameof(Days);
                public const string Easy = Base + nameof(Easy);
                public const string Expert = Base + nameof(Expert);
                public const string Hard = Base + nameof(Hard);
                public const string Hour = Base + nameof(Hour);
                public const string Hours = Base + nameof(Hours);
                public const string Medium = Base + nameof(Medium);
                public const string Minute = Base + nameof(Minute);
                public const string Minutes = Base + nameof(Minutes);
                public const string Nightmare = Base + nameof(Nightmare);
                public const string Second = Base + nameof(Second);
                public const string Seconds = Base + nameof(Seconds);
            }
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.Event.AirEvent] = ":helicopter: {time} Air Event started",
                [LangKeys.Event.AirfieldEvent] = ":airplane: {time} Airfield Event started",
                [LangKeys.Event.ArcticBaseEvent] = ":snowflake: {time} Arctic Base Event started",
                [LangKeys.Event.ArmoredTrainEvent] = ":train2: {time} Armored Train Event started",
                [LangKeys.Event.Chat] = ":speech_left: {time} **{0}**: {1}",
                [LangKeys.Event.ChatTeam] = ":busts_in_silhouette: {time} **{0}**: {1}",
                [LangKeys.Event.Christmas] = ":christmas_tree: {time} Christmas event started",
                [LangKeys.Event.Death] = ":skull: {time} {0}",
                [LangKeys.Event.Easter] = ":egg: {time} Easter event started",
                [LangKeys.Event.EasterWinner] = ":egg: {time} Easter event ended. The winner is `{0}`",
                [LangKeys.Event.GasStationEvent] = ":fuelpump: {time} Gas Station Event started",
                [LangKeys.Event.Halloween] = ":jack_o_lantern: {time} Halloween event started",
                [LangKeys.Event.HalloweenWinner] = ":jack_o_lantern: {time} Halloween event ended. The winner is `{0}`",
                [LangKeys.Event.Initialized] = ":ballot_box_with_check: {time} Server is online again!",
                [LangKeys.Event.PlayerConnectedInfo] = ":detective: {time} {0} connected. SteamID: `{1}` IP: `{2}`",
                [LangKeys.Event.PlayerDisconnected] = ":x: {time} {0} disconnected ({1}) SteamID: `{2}` IP: `{3}`",
                [LangKeys.Event.RconCommand] = ":satellite: {time} RCON command `{0}` is run from `{1}`",
                [LangKeys.Event.RconConnection] = ":satellite: {time} RCON connection is opened from `{0}`",
                [LangKeys.Event.SantaSleigh] = ":santa: {time} SantaSleigh Event started",
                [LangKeys.Event.ServerMessage] = ":desktop: {time} `{0}`",
                [LangKeys.Event.Shutdown] = ":stop_sign: {time} Server is shutting down!",
                [LangKeys.Event.SputnikEvent] = ":satellite_orbital: {time} Sputnik Event started",
                [LangKeys.Event.SupermarketEvent] = ":convenience_store: {time} Supermarket Event started",
                [LangKeys.Event.UserBanned] = ":no_entry: {time} Player `{0}` SteamID: `{1}` IP: `{2}` was banned: `{3}`",
                [LangKeys.Event.UserKicked] = ":hiking_boot: {time} Player `{0}` SteamID: `{1}` was kicked: `{2}`",
                [LangKeys.Event.UserMuted] = ":mute: {time} `{0}` was muted by `{1}` for `{2}` (`{3}`)",
                [LangKeys.Event.UserNameUpdated] = ":label: {time} `{0}` changed name to `{1}` SteamID: `{2}`",
                [LangKeys.Event.UserUnbanned] = ":ok: {time} Player `{0}` SteamID: `{1}` IP: `{2}` was unbanned",
                [LangKeys.Event.UserUnmuted] = ":speaker: {time} `{0}` was unmuted `{1}`",
                [LangKeys.Format.Day] = "day",
                [LangKeys.Format.Days] = "days",
                [LangKeys.Format.Easy] = "Easy",
                [LangKeys.Format.Expert] = "Expert",
                [LangKeys.Format.Hard] = "Hard",
                [LangKeys.Format.Hour] = "hour",
                [LangKeys.Format.Hours] = "hours",
                [LangKeys.Format.Medium] = "Medium",
                [LangKeys.Format.Minute] = "minute",
                [LangKeys.Format.Minutes] = "minutes",
                [LangKeys.Format.Nightmare] = "Nightmare",
                [LangKeys.Format.Second] = "second",
                [LangKeys.Format.Seconds] = "seconds",
                [LangKeys.Permission.GroupCreated] = ":family: {time} Group `{0}` has been created",
                [LangKeys.Permission.GroupDeleted] = ":family: {time} Group `{0}` has been deleted",
                [LangKeys.Permission.UserGroupAdded] = ":family: {time} `{0}` `{1}` is added to group `{2}`",
                [LangKeys.Permission.UserGroupRemoved] = ":family: {time} `{0}` `{1}` is removed from group `{2}`",
                [LangKeys.Permission.UserPermissionGranted] = ":key: {time} `{0}` `{1}` is granted `{2}`",
                [LangKeys.Permission.UserPermissionRevoked] = ":key: {time} `{0}` `{1}` is revoked `{2}`",
                [LangKeys.Plugin.RaidableBaseCompleted] = ":homes: {time} {1} Raidable Base owned by {2} at `{0}` has been raided by **{3}**",
                [LangKeys.Plugin.RaidableBaseEnded] = ":homes: {time} {1} Raidable Base at `{0}` has ended",
                [LangKeys.Plugin.RaidableBaseStarted] = ":homes: {time} {1} Raidable Base spawned at `{0}`",
                [LangKeys.Plugin.TimedGroupAdded] = ":timer: {time} `{0}` `{1}` is added to `{2}` for {3}",
                [LangKeys.Plugin.TimedGroupExtended] = ":timer: {time} `{0}` `{1}` timed group `{2}` is extended to {3}",
                [LangKeys.Plugin.TimedPermissionExtended] = ":timer: {time} `{0}` `{1}` timed permission `{2}` is extended to {3}",
                [LangKeys.Plugin.TimedPermissionGranted] = ":timer: {time} `{0}` `{1}` is granted `{2}` for {3}",
            }, this);
        }

        #endregion Localization

        #region Events Hooks

        private void OnBetterChatMuted(IPlayer target, IPlayer initiator, string reason)
        {
            LogToConsole($"{target.Name} was muted by {initiator.Name} for ever ({reason})");

            DiscordSendMessage(Lang(LangKeys.Event.UserMuted, null, ReplaceChars(target.Name), ReplaceChars(initiator.Name), "ever", ReplaceChars(reason)), _configData.GlobalSettings.PrivateAdminWebhook);
        }

        private void OnBetterChatMuteExpired(IPlayer player)
        {
            LogToConsole($"{player.Name} was unmuted by SERVER");

            DiscordSendMessage(Lang(LangKeys.Event.UserUnmuted, null, ReplaceChars(player.Name), "SERVER"), _configData.GlobalSettings.PrivateAdminWebhook);
        }

        private void OnBetterChatTimeMuted(IPlayer target, IPlayer initiator, TimeSpan time, string reason)
        {
            LogToConsole($"{target.Name} was muted by {initiator.Name} for {time.ToShortString()} ({reason})");

            DiscordSendMessage(Lang(LangKeys.Event.UserMuted, null, ReplaceChars(target.Name), ReplaceChars(initiator.Name), time.ToShortString(), ReplaceChars(reason)), _configData.GlobalSettings.PrivateAdminWebhook);
        }

        private void OnBetterChatUnmuted(IPlayer target, IPlayer initiator)
        {
            LogToConsole($"{target.Name} was unmuted by {initiator.Name}");

            DiscordSendMessage(Lang(LangKeys.Event.UserUnmuted, null, ReplaceChars(target.Name), ReplaceChars(initiator.Name)), _configData.GlobalSettings.PrivateAdminWebhook);
        }

        private void OnEntityDeath(BasePlayer player, HitInfo info)
        {
            if (!_configData.PlayerDeathSettings.Enabled || !player.IsValid())
            {
                return;
            }

            if (_configData.GlobalSettings.HideNPC && (player.IsNpc || !player.userID.IsSteamId()))
            {
                return;
            }

            // Check if this death type should be reported
            string deathType = GetDeathType(player, info);
            if (!ShouldReportDeathType(deathType))
            {
                return;
            }

            // Capture death position
            Vector3 deathPosition = player.transform.position;
            
            // Send enhanced death message with embeds if enabled
            if (_configData.GlobalSettings.UseEmbedForDeaths)
            {
                SendEnhancedDeathMessage(player, info, deathPosition);
            }
            else
            {
                // Fallback to simple text message
                string deathMessage = GetSimpleDeathMessage(player, info, deathPosition);
                string discordMessage = $":skull: <t:{DateTimeOffset.Now.ToUnixTimeSeconds()}:t> {deathMessage}";
                DiscordSendMessage(discordMessage, _configData.GlobalSettings.PrivateAdminWebhook);
            }
        }

        private void SendEnhancedDeathMessage(BasePlayer victim, HitInfo info, Vector3 deathPosition)
        {
            string victimName = ReplaceChars(victim.displayName);
            string deathType = GetDeathType(victim, info);
            string deathIcon = GetDeathIcon(victim, info);
            int embedColor = GetDeathColor(victim, info);
            
            var embed = new DiscordEmbed()
                .SetColor(embedColor)
                .SetTimestamp(DateTimeOffset.Now);

            string description = "";
            string weapon = GetWeaponName(info);
            
            // Check for drowning first
            if (IsDrowningDeath(info))
            {
                description = $"**{victimName}** drowned";
                embed.SetTitle($"🌊 Drowning")
                     .AddField("Death Type", "Drowning", true);
            }
            // Build death description based on type
            else if (info?.Initiator == null)
            {
                description = $"**{victimName}** died";
                embed.SetTitle($"{deathIcon} Player Death")
                     .AddField("Death Type", "Unknown", true);
            }
            else if (info.Initiator is BasePlayer killer)
            {
                if (killer.userID == victim.userID)
                {
                    description = $"**{victimName}** committed suicide";
                    embed.SetTitle($"{deathIcon} Suicide")
                         .AddField("Death Type", "Suicide", true);
                    
                    if (!string.IsNullOrEmpty(weapon))
                    {
                        embed.AddField("Method", weapon, true);
                    }
                }
                else
                {
                    // PvP Kill
                    float damage = info.damageTypes.Total();
                    float distance = Vector3.Distance(victim.transform.position, killer.transform.position);
                    bool isHighDamage = damage >= _configData.GlobalSettings.HighDamageThreshold;
                    bool isHeadshot = IsHeadshot(info);
                    
                    string killerName = ReplaceChars(killer.displayName);
                    description = $"**{victimName}** was eliminated by **{killerName}**";
                    
                    // Enhanced title based on kill type - prioritize headshot
                    string title;
                    if (isHeadshot)
                    {
                        title = "🎯💀 Devastating Headshot!";
                    }
                    else if (isHighDamage)
                    {
                        title = "🎯 Devastating Kill!";
                    }
                    else
                    {
                        title = "⚔️ Player Eliminated";
                    }
                    
                    embed.SetTitle(title)
                         .SetColor(embedColor)
                         .AddField("💀 Elimination", $"**Killer:** {killerName}\n**Victim:** {victimName}", false);
                    
                    // Combat details
                    if (_configData.GlobalSettings.ShowCombatDetails)
                    {
                        string combatDetails = GetCombatDetails(info, distance, damage, weapon, isHighDamage);
                        embed.AddField("🔫 Combat Stats", combatDetails, false);
                    }
                }
            }
            else
            {
                // PvE Death
                string entityName = GetEntityName(info.Initiator);
                description = $"**{victimName}** was killed by **{entityName}**";
                embed.SetTitle($"{deathIcon} Environmental Death")
                     .AddField("Death Type", "PvE", true)
                     .AddField("Killed By", entityName, true);
                
                if (!string.IsNullOrEmpty(weapon))
                {
                    embed.AddField("Method", weapon, true);
                }
            }
            
            embed.SetDescription(description);
            
            // Add coordinates if enabled
            if (_configData.GlobalSettings.IncludeDeathCoordinates)
            {
                string gridPos = GetGridPosition(deathPosition);
                string coords = $"**Grid:** {gridPos}\n**Position:** {deathPosition.x:F1}, {deathPosition.y:F1}, {deathPosition.z:F1}";
                embed.AddField("📍 Death Location", coords, false);
                embed.AddField("🚁 Quick Teleport", $"`teleportpos {deathPosition.x:F1} {deathPosition.y:F1} {deathPosition.z:F1}`", false);
            }
            
            // Add separator
            embed.AddField("─────────────────────", "​", false);
            
            var discordMessage = new DiscordMessage().AddEmbed(embed);
            DiscordSendEmbedMessage(discordMessage, _configData.GlobalSettings.PrivateAdminWebhook);
        }

        private string GetCombatDetails(HitInfo info, float distance, float damage, string weapon, bool isHighDamage)
        {
            string distanceCategory = GetDistanceCategory(distance);
            string bodyPart = GetBodyPartHit(info);
            bool isHeadshot = IsHeadshot(info);
            
            // Enhanced damage display with body part
            string damageInfo;
            if (isHeadshot)
            {
                damageInfo = $"**{damage:F1}** 💀 ({bodyPart} shot)";
            }
            else if (isHighDamage)
            {
                damageInfo = $"**{damage:F1}** 🎯 ({bodyPart} shot)";
            }
            else
            {
                damageInfo = $"{damage:F1} ({bodyPart} shot)";
            }
            
            string details = $"**Weapon:** {weapon ?? "Unknown"}\n**Damage:** {damageInfo}";
            
            if (_configData.GlobalSettings.ShowKillDistance)
            {
                details += $"\n**Range:** {distance:F1}m ({distanceCategory})";
            }
            
            // Add special indicators
            if (isHeadshot)
            {
                details += "\n💀 **HEADSHOT ELIMINATION!**";
            }
            else if (isHighDamage)
            {
                details += "\n🎯 **High Damage Hit!**";
            }
            
            if (distance > 150f)
            {
                details += "\n🏹 **Long Range Snipe!**";
            }
            else if (distance < 3f)
            {
                details += "\n💥 **Point Blank!**";
            }
            
            return details;
        }

        private string GetDistanceCategory(float distance)
        {
            return distance switch
            {
                < 3f => "Point Blank",
                < 15f => "Close Quarters",
                < 50f => "Medium Range",
                < 100f => "Long Range",
                < 200f => "Sniper Range",
                _ => "Extreme Range"
            };
        }

        private bool IsDrowningDeath(HitInfo info)
        {
            if (info == null)
                return false;

            // Check if the primary damage type is drowning
            if (info.damageTypes.Has(Rust.DamageType.Drowned))
                return true;

            // Check if drowning damage is the highest damage type
            float drownDamage = info.damageTypes.Get(Rust.DamageType.Drowned);
            if (drownDamage > 0 && drownDamage >= info.damageTypes.Total() * 0.8f) // 80% or more is drowning
                return true;

            return false;
        }

        private string GetDeathType(BasePlayer victim, HitInfo info)
        {
            if (info?.Initiator == null)
                return "Unknown";
            
            if (IsDrowningDeath(info))
                return "Drowning";
            
            if (info.Initiator is BasePlayer killer)
            {
                return killer.userID == victim.userID ? "Suicide" : "PvP";
            }
            
            return "PvE";
        }

        private bool ShouldReportDeathType(string deathType)
        {
            return deathType switch
            {
                "PvP" => _configData.PlayerDeathSettings.EnablePvP,
                "PvE" => _configData.PlayerDeathSettings.EnablePvE,
                "Suicide" => _configData.PlayerDeathSettings.EnableSuicide,
                "Drowning" => _configData.PlayerDeathSettings.EnableDrowning,
                _ => true // Unknown deaths still reported
            };
        }

        private string GetDeathIcon(BasePlayer victim, HitInfo info)
        {
            if (info?.Initiator == null)
                return "💀";
            
            if (info.Initiator is BasePlayer killer)
            {
                if (killer.userID == victim.userID)
                    return "🔫"; // Suicide
                else
                    return "⚔️"; // PvP
            }
            
            // PvE deaths - different icons based on entity type
            string typeName = info.Initiator.GetType().Name;
            
            if (typeName.Contains("Bear") || typeName.Contains("Wolf") || typeName.Contains("Boar"))
                return "🐻";
            if (typeName.Contains("Helicopter") || typeName.Contains("Bradley"))
                return "🚁";
            if (typeName.Contains("Turret") || typeName.Contains("Trap"))
                return "🔧";
            if (typeName.Contains("Fire") || typeName.Contains("Heat"))
                return "🔥";
            if (typeName.Contains("Scientist") || typeName.Contains("NPC"))
                return "🤖";
            
            return "💀"; // Default
        }

        private int GetDeathColor(BasePlayer victim, HitInfo info)
        {
            if (info?.Initiator == null)
                return 0x808080; // Gray for unknown
            
            if (info.Initiator is BasePlayer killer)
            {
                if (killer.userID == victim.userID)
                    return 0xFFFF00; // Yellow for suicide
                else
                    return 0xFF0000; // Red for PvP
            }
            
            return 0xFF8C00; // Orange for PvE
        }

        private string GetSimpleDeathMessage(BasePlayer victim, HitInfo info, Vector3 deathPosition)
        {
            string victimName = ReplaceChars(victim.displayName);
            string deathText = "";
            
            // Check for drowning first
            if (IsDrowningDeath(info))
            {
                deathText = $"{victimName} drowned";
            }
            // Build the death message based on what killed the player
            else if (info?.Initiator == null)
            {
                deathText = $"{victimName} died";
            }
            else if (info.Initiator is BasePlayer killer)
            {
                // Check for suicide
                if (killer.userID == victim.userID)
                {
                    deathText = $"{victimName} committed suicide";
                }
                else
                {
                    // PvP kill - include body part and headshot info
                    string weapon = GetWeaponName(info);
                    string bodyPart = GetBodyPartHit(info);
                    bool isHeadshot = IsHeadshot(info);
                    
                    string killerName = ReplaceChars(killer.displayName);
                    string bodyPartInfo = isHeadshot ? " (HEADSHOT)" : $" ({bodyPart} shot)";
                    
                    if (!string.IsNullOrEmpty(weapon))
                    {
                        deathText = $"{victimName} was killed by {killerName} with {weapon}{bodyPartInfo}";
                    }
                    else
                    {
                        deathText = $"{victimName} was killed by {killerName}{bodyPartInfo}";
                    }
                }
            }
            else
            {
                // Handle other entities (NPCs, animals, turrets, etc.)
                string entityName = GetEntityName(info.Initiator);
                deathText = $"{victimName} was killed by {entityName}";
            }
            
            // Add coordinates if enabled in config
            if (_configData.GlobalSettings.IncludeDeathCoordinates)
            {
                deathText = deathText + $"\nteleportpos {deathPosition.x:F1} {deathPosition.y:F1} {deathPosition.z:F1}";
            }
            
            return deathText;
        }

        private string GetEntityName(BaseEntity entity)
        {
            if (entity == null)
                return "unknown";

            string typeName = entity.GetType().Name;
            
            // Check if we have a friendly name for this entity type
            if (_entityNames.TryGetValue(typeName, out string friendlyName))
                return friendlyName;
            
            // Check prefab name for some special cases
            string prefabName = entity.ShortPrefabName?.ToLower() ?? "";
            
            if (prefabName.Contains("scientist"))
                return "Scientist";
            if (prefabName.Contains("bradley"))
                return "Bradley APC";
            if (prefabName.Contains("helicopter"))
                return "Helicopter";
            if (prefabName.Contains("turret"))
                return "Turret";
            if (prefabName.Contains("trap"))
                return "Trap";
            if (prefabName.Contains("shark"))
                return "Shark";
            if (prefabName.Contains("bear"))
                return "Bear";
            if (prefabName.Contains("wolf"))
                return "Wolf";
            
            // Fallback: clean up the prefab name
            string cleanName = entity.ShortPrefabName ?? typeName;
            cleanName = cleanName.Replace("_", " ").Replace(".entity", "").Replace(".deployed", "");
            
            // Capitalize first letter
            if (!string.IsNullOrEmpty(cleanName))
            {
                cleanName = char.ToUpper(cleanName[0]) + cleanName.Substring(1);
            }
            
            return cleanName;
        }

        private string GetWeaponName(HitInfo info)
        {
            if (info == null)
                return null;

            // Try to get weapon from item first
            Item item = info.Weapon?.GetItem();
            if (item != null)
                return item.info.displayName.english;

            // Fallback to weapon prefab
            if (info.WeaponPrefab != null)
            {
                string weaponName = info.WeaponPrefab.ShortPrefabName;
                
                // Clean up common weapon prefab names
                weaponName = weaponName.Replace(".entity", "").Replace("_", " ");
                
                // Capitalize first letter of each word
                return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(weaponName);
            }

            return null;
        }

        private string GetBodyPartHit(HitInfo info)
        {
            if (info == null || info.HitBone == 0)
                return "Unknown";

            // Rust bone names mapping to friendly body parts
            string boneName = StringPool.Get(info.HitBone);
            if (string.IsNullOrEmpty(boneName))
                return "Unknown";

            // Convert bone name to body part
            string lowerBone = boneName.ToLower();
            
            if (lowerBone.Contains("head") || lowerBone.Contains("skull") || lowerBone.Contains("jaw") || lowerBone.Contains("neck"))
                return "Head";
            if (lowerBone.Contains("chest") || lowerBone.Contains("spine") || lowerBone.Contains("ribs"))
                return "Chest";
            if (lowerBone.Contains("stomach") || lowerBone.Contains("pelvis") || lowerBone.Contains("hip"))
                return "Stomach";
            if (lowerBone.Contains("arm") || lowerBone.Contains("hand") || lowerBone.Contains("finger") || lowerBone.Contains("shoulder"))
                return "Arm";
            if (lowerBone.Contains("leg") || lowerBone.Contains("foot") || lowerBone.Contains("toe") || lowerBone.Contains("thigh") || lowerBone.Contains("calf"))
                return "Leg";
                
            return "Torso"; // Fallback for unidentified bones
        }

        private bool IsHeadshot(HitInfo info)
        {
            if (info == null || info.HitBone == 0)
                return false;

            string boneName = StringPool.Get(info.HitBone);
            if (string.IsNullOrEmpty(boneName))
                return false;

            string lowerBone = boneName.ToLower();
            return lowerBone.Contains("head") || lowerBone.Contains("skull") || lowerBone.Contains("jaw");
        }

        private void OnEntitySpawned(EggHuntEvent entity) => HandleEntity(entity);

        private void OnEntitySpawned(SantaSleigh entity) => HandleEntity(entity);

        private void OnEntitySpawned(XMasRefill entity) => HandleEntity(entity);

        private void OnEntityKill(EggHuntEvent entity)
        {
            if (!entity.IsValid())
            {
                return;
            }

            List<EggHuntEvent.EggHunter> topHunters = Facepunch.Pool.Get<List<EggHuntEvent.EggHunter>>();
            foreach (KeyValuePair<ulong, EggHuntEvent.EggHunter> eggHunter in entity._eggHunters)
            {
                topHunters.Add(eggHunter.Value);
            }

            topHunters.Sort((EggHuntEvent.EggHunter a, EggHuntEvent.EggHunter b) => b.numEggs.CompareTo(a.numEggs));

            string winner;
            if (topHunters.Count > 0)
            {
                winner = ReplaceChars(topHunters[0].displayName);
            }
            else
            {
                winner = "No winner";
            }

            Facepunch.Pool.FreeUnmanaged(ref topHunters);

            bool isHalloween = entity is HalloweenHunt;
            if (isHalloween)
            {
                if (_configData.HalloweenSettings.Enabled)
                {
                    LogToConsole("Halloween Hunt Event has ended. The winner is " + winner);

                    DiscordSendMessage(Lang(LangKeys.Event.HalloweenWinner, null, winner), _configData.GlobalSettings.ServerMessagesWebhook);
                }
            }
            else
            {
                if (_configData.EasterSettings.Enabled)
                {
                    LogToConsole("Egg Hunt Event has ended. The winner is " + winner);

                    DiscordSendMessage(Lang(LangKeys.Event.EasterWinner, null, winner), _configData.GlobalSettings.ServerMessagesWebhook);
                }
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (_configData.PlayerConnectedInfoSettings.Enabled)
            {
                if (_configData.GlobalSettings.UseEmbedForConnections)
                {
                    SendEnhancedConnectionMessage(player);
                }
                else
                {
                    // Fallback to simple message
                    string discordMessage = $":link: <t:{DateTimeOffset.Now.ToUnixTimeSeconds()}:t> {ReplaceChars(player.displayName)} connected. SteamID: `{player.UserIDString}` IP: `{player.net.connection.ipaddress.Split(':')[0]}`";
                    DiscordSendMessage(discordMessage, _configData.GlobalSettings.PrivateAdminWebhook);
                }
            }
        }

        private void SendEnhancedConnectionMessage(BasePlayer player)
        {
            string playerName = ReplaceChars(player.displayName);
            string ipAddress = player.net.connection.ipaddress.Split(':')[0];
            string steamProfileUrl = $"https://steamcommunity.com/profiles/{player.userID}";
            
            var embed = new DiscordEmbed()
                .SetColor(0x00FF00) // Green for connections
                .SetTitle("🔗 Player Connected")
                .SetTimestamp(DateTimeOffset.Now);

            // Start building player details with Steam profile link
            string playerDetails = $"**Name:** {playerName}\n**Steam ID:** [{player.UserIDString}]({steamProfileUrl})\n**IP Address:** `{ipAddress}`";
            
            // Get Steam profile data and country info (both async)
            GetSteamProfileData(player.userID.ToString(), (steamData) => {
                if (!string.IsNullOrEmpty(steamData))
                {
                    playerDetails += $"\n{steamData}";
                }
                
                // Add country info if enabled
                if (_configData.GlobalSettings.ShowCountryInfo)
                {
                    GetCountryInfo(ipAddress, (countryInfo) => {
                        if (!string.IsNullOrEmpty(countryInfo))
                        {
                            playerDetails += $"\n**Location:** {countryInfo}";
                        }
                        
                        embed.AddField("👤 Player Details", playerDetails, false);
                        
                        // Add separator
                        embed.AddField("─────────────────────", "​", false);
                        
                        var discordMessage = new DiscordMessage().AddEmbed(embed);
                        DiscordSendEmbedMessage(discordMessage, _configData.GlobalSettings.PrivateAdminWebhook);
                    });
                }
                else
                {
                    embed.AddField("👤 Player Details", playerDetails, false);
                    
                    // Add separator
                    embed.AddField("─────────────────────", "​", false);
                    
                    var discordMessage = new DiscordMessage().AddEmbed(embed);
                    DiscordSendEmbedMessage(discordMessage, _configData.GlobalSettings.PrivateAdminWebhook);
                }
            });
        }

        private void GetSteamProfileData(string steamID, System.Action<string> callback)
        {
            try
            {
                // Check if Steam API key is configured
                if (string.IsNullOrEmpty(_configData.GlobalSettings.SteamWebAPIKey))
                {
                    callback(null);
                    return;
                }

                // Use Steam Web API with API key to get detailed player info
                string url = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={_configData.GlobalSettings.SteamWebAPIKey}&steamids={steamID}";
                
                webrequest.Enqueue(url, null, (code, response) =>
                {
                    try
                    {
                        if (code == 200 && !string.IsNullOrEmpty(response))
                        {
                            var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
                            if (data != null && data.ContainsKey("response"))
                            {
                                var responseData = JsonConvert.DeserializeObject<Dictionary<string, object>>(data["response"].ToString());
                                if (responseData != null && responseData.ContainsKey("players"))
                                {
                                    var playersArray = JsonConvert.DeserializeObject<object[]>(responseData["players"].ToString());
                                    if (playersArray != null && playersArray.Length > 0)
                                    {
                                        var playerData = JsonConvert.DeserializeObject<Dictionary<string, object>>(playersArray[0].ToString());
                                        if (playerData != null)
                                        {
                                            string steamInfo = ParseSteamProfileData(playerData);
                                            callback(steamInfo);
                                            return;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        PrintError($"Error parsing Steam profile data: {ex.Message}");
                    }
                    
                    // Fallback if request fails
                    callback(null);
                }, this, RequestMethod.GET);
            }
            catch (Exception ex)
            {
                PrintError($"Error getting Steam profile data: {ex.Message}");
                callback(null);
            }
        }

        private string ParseSteamProfileData(Dictionary<string, object> playerData)
        {
            try
            {
                string steamInfo = "";
                
                // Account creation date
                if (playerData.ContainsKey("timecreated") && long.TryParse(playerData["timecreated"].ToString(), out long timeCreated))
                {
                    DateTime createdDate = DateTimeOffset.FromUnixTimeSeconds(timeCreated).DateTime;
                    TimeSpan accountAge = DateTime.Now - createdDate;
                    string ageText = GetAccountAgeText(accountAge);
                    steamInfo += $"**Account Age:** {ageText}";
                }
                
                // Profile state (public/private)
                if (playerData.ContainsKey("communityvisibilitystate"))
                {
                    int visibilityState = int.Parse(playerData["communityvisibilitystate"].ToString());
                    string profileStatus = visibilityState == 3 ? "Public" : "Private/Limited";
                    if (!string.IsNullOrEmpty(steamInfo)) steamInfo += "\n";
                    steamInfo += $"**Profile:** {profileStatus}";
                }
                
                // Profile configured status
                if (playerData.ContainsKey("profilestate"))
                {
                    int profileState = int.Parse(playerData["profilestate"].ToString());
                    string configStatus = profileState == 1 ? "Configured" : "Not Configured";
                    if (!string.IsNullOrEmpty(steamInfo)) steamInfo += "\n";
                    steamInfo += $"**Profile Status:** {configStatus}";
                }
                
                // Last logoff (if available and profile is public)
                if (playerData.ContainsKey("lastlogoff") && long.TryParse(playerData["lastlogoff"].ToString(), out long lastLogoff))
                {
                    DateTime lastSeen = DateTimeOffset.FromUnixTimeSeconds(lastLogoff).DateTime;
                    TimeSpan timeSince = DateTime.Now - lastSeen;
                    if (timeSince.TotalMinutes < 5) // Recently online
                    {
                        if (!string.IsNullOrEmpty(steamInfo)) steamInfo += "\n";
                        steamInfo += $"**Last Seen:** Just now";
                    }
                    else if (timeSince.TotalHours < 24)
                    {
                        if (!string.IsNullOrEmpty(steamInfo)) steamInfo += "\n";
                        steamInfo += $"**Last Seen:** {timeSince.Hours}h {timeSince.Minutes}m ago";
                    }
                }
                
                return steamInfo;
            }
            catch (Exception ex)
            {
                PrintWarning($"Error parsing Steam profile data: {ex.Message}");
                return null;
            }
        }

        private string GetAccountAgeText(TimeSpan accountAge)
        {
            if (accountAge.TotalDays < 30)
            {
                return $"{(int)accountAge.TotalDays} days (New Account ⚠️)";
            }
            else if (accountAge.TotalDays < 365)
            {
                int months = (int)(accountAge.TotalDays / 30);
                return $"{months} months";
            }
            else
            {
                int years = (int)(accountAge.TotalDays / 365);
                int remainingMonths = (int)((accountAge.TotalDays % 365) / 30);
                if (remainingMonths > 0)
                {
                    return $"{years}y {remainingMonths}m";
                }
                else
                {
                    return $"{years} years";
                }
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (!player.IsValid())
            {
                return;
            }

            if (_configData.PlayerDisconnectedSettings.Enabled)
            {
                LogToConsole($"Player {player.displayName} disconnected ({reason}).");

                if (!_configData.GlobalSettings.HideAdmin || !player.IsAdmin)
                {
                    if (_configData.GlobalSettings.UseEmbedForConnections)
                    {
                        SendEnhancedDisconnectionMessage(player, reason);
                    }
                    else
                    {
                        // Try to get IP but handle null connection gracefully
                        string ipAddress = "Unknown";
                        try
                        {
                            if (player.net?.connection?.ipaddress != null)
                            {
                                ipAddress = player.net.connection.ipaddress.Split(':')[0];
                            }
                        }
                        catch
                        {
                            ipAddress = "Unknown";
                        }

                        // Fallback to simple message
                        string discordMessage = $":wave: <t:{DateTimeOffset.Now.ToUnixTimeSeconds()}:t> {ReplaceChars(player.displayName)} disconnected ({ReplaceChars(reason)}) SteamID: `{player.UserIDString}` IP: `{ipAddress}`";
                        DiscordSendMessage(discordMessage, _configData.GlobalSettings.PrivateAdminWebhook);
                    }
                }
            }
        }

        private void SendEnhancedDisconnectionMessage(BasePlayer player, string reason)
        {
            string playerName = ReplaceChars(player.displayName);
            string steamProfileUrl = $"https://steamcommunity.com/profiles/{player.userID}";
            
            // Try to get IP but handle null connection gracefully
            string ipAddress = "Unknown";
            try
            {
                if (player.net?.connection?.ipaddress != null)
                {
                    ipAddress = player.net.connection.ipaddress.Split(':')[0];
                }
            }
            catch
            {
                ipAddress = "Unknown";
            }
            
            var embed = new DiscordEmbed()
                .SetColor(0xFF4500) // Orange-Red for disconnections
                .SetTitle("👋 Player Disconnected")
                .SetTimestamp(DateTimeOffset.Now);

            // Player Details Section with clickable Steam ID
            string playerDetails = $"**Name:** {playerName}\n**Steam ID:** [{player.UserIDString}]({steamProfileUrl})\n**IP Address:** `{ipAddress}`\n**Reason:** {ReplaceChars(reason)}";
            embed.AddField("👤 Player Details", playerDetails, false);
            
            // Add separator
            embed.AddField("─────────────────────", "​", false);
            
            var discordMessage = new DiscordMessage().AddEmbed(embed);
            DiscordSendEmbedMessage(discordMessage, _configData.GlobalSettings.PrivateAdminWebhook);
        }

        private void GetCountryInfo(string ipAddress, System.Action<string> callback)
        {
            // Use free IP geolocation service (ip-api.com - no API key required, 45 requests/minute limit)
            string url = $"http://ip-api.com/json/{ipAddress}?fields=country,countryCode";
            
            webrequest.Enqueue(url, null, (code, response) =>
            {
                try
                {
                    if (code == 200 && !string.IsNullOrEmpty(response))
                    {
                        var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
                        if (data != null && data.ContainsKey("country") && data.ContainsKey("countryCode"))
                        {
                            string country = data["country"].ToString();
                            string countryCode = data["countryCode"].ToString();
                            
                            // Get country flag emoji
                            string flagEmoji = GetCountryFlagEmoji(countryCode);
                            callback($"{flagEmoji} {country}");
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    PrintWarning($"Error getting country info: {ex.Message}");
                }
                
                // Fallback if request fails
                callback(null);
            }, this, RequestMethod.GET);
        }

        private string GetCountryFlagEmoji(string countryCode)
        {
            if (string.IsNullOrEmpty(countryCode) || countryCode.Length != 2)
                return "🌍";
            
            // Convert country code to flag emoji
            // Each flag emoji is made up of two Regional Indicator Symbol letters
            countryCode = countryCode.ToUpper();
            int firstLetter = countryCode[0] - 'A' + 0x1F1E6;
            int secondLetter = countryCode[1] - 'A' + 0x1F1E6;
            
            return char.ConvertFromUtf32(firstLetter) + char.ConvertFromUtf32(secondLetter);
        }

        private void OnPlayerChat(BasePlayer player, string message, ConVar.Chat.ChatChannel channel)
        {
            if (!player.IsValid() || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (IsPluginLoaded(BetterChatMute))
            {
                _resultCall = BetterChatMute.Call("API_IsMuted", player.IPlayer);

                if (_resultCall is bool && (bool)_resultCall)
                {
                    return;
                }
            }

            if (_configData.GlobalSettings.UseAntiSpam && IsPluginLoaded(AntiSpam))
            {
                _resultCall = AntiSpam.Call("GetSpamFreeText", message);

                message = (_resultCall as string);

                if (string.IsNullOrWhiteSpace(message))
                {
                    return;
                }
            }

            if (_configData.GlobalSettings.UseUFilter && IsPluginLoaded(UFilter))
            {
                _sb.Clear();
                _sb.Append(message);

                _resultCall = UFilter.Call("Profanities", message);

                if (_resultCall is string[])
                {
                    _profanities = _resultCall as string[];
                }

                if (_profanities != null)
                {
                    foreach (string profanity in _profanities)
                    {
                        _sb.Replace(profanity, new string('＊', profanity.Length));
                    }
                }

                message = _sb.ToString();

                if (string.IsNullOrWhiteSpace(message))
                {
                    return;
                }
            }

            message = ReplaceChars(message);

            switch (channel)
            {
                case ConVar.Chat.ChatChannel.Global:
                case ConVar.Chat.ChatChannel.Local:
                    if (_configData.ChatSettings.Enabled)
                    {
                        DiscordSendMessage(Lang(LangKeys.Event.Chat, null, ReplaceChars(player.displayName), message), _configData.GlobalSettings.PublicChatWebhook);
                    }
                    break;
                case ConVar.Chat.ChatChannel.Team:
                    if (_configData.ChatTeamSettings.Enabled)
                    {
                        SendEnhancedTeamChatMessage(player, message);
                    }
                    break;
            }
        }

        private void SendEnhancedTeamChatMessage(BasePlayer player, string message)
        {
            string playerName = ReplaceChars(player.displayName);
            string steamProfileUrl = $"https://steamcommunity.com/profiles/{player.userID}";
            
            var embed = new DiscordEmbed()
                .SetColor(0x3498DB) // Blue color for team chat
                .SetTitle("👥 Team Chat")
                .SetTimestamp(DateTimeOffset.Now);

            // Player details with Steam profile link and location
            Vector3 playerPos = player.transform.position;
            string gridPos = GetGridPosition(playerPos);
            string playerDetails = $"**Player:** {playerName}\n**Steam ID:** [{player.UserIDString}]({steamProfileUrl})\n**Location:** {gridPos}\n**Teleport:** `teleportpos {playerPos.x:F1} {playerPos.y:F1} {playerPos.z:F1}`";
            embed.AddField("👤 Player Info", playerDetails, false);
            
            // Message content
            embed.AddField("💬 Message", message, false);
            
            // Team information
            string teamInfo = GetTeamInformation(player);
            if (!string.IsNullOrEmpty(teamInfo))
            {
                embed.AddField("🛡️ Team Information", teamInfo, false);
            }
            
            // Add separator
            embed.AddField("─────────────────────", "​", false);
            
            var discordMessage = new DiscordMessage().AddEmbed(embed);
            DiscordSendEmbedMessage(discordMessage, _configData.GlobalSettings.PrivateAdminWebhook);
        }

        private string GetTeamInformation(BasePlayer player)
        {
            try
            {
                // Check if player has a team
                if (player.currentTeam == 0UL)
                {
                    return "**Team Status:** Not in a team";
                }

                // Get the team
                RelationshipManager.PlayerTeam team = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
                if (team == null)
                {
                    return "**Team Status:** Team not found";
                }

                _sb.Clear();
                
                // Team basic info
                _sb.AppendLine($"**Team ID:** {team.teamID}");
                _sb.AppendLine($"**Team Size:** {team.members.Count}");
                
                // Count online vs offline members
                int onlineCount = 0;
                int sleepingCount = 0;
                int offlineCount = 0;
                
                _sb.AppendLine($"**Team Members:**");
                
                foreach (ulong memberID in team.members)
                {
                    BasePlayer member = BasePlayer.FindByID(memberID);
                    string memberName = "Unknown";
                    string status = "Offline";
                    
                    if (member != null)
                    {
                        memberName = ReplaceChars(member.displayName);
                        
                        if (member.IsConnected)
                        {
                            if (member.IsSleeping())
                            {
                                status = "💤 Sleeping";
                                sleepingCount++;
                            }
                            else
                            {
                                status = "🟢 Online";
                                onlineCount++;
                            }
                        }
                        else
                        {
                            status = "🔴 Offline";
                            offlineCount++;
                        }
                    }
                    else
                    {
                        // Try to get name from sleeping player or cached data
                        var sleepingPlayer = BasePlayer.FindSleeping(memberID);
                        if (sleepingPlayer != null)
                        {
                            memberName = ReplaceChars(sleepingPlayer.displayName);
                            status = "💤 Sleeping";
                            sleepingCount++;
                        }
                        else
                        {
                            // Player is completely offline
                            memberName = $"Player_{memberID.ToString().Substring(0, 8)}";
                            status = "🔴 Offline";
                            offlineCount++;
                        }
                    }
                    
                    _sb.AppendLine($"• {memberName} - {status}");
                }
                
                // Add summary
                _sb.AppendLine($"**Status Summary:** {onlineCount} Online, {sleepingCount} Sleeping, {offlineCount} Offline");
                
                return _sb.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                PrintError($"Error getting team information: {ex.Message}");
                return "**Team Status:** Error retrieving team data";
            }
        }

        private void OnRaidableBaseStarted(Vector3 raidPos, int difficulty)
        {
            HandleRaidableBase(raidPos, difficulty, LangKeys.Plugin.RaidableBaseStarted);
        }

        private void OnRaidableBaseEnded(Vector3 raidPos, int difficulty)
        {
            HandleRaidableBase(raidPos, difficulty, LangKeys.Plugin.RaidableBaseEnded);
        }

        private void OnRaidableBaseCompleted(Vector3 raidPos, int difficulty, bool allowPVP, string id, float spawnTime, float despawnTime, float loadTime, ulong ownerId, BasePlayer owner, List<BasePlayer> raiders)
        {
            HandleRaidableBase(raidPos, difficulty, LangKeys.Plugin.RaidableBaseCompleted, owner, raiders);
        }

        private void OnRconConnection(IPAddress ip)
        {
            if (!_configData.RconConnectionSettings.Enabled)
            {
                return;
            }

            // Check if IP is in trusted whitelist (skip notifications for trusted IPs)
            string ipString = ip.ToString();
            if (_configData.GlobalSettings.RCONTrustedIPs.Contains(ipString))
            {
                LogToConsole($"RCON connection from trusted IP {ip} (notification suppressed)");
                return;
            }

            LogToConsole($"RCON connection is opened from {ip}");

            if (_configData.GlobalSettings.UseEmbedForRcon)
            {
                SendEnhancedRconConnectionMessage(ipString);
            }
            else
            {
                // Fallback to simple message
                DiscordSendMessage(Lang(LangKeys.Event.RconConnection, null, ipString), _configData.GlobalSettings.PrivateAdminWebhook);
            }
        }

        private void SendEnhancedRconConnectionMessage(string ipAddress)
        {
            var embed = new DiscordEmbed()
                .SetColor(0xFF4500) // Orange-Red for security alert (untrusted IP)
                .SetTitle("🚨 RCON Connection from Unknown IP")
                .SetDescription("**⚠️ Remote administration connection from untrusted IP address**")
                .SetTimestamp(DateTimeOffset.Now);

            // Connection Info
            embed.AddField("🌐 Connection Details", $"**IP Address:** `{ipAddress}`", true);
            
            // Add country info if enabled (async call)
            if (_configData.GlobalSettings.ShowCountryInfo)
            {
                GetCountryInfo(ipAddress, (countryInfo) => {
                    if (!string.IsNullOrEmpty(countryInfo))
                    {
                        embed.AddField("📍 Location", countryInfo, true);
                    }
                    
                    // Security warning
                    embed.AddField("⚠️ Security Notice", "Administrative access granted to remote connection", false);
                    
                    // Add separator
                    embed.AddField("─────────────────────", "​", false);
                    
                    var discordMessage = new DiscordMessage().AddEmbed(embed);
                    DiscordSendEmbedMessage(discordMessage, _configData.GlobalSettings.PrivateAdminWebhook);
                });
            }
            else
            {
                // Security warning
                embed.AddField("⚠️ Security Notice", "Administrative access granted to remote connection", false);
                
                // Add separator
                embed.AddField("─────────────────────", "​", false);
                
                var discordMessage = new DiscordMessage().AddEmbed(embed);
                DiscordSendEmbedMessage(discordMessage, _configData.GlobalSettings.PrivateAdminWebhook);
            }
        }

        private void OnRconCommand(IPAddress ip, string command, string[] args)
        {
            if (!_configData.RconCommandSettings.Enabled)
            {
                return;
            }

            foreach (string rconCommand in _configData.GlobalSettings.RCONCommandBlacklist)
            {
                if (command.ToLower().Equals(rconCommand.ToLower()))
                {
                    return;
                }
            }

            for (int i = 0; i < args.Length; i++)
            {
                command += $" {args[i]}";
            }

            LogToConsole($"RCON command {command} is run from {ip}");

            if (_configData.GlobalSettings.UseEmbedForRcon)
            {
                SendEnhancedRconCommandMessage(ip.ToString(), command);
            }
            else
            {
                // Fallback to simple message
                DiscordSendMessage(Lang(LangKeys.Event.RconCommand, null, command, ip), _configData.GlobalSettings.PrivateAdminWebhook);
            }
        }

        private void SendEnhancedRconCommandMessage(string ipAddress, string command)
        {
            // Determine command category and appropriate color/icon
            var commandInfo = CategorizeRconCommand(command);
            
            var embed = new DiscordEmbed()
                .SetColor(commandInfo.Color)
                .SetTitle($"{commandInfo.Icon} RCON Command - {commandInfo.Category}")
                .SetTimestamp(DateTimeOffset.Now);

            // Command Details
            embed.AddField("💻 Command Executed", $"`{command}`", false);
            embed.AddField("🌐 Source IP", $"`{ipAddress}`", true);
            
            // Add severity indicator for critical commands
            if (commandInfo.IsCritical)
            {
                embed.AddField("⚠️ Severity", "**Critical Admin Action**", true);
            }
            
            // Add country info if enabled (async call)
            if (_configData.GlobalSettings.ShowCountryInfo)
            {
                GetCountryInfo(ipAddress, (countryInfo) => {
                    if (!string.IsNullOrEmpty(countryInfo))
                    {
                        embed.AddField("📍 Location", countryInfo, true);
                    }
                    
                    // Add separator
                    embed.AddField("─────────────────────", "​", false);
                    
                    var discordMessage = new DiscordMessage().AddEmbed(embed);
                    DiscordSendEmbedMessage(discordMessage, _configData.GlobalSettings.PrivateAdminWebhook);
                });
            }
            else
            {
                // Add separator
                embed.AddField("─────────────────────", "​", false);
                
                var discordMessage = new DiscordMessage().AddEmbed(embed);
                DiscordSendEmbedMessage(discordMessage, _configData.GlobalSettings.PrivateAdminWebhook);
            }
        }

        private RconCommandInfo CategorizeRconCommand(string command)
        {
            string lowerCommand = command.ToLower();
            
            // Server Configuration Commands
            if (lowerCommand.StartsWith("server."))
            {
                return new RconCommandInfo
                {
                    Category = "Server Config",
                    Icon = "🔧",
                    Color = 0x3498DB, // Blue
                    IsCritical = false
                };
            }
            
            // Admin Actions (Critical)
            if (lowerCommand.StartsWith("kick ") || lowerCommand.StartsWith("ban ") || 
                lowerCommand.StartsWith("unban ") || lowerCommand.StartsWith("mute ") ||
                lowerCommand.StartsWith("unmute ") || lowerCommand.StartsWith("teleport ") ||
                lowerCommand.Contains("oxide.grant") || lowerCommand.Contains("oxide.revoke"))
            {
                return new RconCommandInfo
                {
                    Category = "Admin Action",
                    Icon = "⚠️",
                    Color = 0xE74C3C, // Red
                    IsCritical = true
                };
            }
            
            // Information Commands
            if (lowerCommand.StartsWith("status") || lowerCommand.StartsWith("playerlist") ||
                lowerCommand.StartsWith("listid") || lowerCommand.StartsWith("find ") ||
                lowerCommand.StartsWith("players") || lowerCommand.StartsWith("global."))
            {
                return new RconCommandInfo
                {
                    Category = "Information",
                    Icon = "📊",
                    Color = 0x95A5A6, // Gray
                    IsCritical = false
                };
            }
            
            // Game Commands
            if (lowerCommand.StartsWith("say ") || lowerCommand.StartsWith("give ") ||
                lowerCommand.StartsWith("spawn ") || lowerCommand.StartsWith("weather ") ||
                lowerCommand.StartsWith("time ") || lowerCommand.StartsWith("env."))
            {
                return new RconCommandInfo
                {
                    Category = "Game Control",
                    Icon = "🎮",
                    Color = 0x9B59B6, // Purple
                    IsCritical = false
                };
            }
            
            // Default for unknown commands
            return new RconCommandInfo
            {
                Category = "Other",
                Icon = "⚡",
                Color = 0xF39C12, // Orange
                IsCritical = false
            };
        }

        private class RconCommandInfo
        {
            public string Category { get; set; }
            public string Icon { get; set; }
            public int Color { get; set; }
            public bool IsCritical { get; set; }
        }

        private void OnUserBanned(string name, string id, string ipAddress, string reason)
        {
            LogToConsole($"Player {name} ({id}) at {ipAddress} was banned: {reason}");

            DiscordSendMessage(Lang(LangKeys.Event.UserBanned, null, ReplaceChars(name), id, ipAddress, ReplaceChars(reason)), _configData.GlobalSettings.PrivateAdminWebhook);
        }

        private void OnUserKicked(IPlayer player, string reason)
        {
            LogToConsole($"Player {player.Name} ({player.Id}) was kicked ({reason})");

            DiscordSendMessage(Lang(LangKeys.Event.UserKicked, null, ReplaceChars(player.Name), player.Id, ReplaceChars(reason)), _configData.GlobalSettings.PrivateAdminWebhook);
        }

        private void OnUserUnbanned(string name, string id, string ipAddress)
        {
            LogToConsole($"Player {name} ({id}) at {ipAddress} was unbanned");

            DiscordSendMessage(Lang(LangKeys.Event.UserUnbanned, null, ReplaceChars(name), id, ipAddress), _configData.GlobalSettings.PrivateAdminWebhook);
        }

        private void OnUserNameUpdated(string id, string oldName, string newName)
        {
            if (oldName.Equals(newName) || oldName.Equals("Unnamed"))
            {
                return;
            }

            LogToConsole($"Player name changed from {oldName} to {newName} for ID {id}");

            DiscordSendMessage(Lang(LangKeys.Event.UserNameUpdated, null, ReplaceChars(oldName), ReplaceChars(newName), id), _configData.GlobalSettings.PrivateAdminWebhook);

            // Log name change to file
            string ipAddress = "Unknown";
            if (ulong.TryParse(id, out ulong steamId))
            {
                BasePlayer player = BasePlayer.FindByID(steamId);
                if (player != null)
                {
                    try
                    {
                        if (player.net?.connection?.ipaddress != null)
                        {
                            ipAddress = player.net.connection.ipaddress.Split(':')[0];
                        }
                    }
                    catch
                    {
                        ipAddress = "Unknown";
                    }
                }
            }

            var entry = new NameChangeEntry
            {
                SteamID = id,
                OldName = oldName,
                NewName = newName,
                TimestampUTC = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                TimestampLocal = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                IPAddress = ipAddress,
                ServerName = ConVar.Server.hostname
            };

            _nameChangeLogData.NameChanges.Add(entry);
            SaveNameChangeLogData();
        }

        private object CanRenameBed(BasePlayer player, SleepingBag bed, string bedName)
        {
            if (!_configData.BedRenameSettings.Enabled || player == null || bed == null)
                return null;

            string playerName = ReplaceChars(player.displayName);
            string ownerName = GetPlayerName(bed.OwnerID);
            string deployerName = GetPlayerName(bed.deployerUserID);
            string oldName = bed.niceName ?? "";
            string itemType = GetBedItemType(bed);
            string position = $"X: {Math.Round((decimal)bed.ServerPosition.x)} Y: {Math.Round((decimal)bed.ServerPosition.y)} Z: {Math.Round((decimal)bed.ServerPosition.z)}";

            // Blacklist check
            if (_configData.BedRenameSettings.Blacklist.Enabled)
            {
                List<string> detectedTerms = CheckBedNameBlacklist(bedName);

                if (detectedTerms.Count > 0)
                {
                    string detectedStr = string.Join(", ", detectedTerms);

                    LogToConsole($"[Blacklist] {player.displayName} ({player.UserIDString}) attempted to rename {itemType} owned by {ownerName} ({bed.OwnerID}) to: {bedName} - Detected: {detectedStr}");

                    // Discord alert for blacklist hit
                    if (_configData.BedRenameSettings.SendDiscordEmbed)
                    {
                        SendBedRenameBlacklistEmbed(player, bed, bedName, oldName, itemType, position, ownerName, deployerName, detectedStr);
                    }

                    // Log blacklist hit to file
                    if (_configData.BedRenameSettings.LogToFile)
                    {
                        SaveBedRenameEntry(player, bed, bedName, oldName, itemType, position, ownerName, deployerName, true, detectedStr);
                    }

                    // Block the rename if configured
                    if (_configData.BedRenameSettings.Blacklist.BlockRename)
                    {
                        return true;
                    }
                }
            }

            LogToConsole($"{player.displayName} ({player.UserIDString}) renamed {itemType} owned by {ownerName} ({bed.OwnerID}) from \"{oldName}\" to \"{bedName}\"");

            // Discord embed for normal rename
            if (_configData.BedRenameSettings.SendDiscordEmbed)
            {
                SendBedRenameEmbed(player, bed, bedName, oldName, itemType, position, ownerName, deployerName);
            }

            // Log to file
            if (_configData.BedRenameSettings.LogToFile)
            {
                SaveBedRenameEntry(player, bed, bedName, oldName, itemType, position, ownerName, deployerName, false, null);
            }

            return null;
        }

        private string GetBedItemType(SleepingBag bed)
        {
            switch (bed.ShortPrefabName)
            {
                case "sleepingbag_leather_deployed":
                    return "Sleeping Bag";
                case "bed_deployed":
                    return "Bed";
                case "beach_towel_deployed":
                    return "Beach Towel";
                default:
                    return "Sleeping Bag";
            }
        }

        private string GetPlayerName(ulong playerID)
        {
            var player = covalence.Players.FindPlayerById(playerID.ToString());
            if (player != null)
                return player.Name;
            return playerID + " (Unknown)";
        }

        private List<string> CheckBedNameBlacklist(string bedName)
        {
            var detected = new List<string>();
            var blacklist = _configData.BedRenameSettings.Blacklist;

            string bedNameLeet = blacklist.LeetConversionEnabled ? TranslateLeet(bedName) : null;

            // Simple term matching
            if (blacklist.BlacklistedTerms != null)
            {
                string[] words = bedName.Split(' ');
                foreach (string word in words)
                {
                    foreach (string term in blacklist.BlacklistedTerms)
                    {
                        if (word.Equals(term, StringComparison.OrdinalIgnoreCase) && !detected.Contains(term))
                        {
                            detected.Add(term);
                        }
                    }
                }

                // Leet-converted term matching
                if (bedNameLeet != null)
                {
                    string[] leetWords = bedNameLeet.Split(' ');
                    foreach (string word in leetWords)
                    {
                        foreach (string term in blacklist.BlacklistedTerms)
                        {
                            if (word.Equals(term, StringComparison.OrdinalIgnoreCase) && !detected.Contains(term))
                            {
                                detected.Add(term);
                            }
                        }
                    }
                }
            }

            // Regex matching
            if (blacklist.BlacklistedREGEX != null)
            {
                foreach (string pattern in blacklist.BlacklistedREGEX)
                {
                    try
                    {
                        Regex regex = new Regex(pattern);

                        if (regex.IsMatch(bedName) && !detected.Contains(pattern))
                            detected.Add(pattern);

                        if (bedNameLeet != null && regex.IsMatch(bedNameLeet) && !detected.Contains(pattern))
                            detected.Add(pattern);
                    }
                    catch (Exception ex)
                    {
                        PrintWarning($"Invalid blacklist regex pattern '{pattern}': {ex.Message}");
                    }
                }
            }

            return detected;
        }

        private string TranslateLeet(string original)
        {
            string translated = original;
            var leetTable = _configData.BedRenameSettings.Blacklist.LeetTable;

            if (leetTable != null)
            {
                foreach (var entry in leetTable)
                    translated = translated.Replace(entry.Key, entry.Value);
            }

            return translated;
        }

        private void SendBedRenameEmbed(BasePlayer player, SleepingBag bed, string newName, string oldName, string itemType, string position, string ownerName, string deployerName)
        {
            string steamProfileUrl = $"https://steamcommunity.com/profiles/{player.userID}";
            string ownerProfileUrl = $"https://steamcommunity.com/profiles/{bed.OwnerID}";

            var embed = new DiscordEmbed()
                .SetColor(0xFFA500) // Orange for renames
                .SetTitle($"🏷️ {itemType} Renamed")
                .SetTimestamp(DateTimeOffset.Now);

            string playerDetails = $"**Name:** {ReplaceChars(player.displayName)}\n**Steam ID:** [{player.UserIDString}]({steamProfileUrl})";
            embed.AddField("👤 Renamed By", playerDetails, false);

            if (bed.OwnerID != player.userID)
            {
                embed.AddField("👑 Owner", $"[{ReplaceChars(ownerName)}]({ownerProfileUrl}) (`{bed.OwnerID}`)", false);
            }

            if (bed.deployerUserID != 0 && bed.deployerUserID != player.userID && bed.deployerUserID != bed.OwnerID)
            {
                string deployerProfileUrl = $"https://steamcommunity.com/profiles/{bed.deployerUserID}";
                embed.AddField("🔨 Deployer", $"[{ReplaceChars(deployerName)}]({deployerProfileUrl}) (`{bed.deployerUserID}`)", false);
            }

            embed.AddField("📝 Name Change", $"**From:** `{oldName}`\n**To:** `{newName}`", false);
            embed.AddField("📍 Position", $"`{position}`", true);
            embed.AddField("🏷️ Type", itemType, true);
            embed.AddField("🚁 Quick Teleport", $"`teleportpos {bed.ServerPosition.x:F1} {bed.ServerPosition.y:F1} {bed.ServerPosition.z:F1}`", false);
            embed.AddField("─────────────────────", "​", false);

            var discordMessage = new DiscordMessage().AddEmbed(embed);
            DiscordSendEmbedMessage(discordMessage, _configData.GlobalSettings.PrivateAdminWebhook);
        }

        private void SendBedRenameBlacklistEmbed(BasePlayer player, SleepingBag bed, string newName, string oldName, string itemType, string position, string ownerName, string deployerName, string detectedTerms)
        {
            string steamProfileUrl = $"https://steamcommunity.com/profiles/{player.userID}";
            string ownerProfileUrl = $"https://steamcommunity.com/profiles/{bed.OwnerID}";
            bool wasBlocked = _configData.BedRenameSettings.Blacklist.BlockRename;

            var embed = new DiscordEmbed()
                .SetColor(0xFF0000) // Red for blacklist
                .SetTitle($"🚫 {itemType} Rename {(wasBlocked ? "Blocked" : "Flagged")} (Blacklist)")
                .SetTimestamp(DateTimeOffset.Now);

            string playerDetails = $"**Name:** {ReplaceChars(player.displayName)}\n**Steam ID:** [{player.UserIDString}]({steamProfileUrl})";
            embed.AddField("👤 Player", playerDetails, false);

            if (bed.OwnerID != player.userID)
            {
                embed.AddField("👑 Owner", $"[{ReplaceChars(ownerName)}]({ownerProfileUrl}) (`{bed.OwnerID}`)", false);
            }

            embed.AddField("📝 Attempted Name", $"`{newName}`", false);
            embed.AddField("⚠️ Detected Terms", $"`{detectedTerms}`", false);
            embed.AddField("🛡️ Action", wasBlocked ? "Rename was **blocked**" : "Rename was **allowed** (logging only)", false);
            embed.AddField("📍 Position", $"`{position}`", true);
            embed.AddField("🏷️ Type", itemType, true);
            embed.AddField("🚁 Quick Teleport", $"`teleportpos {bed.ServerPosition.x:F1} {bed.ServerPosition.y:F1} {bed.ServerPosition.z:F1}`", false);
            embed.AddField("─────────────────────", "​", false);

            var discordMessage = new DiscordMessage().AddEmbed(embed);
            DiscordSendEmbedMessage(discordMessage, _configData.GlobalSettings.PrivateAdminWebhook);
        }

        private void SaveBedRenameEntry(BasePlayer player, SleepingBag bed, string newName, string oldName, string itemType, string position, string ownerName, string deployerName, bool wasBlacklisted, string detectedTerms)
        {
            var entry = new BedRenameLogEntry
            {
                PlayerSteamID = player.UserIDString,
                PlayerName = player.displayName,
                OwnerSteamID = bed.OwnerID.ToString(),
                OwnerName = ownerName,
                DeployerSteamID = bed.deployerUserID.ToString(),
                DeployerName = deployerName,
                ItemType = itemType,
                OldName = oldName,
                NewName = newName,
                Position = position,
                TimestampUTC = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                TimestampLocal = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                ServerName = ConVar.Server.hostname,
                WasBlacklisted = wasBlacklisted,
                BlacklistTermsDetected = detectedTerms
            };

            _bedRenameLogData.BedRenames.Add(entry);
            SaveBedRenameLogData();
        }

        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (!_configData.C4LogSettings.Enabled || player == null || entity == null)
                return;

            if (!(entity is TimedExplosive))
                return;

            // Only log C4
            if (entity.ShortPrefabName != "explosive.timed.deployed")
                return;

            bool isNpc = player.IsNpc || !player.userID.IsSteamId();
            if (isNpc && _configData.C4LogSettings.HideNPC)
                return;

            string playerName = ReplaceChars(player.displayName);
            Vector3 pos = entity.transform.position;
            string position = $"X: {pos.x:F1} Y: {pos.y:F1} Z: {pos.z:F1}";

            LogToConsole($"[C4] {player.displayName} ({player.UserIDString}) placed C4 at {position}{(isNpc ? " (NPC)" : "")}");

            if (_configData.C4LogSettings.SendDiscordEmbed)
            {
                SendExplosiveEmbed(player, "C4", position, pos, isNpc, 0xE74C3C); // Red
            }

            if (_configData.C4LogSettings.LogToFile)
            {
                SaveExplosiveLogEntry(_c4LogData, "C4Log", player, "C4", position, isNpc);
            }
        }

        private void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            if (!_configData.RocketLogSettings.Enabled || player == null || entity == null)
                return;

            bool isNpc = player.IsNpc || !player.userID.IsSteamId();
            if (isNpc && _configData.RocketLogSettings.HideNPC)
                return;

            string rocketType = GetRocketType(entity);
            string playerName = ReplaceChars(player.displayName);
            Vector3 pos = player.transform.position;
            string position = $"X: {pos.x:F1} Y: {pos.y:F1} Z: {pos.z:F1}";

            LogToConsole($"[Rocket] {player.displayName} ({player.UserIDString}) launched {rocketType} at {position}{(isNpc ? " (NPC)" : "")}");

            if (_configData.RocketLogSettings.SendDiscordEmbed)
            {
                SendExplosiveEmbed(player, rocketType, position, pos, isNpc, 0xFF6600); // Orange
            }

            if (_configData.RocketLogSettings.LogToFile)
            {
                SaveExplosiveLogEntry(_rocketLogData, "RocketLog", player, rocketType, position, isNpc);
            }
        }

        private string GetRocketType(BaseEntity entity)
        {
            if (entity == null) return "Rocket";

            string prefab = entity.ShortPrefabName;
            if (prefab.Contains("hv")) return "HV Rocket";
            if (prefab.Contains("incendiary")) return "Incendiary Rocket";
            if (prefab.Contains("mlrs")) return "MLRS Rocket";
            return "Rocket";
        }

        private void SendExplosiveEmbed(BasePlayer player, string explosiveType, string position, Vector3 pos, bool isNpc, int color)
        {
            string steamProfileUrl = $"https://steamcommunity.com/profiles/{player.userID}";

            var embed = new DiscordEmbed()
                .SetColor(color)
                .SetTitle($"💥 {explosiveType} {(isNpc ? "(NPC)" : "Detected")}")
                .SetTimestamp(DateTimeOffset.Now);

            string playerDetails = $"**Name:** {ReplaceChars(player.displayName)}\n**Steam ID:** [{player.UserIDString}]({steamProfileUrl})";
            if (isNpc)
            {
                playerDetails += "\n**Type:** NPC";
            }
            embed.AddField("👤 Player", playerDetails, false);

            embed.AddField("💣 Explosive", explosiveType, true);
            embed.AddField("📍 Position", $"`{position}`", true);
            embed.AddField("🚁 Quick Teleport", $"`teleportpos {pos.x:F1} {pos.y:F1} {pos.z:F1}`", false);
            embed.AddField("─────────────────────", "​", false);

            var discordMessage = new DiscordMessage().AddEmbed(embed);
            DiscordSendEmbedMessage(discordMessage, _configData.GlobalSettings.PrivateAdminWebhook);
        }

        private void SaveExplosiveLogEntry(ExplosiveLogData logData, string logName, BasePlayer player, string explosiveType, string position, bool isNpc)
        {
            var entry = new ExplosiveLogEntry
            {
                PlayerSteamID = player.UserIDString,
                PlayerName = player.displayName,
                IsNPC = isNpc,
                ExplosiveType = explosiveType,
                Position = position,
                TimestampUTC = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                TimestampLocal = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                ServerName = ConVar.Server.hostname
            };

            logData.Events.Add(entry);

            try
            {
                Interface.Oxide.DataFileSystem.WriteObject(GetMonthlyLogFileName(logName), logData);
            }
            catch (Exception ex)
            {
                Puts($"Error saving {logName}: {ex.Message}");
            }
        }

        private void OnServerMessage(string message, string name, string color, ulong id)
        {
            if (_configData.ServerMessagesSettings.Enabled)
            {
                LogToConsole($"ServerMessage: {message}");
                
                // Filter messages containing "gave" from SERVER to PrivateAdminWebhook
                if (message.Contains("gave") && name == "SERVER")
                {
                    if (_configData.GlobalSettings.UseEmbedForServerMessages)
                    {
                        SendEnhancedGaveMessage(message);
                    }
                    else
                    {
                        // Fallback to simple message
                        DiscordSendMessage(Lang(LangKeys.Event.ServerMessage, null, message), _configData.GlobalSettings.PrivateAdminWebhook);
                    }
                }
                else
                {
                    DiscordSendMessage(Lang(LangKeys.Event.ServerMessage, null, message), _configData.GlobalSettings.ServerMessagesWebhook);
                }
            }
        }

        private void SendEnhancedGaveMessage(string message)
        {
            // Parse the gave message to extract details
            var gaveInfo = ParseGaveMessage(message);
            
            var embed = new DiscordEmbed()
                .SetColor(0x32CD32) // Lime green for item giving
                .SetTitle("🎁 Admin Item Grant")
                .SetDescription("**Server administrator granted items**")
                .SetTimestamp(DateTimeOffset.Now);

            // Add parsed information
            if (!string.IsNullOrEmpty(gaveInfo.AdminName))
            {
                embed.AddField("👤 Administrator", gaveInfo.AdminName, true);
            }
            
            if (!string.IsNullOrEmpty(gaveInfo.TargetName))
            {
                embed.AddField("🎯 Recipient", gaveInfo.TargetName, true);
            }
            
            if (!string.IsNullOrEmpty(gaveInfo.ItemDetails))
            {
                embed.AddField("📦 Items Granted", gaveInfo.ItemDetails, false);
            }
            
            // Add the full raw message as a fallback
            embed.AddField("📝 Full Message", $"`{message}`", false);
            
            // Add separator
            embed.AddField("─────────────────────", "​", false);
            
            var discordMessage = new DiscordMessage().AddEmbed(embed);
            DiscordSendEmbedMessage(discordMessage, _configData.GlobalSettings.PrivateAdminWebhook);
        }

        private GaveMessageInfo ParseGaveMessage(string message)
        {
            var info = new GaveMessageInfo();
            
            try
            {
                // Common patterns for gave messages:
                // "AdminName gave PlayerName 1 x Item Name"
                // "AdminName gave themselves 1000 x Item Name"
                
                if (message.Contains(" gave "))
                {
                    var parts = message.Split(" gave ");
                    if (parts.Length >= 2)
                    {
                        info.AdminName = parts[0].Trim();
                        
                        var rightPart = parts[1];
                        
                        // Check for "themselves" pattern
                        if (rightPart.StartsWith("themselves "))
                        {
                            info.TargetName = "themselves";
                            info.ItemDetails = rightPart.Substring("themselves ".Length).Trim();
                        }
                        else
                        {
                            // Try to find where the item details start (usually after player name)
                            // Look for patterns like "PlayerName 1 x" or "PlayerName 1000 x"
                            var itemMatch = System.Text.RegularExpressions.Regex.Match(rightPart, @"^(.+?)\s+(\d+\s+x\s+.+)$");
                            if (itemMatch.Success)
                            {
                                info.TargetName = itemMatch.Groups[1].Value.Trim();
                                info.ItemDetails = itemMatch.Groups[2].Value.Trim();
                            }
                            else
                            {
                                // Fallback - everything after "gave" if we can't parse it
                                info.ItemDetails = rightPart.Trim();
                            }
                        }
                    }
                }
                
                // Clean up any special characters
                info.AdminName = ReplaceChars(info.AdminName ?? "");
                info.TargetName = ReplaceChars(info.TargetName ?? "");
                info.ItemDetails = ReplaceChars(info.ItemDetails ?? "");
            }
            catch (Exception ex)
            {
                PrintWarning($"Error parsing gave message '{message}': {ex.Message}");
                // Set fallback values
                info.ItemDetails = message;
            }
            
            return info;
        }

        private class GaveMessageInfo
        {
            public string AdminName { get; set; } = "";
            public string TargetName { get; set; } = "";
            public string ItemDetails { get; set; } = "";
        }

        // Premium Plugin Event Hooks - Using exact same methods as Rustcord.cs
        private void OnAirEventStart(HashSet<BaseEntity> entities)
        {
            if (_configData.AirEventSettings.Enabled)
            {
                LogToConsole("Air Event has started");
                DiscordSendMessage(Lang(LangKeys.Event.AirEvent), _configData.GlobalSettings.ServerMessagesWebhook);
            }
        }

        private void OnAirEventEnd(HashSet<BaseEntity> entities)
        {
            if (_configData.AirEventSettings.Enabled)
            {
                LogToConsole("Air Event has ended");
                DiscordSendMessage(Lang(LangKeys.Event.AirEvent).Replace("started", "ended"), _configData.GlobalSettings.ServerMessagesWebhook);
            }
        }

        // Added correct Airfield Event hooks to match AirfieldEvent.cs
        private void AirfieldEventStarted()
        {
            if (_configData.AirfieldEventSettings.Enabled)
            {
                LogToConsole("Airfield Event has started");
                DiscordSendMessage(Lang(LangKeys.Event.AirfieldEvent), _configData.GlobalSettings.ServerMessagesWebhook);
            }
        }

        private void AirfieldEventEnded()
        {
            if (_configData.AirfieldEventSettings.Enabled)
            {
                LogToConsole("Airfield Event has ended");
                DiscordSendMessage(Lang(LangKeys.Event.AirfieldEvent).Replace("started", "ended"), _configData.GlobalSettings.ServerMessagesWebhook);
            }
        }

        private void OnArmoredTrainEventStart()
        {
            if (_configData.ArmoredTrainEventSettings.Enabled)
            {
                LogToConsole("Armored Train Event has started");
                DiscordSendMessage(Lang(LangKeys.Event.ArmoredTrainEvent), _configData.GlobalSettings.ServerMessagesWebhook);
            }
        }

        private void OnArmoredTrainEventStop()
        {
            if (_configData.ArmoredTrainEventSettings.Enabled)
            {
                LogToConsole("Armored Train Event has ended");
                DiscordSendMessage(Lang(LangKeys.Event.ArmoredTrainEvent).Replace("started", "ended"), _configData.GlobalSettings.ServerMessagesWebhook);
            }
        }

        private void OnSputnikEventStart()
        {
            if (_configData.SputnikEventSettings.Enabled)
            {
                LogToConsole("Sputnik Event has started");
                DiscordSendMessage(Lang(LangKeys.Event.SputnikEvent), _configData.GlobalSettings.ServerMessagesWebhook);
            }
        }

        private void OnSputnikEventStop()
        {
            if (_configData.SputnikEventSettings.Enabled)
            {
                LogToConsole("Sputnik Event has ended");
                DiscordSendMessage(Lang(LangKeys.Event.SputnikEvent).Replace("started", "ended"), _configData.GlobalSettings.ServerMessagesWebhook);
            }
        }

        private void OnSupermarketEventStart(Vector3 position, float radius)
        {
            if (_configData.SupermarketEventSettings.Enabled)
            {
                LogToConsole("Supermarket Event has started at " + GetGridPosition(position));
                DiscordSendMessage(Lang(LangKeys.Event.SupermarketEvent), _configData.GlobalSettings.ServerMessagesWebhook);
            }
        }

        private void OnSupermarketEventEnd()
        {
            if (_configData.SupermarketEventSettings.Enabled)
            {
                LogToConsole("Supermarket Event has ended");
                DiscordSendMessage(Lang(LangKeys.Event.SupermarketEvent).Replace("started", "ended"), _configData.GlobalSettings.ServerMessagesWebhook);
            }
        }

        private void OnArcticBaseEventStart(Vector3 position, float radius)
        {
            if (_configData.ArcticBaseEventSettings.Enabled)
            {
                LogToConsole("Arctic Base Event has started at " + GetGridPosition(position));
                DiscordSendMessage(Lang(LangKeys.Event.ArcticBaseEvent), _configData.GlobalSettings.ServerMessagesWebhook);
            }
        }

        private void OnArcticBaseEventEnd()
        {
            if (_configData.ArcticBaseEventSettings.Enabled)
            {
                LogToConsole("Arctic Base Event has ended");
                DiscordSendMessage(Lang(LangKeys.Event.ArcticBaseEvent).Replace("started", "ended"), _configData.GlobalSettings.ServerMessagesWebhook);
            }
        }

        private void OnGasStationEventStart(Vector3 position, float radius)
        {
            if (_configData.GasStationEventSettings.Enabled)
            {
                LogToConsole("Gas Station Event has started at " + GetGridPosition(position));
                DiscordSendMessage(Lang(LangKeys.Event.GasStationEvent), _configData.GlobalSettings.ServerMessagesWebhook);
            }
        }

        private void OnGasStationEventEnd()
        {
            if (_configData.GasStationEventSettings.Enabled)
            {
                LogToConsole("Gas Station Event has ended");
                DiscordSendMessage(Lang(LangKeys.Event.GasStationEvent).Replace("started", "ended"), _configData.GlobalSettings.ServerMessagesWebhook);
            }
        }

        #region Permissions

        private void OnGroupCreated(string name)
        {
            LogToConsole($"Group {name} has been created");

            DiscordSendMessage(Lang(LangKeys.Permission.GroupCreated, null, name), _configData.GlobalSettings.PrivateAdminWebhook);
        }

        private void OnGroupDeleted(string name)
        {
            LogToConsole($"Group {name} has been deleted");

            DiscordSendMessage(Lang(LangKeys.Permission.GroupDeleted, null, name), _configData.GlobalSettings.PrivateAdminWebhook);
        }

        private void OnTimedPermissionGranted(string playerID, string permission, TimeSpan duration)
        {
            IPlayer player = covalence.Players.FindPlayerById(playerID);

            if (player == null)
            {
                return;
            }

            LogToConsole($"{playerID} {player.Name} is granted {permission} for {duration}");

            DiscordSendMessage(Lang(LangKeys.Plugin.TimedPermissionGranted, null, playerID, ReplaceChars(player.Name), permission, GetFormattedDurationTime(duration)), _configData.GlobalSettings.PrivateAdminWebhook);
        }

        private void OnTimedPermissionExtended(string playerID, string permission, TimeSpan duration)
        {
            IPlayer player = covalence.Players.FindPlayerById(playerID);

            if (player == null)
            {
                return;
            }

            LogToConsole($"{playerID} {player.Name} timed permission {permission} is extended for {duration}");

            DiscordSendMessage(Lang(LangKeys.Plugin.TimedPermissionExtended, null, playerID, ReplaceChars(player.Name), permission, GetFormattedDurationTime(duration)), _configData.GlobalSettings.PrivateAdminWebhook);
        }

        private void OnTimedGroupAdded(string playerID, string group, TimeSpan duration)
        {
            IPlayer player = covalence.Players.FindPlayerById(playerID);

            if (player == null)
            {
                return;
            }

            LogToConsole($"{playerID} {player.Name} is added to {group} for {duration}");

            DiscordSendMessage(Lang(LangKeys.Plugin.TimedGroupAdded, null, playerID, ReplaceChars(player.Name), group, GetFormattedDurationTime(duration)), _configData.GlobalSettings.PrivateAdminWebhook);
        }

        private void OnTimedGroupExtended(string playerID, string group, TimeSpan duration)
        {
            IPlayer player = covalence.Players.FindPlayerById(playerID);

            if (player == null)
            {
                return;
            }

            LogToConsole($"{playerID} {player.Name} timed group {group} is extended for {duration}");

            DiscordSendMessage(Lang(LangKeys.Plugin.TimedGroupExtended, null, playerID, ReplaceChars(player.Name), group, GetFormattedDurationTime(duration)), _configData.GlobalSettings.PrivateAdminWebhook);
        }

        private void OnUserGroupAdded(string playerID, string groupName)
        {
            IPlayer player = covalence.Players.FindPlayerById(playerID);

            if (player == null)
            {
                return;
            }

            LogToConsole($"{playerID} {player.Name} is added to group {groupName}");

            DiscordSendMessage(Lang(LangKeys.Permission.UserGroupAdded, null, playerID, ReplaceChars(player.Name), groupName), _configData.GlobalSettings.PrivateAdminWebhook);
        }

        private void OnUserGroupRemoved(string playerID, string groupName)
        {
            IPlayer player = covalence.Players.FindPlayerById(playerID);

            if (player == null)
            {
                return;
            }

            LogToConsole($"{playerID} {player.Name} is removed from group {groupName}");

            DiscordSendMessage(Lang(LangKeys.Permission.UserGroupRemoved, null, playerID, ReplaceChars(player.Name), groupName), _configData.GlobalSettings.PrivateAdminWebhook);
        }

        private void OnUserPermissionGranted(string playerID, string permName)
        {
            IPlayer player = covalence.Players.FindPlayerById(playerID);

            if (player == null)
            {
                return;
            }

            LogToConsole($"{playerID} {player.Name} is granted permission {permName}");

            DiscordSendMessage(Lang(LangKeys.Permission.UserPermissionGranted, null, playerID, ReplaceChars(player.Name), permName), _configData.GlobalSettings.PrivateAdminWebhook);
        }

        private void OnUserPermissionRevoked(string playerID, string permName)
        {
            IPlayer player = covalence.Players.FindPlayerById(playerID);

            if (player == null)
            {
                return;
            }

            LogToConsole($"{playerID} {player.Name} is revoked permission {permName}");

            DiscordSendMessage(Lang(LangKeys.Permission.UserPermissionRevoked, null, playerID, ReplaceChars(player.Name), permName), _configData.GlobalSettings.PrivateAdminWebhook);
        }

        #endregion

        #endregion Events Hooks

        #region Core Methods

        public string ReplaceChars(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            _sb.Clear();
            _sb.Append(text);
            _sb.Replace("*", "＊");
            _sb.Replace("`", "'");
            _sb.Replace("_", "＿");
            _sb.Replace("~", "～");
            _sb.Replace(">", "＞");
            _sb.Replace("@here", "here");
            _sb.Replace("@everyone", "everyone");

            return _sb.ToString();
        }

        public void HandleQueue()
        {
            // Prevent recursive processing
            if (_isProcessingQueue)
            {
                return;
            }

            _isProcessingQueue = true;

            try
            {
                // Check if we've exceeded max retries
                if (_retryCount > _maxRetries)
                {
                    PrintWarning($"HandleQueue: Maximum retry attempts ({_maxRetries}) exceeded. Clearing queue and resetting retry count.");
                    _queue.Clear();
                    _retryCount = 0;
                    _queuedMessage = null;
                    QueueCooldownDisable();
                    _isProcessingQueue = false;
                    return;
                }

                if (_retryCount > 0)
                {
                    if (_timerQueueCooldown == null && _queuedMessage != null)
                    {
                        float timeout = _configData.GlobalSettings.QueueCooldown * Math.Min(_retryCount, 10);
                        PrintWarning($"HandleQueue: connection problem detected! Retry # {_retryCount}. Next try in {timeout} seconds. Messages in queue: {_queue.Count}");

                        _timerQueueCooldown = timer.Once(timeout, () =>
                        {
                            try
                            {
                                if (_queuedMessage != null)
                                {
                                    if (_queuedMessage.IsEmbed && _queuedMessage.DiscordMessage != null)
                                    {
                                        webrequest.Enqueue(_queuedMessage.WebhookUrl, _queuedMessage.DiscordMessage.ToJson(), DiscordSendMessageCallback, this, RequestMethod.POST, _headers);
                                    }
                                    else if (!_queuedMessage.IsEmbed && !string.IsNullOrEmpty(_queuedMessage.Message))
                                    {
                                        var discordMsg = new DiscordMessage(_queuedMessage.Message);
                                        webrequest.Enqueue(_queuedMessage.WebhookUrl, discordMsg.ToJson(), DiscordSendMessageCallback, this, RequestMethod.POST, _headers);
                                    }
                                }

                                QueueCooldownDisable();
                                
                                // Schedule next queue processing instead of recursion
                                timer.Once(0.1f, () => {
                                    _isProcessingQueue = false;
                                    HandleQueue();
                                });
                            }
                            catch (Exception ex)
                            {
                                PrintError($"Error in retry timer callback: {ex.Message}");
                                _isProcessingQueue = false;
                                _retryCount = 0;
                                _queuedMessage = null;
                                QueueCooldownDisable();
                            }
                        });
                    }
                    else if (_queuedMessage == null)
                    {
                        // No message to retry, reset and continue
                        _retryCount = 0;
                        _isProcessingQueue = false;
                        timer.Once(0.1f, HandleQueue);
                        return;
                    }

                    return;
                }

                if (_timerQueueCooldown == null && _timerQueue == null && _queue.Count > 0)
                {
                    try
                    {
                        _queuedMessage = _queue.Dequeue();

                        if (_queuedMessage == null)
                        {
                            _isProcessingQueue = false;
                            timer.Once(0.1f, HandleQueue);
                            return;
                        }

                        // Validate webhook URL
                        if (string.IsNullOrEmpty(_queuedMessage.WebhookUrl))
                        {
                            PrintError("DiscordSendMessage: webhookUrl is null or empty!");
                            _isProcessingQueue = false;
                            timer.Once(0.1f, HandleQueue);
                            return;
                        }

                        if (_queuedMessage.IsEmbed)
                        {
                            // For embeds, send directly
                            if (_queuedMessage.DiscordMessage != null)
                            {
                                webrequest.Enqueue(_queuedMessage.WebhookUrl, _queuedMessage.DiscordMessage.ToJson(), DiscordSendMessageCallback, this, RequestMethod.POST, _headers);
                            }
                        }
                        else
                        {
                            // Handle text messages - batch them if possible
                            _sb.Clear();

                            if (!string.IsNullOrEmpty(_queuedMessage.Message))
                            {
                                if (_queuedMessage.Message.Length > 1990)
                                {
                                    _queuedMessage.Message = $"{_queuedMessage.Message[..1990]}```";
                                }
                                _sb.AppendLine(_queuedMessage.Message);
                            }

                            // Try to batch multiple messages
                            while (_queue.Count > 0)
                            {
                                _nextMessage = _queue.Peek();

                                if (_nextMessage == null 
                                    || _nextMessage.IsEmbed
                                    || string.IsNullOrEmpty(_nextMessage.Message)
                                    || _sb.Length + _nextMessage.Message.Length > 1990
                                    || _queuedMessage.WebhookUrl != _nextMessage.WebhookUrl)
                                {
                                    break;
                                }

                                _nextMessage = _queue.Dequeue();
                                _sb.AppendLine(_nextMessage.Message);
                            }

                            string finalMessage = _sb.ToString().Trim();
                            if (!string.IsNullOrEmpty(finalMessage))
                            {
                                var discordMessage = new DiscordMessage(finalMessage);
                                webrequest.Enqueue(_queuedMessage.WebhookUrl, discordMessage.ToJson(), DiscordSendMessageCallback, this, RequestMethod.POST, _headers);
                            }
                        }

                        _timerQueue = timer.Once(_configData.GlobalSettings.QueueInterval, () => {
                            try
                            {
                                _timerQueue?.Destroy();
                                _timerQueue = null;
                                _isProcessingQueue = false;
                                HandleQueue();
                            }
                            catch (Exception ex)
                            {
                                PrintError($"Error in queue timer callback: {ex.Message}");
                                _isProcessingQueue = false;
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        PrintError($"Error processing queue message: {ex.Message}");
                        _queuedMessage = null;
                        _isProcessingQueue = false;
                        timer.Once(0.1f, HandleQueue);
                    }
                }
                else
                {
                    // Nothing to process
                    _isProcessingQueue = false;
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error in HandleQueue: {ex.Message}\nStack trace: {ex.StackTrace}");
                _retryCount = 0;
                _queuedMessage = null;
                _isProcessingQueue = false;
            }
        }

        public void QueueCooldownDisable()
        {
            _timerQueueCooldown?.Destroy();
            _timerQueueCooldown = null;
        }

        public void HandleEntity(BaseEntity baseEntity)
        {
            if (!baseEntity.IsValid())
            {
                return;
            }

            string langKey = null;
            bool enabled = false;

            if (baseEntity is HalloweenHunt)
            {
                langKey = LangKeys.Event.Halloween;
                enabled = _configData.HalloweenSettings.Enabled;
                LogToConsole("Halloween Hunt Event has started");
            }
            else if (baseEntity is EggHuntEvent)
            {
                langKey = LangKeys.Event.Easter;
                enabled = _configData.EasterSettings.Enabled;
                LogToConsole("Easter event has started");
            }
            else if (baseEntity is SantaSleigh)
            {
                langKey = LangKeys.Event.SantaSleigh;
                enabled = _configData.SantaSleighSettings.Enabled;
                LogToConsole("SantaSleigh Event has started");
            }
            else if (baseEntity is XMasRefill)
            {
                langKey = LangKeys.Event.Christmas;
                enabled = _configData.ChristmasSettings.Enabled;
                LogToConsole("Christmas event has started");
            }

            if (enabled && !string.IsNullOrEmpty(langKey))
            {
                DiscordSendMessage(Lang(langKey), _configData.GlobalSettings.ServerMessagesWebhook);
            }
        }

        public void HandleRaidableBase(Vector3 raidPos, int difficulty, string langKey, BasePlayer owner = null, List<BasePlayer> raiders = null)
        {
            if (raidPos == null)
            {
                PrintError($"{langKey}: raidPos == null");
                return;
            }

            string difficultyString;
            switch (difficulty)
            {
                case 0:
                    difficultyString = LangKeys.Format.Easy;
                    break;
                case 1:
                    difficultyString = LangKeys.Format.Medium;
                    break;
                case 2:
                    difficultyString = LangKeys.Format.Hard;
                    break;
                case 3:
                    difficultyString = LangKeys.Format.Expert;
                    break;
                case 4:
                    difficultyString = LangKeys.Format.Nightmare;
                    break;
                case 512:
                    difficultyString = string.Empty;
                    break;
                default:
                    PrintError($"{langKey}: Unknown difficulty: {difficulty}");
                    return;
            }

            switch (langKey)
            {
                case LangKeys.Plugin.RaidableBaseCompleted:
                    _sb.Clear();
                    for (int i = 0; i < raiders?.Count; i++)
                    {
                        if (i > 0)
                        {
                            _sb.Append(", ");
                        }
                        _sb.Append(ReplaceChars(raiders[i].displayName));
                    }
                    LogToConsole($"{difficultyString} Raidable Base owned by {owner?.displayName} at {GetGridPosition(raidPos)} has been raided by {_sb.ToString()}");
                    DiscordSendMessage(Lang(langKey, null, GetGridPosition(raidPos), Lang(difficultyString), ReplaceChars(owner?.displayName), _sb.ToString()), _configData.GlobalSettings.ServerMessagesWebhook);
                    break;
                case LangKeys.Plugin.RaidableBaseEnded:
                case LangKeys.Plugin.RaidableBaseStarted:
                    LogToConsole(difficultyString + " Raidable Base at " + GetGridPosition(raidPos) + " has " + (langKey == LangKeys.Plugin.RaidableBaseStarted ? "spawned" : "ended"));
                    DiscordSendMessage(Lang(langKey, null, GetGridPosition(raidPos), Lang(difficultyString)), _configData.GlobalSettings.ServerMessagesWebhook);
                    break;
            }
        }

        private void DiscordSendMessage(string message, string webhookUrl, bool stripTags = false)
        {
            if (string.IsNullOrWhiteSpace(webhookUrl))
            {
                PrintError("DiscordSendMessage: webhookUrl is null or empty!");
                return;
            }

            if (stripTags)
            {
                message = StripRustTags(message);
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                PrintError("DiscordSendMessage: message is null or empty!");
                return;
            }

            _queue.Enqueue(new QueuedMessage
            {
                Message = message,
                WebhookUrl = webhookUrl,
                IsEmbed = false
            });

            HandleQueue();
        }

        private void DiscordSendEmbedMessage(DiscordMessage message, string webhookUrl)
        {
            if (string.IsNullOrWhiteSpace(webhookUrl))
            {
                PrintError("DiscordSendEmbedMessage: webhookUrl is null or empty!");
                return;
            }

            if (message == null)
            {
                PrintError("DiscordSendEmbedMessage: message is null!");
                return;
            }

            _queue.Enqueue(new QueuedMessage
            {
                DiscordMessage = message,
                WebhookUrl = webhookUrl,
                IsEmbed = true
            });

            HandleQueue();
        }

        #endregion Core Methods

        #region Helpers

        public void UnsubscribeHooks()
        {
            Unsubscribe(nameof(OnAirEventEnd));
            Unsubscribe(nameof(OnAirEventStart));
            Unsubscribe(nameof(AirfieldEventStarted));
            Unsubscribe(nameof(AirfieldEventEnded));
            Unsubscribe(nameof(OnArcticBaseEventEnd));
            Unsubscribe(nameof(OnArcticBaseEventStart));
            Unsubscribe(nameof(OnArmoredTrainEventStart));
            Unsubscribe(nameof(OnArmoredTrainEventStop));
            Unsubscribe(nameof(OnBetterChatMuted));
            Unsubscribe(nameof(OnBetterChatMuteExpired));
            Unsubscribe(nameof(OnBetterChatTimeMuted));
            Unsubscribe(nameof(OnBetterChatUnmuted));
            Unsubscribe(nameof(OnEntityDeath));
            Unsubscribe(nameof(OnEntityKill));
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnGasStationEventEnd));
            Unsubscribe(nameof(OnGasStationEventStart));
            Unsubscribe(nameof(OnGroupCreated));
            Unsubscribe(nameof(OnGroupDeleted));
            Unsubscribe(nameof(OnPlayerChat));
            Unsubscribe(nameof(OnPlayerConnected));
            Unsubscribe(nameof(OnPlayerDisconnected));
            Unsubscribe(nameof(OnRaidableBaseCompleted));
            Unsubscribe(nameof(OnRaidableBaseEnded));
            Unsubscribe(nameof(OnRaidableBaseStarted));
            Unsubscribe(nameof(OnRconCommand));
            Unsubscribe(nameof(OnRconConnection));
            Unsubscribe(nameof(OnServerMessage));
            Unsubscribe(nameof(OnSputnikEventStart));
            Unsubscribe(nameof(OnSputnikEventStop));
            Unsubscribe(nameof(OnSupermarketEventEnd));
            Unsubscribe(nameof(OnSupermarketEventStart));
            Unsubscribe(nameof(OnTimedGroupAdded));
            Unsubscribe(nameof(OnTimedGroupExtended));
            Unsubscribe(nameof(OnTimedPermissionExtended));
            Unsubscribe(nameof(OnTimedPermissionGranted));
            Unsubscribe(nameof(OnUserBanned));
            Unsubscribe(nameof(OnUserGroupAdded));
            Unsubscribe(nameof(OnUserGroupRemoved));
            Unsubscribe(nameof(OnUserKicked));
            Unsubscribe(nameof(OnUserNameUpdated));
            Unsubscribe(nameof(OnUserPermissionGranted));
            Unsubscribe(nameof(OnUserPermissionRevoked));
            Unsubscribe(nameof(OnUserUnbanned));
            Unsubscribe(nameof(CanRenameBed));
            Unsubscribe(nameof(OnExplosiveThrown));
            Unsubscribe(nameof(OnRocketLaunched));
        }

        public void SubscribeHooks()
        {
            if (_configData.AirEventSettings.Enabled)
            {
                Subscribe(nameof(OnAirEventEnd));
                Subscribe(nameof(OnAirEventStart));
            }

            if (_configData.AirfieldEventSettings.Enabled)
            {
                Subscribe(nameof(AirfieldEventStarted));
                Subscribe(nameof(AirfieldEventEnded));
            }

            if (_configData.ArcticBaseEventSettings.Enabled)
            {
                Subscribe(nameof(OnArcticBaseEventEnd));
                Subscribe(nameof(OnArcticBaseEventStart));
            }

            if (_configData.ArmoredTrainEventSettings.Enabled)
            {
                Subscribe(nameof(OnArmoredTrainEventStart));
                Subscribe(nameof(OnArmoredTrainEventStop));
            }

            if (_configData.UserMutedSettings.Enabled)
            {
                Subscribe(nameof(OnBetterChatMuted));
                Subscribe(nameof(OnBetterChatMuteExpired));
                Subscribe(nameof(OnBetterChatTimeMuted));
                Subscribe(nameof(OnBetterChatUnmuted));
            }

            if (_configData.ChristmasSettings.Enabled)
            {
                Subscribe(nameof(OnEntitySpawned));
            }

            if (_configData.EasterSettings.Enabled
             || _configData.HalloweenSettings.Enabled)
            {
                Subscribe(nameof(OnEntityKill));
                Subscribe(nameof(OnEntitySpawned));
            }

            if (_configData.GasStationEventSettings.Enabled)
            {
                Subscribe(nameof(OnGasStationEventEnd));
                Subscribe(nameof(OnGasStationEventStart));
            }

            if (_configData.PlayerDeathSettings.Enabled)
            {
                Subscribe(nameof(OnEntityDeath));
            }

            if (_configData.PermissionsSettings.Enabled)
            {
                Subscribe(nameof(OnGroupCreated));
                Subscribe(nameof(OnGroupDeleted));
                Subscribe(nameof(OnTimedGroupAdded));
                Subscribe(nameof(OnTimedGroupExtended));
                Subscribe(nameof(OnTimedPermissionExtended));
                Subscribe(nameof(OnTimedPermissionGranted));
                Subscribe(nameof(OnUserGroupAdded));
                Subscribe(nameof(OnUserGroupRemoved));
                Subscribe(nameof(OnUserPermissionGranted));
                Subscribe(nameof(OnUserPermissionRevoked));
            }

            if (_configData.PlayerConnectedInfoSettings.Enabled)
            {
                Subscribe(nameof(OnPlayerConnected));
            }

            if (_configData.ChatSettings.Enabled
             || _configData.ChatTeamSettings.Enabled)
            {
                Subscribe(nameof(OnPlayerChat));
            }

            if (_configData.PlayerDisconnectedSettings.Enabled)
            {
                Subscribe(nameof(OnPlayerDisconnected));
            }

            if (_configData.RaidableBasesSettings.Enabled)
            {
                Subscribe(nameof(OnRaidableBaseCompleted));
                Subscribe(nameof(OnRaidableBaseEnded));
                Subscribe(nameof(OnRaidableBaseStarted));
            }

            if (_configData.RconCommandSettings.Enabled)
            {
                Subscribe(nameof(OnRconCommand));
            }

            if (_configData.RconConnectionSettings.Enabled)
            {
                Subscribe(nameof(OnRconConnection));
            }

            if (_configData.SantaSleighSettings.Enabled)
            {
                Subscribe(nameof(OnEntitySpawned));
            }

            if (_configData.ServerMessagesSettings.Enabled)
            {
                Subscribe(nameof(OnServerMessage));
            }

            if (_configData.SputnikEventSettings.Enabled)
            {
                Subscribe(nameof(OnSputnikEventStart));
                Subscribe(nameof(OnSputnikEventStop));
            }

            if (_configData.SupermarketEventSettings.Enabled)
            {
                Subscribe(nameof(OnSupermarketEventEnd));
                Subscribe(nameof(OnSupermarketEventStart));
            }

            if (_configData.UserBannedSettings.Enabled)
            {
                Subscribe(nameof(OnUserBanned));
                Subscribe(nameof(OnUserUnbanned));
            }

            if (_configData.UserKickedSettings.Enabled)
            {
                Subscribe(nameof(OnUserKicked));
            }

            if (_configData.UserNameUpdateSettings.Enabled)
            {
                Subscribe(nameof(OnUserNameUpdated));
            }

            if (_configData.BedRenameSettings.Enabled)
            {
                Subscribe(nameof(CanRenameBed));
            }

            if (_configData.C4LogSettings.Enabled)
            {
                Subscribe(nameof(OnExplosiveThrown));
            }

            if (_configData.RocketLogSettings.Enabled)
            {
                Subscribe(nameof(OnRocketLaunched));
            }
        }

        public string StripRustTags(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            foreach (string tag in _tags)
            {
                text = text.Replace(tag, _configData.GlobalSettings.TagsReplacement);
            }

            foreach (Regex regexTag in _regexTags)
            {
                text = regexTag.Replace(text, _configData.GlobalSettings.TagsReplacement);
            }

            return text;
        }

        public string GetGridPosition(Vector3 position) => MapHelper.PositionToString(position);

        public string GetFormattedDurationTime(TimeSpan time, string id = null)
        {
            _sb.Clear();

            if (time.Days > 0)
            {
                BuildTime(_sb, time.Days == 1 ? LangKeys.Format.Day : LangKeys.Format.Days, id, time.Days);
            }

            if (time.Hours > 0)
            {
                BuildTime(_sb, time.Hours == 1 ? LangKeys.Format.Hour : LangKeys.Format.Hours, id, time.Hours);
            }

            if (time.Minutes > 0)
            {
                BuildTime(_sb, time.Minutes == 1 ? LangKeys.Format.Minute : LangKeys.Format.Minutes, id, time.Minutes);
            }

            BuildTime(_sb, time.Seconds == 1 ? LangKeys.Format.Second : LangKeys.Format.Seconds, id, time.Seconds);

            return _sb.ToString();
        }

        public void BuildTime(StringBuilder sb, string lang, string playerID, int value)
        {
            sb.Append(_configData.GlobalSettings.TagsReplacement);
            sb.Append(value);
            sb.Append(_configData.GlobalSettings.TagsReplacement);
            sb.Append(" ");
            sb.Append(Lang(lang, playerID));
            sb.Append(" ");
        }

        public bool IsPluginLoaded(Plugin plugin) => plugin != null && plugin.IsLoaded;

        public void LogToConsole(string text)
        {
            if (_configData.GlobalSettings.LoggingEnabled)
            {
                Puts(text);
            }
        }

        private string GetMonthlyLogFileName(string baseName)
        {
            return $"rServerMessages/{baseName}/{baseName}_{DateTime.Now:yyyy-MM}";
        }

        private void LoadNameChangeLogData()
        {
            try
            {
                _nameChangeLogData = Interface.Oxide.DataFileSystem.ReadObject<PlayerNameChangeLogData>(GetMonthlyLogFileName("PlayerNameChangeLog"));
                if (_nameChangeLogData == null)
                {
                    _nameChangeLogData = new PlayerNameChangeLogData();
                }
            }
            catch
            {
                _nameChangeLogData = new PlayerNameChangeLogData();
            }
        }

        private void SaveNameChangeLogData()
        {
            try
            {
                Interface.Oxide.DataFileSystem.WriteObject(GetMonthlyLogFileName("PlayerNameChangeLog"), _nameChangeLogData);
            }
            catch (Exception ex)
            {
                Puts($"Error saving PlayerNameChangeLog: {ex.Message}");
            }
        }

        private void LoadBedRenameLogData()
        {
            try
            {
                _bedRenameLogData = Interface.Oxide.DataFileSystem.ReadObject<BedRenameLogData>(GetMonthlyLogFileName("BedRenameLog"));
                if (_bedRenameLogData == null)
                {
                    _bedRenameLogData = new BedRenameLogData();
                }
            }
            catch
            {
                _bedRenameLogData = new BedRenameLogData();
            }
        }

        private void SaveBedRenameLogData()
        {
            try
            {
                Interface.Oxide.DataFileSystem.WriteObject(GetMonthlyLogFileName("BedRenameLog"), _bedRenameLogData);
            }
            catch (Exception ex)
            {
                Puts($"Error saving BedRenameLog: {ex.Message}");
            }
        }

        private void LoadC4LogData()
        {
            try
            {
                _c4LogData = Interface.Oxide.DataFileSystem.ReadObject<ExplosiveLogData>(GetMonthlyLogFileName("C4Log"));
                if (_c4LogData == null)
                {
                    _c4LogData = new ExplosiveLogData();
                }
            }
            catch
            {
                _c4LogData = new ExplosiveLogData();
            }
        }

        private void LoadRocketLogData()
        {
            try
            {
                _rocketLogData = Interface.Oxide.DataFileSystem.ReadObject<ExplosiveLogData>(GetMonthlyLogFileName("RocketLog"));
                if (_rocketLogData == null)
                {
                    _rocketLogData = new ExplosiveLogData();
                }
            }
            catch
            {
                _rocketLogData = new ExplosiveLogData();
            }
        }

        #endregion Helpers

        #region Discord Embed

        #region Send Embed Methods
        /// <summary>
        /// Headers when sending an embeded message
        /// </summary>
        private readonly Dictionary<string, string> _headers = new()
        {
            {"Content-Type", "application/json"}
        };

        /// <summary>
        /// Sends the DiscordMessage to the specified webhook url
        /// </summary>
        /// <param name="url">Webhook url</param>
        /// <param name="message">Message being sent</param>
        public void DiscordSendMessage(string url, DiscordMessage message)
        {
            if (string.IsNullOrEmpty(url) || message == null)
            {
                PrintError($"DiscordSendMessage: Invalid parameters - URL: {url}, Message: {message}");
                return;
            }

            webrequest.Enqueue(url, message.ToJson(), DiscordSendMessageCallback, this, RequestMethod.POST, _headers);
        }

        /// <summary>
        /// Callback when sending the embed if any errors occured
        /// </summary>
        /// <param name="code">HTTP response code</param>
        /// <param name="message">Response message</param>
        public void DiscordSendMessageCallback(int code, string message)
        {
            switch (code)
            {
                case 204:
                    _retryCount = 0;
                    _queuedMessage = null;
                    QueueCooldownDisable();
                    return;
                case 401:
                    try
                    {
                        Dictionary<string, object> objectJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(message);
                        int messageCode = 0;
                        if (objectJson?["code"] != null && int.TryParse(objectJson["code"].ToString(), out messageCode))
                        {
                            if (messageCode == 50027)
                            {
                                PrintError($"Invalid Webhook Token: '{_queuedMessage?.WebhookUrl ?? "Unknown"}'");
                                _retryCount = 0; // Don't retry invalid webhooks
                                _queuedMessage = null;
                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        PrintError($"Error parsing 401 response: {ex.Message}");
                    }
                    break;
                case 404:
                    PrintError($"Invalid Webhook (404: Not Found): '{_queuedMessage?.WebhookUrl ?? "Unknown"}'");
                    _retryCount = 0; // Don't retry 404s
                    _queuedMessage = null;
                    return;
                case 405:
                    PrintError($"Invalid Webhook (405: Method Not Allowed): '{_queuedMessage?.WebhookUrl ?? "Unknown"}'");
                    _retryCount = 0; // Don't retry 405s
                    _queuedMessage = null;
                    return;
                case 429:
                    message = "You are being rate limited. To avoid this try to increase queue interval in your config file.";
                    break;
                case 500:
                    message = "There are some issues with Discord server (500 Internal Server Error)";
                    break;
                case 502:
                    message = "There are some issues with Discord server (502 Bad Gateway)";
                    break;
                default:
                    message = $"DiscordSendMessageCallback: code = {code} message = {message}";
                    break;
            }

            _retryCount++;
            PrintError($"Discord webhook error (attempt {_retryCount}/{_maxRetries}): {message}");
            
            // If we've hit max retries, clear the current message
            if (_retryCount > _maxRetries)
            {
                PrintError($"Max retries exceeded for webhook: '{_queuedMessage?.WebhookUrl ?? "Unknown"}'. Discarding message.");
                _queuedMessage = null;
                _retryCount = 0;
            }
        }
        #endregion Send Embed Methods

        #region Embed Classes

        public class DiscordMessage
        {
            /// <summary>
            /// String only content to be sent
            /// </summary>
            [JsonProperty("content")]
            private string Content { get; set; }

            /// <summary>
            /// List of embeds to be sent
            /// </summary>
            [JsonProperty("embeds")]
            private List<DiscordEmbed> Embeds { get; set; }

            public DiscordMessage(string content = null)
            {
                Content = content;
                Embeds = new List<DiscordEmbed>();
            }

            /// <summary>
            /// Adds string content to the message
            /// </summary>
            /// <param name="content"></param>
            /// <returns></returns>
            public DiscordMessage AddContent(string content)
            {
                Content = content;
                return this;
            }

            /// <summary>
            /// Adds an embed to the message
            /// </summary>
            /// <param name="embed"></param>
            /// <returns></returns>
            public DiscordMessage AddEmbed(DiscordEmbed embed)
            {
                Embeds.Add(embed);
                return this;
            }

            /// <summary>
            /// Returns string content of the message
            /// </summary>
            /// <returns></returns>
            public string GetContent()
            {
                return Content;
            }

            /// <summary>
            /// Returns message as JSON to be sent in the web request
            /// </summary>
            /// <returns></returns>
            public string ToJson() => JsonConvert.SerializeObject(this, Formatting.None,
                new JsonSerializerSettings {NullValueHandling = NullValueHandling.Ignore});
        }

        public class DiscordEmbed
        {
            /// <summary>
            /// Title of the embed
            /// </summary>
            [JsonProperty("title")]
            public string Title { get; set; }

            /// <summary>
            /// Description of the embed
            /// </summary>
            [JsonProperty("description")]
            public string Description { get; set; }

            /// <summary>
            /// Color of the embed
            /// </summary>
            [JsonProperty("color")]
            public int Color { get; set; }

            /// <summary>
            /// Timestamp of the embed
            /// </summary>
            [JsonProperty("timestamp")]
            public DateTimeOffset? Timestamp { get; set; }

            /// <summary>
            /// Fields of the embed
            /// </summary>
            [JsonProperty("fields")]
            public List<DiscordEmbedField> Fields { get; set; }

            /// <summary>
            /// Footer of the embed
            /// </summary>
            [JsonProperty("footer")]
            public DiscordEmbedFooter Footer { get; set; }

            /// <summary>
            /// Author of the embed
            /// </summary>
            [JsonProperty("author")]
            public DiscordEmbedAuthor Author { get; set; }

            /// <summary>
            /// Thumbnail of the embed
            /// </summary>
            [JsonProperty("thumbnail")]
            public DiscordEmbedThumbnail Thumbnail { get; set; }

            /// <summary>
            /// Image of the embed
            /// </summary>
            [JsonProperty("image")]
            public DiscordEmbedImage Image { get; set; }

            public DiscordEmbed()
            {
                Fields = new List<DiscordEmbedField>();
            }

            /// <summary>
            /// Sets the title of the embed
            /// </summary>
            /// <param name="title"></param>
            /// <returns></returns>
            public DiscordEmbed SetTitle(string title)
            {
                Title = title;
                return this;
            }

            /// <summary>
            /// Sets the description of the embed
            /// </summary>
            /// <param name="description"></param>
            /// <returns></returns>
            public DiscordEmbed SetDescription(string description)
            {
                Description = description;
                return this;
            }

            /// <summary>
            /// Sets the color of the embed
            /// </summary>
            /// <param name="color"></param>
            /// <returns></returns>
            public DiscordEmbed SetColor(int color)
            {
                Color = color;
                return this;
            }

            /// <summary>
            /// Sets the timestamp of the embed
            /// </summary>
            /// <param name="timestamp"></param>
            /// <returns></returns>
            public DiscordEmbed SetTimestamp(DateTimeOffset timestamp)
            {
                Timestamp = timestamp;
                return this;
            }

            /// <summary>
            /// Adds a field to the embed
            /// </summary>
            /// <param name="name"></param>
            /// <param name="value"></param>
            /// <param name="inline"></param>
            /// <returns></returns>
            public DiscordEmbed AddField(string name, string value, bool inline = false)
            {
                Fields.Add(new DiscordEmbedField
                {
                    Name = name,
                    Value = value,
                    Inline = inline
                });
                return this;
            }

            /// <summary>
            /// Sets the footer of the embed
            /// </summary>
            /// <param name="text"></param>
            /// <param name="iconUrl"></param>
            /// <returns></returns>
            public DiscordEmbed SetFooter(string text, string iconUrl = null)
            {
                Footer = new DiscordEmbedFooter
                {
                    Text = text,
                    IconUrl = iconUrl
                };
                return this;
            }

            /// <summary>
            /// Sets the author of the embed
            /// </summary>
            /// <param name="name"></param>
            /// <param name="url"></param>
            /// <param name="iconUrl"></param>
            /// <returns></returns>
            public DiscordEmbed SetAuthor(string name, string url = null, string iconUrl = null)
            {
                Author = new DiscordEmbedAuthor
                {
                    Name = name,
                    Url = url,
                    IconUrl = iconUrl
                };
                return this;
            }

            /// <summary>
            /// Sets the thumbnail of the embed
            /// </summary>
            /// <param name="url"></param>
            /// <returns></returns>
            public DiscordEmbed SetThumbnail(string url)
            {
                Thumbnail = new DiscordEmbedThumbnail
                {
                    Url = url
                };
                return this;
            }

            /// <summary>
            /// Sets the image of the embed
            /// </summary>
            /// <param name="url"></param>
            /// <returns></returns>
            public DiscordEmbed SetImage(string url)
            {
                Image = new DiscordEmbedImage
                {
                    Url = url
                };
                return this;
            }
        }

        public class DiscordEmbedField
        {
            /// <summary>
            /// Name of the field
            /// </summary>
            [JsonProperty("name")]
            public string Name { get; set; }

            /// <summary>
            /// Value of the field
            /// </summary>
            [JsonProperty("value")]
            public string Value { get; set; }

            /// <summary>
            /// Whether the field should be inline
            /// </summary>
            [JsonProperty("inline")]
            public bool Inline { get; set; }
        }

        public class DiscordEmbedFooter
        {
            /// <summary>
            /// Text of the footer
            /// </summary>
            [JsonProperty("text")]
            public string Text { get; set; }

            /// <summary>
            /// Icon URL of the footer
            /// </summary>
            [JsonProperty("icon_url")]
            public string IconUrl { get; set; }
        }

        public class DiscordEmbedAuthor
        {
            /// <summary>
            /// Name of the author
            /// </summary>
            [JsonProperty("name")]
            public string Name { get; set; }

            /// <summary>
            /// URL of the author
            /// </summary>
            [JsonProperty("url")]
            public string Url { get; set; }

            /// <summary>
            /// Icon URL of the author
            /// </summary>
            [JsonProperty("icon_url")]
            public string IconUrl { get; set; }
        }

        public class DiscordEmbedThumbnail
        {
            /// <summary>
            /// URL of the thumbnail
            /// </summary>
            [JsonProperty("url")]
            public string Url { get; set; }
        }

        public class DiscordEmbedImage
        {
            /// <summary>
            /// URL of the image
            /// </summary>
            [JsonProperty("url")]
            public string Url { get; set; }
        }
        #endregion Embed Classes

        #endregion Discord Embed
    }
}