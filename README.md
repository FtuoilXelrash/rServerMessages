# rServerMessages

![Rust](https://img.shields.io/badge/Game-Rust-orange)
![Umod](https://img.shields.io/badge/Framework-Umod-blue)
![Version](https://img.shields.io/badge/Version-0.0.236-green)
![License](https://img.shields.io/badge/License-MIT-yellow)

A comprehensive Umod plugin for Rust servers that logs essential server events to Discord channels using webhooks with advanced embed support, smart queue management, and extensive event tracking.

<div align="center">
  <img src="plugin-01.jpg" alt="rServerMessages Plugin Screenshot" width="600">
  <p><em>Rich Discord embeds showing server events with detailed information and visual styling</em></p>
</div>

## 🚀 Features

### 📊 Comprehensive Event Tracking
- **Player Events** - Connections, disconnections, deaths with detailed analytics
- **Chat Monitoring** - Global and team chat with enhanced formatting
- **Admin Actions** - RCON commands, bans, kicks, mutes with security tracking
- **Server Events** - Startup, shutdown, and performance monitoring
- **Premium Plugin Events** - Support for 15+ popular Rust plugins

### 💀 Advanced Death System
- **Enhanced Death Messages** with rich embeds and combat analytics
- **PvP Combat Details** - Weapon, damage, distance, and kill classification
- **PvE Death Tracking** - Animals, NPCs, environmental hazards
- **Drowning Detection** - Specialized handling for water deaths
- **Death Coordinates** - Grid positions and teleport commands
- **High Damage Indicators** - Special highlighting for devastating hits
- **Distance Categories** - Point blank, close quarters, long range, sniper kills

### 🔐 Security & Administration
- **RCON Command Monitoring** with IP tracking and country detection
- **User Management** - Bans, kicks, unbans with detailed logging
- **Permission Tracking** - Group and permission changes
- **Steam Profile Integration** - Account age, profile status, last activity
- **Geographic Information** - Country detection with flag emojis
- **Admin Connection Filtering** - Hide admin activities when configured

### 💬 Advanced Chat Features
- **Team Chat Enhancement** - Team member status, locations, and structure
- **Chat Filtering** - Integration with AntiSpam, UFilter, and BetterChatMute
- **Rich Embeds** - Enhanced team chat with player details and teleport commands
- **Profanity Filtering** - Automatic content moderation support

### 🎮 Premium Plugin Integration
- **Air Events** - Helicopter and air drop monitoring
- **Airfield Events** - Military base activities
- **Arctic Base Events** - Cold environment challenges
- **Armored Train Events** - Train heist tracking
- **Gas Station Events** - Fuel station activities
- **Supermarket Events** - Shopping center events
- **Sputnik Events** - Satellite activities
- **Raidable Bases** - Base raids with difficulty tracking and completion status
- **Holiday Events** - Christmas, Easter, Halloween with winner tracking
- **Meteor Showers** - Space event detection

### 🚀 Smart Queue Management
- **Rate Limiting Protection** - Prevents Discord API abuse
- **Message Batching** - Combines multiple messages for efficiency
- **Retry System** - Automatic retry with exponential backoff
- **Error Recovery** - Graceful handling of network issues
- **Queue Cooldown** - Intelligent delay system for connection problems

### 🎨 Rich Discord Integration
- **Advanced Embeds** - Color-coded, structured messages with timestamps
- **Webhook Management** - Multiple webhook support for different channels
- **Message Editing** - Single updating messages to prevent spam
- **Custom Avatars** - Bot customization with server branding
- **Field Organization** - Structured information display
- **Thumbnail Support** - Visual enhancement for better readability

## 📋 Requirements

- **Rust Dedicated Server**
- **Umod (Oxide)** framework
- **Discord webhook(s)** for message delivery
- **Optional:** Steam Web API key for enhanced player data
- **Optional:** Supported premium plugins for event integration

## 🔧 Installation

1. Download the [latest release](https://github.com/FtuoilXelrash/rServerMessages/releases)
2. Copy `rServerMessages.cs` to your server's `oxide/plugins/` directory
3. The plugin will auto-generate its configuration file on first load
4. Configure your Discord webhooks in `oxide/config/rServerMessages.json`
5. Reload the plugin or restart your server

## ⚙️ Complete Configuration Reference

The plugin creates a comprehensive configuration file at `oxide/config/rServerMessages.json`:

### Global Settings
```json
{
  "Global settings": {
    "Log to console?": false,
    "Use AntiSpam plugin on chat messages": false,
    "Use UFilter plugin on chat messages": false,
    "Hide admin connect/disconnect messages": false,
    "Hide NPC death messages": false,
    "Include death coordinates in death messages": true,
    "Use Discord Embeds for death messages": true,
    "Use enhanced embeds for connections": true,
    "Show country information (requires internet)": true,
    "Show server population in connection messages": true,
    "Show combat details in death messages": true,
    "Use enhanced embeds for server messages": true,
    "Show kill distance in PvP deaths": true,
    "High damage threshold for special kills": 75.0,
    "Use enhanced embeds for RCON messages": true,
    "Replacement string for tags": "`",
    "Queue interval (1 message per ? seconds)": 1.0,
    "Queue cooldown if connection error (seconds)": 60.0,
    "Public Chat Webhook URL": "",
    "Private Admin Webhook URL": "",
    "Server Messages Webhook URL": "",
    "Meteor shower message keywords (comma separated)": "meteor,shower,incoming",
    "RCON command blacklist": [
      "serverinfo",
      "server.hostname",
      "server.headerimage",
      "server.description",
      "server.url",
      "playerlist",
      "status"
    ],
    "Steam Web API Key (for profile data)": ""
  }
}
```

### Discord Webhook Configuration
| Setting | Description | Recommended Use |
|---------|-------------|-----------------|
| `Public Chat Webhook URL` | Webhook for general chat messages | Public channel for community chat |
| `Private Admin Webhook URL` | Webhook for administrative events | Private admin channel for security events |
| `Server Messages Webhook URL` | Webhook for server events and plugins | Public or private channel for server updates |

### Event Categories
All event categories can be individually enabled/disabled:

#### Player Events
- **Player death settings** - Combat and death tracking
- **Player connect advanced info settings** - Enhanced connection details
- **Player disconnect settings** - Disconnection monitoring

#### Chat & Communication
- **Chat settings** - Global and local chat monitoring
- **Chat (Team) settings** - Team communication tracking

#### Administrative Events
- **User Banned settings** - Ban/unban notifications
- **User Kicked settings** - Kick notifications
- **User Muted settings** - Mute/unmute tracking
- **User Name Updated settings** - Name change monitoring
- **Permissions settings** - Permission and group changes

#### Server Management
- **Server state settings** - Startup/shutdown notifications
- **Server messages settings** - General server announcements
- **Rcon command settings** - RCON command logging
- **Rcon connection settings** - RCON access monitoring

#### Premium Plugin Events
- **Air Event settings** - Helicopter events
- **Airfield Event settings** - Military base activities
- **Arctic Base Event settings** - Cold environment events
- **Armored Train Event settings** - Train heist tracking
- **Gas Station Event settings** - Fuel station activities
- **Supermarket Event settings** - Shopping events
- **Sputnik Event settings** - Satellite activities
- **Raidable Bases settings** - Base raid tracking

#### Holiday Events
- **Christmas settings** - Christmas event tracking
- **Easter settings** - Easter hunt monitoring
- **Halloween settings** - Halloween event tracking
- **SantaSleigh settings** - Santa sleigh events

#### Special Events
- **Meteor Shower settings** - Space event detection

## 🎮 Advanced Features

### Enhanced Death System

#### Combat Analytics
The plugin provides detailed combat information including:

**Distance Categories:**
- Point Blank (< 3m) 💥
- Close Quarters (3-15m)
- Medium Range (15-50m)
- Long Range (50-100m)
- Sniper Range (100-200m) 🏹
- Extreme Range (200m+)

**Special Kill Indicators:**
- High Damage Hits (configurable threshold) 🎯
- Long Range Snipes 🏹
- Point Blank Eliminations 💥

**Death Types:**
- PvP (Player vs Player) ⚔️
- PvE (Player vs Environment) 🐻
- Suicide 🔫
- Drowning 🌊
- Environmental 💀

#### Entity Recognition
The plugin recognizes 50+ entity types including:

**NPCs & Animals:**
- Scientists, Heavy Scientists, Murderers
- Bears, Wolves, Boars, Sharks
- Tunnel Dwellers, Underwater Dwellers
- Zombies, Gingerbread NPCs

**Military & Vehicles:**
- Patrol Helicopters, Bradley APC
- Attack Helicopters, Minicopters
- Scrap Transport Helicopters

**Defensive Structures:**
- Auto Turrets, Shotgun Traps
- Flame Turrets, SAM Sites
- Tesla Coils, Bear Traps, Landmines

### Steam Profile Integration

When a Steam Web API key is configured, the plugin provides:

**Account Information:**
- Account age with new account warnings ⚠️
- Profile visibility status (Public/Private)
- Profile configuration status
- Last activity tracking

**Security Features:**
- New account detection (< 30 days)
- Profile status validation
- Recent activity monitoring

### Geographic Information

**IP Geolocation:**
- Country detection with flag emojis 🌍
- Automatic timezone consideration
- Privacy-focused implementation

### Team Chat Enhancement

**Team Information Display:**
- Team member count and status
- Online/Sleeping/Offline indicators 🟢💤🔴
- Member locations with grid coordinates
- Teleport commands for admin convenience

**Status Indicators:**
- 🟢 Online - Active players
- 💤 Sleeping - Sleeping players
- 🔴 Offline - Disconnected players

### RCON Security Monitoring

**Command Categorization:**
- 🔧 Server Configuration
- ⚠️ Admin Actions (Critical)
- 📊 Information Queries
- 🎮 Game Control Commands
- ⚡ Other Commands

**Security Features:**
- IP address tracking with geolocation
- Critical action highlighting
- Command blacklist support
- Access pattern monitoring

## 📱 Discord Setup Guide

### Step 1: Create Discord Webhooks

**For Public Chat:**
1. Go to your public Discord channel
2. Right-click → "Edit Channel"
3. Navigate to "Integrations" → "Webhooks"
4. Click "New Webhook"
5. Name: "Rust Server Chat"
6. Copy webhook URL

**For Admin Events:**
1. Go to your private admin Discord channel
2. Follow same steps as above
3. Name: "Rust Server Admin"
4. Copy webhook URL

**For Server Events:**
1. Go to your server updates Discord channel
2. Follow same steps as above
3. Name: "Rust Server Events"
4. Copy webhook URL

### Step 2: Configure Plugin
```json
{
  "Global settings": {
    "Public Chat Webhook URL": "https://discord.com/api/webhooks/YOUR_PUBLIC_WEBHOOK_ID/YOUR_PUBLIC_TOKEN",
    "Private Admin Webhook URL": "https://discord.com/api/webhooks/YOUR_ADMIN_WEBHOOK_ID/YOUR_ADMIN_TOKEN",
    "Server Messages Webhook URL": "https://discord.com/api/webhooks/YOUR_SERVER_WEBHOOK_ID/YOUR_SERVER_TOKEN",
    "Steam Web API Key (for profile data)": "YOUR_STEAM_API_KEY_HERE"
  }
}
```

### Step 3: Enable Events
Enable the events you want to monitor:
```json
{
  "Player death settings": {
    "Enabled?": true
  },
  "Chat settings": {
    "Enabled?": true
  },
  "User Banned settings": {
    "Enabled?": true
  }
}
```

## 🔑 Steam Web API Key Setup

### Obtaining Your API Key
1. Visit [Steam Web API Key Registration](https://steamcommunity.com/dev/apikey)
2. Log in with your Steam account
3. Enter your domain name (can be localhost for development)
4. Copy the generated 32-character API key
5. Add it to your configuration

### Benefits of API Key
- **Account Age Detection** - Identify new/suspicious accounts
- **Profile Status** - Public vs private profiles
- **Activity Tracking** - Recent login information
- **Enhanced Security** - Better player vetting

## 🎨 Discord Message Examples

### Death Message Example
```
⚔️ Player Eliminated

💀 Elimination
Killer: PlayerKiller
Victim: PlayerVictim

🔫 Combat Stats
Weapon: AK-47
Damage: 87.5 🎯
Range: 45.2m (Medium Range)
🎯 High Damage Hit!

📍 Death Location
Grid: H14
Position: 234.5, 15.2, -156.8
🚁 Quick Teleport: teleportpos 234.5 15.2 -156.8
```

### Connection Message Example
```
🔗 Player Connected

👤 Player Details
Name: NewPlayer
Steam ID: 76561198000000000
IP Address: 192.168.1.100
Account Age: 2 years 3 months
Profile: Public
Location: 🇺🇸 United States
```

### Team Chat Example
```
👥 Team Chat

👤 Player Info
Player: TeamPlayer
Steam ID: 76561198000000000
Location: G15
Teleport: teleportpos 123.4 56.7 -890.1

💬 Message
Need backup at my base, under attack!

🛡️ Team Information
Team ID: 1234567890
Team Size: 4
Team Members:
• TeamLeader - 🟢 Online
• TeamPlayer - 🟢 Online
• TeamMate1 - 💤 Sleeping
• TeamMate2 - 🔴 Offline
Status Summary: 2 Online, 1 Sleeping, 1 Offline
```

### RCON Command Example
```
🛰️ RCON Command - Admin Action

💻 Command Executed
kick "BadPlayer" "Cheating"

🌐 Source IP
192.168.1.50

⚠️ Severity
Critical Admin Action

📍 Location
🇺🇸 United States
```

## 🔄 Queue Management System

### Smart Message Batching
- **Combines similar messages** to reduce Discord API calls
- **Prevents rate limiting** with intelligent delays
- **Maintains message order** for chronological accuracy

### Error Recovery
- **Automatic retries** with exponential backoff (max 5 attempts)
- **Queue cooldown** during connection issues
- **Graceful degradation** when Discord is unavailable

### Rate Limiting Protection
```json
{
  "Queue interval (1 message per ? seconds)": 1.0,
  "Queue cooldown if connection error (seconds)": 60.0
}
```

## 🔧 Customization Options

### Message Filtering
- **Admin Activity Hiding** - Hide admin connections/disconnections
- **NPC Death Filtering** - Hide non-player entity deaths
- **Chat Integration** - Filter spam and profanity
- **Zero Value Hiding** - Cleaner displays

### Visual Customization
- **Embed Colors** - Different colors for event types
- **Custom Icons** - Emoji and icon customization
- **Field Organization** - Structured information display
- **Timestamp Formatting** - Consistent time display

### Performance Tuning
For **High Population Servers** (100+ players):
```json
{
  "Queue interval (1 message per ? seconds)": 2.0,
  "Queue cooldown if connection error (seconds)": 120.0,
  "High damage threshold for special kills": 100.0
}
```

For **Low Population Servers** (< 50 players):
```json
{
  "Queue interval (1 message per ? seconds)": 0.5,
  "Queue cooldown if connection error (seconds)": 30.0,
  "High damage threshold for special kills": 50.0
}
```

## 🐛 Troubleshooting

### Discord Integration Issues

**Messages Not Sending:**
1. Verify webhook URLs are valid
2. Check webhook permissions in Discord
3. Ensure events are enabled in configuration
4. Monitor console for error messages

**Rate Limiting:**
```
Error: You are being rate limited
Solution: Increase "Queue interval" in configuration
```

**Invalid Webhook:**
```
Error: Invalid Webhook (404: Not Found)
Solution: Regenerate webhook URL in Discord
```

### Steam API Issues

**Invalid API Key:**
```
Warning: Steam Web API Key appears to be invalid
Solution: Verify 32-character API key from Steam
```

**Profile Data Missing:**
- Ensure API key is configured
- Check internet connectivity
- Verify Steam API service status

### Plugin Conflicts

**Chat Plugin Conflicts:**
- Configure AntiSpam integration settings
- Adjust UFilter and BetterChatMute compatibility
- Check plugin loading order

**Performance Issues:**
- Reduce queue interval frequency
- Disable unused event categories
- Monitor server console for errors

### Configuration Problems

**Missing Event Categories:**
```bash
# Reload plugin to regenerate missing settings
oxide.reload rServerMessages
```

**Invalid JSON:**
- Use online JSON validator
- Check for syntax errors
- Verify quotation marks and commas

## 📈 Performance Impact

### Resource Usage
- **CPU Impact:** < 0.5% on modern hardware
- **Memory Footprint:** ~3-8 MB RAM usage
- **Network Overhead:** Optimized Discord API usage
- **Disk I/O:** Minimal configuration file access

### Optimization Features
- **Event Filtering** - Only process enabled events
- **Message Batching** - Reduce API calls
- **Smart Caching** - Efficient data reuse
- **Graceful Degradation** - Handle errors without crashes

## 🔗 Premium Plugin Compatibility

### Verified Compatible Plugins
- **Air Event** - Helicopter and airdrop events
- **Airfield Event** - Military base activities
- **Arctic Base Event** - Cold environment challenges
- **Armored Train Event** - Train heist tracking
- **Gas Station Event** - Fuel station activities
- **Supermarket Event** - Shopping center events
- **Sputnik Event** - Satellite activities
- **Raidable Bases** - Base raid system with difficulty tracking
- **BetterChatMute** - Advanced chat moderation
- **AntiSpam** - Chat spam prevention
- **UFilter** - Profanity filtering

### Plugin Integration Benefits
- **Automatic Detection** - Events are captured automatically
- **Rich Information** - Detailed event data and context
- **Difficulty Tracking** - Easy/Medium/Hard/Expert/Nightmare ratings
- **Winner Announcements** - Holiday event completion tracking
- **Location Data** - Grid coordinates for events

## 🤝 Contributing

We welcome contributions! Here's how to get started:

1. **Fork the Repository**
   ```bash
   git clone https://github.com/FtuoilXelrash/rServerMessages.git
   ```

2. **Create Feature Branch**
   ```bash
   git checkout -b feature/your-feature-name
   ```

3. **Follow Coding Standards**
   - Use [Umod Approval Guidelines](https://umod.org/documentation/api/approval-guide)
   - Add comprehensive error handling
   - Include configuration validation
   - Test with multiple plugins

4. **Submit Pull Request**
   - Include detailed description
   - Test all event types
   - Update documentation if needed

### Development Guidelines
- **Event Hook Testing** - Verify all event categories
- **Discord Integration** - Test webhook functionality
- **Queue Management** - Verify message batching
- **Error Handling** - Test network failure scenarios

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 👨‍💻 Author

**Ftuoil Xelrash**
- GitHub: [@FtuoilXelrash](https://github.com/FtuoilXelrash)
- **[Discord Plugin Support](https://discord.gg/G8mfZH2TMp)** - Author Community Support

## 🆘 Support

### Getting Help
1. **Check Configuration:** Verify all webhook URLs and settings
2. **Console Output:** Monitor for error messages and warnings
3. **Event Testing:** Enable individual event categories for testing
4. **Discord Permissions:** Ensure webhook has message sending permissions

### Issue Template
When reporting bugs, please include:
```
**Plugin Version:** 0.0.236
**Umod Version:** [Your Version]
**Server Population:** [Typical player count]
**Event Category:** [Which events are affected]
**Error Message:** [Full console output]
**Configuration:** [Relevant webhook and event settings]
**Premium Plugins:** [List any premium plugins installed]
**Steps to Reproduce:** [Detailed steps]
```

## 🔗 Links

- **[Download Latest Release](https://github.com/FtuoilXelrash/rServerMessages/releases)** - Always get the newest version
- **[Report Issues](https://github.com/FtuoilXelrash/rServerMessages/issues)** - Bug reports and feature requests
- **[Steam Web API Key](https://steamcommunity.com/dev/apikey)** - Enhanced player data integration

## 🏆 Recognition

Special thanks to:
- **Umod Team** - For the excellent modding framework
- **Rust Community** - For extensive testing and feedback
- **Premium Plugin Developers** - For event integration support
- **Discord Community** - For webhook testing and validation

---

## 📊 Statistics

![GitHub Downloads](https://img.shields.io/github/downloads/FtuoilXelrash/rServerMessages/total)
![GitHub Stars](https://img.shields.io/github/stars/FtuoilXelrash/rServerMessages)
![GitHub Issues](https://img.shields.io/github/issues/FtuoilXelrash/rServerMessages)
![GitHub License](https://img.shields.io/github/license/FtuoilXelrash/rServerMessages)

⭐ **Star this repository if you find it useful!** ⭐